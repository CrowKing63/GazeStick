using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace GazeStick.Services;

public sealed class UpdateChecker
{
    private const string ApiUrl = "https://api.github.com/repos/CrowKing63/GazeStick/releases/latest";
    private static readonly Version CurrentVersion = ParseCurrentVersion();

    private static Version ParseCurrentVersion()
    {
        var raw = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";
        var plusIdx = raw.IndexOf('+');
        if (plusIdx >= 0) raw = raw[..plusIdx];
        return Version.TryParse(raw, out var v) ? v : new Version(0, 0, 0);
    }

    public static string CurrentVersionString => CurrentVersion.ToString();

    public static async Task<Version?> CheckForUpdateAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GazeStick", CurrentVersionString));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync(ApiUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var tagName = doc.RootElement.GetProperty("tag_name").GetString();

            if (tagName == null) return null;
            tagName = tagName.TrimStart('v');

            return Version.TryParse(tagName, out var latest) ? latest : null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsNewerAvailable(Version? latest)
    {
        return latest != null && latest > CurrentVersion;
    }
}
