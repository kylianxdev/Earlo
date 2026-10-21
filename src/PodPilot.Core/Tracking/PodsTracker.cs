using PodPilot.Core.Devices;
using PodPilot.Core.Protocol;

namespace PodPilot.Core.Tracking;

/// <summary>
/// Turns the stream of raw advertisements into one stable device state.
/// </summary>
/// <remarks>
/// AirPods rotate their BLE address, alternate which pod broadcasts and often omit values
/// (a pod out of range, the case closed). The tracker groups messages per model, keeps the last
/// known value of each field for a while, picks the closest device and raises events on changes.
/// Not thread-safe: call it from a single thread.
/// </remarks>
public sealed class PodsTracker(TimeProvider? timeProvider = null)
{
    static readonly TimeSpan PodHold = TimeSpan.FromMinutes(2);
    static readonly TimeSpan CaseHold = TimeSpan.FromMinutes(30);
    static readonly TimeSpan LidPopupCooldown = TimeSpan.FromSeconds(15);

    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly Dictionary<(ushort Model, byte Color), Entry> _entries = [];
    (ushort Model, byte Color)? _currentKey;

    /// <summary>Advertisements weaker than this (in dBm) are ignored, so a neighbour's AirPods do not show up.</summary>
    public int RssiThreshold { get; set; } = -70;

    /// <summary>A device that has not been heard for this long is considered gone.</summary>
    public TimeSpan DeviceTimeout { get; set; } = TimeSpan.FromSeconds(45);

    public PodsSnapshot? Current => _currentKey is { } key && _entries.TryGetValue(key, out var e) ? e.Snapshot : null;

    public event EventHandler<PodsSnapshot>? Updated;
    public event EventHandler<PodsSnapshot>? LidOpened;
    public event EventHandler<EarStatusChangedEventArgs>? EarStatusChanged;
    public event EventHandler? Lost;

    public void Process(ProximityPairingMessage message, int rssi)
    {
        var now = _time.GetUtcNow();
        var key = (message.ModelId, message.Color);
        bool isCurrent = _currentKey == key;

        // Some hysteresis for the device we already show, so it does not flicker at the edge.
        int threshold = isCurrent ? RssiThreshold - 8 : RssiThreshold;
        if (rssi < threshold)
            return;

        bool isNew = !_entries.TryGetValue(key, out var entry);
        if (entry is null)
        {
            entry = new Entry();
            _entries[key] = entry;
        }

        var previous = entry.Snapshot;
        entry.SmoothedRssi = isNew ? rssi : (entry.SmoothedRssi * 0.7) + (rssi * 0.3);

        var device = previous?.Device is { IsKnown: true } known
            ? known
            : DeviceCatalog.Resolve(message.ModelId, message.ReportsPods);

        var snapshot = new PodsSnapshot(
            device,
            message.Color,
            entry.Left.Update(message.LeftBattery, message.LeftCharging, now, PodHold),
            entry.Right.Update(message.RightBattery, message.RightCharging, now, PodHold),
            entry.Case.Update(message.CaseBattery, message.CaseCharging, now, CaseHold),
            entry.Single.Update(message.SingleBattery, message.SingleCharging, now, PodHold),
            message.LeftInEar,
            message.RightInEar,
            message.AnyPodInCase,
            message.LidOpen,
            (int)Math.Round(entry.SmoothedRssi),
            now);
        entry.Snapshot = snapshot;

        SelectCurrent(now);
        if (_currentKey != key)
            return;

        if (snapshot.LidOpen && snapshot.AnyPodInCase && device.HasCase
            && (previous is null || !previous.LidOpen || !previous.AnyPodInCase || previous.LastSeen < now - DeviceTimeout)
            && now - entry.LastLidPopup > LidPopupCooldown)
        {
            entry.LastLidPopup = now;
            LidOpened?.Invoke(this, snapshot);
        }

        if (previous is not null && device.SupportsEarDetection
            && (previous.LeftInEar != snapshot.LeftInEar || previous.RightInEar != snapshot.RightInEar))
        {
            EarStatusChanged?.Invoke(this, new EarStatusChangedEventArgs(snapshot, previous.LeftInEar, previous.RightInEar));
        }

        if (previous is null || !SameVisibleState(previous, snapshot) || !isCurrent)
            Updated?.Invoke(this, snapshot);
    }

    /// <summary>Drops devices that went quiet. Call periodically.</summary>
    public void Expire()
    {
        var now = _time.GetUtcNow();
        foreach (var (key, entry) in _entries.ToList())
        {
            if (entry.Snapshot is null || now - entry.Snapshot.LastSeen > DeviceTimeout)
                _entries.Remove(key);
        }

        var hadCurrent = _currentKey is not null;
        SelectCurrent(now);
        if (hadCurrent && _currentKey is null)
            Lost?.Invoke(this, EventArgs.Empty);
        else if (hadCurrent && Current is { } current)
            Updated?.Invoke(this, current);
    }

    void SelectCurrent(DateTimeOffset now)
    {
        var alive = _entries
            .Where(kv => kv.Value.Snapshot is not null && now - kv.Value.Snapshot.LastSeen <= DeviceTimeout)
            .ToList();
        if (alive.Count == 0)
        {
            _currentKey = null;
            return;
        }

        var best = alive.MaxBy(kv => kv.Value.SmoothedRssi);
        if (_currentKey is { } current && _entries.TryGetValue(current, out var cur)
            && alive.Any(kv => kv.Key == current)
            && cur.SmoothedRssi >= best.Value.SmoothedRssi - 6)
        {
            return; // keep the current device unless another one is clearly closer
        }

        _currentKey = best.Key;
    }

    static bool SameVisibleState(PodsSnapshot a, PodsSnapshot b) =>
        a.Left == b.Left && a.Right == b.Right && a.Case == b.Case && a.Single == b.Single
        && a.LeftInEar == b.LeftInEar && a.RightInEar == b.RightInEar
        && a.LidOpen == b.LidOpen && a.AnyPodInCase == b.AnyPodInCase;

    sealed class Entry
    {
        public PodsSnapshot? Snapshot;
        public double SmoothedRssi;
        public DateTimeOffset LastLidPopup = DateTimeOffset.MinValue;
        public readonly HeldBattery Left = new();
        public readonly HeldBattery Right = new();
        public readonly HeldBattery Case = new();
        public readonly HeldBattery Single = new();
    }

    /// <summary>Keeps the last reported level for a while when a message omits it.</summary>
    sealed class HeldBattery
    {
        BatteryState _value = BatteryState.Unknown;
        DateTimeOffset _updated;

        public BatteryState Update(int? percent, bool charging, DateTimeOffset now, TimeSpan hold)
        {
            if (percent.HasValue)
            {
                _value = new BatteryState(percent, charging);
                _updated = now;
            }
            else if (now - _updated > hold)
            {
                _value = BatteryState.Unknown;
            }
            return _value;
        }
    }
}
