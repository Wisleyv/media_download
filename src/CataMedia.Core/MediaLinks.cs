namespace CataMedia.Core;

public sealed record ResolvedMediaLink(string Url, string Title);
public sealed record MediaLinkResolution(string Title, IReadOnlyList<ResolvedMediaLink> Entries,
    bool IsPlaylist = false, ResolvedMediaLink? SingleVideo = null);
public enum PlaylistSelection { Cancel, SingleVideo, EntirePlaylist }

public interface IMediaLinkResolver
{
    Task<MediaLinkResolution> ResolveAsync(MediaDownloadRequest request, DownloadTools tools, CancellationToken cancellationToken);
}

public static class PlaylistPolicy
{
    public static IReadOnlyList<ResolvedMediaLink> Select(MediaLinkResolution resolution, PlaylistSelection selection) => selection switch
    {
        PlaylistSelection.EntirePlaylist => resolution.Entries,
        PlaylistSelection.SingleVideo when resolution.SingleVideo is not null => [resolution.SingleVideo],
        _ => []
    };
}
