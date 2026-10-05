using System.Net;
using System.Net.Http;
using System.Text.Json;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class AppReleaseTests
{
    [Theory]
    [InlineData("3.0.0-stage5+commit", "v3.0.0", true)]
    [InlineData("3.0.0", "v3.0.0", false)]
    [InlineData("3.1.0", "v3.0.9", false)]
    [InlineData("3.0.0", "v3.0.10", true)]
    [InlineData("3.1.0-preview.1", "v3.1.0", true)]
    public async Task ComparesStableNumericVersionsAndInstalledPreviews(string installed, string published, bool newer)
    {
        using var handler = new Handler(_ => Response(Release(published)));
        using var client = new HttpClient(handler);
        var result = await new AppReleaseChecker(client, installed).CheckAsync(false, default);
        Assert.Equal(newer, result.UpdateAvailable);
        Assert.Equal(published.TrimStart('v'), result.Latest?.Version);
    }

    [Fact]
    public async Task IgnoresPowerShellDraftPreviewOtherMajorAndIncompleteWindowsAssets()
    {
        using var handler = new Handler(_ => Response(
            Release("v2.0.1"), Release("v4.0.0"), Release("v3.9.0", draft: true),
            Release("v3.8.0", preview: true), Release("v3.7.0-preview.1"), Release("v3.6.0", complete: false)));
        using var client = new HttpClient(handler);
        var result = await new AppReleaseChecker(client, "3.0.0-stage5").CheckAsync(false, default);
        Assert.Null(result.Latest); Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task FindsHighestCompatibleVersionAcrossPagesAndCachesUntilForced()
    {
        using var handler = new Handler(request =>
        {
            if (request.RequestUri!.Query.EndsWith("&page=1", StringComparison.Ordinal))
            {
                var first = Response(Release("v2.0.1"), Release("v3.0.9"));
                first.Headers.Add("Link", "<https://api.github.com/repos/Wisleyv/media_download/releases?per_page=100&page=2>; rel=\"next\"");
                return first;
            }
            return Response(Release("v3.0.10"), Release("v3.0.2"));
        });
        using var client = new HttpClient(handler);
        var checker = new AppReleaseChecker(client, "3.0.0");
        var result = await checker.CheckAsync(false, default);
        Assert.Equal("3.0.10", result.Latest?.Version); Assert.True(result.UpdateAvailable);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(result, await checker.CheckAsync(false, default)); Assert.Equal(2, handler.Calls);
        await checker.CheckAsync(true, default); Assert.Equal(4, handler.Calls);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("api-limit")]
    [InlineData("json")]
    [InlineData("size")]
    [InlineData("url")]
    public async Task FailedChecksNeverReplaceSuccessfulCachedResult(string failure)
    {
        var fail = false;
        using var handler = new Handler(_ =>
        {
            if (!fail) return Response(Release("v3.1.0"));
            return failure switch
            {
                "network" => throw new HttpRequestException("Offline fixture"),
                "api-limit" => new(HttpStatusCode.Forbidden),
                "json" => new(HttpStatusCode.OK) { Content = new StringContent("broken JSON") },
                "size" => new(HttpStatusCode.OK) { Content = new StringContent(new string(' ', 2_000_001)) },
                _ => Response(Release("v3.1.0", url: "https://example.com/untrusted"))
            };
        });
        using var client = new HttpClient(handler);
        var checker = new AppReleaseChecker(client, "3.0.0");
        var good = await checker.CheckAsync(false, default);
        fail = true;
        await Assert.ThrowsAnyAsync<Exception>(() => checker.CheckAsync(true, default));
        Assert.Equal(good, await checker.CheckAsync(false, default));
    }

    [Fact]
    public async Task CancellationInterruptsPendingNetworkCheck()
    {
        using var handler = new PendingHandler();
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var task = new AppReleaseChecker(client, "3.0.0").CheckAsync(false, cancellation.Token);
        Assert.False(task.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    private sealed class PendingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new(HttpStatusCode.OK); }
    }

    internal static object Release(string tag, bool draft = false, bool preview = false, bool complete = true, string? url = null)
    {
        var version = tag.TrimStart('v');
        return new { tag_name = tag, draft, prerelease = preview,
            html_url = url ?? $"https://github.com/Wisleyv/media_download/releases/tag/{Uri.EscapeDataString(tag)}",
            assets = (complete ? new[] { $"CataMedia-{version}-win-x64-portable.zip", $"CataMedia-Setup-{version}-win-x64.exe", "SHA256SUMS.txt" }
                : new[] { $"CataMedia-{version}-win-x64-portable.zip" }).Select(name => new { name }).ToArray() };
    }

    internal static HttpResponseMessage Response(params object[] releases) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(releases)) };

    internal sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; return Task.FromResult(respond(request)); }
    }
}
