using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Earlo.App;

/// <summary>Light/dark brushes for the custom-drawn parts (popup, artwork). Standard controls use the Fluent theme.</summary>
public static class Theme
{
    const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static event EventHandler? Changed;

    public static bool AppsUseDark => ReadDword("AppsUseLightTheme") == 0;

    public static bool TaskbarUsesLight => ReadDword("SystemUsesLightTheme") == 1;

    public static void Initialize(Application app)
    {
        Apply(app);
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color)
            {
                app.Dispatcher.BeginInvoke(() =>
                {
                    Apply(app);
                    Changed?.Invoke(null, EventArgs.Empty);
                });
            }
        };
    }

    public static void Apply(Application app)
    {
        bool dark = AppsUseDark;
        var r = app.Resources;
        r["PopupBackground"] = Brush(dark ? "#F0202024" : "#F7FAFAFC");
        r["PopupBorder"] = Brush(dark ? "#33FFFFFF" : "#1F000000");
        r["TextPrimary"] = Brush(dark ? "#FFFFFFFF" : "#FF1C1C1E");
        r["TextSecondary"] = Brush(dark ? "#B3FFFFFF" : "#FF6E6E73");
        r["BarTrack"] = Brush(dark ? "#33FFFFFF" : "#1A000000");
        r["SubtleFill"] = Brush(dark ? "#14FFFFFF" : "#0A000000");
        r["BatteryGood"] = Brush("#FF34C759");
        r["BatteryLow"] = Brush("#FFFF3B30");
        r["BatteryUnknown"] = Brush(dark ? "#66FFFFFF" : "#40000000");
        r["Accent"] = Brush("#FF0A84FF");
    }

    static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    static int? ReadDword(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(name) as int?;
    }
}
