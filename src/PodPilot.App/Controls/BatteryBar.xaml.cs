using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using PodPilot.Core.Tracking;

namespace PodPilot.App.Controls;

public partial class BatteryBar : UserControl
{
    BatteryState _state = BatteryState.Unknown;

    public BatteryBar()
    {
        InitializeComponent();
        Render(animate: false);
    }

    public string Label
    {
        get => LabelText.Text;
        set => LabelText.Text = value;
    }

    public int LowThreshold { get; set; } = 20;

    public BatteryState State
    {
        get => _state;
        set
        {
            if (_state == value)
                return;
            _state = value;
            Render(animate: true);
        }
    }

    void OnTrackSizeChanged(object sender, SizeChangedEventArgs e) => Render(animate: false);

    void Render(bool animate)
    {
        int? percent = _state.Percent;
        PercentText.Text = percent is null ? "--" : $"{percent}%";
        Bolt.Visibility = _state.Charging && percent.HasValue ? Visibility.Visible : Visibility.Collapsed;

        string brush = percent is null ? "BatteryUnknown"
            : percent <= LowThreshold && !_state.Charging ? "BatteryLow"
            : "BatteryGood";
        Fill.SetResourceReference(Border.BackgroundProperty, brush);

        double target = Math.Max(Track.ActualWidth * (percent ?? 0) / 100.0, percent is null ? 0 : 8);
        if (animate && Track.ActualWidth > 0)
        {
            Fill.BeginAnimation(WidthProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        }
        else
        {
            Fill.BeginAnimation(WidthProperty, null);
            Fill.Width = target;
        }
    }
}
