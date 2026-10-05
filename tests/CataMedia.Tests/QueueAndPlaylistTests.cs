using System.IO;
using CataMedia.Core;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class QueueAndPlaylistTests
{
    private static QueueItem Item(string name) => new(Guid.NewGuid(), name, new("https://example.com/" + name, "destination", 720));

    [Fact]
    public async Task QueueContinuesAfterFailureAndRetryRunsOnlyFailedItems()
    {
        var items = new[] { Item("first"), Item("second"), Item("third") };
        var calls = new List<string>();
        var failSecond = true;
        var active = 0;
        var service = new StubService(async (request, token) =>
        {
            Assert.Equal(1, Interlocked.Increment(ref active));
            try
            {
                calls.Add(request.Url);
                await Task.Delay(5, token);
                if (request.Url.EndsWith("second", StringComparison.Ordinal) && failSecond) throw new IOException("fixture failure");
                return new("file.mp4", 720, 10);
            }
            finally { Interlocked.Decrement(ref active); }
        });
        var queue = new DownloadQueue(service);
        var result = await queue.RunAsync(items, new("yt-dlp", "ffmpeg"), null, default);
        Assert.Equal(new[] { QueueItemState.Completed, QueueItemState.Failed, QueueItemState.Completed }, result.Results.Select(row => row.State));
        Assert.Equal(items.Select(row => row.Request.Url), calls);
        var failedIds = result.Results.Where(row => row.State == QueueItemState.Failed).Select(row => row.Id).ToHashSet();
        failSecond = false;
        var retry = await queue.RunAsync(items.Where(item => failedIds.Contains(item.Id)).ToArray(), new("yt-dlp", "ffmpeg"), null, default);
        Assert.Single(retry.Results);
        Assert.Equal(QueueItemState.Completed, retry.Results[0].State);
        Assert.Equal(items[1].Request.Url, calls[^1]);
        Assert.Equal(4, calls.Count);
    }

    [Fact]
    public async Task CancellationStopsTheActiveItemAndDoesNotStartTheNext()
    {
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        var service = new StubService((_, token) =>
        {
            calls++;
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new MediaDownloadResult("file", 720, 10));
        });
        var result = await new DownloadQueue(service).RunAsync([Item("first"), Item("second")], new("yt", "ff"), null, cancellation.Token);
        Assert.True(result.Cancelled);
        Assert.Single(result.Results);
        Assert.Equal(QueueItemState.Cancelled, result.Results[0].State);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void PlaylistRequiresExplicitSelectionAndSingleVideoRequiresContext()
    {
        var request = new MediaDownloadRequest("https://www.youtube.com/watch?v=video&list=list", "folder", 720);
        const string metadata = "{\"title\":\"List\",\"playlist_count\":null,\"entries\":[{\"title\":\"First\",\"url\":\"https://example.com/first\"},{\"title\":\"Second\",\"url\":\"https://example.com/second\"}]}";
        var resolution = YtDlpMediaLinkResolver.Parse(metadata, request);
        Assert.True(resolution.IsPlaylist);
        Assert.Equal(2, PlaylistPolicy.Select(resolution, PlaylistSelection.EntirePlaylist).Count);
        Assert.Single(PlaylistPolicy.Select(resolution, PlaylistSelection.SingleVideo));
        Assert.Empty(PlaylistPolicy.Select(resolution, PlaylistSelection.Cancel));
        Assert.Null(YtDlpMediaLinkResolver.Parse(metadata, request with { Url = "https://www.youtube.com/playlist?list=list" }).SingleVideo);
        Assert.Empty(PlaylistPolicy.Select(resolution with { SingleVideo = null }, PlaylistSelection.SingleVideo));
    }

    [Theory]
    [InlineData("{\"entries\":[]}")]
    [InlineData("{\"_type\":\"playlist\",\"entries\":null}")]
    [InlineData("{\"entries\":[null]}")]
    [InlineData("{\"entries\":[{\"url\":\"file:///test\"}]}")]
    [InlineData("{\"playlist_count\":501,\"entries\":[]}")]
    public void InvalidOrOversizedPlaylistIsRejected(string metadata)
    {
        Assert.Throws<InvalidDataException>(() => YtDlpMediaLinkResolver.Parse(metadata, new("https://example.com/list", "folder", 720)));
    }

    private sealed class StubService(Func<MediaDownloadRequest, CancellationToken, Task<MediaDownloadResult>> download) : IMediaDownloadService
    {
        public Task<MediaDownloadResult> DownloadAsync(MediaDownloadRequest request, DownloadTools tools, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken) => download(request, cancellationToken);
    }
}
