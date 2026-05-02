# media_download

Simple Windows GUI for yt-dlp downloads, focused on easy use by non-technical users.

## Features
- Paste multiple URLs (one per line)
- Choose output folder
- Video downloads: select 720p or 1080p (MP4)
- Audio-only downloads: choose format (mp3/wav/flac) and quality (Low/Standard/HQ)
- Responsive UI with cancel
- Optional download/update of yt-dlp

## Requirements
- Windows 10/11
- Windows PowerShell 5.1 (pre-installed)
- yt-dlp (can be downloaded from the UI)
- FFmpeg + FFprobe (recommended for best video results; required for audio-only downloads; the UI can download a portable copy)

## Run
- Double-click `yt-gui.bat`
- Or run:
```powershell
powershell.exe -STA -File .\yt-gui.ps1
```

## Tutorial (PT-BR)
See [TUTORIAL_PT_BR.md](TUTORIAL_PT_BR.md)

## Notes
- The app stores settings in `%APPDATA%\yt-gui\settings.json`.
- Logs are created in the chosen output folder when downloads run.

## Author
Developed by Wisley Vilela
https://github.com/Wisleyv

## License
MIT
