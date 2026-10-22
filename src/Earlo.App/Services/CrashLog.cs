using System;
using System.IO;

namespace Earlo.App.Services;

/// <summary>Appends unexpected errors to %APPDATA%\Earlo\crash.log, for bug reports.</summary>
public static class CrashLog
{
    public static readonly string FilePath = Path.Combine(DataFolder.Root, "crash.log");

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"--- {DateTimeOffset.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
