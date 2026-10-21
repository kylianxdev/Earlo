using System;
using System.Windows.Threading;
using PodPilot.Core.Protocol;
using PodPilot.Core.Tracking;

namespace PodPilot.App.Services;

/// <summary>
/// Feeds fake advertisements into the tracker (start with <c>--demo</c>), so the UI can be tried
/// and developed without AirPods nearby.
/// </summary>
public static class DemoMode
{
    public const ushort DemoModel = 0x1420;

    public static PodsSnapshot Snapshot(ushort model = DemoModel, int? left = 80, int? right = 70, int? caseLevel = 50, bool caseCharging = true)
    {
        var tracker = new PodsTracker();
        tracker.Process(Message(model, left, right, caseLevel, caseCharging: caseCharging, bothInCase: true, lidOpen: true), -40);
        return tracker.Current!;
    }

    /// <summary>Left AirPod in the ear, right one taken out, case level remembered.</summary>
    public static PodsSnapshot RightOutSnapshot()
    {
        var tracker = new PodsTracker();
        tracker.Process(Message(DemoModel, 85, 75, null, leftInEar: true), -40);
        return tracker.Current! with { Case = new BatteryState(55, false, Stale: true) };
    }

    public static void Start(PodsTracker tracker, Dispatcher dispatcher)
    {
        int step = 0;
        var timer = new DispatcherTimer(TimeSpan.FromSeconds(1.5), DispatcherPriority.Normal, (_, _) =>
        {
            // Lid opens, pods are taken out and worn, one is taken out, then back in the case.
            var msg = (step++ % 16) switch
            {
                < 4 => Message(DemoModel, 80, 70, 50, caseCharging: true, bothInCase: true, lidOpen: true),
                < 10 => Message(DemoModel, 80, 70, null, leftInEar: true, rightInEar: true),
                < 13 => Message(DemoModel, 80, 70, null, leftInEar: true),
                _ => Message(DemoModel, 80, 70, 50, bothInCase: true),
            };
            tracker.Process(msg, -45);
        }, dispatcher);
        timer.Start();
    }

    static ProximityPairingMessage Message(ushort model, int? left, int? right, int? caseLevel,
        bool caseCharging = false, bool leftInEar = false, bool rightInEar = false, bool bothInCase = false, bool lidOpen = false)
    {
        var payload = ProximityPairingPayload.Build(model, left, right, caseLevel,
            caseCharging: caseCharging, leftInEar: leftInEar, rightInEar: rightInEar, bothInCase: bothInCase, lidOpen: lidOpen);
        ProximityPairingMessage.TryParse(payload, out var msg);
        return msg!;
    }
}
