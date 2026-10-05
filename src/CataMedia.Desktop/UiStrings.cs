namespace CataMedia.Desktop;

public static class UiStrings
{
    public static IReadOnlyDictionary<string, (string Pt, string En)> Values { get; } = new Dictionary<string, (string, string)>
    {
        ["Hint"] = ("Cole os links, escolha o formato e baixe. Um link por linha.", "Paste links, choose a format and download. One link per line."),
        ["Links"] = ("_Links", "_Links"), ["Paste"] = ("_Colar", "_Paste"),
        ["Language"] = ("_Idioma", "_Language"), ["Destination"] = ("_Salvar em", "_Save to"),
        ["ChooseFolder"] = ("Escolher pasta", "Choose folder"), ["MediaType"] = ("_Salvar como", "Save _as"),
        ["Video"] = ("Vídeo MP4", "MP4 video"), ["Audio"] = ("Só áudio", "Audio only"),
        ["Resolution"] = ("_Resolução máxima", "Maximum _resolution"),
        ["UpTo720"] = ("Até 720p", "Up to 720p"), ["UpTo1080"] = ("Até 1080p", "Up to 1080p"),
        ["ResolutionHint"] = ("A origem pode oferecer uma resolução menor; a resolução efetiva aparece ao concluir.", "The source may offer a lower resolution; the actual resolution is shown when complete."),
        ["AudioFormat"] = ("_Formato de áudio", "Audio _format"), ["AudioQuality"] = ("_Qualidade MP3", "MP3 _quality"),
        ["Low"] = ("Baixa — arquivo menor", "Low — smaller file"), ["Standard"] = ("Padrão — recomendada", "Standard — recommended"), ["High"] = ("Alta", "High"),
        ["ShortLow"] = ("Baixa", "Low"), ["ShortStandard"] = ("Padrão", "Standard"), ["ShortHigh"] = ("Alta", "High"),
        ["Lossless"] = ("WAV e FLAC preservam o áudio recebido, mas não recuperam qualidade perdida na origem.", "WAV and FLAC preserve the received audio but cannot restore quality lost at the source."),
        ["Advanced"] = ("Mais opções e componentes", "More options and components"),
        ["ComponentsHint"] = ("Use os componentes já instalados. A instalação e atualização assistidas serão adicionadas posteriormente.", "Use existing components. Assisted installation and updates will be added later."),
        ["ChooseYtDlp"] = ("Escolher yt-dlp", "Choose yt-dlp"), ["FfmpegFolder"] = ("Pasta com FFmpeg e FFprobe", "Folder containing FFmpeg and FFprobe"),
        ["ChooseComponents"] = ("Escolher componentes", "Choose components"), ["ChooseNode"] = ("Escolher Node.js", "Choose Node.js"),
        ["Cookies"] = ("Cookies do navegador (opcional)", "Browser cookies (optional)"), ["NoCookies"] = ("Nenhum", "None"),
        ["CookiesHint"] = ("Só habilite para conteúdo que você tem autorização para acessar. O aplicativo não armazena seus cookies.", "Only enable for content you are authorized to access. The application does not store your cookies."),
        ["Save"] = ("Salvar preferências", "Save preferences"), ["Download"] = ("_Baixar", "_Download"),
        ["Cancel"] = ("_Cancelar", "_Cancel"), ["Retry"] = ("Repetir falhas", "Retry failed items"), ["OpenFolder"] = ("Abrir pasta", "Open folder"),
        ["Queue"] = ("Fila", "Queue"), ["QueueTitle"] = ("Título ou link", "Title or link"), ["QueueFormat"] = ("Formato", "Format"), ["QueueState"] = ("Estado", "Status"),
        ["Details"] = ("Detalhes do item selecionado", "Selected item details"),
        ["DetailsHint"] = ("Informações técnicas do motor podem estar em outro idioma. Nenhum log é salvo automaticamente.", "Technical engine details may be in another language. No logs are saved automatically."),
        ["Ready"] = ("Pronto para baixar.", "Ready to download."), ["PreferencesSaved"] = ("Preferências salvas.", "Preferences saved."),
        ["PreferencesUnreadable"] = ("As preferências não puderam ser lidas. O arquivo foi preservado e o salvamento está bloqueado.", "Preferences could not be read. The file was preserved and saving is disabled."),
        ["SaveFailed"] = ("Não foi possível salvar. Confira a permissão de gravação da pasta.", "Could not save. Check folder write permissions."),
        ["Inspecting"] = ("Consultando os links antes de baixar…", "Inspecting links before downloading…"),
        ["Preparing"] = ("Preparando…", "Preparing…"), ["Downloading"] = ("Baixando…", "Downloading…"),
        ["Processing"] = ("Preparando o arquivo final…", "Preparing the final file…"), ["Verifying"] = ("Verificando o arquivo…", "Verifying the file…"),
        ["Pending"] = ("Aguardando", "Waiting"), ["Running"] = ("Em andamento", "In progress"), ["Completed"] = ("Concluído", "Completed"),
        ["Failed"] = ("Falhou — consulte os detalhes", "Failed — see details"), ["Cancelled"] = ("Cancelado", "Cancelled"),
        ["Cancelling"] = ("Cancelando…", "Cancelling…"),
        ["CancelledHint"] = ("Cancelado. Arquivos parciais podem permanecer; os itens seguintes não foram baixados.", "Cancelled. Partial files may remain; subsequent items were not downloaded."),
        ["Summary"] = ("Concluídos: {0}. Falhas: {1}.", "Completed: {0}. Failed: {1}."),
        ["InputError"] = ("Confira os links, o destino e os componentes. Consulte os detalhes para identificar a causa.", "Check links, destination and components. See details to identify the cause."),
        ["NoLinks"] = ("Cole pelo menos um link válido, um por linha.", "Paste at least one valid link, one per line."),
        ["OpenFailed"] = ("Não foi possível abrir a pasta.", "Could not open the folder."),
        ["Portable"] = ("Modo portátil", "Portable mode"), ["Installed"] = ("Modo instalado", "Installed mode"),
        ["DataFolder"] = ("Dados: {0}", "Data: {0}"), ["ExecutableFilter"] = ("Executável (*.exe)|*.exe", "Executable (*.exe)|*.exe"),
        ["PlaylistTitle"] = ("Confirmar lista", "Confirm playlist"),
        ["PlaylistPrompt"] = ("{0}\nEsta lista contém {1} itens. O que deseja baixar?", "{0}\nThis playlist contains {1} items. What would you like to download?"),
        ["StartupError"] = ("Não foi possível abrir o CataMedia. Confira a pasta do aplicativo e os marcadores de distribuição.", "Could not open CataMedia. Check the application folder and distribution markers."),
        ["WholePlaylist"] = ("Baixar a lista", "Download playlist"), ["SingleVideo"] = ("Só este vídeo", "Only this video"),
        ["SingleUnavailable"] = ("Este link aponta somente para uma lista; não há um vídeo individual selecionado.", "This link points only to a playlist; no individual video is selected."),
        ["PasteFailed"] = ("Não foi possível ler a área de transferência. Cole os links manualmente.", "Could not read the clipboard. Paste the links manually.")
    };

    public static string Get(string key, string language) => language == "en" ? Values[key].En : Values[key].Pt;
}
