namespace PodPilot.Core.Samsung;

/// <summary>Where a Galaxy Bud is, as the buds report it.</summary>
public enum BudPlacement
{
    Disconnected = 0,
    Wearing = 1,
    /// <summary>Out of the ear, not in the case.</summary>
    Idle = 2,
    /// <summary>In the case with the lid open.</summary>
    InOpenCase = 3,
    InClosedCase = 4,
}

/// <param name="Case">Null when the case has not reported (it only does while a bud is in it).</param>
public sealed record GalaxyBudsStatus(
    int Left, int Right, int? Case,
    bool LeftCharging, bool RightCharging, bool CaseCharging,
    BudPlacement LeftPlacement, BudPlacement RightPlacement);

/// <summary>
/// Framing of the Galaxy Buds serial protocol:
/// <c>SOM | header | id | payload | CRC16 (little endian) | EOM</c>.
/// Only the facts needed for battery and wearing state are implemented.
/// </summary>
public static class GalaxyBudsMessage
{
    public const byte StatusUpdated = 0x60;
    public const byte ExtendedStatusUpdated = 0x61;
    public const byte ManagerInfo = 0x88;

    const byte Som = 0xFD, Eom = 0xDD, LegacySom = 0xFE, LegacyEom = 0xEE;

    /// <summary>CRC-16/XMODEM (polynomial 0x1021, start 0) over id and payload.</summary>
    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (byte b in data)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    public static byte[] Build(byte id, ReadOnlySpan<byte> payload, bool legacy = false)
    {
        int size = payload.Length + 3; // id + payload + CRC
        var frame = new byte[size + 4];
        frame[0] = legacy ? LegacySom : Som;
        if (legacy)
        {
            frame[1] = 0; // request
            frame[2] = (byte)size;
        }
        else
        {
            frame[1] = (byte)size;
            frame[2] = (byte)(size >> 8);
        }
        frame[3] = id;
        payload.CopyTo(frame.AsSpan(4));
        ushort crc = Crc16(frame.AsSpan(3, payload.Length + 1));
        frame[4 + payload.Length] = (byte)crc;
        frame[5 + payload.Length] = (byte)(crc >> 8);
        frame[^1] = legacy ? LegacyEom : Eom;
        return frame;
    }

    /// <summary>The reply the official app sends after each extended status: version 1, Samsung client, Android 14.</summary>
    public static byte[] ManagerInfoReply(bool legacy) => Build(ManagerInfo, [0x01, 0x01, 0x22], legacy);

    /// <summary>
    /// Reads battery and placement from a status message. Offsets are payload offsets
    /// (the byte after the id is 0). Returns null for other messages or short payloads.
    /// </summary>
    public static GalaxyBudsStatus? ParseStatus(GalaxyBudsModel model, byte id, ReadOnlySpan<byte> p)
    {
        bool legacy = model == GalaxyBudsModel.Buds;
        if (id == ExtendedStatusUpdated)
        {
            if (p.Length < (legacy ? 7 : 8))
                return null;
            int revision = p[0];
            var (left, right) = legacy ? LegacyWear(p[6]) : Nibbles(p[6]);
            int? @case = legacy ? null : Percent(p[7]);
            int chargingOffset = model switch
            {
                GalaxyBudsModel.Buds2 when revision >= 10 => 36,
                GalaxyBudsModel.Buds2Pro when revision >= 11 => 43,
                GalaxyBudsModel.BudsFe or GalaxyBudsModel.BudsCore or GalaxyBudsModel.Buds3Fe => 43,
                GalaxyBudsModel.Buds3 or GalaxyBudsModel.Buds3Pro => 42,
                _ => -1,
            };
            return Build(p[2], p[3], @case, Charging(p, chargingOffset), left, right);
        }

        if (id == StatusUpdated)
        {
            if (legacy)
            {
                if (p.Length < 6)
                    return null;
                var (l, r) = LegacyWear(p[5]);
                return Build(p[1], p[2], null, 0, l, r);
            }
            if (p.Length < 7)
                return null;
            var (left, right) = Nibbles(p[5]);
            bool reportsCharging = model switch
            {
                GalaxyBudsModel.BudsPlus or GalaxyBudsModel.BudsLive or GalaxyBudsModel.BudsPro => false,
                GalaxyBudsModel.Buds2Pro => p[0] >= 1,
                _ => true,
            };
            return Build(p[1], p[2], Percent(p[6]), Charging(p, reportsCharging ? 7 : -1), left, right);
        }

        return null;
    }

    static GalaxyBudsStatus Build(byte left, byte right, int? @case, int charging, BudPlacement l, BudPlacement r) =>
        new(Math.Min((int)left, 100), Math.Min((int)right, 100), @case,
            (charging & 0x10) != 0, (charging & 0x04) != 0, (charging & 0x01) != 0, l, r);

    static int Charging(ReadOnlySpan<byte> p, int offset) => offset >= 0 && offset < p.Length ? p[offset] : 0;

    /// <summary>Above 100 means the case has no data.</summary>
    static int? Percent(byte value) => value <= 100 ? value : null;

    static (BudPlacement, BudPlacement) Nibbles(byte value) => (Placement(value >> 4), Placement(value & 0x0F));

    static BudPlacement Placement(int value) => value is >= 0 and <= 4 ? (BudPlacement)value : BudPlacement.Disconnected;

    /// <summary>The 2019 Buds only report wearing: 0 none, 1 right, 16 left, 17 both.</summary>
    static (BudPlacement, BudPlacement) LegacyWear(byte value) =>
        ((value & 0x10) != 0 ? BudPlacement.Wearing : BudPlacement.Idle,
         (value & 0x01) != 0 ? BudPlacement.Wearing : BudPlacement.Idle);

    /// <summary>Splits a byte stream into messages. Partial frames wait for more data; corrupt ones are skipped.</summary>
    public sealed class Reader(bool legacy)
    {
        readonly List<byte> _buffer = [];

        public IEnumerable<(byte Id, byte[] Payload)> Append(ReadOnlySpan<byte> data)
        {
            _buffer.AddRange(data.ToArray());
            var messages = new List<(byte, byte[])>();
            byte som = legacy ? LegacySom : Som, eom = legacy ? LegacyEom : Eom;

            while (true)
            {
                int start = _buffer.IndexOf(som);
                if (start < 0)
                {
                    _buffer.Clear();
                    break;
                }
                if (start > 0)
                    _buffer.RemoveRange(0, start);
                if (_buffer.Count < 3)
                    break;

                int size = legacy ? _buffer[2] : (_buffer[1] | (_buffer[2] << 8)) & 0x3FF;
                if (size < 3)
                {
                    _buffer.RemoveAt(0);
                    continue;
                }
                if (_buffer.Count < size + 4)
                    break;

                var frame = _buffer.GetRange(0, size + 4).ToArray();
                var body = frame.AsSpan(3, size - 2);
                ushort crc = (ushort)(frame[size + 1] | (frame[size + 2] << 8));
                if (frame[^1] != eom || Crc16(body) != crc)
                {
                    _buffer.RemoveAt(0); // not a real frame start, look for the next one
                    continue;
                }
                _buffer.RemoveRange(0, size + 4);
                messages.Add((body[0], body[1..].ToArray()));
            }
            return messages;
        }
    }
}
