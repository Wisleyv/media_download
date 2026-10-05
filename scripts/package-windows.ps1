param(
    [string]$DotNet = 'dotnet',
    [Parameter(Mandatory)][string]$Iscc
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$changes = & git -C $root status --porcelain --untracked-files=normal
if ($LASTEXITCODE -ne 0 -or $changes) { throw 'Commit project changes before packaging.' }
$commit = & git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source commit.' }
$project = Join-Path $root 'src/CataMedia.Desktop/CataMedia.Desktop.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?$') { throw 'Invalid package version.' }
if ((Get-FileHash -LiteralPath $Iscc -Algorithm SHA256).Hash -ne '0A8757031B33777E4C9CBFFEE40F11A5062B36D25CBE144C1DB73B6102B80AD7') {
    throw 'Use the validated official Inno Setup 6.7.3 compiler.'
}
$compilerVersion = '6.7.3'
$destination = Join-Path $root "artifacts/packages/$version-$($commit.Substring(0,12))"
if (Test-Path -LiteralPath $destination) { throw "Package directory already exists: $destination" }
# Never delete an existing build, data directory or release.
New-Item -ItemType Directory -Path $destination | Out-Null
$payload = Join-Path $destination 'payload'
& $DotNet restore $project --runtime win-x64 --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
& $DotNet publish $project -c Release -r win-x64 --self-contained true --no-restore -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false "-p:SourceRevisionId=$commit" -o $payload
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
if (Test-Path -LiteralPath (Join-Path $payload 'LICENSE.txt')) {
    Move-Item -LiteralPath (Join-Path $payload 'LICENSE.txt') -Destination (Join-Path $payload 'DOTNET-LICENSE.txt')
}
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $payload 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $root 'docs/WINDOWS_GUIDE.md') -Destination (Join-Path $payload 'GUIDE.txt')
Copy-Item -LiteralPath (Join-Path $root 'docs/WINDOWS_THIRD_PARTY.md') -Destination (Join-Path $payload 'THIRD-PARTY.txt')
$packageRoot = (& $DotNet msbuild $project -nologo -getProperty:NuGetPackageRoot | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $packageRoot)) { throw 'Cannot locate restored runtime notices.' }
$frameworks = (Get-Content -LiteralPath (Join-Path $payload 'CataMedia.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks
foreach ($framework in $frameworks) {
    $package = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $noticeRoot = Join-Path $packageRoot "$package/$($framework.version)"
    $notices = @(Get-ChildItem -LiteralPath $noticeRoot -File | Where-Object { $_.Name -match 'LICENSE|THIRD-PARTY-NOTICES' })
    if (!($notices | Where-Object { $_.Name -match '^LICENSE' })) { throw "Runtime license missing: $package" }
    if ($package -eq 'microsoft.netcore.app.runtime.win-x64' -and $notices.Count -lt 2) { throw 'Core runtime third-party notices missing.' }
    $licenseFolder = Join-Path $payload "licenses/$package"
    New-Item -ItemType Directory -Path $licenseFolder -Force | Out-Null
    $notices | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $licenseFolder }
}
if (!(Test-Path -LiteralPath (Join-Path $payload 'coreclr.dll'))) { throw 'Self-contained runtime is missing.' }
if (!(Test-Path -LiteralPath (Join-Path $payload 'LICENSE.txt'))) { throw 'Application license is missing.' }
$manifest = [ordered]@{ version=$version; commit=$commit; runtime='win-x64'; selfContained=$true; sdk=(& $DotNet --version); innoSetup=$compilerVersion; files=@() }
$manifest.files = @(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{ path=[IO.Path]::GetRelativePath($payload,$_.FullName); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $payload 'BUILD.json') -Encoding utf8
$zip = Join-Path $destination "CataMedia-$version-win-x64-portable.zip"
Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zip -CompressionLevel Optimal
$marker = Join-Path $destination 'installed.mode'
Set-Content -LiteralPath $marker -Value 'installed' -Encoding ascii
& $Iscc "/DPayload=$payload" "/DInstalledMarker=$marker" "/DOutput=$destination" "/DAppVersion=$version" (Join-Path $PSScriptRoot 'windows/CataMedia.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installer = Join-Path $destination "CataMedia-Setup-$version-win-x64.exe"
@($zip,$installer) | ForEach-Object { "{0}  {1}" -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(),[IO.Path]::GetFileName($_) } | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Packages: $destination"
