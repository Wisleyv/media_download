using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using CataMedia.Core;
using CataMedia.Windows;
using Microsoft.Win32;

namespace CataMedia.Desktop;

public partial class MainWindow : Window
{
    private readonly JsonPreferencesStore store;
    private readonly ApplicationPaths paths;
    private readonly System.Net.Http.HttpClient dependencyClient;
    private readonly DependencyManager dependencies;
    private readonly CancellationTokenSource componentLifetime = new();
    private bool checkingComponents;
    private string componentNoticeKey = "Ready";
    private object[] componentNoticeArguments = [];
    private readonly DownloadQueue downloadQueue;
    private readonly IMediaLinkResolver linkResolver;
    private readonly Func<MediaLinkResolution, PlaylistSelection> choosePlaylist;
    private readonly ObservableCollection<QueueEntry> queue = [];
    private CancellationTokenSource? activeDownload;
    private bool closeAfterCancel;
    private bool initialized;
    private string statusKey = "Ready";
    private object[] statusArguments = [];
    private string UiLanguage => LanguageChoice.SelectedIndex == 1 ? "en" : "pt";
    private string T(string key) => UiStrings.Get(key, UiLanguage);
    private void Help_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window { Title = T("Help"), Owner = this, Width = 490, Height = 330,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(20) };
        var version = typeof(MainWindow).Assembly.GetCustomAttributes(false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion;
        panel.Children.Add(new TextBlock { Text = "CataMedia " + version, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = T("Credits"), Margin = new Thickness(0, 8, 0, 12) });
        AddLink("Manual", "https://github.com/Wisleyv/media_download/blob/feature/windows-dotnet-stage5/docs/WINDOWS_GUIDE.md");
        var guide = Path.Combine(AppContext.BaseDirectory, "GUIDE.txt");
        if (File.Exists(guide)) AddLink("LocalGuide", guide);
        AddLink("AppReleases", "https://github.com/Wisleyv/media_download/releases");
        panel.Children.Add(new TextBlock { Text = T("ReleaseHint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
        dialog.Content = new ScrollViewer { Content = panel };
        dialog.ShowDialog();

        void AddLink(string label, string target)
        {
            var button = new Button { Content = T(label), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(8) };
            button.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
                catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
                { MessageBox.Show(dialog, T("OpenFailed"), T("Help"), MessageBoxButton.OK, MessageBoxImage.Error); }
            };
            panel.Children.Add(button);
        }
    }
    private static readonly string?[] Browsers = [null, "chrome", "edge", "firefox", "brave"];

    public MainWindow(ApplicationPaths paths, IMediaDownloadService? downloadService = null,
        IMediaLinkResolver? linkResolver = null, Func<MediaLinkResolution, PlaylistSelection>? choosePlaylist = null,
        DependencyManager? dependencyManager = null)
    {
        InitializeComponent();
        this.paths = paths;
        store = new(paths.PreferencesFile);
        var runner = new ExternalProcessRunner();
        dependencyClient = DependencyManager.CreateClient();
        dependencies = dependencyManager ?? new(paths.DependenciesDirectory, dependencyClient, runner);
        downloadQueue = new(downloadService ?? new YtDlpMediaDownloadService(runner));
        this.linkResolver = linkResolver ?? new YtDlpMediaLinkResolver(runner);
        this.choosePlaylist = choosePlaylist ?? (resolution =>
        {
            var dialog = new PlaylistDialog(resolution, UiLanguage) { Owner = this };
            dialog.ShowDialog();
            return dialog.Selection;
        });
        var loaded = store.Load();
        var preferences = loaded.Value;
        LanguageChoice.SelectedIndex = preferences.Language == "en" ? 1 : 0;
        Destination.Text = preferences.DestinationFolder.Length > 0 ? preferences.DestinationFolder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        MediaChoice.SelectedIndex = (int)preferences.Mode;
        ResolutionChoice.SelectedIndex = preferences.VideoHeight == 1080 ? 1 : 0;
        AudioChoice.SelectedIndex = (int)preferences.AudioFormat;
        QualityChoice.SelectedIndex = (int)preferences.AudioQuality;
        CookiesChoice.SelectedIndex = Array.IndexOf(Browsers, preferences.CookiesBrowser);
        YtDlpPath.Text = FindTool("yt-dlp.exe", paths.DependenciesDirectory) ?? "";
        FfmpegFolder.Text = Path.GetDirectoryName(FindTool("ffmpeg.exe", paths.DependenciesDirectory)) ?? "";
        NodePath.Text = FindTool("node.exe", paths.DependenciesDirectory) ?? "";
        RefreshManagedComponents();
        QueueList.ItemsSource = queue;
        SaveButton.IsEnabled = loaded.CanSave;
        statusKey = loaded.CanSave ? "Ready" : "PreferencesUnreadable";
        initialized = true;
        ApplyLanguage();
        UpdateOptions();
        Closed += (_, _) => { componentLifetime.Cancel(); dependencyClient.Dispose(); };
    }

    private void RefreshManagedComponents()
    {
        foreach (var kind in Enum.GetValues<DependencyKind>())
        {
            try { if (dependencies.ActiveExecutable(kind) is { } path) ActivateComponent(kind, path); }
            catch (Exception error) when (DependencyDialog.Recoverable(error)) { }
        }
    }

    private string? CurrentComponent(DependencyKind kind) => kind switch
    {
        DependencyKind.YtDlp => string.IsNullOrWhiteSpace(YtDlpPath.Text) ? null : YtDlpPath.Text.Trim(),
        DependencyKind.Ffmpeg => string.IsNullOrWhiteSpace(FfmpegFolder.Text) ? null : Path.Combine(FfmpegFolder.Text.Trim(), "ffmpeg.exe"),
        _ => string.IsNullOrWhiteSpace(NodePath.Text) ? null : NodePath.Text.Trim()
    };

    private void ActivateComponent(DependencyKind kind, string path)
    {
        if (kind == DependencyKind.YtDlp) YtDlpPath.Text = path;
        else if (kind == DependencyKind.Ffmpeg) FfmpegFolder.Text = Path.GetDirectoryName(path)!;
        else NodePath.Text = path;
    }

    // App startup calls this; constructing a window in offline tests does not contact the network.
    public async Task CheckStartupComponentsAsync()
    {
        if (checkingComponents) return;
        checkingComponents = true;
        try
        {
            var missingKinds = Enum.GetValues<DependencyKind>()
                .Where(kind => CurrentComponent(kind) is not { } path || !File.Exists(path)).ToList();
            if (CurrentComponent(DependencyKind.Ffmpeg) is { } ffmpeg && !File.Exists(Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe"))
                && !missingKinds.Contains(DependencyKind.Ffmpeg)) missingKinds.Add(DependencyKind.Ffmpeg);
            if (CurrentComponent(DependencyKind.Node) is { } node)
            {
                try { await dependencies.ReadVersionAsync(DependencyKind.Node, node, componentLifetime.Token); }
                catch (Exception error) when (DependencyDialog.Recoverable(error))
                { if (!missingKinds.Contains(DependencyKind.Node)) missingKinds.Add(DependencyKind.Node); }
            }
            var missing = missingKinds.Count > 0;
            if (missing) ShowComponentNotice("MissingComponents", string.Join(", ", missingKinds.Select(kind => kind switch
                { DependencyKind.YtDlp => "yt-dlp", DependencyKind.Ffmpeg => "FFmpeg + FFprobe", _ => "Node.js" })));
            else if (componentNoticeKey == "MissingComponents") ComponentBanner.Visibility = Visibility.Collapsed;
            var executable = CurrentComponent(DependencyKind.YtDlp);
            string? installed = null;
            if (executable is not null)
            {
                try { installed = await dependencies.ReadVersionAsync(DependencyKind.YtDlp, executable, componentLifetime.Token); }
                catch (Exception error) when (DependencyDialog.Recoverable(error)) { }
            }
            var release = await dependencies.CheckAsync(DependencyKind.YtDlp, false, componentLifetime.Token);
            // First-run setup must remain visible even when the release check succeeds.
            if (!missing && (installed is null || DependencyManager.IsNewer(DependencyKind.YtDlp, release.Version, installed)))
            {
                ShowComponentNotice("UpdateNotice", installed ?? "—", release.Version);
            }
        }
        catch (Exception error) when (DependencyDialog.Recoverable(error)) { /* Existing components remain usable offline. */ }
        finally { checkingComponents = false; }
    }

    private void ShowComponentNotice(string key, params object[] arguments)
    {
        componentNoticeKey = key; componentNoticeArguments = arguments;
        ComponentNotice.Text = string.Format(T(key), arguments);
        ComponentBanner.Visibility = Visibility.Visible;
    }

    private async void Components_Click(object sender, RoutedEventArgs e)
    {
        if (activeDownload is not null) return;
        var dialog = new DependencyDialog(dependencies, UiLanguage, CurrentComponent, ActivateComponent) { Owner = this };
        dialog.ShowDialog();
        await CheckStartupComponentsAsync();
    }
    private void Later_Click(object sender, RoutedEventArgs e) => ComponentBanner.Visibility = Visibility.Collapsed;

    private Preferences ReadPreferences() => new()
    {
        Language = UiLanguage, DestinationFolder = Destination.Text.Trim(), Mode = (MediaMode)MediaChoice.SelectedIndex,
        VideoHeight = ResolutionChoice.SelectedIndex == 1 ? 1080 : 720,
        AudioFormat = (AudioFormat)AudioChoice.SelectedIndex, AudioQuality = (AudioQuality)QualityChoice.SelectedIndex,
        CookiesBrowser = Browsers[CookiesChoice.SelectedIndex]
    };

    private DownloadTools ReadTools() => new(YtDlpPath.Text.Trim(), FfmpegFolder.Text.Trim(),
        string.IsNullOrWhiteSpace(NodePath.Text) ? null : NodePath.Text.Trim());

    private MediaDownloadRequest Request(string url)
    {
        var options = ReadPreferences();
        return new(url, options.DestinationFolder, options.VideoHeight)
        {
            Mode = options.Mode, AudioFormat = options.AudioFormat,
            AudioQuality = options.AudioQuality, CookiesBrowser = options.CookiesBrowser
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { store.Save(ReadPreferences()); SetStatus("PreferencesSaved"); }
        catch (Exception error) when (DownloadQueue.IsExpectedFailure(error)) { SetStatus("SaveFailed"); }
    }

    private void OptionsChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized) return;
        ApplyLanguage();
        UpdateOptions();
    }

    private void ApplyLanguage()
    {
        foreach (var key in UiStrings.Values.Keys) Resources[key] = T(key);
        TitleColumn.Header = T("QueueTitle"); FormatColumn.Header = T("QueueFormat"); StateColumn.Header = T("QueueState");
        ModeLabel.Text = T(paths.Mode == DistributionMode.Portable ? "Portable" : "Installed");
        DataLabel.Text = string.Format(T("DataFolder"), paths.DataDirectory);
        foreach (var row in queue) row.Refresh(UiLanguage);
        StatusLabel.Text = string.Format(T(statusKey), statusArguments);
        ComponentNotice.Text = string.Format(T(componentNoticeKey), componentNoticeArguments);
    }

    private void UpdateOptions()
    {
        var audio = MediaChoice.SelectedIndex == 1;
        VideoOptions.Visibility = audio ? Visibility.Collapsed : Visibility.Visible;
        AudioOptions.Visibility = audio ? Visibility.Visible : Visibility.Collapsed;
        Mp3Options.Visibility = AudioChoice.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        LosslessHint.Visibility = AudioChoice.SelectedIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetStatus(string key, params object[] arguments)
    {
        statusKey = key;
        statusArguments = arguments;
        StatusLabel.Text = string.Format(T(key), arguments);
    }

    private static string? FindTool(string fileName, string dependencies)
    {
        foreach (var directory in new[] { dependencies, AppContext.BaseDirectory }
                     .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)))
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) continue;
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private async void Download_Click(object sender, RoutedEventArgs e) => await RunQueueAsync();
    private async void Retry_Click(object sender, RoutedEventArgs e) =>
        await RunQueueAsync(queue.Where(row => row.State == QueueItemState.Failed).ToArray());

    private async Task RunQueueAsync(IReadOnlyList<QueueEntry>? retry = null)
    {
        if (activeDownload is not null || retry is { Count: 0 }) return;
        using var cancellation = new CancellationTokenSource();
        activeDownload = cancellation;
        SetBusy(true);
        DiagnosticDetails.Clear();
        try
        {
            var tools = ReadTools();
            IReadOnlyList<QueueEntry> rows;
            if (retry is null)
            {
                var requests = VideoUrl.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => Request(line.Trim())).ToArray();
                if (requests.Length == 0) { SetStatus("NoLinks"); return; }
                foreach (var request in requests) request.Validate();
                List<QueueEntry> prepared = [];
                SetStatus("Inspecting");
                DownloadProgressBar.IsIndeterminate = true;
                foreach (var request in requests)
                {
                    prepared.AddRange(await InspectAsync(request, tools, cancellation.Token));
                    if (prepared.Count > 500) throw new InvalidDataException("The queue is limited to 500 items.");
                }
                queue.Clear();
                foreach (var row in prepared) { row.Refresh(UiLanguage); queue.Add(row); }
                rows = prepared.Where(row => row.State == QueueItemState.Pending).ToArray();
            }
            else
            {
                List<QueueEntry> ready = [];
                foreach (var row in retry)
                {
                    if (row.RequiresInspection)
                    {
                        var inspected = await InspectAsync(row.Item.Request, tools, cancellation.Token);
                        if (queue.Count - 1 + inspected.Count > 500) throw new InvalidDataException("The queue is limited to 500 items.");
                        var position = queue.IndexOf(row);
                        queue.Remove(row);
                        foreach (var replacement in inspected)
                        {
                            replacement.Refresh(UiLanguage);
                            queue.Insert(position++, replacement);
                            if (replacement.State == QueueItemState.Pending) ready.Add(replacement);
                        }
                    }
                    else
                    {
                        row.Update(new(row.Item.Id, QueueItemState.Pending), UiLanguage);
                        ready.Add(row);
                    }
                }
                rows = ready;
            }
            var updates = new Progress<QueueUpdate>(update =>
            {
                if (activeDownload != cancellation || cancellation.IsCancellationRequested) return;
                var row = rows.First(entry => entry.Item.Id == update.Id);
                row.Update(update, UiLanguage);
                if (update.State == QueueItemState.Running)
                {
                    QueueList.SelectedItem = row;
                    SetStatus(update.Progress?.Stage.ToString() ?? "Preparing");
                    DownloadProgressBar.IsIndeterminate = update.Progress?.Percentage is null;
                    DownloadProgressBar.Value = update.Progress?.Percentage ?? 0;
                }
                ShowSelectionDetails();
            });
            var result = await downloadQueue.RunAsync(rows.Select(row => row.Item).ToArray(), tools, updates, cancellation.Token);
            foreach (var outcome in result.Results) rows.First(row => row.Item.Id == outcome.Id).Update(outcome, UiLanguage);
            QueueList.SelectedItem = queue.FirstOrDefault(row => row.State == QueueItemState.Failed)
                ?? queue.LastOrDefault(row => row.State == QueueItemState.Completed);
            ShowSelectionDetails();
            if (result.Cancelled) SetStatus("CancelledHint");
            else
            {
                DownloadProgressBar.Value = 100;
                SetStatus("Summary", queue.Count(row => row.State == QueueItemState.Completed), queue.Count(row => row.State == QueueItemState.Failed));
            }
        }
        catch (OperationCanceledException) { SetStatus("CancelledHint"); }
        catch (Exception error) when (DownloadQueue.IsExpectedFailure(error))
        {
            SetStatus("InputError");
            DiagnosticDetails.Text = error.Message;
        }
        finally
        {
            activeDownload = null;
            DownloadProgressBar.IsIndeterminate = false;
            SetBusy(false);
            if (closeAfterCancel) Close();
        }
    }

    private async Task<IReadOnlyList<QueueEntry>> InspectAsync(MediaDownloadRequest request, DownloadTools tools, CancellationToken token)
    {
        try
        {
            var resolution = await linkResolver.ResolveAsync(request, tools, token);
            token.ThrowIfCancellationRequested();
            var entries = resolution.IsPlaylist ? PlaylistPolicy.Select(resolution, choosePlaylist(resolution)) : resolution.Entries;
            if (entries.Count == 0) throw new OperationCanceledException();
            List<QueueEntry> prepared = [];
            foreach (var entry in entries)
            {
                var resolvedRequest = request with { Url = entry.Url };
                resolvedRequest.Validate();
                prepared.Add(new(new(Guid.NewGuid(), entry.Title, resolvedRequest)));
            }
            return prepared;
        }
        catch (Exception error) when (DownloadQueue.IsExpectedFailure(error))
        {
            var row = new QueueEntry(new(Guid.NewGuid(), request.Url, request)) { RequiresInspection = true };
            row.Update(new(row.Item.Id, QueueItemState.Failed, Error: error.Message), UiLanguage);
            return [row];
        }
    }

    private void SetBusy(bool busy)
    {
        DownloadInputs.IsEnabled = !busy;
        DownloadButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        ComponentsButton.IsEnabled = ComponentUpdateButton.IsEnabled = !busy;
        UpdateActions();
    }

    private void UpdateActions()
    {
        OpenFolderButton.IsEnabled = activeDownload is null && QueueList.SelectedItem is QueueEntry { Result: not null };
        RetryButton.IsEnabled = activeDownload is null && queue.Any(row => row.State == QueueItemState.Failed);
    }

    private void ShowSelectionDetails()
    {
        if (QueueList.SelectedItem is QueueEntry row)
        {
            ResultFile.Text = row.Result?.FilePath ?? "";
            DiagnosticDetails.Text = row.Error ?? "";
        }
        UpdateActions();
    }

    private void QueueSelectionChanged(object sender, SelectionChangedEventArgs e) => ShowSelectionDetails();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        SetStatus("Cancelling");
        activeDownload?.Cancel();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (activeDownload is not null)
        {
            e.Cancel = true;
            closeAfterCancel = true;
            Cancel_Click(this, new RoutedEventArgs());
        }
        base.OnClosing(e);
    }

    private void ChooseDestination_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = T("ChooseFolder") };
        if (dialog.ShowDialog(this) == true) Destination.Text = dialog.FolderName;
    }
    private void ChooseFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = T("ChooseComponents") };
        if (dialog.ShowDialog(this) == true) FfmpegFolder.Text = dialog.FolderName;
    }
    private void ChooseYtDlp_Click(object sender, RoutedEventArgs e) => ChooseExecutable(path => YtDlpPath.Text = path);
    private void ChooseNode_Click(object sender, RoutedEventArgs e) => ChooseExecutable(path => NodePath.Text = path);
    private void ChooseExecutable(Action<string> selected)
    {
        var dialog = new OpenFileDialog { Filter = T("ExecutableFilter"), CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) selected(dialog.FileName);
    }
    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        try { if (Clipboard.ContainsText()) VideoUrl.Text = Clipboard.GetText(); }
        catch (ExternalException) { SetStatus("PasteFailed"); }
    }
    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (QueueList.SelectedItem is QueueEntry { Result: not null } row && Path.GetDirectoryName(row.Result.FilePath) is { } folder && Directory.Exists(folder))
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception error) when (DownloadQueue.IsExpectedFailure(error)) { SetStatus("OpenFailed"); }
    }
}
