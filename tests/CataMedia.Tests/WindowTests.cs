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
    [Fact]
    public void OfflineStartupCheckKeepsDownloadAvailableAndWindowConstructionDoesNotUseNetwork()
    {
        OnStaThread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "CataMedia-offline-" + Guid.NewGuid().ToString("N"));
            using var handler = new OfflineHandler();
            using var client = new System.Net.Http.HttpClient(handler);
            var manager = new DependencyManager(root, client, new VersionRunner());
            var window = new MainWindow(new(DistributionMode.Portable, root, Path.Combine(root, "temp")), dependencyManager: manager);
            try
            {
                Assert.Equal(0, handler.Calls);
                ((TextBox)window.FindName("YtDlpPath")).Text = Path.Combine(root, "yt-dlp.exe");
                ((TextBox)window.FindName("NodePath")).Text = "";
                var check = window.CheckStartupComponentsAsync();
                PumpUntil(() => check.IsCompleted);
                check.GetAwaiter().GetResult();
                Assert.Equal(1, handler.Calls);
                Assert.True(((Button)window.FindName("DownloadButton")).IsEnabled);
                Assert.Equal(Visibility.Visible, ((StackPanel)window.FindName("ComponentBanner")).Visibility);
                ((ComboBox)window.FindName("LanguageChoice")).SelectedIndex = 1;
                Assert.Contains("missing", ((TextBlock)window.FindName("ComponentNotice")).Text);
                Assert.False(File.Exists(Path.Combine(root, "preferences.json")));
            }
            finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
        });
    }

    private sealed class OfflineHandler : System.Net.Http.HttpMessageHandler
    {
        public int Calls;
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; throw new System.Net.Http.HttpRequestException("Offline fixture"); }
    }
    private sealed class VersionRunner : IProcessRunner
    {
        public Task<int> RunAsync(ProcessCommand command, Action<string> standardOutput, Action<string> standardError, CancellationToken cancellationToken)
        { standardOutput("2026.08.19"); return Task.FromResult(0); }
    }

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
            var window = new MainWindow(paths, service, new SingleLinkResolver());
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
                Assert.False(((Button)window.FindName("ComponentsButton")).IsEnabled);
                Assert.False(((Button)window.FindName("ComponentUpdateButton")).IsEnabled);
                Assert.Equal(1080, service.Request?.MaximumHeight);
                if (close) window.Close();
                else if (cancel) ((Button)window.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else service.Complete(new(Path.Combine(root, "video.mp4"), 1080, 12));
                PumpUntil(() => start.IsEnabled);
                Assert.True(((Button)window.FindName("ComponentsButton")).IsEnabled);
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

    [Fact]
    public void LanguageAndAudioChoicesAreVisibleApplicableAndSavedExplicitly()
    {
        OnStaThread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "CataMedia-language-" + Guid.NewGuid().ToString("N"));
            var paths = new ApplicationPaths(DistributionMode.Portable, root, Path.Combine(root, "temp"));
            var window = new MainWindow(paths);
            try
            {
                window.Show();
                ((ComboBox)window.FindName("MediaChoice")).SelectedIndex = 1;
                ((ComboBox)window.FindName("AudioChoice")).SelectedIndex = 1;
                Assert.Equal(Visibility.Collapsed, ((StackPanel)window.FindName("VideoOptions")).Visibility);
                Assert.Equal(Visibility.Visible, ((StackPanel)window.FindName("AudioOptions")).Visibility);
                Assert.Equal(Visibility.Collapsed, ((StackPanel)window.FindName("Mp3Options")).Visibility);
                ((ComboBox)window.FindName("LanguageChoice")).SelectedIndex = 1;
                Assert.Equal("_Download", ((Button)window.FindName("DownloadButton")).Content);
                Assert.Equal("Ready to download.", ((TextBlock)window.FindName("StatusLabel")).Text);
                Assert.False(File.Exists(paths.PreferencesFile));
                ((ComboBox)window.FindName("AudioChoice")).SelectedIndex = 2;
                ((ComboBox)window.FindName("CookiesChoice")).SelectedIndex = 3;
                ((Button)window.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var saved = new JsonPreferencesStore(paths.PreferencesFile).Load().Value;
                Assert.Equal("en", saved.Language);
                Assert.Equal(MediaMode.Audio, saved.Mode);
                Assert.Equal(AudioFormat.Flac, saved.AudioFormat);
                Assert.Equal("firefox", saved.CookiesBrowser);
            }
            finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
        });
    }

    [Theory]
    [InlineData(PlaylistSelection.Cancel, 0)]
    [InlineData(PlaylistSelection.SingleVideo, 1)]
    [InlineData(PlaylistSelection.EntirePlaylist, 2)]
    public void PlaylistChoiceControlsTheActualTransferCount(PlaylistSelection selection, int expected)
    {
        OnStaThread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var root = Path.Combine(Path.GetTempPath(), "CataMedia-playlist-" + Guid.NewGuid().ToString("N"));
            var paths = new ApplicationPaths(DistributionMode.Portable, root, Path.Combine(root, "temp"));
            var service = new CountingDownload();
            var choices = 0;
            var window = new MainWindow(paths, service, new PlaylistResolver(), _ => { choices++; return selection; });
            try
            {
                window.Show();
                ((TextBox)window.FindName("VideoUrl")).Text = "https://example.com/list";
                var button = (Button)window.FindName("DownloadButton");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpUntil(() => button.IsEnabled);
                Assert.Equal(1, choices);
                Assert.Equal(expected, service.Calls.Count);
                Assert.Equal(expected, ((DataGrid)window.FindName("QueueList")).Items.Count);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void RetryButtonRepeatsOnlyFailedRows()
    {
        OnStaThread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var root = Path.Combine(Path.GetTempPath(), "CataMedia-retry-" + Guid.NewGuid().ToString("N"));
            var service = new CountingDownload { FailFirst = true };
            var window = new MainWindow(new(DistributionMode.Portable, root, Path.Combine(root, "temp")), service, new SingleLinkResolver());
            try
            {
                window.Show();
                ((TextBox)window.FindName("VideoUrl")).Text = "https://example.com/first\nhttps://example.com/second";
                var download = (Button)window.FindName("DownloadButton");
                download.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpUntil(() => download.IsEnabled);
                var retry = (Button)window.FindName("RetryButton");
                Assert.True(retry.IsEnabled);
                Assert.Equal(2, service.Calls.Count);
                service.FailFirst = false;
                retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpUntil(() => download.IsEnabled);
                Assert.Equal(3, service.Calls.Count);
                Assert.Equal("https://example.com/first", service.Calls[^1]);
                Assert.False(retry.IsEnabled);
                Assert.All(((DataGrid)window.FindName("QueueList")).Items.Cast<QueueEntry>(), row => Assert.Equal(QueueItemState.Completed, row.State));
            }
            finally { window.Close(); }
        });
    }

    private sealed class CountingDownload : IMediaDownloadService
    {
        public List<string> Calls { get; } = [];
        public bool FailFirst { get; set; }
        public Task<MediaDownloadResult> DownloadAsync(MediaDownloadRequest request, DownloadTools tools, IProgress<DownloadProgress>? progress, CancellationToken token)
        {
            Calls.Add(request.Url);
            if (FailFirst && request.Url.EndsWith("first", StringComparison.Ordinal)) throw new IOException("fixture failure");
            return Task.FromResult(new MediaDownloadResult(Path.Combine(request.DestinationFolder, "file.mp4"), 720, 10));
        }
    }

    private sealed class PlaylistResolver : IMediaLinkResolver
    {
        public Task<MediaLinkResolution> ResolveAsync(MediaDownloadRequest request, DownloadTools tools, CancellationToken token) =>
            Task.FromResult(new MediaLinkResolution("List", [new("https://example.com/first", "First"), new("https://example.com/second", "Second")], true, new(request.Url, "Single")));
    }

    [Fact]
    public void FailedInspectionDoesNotBlockOtherItemsAndRetryKeepsPlaylistConfirmation()
    {
        OnStaThread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var root = Path.Combine(Path.GetTempPath(), "CataMedia-inspection-" + Guid.NewGuid().ToString("N"));
            var service = new CountingDownload();
            var resolver = new RecoveringPlaylistResolver();
            var confirmations = 0;
            var window = new MainWindow(new(DistributionMode.Portable, root, Path.Combine(root, "temp")), service, resolver,
                _ => { confirmations++; return PlaylistSelection.Cancel; });
            try
            {
                window.Show();
                ((TextBox)window.FindName("VideoUrl")).Text = "https://example.com/first\nhttps://example.com/list";
                var download = (Button)window.FindName("DownloadButton");
                download.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpUntil(() => download.IsEnabled);
                Assert.Single(service.Calls);
                Assert.Equal("https://example.com/first", service.Calls[0]);
                Assert.True(((Button)window.FindName("RetryButton")).IsEnabled);
                resolver.FailList = false;
                ((Button)window.FindName("RetryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpUntil(() => download.IsEnabled);
                Assert.Equal(1, confirmations);
                Assert.Single(service.Calls); // Canceling a newly inspectable playlist never downloads it.
            }
            finally { window.Close(); }
        });
    }

    private sealed class RecoveringPlaylistResolver : IMediaLinkResolver
    {
        public bool FailList { get; set; } = true;
        public Task<MediaLinkResolution> ResolveAsync(MediaDownloadRequest request, DownloadTools tools, CancellationToken token)
        {
            if (!request.Url.EndsWith("list", StringComparison.Ordinal)) return Task.FromResult(new MediaLinkResolution("First", [new(request.Url, "First")]));
            if (FailList) throw new IOException("fixture metadata failure");
            return Task.FromResult(new MediaLinkResolution("List", [new("https://example.com/list-first", "List first")], true));
        }
    }

    private sealed class ControlledDownload : IMediaDownloadService
    {
        private readonly TaskCompletionSource<MediaDownloadResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public MediaDownloadRequest? Request { get; private set; }
        public async Task<MediaDownloadResult> DownloadAsync(MediaDownloadRequest request, DownloadTools tools,
            IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            Request = request;
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            return await completion.Task;
        }
        public void Complete(MediaDownloadResult result) => completion.TrySetResult(result);
        public void Cancel() => completion.TrySetCanceled();
    }

    private sealed class SingleLinkResolver : IMediaLinkResolver
    {
        public Task<MediaLinkResolution> ResolveAsync(MediaDownloadRequest request, DownloadTools tools, CancellationToken cancellationToken) =>
            Task.FromResult(new MediaLinkResolution("Test", [new(request.Url, "Test")]));
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
