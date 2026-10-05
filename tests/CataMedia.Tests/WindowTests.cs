using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void WindowTracksDownloadCompletionCancellationAndClose(bool cancel, bool close)
    {
        OnStaThread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "CataMedia-window-" + Guid.NewGuid().ToString("N"));
            var paths = new ApplicationPaths(DistributionMode.Portable, root, Path.Combine(root, "temp"));
            var service = new ControlledDownload();
            var window = new MainWindow(paths, service);
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                window.Show();
                ((TextBox)window.FindName("VideoUrl")).Text = "https://example.com/video";
                ((TextBox)window.FindName("Destination")).Text = root;
                ((ComboBox)window.FindName("ResolutionChoice")).SelectedIndex = 1;
                var start = (Button)window.FindName("DownloadButton");
                start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(start.IsEnabled);
                Assert.Equal(1080, service.Request?.MaximumHeight);
                if (close) window.Close();
                else if (cancel) ((Button)window.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else service.Complete(new(Path.Combine(root, "video.mp4"), 1080, 12));
                PumpUntil(() => start.IsEnabled);
                Assert.Equal(!cancel, ((Button)window.FindName("OpenFolderButton")).IsEnabled);
                var status = ((TextBlock)window.FindName("StatusLabel")).Text;
                Assert.Contains(cancel ? "Cancelado" : "Concluído", status);
                if (close) Assert.False(window.IsVisible);
                Assert.False(Directory.Exists(root)); // Editing/downloading never implicitly saves preferences.
            }
            finally
            {
                service.Cancel();
                window.Close();
            }
        });
    }

    private static void PumpUntil(Func<bool> ready)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        var deadline = DateTime.UtcNow.AddSeconds(5);
        timer.Tick += (_, _) => { if (ready() || DateTime.UtcNow >= deadline) frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        Assert.True(ready(), "UI did not finish the download state transition.");
    }

    private sealed class ControlledDownload : IVideoDownloadService
    {
        private readonly TaskCompletionSource<VideoDownloadResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public VideoDownloadRequest? Request { get; private set; }
        public async Task<VideoDownloadResult> DownloadAsync(VideoDownloadRequest request, DownloadTools tools,
            IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            Request = request;
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            return await completion.Task;
        }
        public void Complete(VideoDownloadResult result) => completion.TrySetResult(result);
        public void Cancel() => completion.TrySetCanceled();
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
