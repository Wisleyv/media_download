using System.IO;
using System.Text.Json;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class RealDependencyTests
{
    [RealDependencyFact]
    public async Task ObtainsValidatesAndRestoresRealExternalComponents()
    {
        var output = Environment.GetEnvironmentVariable("CATAMEDIA_DEPENDENCY_TEST_OUTPUT")
            ?? throw new InvalidOperationException("Set an isolated dependency output folder.");
        Directory.CreateDirectory(output);
        var root = Path.Combine(output, "components-" + Guid.NewGuid().ToString("N"));
        using var client = DependencyManager.CreateClient();
        var manager = new DependencyManager(root, client, new ExternalProcessRunner());
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var evidence = new Dictionary<string, object>();
        foreach (var kind in Enum.GetValues<DependencyKind>())
        {
            var release = await manager.CheckAsync(kind, true, timeout.Token);
            var path = await manager.InstallAsync(release, null, timeout.Token);
            var version = await manager.ReadVersionAsync(kind, path, timeout.Token);
            Assert.Equal(release.Version, version);
            Assert.Equal(path, manager.ActiveExecutable(kind));
            if (kind == DependencyKind.Ffmpeg)
                Assert.Equal(version, await manager.ReadVersionAsync(kind, Path.Combine(Path.GetDirectoryName(path)!, "ffprobe.exe"), timeout.Token));
            evidence[kind.ToString()] = new { release, path, version };
            if (kind == DependencyKind.YtDlp)
            {
                // A second validated installation tests activation/restore with real binaries,
                // without requesting or replacing an older third-party release.
                var second = await manager.InstallAsync(release, null, timeout.Token);
                Assert.NotEqual(path, second);
                Assert.Equal(path, await manager.RollbackAsync(kind, timeout.Token));
            }
        }
        File.WriteAllText(Path.Combine(output, "dependency-evidence.json"), JsonSerializer.Serialize(evidence));
    }
}

public sealed class RealDependencyFactAttribute : FactAttribute
{
    public RealDependencyFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CATAMEDIA_REAL_DEPENDENCIES") != "1")
            Skip = "Opt-in acquisition of external packages; requires an isolated output folder.";
    }
}
