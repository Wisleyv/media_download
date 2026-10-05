using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using CataMedia.Core;
using CataMedia.Desktop;
using CataMedia.Windows;

namespace CataMedia.Tests;

public sealed class WindowTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WindowLoadsAndExplicitlySavesPreferencesInEachMode(bool portable)
    {
        OnStaThread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "CataMedia-window-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            try
            {
                File.WriteAllText(Path.Combine(root, portable ? "portable.mode" : "installed.mode"), "");
                var paths = ApplicationPaths.Resolve(root, Path.Combine(root, "local"));
                var store = new JsonPreferencesStore(paths.PreferencesFile);
                window = new MainWindow(paths);
                window.Show();
                Assert.False(File.Exists(paths.PreferencesFile));
                ((TextBox)window.FindName("Destination")).Text = @"C:\Vídeos de teste";
                ((ComboBox)window.FindName("ResolutionChoice")).SelectedIndex = 1;
                ((Button)window.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(1080, store.Load().Value.VideoHeight);
                Assert.Equal(@"C:\Vídeos de teste", store.Load().Value.DestinationFolder);
                window.Close();
                window = new MainWindow(paths);
                window.Show();
                Assert.Equal(1, ((ComboBox)window.FindName("ResolutionChoice")).SelectedIndex);
                Assert.Equal(@"C:\Vídeos de teste", ((TextBox)window.FindName("Destination")).Text);
            }
            finally
            {
                window?.Close();
                Directory.Delete(root, recursive: true);
            }
        });
    }

    [Fact]
    public void WindowDisablesSavingWhenPreferencesCannotBeRead()
    {
        OnStaThread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "CataMedia-window-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var paths = new ApplicationPaths(DistributionMode.Portable, root, Path.Combine(root, "temp"));
            File.WriteAllText(paths.PreferencesFile, "broken");
            MainWindow? window = null;
            try
            {
                window = new MainWindow(paths);
                Assert.False(((Button)window.FindName("SaveButton")).IsEnabled);
                Assert.Equal("broken", File.ReadAllText(paths.PreferencesFile));
            }
            finally
            {
                window?.Close();
                Directory.Delete(root, recursive: true);
            }
        });
    }

    private static void OnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Window test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
