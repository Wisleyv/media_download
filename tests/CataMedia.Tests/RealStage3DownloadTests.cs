using System.IO;
using System.Text.Json;
using CataMedia.Core;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class RealStage3DownloadTests
{
    private static DownloadTools Tools => new(Environment.GetEnvironmentVariable("CATAMEDIA_YTDLP")!,
        Environment.GetEnvironmentVariable("CATAMEDIA_FFMPEG")!, Environment.GetEnvironmentVariable("CATAMEDIA_NODE"));
    private static string Output(string name)
    {
        var root = Environment.GetEnvironmentVariable("CATAMEDIA_TEST_OUTPUT") ?? throw new InvalidOperationException("Set an isolated output directory.");
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        return directory;
    }

    [RealDownloadTheory]
    [InlineData(AudioFormat.Mp3)]
    [InlineData(AudioFormat.Wav)]
    [InlineData(AudioFormat.Flac)]
    public async Task DownloadsAndVerifiesEachAudioFormat(AudioFormat format)
    {
        var destination = Output(format.ToString());
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var request = new MediaDownloadRequest(Environment.GetEnvironmentVariable("CATAMEDIA_REAL_TEST_URL")!, destination, 720)
            { Mode = MediaMode.Audio, AudioFormat = format };
        var result = await new YtDlpMediaDownloadService(new ExternalProcessRunner()).DownloadAsync(request, Tools, null, timeout.Token);
        Assert.Equal(MediaMode.Audio, result.Mode);
        Assert.Equal(format, result.AudioFormat);
        Assert.True(result.DurationSeconds > 0);
        Assert.Empty(Directory.GetFiles(destination, "*.log"));
        Assert.Empty(Directory.GetFiles(destination, "*.txt"));
        File.WriteAllText(Path.Combine(destination, "test-result.json"), JsonSerializer.Serialize(result));
    }

    [RealQueueFact]
    public async Task RunsSequentialQueueWithBothProvidedLinks()
    {
        var destination = Output("Queue");
        var urls = new[] { Environment.GetEnvironmentVariable("CATAMEDIA_REAL_TEST_URL")!, Environment.GetEnvironmentVariable("CATAMEDIA_SECOND_TEST_URL")! };
        var runner = new ExternalProcessRunner();
        var resolver = new YtDlpMediaLinkResolver(runner);
        List<QueueItem> items = [];
        var privateSecond = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        foreach (var url in urls)
        {
            var request = new MediaDownloadRequest(url, destination, 720);
            try
            {
                var resolution = await resolver.ResolveAsync(request, Tools, timeout.Token);
                Assert.False(resolution.IsPlaylist);
                Assert.Single(resolution.Entries);
                items.Add(new(Guid.NewGuid(), resolution.Title, request));
            }
            catch (IOException error) when (url == urls[1] && error.Message.Contains("Private video", StringComparison.Ordinal))
            {
                privateSecond = true;
                items.Add(new(Guid.NewGuid(), "Private video fixture", request));
            }
        }
        var results = await new DownloadQueue(new YtDlpMediaDownloadService(runner)).RunAsync(items, Tools, null, timeout.Token);
        Assert.False(results.Cancelled);
        Assert.Equal(2, results.Results.Count);
        Assert.Equal(QueueItemState.Completed, results.Results[0].State);
        Assert.Equal(privateSecond ? QueueItemState.Failed : QueueItemState.Completed, results.Results[1].State);
        if (privateSecond) Assert.Contains("Private video", results.Results[1].Error);
        Assert.Equal(items.Select(item => item.Id), results.Results.Select(result => result.Id));
        Assert.Equal(privateSecond ? 1 : 2, Directory.GetFiles(destination, "*.mp4").Length);
        Assert.Empty(Directory.GetFiles(destination, "*.log"));
        Assert.Empty(Directory.GetFiles(destination, "*.txt"));
        File.WriteAllText(Path.Combine(destination, "test-result.json"), JsonSerializer.Serialize(results));
    }
}

public sealed class RealDownloadTheoryAttribute : TheoryAttribute
{
    public RealDownloadTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CATAMEDIA_REAL_TEST_URL")))
            Skip = "Opt-in audio downloads require an authorized link and external tools.";
    }
}

public sealed class RealQueueFactAttribute : FactAttribute
{
    public RealQueueFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CATAMEDIA_REAL_TEST_URL")) ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CATAMEDIA_SECOND_TEST_URL")))
            Skip = "Opt-in queue download requires two authorized links and external tools.";
    }
}
