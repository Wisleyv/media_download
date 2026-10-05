using System.IO;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class DistributionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StartupChecksTheCorrectDataFolderWithoutChangingExistingPreferences(bool portable)
    {
        var root = Path.Combine(Path.GetTempPath(), "CataMedia-distribution-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, portable ? "portable.mode" : "installed.mode"), "");
            var paths = ApplicationPaths.Resolve(root, Path.Combine(root, "user-profile"));
            Directory.CreateDirectory(paths.DataDirectory);
            File.WriteAllText(paths.PreferencesFile, "existing data");
            paths.VerifyWritable();
            Assert.Equal("existing data", File.ReadAllText(paths.PreferencesFile));
            Assert.Single(Directory.GetFiles(paths.DataDirectory));
            Assert.Equal(portable ? Path.Combine(root, "data") : Path.Combine(root, "user-profile", "CataMedia", "data"), paths.DataDirectory);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void UnusablePortableDataFolderFailsWithoutOverwritingTheObstruction()
    {
        var root = Path.Combine(Path.GetTempPath(), "CataMedia-distribution-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var paths = new ApplicationPaths(DistributionMode.Portable, Path.Combine(root, "data"), Path.Combine(root, "temp"));
            File.WriteAllText(paths.DataDirectory, "preserve");
            Assert.Throws<IOException>(() => paths.VerifyWritable());
            Assert.Equal("preserve", File.ReadAllText(paths.DataDirectory));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
