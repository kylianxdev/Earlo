using System;
using System.IO;

namespace Earlo.App.Services;

/// <summary>
/// %APPDATA%\Earlo, where settings, the case battery memory and the crash log live.
/// Earlo used to be called PodPilot: the first start moves %APPDATA%\PodPilot here, so nothing is lost.
/// </summary>
public static class DataFolder
{
    public static string Root { get; } = Prepare();

    static string Prepare()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "Earlo");
        string legacy = Path.Combine(appData, "PodPilot");
        try
        {
            if (!Directory.Exists(folder) && Directory.Exists(legacy))
                Directory.Move(legacy, folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return legacy; // locked by a PodPilot that is still running: keep using it, move next time
        }
        return folder;
    }
}
