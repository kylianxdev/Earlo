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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Never crash the tray app on a UI glitch, but keep a record for bug reports.
        DispatcherUnhandledException += (_, ex) =>
        {
            CrashLog.Write(ex.Exception);
            ex.Handled = true;
        };

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

        _instanceMutex = new Mutex(true, InstanceName, out bool firstInstance);
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
        AutoStart.Apply(_settings.StartWithWindows);

        _tray = new TrayIcon();
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

        int logIndex = Array.IndexOf(e.Args, "--log");
        if (logIndex >= 0)
            DiagnosticsLog.Attach(_scanner, e.Args.ElementAtOrDefault(logIndex + 1) ?? "podpilot-log.txt");

        if (e.Args.Contains("--demo"))
            DemoMode.Start(_tracker, Dispatcher);

        if (!_settings.FirstRunDone || !e.Args.Contains("--minimized"))
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
                AutoStart.Apply(_settings.StartWithWindows);
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
