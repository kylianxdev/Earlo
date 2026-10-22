namespace Earlo.Core.Samsung;

public enum GalaxyBudsModel
{
    Buds,
    BudsPlus,
    BudsLive,
    BudsPro,
    Buds2,
    Buds2Pro,
    BudsFe,
    Buds3,
    Buds3Pro,
    Buds3Fe,
    BudsCore,
}

/// <summary>Per-model facts of the Galaxy Buds serial (RFCOMM) protocol.</summary>
public static class GalaxyBudsModels
{
    /// <summary>Galaxy Buds (2019) uses its own service id.</summary>
    public static readonly Guid LegacyService = new("00001102-0000-1000-8000-00805f9b34fd");

    /// <summary>Standard serial port profile, used by Buds+, Buds Live and Buds Pro.</summary>
    public static readonly Guid SerialPortService = new("00001101-0000-1000-8000-00805f9b34fb");

    /// <summary>Samsung's own service on Buds2 and newer.</summary>
    public static readonly Guid SamsungService = new("2e73a4ad-332d-41fc-90e2-16bef06523f2");

    /// <summary>
    /// Picks the model from the Bluetooth name, e.g. "Galaxy Buds2 Pro (A1B2)".
    /// Pro, FE and Live names are checked before the plain ones they contain.
    /// </summary>
    public static GalaxyBudsModel? FromName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        bool Has(string part) => name.Contains(part, StringComparison.OrdinalIgnoreCase);

        if (Has("Buds3 Pro")) return GalaxyBudsModel.Buds3Pro;
        if (Has("Buds3 FE")) return GalaxyBudsModel.Buds3Fe;
        if (Has("Buds3")) return GalaxyBudsModel.Buds3;
        if (Has("Buds2 Pro")) return GalaxyBudsModel.Buds2Pro;
        if (Has("Buds2")) return GalaxyBudsModel.Buds2;
        if (Has("Buds FE")) return GalaxyBudsModel.BudsFe;
        if (Has("Buds Core")) return GalaxyBudsModel.BudsCore;
        if (Has("Buds Pro")) return GalaxyBudsModel.BudsPro;
        if (Has("Buds Live")) return GalaxyBudsModel.BudsLive;
        if (Has("Buds+")) return GalaxyBudsModel.BudsPlus;
        if (Has("Galaxy Buds")) return GalaxyBudsModel.Buds;
        return null;
    }

    /// <summary>Like <see cref="FromName"/>, but ignores names other brands share ("Pixel Buds Pro").</summary>
    public static GalaxyBudsModel? FromPairedName(string? name)
    {
        var model = FromName(name);
        bool generic = model is GalaxyBudsModel.BudsPro or GalaxyBudsModel.BudsLive;
        return generic && !name!.Contains("Galaxy", StringComparison.OrdinalIgnoreCase) ? null : model;
    }

    public static Guid ServiceFor(GalaxyBudsModel model) => model switch
    {
        GalaxyBudsModel.Buds => LegacyService,
        GalaxyBudsModel.BudsPlus or GalaxyBudsModel.BudsLive or GalaxyBudsModel.BudsPro => SerialPortService,
        _ => SamsungService,
    };

    /// <summary>The 2019 Buds frame messages with FE/EE and a shorter header; all later models use FD/DD.</summary>
    public static bool UsesLegacyFraming(GalaxyBudsModel model) => model == GalaxyBudsModel.Buds;

    /// <summary>The id Earlo uses for the model in <see cref="Devices.DeviceCatalog"/>.</summary>
    public static ushort CatalogId(GalaxyBudsModel model) => (ushort)(0x5300 + (int)model);
}
