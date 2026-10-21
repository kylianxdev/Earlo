using System;
using System.Windows;
using System.Windows.Controls;
using PodPilot.Core.Aap;
using PodPilot.Core.Bluetooth;
using PodPilot.Core.Devices;
using PodPilot.Core.Tracking;

namespace PodPilot.App.Controls;

/// <summary>Everything PodPilot knows about the current device: artwork, batteries and connection.</summary>
public partial class DevicePanel : UserControl
{
    public DevicePanel()
    {
        InitializeComponent();
        NoiseBar.ModeRequested += (_, mode) => NoiseControlRequested?.Invoke(this, mode);
        Update(new DeviceViewState(null, null, ScannerStatus.Starting, 20));
    }

    public event EventHandler<NoiseControlMode>? NoiseControlRequested;

    /// <summary>The Connect/Disconnect button was clicked; true means connect.</summary>
    public event EventHandler<bool>? ConnectRequested;

    bool _connected;

    public void Update(DeviceViewState state)
    {
        var snapshot = state.Snapshot;
        if (snapshot is null)
        {
            DeviceView.Visibility = Visibility.Collapsed;
            EmptyView.Visibility = Visibility.Visible;
            (EmptyTitle.Text, EmptyText.Text) = state.ScannerStatus switch
            {
                ScannerStatus.NoAdapter => (Strings.NoAdapterTitle, Strings.NoAdapterText),
                ScannerStatus.RadioOff => (Strings.RadioOffTitle, Strings.RadioOffText),
                ScannerStatus.Error => (Strings.ScanErrorTitle, Strings.ScanErrorText),
                _ => (Strings.LookingTitle, Strings.LookingText),
            };
            return;
        }

        DeviceView.Visibility = Visibility.Visible;
        EmptyView.Visibility = Visibility.Collapsed;

        var device = snapshot.Device;
        bool hasCase = device.HasCase;
        NameText.Text = state.Connected?.Name ?? device.Name;
        ModelText.Text = state.Connected is null ? (device.IsKnown ? Strings.Nearby : Strings.UnknownModel) : device.Name;

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
        // Exact 1% levels over AAP (driver installed) win over the 10% steps from the advertisement.
        var aap = state.Aap is { Connected: true } a ? a : null;
        LeftBar.State = Exact(aap?.Left) ?? snapshot.Left;
        RightBar.State = Exact(aap?.Right) ?? snapshot.Right;
        CaseBar.State = Exact(aap?.Case) ?? snapshot.Case;
        SingleBar.State = Exact(aap?.Single) ?? snapshot.Single;
        LeftBar.Placement = Placement(snapshot.LeftInEar, snapshot.LeftInCase, snapshot.Left.IsKnown);
        RightBar.Placement = Placement(snapshot.RightInEar, snapshot.RightInCase, snapshot.Right.IsKnown);
        CaseBar.Hint = CaseBar.State.IsKnown ? null : Strings.CaseUnknown;

        bool hasNoiseControl = aap is not null && device.NoiseControl != NoiseControlSupport.None;
        NoiseBar.Visibility = hasNoiseControl ? Visibility.Visible : Visibility.Collapsed;
        if (hasNoiseControl)
            NoiseBar.Update(aap!.NoiseControl, device.NoiseControl == NoiseControlSupport.Adaptive);

        bool connected = state.Connected is not null;
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, connected ? "BatteryGood" : "BatteryUnknown");
        StatusText.Text = (connected ? Strings.ConnectedToPc : Strings.NotConnectedToPc)
            + (hasCase && snapshot.AnyInEar ? " · " + (snapshot.BothInEar ? Strings.InBothEars : Strings.InOneEar) : string.Empty);

        _connected = connected;
        ConnectButton.Visibility = state.CanConnect ? Visibility.Visible : Visibility.Collapsed;
        ConnectButton.IsEnabled = !state.Connecting && !state.Disconnecting;
        ConnectButton.Content = state.Connecting ? Strings.Connecting
            : state.Disconnecting ? Strings.Disconnecting
            : connected ? Strings.Disconnect : Strings.Connect;
    }

    void OnConnectClick(object sender, RoutedEventArgs e) => ConnectRequested?.Invoke(this, !_connected);

    static PodPlacement Placement(bool inEar, bool inCase, bool reported) =>
        inEar ? PodPlacement.InEar
        : inCase ? PodPlacement.InCase
        : reported ? PodPlacement.OutOfEar
        : PodPlacement.Unknown;

    static BatteryState? Exact(AapBattery? battery) =>
        battery is { } b ? new BatteryState(b.Level, b.Status == AapBatteryStatus.Charging) : null;
}

public sealed record DeviceViewState(
    PodsSnapshot? Snapshot, PairedDeviceInfo? Connected, ScannerStatus ScannerStatus, int LowThreshold, AapState? Aap = null,
    bool CanConnect = false, bool Connecting = false, bool Disconnecting = false);
