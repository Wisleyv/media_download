# media_download

Simple Windows GUI for yt-dlp downloads, focused on easy use by non-technical users.

## Features
- Paste multiple URLs (one per line)
- Choose output folder
- Select 720p or 1080p
- Responsive UI with cancel
- Optional download/update of yt-dlp

## Requirements
- Windows
- PowerShell 7+ (`pwsh`)
- yt-dlp (can be downloaded from the UI)

## Run
```powershell
pwsh -STA -File .\yt-gui.ps1
```

## Notes
- The app stores settings in `%APPDATA%\yt-gui\settings.json`.
- Logs are created in the chosen output folder when downloads run.

## Author
Developed by Wisley Vilela
https://github.com/Wisleyv

## License
MIT
