using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Earlo.Core.Devices;

namespace Earlo.App.Controls;

public partial class DeviceArt : UserControl
{
    public static readonly DependencyProperty FormFactorProperty = DependencyProperty.Register(
        nameof(FormFactor), typeof(FormFactor), typeof(DeviceArt), new PropertyMetadata(FormFactor.ProBuds, (d, _) => ((DeviceArt)d).Refresh()));

    public static readonly DependencyProperty IsDarkDeviceProperty = DependencyProperty.Register(
        nameof(IsDarkDevice), typeof(bool), typeof(DeviceArt), new PropertyMetadata(false, (d, _) => ((DeviceArt)d).Refresh()));

    public static readonly DependencyProperty ShowCaseProperty = DependencyProperty.Register(
        nameof(ShowCase), typeof(bool), typeof(DeviceArt), new PropertyMetadata(false, (d, _) => ((DeviceArt)d).Refresh()));

    public DeviceArt()
    {
        InitializeComponent();
        Refresh();
    }

    public FormFactor FormFactor
    {
        get => (FormFactor)GetValue(FormFactorProperty);
        set => SetValue(FormFactorProperty, value);
    }

    /// <summary>Beats products are drawn dark, AirPods white.</summary>
    public bool IsDarkDevice
    {
        get => (bool)GetValue(IsDarkDeviceProperty);
        set => SetValue(IsDarkDeviceProperty, value);
    }

    /// <summary>Draws the charging case instead of the buds.</summary>
    public bool ShowCase
    {
        get => (bool)GetValue(ShowCaseProperty);
        set => SetValue(ShowCaseProperty, value);
    }

    void Refresh()
    {
        Resources["ArtFill"] = IsDarkDevice
            ? Gradient("#FF4A4A50", "#FF1E1E22")
            : Gradient("#FFFFFFFF", "#FFE2E2E8");
        Resources["ArtStroke"] = Solid(IsDarkDevice ? "#FF0E0E10" : "#FFB8B8C0");
        Resources["ArtShade"] = Solid(IsDarkDevice ? "#FF101012" : "#FF9A9AA4");

        var visible = ShowCase ? CaseArt : FormFactor switch
        {
            FormFactor.StemBuds => StemArt,
            FormFactor.ProBuds => ProArt,
            FormFactor.RoundBuds => RoundArt,
            FormFactor.HookBuds => HookArt,
            FormFactor.Headphones => HeadphonesArt,
            _ => NeckbandArt,
        };

        foreach (var art in new[] { StemArt, ProArt, RoundArt, HookArt, HeadphonesArt, NeckbandArt, CaseArt })
            art.Visibility = art == visible ? Visibility.Visible : Visibility.Collapsed;
    }

    static Brush Gradient(string from, string to)
    {
        var b = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), 90);
        b.Freeze();
        return b;
    }

    static Brush Solid(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
