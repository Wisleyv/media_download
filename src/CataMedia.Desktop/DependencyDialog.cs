using System.Windows;
using System.Windows.Controls;
using CataMedia.Windows;

namespace CataMedia.Desktop;

public sealed class DependencyDialog : Window
{
    private readonly DependencyManager manager;
    private readonly string language;
    private readonly Func<DependencyKind, string?> current;
    private readonly Action<DependencyKind, string> activated;
    private readonly ComboBox choice = new() { ItemsSource = new[] { "yt-dlp", "FFmpeg + FFprobe", "Node.js 22 (YouTube)" }, SelectedIndex = 0,
        Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock details = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 12) };
    private readonly Button check = new(), install = new(), rollback = new();
    private readonly ProgressBar transferBar = new() { Minimum = 0, Maximum = 100, Height = 16, Visibility = Visibility.Collapsed };
    private readonly TextBlock transferDetails = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 8) };
    private readonly System.Windows.Threading.DispatcherTimer transferTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DependencyDownloadProgress transfer = new(0, null);
    private readonly System.Diagnostics.Stopwatch sinceData = new();
    private bool downloading;
    private readonly CancellationTokenSource lifetime = new();
    private DependencyRelease? release;
    private bool busy;
    private bool closeAfterCancel;
    private string? installedVersion;
    private string T(string key) => UiStrings.Get(key, language);
    private DependencyKind Kind => (DependencyKind)choice.SelectedIndex;

    public DependencyDialog(DependencyManager manager, string language, Func<DependencyKind, string?> current,
        Action<DependencyKind, string> activated)
    {
        this.manager = manager; this.language = language; this.current = current; this.activated = activated;
        Title = T("Components"); Width = 640; Height = 640; MinWidth = 540; MinHeight = 500;
        SetResourceReference(StyleProperty, "WindowStyle");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new(20) };
        panel.Children.Add(new TextBlock { Text = T("ComponentsConsent"), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new Label { Content = T("Component"), Target = choice });
        panel.Children.Add(choice); panel.Children.Add(details);
        panel.Children.Add(transferBar); panel.Children.Add(transferDetails);
        transferTimer.Tick += (_, _) => UpdateTransfer();
        check.Content = T("CheckNow"); install.Content = T("InstallUpdate"); rollback.Content = T("RestorePrevious");
        install.SetResourceReference(StyleProperty, "PrimaryButton");
        foreach (var button in new[] { check, install, rollback }) { button.Margin = new(0, 3, 0, 3); button.Padding = new(8); panel.Children.Add(button); }
        var cancel = new Button { Content = T("Close"), Margin = new(0, 10, 0, 0), Padding = new(8) };
        cancel.Click += (_, _) => Close(); panel.Children.Add(cancel);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        choice.SelectionChanged += async (_, _) => { Reset(); await CheckSelectedAsync(); };
        Loaded += async (_, _) => await CheckSelectedAsync();
        check.Click += async (_, _) => await CheckSelectedAsync();
        install.Click += async (_, _) =>
        {
            if (release is null || busy) return;
            var selected = release;
            if (installedVersion is not null && !DependencyManager.IsNewer(Kind, selected.Version, installedVersion)) return;
            if (MessageBox.Show(this, string.Format(T("ConfirmComponent"), choice.SelectedItem, selected.Version, selected.Source),
                Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            await RunAsync(async () =>
            {
                downloading = true; transfer = new(0, null); sinceData.Restart(); transferTimer.Start(); UpdateTransfer();
                var path = await manager.InstallAsync(selected, new Progress<string>(key =>
                {
                    if (!busy || release is null) return;
                    details.Text = T(key);
                    if (key == "ValidatingComponent")
                    {
                        downloading = false; transferTimer.Stop();
                        transferBar.IsIndeterminate = true; transferDetails.Text = T("ValidatingComponent");
                    }
                }), lifetime.Token, new Progress<DependencyDownloadProgress>(value =>
                {
                    if (!busy || !downloading) return;
                    if (value.ReceivedBytes > transfer.ReceivedBytes) sinceData.Restart();
                    transfer = value; UpdateTransfer();
                }));
                activated(selected.Kind, path); release = null;
                details.Text = string.Format(T("ComponentReadyNext"), choice.SelectedItem, selected.Version);
            });
        };
        rollback.Click += async (_, _) =>
        {
            if (MessageBox.Show(this, T("ConfirmRestore"), Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            await RunAsync(async () =>
            {
                var path = await manager.RollbackAsync(Kind, lifetime.Token);
                activated(Kind, path); release = null; details.Text = T("ComponentRestored");
            });
        };
        Closing += (_, e) =>
        {
            if (busy) { e.Cancel = true; closeAfterCancel = true; lifetime.Cancel(); }
        };
        Closed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
        Reset();
    }

    private void Reset() { release = null; installedVersion = null; details.Text = T("CheckComponentsHint"); UpdateButtons(); }
    private void UpdateTransfer()
    {
        const double megabyte = 1_000_000;
        transferBar.Visibility = Visibility.Visible;
        transferBar.IsIndeterminate = transfer.TotalBytes is null;
        var format = System.Globalization.CultureInfo.GetCultureInfo(language == "en" ? "en-US" : "pt-BR");
        if (transfer.TotalBytes is { } total)
        {
            transferBar.Value = Math.Min(100, 100.0 * transfer.ReceivedBytes / total);
            transferDetails.Text = string.Format(format, T("ComponentTransferKnown"), transfer.ReceivedBytes / megabyte,
                total / megabyte, transferBar.Value);
        }
        else transferDetails.Text = string.Format(format, T("ComponentTransferUnknown"), transfer.ReceivedBytes / megabyte);
        transferDetails.Text += "\n" + string.Format(format,
            T(transfer.ReceivedBytes == 0 ? "ComponentTransferConnecting" : "ComponentTransferWaiting"),
            (int)sinceData.Elapsed.TotalSeconds);
    }
    private Task CheckSelectedAsync() => RunAsync(async () =>
    {
        release = null; installedVersion = null;
        var executable = current(Kind);
        if (executable is not null)
        {
            try { installedVersion = await manager.ReadVersionAsync(Kind, executable, lifetime.Token); }
            catch (Exception error) when (Recoverable(error)) { }
        }
        release = await manager.CheckAsync(Kind, true, lifetime.Token);
        details.Text = string.Format(T("Versions"), installedVersion ?? T("NotAvailable"), release.Version)
            + "\n" + release.Source + "\n\n" + T("ComponentCheckResultHint");
    });
    private void UpdateButtons()
    {
        choice.IsEnabled = check.IsEnabled = rollback.IsEnabled = !busy;
        install.IsEnabled = !busy && release is not null &&
            (installedVersion is null || DependencyManager.IsNewer(Kind, release.Version, installedVersion));
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if (busy) return;
        busy = true; UpdateButtons(); details.Text = T("CheckingComponents");
        try { await operation(); }
        catch (Exception error) when (Recoverable(error))
        {
            details.Text = T(error is OperationCanceledException ? "ComponentCancelled" : "ComponentFailed");
            // Bounded, in-memory technical details; no per-download log files.
            details.Text += "\n" + error.Message[..Math.Min(error.Message.Length, 600)];
        }
        finally
        {
            downloading = false; transferTimer.Stop(); sinceData.Stop();
            transferBar.Visibility = Visibility.Collapsed; transferDetails.Text = "";
            busy = false; UpdateButtons(); if (closeAfterCancel) Close();
        }
    }
    internal static bool Recoverable(Exception error) => error is System.IO.IOException or UnauthorizedAccessException
        or System.Net.Http.HttpRequestException or OperationCanceledException or System.Text.Json.JsonException
        or InvalidOperationException or ArgumentException or KeyNotFoundException or System.ComponentModel.Win32Exception or PlatformNotSupportedException;
}
