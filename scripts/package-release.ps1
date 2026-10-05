param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^v\d+\.\d+\.\d+$')]
  [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$outputDirectory = Join-Path $root 'releases'
$outputPath = Join-Path $outputDirectory "catamedia-$Version.zip"
if (Test-Path $outputPath) { throw "Package already exists: $outputPath" }
$trackedChanges = & git -C $root status --porcelain --untracked-files=no
if ($LASTEXITCODE -ne 0 -or $trackedChanges) { throw 'Commit tracked changes before packaging.' }
$commit = & git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve HEAD.' }
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
# Archive committed files only; never include downloaded dependencies or local state.
& git -C $root archive --format=zip "--output=$outputPath" $commit -- catamedia.ps1 catamedia.bat catamedia.vbs LICENSE docs/README.md docs/TUTORIAL.md
if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
Get-FileHash -LiteralPath $outputPath -Algorithm SHA256
