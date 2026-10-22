using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Earlo.Core.Samsung;

/// <summary>
/// The serial (RFCOMM) channel to a pair of Galaxy Buds. The buds push their status on connect and
/// on every change (case opened, bud taken out, battery), so this only listens and acknowledges.
/// </summary>
public sealed class GalaxyBudsConnection : IDisposable
{
    readonly StreamSocket _socket;
    readonly GalaxyBudsModel _model;
    readonly CancellationTokenSource _stop = new();

    GalaxyBudsConnection(StreamSocket socket, GalaxyBudsModel model)
    {
        _socket = socket;
        _model = model;
    }

    /// <summary>Raised on a background thread for every status message.</summary>
    public event EventHandler<GalaxyBudsStatus>? StatusChanged;

    /// <summary>Raised once when the channel closes (buds disconnected or out of range).</summary>
    public event EventHandler? Closed;

    public static async Task<GalaxyBudsConnection?> OpenAsync(string deviceId, GalaxyBudsModel model)
    {
        using var device = await BluetoothDevice.FromIdAsync(deviceId);
        if (device is null)
            return null;
        var result = await device.GetRfcommServicesForIdAsync(
            RfcommServiceId.FromUuid(GalaxyBudsModels.ServiceFor(model)), BluetoothCacheMode.Uncached);
        if (result.Services.FirstOrDefault() is not { } service)
            return null;

        var socket = new StreamSocket();
        try
        {
            await socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName,
                SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);
        }
        catch (Exception)
        {
            socket.Dispose();
            return null;
        }

        var connection = new GalaxyBudsConnection(socket, model);
        _ = connection.ReadLoopAsync();
        return connection;
    }

    async Task ReadLoopAsync()
    {
        bool legacy = GalaxyBudsModels.UsesLegacyFraming(_model);
        var parser = new GalaxyBudsMessage.Reader(legacy);
        try
        {
            using var reader = new DataReader(_socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
            while (!_stop.IsCancellationRequested)
            {
                uint count = await reader.LoadAsync(1024).AsTask(_stop.Token);
                if (count == 0)
                    break;
                var bytes = new byte[count];
                reader.ReadBytes(bytes);

                foreach (var (id, payload) in parser.Append(bytes))
                {
                    if (GalaxyBudsMessage.ParseStatus(_model, id, payload) is { } status)
                        StatusChanged?.Invoke(this, status);
                    // The official app acknowledges every extended status with its client info.
                    if (id == GalaxyBudsMessage.ExtendedStatusUpdated)
                        await SendAsync(GalaxyBudsMessage.ManagerInfoReply(legacy));
                }
            }
        }
        catch (Exception)
        {
            // Socket closed or buds gone.
        }
        Closed?.Invoke(this, EventArgs.Empty);
    }

    async Task SendAsync(byte[] frame)
    {
        using var writer = new DataWriter(_socket.OutputStream);
        writer.WriteBytes(frame);
        await writer.StoreAsync();
        writer.DetachStream();
    }

    public void Dispose()
    {
        _stop.Cancel();
        _socket.Dispose();
    }
}
