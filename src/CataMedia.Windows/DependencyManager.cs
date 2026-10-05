using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CataMedia.Windows;

public enum DependencyKind { YtDlp, Ffmpeg, Node }
public sealed record DependencyRelease(DependencyKind Kind, string Version, string PackageUrl, string Sha256, string Source)
{
    public string License => Kind switch
    {
        DependencyKind.YtDlp => "GPL-3.0-or-later (Windows bundled executable; see upstream third-party licenses)",
        DependencyKind.Ffmpeg => "GPL-3.0-or-later (Gyan essentials build; see upstream notices)",
        DependencyKind.Node => "MIT and bundled third-party licenses (see Node.js LICENSE)",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
}
public sealed record DependencyActivation(string Active, string? Previous);
public sealed record DependencyDownloadProgress(long ReceivedBytes, long? TotalBytes);

/// <summary>Versioned external components. Preparation never overwrites a working executable.</summary>
public sealed class DependencyManager
{
    private readonly string root;
    private readonly HttpClient client;
    private readonly IProcessRunner runner;
    private readonly SemaphoreSlim mutation = new(1, 1);
    private sealed record Cache(DateTimeOffset CheckedAt, string? ETag, DependencyRelease Release);
    public DependencyManager(string root, HttpClient client, IProcessRunner runner)
    {
        this.root = Path.GetFullPath(root);
        this.client = client;
        this.runner = runner;
    }

    public static HttpClient CreateClient()
    {
        var result = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        result.DefaultRequestHeaders.UserAgent.ParseAdd("CataMedia/3.0");
        return result;
    }

    private string ComponentRoot(DependencyKind kind) => Path.Combine(root, kind.ToString());
    private string ManifestPath(DependencyKind kind) => Path.Combine(ComponentRoot(kind), "active.json");
    private static string Executable(DependencyKind kind) => kind switch
    {
        DependencyKind.YtDlp => "yt-dlp.exe", DependencyKind.Ffmpeg => "ffmpeg.exe", DependencyKind.Node => "node.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public string? ActiveExecutable(DependencyKind kind)
    {
        var manifest = ReadManifest(kind);
        if (manifest is null) return null;
        var path = Path.Combine(ComponentRoot(kind), "versions", manifest.Active, Executable(kind));
        return File.Exists(path) ? path : null;
    }

    private DependencyActivation? ReadManifest(DependencyKind kind)
    {
        if (!File.Exists(ManifestPath(kind))) return null;
        var result = JsonSerializer.Deserialize<DependencyActivation>(File.ReadAllText(ManifestPath(kind)))
            ?? throw new InvalidDataException("Invalid component activation.");
        ValidateFolder(result.Active);
        if (result.Previous is not null) ValidateFolder(result.Previous);
        return result;
    }

    private static void ValidateFolder(string value)
    {
        if (!Regex.IsMatch(value, @"\A[a-zA-Z0-9._-]{1,100}\z") || value is "." or "..")
            throw new InvalidDataException("Invalid component folder.");
    }

    public async Task<DependencyRelease> CheckAsync(DependencyKind kind, bool force, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        var token = timeout.Token;
        var cachePath = Path.Combine(ComponentRoot(kind), "release-cache.json");
        Cache? cache = null;
        try { if (File.Exists(cachePath)) cache = JsonSerializer.Deserialize<Cache>(await File.ReadAllTextAsync(cachePath, token)); }
        catch (Exception e) when (e is IOException or JsonException) { }
        if (!force && cache is not null && cache.CheckedAt <= DateTimeOffset.UtcNow &&
            DateTimeOffset.UtcNow - cache.CheckedAt < TimeSpan.FromHours(6))
        {
            ValidateRelease(cache.Release, kind);
            return cache.Release;
        }
        DependencyRelease release;
        string? etag = null;
        if (kind == DependencyKind.YtDlp)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");
            if (cache?.ETag is { } existing) request.Headers.TryAddWithoutValidation("If-None-Match", existing);
            using var response = await SendAsync(request, token);
            if (response.StatusCode == HttpStatusCode.NotModified && cache is not null) release = cache.Release;
            else
            {
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await ReadBoundedAsync(response, 2_000_000, token));
                var value = json.RootElement;
                if (value.GetProperty("prerelease").GetBoolean() || value.GetProperty("draft").GetBoolean())
                    throw new InvalidDataException("Only stable releases are supported.");
                var version = value.GetProperty("tag_name").GetString()!;
                var assets = value.GetProperty("assets").EnumerateArray().ToArray();
                string Asset(string name) => assets.Single(a => a.GetProperty("name").GetString() == name)
                    .GetProperty("browser_download_url").GetString()!;
                var package = Asset("yt-dlp.exe");
                var sumsUrl = Asset("SHA2-256SUMS");
                ValidateSource(new Uri(sumsUrl));
                if (sumsUrl != $"https://github.com/yt-dlp/yt-dlp/releases/download/{version}/SHA2-256SUMS")
                    throw new InvalidDataException("Unexpected checksum source.");
                var hash = FindHash(await GetTextAsync(sumsUrl, token), "yt-dlp.exe");
                release = new(kind, version, package, hash, "https://github.com/yt-dlp/yt-dlp");
            }
            etag = response.Headers.ETag?.ToString() ?? cache?.ETag;
        }
        else if (kind == DependencyKind.Ffmpeg)
        {
            const string package = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
            var version = (await GetTextAsync("https://www.gyan.dev/ffmpeg/builds/release-version", token)).Trim();
            var hash = Regex.Match(await GetTextAsync(package + ".sha256", token), @"\b[0-9a-fA-F]{64}\b").Value;
            release = new(kind, version, package, hash, "https://www.gyan.dev/ffmpeg/builds/");
        }
        else
        {
            // v22 is the supported LTS line selected for this preview, independent of app code patch versions.
            var index = await GetTextAsync("https://nodejs.org/dist/latest-v22.x/", token);
            var version = Regex.Match(index, @"node-(v22\.\d+\.\d+)-win-x64\.zip").Groups[1].Value;
            if (version.Length == 0) throw new InvalidDataException("Node.js release not found.");
            var baseUrl = "https://nodejs.org/dist/" + version + "/";
            var hash = FindHash(await GetTextAsync(baseUrl + "SHASUMS256.txt", token), "win-x64/node.exe");
            release = new(kind, version, baseUrl + "win-x64/node.exe", hash, "https://nodejs.org/");
        }
        ValidateRelease(release, kind);
        // Cache is advisory: a read-only data folder must not hide a successfully checked version.
        try { await WriteJsonAsync(cachePath, new Cache(DateTimeOffset.UtcNow, etag, release), token); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return release;
    }

    public static bool IsNewer(DependencyKind kind, string available, string installed)
    {
        if (kind == DependencyKind.YtDlp)
            return DateTime.TryParseExact(available, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var a) &&
                DateTime.TryParseExact(installed.Trim().Length >= 10 ? installed.Trim()[..10] : installed.Trim(), "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var b) && a > b;
        return Version.TryParse(available.TrimStart('v'), out var av) && Version.TryParse(installed.TrimStart('v'), out var bv) && av > bv;
    }

    public async Task<string> ReadVersionAsync(DependencyKind kind, string executable, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var output = new List<string>();
        void Receive(string line) { lock (output) { if (output.Sum(s => s.Length) + line.Length > 32_768) throw new InvalidDataException("Component version output too large."); output.Add(line); } }
        var code = await runner.RunAsync(new(executable, [kind == DependencyKind.Ffmpeg ? "-version" : "--version"],
            Path.GetDirectoryName(Path.GetFullPath(executable))!), Receive, Receive, timeout.Token);
        if (code != 0) throw new InvalidDataException("Component version check failed.");
        var text = string.Join('\n', output);
        var pattern = kind switch
        {
            DependencyKind.YtDlp => @"(?m)^\d{4}\.\d{2}\.\d{2}(?:\.\d+)?$",
            DependencyKind.Node => @"(?m)^v\d+\.\d+\.\d+$",
            _ => @"(?m)^(?:ffmpeg|ffprobe) version (\d+\.\d+(?:\.\d+)?)"
        };
        var match = Regex.Match(text.Replace("\r", ""), pattern);
        if (!match.Success) throw new InvalidDataException("Unrecognized component version.");
        var version = kind == DependencyKind.Ffmpeg ? match.Groups[1].Value : match.Value;
        if (kind == DependencyKind.Node && (!Version.TryParse(version[1..], out var node) || node.Major < 22))
            throw new InvalidDataException("Node.js 22 or newer is required.");
        return version;
    }

    public async Task<string> InstallAsync(DependencyRelease release, IProgress<string>? progress, CancellationToken cancellationToken,
        IProgress<DependencyDownloadProgress>? downloadProgress = null)
    {
        ValidateRelease(release, release.Kind);
        await mutation.WaitAsync(cancellationToken);
        var component = ComponentRoot(release.Kind);
        var staging = Path.Combine(component, "staging", Guid.NewGuid().ToString("N"));
        try
        {
            if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("Windows x64 is required.");
            Directory.CreateDirectory(staging);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            var token = timeout.Token;
            progress?.Report("DownloadingComponent");
            var package = Path.Combine(staging, "package");
            await DownloadAsync(release.PackageUrl, package, release.Kind == DependencyKind.Ffmpeg ? 250_000_000 : 150_000_000, token, downloadProgress);
            progress?.Report("ValidatingComponent");
            await using (var stream = File.OpenRead(package))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
                if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 mismatch. Nothing was activated.");
            }
            var prepared = Path.Combine(staging, "prepared");
            Directory.CreateDirectory(prepared);
            if (release.Kind == DependencyKind.Ffmpeg) await ExtractPairAsync(package, prepared, token);
            else File.Move(package, Path.Combine(prepared, Executable(release.Kind)));
            var names = release.Kind == DependencyKind.Ffmpeg ? new[] { "ffmpeg.exe", "ffprobe.exe" } : [Executable(release.Kind)];
            var executableHashes = new Dictionary<string, string>();
            foreach (var name in names)
            {
                var executable = Path.Combine(prepared, name);
                ValidatePeX64(executable);
                var version = await ReadVersionAsync(release.Kind, executable, token);
                if (!VersionsMatch(version, release.Version)) throw new InvalidDataException("Downloaded version differs from the offered version.");
                await using var input = File.OpenRead(executable);
                executableHashes[name] = Convert.ToHexString(await SHA256.HashDataAsync(input, token));
            }
            await WriteJsonAsync(Path.Combine(prepared, "source.json"), release, token);
            await WriteJsonAsync(Path.Combine(prepared, "files.json"), executableHashes, token);
            var old = ReadManifest(release.Kind);
            var folder = release.Version + "-" + Guid.NewGuid().ToString("N");
            var final = Path.Combine(component, "versions", folder);
            Directory.CreateDirectory(Path.GetDirectoryName(final)!);
            token.ThrowIfCancellationRequested();
            Directory.Move(prepared, final);
            // If publication fails, the old manifest still points at the working version.
            await WriteJsonAsync(ManifestPath(release.Kind), new DependencyActivation(folder, old?.Active), token);
            return Path.Combine(final, Executable(release.Kind));
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            mutation.Release();
        }
    }

    public async Task<string> RollbackAsync(DependencyKind kind, CancellationToken cancellationToken)
    {
        await mutation.WaitAsync(cancellationToken);
        try
        {
            var old = ReadManifest(kind);
            if (old?.Previous is null) throw new InvalidOperationException("No previous managed version.");
            var folder = Path.Combine(ComponentRoot(kind), "versions", old.Previous);
            var names = kind == DependencyKind.Ffmpeg ? new[] { "ffmpeg.exe", "ffprobe.exe" } : [Executable(kind)];
            var versions = new List<string>();
            var hashes = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(Path.Combine(folder, "files.json"), cancellationToken))
                ?? throw new InvalidDataException("Missing component integrity record.");
            foreach (var name in names)
            {
                var executable = Path.Combine(folder, name);
                await using (var input = File.OpenRead(executable))
                {
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
                    if (!hashes.TryGetValue(name, out var expected) || hash != expected) throw new InvalidDataException("Previous component integrity check failed.");
                }
                ValidatePeX64(executable);
                versions.Add(await ReadVersionAsync(kind, executable, cancellationToken));
            }
            if (versions.Distinct().Count() != 1) throw new InvalidDataException("FFmpeg and FFprobe versions differ.");
            await WriteJsonAsync(ManifestPath(kind), new DependencyActivation(old.Previous, old.Active), cancellationToken);
            return Path.Combine(folder, Executable(kind));
        }
        finally { mutation.Release(); }
    }

    private static bool VersionsMatch(string a, string b) => a == b ||
        (Version.TryParse(a.TrimStart('v'), out var av) && Version.TryParse(b.TrimStart('v'), out var bv) &&
         av.Major == bv.Major && av.Minor == bv.Minor && Math.Max(av.Build, 0) == Math.Max(bv.Build, 0));

    private static async Task ExtractPairAsync(string package, string destination, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(package);
        foreach (var name in new[] { "ffmpeg.exe", "ffprobe.exe" })
        {
            var matches = archive.Entries.Where(e => e.FullName.Replace('\\', '/').EndsWith("/bin/" + name, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || matches[0].Length is <= 0 or > 300_000_000)
                throw new InvalidDataException("The archive must contain one complete FFmpeg/FFprobe pair.");
            await using var input = matches[0].Open();
            await using var output = File.Create(Path.Combine(destination, name));
            await CopyBoundedAsync(input, output, 300_000_000, token);
        }
    }

    public static void ValidatePeX64(string executable)
    {
        using var stream = File.OpenRead(executable);
        using var reader = new BinaryReader(stream);
        if (stream.Length < 64 || reader.ReadUInt16() != 0x5a4d) throw new InvalidDataException("Invalid Windows executable.");
        stream.Position = 0x3c;
        var offset = reader.ReadInt32();
        if (offset < 64 || offset > stream.Length - 6) throw new InvalidDataException("Invalid PE header.");
        stream.Position = offset;
        if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664) throw new InvalidDataException("A Windows x64 executable is required.");
    }

    private static void ValidateRelease(DependencyRelease release, DependencyKind expected)
    {
        if (release.Kind != expected || !Regex.IsMatch(release.Sha256, @"\A[0-9a-fA-F]{64}\z")) throw new InvalidDataException("Invalid release checksum.");
        ValidateFolder(release.Version);
        var uri = new Uri(release.PackageUrl);
        ValidateSource(uri);
        var valid = expected switch
        {
            DependencyKind.YtDlp => uri.Host == "github.com" && uri.AbsolutePath == $"/yt-dlp/yt-dlp/releases/download/{release.Version}/yt-dlp.exe",
            DependencyKind.Ffmpeg => uri.AbsoluteUri == "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
            DependencyKind.Node => uri.AbsoluteUri == $"https://nodejs.org/dist/{release.Version}/win-x64/node.exe" && Regex.IsMatch(release.Version, @"\Av22\.\d+\.\d+\z"),
            _ => false
        };
        if (!valid) throw new InvalidDataException("Unexpected component source.");
    }

    private static void ValidateSource(Uri uri)
    {
        if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
            uri.Host is not ("api.github.com" or "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com" or "www.gyan.dev" or "nodejs.org"))
            throw new InvalidDataException("Unexpected download origin.");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var uri = request.RequestUri!;
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            ValidateSource(uri);
            using var next = new HttpRequestMessage(HttpMethod.Get, uri);
            foreach (var header in request.Headers) next.Headers.TryAddWithoutValidation(header.Key, header.Value);
            var response = await client.SendAsync(next, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (location is null) throw new InvalidDataException("Invalid redirect.");
            uri = new Uri(uri, location);
        }
        throw new InvalidDataException("Too many redirects.");
    }

    private async Task<string> GetTextAsync(string url, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        return System.Text.Encoding.UTF8.GetString(await ReadBoundedAsync(response, 2_000_000, token));
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, int limit, CancellationToken token)
    {
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        await CopyBoundedAsync(input, output, limit, token);
        return output.ToArray();
    }

    private async Task DownloadAsync(string url, string destination, int limit, CancellationToken token,
        IProgress<DependencyDownloadProgress>? progress)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Component package too large.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var length = response.Content.Headers.ContentLength;
        var total = length is > 0 ? length : null;
        progress?.Report(new(0, total));
        await CopyBoundedAsync(input, output, limit, token, received => progress?.Report(new(received, total)));
    }

    private static async Task CopyBoundedAsync(Stream input, Stream output, long limit, CancellationToken token,
        Action<long>? received = null)
    {
        var buffer = new byte[81920];
        long total = 0;
        var reportedAt = System.Diagnostics.Stopwatch.StartNew();
        int count;
        while ((count = await input.ReadAsync(buffer, token)) != 0)
        {
            total += count;
            if (total > limit) throw new InvalidDataException("Component data too large.");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
            if (reportedAt.ElapsedMilliseconds >= 200)
            {
                received?.Invoke(total);
                reportedAt.Restart();
            }
        }
        received?.Invoke(total);
    }

    private static string FindHash(string sums, string name)
    {
        foreach (var line in sums.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), @"\A([0-9a-fA-F]{64})\s+\*?(.+)\z");
            if (match.Success && match.Groups[2].Value == name) return match.Groups[1].Value;
        }
        throw new InvalidDataException("Release checksum missing.");
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value), token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
