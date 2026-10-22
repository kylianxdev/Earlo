using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Earlo.Core.Bluetooth;

/// <param name="ContainerId">Groups the device with its other parts, like its Windows audio endpoints.</param>
public sealed record PairedDeviceInfo(string Id, string Name, bool IsConnected, Guid? ContainerId = null);

/// <summary>
/// Tracks AirPods, Beats and Galaxy Buds that are paired with this PC and whether they are connected.
/// Used to show the user's own device name and to only control media for headphones in use.
/// Events are raised on a background thread.
/// </summary>
public sealed partial class PairedDeviceMonitor : IDisposable
{
    const string ContainerIdProperty = "System.Devices.Aep.ContainerId";

    readonly ConcurrentDictionary<string, BluetoothDevice> _devices = new();
    readonly ConcurrentDictionary<string, Guid> _containers = new();
    DeviceWatcher? _watcher;

    public event EventHandler<PairedDeviceInfo>? ConnectionChanged;
    public event EventHandler? DevicesChanged;

    public IReadOnlyList<PairedDeviceInfo> Devices =>
        _devices.Values.Select(ToInfo).OrderByDescending(d => d.IsConnected).ThenBy(d => d.Name).ToList();

    public PairedDeviceInfo? ConnectedDevice => Devices.FirstOrDefault(d => d.IsConnected);

    public static bool IsAppleAudioName(string? name) => name is not null && AppleAudioName().IsMatch(name);

    /// <summary>AirPods, Beats or Galaxy Buds.</summary>
    public static bool IsSupportedName(string? name) => IsAppleAudioName(name) || Samsung.GalaxyBudsModels.FromPairedName(name) is not null;

    public void Start()
    {
        if (_watcher is not null)
            return;

        _watcher = DeviceInformation.CreateWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
        _watcher.Added += (sender, info) => _ = AddAsync(info.Id);
        _watcher.Removed += (sender, update) => Remove(update.Id);
        _watcher.Updated += (sender, update) => _ = AddAsync(update.Id);
        _watcher.Start();
    }

    async Task AddAsync(string id)
    {
        if (_devices.ContainsKey(id))
            return;

        BluetoothDevice? device;
        try
        {
            device = await BluetoothDevice.FromIdAsync(id);
        }
        catch (Exception)
        {
            return;
        }

        if (device is null || !IsSupportedName(device.Name) || !_devices.TryAdd(id, device))
        {
            device?.Dispose();
            return;
        }

        try
        {
            var info = await DeviceInformation.CreateFromIdAsync(id, [ContainerIdProperty]);
            if (info.Properties.TryGetValue(ContainerIdProperty, out var container) && container is Guid guid)
                _containers[id] = guid;
        }
        catch (Exception)
        {
            // Without it audio endpoints are matched by name.
        }

        device.ConnectionStatusChanged += OnConnectionStatusChanged;
        device.NameChanged += (_, _) => DevicesChanged?.Invoke(this, EventArgs.Empty);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
        if (device.ConnectionStatus == BluetoothConnectionStatus.Connected)
            ConnectionChanged?.Invoke(this, ToInfo(device));
    }

    void Remove(string id)
    {
        if (!_devices.TryRemove(id, out var device))
            return;
        _containers.TryRemove(id, out _);
        device.ConnectionStatusChanged -= OnConnectionStatusChanged;
        device.Dispose();
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    void OnConnectionStatusChanged(BluetoothDevice sender, object args)
    {
        ConnectionChanged?.Invoke(this, ToInfo(sender));
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    PairedDeviceInfo ToInfo(BluetoothDevice d) =>
        new(d.DeviceId, d.Name, d.ConnectionStatus == BluetoothConnectionStatus.Connected,
            _containers.TryGetValue(d.DeviceId, out var container) ? container : null);

    [GeneratedRegex("airpods|beats", RegexOptions.IgnoreCase)]
    private static partial Regex AppleAudioName();

    public void Dispose()
    {
        if (_watcher is { Status: DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted })
            _watcher.Stop();
        foreach (var device in _devices.Values)
            device.Dispose();
        _devices.Clear();
    }
}
