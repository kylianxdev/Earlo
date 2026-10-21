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

    PodPlacement _placement = PodPlacement.Unknown;

    /// <summary>Shows where the earbud is under its label. Unknown hides the line.</summary>
    public PodPlacement Placement
    {
        get => _placement;
        set
        {
            if (_placement == value)
                return;
            bool wasShown = _placement != PodPlacement.Unknown;
            _placement = value;

            (string text, string brush) = value switch
            {
                PodPlacement.InEar => (Strings.InEar, "BatteryGood"),
                PodPlacement.OutOfEar => (Strings.OutOfEar, "TextSecondary"),
                PodPlacement.InCase => (Strings.InCase, "Accent"),
                _ => (string.Empty, "TextSecondary"),
            };
            StatusPanel.Visibility = value == PodPlacement.Unknown ? Visibility.Collapsed : Visibility.Visible;
            StatusText.Text = text;
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, brush);
            StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brush);
            // Fade in on a change (an AirPod taken out), not on first display.
            if (wasShown)
                StatusPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
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

        // A remembered level the device is not reporting right now: show it, but faded.
        Fill.Opacity = PercentText.Opacity = _state.Stale ? 0.45 : 1.0;
        ToolTip = _state.Stale ? Strings.StaleBattery : null;

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
