using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class DependencyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CataMedia-components-ç " + Guid.NewGuid().ToString("N"));
    private readonly Handler handler = new();
    private readonly Runner runner = new();
    private readonly HttpClient client;
    private readonly DependencyManager manager;
    public DependencyTests()
    {
        Directory.CreateDirectory(root);
        client = new(handler);
        manager = new(root, client, runner);
    }

    [Fact]
    public async Task ActivationAndRollbackKeepVersionsAndRejectModifiedPrevious()
    {
        var first = await InstallYtAsync("2026.08.19");
        var second = await InstallYtAsync("2026.09.01");
        Assert.Equal(second, manager.ActiveExecutable(DependencyKind.YtDlp));
        Assert.True(File.Exists(first));
        Assert.Equal(first, await manager.RollbackAsync(DependencyKind.YtDlp, default));
        Assert.Equal(second, await manager.RollbackAsync(DependencyKind.YtDlp, default));
        File.AppendAllText(first, "changed");
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.RollbackAsync(DependencyKind.YtDlp, default));
        Assert.Equal(second, manager.ActiveExecutable(DependencyKind.YtDlp));
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("architecture")]
    [InlineData("version")]
    [InlineData("network")]
    [InlineData("disk-full")]
    [InlineData("cancel")]
    public async Task FailedPreparationNeverReplacesActiveVersion(string failure)
    {
        var working = await InstallYtAsync("2026.08.19");
        var bytes = Pe(failure == "architecture" ? (ushort)0x14c : (ushort)0x8664);
        var release = Release(DependencyKind.YtDlp, "2026.09.01", bytes);
        handler.Bytes = bytes;
        if (failure == "hash") release = release with { Sha256 = new string('0', 64) };
        runner.Version = failure == "version" ? "2026.08.19" : "2026.09.01";
        if (failure == "network") handler.Failure = new HttpRequestException("Interrupted transfer");
        if (failure == "disk-full") handler.Failure = new IOException("There is not enough space on the disk");
        using var cancellation = new CancellationTokenSource();
        if (failure == "cancel") cancellation.Cancel();
        await Assert.ThrowsAnyAsync<Exception>(() => manager.InstallAsync(release, null, cancellation.Token));
        Assert.Equal(working, manager.ActiveExecutable(DependencyKind.YtDlp));
        Assert.True(File.Exists(working));
        var staging = Path.Combine(root, "YtDlp", "staging");
        Assert.Empty(Directory.GetDirectories(staging));
    }

    [Fact]
    public async Task InterruptedBodyRemovesPartialPackageAndKeepsWorkingExecutable()
    {
        var working = await InstallYtAsync("2026.08.19");
        handler.Respond = _ => new(HttpStatusCode.OK) { Content = new StreamContent(new InterruptedStream()) };
        await Assert.ThrowsAsync<IOException>(() => manager.InstallAsync(Release(DependencyKind.YtDlp, "2026.09.01", Pe()), null, default));
        Assert.Equal(working, manager.ActiveExecutable(DependencyKind.YtDlp));
        Assert.Empty(Directory.GetDirectories(Path.Combine(root, "YtDlp", "staging")));
    }

    private sealed class InterruptedStream : MemoryStream
    {
        private bool started;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (started) throw new IOException("Fixture network disconnected after partial bytes.");
            started = true; buffer.Span[0] = 0x4d;
            return ValueTask.FromResult(1);
        }
    }

    [Fact]
    public async Task ManifestPublicationFailureKeepsOldManifest()
    {
        var working = await InstallYtAsync("2026.08.19");
        var manifest = Path.Combine(root, "YtDlp", "active.json");
        var original = File.ReadAllText(manifest);
        using (var locked = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() => InstallYtAsync("2026.09.01"));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(original, File.ReadAllText(manifest));
        Assert.Equal(working, manager.ActiveExecutable(DependencyKind.YtDlp));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FfmpegPairActivatesOnlyWhenBothVersionsMatch(bool includeProbe, bool matching)
    {
        handler.Bytes = Zip(includeProbe);
        runner.Version = "9.0.2";
        runner.ProbeVersion = matching ? "9.0.2" : "9.0.1";
        var release = Release(DependencyKind.Ffmpeg, "9.0.2", handler.Bytes);
        if (!includeProbe || !matching)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => manager.InstallAsync(release, null, default));
            Assert.Null(manager.ActiveExecutable(DependencyKind.Ffmpeg));
        }
        else
        {
            var active = await manager.InstallAsync(release, null, default);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(active)!, "ffprobe.exe")));
            // An unrelated/traversal ZIP entry is never extracted.
            Assert.False(File.Exists(Path.Combine(root, "outside.txt")));
        }
    }

    [Fact]
    public async Task CacheSkipsRepeatedStartupQueriesAndForceUsesEtag()
    {
        var bytes = Pe();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        handler.Respond = request =>
        {
            if (request.RequestUri!.Host == "api.github.com")
            {
                if (request.Headers.Contains("If-None-Match")) return new(HttpStatusCode.NotModified);
                var response = Text(JsonSerializer.Serialize(new
                {
                    tag_name = "2026.08.19", prerelease = false, draft = false,
                    assets = new[] {
                        new { name = "yt-dlp.exe", browser_download_url = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/yt-dlp.exe" },
                        new { name = "SHA2-256SUMS", browser_download_url = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/SHA2-256SUMS" }
                    }
                }));
                response.Headers.ETag = new("\"fixture\"");
                return response;
            }
            return Text(hash + "  yt-dlp.exe\n");
        };
        var first = await manager.CheckAsync(DependencyKind.YtDlp, false, default);
        var count = handler.Calls;
        Assert.Equal(first, await manager.CheckAsync(DependencyKind.YtDlp, false, default));
        Assert.Equal(count, handler.Calls);
        Assert.Equal(first, await manager.CheckAsync(DependencyKind.YtDlp, true, default));
        Assert.Equal(count + 1, handler.Calls);
    }

    [Fact]
    public async Task ProviderMetadataSelectsNodeAndFfmpegChecksums()
    {
        var hash = new string('a', 64);
        handler.Respond = request => Text(request.RequestUri!.AbsolutePath switch
        {
            "/dist/latest-v22.x/" => "<a href=\"node-v22.23.3-win-x64.zip\">ZIP</a>",
            "/dist/v22.23.3/SHASUMS256.txt" => hash + "  win-x64/node.exe\n",
            "/ffmpeg/builds/release-version" => "9.0.2\n",
            _ => hash + "\n"
        });
        var node = await manager.CheckAsync(DependencyKind.Node, true, default);
        Assert.Equal("https://nodejs.org/dist/v22.23.3/win-x64/node.exe", node.PackageUrl);
        Assert.Equal(hash, node.Sha256);
        var ffmpeg = await manager.CheckAsync(DependencyKind.Ffmpeg, true, default);
        Assert.Equal("9.0.2", ffmpeg.Version);
        Assert.Equal(hash, ffmpeg.Sha256);
    }

    [Theory]
    [InlineData("https://evil.example/yt-dlp.exe")]
    [InlineData("https://github.com/other/repo/releases/download/2026.08.19/yt-dlp.exe")]
    [InlineData("http://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/yt-dlp.exe")]
    public async Task UnapprovedOriginIsRejectedBeforeNetworkOrExecution(string url)
    {
        var release = Release(DependencyKind.YtDlp, "2026.08.19", Pe()) with { PackageUrl = url };
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.InstallAsync(release, null, default));
        Assert.Equal(0, handler.Calls);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task RedirectToUnapprovedHostIsRejected()
    {
        handler.Respond = _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new("https://evil.example/package");
            return response;
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.InstallAsync(Release(DependencyKind.YtDlp, "2026.08.19", Pe()), null, default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void VersionsNeverRecommendDowngradeOrReplacingNightlyWithOlderStable()
    {
        Assert.False(DependencyManager.IsNewer(DependencyKind.YtDlp, "2026.08.19", "2026.09.01.123456"));
        Assert.False(DependencyManager.IsNewer(DependencyKind.YtDlp, "2026.08.19", "2026.08.19"));
        Assert.True(DependencyManager.IsNewer(DependencyKind.YtDlp, "2026.09.01", "2026.08.19"));
        Assert.False(DependencyManager.IsNewer(DependencyKind.Node, "v22.23.3", "v24.0.0"));
        Assert.False(DependencyManager.IsNewer(DependencyKind.Ffmpeg, "9.0.2", "10.0.0"));
    }

    [Fact]
    public async Task UnsupportedJavaScriptRuntimeFailsVersionValidation()
    {
        runner.Version = "v20.0.0";
        var executable = Path.Combine(root, "node.exe");
        File.WriteAllBytes(executable, Pe());
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.ReadVersionAsync(DependencyKind.Node, executable, default));
    }

    [Fact]
    public void ManifestTraversalCannotSelectOutsideComponentDirectory()
    {
        Directory.CreateDirectory(Path.Combine(root, "YtDlp"));
        File.WriteAllText(Path.Combine(root, "YtDlp", "active.json"), "{\"Active\":\"../outside\",\"Previous\":null}");
        Assert.Throws<InvalidDataException>(() => manager.ActiveExecutable(DependencyKind.YtDlp));
    }

    private async Task<string> InstallYtAsync(string version)
    {
        handler.Bytes = Pe(); runner.Version = version;
        return await manager.InstallAsync(Release(DependencyKind.YtDlp, version, handler.Bytes), null, default);
    }
    private static DependencyRelease Release(DependencyKind kind, string version, byte[] bytes) => new(kind, version,
        kind switch {
            DependencyKind.YtDlp => $"https://github.com/yt-dlp/yt-dlp/releases/download/{version}/yt-dlp.exe",
            DependencyKind.Ffmpeg => "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
            _ => $"https://nodejs.org/dist/{version}/win-x64/node.exe"
        }, Convert.ToHexString(SHA256.HashData(bytes)), "fixture source");

    private static byte[] Pe(ushort machine = 0x8664)
    {
        var bytes = new byte[256];
        bytes[0] = 0x4d; bytes[1] = 0x5a;
        BitConverter.GetBytes(64).CopyTo(bytes, 0x3c);
        BitConverter.GetBytes(0x4550).CopyTo(bytes, 64);
        BitConverter.GetBytes(machine).CopyTo(bytes, 68);
        return bytes;
    }
    private static byte[] Zip(bool probe)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var names = probe ? new[] { "ffmpeg.exe", "ffprobe.exe" } : ["ffmpeg.exe"];
            foreach (var name in names)
            {
                using var entry = archive.CreateEntry("ffmpeg-9.0.2-essentials_build/bin/" + name).Open();
                entry.Write(Pe());
            }
            using var extra = archive.CreateEntry("../../outside.txt").Open();
            extra.Write(Encoding.UTF8.GetBytes("must never be extracted"));
        }
        return stream.ToArray();
    }
    private static HttpResponseMessage Text(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };
    private sealed class Handler : HttpMessageHandler
    {
        public byte[] Bytes = [];
        public Exception? Failure;
        public int Calls;
        public Func<HttpRequestMessage, HttpResponseMessage>? Respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            return Task.FromResult(Respond?.Invoke(request) ?? new(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes) });
        }
    }
    private sealed class Runner : IProcessRunner
    {
        public string Version = "2026.08.19";
        public string? ProbeVersion;
        public int Calls;
        public Task<int> RunAsync(ProcessCommand command, Action<string> standardOutput, Action<string> standardError, CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(command.Executable);
            standardOutput(name is "ffmpeg" or "ffprobe" ? name + " version " + (name == "ffprobe" ? ProbeVersion ?? Version : Version) : Version);
            return Task.FromResult(0);
        }
    }
    public void Dispose() { client.Dispose(); Directory.Delete(root, true); }
}
