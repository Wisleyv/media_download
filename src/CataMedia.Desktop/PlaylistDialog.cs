using System.Windows;
using System.Windows.Controls;
using CataMedia.Core;

namespace CataMedia.Desktop;

public sealed class PlaylistDialog : Window
{
    public PlaylistSelection Selection { get; private set; } = PlaylistSelection.Cancel;

    public PlaylistDialog(MediaLinkResolution resolution, string language)
    {
        Title = UiStrings.Get("PlaylistTitle", language);
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = string.Format(UiStrings.Get("PlaylistPrompt", language), resolution.Title, resolution.Entries.Count), TextWrapping = TextWrapping.Wrap });
        if (resolution.SingleVideo is null)
            panel.Children.Add(new TextBlock { Text = UiStrings.Get("SingleUnavailable", language), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
        AddChoice(panel, "WholePlaylist", PlaylistSelection.EntirePlaylist, language, true);
        AddChoice(panel, "SingleVideo", PlaylistSelection.SingleVideo, language, resolution.SingleVideo is not null);
        AddChoice(panel, "Cancel", PlaylistSelection.Cancel, language, true);
        Content = panel;
    }

    private void AddChoice(Panel panel, string key, PlaylistSelection selection, string language, bool enabled)
    {
        var button = new Button { Content = UiStrings.Get(key, language), IsEnabled = enabled, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 12, 0, 0) };
        button.Click += (_, _) => { Selection = selection; DialogResult = selection != PlaylistSelection.Cancel; };
        panel.Children.Add(button);
    }
}
