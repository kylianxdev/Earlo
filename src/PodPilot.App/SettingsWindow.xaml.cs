using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Navigation;
using PodPilot.App.Controls;
using PodPilot.App.Services;
using PodPilot.Core.Aap;
using PodPilot.Core.Bluetooth;
using PodPilot.Core.Devices;

namespace PodPilot.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        DataContext = settings;
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"PodPilot {version?.ToString(3)}";
        AutoUpdateCheck.Visibility = Updater.Supported ? Visibility.Visible : Visibility.Collapsed;
        SetUpdateStatus(PackageInfo.IsPackaged ? "Updates are installed by the Microsoft Store."
            : Updater.Supported ? string.Empty : "Development build: no automatic updates.");
        Panel.NoiseControlRequested += (_, mode) => NoiseControlRequested?.Invoke(this, mode);
    }

    public event EventHandler? ShowPopupRequested;
    public event EventHandler<NoiseControlMode>? NoiseControlRequested;
    public event EventHandler<bool>? ConversationAwarenessRequested;
    public event EventHandler<int>? AdaptiveStrengthRequested;

    public void Update(DeviceViewState state, IReadOnlyList<PairedDeviceInfo> paired, bool driverPresent)
    {
        Panel.Update(state);
        PairedText.Text = paired.Count == 0
            ? "No AirPods or Beats are paired with this PC yet. Pair them in Bluetooth settings for automatic ear detection."
            : "Paired: " + string.Join(", ", paired.Select(p => p.IsConnected ? $"{p.Name} (connected)" : p.Name));

        var aap = state.Aap is { Connected: true } a ? a : null;
        var support = state.Snapshot?.Device.NoiseControl ?? NoiseControlSupport.None;
        AapStatusText.Text = (driverPresent, aap) switch
        {
            (false, _) => "Switching noise control needs the PodPilot AAP driver (experimental).",
            (true, null) => "Driver installed. Waiting for your AirPods to connect...",
            _ when support == NoiseControlSupport.None => "Connected over AAP. These headphones have no noise control.",
            _ => "Connected over AAP. Switch modes with the buttons on the left.",
        };

        AapControls.Visibility = aap is not null && support == NoiseControlSupport.Adaptive ? Visibility.Visible : Visibility.Collapsed;
        VolumeSwipeCheck.Visibility = aap is not null && support == NoiseControlSupport.Adaptive ? Visibility.Visible : Visibility.Collapsed;
        if (aap is not null)
        {
            ConversationToggle.IsChecked = aap.ConversationAwareness ?? false;
            if (aap.AdaptiveStrength is int strength && !AdaptiveSlider.IsMouseCaptureWithin)
                AdaptiveSlider.Value = strength;
        }
    }

    public event EventHandler? DriverButtonClicked;
    public event EventHandler? CheckUpdatesRequested;

    public void SetUpdateStatus(string text, bool busy = false)
    {
        UpdateStatusText.Text = text;
        CheckUpdatesButton.IsEnabled = !busy && Updater.Supported;
    }

    void OnCheckUpdatesClick(object sender, RoutedEventArgs e) => CheckUpdatesRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Shows the install/remove button for the driver embedded in this build.</summary>
    public void UpdateDriver(bool installed, DriverPackageKind package, bool busy)
    {
        // The Store version may not install drivers, so it never offers it.
        DriverPanel.Visibility = PackageInfo.IsPackaged || (package == DriverPackageKind.None && !installed)
            ? Visibility.Collapsed : Visibility.Visible;
        DriverButton.IsEnabled = !busy;
        DriverButton.Content = busy ? "Working..." : installed ? "Remove noise control driver" : "Install noise control driver";
        DriverNote.Text = installed
            ? "Installed. Windows asks for administrator permission to remove it."
            : package == DriverPackageKind.MicrosoftSigned
                ? "Windows asks for administrator permission once. Secure Boot stays on."
                : "Experimental test build: the driver only loads after starting Windows with \"Disable driver signature enforcement\" (Shift + Restart > Troubleshoot > Advanced options > Startup Settings > F7).";
    }

    void OnDriverClick(object sender, RoutedEventArgs e) => DriverButtonClicked?.Invoke(this, EventArgs.Empty);

    public event EventHandler<bool>? VolumeSwipeRequested;

    void OnVolumeSwipeClick(object sender, RoutedEventArgs e) =>
        VolumeSwipeRequested?.Invoke(this, VolumeSwipeCheck.IsChecked == true);

    void OnConversationClick(object sender, RoutedEventArgs e) =>
        ConversationAwarenessRequested?.Invoke(this, ConversationToggle.IsChecked == true);

    void OnAdaptiveDragCompleted(object sender, DragCompletedEventArgs e) =>
        AdaptiveStrengthRequested?.Invoke(this, (int)AdaptiveSlider.Value);

    void OnShowPopupClick(object sender, RoutedEventArgs e) => ShowPopupRequested?.Invoke(this, EventArgs.Empty);

    void OnBluetoothClick(object sender, RoutedEventArgs e) => OpenUri("ms-settings:bluetooth");

    void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        OpenUri(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    static void OpenUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }
}
