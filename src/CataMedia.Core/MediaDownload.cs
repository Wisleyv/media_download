namespace CataMedia.Core;

public sealed record MediaDownloadRequest(string Url, string DestinationFolder, int MaximumHeight)
{
    public MediaMode Mode { get; init; } = MediaMode.Video;
    public AudioFormat AudioFormat { get; init; } = AudioFormat.Mp3;
    public AudioQuality AudioQuality { get; init; } = AudioQuality.Standard;
    public string? CookiesBrowser { get; init; }
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
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(AudioFormat) || !Enum.IsDefined(AudioQuality) ||
            CookiesBrowser is not (null or "chrome" or "edge" or "firefox" or "brave"))
            throw new ArgumentException("Invalid media options.");
    }
}

public sealed record DownloadTools(string YtDlpPath, string FfmpegDirectory, string? NodePath = null);
public enum DownloadStage { Preparing, Downloading, Processing, Verifying }
public sealed record DownloadProgress(DownloadStage Stage, double? Percentage = null);
public sealed record MediaDownloadResult(string FilePath, int ActualHeight, double DurationSeconds)
{
    public MediaMode Mode { get; init; } = MediaMode.Video;
    public AudioFormat? AudioFormat { get; init; }
}

public interface IMediaDownloadService
{
    Task<MediaDownloadResult> DownloadAsync(MediaDownloadRequest request, DownloadTools tools,
        IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
