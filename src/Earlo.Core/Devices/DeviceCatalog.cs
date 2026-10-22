namespace Earlo.Core.Devices;

/// <summary>
/// Known Apple and Beats models, keyed by the model id as it appears in the advertisement,
/// plus Samsung Galaxy Buds under Earlo's own ids (see <see cref="Samsung.GalaxyBudsModels.CatalogId"/>).
/// </summary>
public static class DeviceCatalog
{
    static readonly Dictionary<ushort, DeviceInfo> Models = new DeviceInfo[]
    {
        new(0x0220, "AirPods", FormFactor.StemBuds),
        new(0x0F20, "AirPods (2nd generation)", FormFactor.StemBuds),
        new(0x1320, "AirPods (3rd generation)", FormFactor.StemBuds),
        new(0x1920, "AirPods 4", FormFactor.StemBuds),
        new(0x1B20, "AirPods 4 (ANC)", FormFactor.StemBuds, NoiseControl: NoiseControlSupport.Adaptive),
        new(0x0E20, "AirPods Pro", FormFactor.ProBuds, NoiseControl: NoiseControlSupport.Basic),
        new(0x1420, "AirPods Pro (2nd generation)", FormFactor.ProBuds, NoiseControl: NoiseControlSupport.Adaptive),
        new(0x2420, "AirPods Pro (2nd generation, USB-C)", FormFactor.ProBuds, NoiseControl: NoiseControlSupport.Adaptive),
        new(0x2720, "AirPods Pro 3", FormFactor.ProBuds, NoiseControl: NoiseControlSupport.Adaptive),
        new(0x0A20, "AirPods Max", FormFactor.Headphones, NoiseControl: NoiseControlSupport.Basic),
        new(0x1F20, "AirPods Max (USB-C)", FormFactor.Headphones, NoiseControl: NoiseControlSupport.Basic),

        new(0x0320, "Powerbeats3", FormFactor.Neckband, IsBeats: true),
        new(0x0520, "BeatsX", FormFactor.Neckband, IsBeats: true),
        new(0x0D20, "Powerbeats", FormFactor.Neckband, IsBeats: true),
        new(0x1020, "Beats Flex", FormFactor.Neckband, IsBeats: true),
        new(0x0B20, "Powerbeats Pro", FormFactor.HookBuds, IsBeats: true),
        new(0x1D20, "Powerbeats Pro 2", FormFactor.HookBuds, IsBeats: true, NoiseControl: NoiseControlSupport.Basic),
        new(0x1120, "Beats Studio Buds", FormFactor.RoundBuds, IsBeats: true, NoiseControl: NoiseControlSupport.Basic),
        new(0x1220, "Beats Fit Pro", FormFactor.RoundBuds, IsBeats: true, NoiseControl: NoiseControlSupport.Basic),
        new(0x1620, "Beats Studio Buds +", FormFactor.RoundBuds, IsBeats: true, NoiseControl: NoiseControlSupport.Basic),
        new(0x2620, "Beats Solo Buds", FormFactor.RoundBuds, IsBeats: true),
        new(0x0620, "Beats Solo3", FormFactor.Headphones, IsBeats: true),
        new(0x0920, "Beats Studio3", FormFactor.Headphones, IsBeats: true, NoiseControl: NoiseControlSupport.Basic),
        new(0x0C20, "Beats Solo Pro", FormFactor.Headphones, IsBeats: true, NoiseControl: NoiseControlSupport.Basic),
        new(0x1720, "Beats Studio Pro", FormFactor.Headphones, IsBeats: true, NoiseControl: NoiseControlSupport.Basic),
        new(0x2520, "Beats Solo 4", FormFactor.Headphones, IsBeats: true),

        new(0x5300, "Galaxy Buds", FormFactor.RoundBuds, IsSamsung: true),
        new(0x5301, "Galaxy Buds+", FormFactor.RoundBuds, IsSamsung: true),
        new(0x5302, "Galaxy Buds Live", FormFactor.RoundBuds, IsSamsung: true),
        new(0x5303, "Galaxy Buds Pro", FormFactor.RoundBuds, IsSamsung: true),
        new(0x5304, "Galaxy Buds2", FormFactor.RoundBuds, IsSamsung: true),
        new(0x5305, "Galaxy Buds2 Pro", FormFactor.RoundBuds, IsSamsung: true),
        new(0x5306, "Galaxy Buds FE", FormFactor.RoundBuds, IsSamsung: true),
        new(0x5307, "Galaxy Buds3", FormFactor.StemBuds, IsSamsung: true),
        new(0x5308, "Galaxy Buds3 Pro", FormFactor.StemBuds, IsSamsung: true),
        new(0x5309, "Galaxy Buds3 FE", FormFactor.StemBuds, IsSamsung: true),
        new(0x530A, "Galaxy Buds Core", FormFactor.RoundBuds, IsSamsung: true),
    }.ToDictionary(d => d.ModelId);

    public static IReadOnlyCollection<DeviceInfo> All => Models.Values;

    public static bool TryGet(ushort modelId, out DeviceInfo info) => Models.TryGetValue(modelId, out info!);

    /// <summary>
    /// Returns the catalog entry, or a best guess for models released after this build.
    /// Unknown models still get battery info; the form factor is inferred from what they report.
    /// </summary>
    public static DeviceInfo Resolve(ushort modelId, bool reportsPods)
    {
        if (Models.TryGetValue(modelId, out var info))
            return info;

        var form = reportsPods ? FormFactor.ProBuds : FormFactor.Headphones;
        return new DeviceInfo(modelId, $"Apple headphones (0x{modelId:X4})", form, IsKnown: false);
    }
}
