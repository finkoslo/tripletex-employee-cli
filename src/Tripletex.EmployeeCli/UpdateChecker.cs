using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Tripletex.EmployeeCli;

public static class UpdateChecker
{
    private const string RepoOwner = "finkoslo";
    private const string RepoName = "tripletex-employee-cli";
    public const string ReleasesUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    public static async Task<string?> CheckForUpdateAsync()
    {
        try
        {
            var currentVersion = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.0.0";

            var plusIndex = currentVersion.IndexOf('+');
            if (plusIndex >= 0)
                currentVersion = currentVersion[..plusIndex];

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("finkletex-updater");

            var release = await http.GetFromJsonAsync<GitHubRelease>(ReleasesUrl);
            if (release?.TagName is null)
                return null;

            var latestVersion = release.TagName.TrimStart('v');
            return latestVersion != currentVersion ? latestVersion : null;
        }
        catch
        {
            return null;
        }
    }

    public static string GetCurrentVersion()
    {
        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

        var plusIndex = version.IndexOf('+');
        return plusIndex >= 0 ? version[..plusIndex] : version;
    }

    public sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    public sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
