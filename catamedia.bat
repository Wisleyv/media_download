@echo off
setlocal
set "SCRIPT=%~dp0catamedia.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%SCRIPT%" %*
endlocal
