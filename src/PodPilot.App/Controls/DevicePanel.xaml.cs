using System.Windows;
using System.Windows.Controls;
using PodPilot.Core.Bluetooth;
using PodPilot.Core.Tracking;

namespace PodPilot.App.Controls;

/// <summary>Everything PodPilot knows about the current device: artwork, batteries and connection.</summary>
public partial class DevicePanel : UserControl
{
    public DevicePanel()
    {
        InitializeComponent();
        Update(new DeviceViewState(null, null, ScannerStatus.Starting, 20));
    }

    public void Update(DeviceViewState state)
    {
        var snapshot = state.Snapshot;
        if (snapshot is null)
        {
            DeviceView.Visibility = Visibility.Collapsed;
            EmptyView.Visibility = Visibility.Visible;
            (EmptyTitle.Text, EmptyText.Text) = state.ScannerStatus switch
            {
                ScannerStatus.NoAdapter => ("No Bluetooth adapter", "PodPilot needs a Bluetooth 4.0 (LE) adapter."),
                ScannerStatus.RadioOff => ("Bluetooth is off", "Turn on Bluetooth in Windows settings."),
                ScannerStatus.Error => ("Bluetooth error", "Scanning stopped. PodPilot keeps retrying."),
                _ => ("Looking for AirPods", "Open the case next to your PC, or put your AirPods in your ears."),
            };
            return;
        }

        DeviceView.Visibility = Visibility.Visible;
        EmptyView.Visibility = Visibility.Collapsed;

        var device = snapshot.Device;
        bool hasCase = device.HasCase;
        NameText.Text = state.Connected?.Name ?? device.Name;
        ModelText.Text = state.Connected is null ? (device.IsKnown ? "Nearby" : "Unknown model, shown as generic headphones") : device.Name;

        var render = DeviceImages.For(device.ModelId);
        Render.Source = render;
        Render.Visibility = render is null ? Visibility.Collapsed : Visibility.Visible;
        BudsArt.FormFactor = device.FormFactor;
        BudsArt.IsDarkDevice = device.IsBeats;
        CaseArt.IsDarkDevice = device.IsBeats;
        BudsArt.Visibility = render is null ? Visibility.Visible : Visibility.Collapsed;
        CaseArt.Visibility = render is null && hasCase ? Visibility.Visible : Visibility.Collapsed;

        foreach (var bar in new[] { LeftBar, RightBar, CaseBar, SingleBar })
            bar.LowThreshold = state.LowThreshold;

        LeftBar.Visibility = RightBar.Visibility = CaseBar.Visibility = hasCase ? Visibility.Visible : Visibility.Collapsed;
        SingleBar.Visibility = hasCase ? Visibility.Collapsed : Visibility.Visible;
        LeftBar.State = snapshot.Left;
        RightBar.State = snapshot.Right;
        CaseBar.State = snapshot.Case;
        SingleBar.State = snapshot.Single;
        LeftBar.Label = snapshot.LeftInEar ? "Left •" : "Left";
        RightBar.Label = snapshot.RightInEar ? "Right •" : "Right";

        bool connected = state.Connected is not null;
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, connected ? "BatteryGood" : "BatteryUnknown");
        StatusText.Text = (connected ? "Connected to this PC" : "Not connected to this PC")
            + (hasCase && snapshot.AnyInEar ? (snapshot.BothInEar ? " · in both ears" : " · in one ear") : string.Empty);
    }
}

public sealed record DeviceViewState(PodsSnapshot? Snapshot, PairedDeviceInfo? Connected, ScannerStatus ScannerStatus, int LowThreshold);
