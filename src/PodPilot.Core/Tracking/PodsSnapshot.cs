using PodPilot.Core.Devices;

namespace PodPilot.Core.Tracking;

public readonly record struct BatteryState(int? Percent, bool Charging)
{
    public static readonly BatteryState Unknown = new(null, false);

    public bool IsKnown => Percent.HasValue;
}

/// <summary>The merged, smoothed state of one pair of headphones.</summary>
public sealed record PodsSnapshot(
    DeviceInfo Device,
    byte Color,
    BatteryState Left,
    BatteryState Right,
    BatteryState Case,
    BatteryState Single,
    bool LeftInEar,
    bool RightInEar,
    bool AnyPodInCase,
    bool LidOpen,
    int Rssi,
    DateTimeOffset LastSeen)
{
    public bool AnyInEar => LeftInEar || RightInEar;

    public bool BothInEar => LeftInEar && RightInEar;

    /// <summary>The lowest known level of the parts that are in use, for the tray icon.</summary>
    public BatteryState Lowest
    {
        get
        {
            if (Device.HasSingleBattery)
                return Single;

            var parts = new[] { Left, Right }.Where(b => b.IsKnown).ToList();
            if (parts.Count == 0 && Case.IsKnown)
                return Case;
            if (parts.Count == 0)
                return BatteryState.Unknown;

            var min = parts.MinBy(b => b.Percent)!;
            return min with { Charging = parts.All(b => b.Charging) };
        }
    }
}

public sealed class EarStatusChangedEventArgs(PodsSnapshot snapshot, bool wasLeftInEar, bool wasRightInEar) : EventArgs
{
    public PodsSnapshot Snapshot { get; } = snapshot;
    public bool WasLeftInEar { get; } = wasLeftInEar;
    public bool WasRightInEar { get; } = wasRightInEar;
}
