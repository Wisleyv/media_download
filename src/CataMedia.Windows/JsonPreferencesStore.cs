using System.Text.Json;
using System.Text.Json.Serialization;
using CataMedia.Core;

namespace CataMedia.Windows;

public sealed class JsonPreferencesStore(string filePath) : IPreferencesStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PreferencesLoadResult Load()
    {
        try
        {
            if (!File.Exists(filePath)) return new(new Preferences());
            var preferences = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(filePath), Options)
                ?? throw new InvalidDataException("Empty preferences.");
            preferences.Validate();
            return new(preferences);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            // Never overwrite a corrupt or unsupported file with defaults.
            return new(new Preferences(), error.Message);
        }
    }

    public void Save(Preferences preferences)
    {
        preferences.Validate();
        if (!Load().CanSave) throw new InvalidDataException("Existing preferences need recovery before saving.");
        var fullPath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, preferences, Options);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, fullPath + ".bak");
            else File.Move(temporary, fullPath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
