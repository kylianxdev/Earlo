using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using PodPilot.App.Controls;
using PodPilot.Core.Bluetooth;

namespace PodPilot.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        DataContext = settings;
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"PodPilot {version?.ToString(3)}";
    }

    public event EventHandler? ShowPopupRequested;

    public void Update(DeviceViewState state, IReadOnlyList<PairedDeviceInfo> paired)
    {
        Panel.Update(state);
        PairedText.Text = paired.Count == 0
            ? "No AirPods or Beats are paired with this PC yet. Pair them in Bluetooth settings for automatic ear detection."
            : "Paired: " + string.Join(", ", paired.Select(p => p.IsConnected ? $"{p.Name} (connected)" : p.Name));
    }

    void OnShowPopupClick(object sender, RoutedEventArgs e) => ShowPopupRequested?.Invoke(this, EventArgs.Empty);

    void OnBluetoothClick(object sender, RoutedEventArgs e) => OpenUri("ms-settings:bluetooth");

    void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        OpenUri(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    static void OpenUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }
}
