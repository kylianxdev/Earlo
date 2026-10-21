using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PodPilot.App.Controls;

public enum PodPlacement
{
    Unknown,
    InEar,
    OutOfEar,
    InCase,
}

/// <summary>Shows where one earbud is: in your ear, out of your ear or in the case.</summary>
public partial class PodStatusChip : UserControl
{
    PodPlacement _placement = (PodPlacement)(-1);

    public PodStatusChip()
    {
        InitializeComponent();
        SideText.Text = "Left";
    }

    public bool IsRight
    {
        get => PodFlip.ScaleX < 0;
        set
        {
            PodFlip.ScaleX = value ? -1 : 1;
            SideText.Text = value ? "Right" : "Left";
        }
    }

    public PodPlacement Placement
    {
        get => _placement;
        set
        {
            if (_placement == value)
                return;
            bool changed = _placement != (PodPlacement)(-1);
            _placement = value;
            Render();
            if (changed)
                Pulse();
        }
    }

    void Render()
    {
        (StateText.Text, string accent) = _placement switch
        {
            PodPlacement.InEar => ("In ear", "BatteryGood"),
            PodPlacement.OutOfEar => ("Out of ear", "TextSecondary"),
            PodPlacement.InCase => ("In case", "Accent"),
            _ => ("Unknown", "TextSecondary"),
        };

        bool inCase = _placement == PodPlacement.InCase;
        Pod.Visibility = inCase ? Visibility.Collapsed : Visibility.Visible;
        CaseIcon.Visibility = inCase ? Visibility.Visible : Visibility.Collapsed;

        Pod.SetResourceReference(Shape.StrokeProperty, accent);
        Pod.SetResourceReference(Shape.FillProperty, _placement == PodPlacement.InEar ? "BatteryGood" : "SubtleFill");
        CaseIcon.SetResourceReference(Border.BorderBrushProperty, accent);
        Chip.SetResourceReference(Border.BorderBrushProperty, _placement == PodPlacement.InEar ? "BatteryGood" : "PopupBorder");
        Chip.Opacity = _placement == PodPlacement.OutOfEar ? 0.75 : 1.0;
    }

    /// <summary>A short bounce so a change (an AirPod taken out) catches the eye.</summary>
    void Pulse()
    {
        var scale = new System.Windows.Media.ScaleTransform(1, 1);
        Chip.RenderTransformOrigin = new Point(0.5, 0.5);
        Chip.RenderTransform = scale;
        var bounce = new DoubleAnimation(1.08, 1.0, TimeSpan.FromMilliseconds(350))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 },
        };
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, bounce);
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, bounce);
    }
}
