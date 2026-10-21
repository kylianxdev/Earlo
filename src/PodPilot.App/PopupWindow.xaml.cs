using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PodPilot.App.Controls;

namespace PodPilot.App;

/// <summary>The iPhone-style card that slides up above the taskbar when the case opens.</summary>
public partial class PopupWindow : Window
{
    readonly DispatcherTimer _hideTimer = new();
    bool _closing;

    public PopupWindow()
    {
        InitializeComponent();
        _hideTimer.Tick += (_, _) => SlideOut();
    }

    public event EventHandler? SettingsRequested;

    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(8);

    public void Update(DeviceViewState state) => Panel.Update(state);

    public void ShowAnimated(DeviceViewState state)
    {
        Update(state);
        _closing = false;

        if (!IsVisible)
        {
            Opacity = 0;
            Show();
            UpdateLayout();
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth + 4;
            double finalTop = area.Bottom - ActualHeight + 4;
            Top = finalTop + 40;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(TopProperty, new DoubleAnimation(finalTop, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
        }
        else
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        }

        RestartHideTimer();
    }

    void RestartHideTimer()
    {
        _hideTimer.Stop();
        _hideTimer.Interval = Duration;
        if (!IsMouseOver)
            _hideTimer.Start();
    }

    public void SlideOut()
    {
        _hideTimer.Stop();
        if (!IsVisible || _closing)
            return;
        _closing = true;

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
        fade.Completed += (_, _) =>
        {
            if (_closing)
                Hide();
        };
        BeginAnimation(TopProperty, new DoubleAnimation(Top + 30, TimeSpan.FromMilliseconds(200)));
        BeginAnimation(OpacityProperty, fade);
    }

    void OnMouseEnter(object sender, MouseEventArgs e) => _hideTimer.Stop();

    void OnMouseLeave(object sender, MouseEventArgs e) => RestartHideTimer();

    void OnCloseClick(object sender, RoutedEventArgs e) => SlideOut();

    void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SlideOut();
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}
