Set WshShell = CreateObject("WScript.Shell")
WshShell.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File """ & Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\")) & "catamedia.ps1""", 0, False
