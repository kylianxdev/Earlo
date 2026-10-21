using PodPilot.Core.Protocol;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Radios;
using Windows.Storage.Streams;

namespace PodPilot.Core.Bluetooth;

public enum ScannerStatus
{
    Starting,
    Scanning,
    NoAdapter,
    RadioOff,
    Error,
}

public sealed class AdvertisementEventArgs(ProximityPairingMessage message, int rssi, ulong address) : EventArgs
{
    public ProximityPairingMessage Message { get; } = message;
    public int Rssi { get; } = rssi;
    public ulong Address { get; } = address;
}

/// <summary>
/// Listens for Apple proximity pairing advertisements. Works without pairing and without drivers,
/// using the standard Windows Bluetooth LE APIs. Events are raised on a background thread.
/// </summary>
public sealed class AdvertisementScanner : IDisposable
{
    readonly BluetoothLEAdvertisementWatcher _watcher = new() { ScanningMode = BluetoothLEScanningMode.Active };
    readonly SemaphoreSlim _startLock = new(1, 1);
    Radio? _radio;
    bool _disposed;

    public AdvertisementScanner()
    {
        _watcher.Received += OnReceived;
        _watcher.Stopped += OnStopped;
    }

    public ScannerStatus Status { get; private set; } = ScannerStatus.Starting;

    public event EventHandler<AdvertisementEventArgs>? Received;
    public event EventHandler<ScannerStatus>? StatusChanged;

    /// <summary>Starts scanning, or reports why it cannot. Safe to call repeatedly.</summary>
    public async Task EnsureRunningAsync()
    {
        if (_disposed || _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
            return;

        await _startLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter is null || !adapter.IsLowEnergySupported)
            {
                SetStatus(ScannerStatus.NoAdapter);
                return;
            }

            if (_radio is null)
            {
                _radio = await adapter.GetRadioAsync();
                if (_radio is not null)
                    _radio.StateChanged += (sender, args) => _ = EnsureRunningAsync();
            }

            if (_radio is { State: not RadioState.On })
            {
                SetStatus(ScannerStatus.RadioOff);
                return;
            }

            if (_watcher.Status is not BluetoothLEAdvertisementWatcherStatus.Started)
                _watcher.Start();
            SetStatus(ScannerStatus.Scanning);
        }
        catch (Exception)
        {
            SetStatus(ScannerStatus.Error);
        }
        finally
        {
            _startLock.Release();
        }
    }

    void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        foreach (var section in args.Advertisement.ManufacturerData)
        {
            if (section.CompanyId != ProximityPairingMessage.AppleCompanyId || section.Data.Length < ProximityPairingMessage.MinimumLength)
                continue;

            var bytes = new byte[section.Data.Length];
            using (var reader = DataReader.FromBuffer(section.Data))
                reader.ReadBytes(bytes);

            if (ProximityPairingMessage.TryParse(bytes, out var message))
                Received?.Invoke(this, new AdvertisementEventArgs(message, args.RawSignalStrengthInDBm, args.BluetoothAddress));
        }
    }

    void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        if (_disposed)
            return;

        SetStatus(args.Error switch
        {
            BluetoothError.RadioNotAvailable => ScannerStatus.RadioOff,
            BluetoothError.NotSupported => ScannerStatus.NoAdapter,
            _ => ScannerStatus.Error,
        });
    }

    void SetStatus(ScannerStatus status)
    {
        if (Status == status)
            return;
        Status = status;
        StatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        _disposed = true;
        _watcher.Received -= OnReceived;
        _watcher.Stopped -= OnStopped;
        if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
            _watcher.Stop();
    }
}
