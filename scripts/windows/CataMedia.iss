#ifndef Payload
  #error Payload must be supplied
#endif
[Setup]
AppId={{CC32D069-602C-4574-A6C8-CA49986A10C4}
AppName=CataMedia
AppVersion={#AppVersion}
AppPublisher=Wisley Vilela
AppPublisherURL=https://github.com/Wisleyv/media_download
AppSupportURL=https://github.com/Wisleyv/media_download/issues
AppUpdatesURL=https://github.com/Wisleyv/media_download/releases
DefaultDirName={localappdata}\Programs\CataMedia
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
DisableProgramGroupPage=yes
LicenseFile={#Payload}\LICENSE.txt
OutputDir={#Output}
OutputBaseFilename=CataMedia-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\CataMedia.exe
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "portable.mode"
Source: "{#InstalledMarker}"; DestDir: "{app}"; DestName: "installed.mode"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{app}\portable.mode"

[Icons]
Name: "{userprograms}\CataMedia"; Filename: "{app}\CataMedia.exe"
Name: "{userdesktop}\CataMedia"; Filename: "{app}\CataMedia.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CataMedia.exe"; Description: "{cm:LaunchProgram,CataMedia}"; Flags: nowait postinstall skipifsilent

; No data-directory or media deletion during uninstall. No dependency downloads.
