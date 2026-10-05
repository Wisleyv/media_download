using System.IO;
using System.Windows;
using CataMedia.Core;
using CataMedia.Windows;

namespace CataMedia.Desktop;

public partial class MainWindow : Window
{
    private readonly JsonPreferencesStore store;

    public MainWindow(ApplicationPaths paths)
    {
        InitializeComponent();
        store = new(paths.PreferencesFile);
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
}
