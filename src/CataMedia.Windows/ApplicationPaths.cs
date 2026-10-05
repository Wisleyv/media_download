namespace CataMedia.Windows;

public enum DistributionMode { Portable, Installed }

public sealed record ApplicationPaths(DistributionMode Mode, string DataDirectory, string TemporaryDirectory)
{
    public string PreferencesFile => Path.Combine(DataDirectory, "preferences.json");
    public string DependenciesDirectory => Path.Combine(DataDirectory, "dependencies");

    public void VerifyWritable()
    {
        Directory.CreateDirectory(DataDirectory);
        var probe = Path.Combine(DataDirectory, ".write-check-" + Guid.NewGuid().ToString("N"));
        using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            1, FileOptions.DeleteOnClose);
        stream.WriteByte(0);
        stream.Flush(flushToDisk: true);
    }

    public static ApplicationPaths Resolve(string executableDirectory, string localApplicationData)
    {
        var portable = File.Exists(Path.Combine(executableDirectory, "portable.mode"));
        var installed = File.Exists(Path.Combine(executableDirectory, "installed.mode"));
        if (portable == installed)
            throw new InvalidDataException("Exactly one distribution marker is required.");

        var root = Path.GetFullPath(portable ? executableDirectory : Path.Combine(localApplicationData, "CataMedia"));
        return new(portable ? DistributionMode.Portable : DistributionMode.Installed,
            Path.Combine(root, "data"), Path.Combine(root, "temp"));
    }
}
