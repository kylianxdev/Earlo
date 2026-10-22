using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Earlo.App.Services;

namespace Earlo.App;

/// <summary>
/// Shows the volume when it changes on the earbuds themselves (a stem swipe). Windows only shows
/// its flyout for volume keys, so without this a swipe changes the volume silently.
/// </summary>
public partial class VolumeOsdWindow : Window
{
    readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(1.6) };

    public VolumeOsdWindow()
    {
        InitializeComponent();
        _hideTimer.Tick += (_, _) => FadeOut();
        SourceInitialized += (_, _) =>
        {
            // Never take focus or clicks: it is only an indicator.
            var hwnd = new WindowInteropHelper(this).Handle;
            const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
            SetWindowLongW(hwnd, GWL_EXSTYLE, GetWindowLongW(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        };
    }

    public void ShowLevel(float level, bool muted)
    {
        SetLevel(level, muted);

        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        if (!IsVisible)
        {
            Show();
            ScreenPlacement.Place(this, centered: true);
        }
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    public void SetLevel(float level, bool muted)
    {
        int percent = (int)Math.Round(level * 100);
        LevelText.Text = muted ? "0" : percent.ToString();
        SpeakerIcon.Text = muted || percent == 0 ? "" : percent < 34 ? "" : percent < 67 ? "" : "";
        Fill.Width = 140 * (muted ? 0 : Math.Clamp(level, 0, 1));
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
