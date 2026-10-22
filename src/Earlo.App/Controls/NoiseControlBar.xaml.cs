using System;
using System.Windows;
using System.Windows.Controls;
using Earlo.Core.Aap;

namespace Earlo.App.Controls;

/// <summary>Off / Noise Cancellation / Transparency / Adaptive picker, like Control Center on an iPhone.</summary>
public partial class NoiseControlBar : UserControl
{
    public NoiseControlBar()
    {
        InitializeComponent();
    }

    public event EventHandler<NoiseControlMode>? ModeRequested;

    public void Update(NoiseControlMode? mode, bool supportsAdaptive)
    {
        AdaptiveButton.Visibility = supportsAdaptive ? Visibility.Visible : Visibility.Collapsed;
        Buttons.Columns = supportsAdaptive ? 4 : 3;
        foreach (var (button, value) in Map())
            button.Tag = mode == value ? "Selected" : null;
    }

    void OnModeClick(object sender, RoutedEventArgs e)
    {
        foreach (var (button, value) in Map())
        {
            if (ReferenceEquals(sender, button))
                ModeRequested?.Invoke(this, value);
        }
    }

    (Button, NoiseControlMode)[] Map() =>
    [
        (OffButton, NoiseControlMode.Off),
        (AncButton, NoiseControlMode.NoiseCancellation),
        (TransparencyButton, NoiseControlMode.Transparency),
        (AdaptiveButton, NoiseControlMode.Adaptive),
    ];
}
