using System.Text.Json;
using System.Text.RegularExpressions;

namespace CataMedia.Windows;

public sealed record AppRelease(string Version, string Url);
public sealed record AppReleaseCheck(AppRelease? Latest, bool UpdateAvailable);

/// <summary>Checks stable C# Windows releases only; never downloads or installs an application.</summary>
public sealed class AppReleaseChecker(HttpClient client, string installedVersion)
{
    private readonly (Version Number, bool Preview) installed = ParseInstalled(installedVersion);
    private AppReleaseCheck? cached;
    private DateTimeOffset checkedAt;

    public async Task<AppReleaseCheck> CheckAsync(bool force, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!force && cached is not null && checkedAt <= DateTimeOffset.UtcNow && DateTimeOffset.UtcNow - checkedAt < TimeSpan.FromHours(6)) return cached;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        AppRelease? latest = null;
        Version? latestNumber = null;
        for (var page = 1; page <= 3; page++)
        {
            using var response = await client.GetAsync($"https://api.github.com/repos/Wisleyv/media_download/releases?per_page=100&page={page}",
                HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var body = new MemoryStream();
            var buffer = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(buffer, timeout.Token)) != 0)
            {
                if (body.Length + count > 2_000_000) throw new InvalidDataException("Application release metadata too large.");
                await body.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
            }
            using var json = JsonDocument.Parse(body.ToArray());
            foreach (var release in json.RootElement.EnumerateArray())
            {
                if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) continue;
                var tag = release.GetProperty("tag_name").GetString() ?? "";
                var match = Regex.Match(tag, @"\Av?(3\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))\z", RegexOptions.CultureInvariant);
                if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var number)) continue;
                var version = match.Groups[1].Value;
                var names = release.GetProperty("assets").EnumerateArray()
                    .Select(asset => asset.GetProperty("name").GetString()).ToHashSet(StringComparer.Ordinal);
                if (!names.Contains($"CataMedia-{version}-win-x64-portable.zip") ||
                    !names.Contains($"CataMedia-Setup-{version}-win-x64.exe") || !names.Contains("SHA256SUMS.txt")) continue;
                var expectedUrl = $"https://github.com/Wisleyv/media_download/releases/tag/{Uri.EscapeDataString(tag)}";
                if (release.GetProperty("html_url").GetString() != expectedUrl)
                    throw new InvalidDataException("Unexpected application release page.");
                if (latestNumber is null || number > latestNumber) { latestNumber = number; latest = new(version, expectedUrl); }
            }
            var more = response.Headers.TryGetValues("Link", out var links) && links.Any(link => link.Contains("rel=\"next\"", StringComparison.Ordinal));
            if (!more) break;
            if (page == 3) throw new InvalidDataException("Application release catalog exceeds the search limit.");
        }
        var result = new AppReleaseCheck(latest, latestNumber is not null &&
            (latestNumber > installed.Number || (latestNumber == installed.Number && installed.Preview)));
        cached = result; checkedAt = DateTimeOffset.UtcNow;
        return result;
    }

    private static (Version, bool) ParseInstalled(string value)
    {
        var match = Regex.Match(value.Split('+')[0], @"\Av?(3\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))(-[0-9A-Za-z.-]+)?\z", RegexOptions.CultureInvariant);
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var version))
            throw new InvalidDataException("Unrecognized installed CataMedia C# version.");
        return (version, match.Groups[2].Success);
    }
}
