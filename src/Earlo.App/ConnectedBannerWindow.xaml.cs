using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Earlo.App.Controls;
using Earlo.App.Services;
using Earlo.Core.Samsung;
using Earlo.Core.Tracking;

namespace Earlo.App;

/// <summary>
/// "Connected" banner in the top right corner when earbuds connect to this PC, with their battery levels.
/// It slides in, stays a few seconds and fades away by itself; it never takes focus or clicks.
/// </summary>
public partial class ConnectedBannerWindow : Window
{
    readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    string _name = string.Empty;

    public ConnectedBannerWindow()
    {
        InitializeComponent();
        _hideTimer.Tick += (_, _) => FadeOut();
        SourceInitialized += (_, _) =>
        {
            // Never take focus or clicks: it is only a notice.
            var hwnd = new WindowInteropHelper(this).Handle;
            const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
            SetWindowLongW(hwnd, GWL_EXSTYLE, GetWindowLongW(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        };
    }

    public void Present(string name, PodsSnapshot? snapshot)
    {
        SetContent(name, snapshot);
        _hideTimer.Stop();
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }
        ScreenPlacement.PlaceTopRight(this);

        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
        Slide.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        _hideTimer.Start();
    }

    /// <summary>Battery levels often arrive a moment after the connection; fills them in while the banner shows.</summary>
    public void Update(PodsSnapshot? snapshot)
    {
        if (!IsVisible || snapshot is null)
            return;
        string before = StatusText.Text;
        SetContent(_name, snapshot);
        if (StatusText.Text != before)
            ScreenPlacement.PlaceTopRight(this); // the card may have grown
    }

    public void SetContent(string name, PodsSnapshot? snapshot)
    {
        _name = name;
        NameText.Text = name;
        string battery = BatteryText(snapshot);
        StatusText.Text = battery.Length == 0 ? Strings.BannerConnected : $"{Strings.BannerConnected} · {battery}";

        // Galaxy Buds tell their model by name; AirPods and Beats once their first advertisement is in.
        var render = snapshot is not null ? DeviceImages.For(snapshot.Device.ModelId)
            : GalaxyBudsModels.FromPairedName(name) is { } galaxy ? DeviceImages.For(GalaxyBudsModels.CatalogId(galaxy))
            : null;
        Render.Source = render;
        Glyph.Visibility = render is null ? Visibility.Visible : Visibility.Collapsed;
    }

    static string BatteryText(PodsSnapshot? snapshot)
    {
        if (snapshot is null)
            return string.Empty;
        if (snapshot.Device.HasSingleBattery)
            return snapshot.Single.IsKnown ? $"{snapshot.Single.Percent}%" : string.Empty;

        var parts = new List<string>();
        if (snapshot.Left.IsKnown)
            parts.Add($"{Strings.ShortLeft} {snapshot.Left.Percent}%");
        if (snapshot.Right.IsKnown)
            parts.Add($"{Strings.ShortRight} {snapshot.Right.Percent}%");
        if (snapshot.Case.IsKnown)
            parts.Add($"{Strings.Case} {snapshot.Case.Percent}%");
        return string.Join("  ", parts);
    }

    void FadeOut()
    {
        _hideTimer.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
        fade.Completed += (_, _) =>
        {
            if (Opacity == 0)
                Hide();
        };
        BeginAnimation(OpacityProperty, fade);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int GetWindowLongW(IntPtr hwnd, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int SetWindowLongW(IntPtr hwnd, int index, int value);
}
