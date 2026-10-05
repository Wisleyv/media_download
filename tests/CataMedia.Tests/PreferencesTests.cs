using System.IO;
using CataMedia.Core;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class PreferencesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CataMedia-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(root, "data", "preferences.json");

    [Fact]
    public void MissingPreferencesDoNotCreateAnyFiles()
    {
        var result = new JsonPreferencesStore(FilePath).Load();
        Assert.True(result.CanSave);
        Assert.Equal(new Preferences(), result.Value);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void SaveRoundTripsAllChoicesAndKeepsPreviousVersion()
    {
        var store = new JsonPreferencesStore(FilePath);
        var original = new Preferences { DestinationFolder = @"C:\Vídeos com espaços" };
        var updated = new Preferences
        {
            DestinationFolder = @"D:\Áudio", Language = "en", Mode = MediaMode.Audio,
            VideoHeight = 1080, AudioFormat = AudioFormat.Flac, AudioQuality = AudioQuality.High
        };
        store.Save(original);
        store.Save(updated);
        Assert.Equal(updated, store.Load().Value);
        Assert.Equal(original, new JsonPreferencesStore(FilePath + ".bak").Load().Value);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(FilePath)!, "*.tmp"));
    }

    [Theory]
    [InlineData("broken json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"SchemaVersion\":99}")]
    [InlineData("{\"VideoHeight\":480}")]
    [InlineData("{\"Mode\":99}")]
    [InlineData("{\"Language\":\"unknown\"}")]
    public void InvalidPreferencesArePreservedAndCannotBeOverwritten(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, contents);
        var store = new JsonPreferencesStore(FilePath);
        Assert.False(store.Load().CanSave);
        Assert.Throws<InvalidDataException>(() => store.Save(new Preferences()));
        Assert.Equal(contents, File.ReadAllText(FilePath));
    }

    [Fact]
    public void InvalidNewValuesDoNotReplaceValidPreferences()
    {
        var store = new JsonPreferencesStore(FilePath);
        store.Save(new Preferences());
        Assert.Throws<InvalidDataException>(() => store.Save(new Preferences { VideoHeight = 480 }));
        Assert.Equal(new Preferences(), store.Load().Value);
    }

    [Fact]
    public void FailedWritePreservesTheExistingFile()
    {
        var store = new JsonPreferencesStore(FilePath);
        store.Save(new Preferences());
        var bytes = File.ReadAllBytes(FilePath);
        using (var locked = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<InvalidDataException>(() => store.Save(new Preferences { Language = "en" }));
        Assert.Equal(bytes, File.ReadAllBytes(FilePath));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DistributionDataIsSeparateFromLegacySettings(bool portable)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, portable ? "portable.mode" : "installed.mode"), "");
        var localData = Path.Combine(root, "user-local");
        var legacy = Path.Combine(root, "roaming", "catamedia", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, "legacy sentinel");
        var paths = ApplicationPaths.Resolve(root, localData);
        var expectedRoot = portable ? root : Path.Combine(localData, "CataMedia");
        Assert.Equal(Path.Combine(expectedRoot, "data"), paths.DataDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "temp"), paths.TemporaryDirectory);
        new JsonPreferencesStore(paths.PreferencesFile).Save(new Preferences());
        Assert.Equal("legacy sentinel", File.ReadAllText(legacy));
        Assert.False(Directory.Exists(paths.TemporaryDirectory));
    }

    [Fact]
    public void AmbiguousOrMissingDistributionMarkersFailExplicitly()
    {
        Assert.Throws<InvalidDataException>(() => ApplicationPaths.Resolve(root, root));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "portable.mode"), "");
        File.WriteAllText(Path.Combine(root, "installed.mode"), "");
        Assert.Throws<InvalidDataException>(() => ApplicationPaths.Resolve(root, root));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
