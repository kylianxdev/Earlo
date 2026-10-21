namespace PodPilot.Core.Aap;

/// <summary>Noise control ("listening mode") values used by control identifier 0x0D.</summary>
public enum NoiseControlMode : byte
{
    Off = 0x01,
    NoiseCancellation = 0x02,
    Transparency = 0x03,
    Adaptive = 0x04,
}

/// <summary>Known AAP opcodes (16-bit, little endian on the wire).</summary>
public static class AapOpcode
{
    public const ushort Battery = 0x0004;
    public const ushort EarDetection = 0x0006;
    public const ushort Control = 0x0009;
    public const ushort RequestNotifications = 0x000F;
    public const ushort DeviceInfo = 0x001D;
    public const ushort ConversationAwareness = 0x004B;
    public const ushort HostCapabilities = 0x004D;
}

/// <summary>Identifiers of control commands (opcode 0x09).</summary>
public static class AapControl
{
    public const byte EarDetection = 0x0A;
    public const byte ListeningMode = 0x0D;
    public const byte ListeningModeConfigs = 0x1A;
    public const byte OneBudAnc = 0x1B;
    public const byte VolumeSwipeInterval = 0x23;
    public const byte VolumeSwipe = 0x25;
    public const byte ConversationDetect = 0x28;
    public const byte AdaptiveStrength = 0x2E;
}

/// <summary>
/// Builds packets for Apple's accessory protocol (AAP), which AirPods speak over Bluetooth
/// L2CAP channel PSM 0x1001. Every message is <c>04 00 04 00 [opcode LE] [payload]</c>.
/// </summary>
/// <remarks>
/// Byte formats follow the public reverse-engineering notes of the LibrePods project
/// (docs/AAP Definitions.md, docs/control_commands.md).
/// </remarks>
public static class AapPacket
{
    public const ushort Psm = 0x1001;

    /// <summary>Must be sent first; AirPods ignore everything until they receive it.</summary>
    public static readonly byte[] Handshake = [0x00, 0x00, 0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    /// <summary>Announces host features; enables Adaptive mode and Conversation Awareness while playing.</summary>
    public static byte[] HostCapabilities() => Message(AapOpcode.HostCapabilities, [0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

    /// <summary>Asks for battery, ear detection, noise control and other notifications.</summary>
    public static byte[] RequestNotifications() => Message(AapOpcode.RequestNotifications, [0xFF, 0xFF, 0xFF, 0xFF]);

    public static byte[] SetNoiseControl(NoiseControlMode mode) => Control(AapControl.ListeningMode, (byte)mode);

    public static byte[] SetConversationAwareness(bool enabled) => Control(AapControl.ConversationDetect, enabled ? (byte)0x01 : (byte)0x02);

    /// <summary>
    /// Swipe up/down on the stem to change the volume (AirPods Pro 2 and later). Apple devices switch this on
    /// over AAP; without it the AirPods ignore swipes on other hosts.
    /// </summary>
    public static byte[] SetVolumeSwipe(bool enabled) => Control(AapControl.VolumeSwipe, enabled ? (byte)0x01 : (byte)0x02);

    /// <summary>How strongly Adaptive mode filters noise, 0 (most ambient sound) to 100.</summary>
    public static byte[] SetAdaptiveStrength(int level) => Control(AapControl.AdaptiveStrength, (byte)Math.Clamp(level, 0, 100));

    /// <summary>Control command: <c>04 00 04 00 09 00 [id] [v1] [v2] 00 00</c>.</summary>
    public static byte[] Control(byte identifier, byte value1, byte value2 = 0) =>
        Message(AapOpcode.Control, [identifier, value1, value2, 0x00, 0x00]);

    public static byte[] Message(ushort opcode, ReadOnlySpan<byte> payload)
    {
        var packet = new byte[6 + payload.Length];
        packet[0] = 0x04;
        packet[2] = 0x04;
        packet[4] = (byte)opcode;
        packet[5] = (byte)(opcode >> 8);
        payload.CopyTo(packet.AsSpan(6));
        return packet;
    }
}
