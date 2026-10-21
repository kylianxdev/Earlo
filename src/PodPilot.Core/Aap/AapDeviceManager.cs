using System.Collections.Concurrent;
using Windows.Devices.Enumeration;

namespace PodPilot.Core.Aap;

/// <summary>
/// Finds AirPods exposed by the PodPilot AAP driver and keeps a session open to the connected one.
/// Without the driver installed this simply never finds anything.
/// </summary>
public sealed class AapDeviceManager : IDisposable
{
    static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(10);

    readonly ConcurrentDictionary<string, AapSession?> _sessions = new();
    readonly Timer _retryTimer;
    DeviceWatcher? _watcher;
    volatile bool _disposed;

    public AapDeviceManager()
    {
        _retryTimer = new Timer(_ => RetryAll(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>True when at least one AirPods device interface from the driver exists.</summary>
    public bool DriverPresent => !_sessions.IsEmpty;

    /// <summary>The session of the AirPods that are currently connected over AAP, if any.</summary>
    public AapSession? Current => _sessions.Values.FirstOrDefault(s => s?.State.Connected == true);

    /// <summary>Raised on a background thread when a session connects, disconnects or reports new state.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised after the handshake, so the app can apply its settings to the AirPods.</summary>
    public event EventHandler<AapSession>? SessionStarted;

    /// <summary>Every AAP packet sent (false) or received (true), for diagnostics.</summary>
    public event Action<bool, ReadOnlyMemory<byte>>? PacketTraced;

    public void Start()
    {
        if (_watcher is not null)
            return;
        _watcher = DeviceInformation.CreateWatcher(DriverProtocol.DeviceSelector);
        _watcher.Added += (sender, info) => { _sessions.TryAdd(info.Id, null); _ = ConnectAsync(info.Id); };
        _watcher.Removed += (sender, update) => Remove(update.Id);
        _watcher.Start();
        _retryTimer.Change(RetryInterval, RetryInterval);
    }

    /// <summary>Try to connect now, for example right after the AirPods connected over Bluetooth.</summary>
    public void RetryAll()
    {
        foreach (var (id, session) in _sessions)
        {
            if (session is null || !session.State.Connected)
                _ = ConnectAsync(id);
        }
    }

    async Task ConnectAsync(string id)
    {
        if (_disposed || !_sessions.TryGetValue(id, out var existing) || existing?.State.Connected == true)
            return;

        existing?.Dispose();
        var session = new AapSession(new DriverAapTransport(id));
        session.StateChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        session.PacketTraced += (incoming, packet) => PacketTraced?.Invoke(incoming, packet);
        try
        {
            await session.StartAsync().ConfigureAwait(false);
            _sessions[id] = session;
            SessionStarted?.Invoke(this, session);
        }
        catch (Exception)
        {
            // AirPods not connected yet; the retry timer tries again.
            session.Dispose();
            _sessions[id] = null;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    void Remove(string id)
    {
        if (_sessions.TryRemove(id, out var session))
            session?.Dispose();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _disposed = true;
        _retryTimer.Dispose();
        if (_watcher is { Status: DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted })
            _watcher.Stop();
        foreach (var session in _sessions.Values)
            session?.Dispose();
        _sessions.Clear();
    }
}
