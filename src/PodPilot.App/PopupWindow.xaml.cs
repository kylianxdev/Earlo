using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PodPilot.App.Controls;
using PodPilot.App.Services;
using PodPilot.Core.Aap;

namespace PodPilot.App;

/// <summary>The iPhone-style card that slides in next to the taskbar when the case opens.</summary>
public partial class PopupWindow : Window
{
    readonly DispatcherTimer _hideTimer = new();
    readonly TranslateTransform _slide = new();
    TaskbarEdge _edge = TaskbarEdge.Bottom;
    bool _closing;

    public PopupWindow()
    {
        InitializeComponent();
        Card.RenderTransform = _slide;
        _hideTimer.Tick += (_, _) => SlideOut();
        Panel.NoiseControlRequested += (_, mode) =>
        {
            RestartHideTimer();
            NoiseControlRequested?.Invoke(this, mode);
        };
        Panel.ConnectRequested += (_, connect) =>
        {
            RestartHideTimer();
            ConnectRequested?.Invoke(this, connect);
        };
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler<NoiseControlMode>? NoiseControlRequested;
    public event EventHandler<bool>? ConnectRequested;

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
            _edge = ScreenPlacement.Place(this, centered: false);

            // Slide the card in from the taskbar side, wherever the taskbar is.
            var from = ScreenPlacement.SlideOffset(_edge, 40);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            _slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from.X, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
            _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(from.Y, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
        }
        else
        {
            // Shown again while sliding out: snap back into place.
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            _slide.BeginAnimation(TranslateTransform.XProperty, null);
            _slide.BeginAnimation(TranslateTransform.YProperty, null);
            _slide.X = _slide.Y = 0;
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
        var to = ScreenPlacement.SlideOffset(_edge, 30);
        _slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(to.X, TimeSpan.FromMilliseconds(200)));
        _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(to.Y, TimeSpan.FromMilliseconds(200)));
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
