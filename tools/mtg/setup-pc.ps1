# Set up the PC that processes MTG recordings (Windows + NVIDIA GPU). Safe to run again: each step checks first, installs only what is missing.
# Usage:
#   .\tools\mtg\setup-pc.ps1 -Inbox "G:\My Drive\mtg-inbox" [-Real]
#   .\tools\mtg\setup-pc.ps1 -CheckOnly          # only report what is missing; install nothing
# Options: -Root (work folder), -Real (watch task writes to GitHub), -SkipWatch, -SkipPower, -SkipClaude, -Test (small GPU test)
param(
  [string]$Inbox = "",
  [string]$Root = "$HOME\Github\MouthOfGod_transcripts",
  [string]$PrivateRepo = "Fialuxe/MouthOfGod_transcripts",
  [switch]$Real,
  [switch]$CheckOnly,
  [switch]$SkipWatch,
  [switch]$SkipPower,
  [switch]$SkipClaude,
  [switch]$Test
)
$ErrorActionPreference = "Continue"
$results = New-Object System.Collections.ArrayList
function Add-Result($name, $status, $note) { [void]$results.Add([pscustomobject]@{ Step = $name; Status = $status; Note = $note }) }
function Say($m) { Write-Host "== $m" -ForegroundColor Cyan }
function Refresh-Path { $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User") }
function Has($cmd) { [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }
function Real-Python {
  # the Microsoft Store "python" stub exists on PATH but does not run
  if (-not (Has "python")) { return $false }
  $null = & python -c "import sys; sys.exit(0)" 2>$null
  return ($LASTEXITCODE -eq 0)
}
function Winget-Install($id) {
  if ($CheckOnly) { return $false }
  & winget install --id $id -e --silent --accept-package-agreements --accept-source-agreements | Out-Host
  $code = $LASTEXITCODE
  Refresh-Path
  return ($code -eq 0)
}

Say "winget"
if (-not (Has "winget")) {
  Add-Result "winget" "FAIL" "Install 'App Installer' from the Microsoft Store, then run this again"
  $results | Format-Table -AutoSize | Out-String -Width 200 | Write-Host; return
}
Add-Result "winget" "OK" ""

Say "Git"
if (Has "git") { Add-Result "git" "OK" "" }
elseif ((Winget-Install "Git.Git") -and (Has "git")) { Add-Result "git" "INSTALLED" "" }
else { Add-Result "git" "MISSING" "winget install --id Git.Git -e" }

Say "Python"
if (Real-Python) { Add-Result "python" "OK" ((& python --version) 2>&1) }
elseif ((Winget-Install "Python.Python.3.12") -and (Real-Python)) { Add-Result "python" "INSTALLED" ((& python --version) 2>&1) }
else { Add-Result "python" "MISSING" "winget install --id Python.Python.3.12 -e   (then open a new PowerShell)" }

Say "ffmpeg"
$ff = (Has "ffmpeg") -or [bool](Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Gyan.FFmpeg*" -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -First 1)
if ($ff) { Add-Result "ffmpeg" "OK" "" }
elseif (Winget-Install "Gyan.FFmpeg") { Add-Result "ffmpeg" "INSTALLED" "" }
else { Add-Result "ffmpeg" "MISSING" "winget install --id Gyan.FFmpeg -e" }

Say "GitHub CLI"
if (-not (Has "gh") -and -not $CheckOnly) { [void](Winget-Install "GitHub.cli") }
if (Has "gh") {
  & gh auth status *> $null
  if ($LASTEXITCODE -ne 0 -and -not $CheckOnly) { Write-Host "Log in to GitHub in the browser:"; & gh auth login --web --git-protocol https; & gh auth setup-git }
  & gh auth status *> $null
  if ($LASTEXITCODE -eq 0) { Add-Result "gh (logged in)" "OK" "" } else { Add-Result "gh (logged in)" "MISSING" "gh auth login" }
} else { Add-Result "gh" "MISSING" "winget install --id GitHub.cli -e" }

Say "NVIDIA GPU"
if (Has "nvidia-smi") {
  $gpu = (& nvidia-smi --query-gpu=name,memory.total --format=csv,noheader 2>$null) -join "; "
  if ($LASTEXITCODE -eq 0) { Add-Result "nvidia-smi" "OK" $gpu } else { Add-Result "nvidia-smi" "FAIL" "The driver does not respond. Update the NVIDIA driver and restart" }
} else { Add-Result "nvidia-smi" "FAIL" "Install the NVIDIA driver (large-v3 needs about 8 GB of VRAM)" }

Say "faster-whisper"
if (Real-Python) {
  & python -c "import faster_whisper" *> $null
  if ($LASTEXITCODE -eq 0) { Add-Result "faster-whisper" "OK" "" }
  elseif (-not $CheckOnly) {
    & python -m pip install --upgrade pip
    & python -m pip install --upgrade faster-whisper
    # CUDA 12 libraries used by faster-whisper (ctranslate2). Harmless if already present.
    & python -m pip install --upgrade nvidia-cublas-cu12 "nvidia-cudnn-cu12==9.*"
    & python -c "import faster_whisper" *> $null
    if ($LASTEXITCODE -eq 0) { Add-Result "faster-whisper" "INSTALLED" "" } else { Add-Result "faster-whisper" "FAIL" "python -m pip install faster-whisper" }
  } else { Add-Result "faster-whisper" "MISSING" "python -m pip install faster-whisper" }
} else { Add-Result "faster-whisper" "SKIPPED" "python is not ready" }

if ($Test -and (Real-Python)) {
  Say "GPU test (small model, 1 second of silence)"
  $code = "from faster_whisper import WhisperModel; import numpy as np; m=WhisperModel('tiny', device='cuda', compute_type='float16'); list(m.transcribe(np.zeros(16000, dtype='float32'))[0]); print('gpu ok')"
  $out = & python -c $code 2>&1
  if ($out -match "gpu ok") { Add-Result "GPU test" "OK" "" } else { Add-Result "GPU test" "FAIL" (($out | Select-Object -Last 3) -join " ") }
}

Say "Claude Code"
if ($SkipClaude) { Add-Result "claude" "SKIPPED" "" }
elseif (Has "claude") { Add-Result "claude" "OK" "Run 'claude' once and log in if you have not" }
elseif (-not $CheckOnly) {
  try { Invoke-RestMethod https://claude.ai/install.ps1 | Invoke-Expression } catch { Write-Host $_ }
  Refresh-Path
  if (Has "claude") { Add-Result "claude" "INSTALLED" "Run 'claude' once and log in" } else { Add-Result "claude" "MISSING" "See https://docs.claude.com/en/docs/claude-code/setup" }
} else { Add-Result "claude" "MISSING" "See https://docs.claude.com/en/docs/claude-code/setup" }

Say "Private transcripts repo"
if (Has "gh") {
  & gh repo view $PrivateRepo --json visibility -q .visibility *> $null
  if ($LASTEXITCODE -ne 0) { Add-Result "private repo access" "FAIL" "Ask to be invited to $PrivateRepo" }
  elseif (Test-Path (Join-Path $Root ".git")) { Add-Result "private repo clone" "OK" $Root }
  elseif (-not $CheckOnly) {
    New-Item -ItemType Directory -Force (Split-Path $Root) | Out-Null
    & gh repo clone $PrivateRepo $Root
    if ($LASTEXITCODE -eq 0) { Add-Result "private repo clone" "INSTALLED" $Root } else { Add-Result "private repo clone" "FAIL" "gh repo clone $PrivateRepo `"$Root`"" }
  } else { Add-Result "private repo clone" "MISSING" $Root }
} else { Add-Result "private repo" "SKIPPED" "gh is not ready" }

Say "Power (the PC must not sleep while it processes)"
if ($SkipPower -or $CheckOnly) { Add-Result "power" "SKIPPED" "" }
else {
  & powercfg /change standby-timeout-ac 0
  & powercfg /change hibernate-timeout-ac 0
  Add-Result "power" "OK" "No sleep or hibernate while on AC power (keep the lid open or set 'lid close = do nothing')"
}

Say "Chrome Remote Desktop host service"
$svc = Get-Service -Name "chromoting" -ErrorAction SilentlyContinue
if (-not $svc) { Add-Result "chrome remote desktop" "SKIPPED" "Not installed" }
elseif ($CheckOnly) { Add-Result "chrome remote desktop" $(if ($svc.Status -eq "Running") { "OK" } else { "MISSING" }) "service is $($svc.Status)" }
else {
  try { Set-Service -Name "chromoting" -StartupType Automatic -ErrorAction Stop; if ($svc.Status -ne "Running") { Start-Service "chromoting" -ErrorAction Stop }; Add-Result "chrome remote desktop" "OK" "service: Automatic, running" }
  catch { Add-Result "chrome remote desktop" "WARN" "Run PowerShell as Administrator to set the service to Automatic" }
}

Say "Watch task (recordings folder -> minutes)"
if ($SkipWatch -or $CheckOnly) { Add-Result "watch task" "SKIPPED" "" }
elseif (-not $Inbox) { Add-Result "watch task" "SKIPPED" "Pass -Inbox `"<synced folder>`" (the Google Drive 'mtg-inbox' folder, mirrored to this PC)" }
else {
  $reg = Join-Path $PSScriptRoot "register-watch-task.ps1"
  if ($Real) { & $reg -Inbox $Inbox -Root $Root -Real } else { & $reg -Inbox $Inbox -Root $Root }
  if ($LASTEXITCODE -eq 0 -or $?) { Add-Result "watch task" "OK" $(if ($Real) { "REAL mode: writes Issues, labels and PRs" } else { "trial mode: only proposals. Re-run with -Real when ready" }) }
  else { Add-Result "watch task" "FAIL" "See the error above" }
}

Write-Host ""
$results | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Write-Host
$bad = $results | Where-Object { $_.Status -in "FAIL", "MISSING" }
Write-Host "Manual steps that remain:" -ForegroundColor Yellow
Write-Host " 1. Google Drive for desktop: sign in and set the 'mtg-inbox' folder to Mirror files (not Stream)."
Write-Host " 2. Run 'claude' once and log in (needed for the minutes step)."
Write-Host " 3. Check later with: .\tools\mtg\setup-pc.ps1 -CheckOnly"
if ($bad) { Write-Host "`nNot ready: $($bad.Step -join ', ')" -ForegroundColor Red; exit 1 } else { Write-Host "`nAll required steps are ready." -ForegroundColor Green }
