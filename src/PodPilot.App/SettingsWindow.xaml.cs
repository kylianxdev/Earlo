using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Navigation;
using PodPilot.App.Controls;
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
            (false, _) => "Needs the PodPilot AAP driver (experimental). See the driver folder on GitHub for installation.",
            (true, null) => "Driver installed. Waiting for your AirPods to connect...",
            _ when support == NoiseControlSupport.None => "Connected over AAP. These headphones have no noise control.",
            _ => "Connected over AAP. Switch modes with the buttons on the left.",
        };

        AapControls.Visibility = aap is not null && support == NoiseControlSupport.Adaptive ? Visibility.Visible : Visibility.Collapsed;
        if (aap is not null)
        {
            ConversationToggle.IsChecked = aap.ConversationAwareness ?? false;
            if (aap.AdaptiveStrength is int strength && !AdaptiveSlider.IsMouseCaptureWithin)
                AdaptiveSlider.Value = strength;
        }
    }

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
