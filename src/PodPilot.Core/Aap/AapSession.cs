namespace PodPilot.Core.Aap;

/// <summary>What the AirPods reported over AAP. Values stay null until the AirPods send them.</summary>
public sealed record AapState
{
    public bool Connected { get; init; }
    public NoiseControlMode? NoiseControl { get; init; }
    public bool? ConversationAwareness { get; init; }
    public int? AdaptiveStrength { get; init; }
    public bool? VolumeSwipe { get; init; }
    public AapBattery? Left { get; init; }
    public AapBattery? Right { get; init; }
    public AapBattery? Case { get; init; }
    public AapBattery? Single { get; init; }
    public AapPodPlacement? PrimaryPod { get; init; }
    public AapPodPlacement? SecondaryPod { get; init; }
    public bool Speaking { get; init; }
    public AapDeviceInfoMessage? DeviceInfo { get; init; }
}

/// <summary>
/// Talks AAP with one pair of AirPods: performs the handshake, keeps <see cref="State"/> up to date
/// from notifications and sends settings such as the noise control mode.
/// </summary>
public sealed class AapSession : IDisposable
{
    readonly IAapTransport _transport;
    readonly object _gate = new();
    AapState _state = new();

    public AapSession(IAapTransport transport)
    {
        _transport = transport;
        _transport.PacketReceived += OnPacket;
        _transport.Closed += (_, _) => Update(s => s with { Connected = false });
    }

    public AapState State
    {
        get { lock (_gate) return _state; }
    }

    /// <summary>Raised on a background thread whenever <see cref="State"/> changes.</summary>
    public event EventHandler<AapState>? StateChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _transport.OpenAsync(cancellationToken).ConfigureAwait(false);
        await Send(AapPacket.Handshake, cancellationToken).ConfigureAwait(false);
        await Send(AapPacket.HostCapabilities(), cancellationToken).ConfigureAwait(false);
        await Send(AapPacket.RequestNotifications(), cancellationToken).ConfigureAwait(false);
        Update(s => s with { Connected = true });
    }

    public Task SetNoiseControlAsync(NoiseControlMode mode, CancellationToken cancellationToken = default)
    {
        // Show the new mode right away; the AirPods confirm with the same packet.
        Update(s => s with { NoiseControl = mode });
        return Send(AapPacket.SetNoiseControl(mode), cancellationToken);
    }

    public Task SetConversationAwarenessAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        Update(s => s with { ConversationAwareness = enabled });
        return Send(AapPacket.SetConversationAwareness(enabled), cancellationToken);
    }

    public Task SetAdaptiveStrengthAsync(int level, CancellationToken cancellationToken = default)
    {
        Update(s => s with { AdaptiveStrength = Math.Clamp(level, 0, 100) });
        return Send(AapPacket.SetAdaptiveStrength(level), cancellationToken);
    }

    public Task SetVolumeSwipeAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        Update(s => s with { VolumeSwipe = enabled });
        return Send(AapPacket.SetVolumeSwipe(enabled), cancellationToken);
    }

    /// <summary>Raised for every packet sent (incoming false) or received (true), for diagnostics.</summary>
    public event Action<bool, ReadOnlyMemory<byte>>? PacketTraced;

    Task Send(byte[] packet, CancellationToken cancellationToken)
    {
        PacketTraced?.Invoke(false, packet);
        return _transport.SendAsync(packet, cancellationToken);
    }

    void OnPacket(object? sender, ReadOnlyMemory<byte> packet)
    {
        PacketTraced?.Invoke(true, packet);
        switch (AapMessage.Parse(packet.Span))
        {
            case AapBatteryMessage battery:
                Update(s => Apply(s, battery));
                break;
            case AapEarDetectionMessage ear:
                Update(s => s with { PrimaryPod = ear.Primary, SecondaryPod = ear.Secondary });
                break;
            case AapControlMessage { Identifier: AapControl.ListeningMode } control when Enum.IsDefined((NoiseControlMode)control.Value1):
                Update(s => s with { NoiseControl = (NoiseControlMode)control.Value1 });
                break;
            case AapControlMessage { Identifier: AapControl.ConversationDetect } control:
                Update(s => s with { ConversationAwareness = control.Value1 == 0x01 });
                break;
            case AapControlMessage { Identifier: AapControl.VolumeSwipe } control:
                Update(s => s with { VolumeSwipe = control.Value1 == 0x01 });
                break;
            case AapControlMessage { Identifier: AapControl.AdaptiveStrength } control:
                Update(s => s with { AdaptiveStrength = control.Value1 });
                break;
            case AapConversationLevelMessage level:
                Update(s => s with { Speaking = level.Level is 1 or 2 });
                break;
            case AapDeviceInfoMessage info:
                Update(s => s with { DeviceInfo = info });
                break;
        }
    }

    static AapState Apply(AapState state, AapBatteryMessage message)
    {
        foreach (var b in message.Batteries)
        {
            // A disconnected part (pod out of range, case not reporting) keeps its last level.
            if (b.Status == AapBatteryStatus.Disconnected)
                continue;
            state = b.Component switch
            {
                AapBatteryComponent.Left => state with { Left = b },
                AapBatteryComponent.Right => state with { Right = b },
                AapBatteryComponent.Case => state with { Case = b },
                AapBatteryComponent.Single => state with { Single = b },
                _ => state,
            };
        }
        return state;
    }

    void Update(Func<AapState, AapState> change)
    {
        AapState updated;
        lock (_gate)
        {
            updated = change(_state);
            if (updated == _state)
                return;
            _state = updated;
        }
        StateChanged?.Invoke(this, updated);
    }

    public void Dispose()
    {
        _transport.PacketReceived -= OnPacket;
        _transport.Dispose();
    }
}
