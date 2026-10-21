using System.Text;

namespace PodPilot.Core.Aap;

public enum AapBatteryComponent : byte
{
    Single = 0x01,
    Right = 0x02,
    Left = 0x04,
    Case = 0x08,
}

public enum AapBatteryStatus : byte
{
    Unknown = 0x00,
    Charging = 0x01,
    Discharging = 0x02,
    Disconnected = 0x04,
}

public enum AapPodPlacement : byte
{
    InEar = 0x00,
    OutOfEar = 0x01,
    InCase = 0x02,
}

public readonly record struct AapBattery(AapBatteryComponent Component, int Level, AapBatteryStatus Status);

/// <summary>A message received from the AirPods.</summary>
public abstract record AapMessage(ushort Opcode)
{
    /// <summary>Parses a received packet. Returns null for packets without the AAP header.</summary>
    public static AapMessage? Parse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 6 || packet[0] != 0x04 || packet[1] != 0x00 || packet[2] != 0x04 || packet[3] != 0x00)
            return null;

        ushort opcode = (ushort)(packet[4] | (packet[5] << 8));
        var data = packet[6..];

        return opcode switch
        {
            AapOpcode.Battery => AapBatteryMessage.ParsePayload(data),
            AapOpcode.EarDetection when data.Length >= 2 =>
                new AapEarDetectionMessage((AapPodPlacement)data[0], (AapPodPlacement)data[1]),
            AapOpcode.Control when data.Length >= 3 =>
                new AapControlMessage(data[0], data[1], data[2]),
            AapOpcode.ConversationAwareness when data.Length >= 4 =>
                new AapConversationLevelMessage(data[3]),
            AapOpcode.DeviceInfo => AapDeviceInfoMessage.ParsePayload(data),
            _ => new AapUnknownMessage(opcode, data.ToArray()),
        };
    }
}

/// <summary>Exact battery levels in 1% steps: <c>[count] ([component] 01 [level] [status] 01)*</c>.</summary>
public sealed record AapBatteryMessage(IReadOnlyList<AapBattery> Batteries) : AapMessage(AapOpcode.Battery)
{
    internal static AapMessage ParsePayload(ReadOnlySpan<byte> data)
    {
        if (data.Length < 1)
            return new AapUnknownMessage(AapOpcode.Battery, data.ToArray());

        int count = data[0];
        var list = new List<AapBattery>(count);
        for (int i = 0; i < count && 1 + (i * 5) + 4 <= data.Length; i++)
        {
            var entry = data.Slice(1 + (i * 5), 5);
            list.Add(new AapBattery((AapBatteryComponent)entry[0], Math.Clamp((int)entry[2], 0, 100), (AapBatteryStatus)entry[3]));
        }
        return new AapBatteryMessage(list);
    }
}

/// <summary>In-ear state of the primary pod (the one with the active microphone) and the other one.</summary>
public sealed record AapEarDetectionMessage(AapPodPlacement Primary, AapPodPlacement Secondary) : AapMessage(AapOpcode.EarDetection);

/// <summary>A setting reported by the AirPods, for example the current noise control mode.</summary>
public sealed record AapControlMessage(byte Identifier, byte Value1, byte Value2) : AapMessage(AapOpcode.Control);

/// <summary>Sent while Conversation Awareness ducks audio: 1-2 started speaking, 3 stopped, 8-9 normal volume.</summary>
public sealed record AapConversationLevelMessage(byte Level) : AapMessage(AapOpcode.ConversationAwareness);

public sealed record AapDeviceInfoMessage(string Name, string ModelNumber, string Manufacturer, string SerialNumber, string Firmware)
    : AapMessage(AapOpcode.DeviceInfo)
{
    internal static AapMessage ParsePayload(ReadOnlySpan<byte> data)
    {
        // The strings start after a few header bytes; find the first printable run.
        int start = 0;
        while (start < data.Length && (data[start] < 0x20 || data[start] > 0x7E))
            start++;

        var strings = new List<string>();
        int pos = start;
        while (pos < data.Length && strings.Count < 5)
        {
            int end = data[pos..].IndexOf((byte)0);
            if (end < 0)
                end = data.Length - pos;
            strings.Add(Encoding.UTF8.GetString(data.Slice(pos, end)));
            pos += end + 1;
        }

        while (strings.Count < 5)
            strings.Add(string.Empty);
        return new AapDeviceInfoMessage(strings[0], strings[1], strings[2], strings[3], strings[4]);
    }
}

public sealed record AapUnknownMessage(ushort RawOpcode, byte[] Data) : AapMessage(RawOpcode);
