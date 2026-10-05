using System.IO;
using System.Text.Json;
using CataMedia.Core;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class VideoDownloadTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CataMedia-download-" + Guid.NewGuid().ToString("N"));
    private readonly DownloadTools tools;
    private const string ValidMetadata = "{\"streams\":[{\"codec_type\":\"video\",\"height\":720},{\"codec_type\":\"audio\"}],\"format\":{\"format_name\":\"mov,mp4\",\"duration\":\"12.5\"}}";

    public VideoDownloadTests()
    {
        Directory.CreateDirectory(root);
        tools = new(Path.Combine(root, "yt-dlp.exe"), root, Path.Combine(root, "node.exe"));
        foreach (var name in new[] { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "node.exe" }) File.WriteAllText(Path.Combine(root, name), "fixture");
    }

    [Theory]
    [InlineData("--exec=bad")]
    [InlineData("file:///C:/test")]
    [InlineData("https://user:password@example.com/video")]
    [InlineData("https://example.com/video\nhttps://example.com/other")]
    public async Task InvalidLinksNeverStartAProcess(string url)
    {
        var runner = new FakeRunner((_, _, _) => throw new Exception("Must not start"));
        await Assert.ThrowsAsync<ArgumentException>(() => new YtDlpMediaDownloadService(runner).DownloadAsync(new(url, root, 720), tools, null, default));
    }

    [Theory]
    [InlineData(720)]
    [InlineData(1080)]
    public void ArgumentsKeepUrlAsDataAndBoundEveryFormatAlternative(int height)
    {
        var url = "https://example.com/video?name=a&other=--exec";
        var command = YtDlpMediaDownloadService.CreateCommand(new(url, root, height), tools, root);
        Assert.Equal("--", command.Arguments[^2]);
        Assert.Equal(url, command.Arguments[^1]);
        Assert.Contains("--ignore-config", command.Arguments);
        Assert.Contains("--no-plugin-dirs", command.Arguments);
        Assert.Contains("--no-playlist", command.Arguments);
        Assert.Contains("--playlist-items", command.Arguments);
        Assert.Contains("--no-overwrites", command.Arguments);
        Assert.Contains($"bv[height<={height}][ext=mp4]+ba[ext=m4a]/b[height<={height}][ext=mp4]", command.Arguments);
        Assert.DoesNotContain(command.Arguments, argument => argument.EndsWith("/best", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompletedDownloadRequiresFileAndValidVideoAudioMetadata()
    {
        var file = Path.Combine(root, "Vídeo com espaços.mp4");
        var stages = new List<DownloadProgress>();
        var runner = new FakeRunner((command, output, error) =>
        {
            if (command.Executable.EndsWith("ffprobe.exe", StringComparison.Ordinal)) output(ValidMetadata);
            else
            {
                error("CATAMEDIA_PROGRESS:{\"downloaded_bytes\":5,\"total_bytes\":null,\"total_bytes_estimate\":10}");
                output("CATAMEDIA_PROCESSING");
                File.WriteAllText(file, "simulated media");
                output("CATAMEDIA_RESULT:" + JsonSerializer.Serialize(file));
            }
            return 0;
        });
        var result = await new YtDlpMediaDownloadService(runner).DownloadAsync(new("https://example.com/video", root, 720), tools,
            new SynchronousProgress(stages.Add), default);
        Assert.Equal(file, result.FilePath);
        Assert.Equal(720, result.ActualHeight);
        Assert.Equal(12.5, result.DurationSeconds);
        Assert.Contains(stages, item => item.Percentage == 50);
        Assert.Contains(stages, item => item.Stage == DownloadStage.Processing);
        Assert.Contains(stages, item => item.Stage == DownloadStage.Verifying);
        Assert.Empty(Directory.GetFiles(root, "*.txt"));
        Assert.Empty(Directory.GetFiles(root, "*.log"));
        Assert.Empty(Directory.GetFiles(root, ".catamedia-write-*"));
    }

    [Fact]
    public async Task NonzeroExitNeverReportsSuccessEvenIfFileIsAnnounced()
    {
        var runner = new FakeRunner((_, output, _) => { output("CATAMEDIA_RESULT:\"file.mp4\""); return 1; });
        await Assert.ThrowsAsync<IOException>(() => new YtDlpMediaDownloadService(runner).DownloadAsync(new("https://example.com/video", root, 720), tools, null, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrOutOfDestinationFileIsRejected(bool outside)
    {
        var file = outside ? root + "-outside.mp4" : Path.Combine(root, "missing.mp4");
        if (outside) File.WriteAllText(file, "fixture");
        var runner = new FakeRunner((command, output, _) =>
        {
            output(Path.GetFileName(command.Executable) == "ffprobe.exe"
                ? ValidMetadata : "CATAMEDIA_RESULT:" + JsonSerializer.Serialize(file));
            return 0;
        });
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => new YtDlpMediaDownloadService(runner).DownloadAsync(new("https://example.com/video", root, 720), tools, null, default));
        }
        finally { if (outside) File.Delete(file); }
    }

    [Theory]
    [InlineData("{\"streams\":[{\"codec_type\":\"video\",\"height\":720}],\"format\":{\"format_name\":\"mp4\",\"duration\":\"12\"}}")]
    [InlineData("{\"streams\":[{\"codec_type\":\"video\",\"height\":1080},{\"codec_type\":\"audio\"}],\"format\":{\"format_name\":\"mp4\",\"duration\":\"12\"}}")]
    [InlineData("{}")]
    [InlineData("bad json")]
    public async Task InvalidOrUnexpectedMediaIsNotReportedAsSuccess(string metadata)
    {
        var file = Path.Combine(root, "video.mp4");
        File.WriteAllText(file, "fixture");
        var runner = new FakeRunner((command, output, _) =>
        {
            output(command.Executable.EndsWith("ffprobe.exe", StringComparison.Ordinal) ? metadata : "CATAMEDIA_RESULT:" + JsonSerializer.Serialize(file));
            return 0;
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => new YtDlpMediaDownloadService(runner).DownloadAsync(new("https://example.com/video", root, 720), tools, null, default));
    }

    [Fact]
    public async Task MissingDependencyDoesNotStartNetworkTransfer()
    {
        File.Delete(Path.Combine(root, "ffprobe.exe"));
        var runner = new FakeRunner((_, _, _) => throw new Exception("Must not start"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => new YtDlpMediaDownloadService(runner).DownloadAsync(new("https://example.com/video", root, 720), tools, null, default));
    }

    [Fact]
    public async Task CancellationNeverBecomesSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        var runner = new FakeRunner((_, _, _) => { cancellation.Cancel(); return 0; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new YtDlpMediaDownloadService(runner).DownloadAsync(new("https://example.com/video", root, 720), tools, null, cancellation.Token));
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    private sealed class FakeRunner(Func<ProcessCommand, Action<string>, Action<string>, int> run) : IProcessRunner
    {
        public Task<int> RunAsync(ProcessCommand command, Action<string> output, Action<string> error, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(run(command, output, error));
        }
    }

    private sealed class SynchronousProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
}
