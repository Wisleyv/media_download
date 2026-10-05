# Licenças / Third-party notices

CataMedia: MIT, copyright 2026 Wisley Vilela; LICENSE.txt accompanies the application.

The self-contained distribution includes Microsoft .NET and Windows Desktop runtime files.
Their LICENSE and ThirdPartyNotices files supplied by the restored runtime packages are
included under licenses/, separately for each runtime and with versions in BUILD.json/runtimeconfig.
.NET uses MIT; included third-party components have their own notices:
https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT
https://github.com/dotnet/wpf/blob/main/LICENSE.TXT
https://github.com/dotnet/wpf/blob/main/THIRD-PARTY-NOTICES.txt

yt-dlp, FFmpeg/FFprobe and Node.js executables are NOT included in these packages.
They are obtained separately with consent or selected by the user. Their version, source,
checksum and license indication are recorded next to managed installations in source.json.
Windows yt-dlp and Gyan FFmpeg essentials use GPLv3+; Node.js uses MIT and additional notices.
https://github.com/yt-dlp/yt-dlp#licensing
https://www.gyan.dev/ffmpeg/builds/
https://github.com/nodejs/node/blob/main/LICENSE

Inno Setup 6.7.3 generates the installer; it is not an application dependency.
License and third-party terms: https://jrsoftware.org/files/is/license.txt
