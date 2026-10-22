using Earlo.Core.Bluetooth;
using Earlo.Core.Devices;
using Earlo.Core.Tracking;

namespace Earlo.Core.Samsung;

/// <summary>
/// Keeps a status channel open to connected Galaxy Buds and turns their reports into the same
/// <see cref="PodsSnapshot"/> the AirPods tracker produces, so the whole UI works for them too.
/// Events are raised on a background thread.
/// </summary>
public sealed class GalaxyBudsManager : IDisposable
{
    readonly object _lock = new();
    GalaxyBudsConnection? _connection;
    string? _deviceId;
    bool _opening;
    DateTime _nextAttempt = DateTime.MinValue;

    public PodsSnapshot? Current { get; private set; }

    public event EventHandler<PodsSnapshot>? Updated;
    public event EventHandler<PodsSnapshot>? LidOpened;
    public event EventHandler<EarStatusChangedEventArgs>? EarStatusChanged;
    public event EventHandler? Lost;

    /// <summary>Opens or closes the channel to match the paired devices. Cheap to call often.</summary>
    public async Task EnsureAsync(IReadOnlyList<PairedDeviceInfo> paired)
    {
        var target = paired.FirstOrDefault(d => d.IsConnected && GalaxyBudsModels.FromPairedName(d.Name) is not null);
        lock (_lock)
        {
            if (target?.Id == _deviceId && (_connection is not null || _opening))
                return;
            if (target is null || target.Id != _deviceId)
                Close();
            if (target is null || _opening || DateTime.UtcNow < _nextAttempt)
                return;
            _opening = true;
            _deviceId = target.Id;
        }

        var model = GalaxyBudsModels.FromPairedName(target.Name)!.Value;
        var connection = await GalaxyBudsConnection.OpenAsync(target.Id, model);
        lock (_lock)
        {
            _opening = false;
            if (connection is null || _deviceId != target.Id)
            {
                connection?.Dispose();
                _nextAttempt = DateTime.UtcNow.AddSeconds(30);
                return;
            }
            _connection = connection;
        }
        connection.StatusChanged += (_, status) => OnStatus(model, status);
        connection.Closed += (_, _) =>
        {
            lock (_lock)
            {
                if (_connection != connection)
                    return;
                Close();
                _nextAttempt = DateTime.UtcNow.AddSeconds(5);
            }
        };
    }

    void OnStatus(GalaxyBudsModel model, GalaxyBudsStatus status)
    {
        var previous = Current;
        var snapshot = ToSnapshot(model, status);
        Current = snapshot;

        Updated?.Invoke(this, snapshot);
        if (snapshot.LidOpen && previous is { LidOpen: false })
            LidOpened?.Invoke(this, snapshot);
        if (previous is not null && (previous.LeftInEar != snapshot.LeftInEar || previous.RightInEar != snapshot.RightInEar))
            EarStatusChanged?.Invoke(this, new EarStatusChangedEventArgs(snapshot, previous.LeftInEar, previous.RightInEar));
    }

    public static PodsSnapshot ToSnapshot(GalaxyBudsModel model, GalaxyBudsStatus s)
    {
        static bool InCase(BudPlacement p) => p is BudPlacement.InOpenCase or BudPlacement.InClosedCase;
        // A bud that is not connected (e.g. left behind in a closed case) reports a meaningless 0.
        static BatteryState Bud(int level, bool charging, BudPlacement p) =>
            p == BudPlacement.Disconnected ? BatteryState.Unknown : new BatteryState(level, charging);

        bool leftInCase = InCase(s.LeftPlacement), rightInCase = InCase(s.RightPlacement);
        return new PodsSnapshot(
            DeviceCatalog.Resolve(GalaxyBudsModels.CatalogId(model), reportsPods: true),
            Color: 0,
            Left: Bud(s.Left, s.LeftCharging, s.LeftPlacement),
            Right: Bud(s.Right, s.RightCharging, s.RightPlacement),
            Case: s.Case is int c ? new BatteryState(c, s.CaseCharging) : BatteryState.Unknown,
            Single: BatteryState.Unknown,
            LeftInEar: s.LeftPlacement == BudPlacement.Wearing,
            RightInEar: s.RightPlacement == BudPlacement.Wearing,
            LeftInCase: leftInCase,
            RightInCase: rightInCase,
            AnyPodInCase: leftInCase || rightInCase,
            LidOpen: s.LeftPlacement == BudPlacement.InOpenCase || s.RightPlacement == BudPlacement.InOpenCase,
            Rssi: 0,
            LastSeen: DateTimeOffset.UtcNow);
    }

    void Close()
    {
        _connection?.Dispose();
        _connection = null;
        _deviceId = null;
        if (Current is null)
            return;
        Current = null;
        Lost?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_lock)
            Close();
    }
}
