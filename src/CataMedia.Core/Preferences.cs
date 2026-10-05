using System.Text.Json.Serialization;

namespace CataMedia.Core;

public enum MediaMode { Video, Audio }
public enum AudioFormat { Mp3, Wav, Flac }
public enum AudioQuality { Low, Standard, High }

public sealed record Preferences
{
    [JsonRequired]
    public int SchemaVersion { get; init; } = 1;
    public string Language { get; init; } = "pt";
    public string DestinationFolder { get; init; } = "";
    public MediaMode Mode { get; init; } = MediaMode.Video;
    public int VideoHeight { get; init; } = 720;
    public AudioFormat AudioFormat { get; init; } = AudioFormat.Mp3;
    public AudioQuality AudioQuality { get; init; } = AudioQuality.Standard;
    public string? CookiesBrowser { get; init; }

    public void Validate()
    {
        if (SchemaVersion != 1 || Language is not ("pt" or "en") ||
            DestinationFolder is null || VideoHeight is not (720 or 1080) ||
            !Enum.IsDefined(Mode) || !Enum.IsDefined(AudioFormat) || !Enum.IsDefined(AudioQuality) ||
            CookiesBrowser is not (null or "chrome" or "edge" or "firefox" or "brave"))
            throw new InvalidDataException("Unsupported or invalid preferences.");
    }
}

public sealed record PreferencesLoadResult(Preferences Value, string? Error = null)
{
    public bool CanSave => Error is null;
}

public interface IPreferencesStore
{
    PreferencesLoadResult Load();
    void Save(Preferences preferences);
}
