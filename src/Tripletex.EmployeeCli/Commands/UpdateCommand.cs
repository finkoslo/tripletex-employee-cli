using System.CommandLine;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace Tripletex.EmployeeCli.Commands;

public static class UpdateCommand
{
    public static Command Create()
    {
        var cmd = new Command("update", "Update finkletex to the latest version");

        cmd.SetHandler(async () =>
        {
            var currentVersion = UpdateChecker.GetCurrentVersion();

            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("finkletex-updater");

            var release = await AnsiConsole.Status()
                .StartAsync("Checking for updates...", async _ =>
                    await http.GetFromJsonAsync<UpdateChecker.GitHubRelease>(UpdateChecker.ReleasesUrl));

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

                // Check write permissions before attempting self-replace
                if (!HasWritePermission(currentBinary))
                {
                    if (!isWindows)
                    {
                        AnsiConsole.MarkupLine("[yellow]No write permission to install directory. Re-running with sudo...[/]");
                        Environment.Exit(ReExecWithSudo(currentBinary));
                        return;
                    }

                    AnsiConsole.MarkupLine("[red]No write permission to install directory. Please run as Administrator.[/]");
                    return;
                }

                // Self-replace: rename current → .old, move new → current, delete .old
                var backupPath = currentBinary + ".old";
                try
                {
                    File.Move(currentBinary, backupPath, overwrite: true);
                }
                catch (UnauthorizedAccessException)
                {
                    AnsiConsole.MarkupLine(isWindows
                        ? "[red]Permission denied. Please run as Administrator.[/]"
                        : "[red]Permission denied. Please run with sudo.[/]");
                    return;
                }

                try
                {
                    File.Move(extractedBinary, currentBinary);
                }
                catch
                {
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

    private static bool HasWritePermission(string binaryPath)
    {
        var dir = Path.GetDirectoryName(binaryPath)!;
        var testFile = Path.Combine(dir, $".finkletex-write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static int ReExecWithSudo(string currentBinary)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "sudo",
            ArgumentList = { currentBinary, "update" },
            UseShellExecute = false
        });

        process?.WaitForExit();
        return process?.ExitCode ?? 1;
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

}
