using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Navigation;
using Earlo.App.Controls;
using Earlo.App.Services;
using Earlo.Core.Aap;
using Earlo.Core.Bluetooth;
using Earlo.Core.Devices;

namespace Earlo.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        DataContext = settings;
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Earlo {version?.ToString(3)}";
        AutoUpdateCheck.Visibility = Updater.Supported ? Visibility.Visible : Visibility.Collapsed;
        SetUpdateStatus(PackageInfo.IsPackaged ? Strings.StoreUpdates
            : Updater.Supported ? string.Empty : Strings.DevBuild);
        Panel.NoiseControlRequested += (_, mode) => NoiseControlRequested?.Invoke(this, mode);
        Panel.ConnectRequested += (_, connect) => ConnectRequested?.Invoke(this, connect);
    }

    public event EventHandler? ShowPopupRequested;
    public event EventHandler<NoiseControlMode>? NoiseControlRequested;
    public event EventHandler<bool>? ConnectRequested;
    public event EventHandler<bool>? ConversationAwarenessRequested;
    public event EventHandler<int>? AdaptiveStrengthRequested;

    public void Update(DeviceViewState state, IReadOnlyList<PairedDeviceInfo> paired, bool driverPresent)
    {
        Panel.Update(state);
        PairedText.Text = paired.Count == 0
            ? Strings.NonePaired
            : Strings.Paired + ": " + string.Join(", ", paired.Select(p => p.IsConnected ? $"{p.Name} ({Strings.ConnectedSuffix})" : p.Name));

        var aap = state.Aap is { Connected: true } a ? a : null;
        var support = state.Snapshot?.Device.NoiseControl ?? NoiseControlSupport.None;
        AapStatusText.Text = (driverPresent, aap) switch
        {
            (false, _) => Strings.AapNeedsDriver,
            (true, null) => Strings.AapWaiting,
            _ when support == NoiseControlSupport.None => Strings.AapNoNoiseControl,
            _ => Strings.AapConnected,
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
        DriverButton.Content = busy ? Strings.Working : installed ? Strings.RemoveDriver : Strings.InstallDriver;
        DriverNote.Text = installed
            ? Strings.DriverInstalledNote
            : package == DriverPackageKind.MicrosoftSigned ? Strings.DriverSignedNote : Strings.DriverTestNote;
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
