using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;
using CataMedia.Core;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class RealVideoDownloadTests
{
    [RealDownloadFact]
    public async Task DownloadsAndValidatesUserProvidedVideo()
    {
        var output = Environment.GetEnvironmentVariable("CATAMEDIA_TEST_OUTPUT")
            ?? throw new InvalidOperationException("Set an isolated CATAMEDIA_TEST_OUTPUT directory.");
        Directory.CreateDirectory(output);
        var tools = new DownloadTools(Environment.GetEnvironmentVariable("CATAMEDIA_YTDLP")!,
            Environment.GetEnvironmentVariable("CATAMEDIA_FFMPEG")!, Environment.GetEnvironmentVariable("CATAMEDIA_NODE"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var stages = new ConcurrentQueue<DownloadProgress>();
        var result = await new YtDlpMediaDownloadService(new ExternalProcessRunner()).DownloadAsync(
            new(Environment.GetEnvironmentVariable("CATAMEDIA_REAL_TEST_URL")!, output, 720), tools,
            new ImmediateProgress(stages.Enqueue), timeout.Token);
        Assert.InRange(result.ActualHeight, 1, 720);
        Assert.True(result.DurationSeconds > 0);
        Assert.Empty(Directory.GetFiles(output, "*.log"));
        Assert.Empty(Directory.GetFiles(output, "*.txt"));
        Assert.Contains(stages, update => update.Stage == DownloadStage.Downloading && update.Percentage > 0);
        Assert.Contains(stages, update => update.Stage == DownloadStage.Processing);
        Assert.Contains(stages, update => update.Stage == DownloadStage.Verifying);
        File.WriteAllText(Path.Combine(output, "test-result.json"), JsonSerializer.Serialize(result));
    }

    private sealed class ImmediateProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
}

public sealed class RealDownloadFactAttribute : FactAttribute
{
    public RealDownloadFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CATAMEDIA_REAL_TEST_URL")))
            Skip = "Opt-in real download: requires an authorized link and external tools.";
    }
}
