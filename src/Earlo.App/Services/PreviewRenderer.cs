using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Earlo.App.Controls;
using Earlo.Core.Aap;
using Earlo.Core.Bluetooth;

namespace Earlo.App.Services;

/// <summary>
/// Renders the popup for several models to PNG files (start with <c>--render-preview &lt;folder&gt;</c>).
/// Used for README screenshots and to check the artwork without hardware.
/// </summary>
public static class PreviewRenderer
{
    public static void Render(string folder)
    {
        Directory.CreateDirectory(folder);
        var connected = new PairedDeviceInfo("demo", "My AirPods Pro", true);

        RenderPopup(Path.Combine(folder, "popup-airpods-pro.png"),
            new DeviceViewState(DemoMode.Snapshot(), connected, ScannerStatus.Scanning, 20));
        var aap = new AapState
        {
            Connected = true,
            NoiseControl = NoiseControlMode.Adaptive,
            Left = new AapBattery(AapBatteryComponent.Left, 87, AapBatteryStatus.Discharging),
            Right = new AapBattery(AapBatteryComponent.Right, 79, AapBatteryStatus.Discharging),
            Case = new AapBattery(AapBatteryComponent.Case, 54, AapBatteryStatus.Charging),
        };
        RenderPopup(Path.Combine(folder, "popup-noise-control.png"),
            new DeviceViewState(DemoMode.Snapshot(), connected, ScannerStatus.Scanning, 20, aap));
        RenderPopup(Path.Combine(folder, "popup-ear-detection.png"),
            new DeviceViewState(DemoMode.RightOutSnapshot(), connected, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-airpods-4.png"),
            new DeviceViewState(DemoMode.Snapshot(0x1920, 100, 90, 15, false), null, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-airpods-max.png"),
            new DeviceViewState(DemoMode.Snapshot(0x0A20, 60, null, null, false), null, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-beats-fit-pro.png"),
            new DeviceViewState(DemoMode.Snapshot(0x1220, 40, 50, 90), null, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-powerbeats-pro.png"),
            new DeviceViewState(DemoMode.Snapshot(0x0B20, 10, 20, 60), null, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-airpods-2.png"),
            new DeviceViewState(DemoMode.Snapshot(0x0F20, 50, 60, 100), null, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-airpods-3.png"),
            new DeviceViewState(DemoMode.Snapshot(0x1320, 70, 70, 30), null, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-airpods-pro-1.png"),
            new DeviceViewState(DemoMode.Snapshot(0x0E20, 90, 90, 80), null, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-beats-studio-buds.png"),
            new DeviceViewState(DemoMode.Snapshot(0x1120, 60, 60, 40), null, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-beats-studio-pro.png"),
            new DeviceViewState(DemoMode.Snapshot(0x1720, 80, null, null, false), null, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-beats-flex.png"),
            new DeviceViewState(DemoMode.Snapshot(0x1020, 30, null, null, false), null, ScannerStatus.Scanning, 20));
        RenderPopup(Path.Combine(folder, "popup-unknown-model.png"),
            new DeviceViewState(DemoMode.Snapshot(0x9920, 50, 40, 70), null, ScannerStatus.Scanning, 20));
        var galaxy = new Earlo.Core.Samsung.GalaxyBudsStatus(72, 68, 45, false, false, true,
            Earlo.Core.Samsung.BudPlacement.Wearing, Earlo.Core.Samsung.BudPlacement.InOpenCase);
        RenderPopup(Path.Combine(folder, "popup-galaxy-buds.png"),
            new DeviceViewState(Earlo.Core.Samsung.GalaxyBudsManager.ToSnapshot(Earlo.Core.Samsung.GalaxyBudsModel.Buds2Pro, galaxy),
                new PairedDeviceInfo("demo", "Galaxy Buds2 Pro (A1B2)", true), ScannerStatus.Scanning, 20, CanConnect: true));
        foreach (var (percent, charging, light) in new[] { (85, false, false), (15, false, false), (60, true, false) })
            SavePng(TrayIcon.RenderTrayBitmap(percent, charging, light),
                Path.Combine(folder, $"tray-{percent}{(charging ? "-charging" : "")}{(light ? "-light" : "")}.png"));
        var osd = new VolumeOsdWindow { Left = -10000, Top = -10000 };
        osd.SetLevel(0.62f, false);
        osd.Show();
        osd.UpdateLayout();
        Save((FrameworkElement)osd.Content, Path.Combine(folder, "volume.png"));
        osd.Close();

        var banner = new ConnectedBannerWindow { Left = -10000, Top = -10000 };
        banner.SetContent("My AirPods Pro", DemoMode.Snapshot());
        banner.Show();
        banner.UpdateLayout();
        Save((FrameworkElement)banner.Content, Path.Combine(folder, "connected.png"));
        banner.Close();

        RenderSettings(Path.Combine(folder, "settings.png"),
            new DeviceViewState(DemoMode.Snapshot(), connected, ScannerStatus.Scanning, 20, aap, CanConnect: true), driverPresent: true);
        // The Store version has no driver, so its screenshots show no noise control.
        RenderSettings(Path.Combine(folder, "settings-store.png"),
            new DeviceViewState(DemoMode.Snapshot(), connected, ScannerStatus.Scanning, 20, CanConnect: true), driverPresent: false);

        RenderPopup(Path.Combine(folder, "popup-searching.png"),
            new DeviceViewState(null, null, ScannerStatus.Scanning, 20));
    }

    static void RenderSettings(string file, DeviceViewState state, bool driverPresent)
    {
        var settings = new SettingsWindow(new AppSettings { Persist = false }) { Left = -10000, Top = -10000, Height = 820 };
        settings.Show();
        settings.Update(state, [state.Connected!], driverPresent);
        settings.UpdateMicrophone(state.Connected!.Name, handsFreeOn: false, busy: false);
        settings.UpdateLayout();
        Save((FrameworkElement)settings.Content, file, background: Brushes.White);
        settings.Close();
    }

    static void RenderPopup(string file, DeviceViewState state)
    {
        var popup = new PopupWindow { Left = -10000, Top = -10000 };
        popup.Update(state);
        popup.Show();
        popup.UpdateLayout();
        Save((FrameworkElement)popup.Content, file);
        popup.Close();
    }

    static void SavePng(BitmapSource bitmap, string file)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }

    static void Save(FrameworkElement element, string file, Brush? background = null)
    {
        const double scale = 2;
        // The element renders at its offset inside the window, so include the margin on both sides.
        double width = element.ActualWidth + element.Margin.Left + element.Margin.Right;
        double height = element.ActualHeight + element.Margin.Top + element.Margin.Bottom;
        var bitmap = new RenderTargetBitmap(
            (int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        if (background is not null)
        {
            var fill = new DrawingVisual();
            using (var dc = fill.RenderOpen())
                dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
            bitmap.Render(fill);
        }
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }
}
