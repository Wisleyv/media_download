using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using CataMedia.Core;
using CataMedia.Windows;
using Microsoft.Win32;

namespace CataMedia.Desktop;

public partial class MainWindow : Window
{
    private readonly JsonPreferencesStore store;
    private readonly IVideoDownloadService downloadService;
    private CancellationTokenSource? activeDownload;
    private bool closeAfterCancel;
    private string? completedFolder;

    public MainWindow(ApplicationPaths paths, IVideoDownloadService? downloadService = null)
    {
        InitializeComponent();
        store = new(paths.PreferencesFile);
        this.downloadService = downloadService ?? new YtDlpVideoDownloadService(new ExternalProcessRunner());
        ModeLabel.Text = paths.Mode == DistributionMode.Portable ? "Modo portátil / Portable mode" : "Modo instalado / Installed mode";
        DataLabel.Text = "Dados / Data: " + paths.DataDirectory;
        var loaded = store.Load();
        var preferences = loaded.Value;
        LanguageChoice.SelectedIndex = preferences.Language == "en" ? 1 : 0;
        Destination.Text = preferences.DestinationFolder;
        MediaChoice.SelectedIndex = (int)preferences.Mode;
        ResolutionChoice.SelectedIndex = preferences.VideoHeight == 1080 ? 1 : 0;
        AudioChoice.SelectedIndex = (int)preferences.AudioFormat;
        QualityChoice.SelectedIndex = (int)preferences.AudioQuality;
        YtDlpPath.Text = FindTool("yt-dlp.exe", paths.DependenciesDirectory) ?? "";
        FfmpegFolder.Text = Path.GetDirectoryName(FindTool("ffmpeg.exe", paths.DependenciesDirectory)) ?? "";
        NodePath.Text = FindTool("node.exe", paths.DependenciesDirectory) ?? "";
        SaveButton.IsEnabled = loaded.CanSave;
        StatusLabel.Text = loaded.CanSave ? "Preferências carregadas / Preferences loaded."
            : "Não foi possível ler as preferências. O arquivo foi preservado; salvar está bloqueado. / Could not read preferences. The file was preserved; saving is disabled.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            store.Save(new Preferences
            {
                Language = LanguageChoice.SelectedIndex == 1 ? "en" : "pt",
                DestinationFolder = Destination.Text.Trim(),
                Mode = (MediaMode)MediaChoice.SelectedIndex,
                VideoHeight = ResolutionChoice.SelectedIndex == 1 ? 1080 : 720,
                AudioFormat = (AudioFormat)AudioChoice.SelectedIndex,
                AudioQuality = (AudioQuality)QualityChoice.SelectedIndex
            });
            StatusLabel.Text = "Preferências salvas / Preferences saved.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusLabel.Text = "Não foi possível salvar. Confira se a pasta permite gravação. / Could not save. Check folder write permissions.";
        }
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

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (activeDownload is not null) return;
        using var cancellation = new CancellationTokenSource();
        activeDownload = cancellation;
        completedFolder = null;
        SetBusy(true);
        var progress = new Progress<DownloadProgress>(update =>
        {
            if (activeDownload != cancellation || cancellation.IsCancellationRequested) return;
            StatusLabel.Text = update.Stage switch
            {
                DownloadStage.Preparing => "Preparando / Preparing…",
                DownloadStage.Processing => "Juntando vídeo e áudio / Merging video and audio…",
                DownloadStage.Verifying => "Verificando o arquivo / Verifying file…",
                _ => "Baixando / Downloading…"
            };
            DownloadProgressBar.IsIndeterminate = update.Percentage is null;
            DownloadProgressBar.Value = update.Percentage ?? 0;
        });
        try
        {
            var result = await downloadService.DownloadAsync(
                new(VideoUrl.Text.Trim(), Destination.Text.Trim(), ResolutionChoice.SelectedIndex == 1 ? 1080 : 720),
                new(YtDlpPath.Text.Trim(), FfmpegFolder.Text.Trim(), string.IsNullOrWhiteSpace(NodePath.Text) ? null : NodePath.Text.Trim()),
                progress, cancellation.Token);
            completedFolder = Path.GetDirectoryName(result.FilePath);
            DownloadProgressBar.Value = 100;
            StatusLabel.Text = $"Concluído / Completed — MP4, {result.ActualHeight}p\n{result.FilePath}";
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Cancelado. Arquivos parciais podem permanecer para uma nova tentativa. / Cancelled. Partial files may remain for retry.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or Win32Exception)
        {
            StatusLabel.Text = error.Message;
        }
        finally
        {
            activeDownload = null;
            DownloadProgressBar.IsIndeterminate = false;
            SetBusy(false);
            if (closeAfterCancel) Close();
        }
    }

    private void SetBusy(bool busy)
    {
        DownloadInputs.IsEnabled = !busy;
        DownloadButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        OpenFolderButton.IsEnabled = !busy && completedFolder is not null;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        StatusLabel.Text = "Cancelando / Cancelling…";
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
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(this) == true) Destination.Text = dialog.FolderName;
    }

    private void ChooseFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(this) == true) FfmpegFolder.Text = dialog.FolderName;
    }

    private void ChooseYtDlp_Click(object sender, RoutedEventArgs e) => ChooseExecutable(path => YtDlpPath.Text = path);
    private void ChooseNode_Click(object sender, RoutedEventArgs e) => ChooseExecutable(path => NodePath.Text = path);
    private void ChooseExecutable(Action<string> selected)
    {
        var dialog = new OpenFileDialog { Filter = "Executável / Executable (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) selected(dialog.FileName);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (completedFolder is not null && Directory.Exists(completedFolder))
                Process.Start(new ProcessStartInfo(completedFolder) { UseShellExecute = true });
        }
        catch (Exception error) when (error is Win32Exception or IOException)
        {
            StatusLabel.Text = "Não foi possível abrir a pasta. / Could not open folder.";
        }
    }
}
