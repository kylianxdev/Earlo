using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using PodPilot.App.Controls;
using PodPilot.App.Services;
using PodPilot.Core.Aap;
using PodPilot.Core.Bluetooth;
using PodPilot.Core.Media;
using PodPilot.Core.Tracking;

namespace PodPilot.App;

public partial class App : Application
{
    const string InstanceName = "PodPilot.SingleInstance.8c2f61";
    const string ActivateEventName = "PodPilot.Activate.8c2f61";

    readonly PodsTracker _tracker = new();
    readonly AdvertisementScanner _scanner = new();
    readonly PairedDeviceMonitor _paired = new();
    readonly MediaController _media = new();
    readonly AapDeviceManager _aap = new();
    readonly CaseBatteryMemory _caseMemory = new();

    Mutex? _instanceMutex;
    EventWaitHandle? _activateEvent;
    AppSettings _settings = new();
    EarDetectionService? _earDetection;
    LowBatteryNotifier? _lowBattery;
    TrayIcon? _tray;
    PopupWindow? _popup;
    SettingsWindow? _settingsWindow;
    DispatcherTimer? _tick;
    DateTime _connectPopupUntil;
    DispatcherTimer? _updateTimer;
    bool _updating;
    bool _driverInstalled;
    bool _driverBusy;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Never crash the tray app on a UI glitch, but keep a record for bug reports.
        DispatcherUnhandledException += (_, ex) =>
        {
            CrashLog.Write(ex.Exception);
            ex.Handled = true;
        };

        // Elevated helper started by DriverInstaller: do the job and exit, no tray app.
        if (DriverInstaller.IsElevatedCommand(e.Args.FirstOrDefault()))
        {
            Shutdown(DriverInstaller.RunElevatedCommand(e.Args[0]));
            return;
        }

        int previewIndex = Array.IndexOf(e.Args, "--render-preview");
        if (previewIndex >= 0)
        {
            Theme.Apply(this);
            try
            {
                PreviewRenderer.Render(e.Args.ElementAtOrDefault(previewIndex + 1) ?? "preview");
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
            Shutdown();
            return;
        }

        // After an update the old version is still closing, so wait for it to let go.
        bool updated = e.Args.Contains(Updater.UpdatedArgument);
        _instanceMutex = new Mutex(false, InstanceName);
        bool firstInstance;
        try
        {
            firstInstance = _instanceMutex.WaitOne(updated ? TimeSpan.FromSeconds(15) : TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            firstInstance = true;
        }
        if (!firstInstance)
        {
            // Already running: ask the running instance to show its window.
            try { EventWaitHandle.OpenExisting(ActivateEventName).Set(); } catch (WaitHandleCannotBeOpenedException) { }
            Shutdown();
            return;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        ThreadPool.RegisterWaitForSingleObject(_activateEvent, (_, _) => Dispatcher.BeginInvoke(ShowSettings), null, -1, false);

        _settings = AppSettings.Load();
        Theme.Initialize(this);
        _ = AutoStart.ApplyAsync(_settings.StartWithWindows);

        _tray = new TrayIcon(_settings);
        _tray.ShowPopup += (_, _) => TogglePopup();
        _tray.OpenSettings += (_, _) => ShowSettings();
        _tray.ExitRequested += (_, _) => Shutdown();
        Theme.Changed += (_, _) => { _tray.Invalidate(); Refresh(); };

        _earDetection = new EarDetectionService(_settings, _media);
        _lowBattery = new LowBatteryNotifier(_settings, (title, text) => _tray.Notify(title, text));
        _settings.PropertyChanged += (_, args) => OnSettingChanged(args.PropertyName);

        _tracker.RssiThreshold = _settings.RssiThreshold;
        _tracker.Updated += (_, snapshot) => OnUpdated(snapshot);
        _tracker.Lost += (_, _) => Refresh();
        _tracker.LidOpened += (_, snapshot) =>
        {
            if (_settings.PopupOnLidOpen)
                ShowPopup();
        };
        _tracker.EarStatusChanged += async (_, args) =>
            await _earDetection.OnEarStatusChangedAsync(args, _paired.ConnectedDevice is not null);

        _scanner.Received += (_, a) => Dispatcher.BeginInvoke(() => _tracker.Process(a.Message, a.Rssi));
        _scanner.StatusChanged += (_, _) => Dispatcher.BeginInvoke(Refresh);
        _paired.ConnectionChanged += (_, device) => Dispatcher.BeginInvoke(() => OnPairedConnectionChanged(device));
        _paired.DevicesChanged += (_, _) => Dispatcher.BeginInvoke(Refresh);

        _paired.Start();
        _aap.Changed += (_, _) => Dispatcher.BeginInvoke(Refresh);
        _aap.Start();
        _ = _scanner.EnsureRunningAsync();

        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _tick.Tick += (_, _) =>
        {
            _tracker.Expire();
            _ = _scanner.EnsureRunningAsync();
        };
        _tick.Start();

        Updater.CleanUp();
        if (updated)
            _tray.Notify("PodPilot updated", $"You now have PodPilot {Updater.CurrentVersion.ToString(3)}.");
        if (Updater.Supported)
        {
            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _updateTimer.Tick += async (_, _) =>
            {
                _updateTimer.Interval = TimeSpan.FromHours(6);
                await CheckForUpdatesAsync(manual: false);
            };
            _updateTimer.Start();
        }

        int logIndex = Array.IndexOf(e.Args, "--log");
        if (logIndex >= 0)
            DiagnosticsLog.Attach(_scanner, e.Args.ElementAtOrDefault(logIndex + 1) ?? "podpilot-log.txt");

        if (e.Args.Contains("--demo"))
            DemoMode.Start(_tracker, Dispatcher);

        bool startHidden = e.Args.Contains("--minimized") || PackageInfo.StartedByStartupTask();
        if (!_settings.FirstRunDone || !startHidden)
        {
            _settings.FirstRunDone = true;
            ShowSettings();
        }
        Refresh();
    }

    DeviceViewState CurrentState() =>
        new(_tracker.Current is { } snapshot ? _caseMemory.Apply(snapshot) : null,
            _paired.ConnectedDevice, _scanner.Status, _settings.LowBatteryThreshold, _aap.Current?.State);

    void OnUpdated(PodsSnapshot snapshot)
    {
        if (DateTime.UtcNow < _connectPopupUntil)
        {
            _connectPopupUntil = DateTime.MinValue;
            ShowPopup();
        }

        _lowBattery?.Check(snapshot, _paired.ConnectedDevice?.Name ?? snapshot.Device.Name);
        Refresh();
    }

    /// <summary>Looks for a newer GitHub release and installs it (automatically, or when asked).</summary>
    async System.Threading.Tasks.Task CheckForUpdatesAsync(bool manual)
    {
        if (!Updater.Supported || _updating)
            return;
        _updating = true;
        _settingsWindow?.SetUpdateStatus("Checking...", busy: true);
        try
        {
            var update = await Updater.CheckAsync();
            if (update is null)
            {
                _settingsWindow?.SetUpdateStatus(manual ? "PodPilot is up to date." : string.Empty);
                return;
            }

            if (!manual && !_settings.AutoUpdate)
            {
                _settingsWindow?.SetUpdateStatus($"Version {update.Version.ToString(3)} is available.");
                _tray?.Notify("PodPilot update available", $"Version {update.Version.ToString(3)} is ready. Open Settings to install it.");
                return;
            }

            _settingsWindow?.SetUpdateStatus($"Installing {update.Version.ToString(3)}...", busy: true);
            if (await Updater.InstallAsync(update))
            {
                Shutdown(); // the new version is already starting
                return;
            }
            _settingsWindow?.SetUpdateStatus("The update could not be installed. Download it from GitHub.");
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or System.Threading.Tasks.TaskCanceledException or System.Text.Json.JsonException)
        {
            // Offline or GitHub unreachable: try again later.
            _settingsWindow?.SetUpdateStatus(manual ? "Could not reach GitHub." : string.Empty);
        }
        finally
        {
            _updating = false;
        }
    }

    async System.Threading.Tasks.Task RefreshDriverStateAsync()
    {
        _driverInstalled = await System.Threading.Tasks.Task.Run(DriverInstaller.IsInstalled);
        _settingsWindow?.UpdateDriver(_driverInstalled, DriverInstaller.EmbeddedPackage, _driverBusy);
    }

    async System.Threading.Tasks.Task ToggleDriverAsync()
    {
        if (_driverBusy)
            return;
        _driverBusy = true;
        _settingsWindow?.UpdateDriver(_driverInstalled, DriverInstaller.EmbeddedPackage, busy: true);

        bool removing = _driverInstalled;
        bool ok = removing ? await DriverInstaller.UninstallAsync() : await DriverInstaller.InstallAsync();
        _driverBusy = false;
        await RefreshDriverStateAsync();

        if (ok)
        {
            _aap.RetryAll();
            _tray?.Notify("PodPilot", removing ? "The noise control driver was removed." : "The noise control driver is installed.");
        }
        else
        {
            _tray?.Notify("PodPilot", removing ? "The driver could not be removed." : "The driver was not installed.");
        }
    }

    void SetNoiseControl(NoiseControlMode mode) => _ = SendAap(s => s.SetNoiseControlAsync(mode));

    async System.Threading.Tasks.Task SendAap(Func<AapSession, System.Threading.Tasks.Task> action)
    {
        if (_aap.Current is not { } session)
            return;
        try
        {
            await action(session);
        }
        catch (Exception)
        {
            // The channel dropped; the manager reconnects and the UI refreshes on its own.
        }
    }

    void OnPairedConnectionChanged(PairedDeviceInfo device)
    {
        if (device.IsConnected)
            _aap.RetryAll();
        Refresh();
        if (!device.IsConnected || !_settings.PopupOnConnect)
            return;

        // Wait for the first advertisement after connecting so the popup has battery levels.
        if (_tracker.Current is not null)
            ShowPopup();
        else
            _connectPopupUntil = DateTime.UtcNow.AddSeconds(15);
    }

    void OnSettingChanged(string? name)
    {
        switch (name)
        {
            case nameof(AppSettings.StartWithWindows):
                _ = AutoStart.ApplyAsync(_settings.StartWithWindows);
                break;
            case nameof(AppSettings.RssiThreshold):
                _tracker.RssiThreshold = _settings.RssiThreshold;
                break;
        }
        Refresh();
    }

    void Refresh()
    {
        var state = CurrentState();
        _tray?.Update(state.Snapshot, state.Connected?.Name, _settings.TrayShowsBattery);
        if (_popup is { IsVisible: true })
            _popup.Update(state);
        _settingsWindow?.Update(state, _paired.Devices, _aap.DriverPresent);
    }

    void ShowPopup()
    {
        if (_popup is null)
        {
            _popup = new PopupWindow();
            _popup.SettingsRequested += (_, _) => ShowSettings();
            _popup.NoiseControlRequested += (_, mode) => SetNoiseControl(mode);
        }
        _popup.Duration = TimeSpan.FromSeconds(_settings.PopupSeconds);
        _popup.ShowAnimated(CurrentState());
    }

    void TogglePopup()
    {
        if (_popup is { IsVisible: true })
            _popup.SlideOut();
        else
            ShowPopup();
    }

    void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.ShowPopupRequested += (_, _) => ShowPopup();
            _settingsWindow.NoiseControlRequested += (_, mode) => SetNoiseControl(mode);
            _settingsWindow.ConversationAwarenessRequested += (_, on) => _ = SendAap(s => s.SetConversationAwarenessAsync(on));
            _settingsWindow.AdaptiveStrengthRequested += (_, level) => _ = SendAap(s => s.SetAdaptiveStrengthAsync(level));
            _settingsWindow.DriverButtonClicked += async (_, _) => await ToggleDriverAsync();
            _settingsWindow.CheckUpdatesRequested += async (_, _) => await CheckForUpdatesAsync(manual: true);
            _ = RefreshDriverStateAsync();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Update(CurrentState(), _paired.Devices, _aap.DriverPresent);
            _settingsWindow.Show();
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
            _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tick?.Stop();
        _tray?.Dispose();
        _scanner.Dispose();
        _aap.Dispose();
        _paired.Dispose();
        _activateEvent?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
