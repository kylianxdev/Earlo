using System;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using PodPilot.Core.Tracking;
using Forms = System.Windows.Forms;

namespace PodPilot.App.Services;

/// <summary>Notification-area icon. Optionally draws the lowest battery level as a number.</summary>
public sealed partial class TrayIcon : IDisposable
{
    readonly Forms.NotifyIcon _icon;
    readonly Icon _appIcon;
    IntPtr _dynamicHandle;
    string _lastKey = string.Empty;

    public TrayIcon()
    {
        _appIcon = LoadAppIcon();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show battery", null, (_, _) => ShowPopup?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Settings", null, (_, _) => OpenSettings?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new Forms.NotifyIcon
        {
            Icon = _appIcon,
            Text = "PodPilot",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                ShowPopup?.Invoke(this, EventArgs.Empty);
        };
        _icon.DoubleClick += (_, _) => OpenSettings?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ShowPopup;
    public event EventHandler? OpenSettings;
    public event EventHandler? ExitRequested;

    public void Update(PodsSnapshot? snapshot, string? name, bool showNumber)
    {
        _icon.Text = Truncate(BuildTooltip(snapshot, name), 127);

        var lowest = snapshot?.Lowest ?? BatteryState.Unknown;
        string key = showNumber && lowest.IsKnown ? $"{lowest.Percent}|{lowest.Charging}|{Theme.TaskbarUsesLight}" : "app";
        if (key == _lastKey)
            return;
        _lastKey = key;

        var old = _dynamicHandle;
        if (key == "app")
        {
            _icon.Icon = _appIcon;
            _dynamicHandle = IntPtr.Zero;
        }
        else
        {
            _dynamicHandle = RenderNumberIcon(lowest.Percent!.Value, lowest.Charging, Theme.TaskbarUsesLight);
            _icon.Icon = Icon.FromHandle(_dynamicHandle);
        }

        if (old != IntPtr.Zero)
            DestroyIcon(old);
    }

    /// <summary>Forces a redraw, for example after the taskbar theme changed.</summary>
    public void Invalidate() => _lastKey = string.Empty;

    public void Notify(string title, string text) =>
        _icon.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.Info);

    static string BuildTooltip(PodsSnapshot? s, string? name)
    {
        if (s is null)
            return "PodPilot: no AirPods nearby";

        string title = name ?? s.Device.Name;
        if (s.Device.HasSingleBattery)
            return $"{title}\n{Format(s.Single)}";
        return $"{title}\nL {Format(s.Left)}  R {Format(s.Right)}  Case {Format(s.Case)}";
    }

    static string Format(BatteryState b) => b.Percent is int p ? $"{p}%{(b.Charging ? "+" : "")}" : "--";

    static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)];

    static Icon LoadAppIcon()
    {
        try
        {
            var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/podpilot.ico"))?.Stream;
            if (stream is not null)
                return new Icon(stream, Forms.SystemInformation.SmallIconSize);
        }
        catch (Exception)
        {
        }
        return SystemIcons.Application;
    }

    static IntPtr RenderNumberIcon(int percent, bool charging, bool lightTaskbar)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var color = percent <= 20 && !charging
            ? Color.FromArgb(255, 255, 69, 58)
            : lightTaskbar ? Color.FromArgb(255, 28, 28, 30) : Color.White;

        string text = percent.ToString();
        float fontSize = text.Length >= 3 ? 15f : 22f;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, new RectangleF(0, -2, size, size - 4), format);

        using var bar = new SolidBrush(charging ? Color.FromArgb(255, 52, 199, 89) : color);
        g.FillRectangle(bar, 4, size - 5, (size - 8) * Math.Clamp(percent, 5, 100) / 100f, 4);

        return bmp.GetHicon();
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        if (_dynamicHandle != IntPtr.Zero)
            DestroyIcon(_dynamicHandle);
    }
}
