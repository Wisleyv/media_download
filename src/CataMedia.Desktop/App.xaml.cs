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
        try
        {
            var paths = ApplicationPaths.Resolve(AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            paths.VerifyWritable();
            var window = new MainWindow(paths);
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
}
