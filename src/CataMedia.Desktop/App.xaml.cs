using System.IO;
using System.Globalization;
using System.Windows;
using CataMedia.Windows;

namespace CataMedia.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplyContrast();
        SystemParameters.StaticPropertyChanged += OnSystemStyleChanged;
        try
        {
            var paths = ApplicationPaths.Resolve(AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            paths.VerifyWritable();
            var window = new MainWindow(paths);
            var area = SystemParameters.WorkArea;
            window.MinWidth = Math.Min(window.MinWidth, area.Width);
            window.MinHeight = Math.Min(window.MinHeight, area.Height);
            window.Width = Math.Min(window.Width, area.Width);
            window.Height = Math.Min(window.Height, area.Height);
            window.Show();
            _ = window.CheckStartupComponentsAsync();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(UiStrings.Get("StartupError", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),
                "CataMedia", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnSystemStyleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast)) Dispatcher.Invoke(ApplyContrast);
    }

    private void ApplyContrast()
    {
        // Keep system colors and keyboard focus usable when Windows high contrast changes.
        Resources.MergedDictionaries.Clear();
        foreach (var key in new[] { "PageBrush", "SurfaceBrush", "FieldBrush", "InkBrush", "MutedBrush", "LineBrush", "AccentBrush", "AccentTextBrush", "TintBrush" })
            Resources.Remove(key);
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/CataMedia;component/Styles.xaml") });
        if (!SystemParameters.HighContrast) return;
        foreach (var key in new[] { "PageBrush", "SurfaceBrush", "FieldBrush" }) Resources[key] = SystemColors.WindowBrush;
        foreach (var key in new[] { "InkBrush", "MutedBrush", "LineBrush" }) Resources[key] = SystemColors.WindowTextBrush;
        Resources["AccentBrush"] = SystemColors.HighlightBrush;
        Resources["AccentTextBrush"] = SystemColors.HighlightTextBrush;
        Resources["TintBrush"] = SystemColors.ControlBrush;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemStyleChanged;
        base.OnExit(e);
    }
}
