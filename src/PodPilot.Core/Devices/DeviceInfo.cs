namespace PodPilot.Core.Devices;

public enum FormFactor
{
    /// <summary>AirPods with a long stem (1st to 4th generation).</summary>
    StemBuds,
    /// <summary>AirPods Pro: short stem with an ear tip.</summary>
    ProBuds,
    /// <summary>Stemless buds such as Beats Studio Buds and Fit Pro.</summary>
    RoundBuds,
    /// <summary>Buds with an ear hook (Powerbeats Pro).</summary>
    HookBuds,
    /// <summary>Over-ear or on-ear headphones with a single battery.</summary>
    Headphones,
    /// <summary>Buds joined by a cable or neckband, single battery.</summary>
    Neckband,
}

public sealed record DeviceInfo(ushort ModelId, string Name, FormFactor FormFactor, bool IsBeats = false, bool IsKnown = true)
{
    /// <summary>Single-battery products report one level instead of left/right/case.</summary>
    public bool HasSingleBattery => FormFactor is FormFactor.Headphones or FormFactor.Neckband;

    public bool HasCase => !HasSingleBattery;

    public bool SupportsEarDetection => !HasSingleBattery;
}
