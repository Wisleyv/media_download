namespace CataMedia.Core;

public sealed record VideoDownloadRequest(string Url, string DestinationFolder, int MaximumHeight)
{
    public void Validate()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) || Url.Any(char.IsWhiteSpace))
            throw new ArgumentException("Informe um link HTTP/HTTPS válido. / Enter a valid HTTP/HTTPS link.");
        if (MaximumHeight is not (720 or 1080))
            throw new ArgumentException("Escolha 720p ou 1080p. / Choose 720p or 1080p.");
        if (string.IsNullOrWhiteSpace(DestinationFolder))
            throw new ArgumentException("Escolha uma pasta de destino. / Choose a destination folder.");
    }
}

public sealed record DownloadTools(string YtDlpPath, string FfmpegDirectory, string? NodePath = null);
public enum DownloadStage { Preparing, Downloading, Processing, Verifying }
public sealed record DownloadProgress(DownloadStage Stage, double? Percentage = null);
public sealed record VideoDownloadResult(string FilePath, int ActualHeight, double DurationSeconds);

public interface IVideoDownloadService
{
    Task<VideoDownloadResult> DownloadAsync(VideoDownloadRequest request, DownloadTools tools,
        IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
