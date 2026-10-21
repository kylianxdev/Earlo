using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PodPilot.App.Controls;
using PodPilot.Core.Aap;
using PodPilot.Core.Bluetooth;

namespace PodPilot.App.Services;

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
        RenderPopup(Path.Combine(folder, "popup-searching.png"),
            new DeviceViewState(null, null, ScannerStatus.Scanning, 20));
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

    static void Save(FrameworkElement element, string file)
    {
        const double scale = 2;
        // The element renders at its offset inside the window, so include the margin on both sides.
        double width = element.ActualWidth + element.Margin.Left + element.Margin.Right;
        double height = element.ActualHeight + element.Margin.Top + element.Margin.Bottom;
        var bitmap = new RenderTargetBitmap(
            (int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }
}
