using PodPilot.Core.Aap;

namespace PodPilot.Core.Tests;

public class AapTests
{
    static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));

    sealed class FakeTransport : IAapTransport
    {
        public readonly List<byte[]> Sent = [];
        public bool Opened;

        public event EventHandler<ReadOnlyMemory<byte>>? PacketReceived;
        public event EventHandler? Closed;

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            Opened = true;
            return Task.CompletedTask;
        }

        public Task SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken = default)
        {
            Sent.Add(packet.ToArray());
            return Task.CompletedTask;
        }

        public void Receive(string hex) => PacketReceived?.Invoke(this, Hex(hex));

        public void Close() => Closed?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
        }
    }

    [Fact]
    public void Builds_known_packets()
    {
        Assert.Equal(Hex("04 00 04 00 09 00 0D 02 00 00 00"), AapPacket.SetNoiseControl(NoiseControlMode.NoiseCancellation));
        Assert.Equal(Hex("04 00 04 00 09 00 0D 04 00 00 00"), AapPacket.SetNoiseControl(NoiseControlMode.Adaptive));
        Assert.Equal(Hex("04 00 04 00 09 00 28 02 00 00 00"), AapPacket.SetConversationAwareness(false));
        Assert.Equal(Hex("04 00 04 00 09 00 2E 64 00 00 00"), AapPacket.SetAdaptiveStrength(150));
        Assert.Equal(Hex("04 00 04 00 0F 00 FF FF FF FF"), AapPacket.RequestNotifications());
        Assert.Equal(Hex("04 00 04 00 4D 00 FF 00 00 00 00 00 00 00"), AapPacket.HostCapabilities());
    }

    [Fact]
    public void Parses_battery_report()
    {
        var msg = Assert.IsType<AapBatteryMessage>(AapMessage.Parse(Hex("04 00 04 00 04 00 03 02 01 64 02 01 04 01 63 01 01 08 01 11 02 01")));

        Assert.Equal(3, msg.Batteries.Count);
        Assert.Equal(new AapBattery(AapBatteryComponent.Right, 100, AapBatteryStatus.Discharging), msg.Batteries[0]);
        Assert.Equal(new AapBattery(AapBatteryComponent.Left, 99, AapBatteryStatus.Charging), msg.Batteries[1]);
        Assert.Equal(new AapBattery(AapBatteryComponent.Case, 17, AapBatteryStatus.Discharging), msg.Batteries[2]);
    }

    [Fact]
    public void Parses_truncated_battery_report_without_throwing()
    {
        var msg = Assert.IsType<AapBatteryMessage>(AapMessage.Parse(Hex("04 00 04 00 04 00 03 02 01 64 02 01 04 01")));
        Assert.Single(msg.Batteries);
    }

    [Fact]
    public void Parses_ear_detection_and_controls()
    {
        Assert.Equal(new AapEarDetectionMessage(AapPodPlacement.InEar, AapPodPlacement.InCase), AapMessage.Parse(Hex("04 00 04 00 06 00 00 02")));
        Assert.Equal(new AapControlMessage(0x0D, 0x03, 0x00), AapMessage.Parse(Hex("04 00 04 00 09 00 0D 03 00 00 00")));
        Assert.Equal(new AapConversationLevelMessage(0x01), AapMessage.Parse(Hex("04 00 04 00 4B 00 02 00 01 01")));
    }

    [Fact]
    public void Parses_device_info_strings()
    {
        var packet = Hex("040004001d0002d5000400416972506f64732050726f004133303438004170706c6520496e632e0051584e524848595850360036312e3138363830343030303230303030303000");
        var info = Assert.IsType<AapDeviceInfoMessage>(AapMessage.Parse(packet));

        Assert.Equal("AirPods Pro", info.Name);
        Assert.Equal("A3048", info.ModelNumber);
        Assert.Equal("Apple Inc.", info.Manufacturer);
    }

    [Fact]
    public void Ignores_packets_without_header()
    {
        Assert.Null(AapMessage.Parse(Hex("00 00 04 00 01 00")));
        Assert.Null(AapMessage.Parse(Hex("04 00")));
    }

    [Fact]
    public async Task Session_handshakes_and_tracks_state()
    {
        var transport = new FakeTransport();
        using var session = new AapSession(transport);
        await session.StartAsync();

        Assert.True(transport.Opened);
        Assert.Equal(AapPacket.Handshake, transport.Sent[0]);
        Assert.Equal(AapPacket.RequestNotifications(), transport.Sent[^1]);
        Assert.True(session.State.Connected);

        transport.Receive("04 00 04 00 09 00 0D 03 00 00 00");
        transport.Receive("04 00 04 00 09 00 28 01 00 00 00");
        transport.Receive("04 00 04 00 04 00 03 02 01 50 02 01 04 01 4B 02 01 08 01 11 04 01");

        var state = session.State;
        Assert.Equal(NoiseControlMode.Transparency, state.NoiseControl);
        Assert.True(state.ConversationAwareness);
        Assert.Equal(80, state.Right!.Value.Level);
        Assert.Equal(75, state.Left!.Value.Level);
        Assert.Null(state.Case); // reported as disconnected

        await session.SetNoiseControlAsync(NoiseControlMode.NoiseCancellation);
        Assert.Equal(AapPacket.SetNoiseControl(NoiseControlMode.NoiseCancellation), transport.Sent[^1]);
        Assert.Equal(NoiseControlMode.NoiseCancellation, session.State.NoiseControl);

        transport.Close();
        Assert.False(session.State.Connected);
    }
}
