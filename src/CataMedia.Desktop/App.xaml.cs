using System.IO;
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
            new MainWindow(paths).Show();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show("Não foi possível abrir o CataMedia. Confira a pasta do aplicativo e os marcadores de distribuição.\n\n" + error.Message,
                "CataMedia", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
