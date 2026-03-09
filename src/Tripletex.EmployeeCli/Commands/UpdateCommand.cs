using System.CommandLine;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace Tripletex.EmployeeCli.Commands;

public static class UpdateCommand
{
    private const string RepoOwner = "finkoslo";
    private const string RepoName = "tripletex-employee-cli";
    private const string ReleasesUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    public static Command Create()
    {
        var cmd = new Command("update", "Update finkletex to the latest version");

        cmd.SetHandler(async () =>
        {
            var currentVersion = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.0.0";

            // Strip build metadata (e.g. 1.0.0+abc123)
            var plusIndex = currentVersion.IndexOf('+');
            if (plusIndex >= 0)
                currentVersion = currentVersion[..plusIndex];

            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("finkletex-updater");

            var release = await AnsiConsole.Status()
                .StartAsync("Checking for updates...", async _ =>
                    await http.GetFromJsonAsync<GitHubRelease>(ReleasesUrl));

            if (release?.TagName is null)
            {
                AnsiConsole.MarkupLine("[red]Could not fetch latest release.[/]");
                return;
            }

            var latestVersion = release.TagName.TrimStart('v');

            if (latestVersion == currentVersion)
            {
                AnsiConsole.MarkupLine($"[green]Already up to date (v{currentVersion}).[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[dim]Current: v{currentVersion} → Latest: v{latestVersion}[/]");

            var (platform, arch) = GetPlatformArch();
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var ext = isWindows ? ".zip" : ".tar.gz";
            var assetName = $"tripletex-employee-{platform}-{arch}{ext}";

            var assetUrl = release.Assets?.FirstOrDefault(a => a.Name == assetName)?.BrowserDownloadUrl;
            if (assetUrl is null)
            {
                AnsiConsole.MarkupLine($"[red]No release asset found for {assetName}.[/]");
                return;
            }

            var currentBinary = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentBinary))
            {
                AnsiConsole.MarkupLine("[red]Cannot determine current binary path.[/]");
                return;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), $"finkletex-update-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var archivePath = Path.Combine(tempDir, assetName);

                await AnsiConsole.Status()
                    .StartAsync("Downloading...", async _ =>
                    {
                        var bytes = await http.GetByteArrayAsync(assetUrl);
                        await File.WriteAllBytesAsync(archivePath, bytes);
                    });

                var binaryName = isWindows ? "finkletex.exe" : "finkletex";
                var extractedBinary = Path.Combine(tempDir, binaryName);

                if (isWindows)
                {
                    ZipFile.ExtractToDirectory(archivePath, tempDir);
                }
                else
                {
                    var tar = Process.Start(new ProcessStartInfo
                    {
                        FileName = "tar",
                        ArgumentList = { "xzf", archivePath, "-C", tempDir },
                        RedirectStandardError = true
                    })!;
                    await tar.WaitForExitAsync();
                    if (tar.ExitCode != 0)
                    {
                        AnsiConsole.MarkupLine("[red]Failed to extract archive.[/]");
                        return;
                    }
                }

                if (!File.Exists(extractedBinary))
                {
                    AnsiConsole.MarkupLine($"[red]Expected binary not found in archive: {binaryName}[/]");
                    return;
                }

                // Self-replace: rename current → .old, move new → current, delete .old
                var backupPath = currentBinary + ".old";
                File.Move(currentBinary, backupPath, overwrite: true);

                try
                {
                    File.Move(extractedBinary, currentBinary);
                }
                catch
                {
                    // Restore backup on failure
                    File.Move(backupPath, currentBinary, overwrite: true);
                    throw;
                }

                try { File.Delete(backupPath); } catch { /* best effort */ }

                if (!isWindows)
                {
                    Process.Start("chmod", ["+x", currentBinary])?.WaitForExit();
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "xattr",
                        ArgumentList = { "-d", "com.apple.quarantine", currentBinary },
                        RedirectStandardError = true
                    })?.WaitForExit();
                }

                AnsiConsole.MarkupLine($"[green]Updated to v{latestVersion}![/]");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
            }
        });

        return cmd;
    }

    private static (string platform, string arch) GetPlatformArch()
    {
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
            : "linux";

        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };

        return (platform, arch);
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
