using System.Diagnostics.CodeAnalysis;

namespace PodPilot.Core.Protocol;

/// <summary>
/// Apple Continuity "Proximity Pairing" message (type 0x07). AirPods and Beats broadcast it over
/// Bluetooth LE as manufacturer data for company 0x004C, both while connected and when the case is opened.
/// </summary>
/// <remarks>
/// Payload layout (after the company id):
/// <code>
/// [0] 0x07 type      [1] remaining length   [2] prefix
/// [3..4] model id    [5] status             [6] pod batteries (two nibbles)
/// [7] charge flags (high nibble) + case battery (low nibble)
/// [8] lid indicator  [9] color              [10..] encrypted, unused here
/// </code>
/// Status bit 5 tells which pod is broadcasting. When it is clear the left/right fields are swapped.
/// </remarks>
public sealed record ProximityPairingMessage
{
    public const ushort AppleCompanyId = 0x004C;
    public const byte MessageType = 0x07;
    public const int MinimumLength = 10;

    public required ushort ModelId { get; init; }
    public required byte Status { get; init; }
    public required byte Color { get; init; }

    /// <summary>True when the left pod is the one broadcasting (fields are not swapped).</summary>
    public required bool LeftIsPrimary { get; init; }

    public required int? LeftBattery { get; init; }
    public required int? RightBattery { get; init; }
    public required int? CaseBattery { get; init; }

    /// <summary>Battery for single-battery products such as AirPods Max and Beats headphones.</summary>
    public required int? SingleBattery { get; init; }

    public required bool LeftCharging { get; init; }
    public required bool RightCharging { get; init; }
    public required bool CaseCharging { get; init; }
    public required bool SingleCharging { get; init; }

    public required bool LeftInEar { get; init; }
    public required bool RightInEar { get; init; }

    public required bool PrimaryInCase { get; init; }
    public required bool BothInCase { get; init; }
    public required bool OneInCase { get; init; }

    public required bool LidOpen { get; init; }

    public bool AnyPodInCase => PrimaryInCase || BothInCase || OneInCase;

    public bool ReportsPods => LeftBattery.HasValue || RightBattery.HasValue || CaseBattery.HasValue;

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ProximityPairingMessage? message)
    {
        message = null;
        if (data.Length < MinimumLength || data[0] != MessageType)
            return false;

        byte status = data[5];
        byte batteries = data[6];
        byte flags = data[7];
        byte lid = data[8];

        bool leftPrimary = (status & 0x20) != 0;
        int low = batteries & 0x0F;
        int high = batteries >> 4;
        int charge = flags >> 4;

        message = new ProximityPairingMessage
        {
            ModelId = (ushort)((data[3] << 8) | data[4]),
            Status = status,
            Color = data[9],
            LeftIsPrimary = leftPrimary,
            LeftBattery = ToPercent(leftPrimary ? low : high),
            RightBattery = ToPercent(leftPrimary ? high : low),
            CaseBattery = ToPercent(flags & 0x0F),
            SingleBattery = ToPercent(low != 0x0F ? low : high),
            LeftCharging = (charge & (leftPrimary ? 0x1 : 0x2)) != 0,
            RightCharging = (charge & (leftPrimary ? 0x2 : 0x1)) != 0,
            CaseCharging = (charge & 0x4) != 0,
            SingleCharging = (charge & 0x1) != 0,
            LeftInEar = (status & (leftPrimary ? 0x02 : 0x08)) != 0,
            RightInEar = (status & (leftPrimary ? 0x08 : 0x02)) != 0,
            PrimaryInCase = (status & 0x40) != 0,
            OneInCase = (status & 0x10) != 0,
            BothInCase = (status & 0x04) != 0,
            LidOpen = (lid & 0x08) == 0,
        };
        return true;
    }

    /// <summary>
    /// Battery nibbles are 0..10 where n means "n*10 to n*10+9 percent"; 0xF means "not reported".
    /// We show the middle of that range (8 becomes 85%), which is closest to the real level on average.
    /// </summary>
    static int? ToPercent(int nibble) => nibble switch
    {
        10 => 100,
        < 10 => (nibble * 10) + 5,
        _ => null,
    };
}
