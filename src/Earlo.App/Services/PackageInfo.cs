using System;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Activation;

namespace Earlo.App.Services;

/// <summary>Whether Earlo runs as an MSIX package (the Microsoft Store version) or as a plain exe.</summary>
public static partial class PackageInfo
{
    const int AppModelErrorNoPackage = 15700;

    public static bool IsPackaged { get; } = DetectPackaged();

    /// <summary>True when Windows started the Store version at sign-in through its startup task.</summary>
    public static bool StartedByStartupTask()
    {
        if (!IsPackaged)
            return false;
        try
        {
            return Windows.ApplicationModel.AppInstance.GetActivatedEventArgs()?.Kind == ActivationKind.StartupTask;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static bool DetectPackaged()
    {
        int length = 0;
        return GetCurrentPackageFullName(ref length, IntPtr.Zero) != AppModelErrorNoPackage;
    }

    [LibraryImport("kernel32.dll")]
    private static partial int GetCurrentPackageFullName(ref int length, IntPtr name);
}
