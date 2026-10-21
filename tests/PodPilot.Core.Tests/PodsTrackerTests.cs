using PodPilot.Core.Protocol;
using PodPilot.Core.Tracking;

namespace PodPilot.Core.Tests;

public class PodsTrackerTests
{
    sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    static ProximityPairingMessage Msg(byte[] payload)
    {
        Assert.True(ProximityPairingMessage.TryParse(payload, out var msg));
        return msg;
    }

    [Fact]
    public void Ignores_weak_signals()
    {
        var tracker = new PodsTracker { RssiThreshold = -70 };
        tracker.Process(Msg(ProximityPairingPayload.Build(0x1420, 50, 50, 50)), -85);
        Assert.Null(tracker.Current);
    }

    [Fact]
    public void Holds_values_that_a_message_omits()
    {
        var time = new FakeTime();
        var tracker = new PodsTracker(time);

        tracker.Process(Msg(ProximityPairingPayload.Build(0x1420, 80, 70, 40, bothInCase: true)), -50);
        time.Now += TimeSpan.FromSeconds(10);
        tracker.Process(Msg(ProximityPairingPayload.Build(0x1420, 80, 70, null, leftInEar: true, rightInEar: true)), -50);

        Assert.Equal(45, tracker.Current!.Case.Percent);

        time.Now += TimeSpan.FromHours(1);
        tracker.Process(Msg(ProximityPairingPayload.Build(0x1420, 80, 70, null)), -50);
        Assert.Null(tracker.Current!.Case.Percent);
    }

    [Fact]
    public void Raises_lid_opened_once_per_opening()
    {
        var time = new FakeTime();
        var tracker = new PodsTracker(time);
        int opened = 0;
        tracker.LidOpened += (_, _) => opened++;

        var open = Msg(ProximityPairingPayload.Build(0x1420, 80, 70, 40, bothInCase: true, lidOpen: true));
        var closed = Msg(ProximityPairingPayload.Build(0x1420, 80, 70, 40, bothInCase: true, lidOpen: false));

        tracker.Process(open, -50);
        tracker.Process(open, -50);
        Assert.Equal(1, opened);

        time.Now += TimeSpan.FromSeconds(30);
        tracker.Process(closed, -50);
        tracker.Process(open, -50);
        Assert.Equal(2, opened);
    }

    [Fact]
    public void Raises_ear_status_changes()
    {
        var tracker = new PodsTracker();
        EarStatusChangedEventArgs? change = null;
        tracker.EarStatusChanged += (_, e) => change = e;

        tracker.Process(Msg(ProximityPairingPayload.Build(0x1420, 80, 70, null, leftInEar: true, rightInEar: true)), -50);
        tracker.Process(Msg(ProximityPairingPayload.Build(0x1420, 80, 70, null, leftInEar: true, rightInEar: false)), -50);

        Assert.NotNull(change);
        Assert.True(change.WasRightInEar);
        Assert.False(change.Snapshot.RightInEar);
    }

    [Fact]
    public void Prefers_the_closest_device_and_expires()
    {
        var time = new FakeTime();
        var tracker = new PodsTracker(time);
        bool lost = false;
        tracker.Lost += (_, _) => lost = true;

        tracker.Process(Msg(ProximityPairingPayload.Build(0x0E20, 50, 50, 50)), -65);
        tracker.Process(Msg(ProximityPairingPayload.Build(0x1920, 60, 60, 60)), -40);
        Assert.Equal(0x1920, tracker.Current!.Device.ModelId);

        time.Now += TimeSpan.FromMinutes(5);
        tracker.Expire();
        Assert.Null(tracker.Current);
        Assert.True(lost);
    }
}
