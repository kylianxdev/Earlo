using PodPilot.Core.Samsung;

namespace PodPilot.Core.Tests;

public class GalaxyBudsTests
{
    [Fact]
    public void Builds_the_manager_info_reply_with_crc_and_markers()
    {
        var frame = GalaxyBudsMessage.ManagerInfoReply(legacy: false);
        Assert.Equal(Convert.FromHexString("FD06008801012 2DA58DD".Replace(" ", "")), frame);
    }

    [Fact]
    public void Reader_splits_frames_and_skips_noise()
    {
        var first = GalaxyBudsMessage.Build(0x60, [1, 2, 3]);
        var second = GalaxyBudsMessage.Build(0x61, [4, 5]);
        var stream = new byte[] { 0x00, 0x13 }.Concat(first).Concat(second).ToArray();
        var reader = new GalaxyBudsMessage.Reader(legacy: false);

        // Arrives in two pieces, the second frame cut in half.
        var messages = reader.Append(stream.AsSpan(0, stream.Length - 3)).ToList();
        messages.AddRange(reader.Append(stream.AsSpan(stream.Length - 3)));

        Assert.Equal(2, messages.Count);
        Assert.Equal(0x60, messages[0].Id);
        Assert.Equal(new byte[] { 1, 2, 3 }, messages[0].Payload);
        Assert.Equal(0x61, messages[1].Id);
        Assert.Equal(new byte[] { 4, 5 }, messages[1].Payload);
    }

    [Fact]
    public void Reader_rejects_a_bad_crc()
    {
        var frame = GalaxyBudsMessage.Build(0x60, [1, 2, 3]);
        frame[4] ^= 0xFF;
        Assert.Empty(new GalaxyBudsMessage.Reader(false).Append(frame));
    }

    [Fact]
    public void Parses_buds2_extended_status_with_charging()
    {
        var p = new byte[39];
        p[0] = 10;   // revision
        p[2] = 64;   // left
        p[3] = 71;   // right
        p[6] = 0x13; // left wearing, right in open case
        p[7] = 51;   // case
        p[36] = 0x05; // right and case charging

        var status = GalaxyBudsMessage.ParseStatus(GalaxyBudsModel.Buds2, GalaxyBudsMessage.ExtendedStatusUpdated, p)!;

        Assert.Equal(64, status.Left);
        Assert.Equal(71, status.Right);
        Assert.Equal(51, status.Case);
        Assert.False(status.LeftCharging);
        Assert.True(status.RightCharging);
        Assert.True(status.CaseCharging);
        Assert.Equal(BudPlacement.Wearing, status.LeftPlacement);
        Assert.Equal(BudPlacement.InOpenCase, status.RightPlacement);
    }

    [Fact]
    public void Case_level_above_100_means_unknown()
    {
        var p = new byte[] { 1, 0, 90, 80, 1, 1, 0x22, 101 };
        var status = GalaxyBudsMessage.ParseStatus(GalaxyBudsModel.BudsPlus, GalaxyBudsMessage.ExtendedStatusUpdated, p)!;
        Assert.Null(status.Case);
        Assert.Equal(BudPlacement.Idle, status.LeftPlacement);
    }

    [Fact]
    public void Parses_status_update_battery_change()
    {
        var p = new byte[] { 1, 55, 56, 0, 0, 0x11, 40, 0x10 };
        var status = GalaxyBudsMessage.ParseStatus(GalaxyBudsModel.Buds3Pro, GalaxyBudsMessage.StatusUpdated, p)!;
        Assert.Equal(55, status.Left);
        Assert.Equal(56, status.Right);
        Assert.Equal(40, status.Case);
        Assert.True(status.LeftCharging);
        Assert.Equal(BudPlacement.Wearing, status.RightPlacement);
    }

    [Theory]
    [InlineData("Galaxy Buds2 Pro (A1B2)", GalaxyBudsModel.Buds2Pro)]
    [InlineData("Galaxy Buds2 (A1B2)", GalaxyBudsModel.Buds2)]
    [InlineData("Galaxy Buds3 Pro", GalaxyBudsModel.Buds3Pro)]
    [InlineData("Galaxy Buds+ (1234)", GalaxyBudsModel.BudsPlus)]
    [InlineData("Galaxy Buds (1234)", GalaxyBudsModel.Buds)]
    [InlineData("Galaxy Buds Live (1234)", GalaxyBudsModel.BudsLive)]
    public void Recognizes_models_by_name(string name, GalaxyBudsModel model) =>
        Assert.Equal(model, GalaxyBudsModels.FromPairedName(name));

    [Theory]
    [InlineData("Pixel Buds Pro")]
    [InlineData("AirPods Pro")]
    [InlineData(null)]
    public void Ignores_other_brands(string? name) => Assert.Null(GalaxyBudsModels.FromPairedName(name));

    [Fact]
    public void Snapshot_reports_open_case_and_hides_disconnected_bud()
    {
        var status = new GalaxyBudsStatus(80, 0, 60, false, false, true, BudPlacement.InOpenCase, BudPlacement.Disconnected);
        var snapshot = GalaxyBudsManager.ToSnapshot(GalaxyBudsModel.Buds2Pro, status);

        Assert.Equal("Galaxy Buds2 Pro", snapshot.Device.Name);
        Assert.True(snapshot.LidOpen);
        Assert.True(snapshot.LeftInCase);
        Assert.Equal(80, snapshot.Left.Percent);
        Assert.False(snapshot.Right.IsKnown);
        Assert.True(snapshot.Case.Charging);
    }
}
