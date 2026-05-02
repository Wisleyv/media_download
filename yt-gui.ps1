Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[System.Windows.Forms.Application]::SetUnhandledExceptionMode([System.Windows.Forms.UnhandledExceptionMode]::CatchException)
[System.Windows.Forms.Application]::add_ThreadException({
  param($sender, $e)
  if ($e.Exception -is [System.Management.Automation.PipelineStoppedException]) { return }
  [System.Windows.Forms.MessageBox]::Show("Erro inesperado: $($e.Exception.Message)") | Out-Null
})

if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
  $ps = Get-Command 'powershell.exe' -ErrorAction SilentlyContinue
  if ($ps) {
    $argsList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-STA', '-File', $PSCommandPath) + @($args)
    Start-Process -FilePath $ps.Source -ArgumentList $argsList -WorkingDirectory (Split-Path -Path $PSCommandPath -Parent) | Out-Null
    return
  }
  [System.Windows.Forms.MessageBox]::Show('Este script precisa rodar em STA. Use: powershell.exe -STA -File yt-gui.ps1') | Out-Null
  return
}

$settingsPath = Join-Path $env:APPDATA 'yt-gui\settings.json'

function Get-Settings {
  if (Test-Path $settingsPath) {
    try {
      return Get-Content -Path $settingsPath -Raw | ConvertFrom-Json
    } catch {
      return [pscustomobject]@{}
    }
  }
  return [pscustomobject]@{}
}

function Save-Settings(
  [string]$ytPath,
  [string]$folder,
  [string]$quality,
  [string]$cookiesBrowser,
  [string]$downloadMode,
  [string]$audioFormat,
  [string]$audioQuality
) {
  $dir = Split-Path -Path $settingsPath -Parent
  if (!(Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }
  [pscustomobject]@{
    ytPath  = $ytPath
    folder  = $folder
    quality = $quality
    cookiesBrowser = $cookiesBrowser
    downloadMode = $downloadMode
    audioFormat = $audioFormat
    audioQuality = $audioQuality
  } | ConvertTo-Json -Depth 3 | Set-Content -Path $settingsPath -Encoding ASCII
}

function Resolve-YtDlpPath([string]$candidate) {
  if ($candidate -and (Test-Path $candidate)) { return $candidate }
  $default = 'C:\yt-dlp\yt-dlp.exe'
  if (Test-Path $default) { return $default }
  $cmd = Get-Command 'yt-dlp.exe','yt-dlp' -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($cmd) { return $cmd.Source }
  return $null
}

function Write-Log([string]$path, [string]$message) {
  if (-not $path) { return }
  try { Add-Content -Path $path -Value $message -Encoding UTF8 } catch { }
}

function Test-PeHeader([string]$path) {
  try {
    $bytes = Get-Content -Path $path -Encoding Byte -TotalCount 2
    return ($bytes.Length -eq 2 -and $bytes[0] -eq 0x4D -and $bytes[1] -eq 0x5A)
  } catch {
    return $false
  }
}

function Move-FileWithRetry([string]$source, [string]$destination, [int]$attempts = 3) {
  for ($i = 1; $i -le $attempts; $i++) {
    try {
      Move-Item -Path $source -Destination $destination -Force
      return $true
    } catch {
      $msg = $_.Exception.Message
      Write-Log $script:updateLogPath ("Move attempt $i/$attempts failed: $msg")
      Start-Sleep -Milliseconds 400
    }
  }
  return $false
}

function Get-FfmpegState([string]$ytPath) {
  $ytDir = if ($ytPath) { Split-Path -Path $ytPath -Parent } else { $null }
  if ($ytDir) {
    $localFfmpeg = Join-Path $ytDir 'ffmpeg.exe'
    $localFfprobe = Join-Path $ytDir 'ffprobe.exe'
    if (Test-Path $localFfmpeg) {
      $ffprobeFound = Test-Path $localFfprobe
      return [pscustomobject]@{
        Found  = $true
        Path   = $localFfmpeg
        Dir    = $ytDir
        Source = 'local'
        FfprobeFound  = $ffprobeFound
        FfprobePath   = if ($ffprobeFound) { $localFfprobe } else { $null }
        FfprobeSource = if ($ffprobeFound) { 'local' } else { 'none' }
      }
    }
  }

  $cmd = Get-Command 'ffmpeg.exe','ffmpeg' -ErrorAction SilentlyContinue | Select-Object -First 1
  $cmdFfprobe = Get-Command 'ffprobe.exe','ffprobe' -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($cmd) {
    return [pscustomobject]@{
      Found  = $true
      Path   = $cmd.Source
      Dir    = Split-Path -Path $cmd.Source -Parent
      Source = 'path'
      FfprobeFound  = [bool]$cmdFfprobe
      FfprobePath   = if ($cmdFfprobe) { $cmdFfprobe.Source } else { $null }
      FfprobeSource = if ($cmdFfprobe) { 'path' } else { 'none' }
    }
  }

  return [pscustomobject]@{
    Found  = $false
    Path   = $null
    Dir    = $ytDir
    Source = 'none'
    FfprobeFound  = [bool]$cmdFfprobe
    FfprobePath   = if ($cmdFfprobe) { $cmdFfprobe.Source } else { $null }
    FfprobeSource = if ($cmdFfprobe) { 'path' } else { 'none' }
  }
}

function Install-FfmpegPortable([string]$targetFolder) {
  if (-not $targetFolder -or -not (Test-Path $targetFolder)) {
    return [pscustomobject]@{ Success = $false; Message = 'Pasta de destino invalida.' }
  }
  $url = 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip'
  $zipPath = Join-Path $env:TEMP ("ffmpeg-{0}.zip" -f ([guid]::NewGuid().ToString('N')))
  $extractDir = Join-Path $env:TEMP ("ffmpeg-{0}" -f ([guid]::NewGuid().ToString('N')))

  try {
    Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing -TimeoutSec 60
    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

    $ffmpegExe = Get-ChildItem -Path $extractDir -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
    $ffprobeExe = Get-ChildItem -Path $extractDir -Recurse -Filter 'ffprobe.exe' | Select-Object -First 1
    if (-not $ffmpegExe -or -not $ffprobeExe) {
      return [pscustomobject]@{ Success = $false; Message = 'ffmpeg.exe e/ou ffprobe.exe nao encontrados no pacote.' }
    }

    Copy-Item -Path $ffmpegExe.FullName -Destination (Join-Path $targetFolder 'ffmpeg.exe') -Force
    Copy-Item -Path $ffprobeExe.FullName -Destination (Join-Path $targetFolder 'ffprobe.exe') -Force

    try { Unblock-File -Path (Join-Path $targetFolder 'ffmpeg.exe') -ErrorAction SilentlyContinue } catch { }
    try { Unblock-File -Path (Join-Path $targetFolder 'ffprobe.exe') -ErrorAction SilentlyContinue } catch { }

    return [pscustomobject]@{ Success = $true; Message = 'ok' }
  } catch {
    return [pscustomobject]@{ Success = $false; Message = $_.Exception.Message }
  } finally {
    try { if (Test-Path $zipPath) { Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue } } catch { }
    try { if (Test-Path $extractDir) { Remove-Item -Path $extractDir -Recurse -Force -ErrorAction SilentlyContinue } } catch { }
  }
}

function Get-JsRuntimeInfo {
  $candidates = @(
    @{ Name = 'node'; Command = @('node.exe', 'node') },
    @{ Name = 'deno'; Command = @('deno.exe', 'deno') },
    @{ Name = 'bun'; Command = @('bun.exe', 'bun') }
  )

  foreach ($c in $candidates) {
    $cmd = Get-Command $c.Command -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) {
      return [pscustomobject]@{
        Name = $c.Name
        Args = @('--js-runtimes', $c.Name)
      }
    }
  }

  return [pscustomobject]@{
    Name = 'none'
    Args = @()
  }
}

$settings = Get-Settings
$resolvedYt = Resolve-YtDlpPath $settings.ytPath
$script:closeAfterCancel = $false
$script:lastLogPath = $null

$form = New-Object System.Windows.Forms.Form
$form.Text = 'YT-DLP Downloader - PowerPoint Ready'
$form.Size = New-Object System.Drawing.Size(760,560)
$form.StartPosition = 'CenterScreen'
$form.AutoScaleMode = [System.Windows.Forms.AutoScaleMode]::Dpi

$menu = New-Object System.Windows.Forms.MenuStrip
$menuAbout = New-Object System.Windows.Forms.ToolStripMenuItem
$menuAbout.Text = 'Sobre'
$menuAbout.Add_Click({
  $aboutText = @(
    'Desenvolvido por Wisley Vilela',
    'https://github.com/Wisleyv',
    'Repositorio: media_download',
    'Licenca: MIT'
  ) -join "`r`n"
  [System.Windows.Forms.MessageBox]::Show($aboutText, 'Sobre') | Out-Null
})
$menu.Items.Add($menuAbout) | Out-Null
$form.MainMenuStrip = $menu
$form.Controls.Add($menu)
$topOffset = $menu.PreferredSize.Height

$lbl1 = New-Object System.Windows.Forms.Label
$lbl1.Text = 'Cole URLs (uma por linha):'
$lbl1.Location = [System.Drawing.Point]::new(10, 10 + $topOffset)
$lbl1.AutoSize = $true
$form.Controls.Add($lbl1)

$txtUrls = New-Object System.Windows.Forms.TextBox
$txtUrls.Multiline = $true
$txtUrls.ScrollBars = 'Vertical'
$txtUrls.Size = '720,200'
$txtUrls.Location = [System.Drawing.Point]::new(10, 30 + $topOffset)
$form.Controls.Add($txtUrls)

$lblYt = New-Object System.Windows.Forms.Label
$lblYt.Text = 'yt-dlp.exe:'
$lblYt.Location = [System.Drawing.Point]::new(10, 240 + $topOffset)
$lblYt.AutoSize = $true
$form.Controls.Add($lblYt)

$txtYt = New-Object System.Windows.Forms.TextBox
$txtYt.Size = '520,25'
$txtYt.Location = [System.Drawing.Point]::new(90, 238 + $topOffset)
if ($settings.ytPath) { $txtYt.Text = $settings.ytPath } elseif ($resolvedYt) { $txtYt.Text = $resolvedYt }
$form.Controls.Add($txtYt)

$btnYt = New-Object System.Windows.Forms.Button
$btnYt.Text = 'Procurar'
$btnYt.Location = [System.Drawing.Point]::new(620, 236 + $topOffset)
$btnYt.Size = '90,27'
$form.Controls.Add($btnYt)

$btnUpdate = New-Object System.Windows.Forms.Button
$btnUpdate.Text = 'Baixar/Atualizar'
$btnUpdate.Location = [System.Drawing.Point]::new(620, 236 + $topOffset)
$btnUpdate.Size = '130,27'
$form.Controls.Add($btnUpdate)

$btnFolder = New-Object System.Windows.Forms.Button
$btnFolder.Text = 'Escolher Pasta'
$btnFolder.Location = [System.Drawing.Point]::new(10, 270 + $topOffset)
$btnFolder.AutoSize = $true
$btnFolder.AutoSizeMode = [System.Windows.Forms.AutoSizeMode]::GrowAndShrink
$form.Controls.Add($btnFolder)

$txtFolder = New-Object System.Windows.Forms.TextBox
$txtFolder.Size = '600,25'
$txtFolder.Location = [System.Drawing.Point]::new(130, 270 + $topOffset)
if ($settings.folder) { $txtFolder.Text = $settings.folder }
$form.Controls.Add($txtFolder)

$grpMode = New-Object System.Windows.Forms.GroupBox
$grpMode.Text = 'Tipo de download'
$grpMode.Size = '220,70'
$grpMode.Location = [System.Drawing.Point]::new(10, 305 + $topOffset)
$form.Controls.Add($grpMode)

$rbModeVideo = New-Object System.Windows.Forms.RadioButton
$rbModeVideo.Text = 'Vídeo'
$rbModeVideo.AutoSize = $true
$rbModeVideo.Location = '10,20'
$rbModeVideo.Checked = $true
$grpMode.Controls.Add($rbModeVideo)

$rbModeAudio = New-Object System.Windows.Forms.RadioButton
$rbModeAudio.Text = 'Áudio'
$rbModeAudio.AutoSize = $true
$rbModeAudio.Location = '10,42'
$grpMode.Controls.Add($rbModeAudio)

if ($settings.downloadMode -and $settings.downloadMode.ToString().ToLower() -eq 'audio') {
  $rbModeAudio.Checked = $true
  $rbModeVideo.Checked = $false
}

$grp = New-Object System.Windows.Forms.GroupBox
$grp.Text = 'Qualidade'
$grp.Size = '220,70'
$grp.Location = [System.Drawing.Point]::new(10, $grpMode.Bottom + 8)
$form.Controls.Add($grp)

$rb720 = New-Object System.Windows.Forms.RadioButton
$rb720.Text = '720p (recomendado)'
$rb720.AutoSize = $true
$rb720.Location = '10,20'
$rb720.Checked = $true
$grp.Controls.Add($rb720)

$rb1080 = New-Object System.Windows.Forms.RadioButton
$rb1080.Text = '1080p'
$rb1080.AutoSize = $true
$rb1080.Location = '10,42'
$grp.Controls.Add($rb1080)
if ($settings.quality -eq '1080') { $rb1080.Checked = $true; $rb720.Checked = $false }

$lblCookies = New-Object System.Windows.Forms.Label
$lblCookies.Text = 'Cookies do navegador:'
$lblCookies.AutoSize = $true
$form.Controls.Add($lblCookies)

$cmbCookies = New-Object System.Windows.Forms.ComboBox
$cmbCookies.DropDownStyle = 'DropDownList'
[void]$cmbCookies.Items.AddRange(@('Nenhum', 'chrome', 'edge', 'firefox', 'brave'))
$cmbCookies.SelectedIndex = 0
if ($settings.cookiesBrowser) {
  $match = $cmbCookies.Items | Where-Object { $_.ToString().ToLower() -eq $settings.cookiesBrowser.ToString().ToLower() } | Select-Object -First 1
  if ($match) { $cmbCookies.SelectedItem = $match }
}
$form.Controls.Add($cmbCookies)

$lblAudioFormat = New-Object System.Windows.Forms.Label
$lblAudioFormat.Text = 'Formato de áudio:'
$lblAudioFormat.AutoSize = $true
$form.Controls.Add($lblAudioFormat)

$cmbAudioFormat = New-Object System.Windows.Forms.ComboBox
$cmbAudioFormat.DropDownStyle = 'DropDownList'
[void]$cmbAudioFormat.Items.AddRange(@('mp3', 'wav', 'flac'))
$cmbAudioFormat.SelectedIndex = 0
if ($settings.audioFormat) {
  $match = $cmbAudioFormat.Items | Where-Object { $_.ToString().ToLower() -eq $settings.audioFormat.ToString().ToLower() } | Select-Object -First 1
  if ($match) { $cmbAudioFormat.SelectedItem = $match }
}
$form.Controls.Add($cmbAudioFormat)

$grpAudioQuality = New-Object System.Windows.Forms.GroupBox
$grpAudioQuality.Text = 'Qualidade do áudio'
$grpAudioQuality.Size = '220,92'
$form.Controls.Add($grpAudioQuality)

$rbAudioLow = New-Object System.Windows.Forms.RadioButton
$rbAudioLow.Text = 'Baixa'
$rbAudioLow.AutoSize = $true
$rbAudioLow.Location = '10,20'
$grpAudioQuality.Controls.Add($rbAudioLow)

$rbAudioStandard = New-Object System.Windows.Forms.RadioButton
$rbAudioStandard.Text = 'Padrão (recomendado)'
$rbAudioStandard.AutoSize = $true
$rbAudioStandard.Location = '10,42'
$rbAudioStandard.Checked = $true
$grpAudioQuality.Controls.Add($rbAudioStandard)

$rbAudioHq = New-Object System.Windows.Forms.RadioButton
$rbAudioHq.Text = 'HQ'
$rbAudioHq.AutoSize = $true
$rbAudioHq.Location = '10,64'
$grpAudioQuality.Controls.Add($rbAudioHq)

if ($settings.audioQuality) {
  switch ($settings.audioQuality.ToString().ToLower()) {
    'low' { $rbAudioLow.Checked = $true; $rbAudioStandard.Checked = $false }
    'hq' { $rbAudioHq.Checked = $true; $rbAudioStandard.Checked = $false }
    default { }
  }
}

$chkShowWarnings = New-Object System.Windows.Forms.CheckBox
$chkShowWarnings.Text = 'Mostrar avisos (avancado)'
$chkShowWarnings.AutoSize = $true
$chkShowWarnings.Location = [System.Drawing.Point]::new(10, 380 + $topOffset)
$form.Controls.Add($chkShowWarnings)

$progress = New-Object System.Windows.Forms.ProgressBar
$progress.Location = [System.Drawing.Point]::new(10, 385 + $topOffset)
$progress.Size = '720,24'
$progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
$form.Controls.Add($progress)

$status = New-Object System.Windows.Forms.Label
$status.Location = [System.Drawing.Point]::new(10, 415 + $topOffset)
$status.Size = '720,40'
$status.Text = 'Pronto.'
$form.Controls.Add($status)

$btnStart = New-Object System.Windows.Forms.Button
$btnStart.Text = 'Baixar Tudo'
$btnStart.Location = [System.Drawing.Point]::new(10, 465 + $topOffset)
$btnStart.Size = '120,35'
$form.Controls.Add($btnStart)

$btnCancel = New-Object System.Windows.Forms.Button
$btnCancel.Text = 'Cancelar'
$btnCancel.Location = [System.Drawing.Point]::new(140, 465 + $topOffset)
$btnCancel.Size = '120,35'
$btnCancel.Enabled = $false
$form.Controls.Add($btnCancel)

$btnClose = New-Object System.Windows.Forms.Button
$btnClose.Text = 'Fechar'
$btnClose.Location = [System.Drawing.Point]::new(610, 465 + $topOffset)
$btnClose.Size = '120,35'
$btnClose.Add_Click({
  try { $form.Close() } catch [System.Management.Automation.PipelineStoppedException] { }
})
$form.Controls.Add($btnClose)

function Update-Layout {
  $form.SuspendLayout()
  $grpMode.SuspendLayout()
  $grp.SuspendLayout()
  $grpAudioQuality.SuspendLayout()

  $txtUrls.Width = $form.ClientSize.Width - 20
  $btnUpdate.Location = [System.Drawing.Point]::new($form.ClientSize.Width - $btnUpdate.Width - 10, $btnUpdate.Location.Y)
  $btnYt.Location = [System.Drawing.Point]::new($btnUpdate.Left - $btnYt.Width - 10, $btnYt.Location.Y)
  $txtYt.Width = $btnYt.Left - $txtYt.Left - 10

  $btnFolder.Location = [System.Drawing.Point]::new(10, $btnFolder.Location.Y)
  $txtFolder.Location = [System.Drawing.Point]::new($btnFolder.Right + 10, $btnFolder.Top)
  $txtFolder.Width = $form.ClientSize.Width - $txtFolder.Left - 10

  $baseY = $txtFolder.Bottom + 10
  $grpMode.Location = [System.Drawing.Point]::new(10, $baseY)
  $rbModeVideo.Location = [System.Drawing.Point]::new(10, 20)
  $rbModeAudio.Location = [System.Drawing.Point]::new(10, $rbModeVideo.Location.Y + $rbModeVideo.PreferredSize.Height + 6)
  $grpMode.Height = $rbModeAudio.Location.Y + $rbModeAudio.PreferredSize.Height + 10

  $grp.Location = [System.Drawing.Point]::new(10, $grpMode.Bottom + 8)

  $rb720.Location = [System.Drawing.Point]::new(10, 20)
  $rb1080.Location = [System.Drawing.Point]::new(10, $rb720.Location.Y + $rb720.PreferredSize.Height + 6)
  $grp.Height = $rb1080.Location.Y + $rb1080.PreferredSize.Height + 10

  $rightX = $grp.Right + 20
  $lblCookies.Location = [System.Drawing.Point]::new($rightX, $grpMode.Top + 6)
  $cmbCookies.Location = [System.Drawing.Point]::new($rightX, $lblCookies.Bottom + 4)
  $cmbCookies.Width = $form.ClientSize.Width - $cmbCookies.Left - 10
  if ($cmbCookies.Width -lt 180) { $cmbCookies.Width = 180 }

  $lblAudioFormat.Location = [System.Drawing.Point]::new($rightX, $cmbCookies.Bottom + 10)
  $cmbAudioFormat.Location = [System.Drawing.Point]::new($rightX, $lblAudioFormat.Bottom + 4)
  $cmbAudioFormat.Width = $cmbCookies.Width

  $grpAudioQuality.Location = [System.Drawing.Point]::new($rightX, $cmbAudioFormat.Bottom + 10)
  $grpAudioQuality.Width = $cmbAudioFormat.Width
  $rbAudioLow.Location = [System.Drawing.Point]::new(10, 20)
  $rbAudioStandard.Location = [System.Drawing.Point]::new(10, $rbAudioLow.Location.Y + $rbAudioLow.PreferredSize.Height + 6)
  $rbAudioHq.Location = [System.Drawing.Point]::new(10, $rbAudioStandard.Location.Y + $rbAudioStandard.PreferredSize.Height + 6)
  $grpAudioQuality.Height = $rbAudioHq.Location.Y + $rbAudioHq.PreferredSize.Height + 10

  $panelBottom = [Math]::Max($grp.Bottom, $cmbCookies.Bottom)
  $panelBottom = [Math]::Max($panelBottom, $cmbAudioFormat.Bottom)
  $panelBottom = [Math]::Max($panelBottom, $grpAudioQuality.Bottom)
  $chkShowWarnings.Location = [System.Drawing.Point]::new(10, $panelBottom + 6)

  $progress.Location = [System.Drawing.Point]::new(10, $chkShowWarnings.Bottom + 10)
  $progress.Width = $form.ClientSize.Width - 20
  $status.Location = [System.Drawing.Point]::new(10, $progress.Bottom + 6)
  $status.Width = $form.ClientSize.Width - 20
  $btnStart.Location = [System.Drawing.Point]::new(10, $status.Bottom + 8)
  $btnCancel.Location = [System.Drawing.Point]::new(140, $status.Bottom + 8)
  $btnClose.Location = [System.Drawing.Point]::new($form.ClientSize.Width - $btnClose.Width - 10, $status.Bottom + 8)

  $requiredHeight = $btnStart.Bottom + 20
  if ($form.ClientSize.Height -lt $requiredHeight) {
    $form.ClientSize = New-Object System.Drawing.Size($form.ClientSize.Width, $requiredHeight)
  }

  $grpAudioQuality.ResumeLayout($false)
  $grp.ResumeLayout($false)
  $grpMode.ResumeLayout($false)
  $form.ResumeLayout($false)
}

Update-Layout
$form.Add_Shown({ Update-Layout })
$form.Add_Resize({ Update-Layout })

function Apply-DownloadModeState {
  $isAudio = $rbModeAudio.Checked
  $grpMode.Enabled = $true
  $rbModeVideo.Enabled = $true
  $rbModeAudio.Enabled = $true

  $grp.Enabled = -not $isAudio
  $rb720.Enabled = -not $isAudio
  $rb1080.Enabled = -not $isAudio

  $lblAudioFormat.Enabled = $isAudio
  $cmbAudioFormat.Enabled = $isAudio
  $grpAudioQuality.Enabled = $isAudio
  $rbAudioLow.Enabled = $isAudio
  $rbAudioStandard.Enabled = $isAudio
  $rbAudioHq.Enabled = $isAudio
}

$rbModeVideo.Add_CheckedChanged({ if ($rbModeVideo.Checked) { Apply-DownloadModeState } })
$rbModeAudio.Add_CheckedChanged({ if ($rbModeAudio.Checked) { Apply-DownloadModeState } })
Apply-DownloadModeState

function Set-UiEnabled([bool]$enabled) {
  $txtUrls.Enabled = $enabled
  $btnFolder.Enabled = $enabled
  $txtFolder.Enabled = $enabled
  $grpMode.Enabled = $enabled
  $rbModeVideo.Enabled = $enabled
  $rbModeAudio.Enabled = $enabled
  $lblAudioFormat.Enabled = $enabled
  $cmbAudioFormat.Enabled = $enabled
  $grpAudioQuality.Enabled = $enabled
  $rbAudioLow.Enabled = $enabled
  $rbAudioStandard.Enabled = $enabled
  $rbAudioHq.Enabled = $enabled
  $grp.Enabled = $enabled
  $rb720.Enabled = $enabled
  $rb1080.Enabled = $enabled
  $cmbCookies.Enabled = $enabled
  $chkShowWarnings.Enabled = $enabled
  $btnStart.Enabled = $enabled
  $btnYt.Enabled = $enabled
  $btnUpdate.Enabled = $enabled
  $txtYt.Enabled = $enabled

  if ($enabled) { Apply-DownloadModeState }
}

function Join-ArgsList([string[]]$argList) {
  $escaped = foreach ($a in $argList) {
    if ($a -match '[\s"]') {
      '"' + ($a -replace '"', '\"') + '"'
    } else {
      $a
    }
  }
  return ($escaped -join ' ')
}

function Get-LatestYtDlpAssetUrl {
  $api = 'https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest'
  try {
    $headers = @{ 'User-Agent' = 'yt-gui'; 'Accept' = 'application/vnd.github+json' }
    $resp = Invoke-RestMethod -Uri $api -Headers $headers -Method Get -TimeoutSec 15
    $asset = $resp.assets | Where-Object { $_.name -eq 'yt-dlp.exe' } | Select-Object -First 1
    if ($asset -and $asset.browser_download_url) { return $asset.browser_download_url }
  } catch {
  }
  return $null
}

function Start-YtDlpUpdate {
  if ($script:timer.Enabled) { [System.Windows.Forms.MessageBox]::Show('Ha downloads em andamento. Aguarde o termino.'); return }
  if ($script:updateInProgress) { return }

  $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
  $dlg.Description = 'Selecione a pasta para salvar o yt-dlp.exe'
  $defaultPath = $null
  if ($txtYt.Text -and (Test-Path $txtYt.Text)) {
    $defaultPath = Split-Path -Path $txtYt.Text -Parent
  } elseif ($txtFolder.Text -and (Test-Path $txtFolder.Text)) {
    $defaultPath = $txtFolder.Text
  }
  if ($defaultPath) { $dlg.SelectedPath = $defaultPath }
  if ($dlg.ShowDialog() -ne 'OK') { return }

  $targetFolder = $dlg.SelectedPath
  if (!(Test-Path $targetFolder)) { [System.Windows.Forms.MessageBox]::Show('Escolha uma pasta valida.'); return }

  $status.Text = 'Buscando ultima versao...'
  $form.Refresh()

  $url = Get-LatestYtDlpAssetUrl
  if (-not $url) { [System.Windows.Forms.MessageBox]::Show('Nao foi possivel obter a ultima versao do yt-dlp.'); return }

  $curl = Get-Command 'curl.exe' -ErrorAction SilentlyContinue
  if (-not $curl) { [System.Windows.Forms.MessageBox]::Show('curl.exe nao encontrado. Atualize manualmente o yt-dlp.'); return }

  $script:updateInProgress = $true
  Set-UiEnabled $false
  $btnCancel.Enabled = $false
  $btnClose.Enabled = $false

  $script:updateTargetPath = Join-Path $targetFolder 'yt-dlp.exe'
  $script:updateTempFile = Join-Path $env:TEMP ("yt-dlp-{0}.exe" -f ([guid]::NewGuid().ToString('N')))
  $script:updateOutFile = Join-Path $env:TEMP ("yt-gui-update-out-{0}.txt" -f ([guid]::NewGuid().ToString('N')))
  $script:updateErrFile = Join-Path $env:TEMP ("yt-gui-update-err-{0}.txt" -f ([guid]::NewGuid().ToString('N')))
  $script:updateLogPath = Join-Path $targetFolder ("yt-gui-update-log-{0}.txt" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

  Write-Log $script:updateLogPath ("yt-dlp update - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`r`nTarget: $($script:updateTargetPath)`r`nUrl: $url`r`n")

  $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Marquee
  $progress.MarqueeAnimationSpeed = 30
  $progress.Value = 0
  $status.Text = 'Baixando yt-dlp...'

  $argsList = @('-L', '--fail', '-o', $script:updateTempFile, $url)
  $argString = Join-ArgsList $argsList
  try {
    $script:updateProcess = Start-Process -FilePath $curl.Source -ArgumentList $argString -PassThru -NoNewWindow -RedirectStandardOutput $script:updateOutFile -RedirectStandardError $script:updateErrFile
  } catch {
    $script:updateInProgress = $false
    Set-UiEnabled $true
    $btnClose.Enabled = $true
    $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
    $errMsg = "Falha ao iniciar download: $($_.Exception.Message)"
    Write-Log $script:updateLogPath $errMsg
    [System.Windows.Forms.MessageBox]::Show($errMsg) | Out-Null
    return
  }

  $script:updateTimer.Start()
}

$script:downloadQueue = @()
$script:currentIndex = 0
$script:currentProcess = $null
$script:currentOutFile = $null
$script:currentErrFile = $null
$script:currentArgsList = @()
$script:results = New-Object System.Collections.Generic.List[object]
$script:cancelRequested = $false
$script:logPath = $null
$script:baseArgs = @()
$script:ytPath = $null
$script:outputTemplate = $null
$script:updateInProgress = $false
$script:updateProcess = $null
$script:updateTempFile = $null
$script:updateTargetPath = $null
$script:updateOutFile = $null
$script:updateErrFile = $null
$script:updateLogPath = $null

$script:ffmpegInstallInProgress = $false
$script:ffmpegInstallProcess = $null
$script:ffmpegInstallOutFile = $null
$script:ffmpegInstallErrFile = $null
$script:ffmpegInstallScriptPath = $null
$script:ffmpegInstallZipPath = $null
$script:ffmpegInstallExtractDir = $null
$script:ffmpegInstallTargetFolder = $null
$script:pendingStartContext = $null

function Start-DownloadsFromContext([pscustomobject]$ctx) {
  if (-not $ctx) { return }

  $folder = $ctx.Folder
  $urls = @($ctx.Urls)
  $ytPath = $ctx.YtPath
  $downloadMode = $ctx.DownloadMode
  $audioFormat = $ctx.AudioFormat
  $audioQuality = $ctx.AudioQuality
  $cookiesBrowser = $ctx.CookiesBrowser

  $ffmpegState = Get-FfmpegState $ytPath
  $forceNoFfmpeg = $ctx.ForceNoFfmpeg -eq $true
  $useFfmpeg = $false
  $ffmpegLocation = $null

  if (-not $forceNoFfmpeg) {
    if ($downloadMode -eq 'audio') {
      $useFfmpeg = $ffmpegState.Found -and $ffmpegState.FfprobeFound
    } else {
      $useFfmpeg = $ffmpegState.Found
    }
  }

  if ($downloadMode -eq 'audio' -and -not $useFfmpeg) {
    $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
    Set-UiEnabled $true
    $btnCancel.Enabled = $false
    $btnClose.Enabled = $true
    $status.Text = 'FFmpeg/FFprobe nao encontrados.'
    [System.Windows.Forms.MessageBox]::Show('Para baixar audio, e necessario FFmpeg + FFprobe. Baixe o FFmpeg portatil e tente novamente.') | Out-Null
    return
  }

  if ($useFfmpeg) {
    if ($downloadMode -eq 'audio') {
      $ffprobeDir = if ($ffmpegState.FfprobePath) { Split-Path -Path $ffmpegState.FfprobePath -Parent } else { $null }
      if ($ffprobeDir -and $ffprobeDir -eq $ffmpegState.Dir) { $ffmpegLocation = $ffmpegState.Dir }
    } else {
      $ffmpegLocation = $ffmpegState.Dir
    }
  }

  $jsRuntime = Get-JsRuntimeInfo
  $script:showWarnings = $ctx.ShowWarnings -eq $true

  $fmt = $null
  $height = $ctx.Height
  if ($downloadMode -eq 'audio') {
    $fmt = 'ba/b'
  } else {
    if ($useFfmpeg) {
      $fmt = "bv*[height<=$height][ext=mp4]+ba[ext=m4a]/b[height<=$height][ext=mp4]/best[ext=mp4]"
    } else {
      $fmt = "b[height<=$height][ext=mp4]/b[height<=$height]/best"
    }
  }

  $quality = $ctx.Quality
  Save-Settings -ytPath $ytPath -folder $folder -quality $quality -cookiesBrowser $cookiesBrowser -downloadMode $downloadMode -audioFormat $audioFormat -audioQuality $audioQuality

  $progress.Minimum = 0
  $progress.Maximum = $urls.Count
  $progress.Value = 0
  $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
  $status.Text = 'Iniciando...'

  Set-UiEnabled $false
  $btnCancel.Enabled = $true
  $btnClose.Enabled = $false

  $outputTemplate = Join-Path -Path $folder -ChildPath '%(title)s.%(ext)s'
  $logPath = Join-Path -Path $folder -ChildPath ("yt-gui-log-{0}.txt" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

  $baseArgs = @('-f', $fmt)
  if ($downloadMode -eq 'audio') {
    $baseArgs += '-x'
    $baseArgs += @('--audio-format', $audioFormat)
    if ($audioFormat -eq 'mp3') {
      $aq = if ($audioQuality -eq 'low') { '7' } elseif ($audioQuality -eq 'hq') { '0' } else { '5' }
      $baseArgs += @('--audio-quality', $aq)
    }
  }

  $baseArgs += $jsRuntime.Args
  if ($cookiesBrowser -and $cookiesBrowser -ne 'Nenhum') { $baseArgs += @('--cookies-from-browser', $cookiesBrowser) }
  if ($useFfmpeg -and $ffmpegLocation) { $baseArgs += @('--ffmpeg-location', $ffmpegLocation) }
  if ($downloadMode -eq 'video' -and $useFfmpeg) { $baseArgs += @('--merge-output-format', 'mp4') }
  if (-not $ctx.ShowWarnings) { $baseArgs += '--no-warnings' }
  $baseArgs += @('--restrict-filenames', '--newline', '-o', $outputTemplate)

  $ffmpegSource = if ($forceNoFfmpeg) { 'disabled' } else { $ffmpegState.Source }
  try {
    "yt-gui log - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" | Set-Content -Path $logPath -Encoding UTF8
    $script:lastLogPath = $logPath
    $startInfo = @(
      "Start: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
      "YtPath: $ytPath",
      "DownloadMode: $downloadMode",
      "AudioFormat: $audioFormat",
      "AudioQuality: $audioQuality",
      "Ffmpeg: $($useFfmpeg)",
      "FfmpegSource: $ffmpegSource",
      "FfmpegLocation: $ffmpegLocation",
      "JsRuntime: $($jsRuntime.Name)",
      "CookiesBrowser: $cookiesBrowser",
      "ShowWarnings: $($ctx.ShowWarnings)",
      "Urls: $($urls.Count)",
      "Output: $outputTemplate",
      "Args: $($baseArgs -join ' ')",
      ''
    ) -join "`r`n"
    Write-Log $logPath $startInfo
  } catch {
    $fallback = Join-Path -Path $env:TEMP -ChildPath ("yt-gui-log-{0}.txt" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
    try {
      "yt-gui log - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" | Set-Content -Path $fallback -Encoding UTF8
      $script:lastLogPath = $fallback
      $logPath = $fallback
      Write-Log $logPath "Log gravado em %TEMP%: $fallback"
    } catch {
      $script:lastLogPath = $null
      $logPath = $null
      [System.Windows.Forms.MessageBox]::Show("Nao foi possivel criar o log no destino nem em %TEMP%.") | Out-Null
    }
  }

  $script:downloadQueue = $urls
  $script:currentIndex = 0
  $script:results = New-Object System.Collections.Generic.List[object]
  $script:cancelRequested = $false
  $script:ytPath = $ytPath
  $script:outputTemplate = $outputTemplate
  $script:baseArgs = $baseArgs
  $script:logPath = $logPath
  $script:timer.Start()
}

function Start-DownloadProcess([int]$index) {
  $u = $script:downloadQueue[$index]
  $script:currentArgsList = @($script:baseArgs + $u)
  $status.Text = "Baixando $($index + 1) de $($script:downloadQueue.Count): $u"
  $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Marquee
  $progress.MarqueeAnimationSpeed = 30

  $script:currentOutFile = Join-Path $env:TEMP ("yt-gui-out-{0}.txt" -f ([guid]::NewGuid().ToString('N')))
  $script:currentErrFile = Join-Path $env:TEMP ("yt-gui-err-{0}.txt" -f ([guid]::NewGuid().ToString('N')))
  $argString = Join-ArgsList $script:currentArgsList
  if (-not $argString) { $argString = '[empty]' }

  Write-Log $script:logPath ("-----`r`nURL: $u`r`nArgs: $argString")
  try {
    $script:currentProcess = Start-Process -FilePath $script:ytPath -ArgumentList $argString -PassThru -NoNewWindow -RedirectStandardOutput $script:currentOutFile -RedirectStandardError $script:currentErrFile
  } catch {
    $script:currentProcess = $null
    $errMsg = "EXCEPTION: $($_.Exception.Message)" + "`r`n" + ($_ | Out-String)
    Write-Log $script:logPath ("ExitCode: 1`r`n$errMsg`r`n")
    $script:results.Add([pscustomobject]@{
      Url      = $u
      Success  = $false
      ExitCode = 1
      Output   = $errMsg
    })
    $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
    $progress.Value = $index + 1
    $status.Text = "Falha ao baixar $($index + 1) de $($script:downloadQueue.Count)."
    $script:currentIndex++
  }
}

function Handle-ProcessExit {
  $exitCode = $script:currentProcess.ExitCode
  $exitCodeText = if ($null -eq $exitCode -or $exitCode -eq '') { '' } else { $exitCode.ToString() }
  $exitKnown = -not [string]::IsNullOrWhiteSpace($exitCodeText)
  $out = ''
  $err = ''
  try { if (Test-Path $script:currentOutFile) { $out = Get-Content -Path $script:currentOutFile -Raw } } catch { }
  try { if (Test-Path $script:currentErrFile) { $err = Get-Content -Path $script:currentErrFile -Raw } } catch { }
  $output = ($out + "`r`n" + $err).Trim()
  if (-not $output) {
    $output = 'Sem saida do yt-dlp. Verifique se o executavel inicia, se ha bloqueio do antivirus, ou se o video exige login/cookies.'
  }

  $hasError = $output -match '(?m)^(ERROR:|ERROR |FATAL|Traceback)'
  $success = if ($exitKnown) { $exitCode -eq 0 } else { -not $hasError }
  $recordExitCode = if ($exitKnown) { $exitCode } elseif ($success) { 0 } else { 1 }

  if ($exitKnown) {
    Write-Log $script:logPath ("ExitCode: $exitCodeText`r`n$output`r`n")
  } else {
    Write-Log $script:logPath ("ExitCode: <unknown>`r`n$output`r`n")
  }
  $script:results.Add([pscustomobject]@{
    Url      = $script:downloadQueue[$script:currentIndex]
    Success  = $success
    ExitCode = $recordExitCode
    Output   = $output
  })

  $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
  $progress.Value = $script:currentIndex + 1
  if ($success) {
    $status.Text = "Concluido $($script:currentIndex + 1) de $($script:downloadQueue.Count)."
  } else {
    $status.Text = "Falha ao baixar $($script:currentIndex + 1) de $($script:downloadQueue.Count)."
  }

  try { if ($script:currentOutFile) { Remove-Item -Path $script:currentOutFile -ErrorAction SilentlyContinue } } catch { }
  try { if ($script:currentErrFile) { Remove-Item -Path $script:currentErrFile -ErrorAction SilentlyContinue } } catch { }
  $script:currentOutFile = $null
  $script:currentErrFile = $null
  $script:currentProcess = $null
  $script:currentIndex++
}

function Finish-Run([bool]$cancelled) {
  $script:timer.Stop()
  $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
  Set-UiEnabled $true
  $btnCancel.Enabled = $false
  $btnClose.Enabled = $true

  if ($cancelled) {
    $status.Text = 'Cancelado.'
    Write-Log $script:logPath 'Cancelado pelo usuario.'
    [System.Windows.Forms.MessageBox]::Show('Operacao cancelada.') | Out-Null
    if ($script:closeAfterCancel) { $form.Close() }
    return
  }

  $failures = @($script:results | Where-Object { -not $_.Success })
  if ($failures.Count -gt 0) {
    $status.Text = 'Concluido com erros.'
    $firstFail = $failures | Select-Object -First 1
    $tail = ''
    $showWarnings = $script:showWarnings -eq $true
    if ($showWarnings) {
      $tail = ($firstFail.Output -split "`r?`n" | Where-Object { $_ -ne '' } | Select-Object -Last 8) -join "`r`n"
    } else {
      $errorLines = $firstFail.Output -split "`r?`n" | Where-Object { $_ -match '^(ERROR:|ERROR |FATAL|Traceback)' }
      if ($errorLines.Count -gt 0) {
        $tail = ($errorLines | Select-Object -Last 2) -join "`r`n"
      }
    }
    $message = "Concluido com erros. Falhas: $($failures.Count)."
    if ($tail) { $message += "`r`n`r`nUltimas linhas:`r`n$tail" }
    if ($script:logPath) { $message += "`r`n`r`nLog: $script:logPath" }
    [System.Windows.Forms.MessageBox]::Show($message) | Out-Null
  } else {
    $status.Text = 'Todos os downloads concluidos.'
    [System.Windows.Forms.MessageBox]::Show('Finalizado!') | Out-Null
  }

  if ($script:closeAfterCancel) { $form.Close() }
}

$script:timer = New-Object System.Windows.Forms.Timer
$script:timer.Interval = 500
$script:timer.Add_Tick({
  if ($script:cancelRequested) {
    if ($script:currentProcess -and -not $script:currentProcess.HasExited) {
      try { Stop-Process -Id $script:currentProcess.Id -Force -ErrorAction SilentlyContinue } catch { }
    }
    Finish-Run $true
    return
  }

  if (-not $script:currentProcess) {
    if ($script:currentIndex -ge $script:downloadQueue.Count) {
      Finish-Run $false
      return
    }
    Start-DownloadProcess $script:currentIndex
    return
  }

  if ($script:currentProcess.HasExited) {
    Handle-ProcessExit
  }
})

$script:updateTimer = New-Object System.Windows.Forms.Timer
$script:updateTimer.Interval = 500
$script:updateTimer.Add_Tick({
  if (-not $script:updateInProgress) { $script:updateTimer.Stop(); return }
  if (-not $script:updateProcess) { return }
  if (-not $script:updateProcess.HasExited) { return }

  $script:updateTimer.Stop()
  $exitCode = $script:updateProcess.ExitCode
  $exitCodeText = if ($null -eq $exitCode -or $exitCode -eq '') { '' } else { $exitCode.ToString() }
  $exitKnown = -not [string]::IsNullOrWhiteSpace($exitCodeText)
  $out = ''
  $err = ''
  try { if (Test-Path $script:updateOutFile) { $out = Get-Content -Path $script:updateOutFile -Raw } } catch { }
  try { if (Test-Path $script:updateErrFile) { $err = Get-Content -Path $script:updateErrFile -Raw } } catch { }
  $output = ($out + "`r`n" + $err).Trim()
  if ($output) { Write-Log $script:updateLogPath $output }
  if ($exitKnown) {
    Write-Log $script:updateLogPath ("ExitCode: $exitCodeText")
  } else {
    Write-Log $script:updateLogPath 'ExitCode: <unknown>'
  }

  $tempExists = Test-Path $script:updateTempFile
  $tempSize = $null
  if ($tempExists) {
    try { $tempSize = (Get-Item -Path $script:updateTempFile).Length } catch { }
    Write-Log $script:updateLogPath ("TempFile: $($script:updateTempFile) Size: $tempSize")
  } else {
    Write-Log $script:updateLogPath ("TempFile missing: $($script:updateTempFile)")
  }

  $success = $false
  $errMsg = $null
  $exitOk = $exitKnown -and $exitCode -eq 0
  $exitUnknown = -not $exitKnown
  if (($exitOk -or $exitUnknown) -and $tempExists) {
    if ($tempSize -gt 0 -and (Test-PeHeader $script:updateTempFile)) {
      if ($exitUnknown) { Write-Log $script:updateLogPath 'ExitCode unknown; proceeding because file looks valid.' }
      if (Move-FileWithRetry -source $script:updateTempFile -destination $script:updateTargetPath -attempts 4) {
        $success = $true
        try { Unblock-File -Path $script:updateTargetPath -ErrorAction SilentlyContinue } catch { }
      } else {
        $errMsg = 'Falha ao salvar o yt-dlp.exe. Verifique permissoes ou antivirus.'
      }
    } elseif ($tempSize -gt 0) {
      $errMsg = 'Arquivo baixado parece invalido. Verifique proxy ou antivirus.'
    } else {
      $errMsg = 'Arquivo baixado esta vazio.'
    }
  } elseif (($exitOk -or $exitUnknown) -and -not $tempExists) {
    $errMsg = 'Arquivo temporario nao encontrado apos download. O antivirus pode ter removido o arquivo.'
  } else {
    $errMsg = "Falha ao baixar yt-dlp. ExitCode: $exitCodeText."
  }

  if ($errMsg) { Write-Log $script:updateLogPath $errMsg }

  if ($success) {
    $txtYt.Text = $script:updateTargetPath
    $quality = if ($rb720.Checked) { '720' } else { '1080' }
    $cookiesBrowser = if ($cmbCookies.SelectedItem) { $cmbCookies.SelectedItem.ToString() } else { 'Nenhum' }
    $downloadMode = if ($rbModeAudio.Checked) { 'audio' } else { 'video' }
    $audioFormat = if ($cmbAudioFormat.SelectedItem) { $cmbAudioFormat.SelectedItem.ToString() } else { 'mp3' }
    $audioQuality = if ($rbAudioLow.Checked) { 'low' } elseif ($rbAudioHq.Checked) { 'hq' } else { 'standard' }
    Save-Settings -ytPath $txtYt.Text -folder $txtFolder.Text.Trim() -quality $quality -cookiesBrowser $cookiesBrowser -downloadMode $downloadMode -audioFormat $audioFormat -audioQuality $audioQuality
    $status.Text = 'yt-dlp atualizado.'
    [System.Windows.Forms.MessageBox]::Show('yt-dlp atualizado com sucesso.') | Out-Null
  } else {
    $status.Text = 'Falha na atualizacao.'
    if ($script:updateLogPath) { $errMsg += "`r`nLog: $script:updateLogPath" }
    [System.Windows.Forms.MessageBox]::Show($errMsg) | Out-Null
  }

  try { if ($script:updateOutFile) { Remove-Item -Path $script:updateOutFile -ErrorAction SilentlyContinue } } catch { }
  try { if ($script:updateErrFile) { Remove-Item -Path $script:updateErrFile -ErrorAction SilentlyContinue } } catch { }
  $script:updateOutFile = $null
  $script:updateErrFile = $null
  $script:updateProcess = $null
  $script:updateInProgress = $false
  $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
  Set-UiEnabled $true
  $btnClose.Enabled = $true
})

function Start-FfmpegPortableInstallAsync([string]$targetFolder) {
  if ($script:timer.Enabled) { return }
  if ($script:updateInProgress) { return }
  if ($script:ffmpegInstallInProgress) { return }
  if (-not $script:pendingStartContext) { return }

  if (-not $targetFolder -or -not (Test-Path $targetFolder)) {
    [System.Windows.Forms.MessageBox]::Show('Pasta de destino invalida.') | Out-Null
    return
  }

  $ps = Get-Command 'powershell.exe' -ErrorAction SilentlyContinue
  if (-not $ps) {
    [System.Windows.Forms.MessageBox]::Show('powershell.exe nao encontrado. Nao foi possivel instalar o FFmpeg.') | Out-Null
    return
  }

  $url = 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip'
  $id = [guid]::NewGuid().ToString('N')

  $script:ffmpegInstallTargetFolder = $targetFolder
  $script:ffmpegInstallZipPath = Join-Path $env:TEMP ("ffmpeg-$id.zip")
  $script:ffmpegInstallExtractDir = Join-Path $env:TEMP ("ffmpeg-$id")
  $script:ffmpegInstallScriptPath = Join-Path $env:TEMP ("yt-gui-ffmpeg-install-$id.ps1")
  $script:ffmpegInstallOutFile = Join-Path $env:TEMP ("yt-gui-ffmpeg-out-$id.txt")
  $script:ffmpegInstallErrFile = Join-Path $env:TEMP ("yt-gui-ffmpeg-err-$id.txt")

  $scriptContent = @'
param(
  [Parameter(Mandatory=$true)][string]$TargetFolder,
  [Parameter(Mandatory=$true)][string]$ZipPath,
  [Parameter(Mandatory=$true)][string]$ExtractDir,
  [Parameter(Mandatory=$true)][string]$Url
)

try {
  if (-not (Test-Path $TargetFolder)) { throw 'Pasta de destino invalida.' }
  Invoke-WebRequest -Uri $Url -OutFile $ZipPath -UseBasicParsing -TimeoutSec 120
  Expand-Archive -Path $ZipPath -DestinationPath $ExtractDir -Force

  $ffmpegExe = Get-ChildItem -Path $ExtractDir -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
  $ffprobeExe = Get-ChildItem -Path $ExtractDir -Recurse -Filter 'ffprobe.exe' | Select-Object -First 1
  if (-not $ffmpegExe -or -not $ffprobeExe) { throw 'ffmpeg.exe e/ou ffprobe.exe nao encontrados no pacote.' }

  Copy-Item -Path $ffmpegExe.FullName -Destination (Join-Path $TargetFolder 'ffmpeg.exe') -Force
  Copy-Item -Path $ffprobeExe.FullName -Destination (Join-Path $TargetFolder 'ffprobe.exe') -Force

  try { Unblock-File -Path (Join-Path $TargetFolder 'ffmpeg.exe') -ErrorAction SilentlyContinue } catch { }
  try { Unblock-File -Path (Join-Path $TargetFolder 'ffprobe.exe') -ErrorAction SilentlyContinue } catch { }

  'ok'
  exit 0
} catch {
  $_.Exception.Message
  exit 1
} finally {
  try { if (Test-Path $ZipPath) { Remove-Item -Path $ZipPath -Force -ErrorAction SilentlyContinue } } catch { }
  try { if (Test-Path $ExtractDir) { Remove-Item -Path $ExtractDir -Recurse -Force -ErrorAction SilentlyContinue } } catch { }
}
'@

  try {
    Set-Content -Path $script:ffmpegInstallScriptPath -Value $scriptContent -Encoding UTF8
  } catch {
    [System.Windows.Forms.MessageBox]::Show("Falha ao preparar instalador do FFmpeg: $($_.Exception.Message)") | Out-Null
    return
  }

  $script:ffmpegInstallInProgress = $true

  $status.Text = 'Baixando e instalando FFmpeg...'
  $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Marquee
  $progress.MarqueeAnimationSpeed = 30
  $progress.Value = 0

  Set-UiEnabled $false
  $btnCancel.Enabled = $false
  $btnClose.Enabled = $false

  $argsList = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', $script:ffmpegInstallScriptPath,
    '-TargetFolder', $targetFolder,
    '-ZipPath', $script:ffmpegInstallZipPath,
    '-ExtractDir', $script:ffmpegInstallExtractDir,
    '-Url', $url
  )
  $argString = Join-ArgsList $argsList

  try {
    $script:ffmpegInstallProcess = Start-Process -FilePath $ps.Source -ArgumentList $argString -PassThru -NoNewWindow -RedirectStandardOutput $script:ffmpegInstallOutFile -RedirectStandardError $script:ffmpegInstallErrFile
  } catch {
    $script:ffmpegInstallInProgress = $false
    Set-UiEnabled $true
    $btnClose.Enabled = $true
    $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
    [System.Windows.Forms.MessageBox]::Show("Falha ao iniciar instalacao do FFmpeg: $($_.Exception.Message)") | Out-Null
    return
  }

  $script:ffmpegInstallTimer.Start()
}

$script:ffmpegInstallTimer = New-Object System.Windows.Forms.Timer
$script:ffmpegInstallTimer.Interval = 500
$script:ffmpegInstallTimer.Add_Tick({
  if (-not $script:ffmpegInstallInProgress) { $script:ffmpegInstallTimer.Stop(); return }
  if (-not $script:ffmpegInstallProcess) { return }
  if (-not $script:ffmpegInstallProcess.HasExited) { return }

  $script:ffmpegInstallTimer.Stop()
  $exitCode = $script:ffmpegInstallProcess.ExitCode
  $out = ''
  $err = ''
  try { if (Test-Path $script:ffmpegInstallOutFile) { $out = Get-Content -Path $script:ffmpegInstallOutFile -Raw } } catch { }
  try { if (Test-Path $script:ffmpegInstallErrFile) { $err = Get-Content -Path $script:ffmpegInstallErrFile -Raw } } catch { }
  $output = ($out + "`r`n" + $err).Trim()

  $ffmpegOk = $false
  try {
    $ffmpegOk = (Test-Path (Join-Path $script:ffmpegInstallTargetFolder 'ffmpeg.exe')) -and (Test-Path (Join-Path $script:ffmpegInstallTargetFolder 'ffprobe.exe'))
  } catch { }

  $success = $exitCode -eq 0 -and $ffmpegOk

  try { if ($script:ffmpegInstallOutFile) { Remove-Item -Path $script:ffmpegInstallOutFile -ErrorAction SilentlyContinue } } catch { }
  try { if ($script:ffmpegInstallErrFile) { Remove-Item -Path $script:ffmpegInstallErrFile -ErrorAction SilentlyContinue } } catch { }
  try { if ($script:ffmpegInstallScriptPath) { Remove-Item -Path $script:ffmpegInstallScriptPath -ErrorAction SilentlyContinue } } catch { }

  $script:ffmpegInstallOutFile = $null
  $script:ffmpegInstallErrFile = $null
  $script:ffmpegInstallScriptPath = $null
  $script:ffmpegInstallZipPath = $null
  $script:ffmpegInstallExtractDir = $null
  $script:ffmpegInstallProcess = $null
  $script:ffmpegInstallInProgress = $false

  $ctx = $script:pendingStartContext
  $script:pendingStartContext = $null

  if ($success) {
    $status.Text = 'FFmpeg instalado. Iniciando...'
    $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
    if ($ctx) {
      Start-DownloadsFromContext $ctx
    } else {
      Set-UiEnabled $true
      $btnCancel.Enabled = $false
      $btnClose.Enabled = $true
      $status.Text = 'Pronto.'
    }
    return
  }

  $msg = if ($output) { $output } else { "Falha ao baixar/instalar FFmpeg. ExitCode: $exitCode" }

  if ($ctx -and $ctx.AllowWithoutFfmpeg -eq $true) {
    $res = [System.Windows.Forms.MessageBox]::Show("Falha ao baixar FFmpeg: $msg`r`nContinuar sem FFmpeg?", 'FFmpeg', [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Warning)
    if ($res -eq [System.Windows.Forms.DialogResult]::Yes) {
      $ctx.ForceNoFfmpeg = $true
      Start-DownloadsFromContext $ctx
      return
    }
  } else {
    [System.Windows.Forms.MessageBox]::Show("Falha ao baixar FFmpeg: $msg", 'FFmpeg', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
  }

  $progress.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
  Set-UiEnabled $true
  $btnCancel.Enabled = $false
  $btnClose.Enabled = $true
  $status.Text = 'Pronto.'
})

$btnFolder.Add_Click({
 $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
 if ($dlg.ShowDialog() -eq 'OK') { $txtFolder.Text = $dlg.SelectedPath }
})

$btnYt.Add_Click({
 $dlg = New-Object System.Windows.Forms.OpenFileDialog
 $dlg.Filter = 'yt-dlp.exe|yt-dlp.exe|Executables|*.exe|Todos os arquivos|*.*'
 $dlg.Title = 'Selecione o yt-dlp.exe'
 if ($dlg.ShowDialog() -eq 'OK') { $txtYt.Text = $dlg.FileName }
})

$btnUpdate.Add_Click({
 Start-YtDlpUpdate
})

$btnStart.Add_Click({
 if ($script:timer.Enabled) { return }
 if ($script:updateInProgress) { return }
 if ($script:ffmpegInstallInProgress) { return }

 $folder = $txtFolder.Text.Trim()
 if (!(Test-Path $folder)) { [System.Windows.Forms.MessageBox]::Show('Escolha uma pasta valida.'); return }
 $urls = @($txtUrls.Lines | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
 if ($urls.Count -eq 0) { [System.Windows.Forms.MessageBox]::Show('Cole ao menos uma URL.'); return }

 $ytPath = Resolve-YtDlpPath $txtYt.Text.Trim()
 if (-not $ytPath) {
   [System.Windows.Forms.MessageBox]::Show('yt-dlp.exe nao encontrado. Informe o caminho ou instale e adicione ao PATH.'); return
 }
 $txtYt.Text = $ytPath

 $downloadMode = if ($rbModeAudio.Checked) { 'audio' } else { 'video' }
 $audioFormat = if ($cmbAudioFormat.SelectedItem) { $cmbAudioFormat.SelectedItem.ToString() } else { 'mp3' }
 $audioQuality = if ($rbAudioLow.Checked) { 'low' } elseif ($rbAudioHq.Checked) { 'hq' } else { 'standard' }
 $cookiesBrowser = if ($cmbCookies.SelectedItem) { $cmbCookies.SelectedItem.ToString() } else { 'Nenhum' }
 $height = if ($rb720.Checked) { 720 } else { 1080 }
 $quality = if ($rb720.Checked) { '720' } else { '1080' }

 $ctx = [pscustomobject]@{
   Folder = $folder
   Urls = $urls
   YtPath = $ytPath
   DownloadMode = $downloadMode
   AudioFormat = $audioFormat
   AudioQuality = $audioQuality
   CookiesBrowser = $cookiesBrowser
   ShowWarnings = $chkShowWarnings.Checked
   Height = $height
   Quality = $quality
   ForceNoFfmpeg = $false
   AllowWithoutFfmpeg = ($downloadMode -eq 'video')
 }

 $ffmpegState = Get-FfmpegState $ytPath
 $installTarget = Split-Path -Path $ytPath -Parent

 if ($downloadMode -eq 'audio') {
   if (-not ($ffmpegState.Found -and $ffmpegState.FfprobeFound)) {
     $msg = @(
       'Para baixar apenas o audio (mp3/wav/flac), e necessario FFmpeg + FFprobe.',
       '',
       'Deseja baixar o FFmpeg portatil para a mesma pasta do yt-dlp.exe?'
     ) -join "`r`n"
     $choice = [System.Windows.Forms.MessageBox]::Show($msg, 'FFmpeg', [System.Windows.Forms.MessageBoxButtons]::YesNoCancel, [System.Windows.Forms.MessageBoxIcon]::Information)
     if ($choice -ne [System.Windows.Forms.DialogResult]::Yes) { return }

     $script:pendingStartContext = $ctx
     Start-FfmpegPortableInstallAsync -targetFolder $installTarget
     return
   }

   Start-DownloadsFromContext $ctx
   return
 }

 if (-not $ffmpegState.Found) {
   $msg = @(
     'FFmpeg melhora a qualidade e permite juntar audio+video em um unico MP4.',
     'Sem ele, o download pode sair em qualidade menor.',
     '',
     'Deseja baixar o FFmpeg portatil para a mesma pasta do yt-dlp.exe?'
   ) -join "`r`n"
   $choice = [System.Windows.Forms.MessageBox]::Show($msg, 'FFmpeg', [System.Windows.Forms.MessageBoxButtons]::YesNoCancel, [System.Windows.Forms.MessageBoxIcon]::Information)
   if ($choice -eq [System.Windows.Forms.DialogResult]::Yes) {
     $script:pendingStartContext = $ctx
     Start-FfmpegPortableInstallAsync -targetFolder $installTarget
     return
   } elseif ($choice -eq [System.Windows.Forms.DialogResult]::Cancel) {
     return
   } else {
     $ctx.ForceNoFfmpeg = $true
   }
 }

 Start-DownloadsFromContext $ctx
})

$btnCancel.Add_Click({
 if ($script:timer.Enabled) {
   $btnCancel.Enabled = $false
   $status.Text = 'Cancelando...'
   $script:cancelRequested = $true
 }
})

$form.Add_FormClosing({
 try {
   if ($script:ffmpegInstallInProgress) {
     $resFfmpeg = [System.Windows.Forms.MessageBox]::Show('Ha instalacao do FFmpeg em andamento. Deseja cancelar e sair?', 'Confirmar', [System.Windows.Forms.MessageBoxButtons]::YesNo)
     if ($resFfmpeg -ne [System.Windows.Forms.DialogResult]::Yes) {
       $args[1].Cancel = $true
       return
     }
     try { if ($script:ffmpegInstallProcess -and -not $script:ffmpegInstallProcess.HasExited) { Stop-Process -Id $script:ffmpegInstallProcess.Id -Force -ErrorAction SilentlyContinue } } catch { }
     $script:ffmpegInstallInProgress = $false
     $script:pendingStartContext = $null
     $script:ffmpegInstallTimer.Stop()
   }
   if ($script:updateInProgress) {
     $resUpdate = [System.Windows.Forms.MessageBox]::Show('Ha atualizacao em andamento. Deseja cancelar e sair?', 'Confirmar', [System.Windows.Forms.MessageBoxButtons]::YesNo)
     if ($resUpdate -ne [System.Windows.Forms.DialogResult]::Yes) {
       $args[1].Cancel = $true
       return
     }
     try { if ($script:updateProcess -and -not $script:updateProcess.HasExited) { Stop-Process -Id $script:updateProcess.Id -Force -ErrorAction SilentlyContinue } } catch { }
     $script:updateInProgress = $false
     $script:updateTimer.Stop()
   }
   if ($script:timer.Enabled) {
     $res = [System.Windows.Forms.MessageBox]::Show('Ha downloads em andamento. Deseja cancelar e sair?', 'Confirmar', [System.Windows.Forms.MessageBoxButtons]::YesNo)
     if ($res -ne [System.Windows.Forms.DialogResult]::Yes) {
       $args[1].Cancel = $true
       return
     }
     $script:closeAfterCancel = $true
     $script:cancelRequested = $true
     $args[1].Cancel = $true
   }
 } catch [System.Management.Automation.PipelineStoppedException] { }
})
[void]$form.ShowDialog()
