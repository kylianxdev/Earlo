namespace Earlo.Core.Protocol;

/// <summary>Builds raw proximity pairing payloads. Used by demo mode, previews and tests.</summary>
public static class ProximityPairingPayload
{
    public static byte[] Build(
        ushort modelId,
        int? left, int? right, int? caseBattery,
        bool leftCharging = false, bool rightCharging = false, bool caseCharging = false,
        bool leftInEar = false, bool rightInEar = false,
        bool bothInCase = false, bool lidOpen = false, byte color = 0,
        bool leftInCase = false, bool rightInCase = false)
    {
        // Always encode as "left is primary" so no fields are swapped.
        byte status = 0x20;
        if (leftInEar) status |= 0x02;
        if (rightInEar) status |= 0x08;
        if (bothInCase || (leftInCase && rightInCase)) status |= 0x04 | 0x40;
        else if (leftInCase) status |= 0x40;
        else if (rightInCase) status |= 0x10;

        int charge = (leftCharging ? 0x1 : 0) | (rightCharging ? 0x2 : 0) | (caseCharging ? 0x4 : 0);

        var data = new byte[27];
        data[0] = ProximityPairingMessage.MessageType;
        data[1] = 25;
        data[2] = 0x01;
        data[3] = (byte)(modelId >> 8);
        data[4] = (byte)modelId;
        data[5] = status;
        data[6] = (byte)((Nibble(right) << 4) | Nibble(left));
        data[7] = (byte)((charge << 4) | Nibble(caseBattery));
        data[8] = (byte)(lidOpen ? 0x01 : 0x09);
        data[9] = color;
        return data;
    }

    static int Nibble(int? percent) => percent is int p ? Math.Clamp(p / 10, 0, 10) : 0x0F;
}
