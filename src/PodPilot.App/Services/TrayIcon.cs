using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PodPilot.Core.Tracking;

namespace PodPilot.App.Services;

/// <summary>
/// Notification-area icon. Optionally draws the lowest battery level as a number.
/// </summary>
/// <remarks>
/// Talks to Shell_NotifyIcon directly instead of using WinForms' NotifyIcon, which would add
/// more than 20 MB of WinForms to the app for this one feature.
/// </remarks>
public sealed partial class TrayIcon : IDisposable
{
    const int CallbackMessage = 0x8000 + 1; // WM_APP + 1
    const int WM_LBUTTONUP = 0x0202;
    const int WM_LBUTTONDBLCLK = 0x0203;
    const int WM_RBUTTONUP = 0x0205;

    readonly HwndSource _window;
    readonly int _taskbarCreatedMessage;
    readonly IntPtr _appIcon;
    readonly ContextMenu _menu;
    IntPtr _dynamicIcon;
    IntPtr _currentIcon;
    string _tooltip = "PodPilot";
    string _lastKey = string.Empty;
    bool _added;

    public TrayIcon()
    {
        _window = new HwndSource(new HwndSourceParameters("PodPilotTray") { Width = 0, Height = 0, WindowStyle = 0 });
        _window.AddHook(WndProc);
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");

        _appIcon = LoadAppIcon();
        _currentIcon = _appIcon;

        _menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        _menu.Items.Add(MenuItem("Show battery", () => ShowPopup?.Invoke(this, EventArgs.Empty)));
        _menu.Items.Add(MenuItem("Settings", () => OpenSettings?.Invoke(this, EventArgs.Empty)));
        _menu.Items.Add(new Separator());
        _menu.Items.Add(MenuItem("Exit", () => ExitRequested?.Invoke(this, EventArgs.Empty)));

        Apply(NIM_ADD);
    }

    public event EventHandler? ShowPopup;
    public event EventHandler? OpenSettings;
    public event EventHandler? ExitRequested;

    public void Update(PodsSnapshot? snapshot, string? name, bool showNumber)
    {
        _tooltip = Truncate(BuildTooltip(snapshot, name), 127);

        var lowest = snapshot?.Lowest ?? BatteryState.Unknown;
        string key = showNumber && lowest.IsKnown ? $"{lowest.Percent}|{lowest.Charging}|{Theme.TaskbarUsesLight}" : "app";
        if (key != _lastKey)
        {
            _lastKey = key;
            var old = _dynamicIcon;
            _dynamicIcon = key == "app" ? IntPtr.Zero : RenderNumberIcon(lowest.Percent!.Value, lowest.Charging, Theme.TaskbarUsesLight);
            _currentIcon = _dynamicIcon != IntPtr.Zero ? _dynamicIcon : _appIcon;
            Apply(NIM_MODIFY);
            if (old != IntPtr.Zero)
                DestroyIcon(old);
            return;
        }
        Apply(NIM_MODIFY);
    }

    /// <summary>Forces a redraw, for example after the taskbar theme changed.</summary>
    public void Invalidate() => _lastKey = string.Empty;

    /// <summary>Shows a Windows notification from the tray icon.</summary>
    public void Notify(string title, string text)
    {
        var data = NewData(NIF_INFO);
        data.szInfoTitle = Truncate(title, 63);
        data.szInfo = Truncate(text, 255);
        data.dwInfoFlags = NIIF_INFO;
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == CallbackMessage)
        {
            switch ((int)lParam & 0xFFFF)
            {
                case WM_LBUTTONUP:
                    ShowPopup?.Invoke(this, EventArgs.Empty);
                    break;
                case WM_LBUTTONDBLCLK:
                    OpenSettings?.Invoke(this, EventArgs.Empty);
                    break;
                case WM_RBUTTONUP:
                    // Needed so the menu closes when the user clicks elsewhere.
                    SetForegroundWindow(_window.Handle);
                    _menu.IsOpen = true;
                    break;
            }
            handled = true;
        }
        else if (msg == _taskbarCreatedMessage)
        {
            // Explorer restarted: the icon is gone and must be added again.
            _added = false;
            Apply(NIM_ADD);
        }
        return IntPtr.Zero;
    }

    void Apply(int action)
    {
        var data = NewData(NIF_MESSAGE | NIF_ICON | NIF_TIP);
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _currentIcon;
        data.szTip = _tooltip;
        if (action == NIM_MODIFY && !_added)
            action = NIM_ADD;
        if (Shell_NotifyIconW(action, ref data) && action == NIM_ADD)
            _added = true;
    }

    NOTIFYICONDATAW NewData(int flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _window.Handle,
        uID = 1,
        uFlags = flags,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    static MenuItem MenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    static string BuildTooltip(PodsSnapshot? s, string? name)
    {
        if (s is null)
            return "PodPilot: no AirPods nearby";

        string title = name ?? s.Device.Name;
        if (s.Device.HasSingleBattery)
            return $"{title}\n{Format(s.Single)}";
        return $"{title}\nL {Format(s.Left)}  R {Format(s.Right)}  Case {Format(s.Case)}";
    }

    static string Format(BatteryState b) => b.Percent is int p ? $"{(b.Stale ? "~" : "")}{p}%{(b.Charging ? "+" : "")}" : "--";

    static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)];

    static int SmallIconSize => Math.Max(16, GetSystemMetrics(SM_CXSMICON));

    static IntPtr LoadAppIcon()
    {
        try
        {
            var decoder = BitmapDecoder.Create(new Uri("pack://application:,,,/Assets/podpilot.ico"),
                BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            int size = SmallIconSize;
            var frame = decoder.Frames.OrderBy(f => Math.Abs(f.PixelWidth - size)).First();
            return CreateIcon(frame);
        }
        catch (Exception)
        {
            return LoadIconW(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
        }
    }

    static IntPtr RenderNumberIcon(int percent, bool charging, bool lightTaskbar)
    {
        int size = Math.Max(32, SmallIconSize * 2);
        var color = percent <= 20 && !charging
            ? Color.FromRgb(255, 69, 58)
            : lightTaskbar ? Color.FromRgb(28, 28, 30) : Colors.White;
        var brush = new SolidColorBrush(color);

        var text = new FormattedText(percent.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size * (percent >= 100 ? 0.47 : 0.69), brush, 1.0);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawText(text, new Point((size - text.Width) / 2, (size * 0.9 - text.Height) / 2));
            var bar = charging ? new SolidColorBrush(Color.FromRgb(52, 199, 89)) : brush;
            double width = (size - size / 4.0) * Math.Clamp(percent, 5, 100) / 100.0;
            dc.DrawRectangle(bar, null, new Rect(size / 8.0, size * 0.84, width, size / 8.0));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return CreateIcon(bitmap);
    }

    /// <summary>Turns a WPF bitmap into a Win32 HICON with alpha.</summary>
    static IntPtr CreateIcon(BitmapSource source)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = bgra.PixelWidth, height = bgra.PixelHeight, stride = width * 4;
        var pixels = new byte[stride * height];
        bgra.CopyPixels(pixels, stride, 0);

        var header = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // top-down
            biPlanes = 1,
            biBitCount = 32,
        };
        IntPtr color = CreateDIBSection(IntPtr.Zero, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
        Marshal.Copy(pixels, 0, bits, pixels.Length);
        IntPtr mask = CreateBitmap(width, height, 1, 1, IntPtr.Zero);

        var info = new ICONINFO { fIcon = true, hbmMask = mask, hbmColor = color };
        IntPtr icon = CreateIconIndirect(ref info);
        DeleteObject(color);
        DeleteObject(mask);
        return icon;
    }

    public void Dispose()
    {
        var data = NewData(0);
        Shell_NotifyIconW(NIM_DELETE, ref data);
        _window.Dispose();
        if (_dynamicIcon != IntPtr.Zero)
            DestroyIcon(_dynamicIcon);
        DestroyIcon(_appIcon);
    }

    // --- Win32 -------------------------------------------------------------------------

    const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    const int NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_INFO = 0x10;
    const int NIIF_INFO = 0x1;
    const int SM_CXSMICON = 49;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATAW
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool Shell_NotifyIconW(int message, ref NOTIFYICONDATAW data);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RegisterWindowMessageW(string name);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    private static partial IntPtr LoadIconW(IntPtr instance, IntPtr name);

    [DllImport("user32.dll")]
    static extern IntPtr CreateIconIndirect(ref ICONINFO info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr handle);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER header, int usage, out IntPtr bits, IntPtr section, int offset);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateBitmap(int width, int height, int planes, int bitCount, IntPtr bits);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr handle);
}
