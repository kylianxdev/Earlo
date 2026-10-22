using System;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Earlo.App.Services;

/// <summary>
/// Starts Earlo at sign-in: through the package's startup task in the Store version, and
/// through the per-user Run key for the plain exe (no admin rights needed either way).
/// </summary>
public static class AutoStart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "Earlo";
    const string LegacyValueName = "PodPilot"; // the Run value from before the rename
    // Matches store/AppxManifest.xml. Still the PodPilot-era id: Windows remembers the user's choice per id.
    const string StartupTaskId = "PodPilotStartup";

    public static async Task ApplyAsync(bool enabled)
    {
        try
        {
            if (PackageInfo.IsPackaged)
                await ApplyStartupTaskAsync(enabled);
            else
                ApplyRunKey(enabled);
        }
        catch (Exception)
        {
            // Policy-locked registries or a disabled startup task are not worth crashing for.
        }
    }

    static async Task ApplyStartupTaskAsync(bool enabled)
    {
        var task = await StartupTask.GetAsync(StartupTaskId);
        if (enabled && task.State == StartupTaskState.Disabled)
            await task.RequestEnableAsync();
        else if (!enabled && task.State is StartupTaskState.Enabled)
            task.Disable();
    }

    static void ApplyRunKey(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        if (enabled && Environment.ProcessPath is { } exe)
            key.SetValue(ValueName, $"\"{exe}\" --minimized");
        else if (!enabled)
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
