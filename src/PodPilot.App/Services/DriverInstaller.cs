using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PodPilot.App.Services;

public enum DriverPackageKind
{
    /// <summary>This build carries no driver (for example a local debug build).</summary>
    None,
    /// <summary>Signed with a test certificate: only loads with test signing or the F7 boot option.</summary>
    TestSigned,
    /// <summary>Attestation-signed by Microsoft: installs and loads normally, Secure Boot stays on.</summary>
    MicrosoftSigned,
}

/// <summary>
/// Installs the PodPilot AAP driver that is embedded in PodPilot.exe (see the DriverPackage items
/// in PodPilot.App.csproj). Installing needs admin rights, so PodPilot starts a second, elevated copy of
/// itself with --install-driver; that copy extracts the package from its own exe into a folder only
/// administrators can change, and hands it to pnputil.
/// </summary>
public static partial class DriverInstaller
{
    const string ResourcePrefix = "driver/";
    const string InstallArgument = "--install-driver";
    const string UninstallArgument = "--uninstall-driver";

    public static DriverPackageKind EmbeddedPackage { get; } = DetectPackage();

    public static bool IsElevatedCommand(string? argument) => argument is InstallArgument or UninstallArgument;

    /// <summary>True when the driver package is in the Windows driver store.</summary>
    public static bool IsInstalled()
    {
        try
        {
            return FindPublishedInfs().Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Asks for admin rights (UAC) and installs the embedded driver. False when declined or failed.</summary>
    public static Task<bool> InstallAsync() => RunElevatedAsync(InstallArgument);

    public static Task<bool> UninstallAsync() => RunElevatedAsync(UninstallArgument);

    /// <summary>Entry point of the elevated copy. Returns the process exit code.</summary>
    public static int RunElevatedCommand(string argument)
    {
        try
        {
            return argument == InstallArgument ? Install() : Uninstall();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return 1;
        }
    }

    static DriverPackageKind DetectPackage()
    {
        var assembly = typeof(DriverInstaller).Assembly;
        if (assembly.GetManifestResourceInfo(ResourcePrefix + "PodPilotAap.inf") is null)
            return DriverPackageKind.None;

        using var marker = assembly.GetManifestResourceStream(ResourcePrefix + "signing.txt");
        string kind = marker is null ? string.Empty : new StreamReader(marker).ReadToEnd().Trim();
        return kind == "microsoft" ? DriverPackageKind.MicrosoftSigned : DriverPackageKind.TestSigned;
    }

    static Task<bool> RunElevatedAsync(string argument) => Task.Run(() =>
    {
        if (EmbeddedPackage == DriverPackageKind.None && argument == InstallArgument)
            return false;
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, argument)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            if (process is null)
                return false;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false; // the user declined the UAC prompt
        }
    });

    static int Install()
    {
        // ProgramData subfolders created by an administrator cannot be modified by normal users.
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PodPilot", "driver");
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);

        var assembly = typeof(DriverInstaller).Assembly;
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var source = assembly.GetManifestResourceStream(name)!;
            using var target = File.Create(Path.Combine(folder, name[ResourcePrefix.Length..]));
            source.CopyTo(target);
        }

        if (EmbeddedPackage == DriverPackageKind.TestSigned)
            TrustTestCertificate(Path.Combine(folder, "PodPilotAap.cer"));

        int code = Run("pnputil.exe", $"/add-driver \"{Path.Combine(folder, "PodPilotAap.inf")}\" /install", out _);
        // 3010: reboot required. 259: added to the driver store, but no matching device right now
        // (AirPods not paired yet); Windows installs it as soon as they are.
        return code is 0 or 3010 or 259 ? 0 : code;
    }

    static int Uninstall()
    {
        int result = 0;
        foreach (string inf in FindPublishedInfs())
        {
            int code = Run("pnputil.exe", $"/delete-driver {inf} /uninstall /force", out _);
            if (code is not (0 or 3010))
                result = code;
        }
        return result;
    }

    static void TrustTestCertificate(string path)
    {
        if (!File.Exists(path))
            return;
        var certificate = X509CertificateLoader.LoadCertificateFromFile(path);
        foreach (var storeName in new[] { StoreName.Root, StoreName.TrustedPublisher })
        {
            using var store = new X509Store(storeName, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
        }
    }

    /// <summary>Published names (oemNN.inf) of PodPilot driver packages in the driver store.</summary>
    static List<string> FindPublishedInfs()
    {
        Run("pnputil.exe", "/enum-drivers", out string output);
        // pnputil output is localised, so match on the file names only.
        return DriverBlock().Split(output)
            .Where(block => block.Contains("podpilotaap.inf", StringComparison.OrdinalIgnoreCase))
            .Select(block => OemInf().Match(block))
            .Where(match => match.Success)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static int Run(string file, string arguments, out string output)
    {
        using var process = Process.Start(new ProcessStartInfo(file, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    [GeneratedRegex(@"(\r?\n){2,}")]
    private static partial Regex DriverBlock();

    [GeneratedRegex(@"oem\d+\.inf", RegexOptions.IgnoreCase)]
    private static partial Regex OemInf();
}
