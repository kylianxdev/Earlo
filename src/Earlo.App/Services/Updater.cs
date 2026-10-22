using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Earlo.App.Services;

public sealed record UpdateInfo(Version Version, string AssetName, string DownloadUrl, string Sha256, string PageUrl);

/// <summary>
/// Keeps the GitHub version of Earlo up to date. The Store version is updated by the Store.
/// </summary>
/// <remarks>
/// Reads the latest GitHub release, downloads the zip for this build flavour and architecture,
/// checks it against the SHA-256 digest GitHub publishes for the asset, swaps Earlo.exe
/// (a running exe can be renamed, not overwritten) and restarts.
/// </remarks>
public static class Updater
{
    public const string UpdatedArgument = "--updated";
    const string LatestReleaseUrl = "https://api.github.com/repos/kylianxdev/Earlo/releases/latest";

    static readonly Lazy<HttpClient> LazyHttp = new(CreateClient);

    static HttpClient Http => LazyHttp.Value;

    /// <summary>standalone, small, store or dev; set at publish time with -p:EarloFlavor.</summary>
    public static string Flavor { get; } =
        typeof(Updater).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "EarloFlavor")?.Value ?? "dev";

    public static Version CurrentVersion { get; } = typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0);

    /// <summary>Only release builds downloaded from GitHub update themselves.</summary>
    public static bool Supported => !PackageInfo.IsPackaged && Flavor is "standalone" or "small";

    /// <summary>Returns the newer release for this build, or null when up to date.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(LatestReleaseUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var release = json.RootElement;

        string tag = release.GetProperty("tag_name").GetString() ?? string.Empty;
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest) || Normalize(latest) <= Normalize(CurrentVersion))
            return null;

        string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string assetName = $"Earlo-win-{arch}{(Flavor == "small" ? "-small" : string.Empty)}.zip";

        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != assetName)
                continue;
            // GitHub publishes "sha256:<hex>" for every uploaded asset; without it we do not update.
            string? digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
            if (digest is null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                return null;
            return new UpdateInfo(latest, assetName, asset.GetProperty("browser_download_url").GetString()!,
                digest["sha256:".Length..], release.GetProperty("html_url").GetString() ?? string.Empty);
        }
        return null;
    }

    /// <summary>
    /// Downloads, verifies and installs the update, then starts the new version.
    /// Returns false when it could not (the caller keeps running); on success the caller should exit.
    /// </summary>
    public static async Task<bool> InstallAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown exe path.");
        string folder = Path.Combine(Path.GetTempPath(), "Earlo-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string zip = Path.Combine(folder, update.AssetName);
            await using (var file = File.Create(zip))
            await using (var download = await Http.GetStreamAsync(update.DownloadUrl, cancellationToken))
                await download.CopyToAsync(file, cancellationToken);

            string hash;
            await using (var file = File.OpenRead(zip))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
            if (!hash.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded update does not match its published checksum.");

            using (var archive = ZipFile.OpenRead(zip))
            {
                var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals("Earlo.exe", StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException("The update does not contain Earlo.exe.");
                entry.ExtractToFile(Path.Combine(folder, "Earlo.exe"));
            }

            string old = exe + ".old";
            File.Delete(old);
            File.Move(exe, old);
            try
            {
                File.Move(Path.Combine(folder, "Earlo.exe"), exe);
            }
            catch
            {
                File.Move(old, exe); // put the running version back
                throw;
            }

            Process.Start(new ProcessStartInfo(exe, $"--minimized {UpdatedArgument}") { UseShellExecute = false });
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or HttpRequestException)
        {
            // For example Earlo.exe in a folder the user cannot write to.
            CrashLog.Write(ex);
            return false;
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Removes the previous exe left behind by an update.</summary>
    public static void CleanUp()
    {
        if (Environment.ProcessPath is not { } exe)
            return;
        try
        {
            File.Delete(exe + ".old");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Earlo/{CurrentVersion.ToString(3)}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
