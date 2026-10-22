using Earlo.Core.Devices;
using Earlo.Core.Protocol;

namespace Earlo.Core.Tests;

public class ProximityPairingMessageTests
{
    static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));

    [Fact]
    public void Parses_airpods_pro_2_worn_with_right_pod_broadcasting()
    {
        // Status 0x0B: bit 5 clear, so the right pod broadcasts and the fields are swapped.
        var data = Hex("07 19 01 14 20 0B 79 8F 01 00 04 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");

        Assert.True(ProximityPairingMessage.TryParse(data, out var msg));
        Assert.Equal(0x1420, msg.ModelId);
        Assert.False(msg.LeftIsPrimary);
        Assert.Equal(75, msg.LeftBattery);
        Assert.Equal(95, msg.RightBattery);
        Assert.Null(msg.CaseBattery);
        Assert.True(msg.LeftInEar);
        Assert.True(msg.RightInEar);
        Assert.Equal("AirPods Pro (2nd generation)", DeviceCatalog.Resolve(msg.ModelId, true).Name);
    }

    [Fact]
    public void Swaps_sides_depending_on_broadcasting_pod()
    {
        var leftPrimary = Hex("07 19 01 0E 20 22 39 1F 09 00");
        var rightPrimary = Hex("07 19 01 0E 20 08 93 2F 09 00");

        Assert.True(ProximityPairingMessage.TryParse(leftPrimary, out var a));
        Assert.True(ProximityPairingMessage.TryParse(rightPrimary, out var b));

        Assert.Equal((95, 35), (a.LeftBattery, a.RightBattery));
        Assert.Equal((95, 35), (b.LeftBattery, b.RightBattery));
        Assert.True(a.LeftInEar);
        Assert.False(a.RightInEar);
        Assert.True(b.LeftInEar);
        Assert.False(b.RightInEar);
        Assert.True(a.LeftCharging);
        Assert.True(b.LeftCharging);
        Assert.False(a.RightCharging);
        Assert.False(b.RightCharging);
    }

    [Fact]
    public void Reads_case_battery_charging_and_lid()
    {
        var data = ProximityPairingPayload.Build(0x1920, 100, 80, 50, caseCharging: true, bothInCase: true, lidOpen: true);

        Assert.True(ProximityPairingMessage.TryParse(data, out var msg));
        Assert.Equal(55, msg.CaseBattery);
        Assert.True(msg.CaseCharging);
        Assert.True(msg.BothInCase);
        Assert.True(msg.LidOpen);
        Assert.Equal(100, msg.LeftBattery);
        Assert.Equal(85, msg.RightBattery);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Reads_which_pod_is_in_the_case(bool left, bool right)
    {
        var data = ProximityPairingPayload.Build(0x1420, 80, 70, 50, leftInCase: left, rightInCase: right);
        Assert.True(ProximityPairingMessage.TryParse(data, out var msg));
        Assert.Equal((left, right), (msg.LeftInCase, msg.RightInCase));
    }

    [Fact]
    public void In_case_follows_the_broadcasting_pod()
    {
        // Status 0x50: right pod broadcasts (bit 5 clear) and is in the case (bit 6); left is out.
        Assert.True(ProximityPairingMessage.TryParse(Hex("07 19 01 14 20 40 78 8F 01 00"), out var msg));
        Assert.True(msg.RightInCase);
        Assert.False(msg.LeftInCase);
    }

    [Fact]
    public void Lid_closed_when_bit_3_set()
    {
        var data = ProximityPairingPayload.Build(0x1920, 100, 80, 50, bothInCase: true, lidOpen: false);
        Assert.True(ProximityPairingMessage.TryParse(data, out var msg));
        Assert.False(msg.LidOpen);
    }

    [Fact]
    public void Unknown_nibble_is_null()
    {
        var data = Hex("07 19 01 0A 20 20 F8 0F 09 00");
        Assert.True(ProximityPairingMessage.TryParse(data, out var msg));
        Assert.Equal(85, msg.SingleBattery);
        Assert.Null(msg.RightBattery);
    }

    [Theory]
    [InlineData("")]
    [InlineData("07 19 01 0E 20")]
    [InlineData("10 05 01 0E 20 22 39 1F 09 00")]
    public void Rejects_other_messages(string hex)
    {
        Assert.False(ProximityPairingMessage.TryParse(Hex(hex), out _));
    }

    [Fact]
    public void Unknown_models_still_resolve()
    {
        var buds = DeviceCatalog.Resolve(0x9920, reportsPods: true);
        var cans = DeviceCatalog.Resolve(0x9921, reportsPods: false);

        Assert.False(buds.IsKnown);
        Assert.True(buds.HasCase);
        Assert.True(cans.HasSingleBattery);
    }

    [Fact]
    public void Catalog_has_no_duplicate_names()
    {
        var names = DeviceCatalog.All.Select(d => d.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
