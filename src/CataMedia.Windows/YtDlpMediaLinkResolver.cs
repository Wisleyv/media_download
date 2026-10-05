using System.Text;
using System.Text.Json;
using CataMedia.Core;

namespace CataMedia.Windows;

public sealed class YtDlpMediaLinkResolver(IProcessRunner runner) : IMediaLinkResolver
{
    public async Task<MediaLinkResolution> ResolveAsync(MediaDownloadRequest request, DownloadTools tools, CancellationToken cancellationToken)
    {
        request.Validate();
        if (!Path.IsPathFullyQualified(tools.YtDlpPath) || !File.Exists(tools.YtDlpPath))
            throw new FileNotFoundException("yt-dlp was not found.");
        var metadata = new StringBuilder();
        var errors = new Queue<string>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        List<string> args = ["--ignore-config", "--no-plugin-dirs", "--no-cache-dir", "--flat-playlist", "--dump-single-json",
            "--skip-download", "--yes-playlist", "--playlist-end", "501", "--encoding", "utf-8",
            "--socket-timeout", "15", "--retries", "1", "--no-js-runtimes", "--no-remote-components"];
        if (tools.NodePath is not null) args.AddRange(["--js-runtimes", "node:" + tools.NodePath]);
        if (request.CookiesBrowser is not null) args.AddRange(["--cookies-from-browser", request.CookiesBrowser]);
        args.AddRange(["--", request.Url]);
        try
        {
            var exit = await runner.RunAsync(new(tools.YtDlpPath, args, Path.GetDirectoryName(tools.YtDlpPath)!), line =>
            {
                if (metadata.Length + line.Length > 4_000_000) throw new InvalidDataException("Playlist metadata is too large.");
                metadata.AppendLine(line);
            }, line =>
            {
                if (errors.Count == 4) errors.Dequeue();
                errors.Enqueue(line.Length > 1000 ? line[..1000] : line);
            }, timeout.Token).ConfigureAwait(false);
            if (exit != 0) throw new IOException("Could not inspect the link. Check network, access and components.\n" + string.Join('\n', errors));
            return Parse(metadata.ToString(), request);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException("Link inspection timed out.");
        }
    }

    public static MediaLinkResolution Parse(string json, MediaDownloadRequest request)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var title = Text(root, "title") ?? request.Url;
            if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                if (Text(root, "_type") is "playlist" or "multi_video")
                    throw new InvalidDataException("Playlist entries could not be confirmed.");
                return new(title, [new(request.Url, title)]);
            }
            if (entries.GetArrayLength() > 500 || root.TryGetProperty("playlist_count", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var total) && total > 500)
                throw new InvalidDataException("Playlists are limited to 500 entries in this version.");
            List<ResolvedMediaLink> links = [];
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) throw new InvalidDataException("An unavailable playlist entry could not be inspected.");
                var url = Text(entry, "webpage_url") ?? Text(entry, "url");
                if (url is null) throw new InvalidDataException("A playlist entry has no valid URL.");
                (request with { Url = url }).Validate();
                links.Add(new(url, Text(entry, "title") ?? url));
            }
            if (links.Count == 0) throw new InvalidDataException("The playlist is empty or unavailable.");
            var uri = new Uri(request.Url);
            var youtube = uri.Host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtu.be";
            var hasVideo = uri.Host == "youtu.be" ? uri.AbsolutePath.Length > 1 : uri.AbsolutePath == "/watch" && uri.Query.Split('&').Any(part => part.TrimStart('?').StartsWith("v=", StringComparison.Ordinal));
            return new(title, links, true, youtube && hasVideo ? new(request.Url, title) : null);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException("Invalid link metadata.", error);
        }
    }

    private static string? Text(JsonElement value, string key) => value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
}
