# Usage: .\transcribe.ps1 <audio> -OutDir <dir> [-Model large-v3]
# Normalises loudness (quiet / distant recordings are otherwise cut or misheard),
# then runs faster-whisper. Output: <OutDir>\<name>.srt / .txt
param(
  [Parameter(Mandatory = $true)][string]$Audio,
  [Parameter(Mandatory = $true)][string]$OutDir,
  [string]$Model = "large-v3"
)
$ErrorActionPreference = "Stop"

# ffmpeg is not on PATH in a shell opened before `winget install Gyan.FFmpeg`
if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
  $ff = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Gyan.FFmpeg*" -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $ff) { throw "ffmpeg not found. Run: winget install Gyan.FFmpeg" }
  $env:Path += ";" + $ff.DirectoryName
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
$stem = [IO.Path]::GetFileNameWithoutExtension($Audio)
$norm = Join-Path ([IO.Path]::GetTempPath()) "$stem.norm.wav"

$ErrorActionPreference = "Continue"  # native tools print harmless warnings on stderr
ffmpeg -y -loglevel error -i $Audio -af "highpass=f=80,loudnorm=I=-16:TP=-1.5:LRA=11" -ar 16000 -ac 1 $norm
if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed ($LASTEXITCODE)" }

python -I (Join-Path $PSScriptRoot "transcribe.py") $norm $OutDir --stem $stem --model $Model
if ($LASTEXITCODE -ne 0) { throw "transcribe.py failed ($LASTEXITCODE)" }
Remove-Item $norm -ErrorAction SilentlyContinue
