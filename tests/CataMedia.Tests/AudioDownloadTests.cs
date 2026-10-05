using System.IO;
using System.Text.Json;
using CataMedia.Core;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class AudioDownloadTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CataMedia-audio-" + Guid.NewGuid().ToString("N"));
    public AudioDownloadTests()
    {
        Directory.CreateDirectory(root);
        foreach (var name in new[] { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe" }) File.WriteAllText(Path.Combine(root, name), "fixture");
    }

    [Theory]
    [InlineData(AudioFormat.Mp3, "mp3")]
    [InlineData(AudioFormat.Wav, "pcm_s16le")]
    [InlineData(AudioFormat.Flac, "flac")]
    public async Task AudioOutputsAreVerifiedByContainerCodecAndAbsenceOfVideo(AudioFormat format, string codec)
    {
        var request = new MediaDownloadRequest("https://example.com/audio", root, 720) { Mode = MediaMode.Audio, AudioFormat = format };
        var tools = new DownloadTools(Path.Combine(root, "yt-dlp.exe"), root);
        var file = Path.Combine(root, "audio." + format.ToString().ToLowerInvariant());
        File.WriteAllText(file, "fixture");
        var runner = new AudioRunner(file, format.ToString().ToLowerInvariant(), codec);
        var result = await new YtDlpMediaDownloadService(runner).DownloadAsync(request, tools, null, default);
        Assert.Equal(MediaMode.Audio, result.Mode);
        Assert.Equal(format, result.AudioFormat);
        Assert.Empty(Directory.GetFiles(root, "*.log"));
    }

    [Theory]
    [InlineData(AudioQuality.Low, "7")]
    [InlineData(AudioQuality.Standard, "5")]
    [InlineData(AudioQuality.High, "0")]
    public void Mp3QualityMatchesTheExistingApplication(AudioQuality quality, string value)
    {
        var request = new MediaDownloadRequest("https://example.com/audio", root, 720) { Mode = MediaMode.Audio, AudioQuality = quality };
        var args = YtDlpMediaDownloadService.CreateCommand(request, new("yt", "ff"), root).Arguments.ToList();
        Assert.Equal(value, args[args.IndexOf("--audio-quality") + 1]);
        Assert.Contains("--extract-audio", args.Select(arg => arg == "-x" ? "--extract-audio" : arg));
        Assert.Contains(quality.ToString().ToLowerInvariant(), args[args.IndexOf("-o") + 1]);
        var wav = YtDlpMediaDownloadService.CreateCommand(request with { AudioFormat = AudioFormat.Wav }, new("yt", "ff"), root);
        Assert.DoesNotContain("--audio-quality", wav.Arguments);
    }

    [Fact]
    public async Task WrongAudioCodecIsRejected()
    {
        var file = Path.Combine(root, "audio.mp3");
        File.WriteAllText(file, "fixture");
        await Assert.ThrowsAsync<InvalidDataException>(() => new YtDlpMediaDownloadService(new AudioRunner(file, "mp3", "aac"))
            .DownloadAsync(new("https://example.com/audio", root, 720) { Mode = MediaMode.Audio }, new(Path.Combine(root, "yt-dlp.exe"), root), null, default));
    }

    [Fact]
    public void BrowserCookiesAreUsedOnlyWhenExplicitlySelected()
    {
        var request = new MediaDownloadRequest("https://example.com/audio", root, 720);
        Assert.DoesNotContain("--cookies-from-browser", YtDlpMediaDownloadService.CreateCommand(request, new("yt", "ff"), root).Arguments);
        Assert.Contains("firefox", YtDlpMediaDownloadService.CreateCommand(request with { CookiesBrowser = "firefox" }, new("yt", "ff"), root).Arguments);
        Assert.Throws<ArgumentException>(() => (request with { CookiesBrowser = "--exec" }).Validate());
    }

    private sealed class AudioRunner(string file, string format, string codec) : IProcessRunner
    {
        public Task<int> RunAsync(ProcessCommand command, Action<string> output, Action<string> error, CancellationToken token)
        {
            if (command.Executable.EndsWith("ffprobe.exe", StringComparison.Ordinal))
                output(JsonSerializer.Serialize(new { streams = new[] { new { codec_type = "audio", codec_name = codec } }, format = new { format_name = format, duration = "10" } }));
            else output("CATAMEDIA_RESULT:" + JsonSerializer.Serialize(file));
            return Task.FromResult(0);
        }
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
