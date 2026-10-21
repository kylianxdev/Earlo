using System;
using Microsoft.Win32;

namespace PodPilot.App.Services;

/// <summary>Registers PodPilot in the per-user Run key, so no admin rights are needed.</summary>
public static class AutoStart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "PodPilot";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled && Environment.ProcessPath is { } exe)
                key.SetValue(ValueName, $"\"{exe}\" --minimized");
            else if (!enabled)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception)
        {
            // Policy-locked registries are not worth crashing for.
        }
    }
}
