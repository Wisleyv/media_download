@echo off
setlocal
set "SCRIPT=%~dp0yt-gui.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%SCRIPT%" %*
endlocal
