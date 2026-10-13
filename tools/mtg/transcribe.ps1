# Usage: .\transcribe.ps1 <audio> -OutDir <dir> [-Model turbo]
# Output: <OutDir>\<name>.srt / .txt / .json
param(
  [Parameter(Mandatory = $true)][string]$Audio,
  [Parameter(Mandatory = $true)][string]$OutDir,
  [string]$Model = "turbo"
)
$ErrorActionPreference = "Stop"

# ffmpeg is not on PATH in a shell opened before `winget install Gyan.FFmpeg`
if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
  $ff = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Gyan.FFmpeg*" -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $ff) { throw "ffmpeg not found. Run: winget install Gyan.FFmpeg" }
  $env:Path += ";" + $ff.DirectoryName
}

# glossary.txt -> initial_prompt (Whisper reads only ~224 tokens: keep the glossary short)
$prompt = ""
$glossary = Join-Path $PSScriptRoot "glossary.txt"
if (Test-Path $glossary) {
  $prompt = ((Get-Content $glossary -Encoding UTF8 | Where-Object { $_.Trim() -ne "" }) -join "、")
}

New-Item -ItemType Directory -Force $OutDir | Out-Null

# --hallucination_silence_threshold: drop "ご視聴ありがとうございました" etc. made up in silence
$ErrorActionPreference = "Continue"  # whisper prints harmless warnings on stderr
whisper $Audio --language Japanese --model $Model --device cuda `
  --output_dir $OutDir --output_format all `
  --condition_on_previous_text False `
  --word_timestamps True --hallucination_silence_threshold 2 `
  --initial_prompt $prompt
if ($LASTEXITCODE -ne 0) { throw "whisper failed ($LASTEXITCODE)" }
