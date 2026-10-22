using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Earlo.App.Services;

public enum TaskbarEdge { Left, Top, Right, Bottom }

/// <summary>
/// Puts flyout windows next to the taskbar, wherever it is: any edge, any monitor, any DPI.
/// Positions are in physical pixels so mixed-DPI setups line up.
/// </summary>
public static partial class ScreenPlacement
{
    /// <summary>The taskbar edge and the work area of the monitor the taskbar is on.</summary>
    public static (TaskbarEdge Edge, Rect WorkArea) Taskbar()
    {
        var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
        var edge = TaskbarEdge.Bottom;
        IntPtr monitor;
        if (SHAppBarMessage(ABM_GETTASKBARPOS, ref data) != 0)
        {
            edge = (TaskbarEdge)Math.Clamp(data.uEdge, 0, 3);
            monitor = MonitorFromRect(ref data.rc, MONITOR_DEFAULTTOPRIMARY);
        }
        else
        {
            monitor = MonitorFromPoint(0, MONITOR_DEFAULTTOPRIMARY); // point (0,0) is always on the primary monitor
        }

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfoW(monitor, ref info))
            return (edge, new Rect(0, 0, 1920, 1080));
        var w = info.rcWork;
        return (edge, new Rect(w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top));
    }

    /// <summary>
    /// Moves a shown window into the taskbar corner (or centered along the taskbar) and returns the edge,
    /// so the caller can slide its content in from that side.
    /// </summary>
    public static TaskbarEdge Place(Window window, bool centered)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var (edge, area) = Taskbar();
        // Twice: moving to a monitor with another DPI resizes the window, which changes the math.
        for (int pass = 0; pass < 2; pass++)
        {
            window.UpdateLayout();
            if (!GetWindowRect(hwnd, out var r))
                break;
            double width = r.Right - r.Left, height = r.Bottom - r.Top;
            double x = edge switch
            {
                TaskbarEdge.Left => area.Left,
                TaskbarEdge.Right => area.Right - width,
                _ => centered ? area.Left + (area.Width - width) / 2 : area.Right - width,
            };
            double y = edge switch
            {
                TaskbarEdge.Top => area.Top,
                TaskbarEdge.Bottom => area.Bottom - height,
                _ => centered ? area.Bottom - height - area.Height * 0.08 : area.Bottom - height,
            };
            SetWindowPos(hwnd, IntPtr.Zero, (int)Math.Round(x), (int)Math.Round(y), 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
        return edge;
    }

    /// <summary>Moves a shown window into the top right corner of the work area, on the taskbar's monitor.</summary>
    public static void PlaceTopRight(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var (_, area) = Taskbar();
        for (int pass = 0; pass < 2; pass++) // twice, for a monitor with another DPI (see Place)
        {
            window.UpdateLayout();
            if (!GetWindowRect(hwnd, out var r))
                break;
            int x = (int)Math.Round(area.Right - (r.Right - r.Left));
            SetWindowPos(hwnd, IntPtr.Zero, x, (int)Math.Round(area.Top), 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    /// <summary>The offset (in DIPs) content starts at when sliding in from the taskbar.</summary>
    public static Vector SlideOffset(TaskbarEdge edge, double distance) => edge switch
    {
        TaskbarEdge.Left => new Vector(-distance, 0),
        TaskbarEdge.Top => new Vector(0, -distance),
        TaskbarEdge.Right => new Vector(distance, 0),
        _ => new Vector(0, distance),
    };

    const int ABM_GETTASKBARPOS = 5;
    const int MONITOR_DEFAULTTOPRIMARY = 1;
    const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [LibraryImport("shell32.dll")]
    private static partial nuint SHAppBarMessage(int message, ref APPBARDATA data);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromRect(ref RECT rect, int flags);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromPoint(long point, int flags); // POINT passed by value

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(IntPtr monitor, ref MONITORINFO info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
