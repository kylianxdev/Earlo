using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using PodPilot.App.Services;

namespace PodPilot.App;

/// <summary>User settings, saved as JSON in %APPDATA%\PodPilot. Saves itself on every change.</summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodPilot");
    static readonly string FilePath = Path.Combine(Folder, "settings.json");

    bool _startWithWindows = true;
    bool _popupOnLidOpen = true;
    bool _popupOnConnect;
    int _popupSeconds = 8;
    bool _earDetection = true;
    int _pauseMode;
    bool _resumeOnInsert = true;
    bool _lowBatteryWarning = true;
    int _lowBatteryThreshold = 20;
    bool _trayShowsBattery;
    int _rssiThreshold = -70;
    bool _requireConnected = true;
    bool _volumeSwipe = true;
    bool _firstRunDone;
    bool _autoUpdate = true;
    bool _autoConnect;
    bool _volumeOsd = true;

    public bool StartWithWindows { get => _startWithWindows; set => Set(ref _startWithWindows, value); }
    public bool PopupOnLidOpen { get => _popupOnLidOpen; set => Set(ref _popupOnLidOpen, value); }
    public bool PopupOnConnect { get => _popupOnConnect; set => Set(ref _popupOnConnect, value); }
    public int PopupSeconds { get => _popupSeconds; set => Set(ref _popupSeconds, Math.Clamp(value, 3, 30)); }
    public bool EarDetection { get => _earDetection; set => Set(ref _earDetection, value); }

    /// <summary>0 = pause when one pod is removed, 1 = only when both are removed.</summary>
    public int PauseMode { get => _pauseMode; set => Set(ref _pauseMode, Math.Clamp(value, 0, 1)); }

    public bool ResumeOnInsert { get => _resumeOnInsert; set => Set(ref _resumeOnInsert, value); }
    public bool LowBatteryWarning { get => _lowBatteryWarning; set => Set(ref _lowBatteryWarning, value); }
    public int LowBatteryThreshold { get => _lowBatteryThreshold; set => Set(ref _lowBatteryThreshold, Math.Clamp(value, 10, 50)); }
    public bool TrayShowsBattery { get => _trayShowsBattery; set => Set(ref _trayShowsBattery, value); }
    public int RssiThreshold { get => _rssiThreshold; set => Set(ref _rssiThreshold, Math.Clamp(value, -95, -40)); }

    /// <summary>Only pause/resume media when AirPods are connected to this PC.</summary>
    public bool RequireConnected { get => _requireConnected; set => Set(ref _requireConnected, value); }

    /// <summary>Turn on swipe-to-change-volume on the AirPods stem (needs the AAP driver).</summary>
    public bool VolumeSwipe { get => _volumeSwipe; set => Set(ref _volumeSwipe, value); }

    public bool FirstRunDone { get => _firstRunDone; set => Set(ref _firstRunDone, value); }

    /// <summary>Install new GitHub releases automatically (the Store version updates through the Store).</summary>
    public bool AutoUpdate { get => _autoUpdate; set => Set(ref _autoUpdate, value); }

    /// <summary>Connect the earbuds to this PC when they go in your ears (like automatic switching on Apple devices).</summary>
    public bool AutoConnect { get => _autoConnect; set => Set(ref _autoConnect, value); }

    /// <summary>Show a volume indicator when the volume changes from the earbuds (stem swipe), which Windows does not show.</summary>
    public bool VolumeOsd { get => _volumeOsd; set => Set(ref _volumeOsd, value); }

    [JsonIgnore]
    public bool Persist { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static AppSettings Load()
    {
        _loading = true;
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), PodPilotJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception)
        {
            // A corrupt file falls back to defaults.
        }
        finally
        {
            _loading = false;
        }
        return new AppSettings();
    }

    [ThreadStatic]
    static bool _loading;

    public void Save()
    {
        if (!Persist || _loading)
            return;
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, PodPilotJsonContext.Default.AppSettings));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return;
        field = value;
        Save();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
