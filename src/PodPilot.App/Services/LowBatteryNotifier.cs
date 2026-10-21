using System;
using System.Collections.Generic;
using PodPilot.Core.Tracking;

namespace PodPilot.App.Services;

/// <summary>Warns once when a part drops below the threshold, and again only after it was charged.</summary>
public sealed class LowBatteryNotifier(AppSettings settings, Action<string, string> notify)
{
    readonly HashSet<string> _warned = [];

    public void Check(PodsSnapshot snapshot, string name)
    {
        if (!settings.LowBatteryWarning)
            return;

        if (snapshot.Device.HasSingleBattery)
        {
            Check("Battery", snapshot.Single, name, Strings.LowSingle(name));
            return;
        }

        Check("Left", snapshot.Left, name, Strings.LowLeft);
        Check("Right", snapshot.Right, name, Strings.LowRight);
        Check("Case", snapshot.Case, name, Strings.LowCase);
    }

    void Check(string part, BatteryState state, string name, string title)
    {
        if (state.Percent is not int percent)
            return;

        if (state.Charging || percent > settings.LowBatteryThreshold + 10)
        {
            _warned.Remove(part);
            return;
        }

        if (percent <= settings.LowBatteryThreshold && _warned.Add(part))
            notify(title, Strings.Remaining(name, percent));
    }
}
