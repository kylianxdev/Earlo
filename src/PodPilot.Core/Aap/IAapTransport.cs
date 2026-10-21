namespace PodPilot.Core.Aap;

/// <summary>A raw AAP channel (L2CAP PSM 0x1001) to one pair of AirPods.</summary>
public interface IAapTransport : IDisposable
{
    /// <summary>Opens the L2CAP channel. Throws when the headphones are not connected.</summary>
    Task OpenAsync(CancellationToken cancellationToken = default);

    Task SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken = default);

    /// <summary>Raised on a background thread for every packet the AirPods send.</summary>
    event EventHandler<ReadOnlyMemory<byte>>? PacketReceived;

    /// <summary>Raised when the channel closes, for example when the AirPods disconnect.</summary>
    event EventHandler? Closed;
}
