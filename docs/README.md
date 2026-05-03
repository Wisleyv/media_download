# CataMedia

> **PT-BR:** Interface gráfica simples para Windows que baixa vídeos e músicas do YouTube usando yt-dlp.
> **EN:** Simple Windows GUI for downloading videos and music from YouTube using yt-dlp.

---

## Recursos / Features

- Cole múltiplas URLs (uma por linha) / Paste multiple URLs (one per line)
- Escolha a pasta de saída / Choose output folder
- Download de vídeo: 720p ou 1080p (MP4) / Video downloads: 720p or 1080p (MP4)
- Extração de áudio: mp3, wav ou flac (Baixa/Padrão/HQ) / Audio extraction: mp3, wav or flac (Low/Standard/HQ)
- Interface responsiva com cancelamento / Responsive UI with cancel
- Download/atualização automática do yt-dlp / Auto download/update of yt-dlp
- Troca de idioma na interface (PT-BR / EN) / Language switch in the UI (PT-BR / EN)
- Executável sem janela de console visível (via `.vbs`) / Run without visible console window (via `.vbs`)

## Requisitos / Requirements

- Windows 10/11
- Windows PowerShell 5.1 (pré-instalado / pre-installed)
- yt-dlp (pode ser baixado pela interface / can be downloaded from the UI)
- FFmpeg + FFprobe (recomendado para vídeo; obrigatório para áudio; a interface oferece download portátil / recommended for video; required for audio; the UI can download a portable copy)

## Execução / Run

**Sem janela de console / Without console window (recommended):**
- Dê duplo clique em `catamedia.vbs` / Double-click `catamedia.vbs`

**Com janela de console / With console window (fallback):**
- Dê duplo clique em `catamedia.bat` / Double-click `catamedia.bat`

**Via linha de comando / Command line:**
```powershell
powershell.exe -STA -File .\catamedia.ps1
```

## Tutorial (PT-BR / EN)

Veja / See [TUTORIAL.md](TUTORIAL.md)

## Notas / Notes

- As configurações ficam em / Settings are stored in `%APPDATA%\catamedia\settings.json`
- Logs são criados na pasta de saída ao rodar downloads / Logs are created in the output folder when downloads run
- O idioma escolhido é salvo automaticamente / The chosen language is saved automatically

## Autor / Author

Desenvolvido por / Developed by Wisley Vilela
https://github.com/Wisleyv

## Licença / License

MIT
