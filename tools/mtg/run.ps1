# Process every recording in <Root>\inbox: transcribe -> clean -> Claude (mtg-sync).
# Usage: .\tools\mtg\run.ps1 [-Root ~\Documents\mtg] [-Ref main] [-NoClaude]
# Put audio in <Root>\inbox, run this, and leave. The PC must stay on (sleep is blocked while running).
param(
  [string]$Root = "$HOME\Documents\mtg",
  [string]$Ref = "main",
  [string]$Model = "large-v3",
  [switch]$NoClaude,
  [switch]$DryRun   # trial: Claude writes <name>.proposal.md only; nothing is written to GitHub or the repo
)
$ErrorActionPreference = "Continue"  # git/claude write progress to stderr; check $LASTEXITCODE instead
$inbox = Join-Path $Root "inbox"
$done = Join-Path $inbox "done"
$tx = Join-Path $Root "transcripts"
$logs = Join-Path $Root "logs"
$repo = Join-Path $Root "repo"
New-Item -ItemType Directory -Force $inbox, $done, $tx, $logs | Out-Null
Start-Transcript -Path (Join-Path $logs ("run-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")) | Out-Null

# keep the PC awake while this script runs
Add-Type -Namespace Win -Name Power -MemberDefinition '[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint f);'
[void][Win.Power]::SetThreadExecutionState([uint32]2147483649)  # ES_CONTINUOUS | ES_SYSTEM_REQUIRED

try {
  # a dedicated clone, so the working copy (Unity!) is never touched
  if (-not (Test-Path $repo)) { git clone https://github.com/Fialuxe/MouthOfGod_System.git $repo }
  git -C $repo fetch origin
  git -C $repo checkout -f $Ref
  git -C $repo reset --hard "origin/$Ref"
  if ($LASTEXITCODE -ne 0) { throw "git reset failed: origin/$Ref not found" }
  $tools = Join-Path $repo "tools\mtg"

  $audio = Get-ChildItem $inbox -File | Where-Object { $_.Extension -in ".mp3", ".m4a", ".wav", ".aac", ".flac", ".mp4", ".ogg" }
  if (-not $audio) { Write-Host "inbox is empty: $inbox"; return }

  foreach ($f in $audio) {
    $name = $f.BaseName
    $date = $f.LastWriteTime.ToString("yyyy-MM-dd")
    Write-Host "=== $($f.Name) ($date) ==="
    & (Join-Path $tools "transcribe.ps1") -Audio $f.FullName -OutDir $tx -Model $Model -ExtraGlossary (Join-Path $Root "glossary.local.txt")
    python -I (Join-Path $tools "clean.py") (Join-Path $tx "$name.srt")
    Move-Item $f.FullName (Join-Path $done $f.Name) -Force

    if ($NoClaude) { continue }
    $clean = Join-Path $tx "$name.clean.txt"
    # what was already written / changed around the MTG (the file time = end of the recording)
    $activity = Join-Path $tx "$name.activity.md"
    $since = $f.LastWriteTime.AddDays(-1).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    Push-Location $repo
    python -I (Join-Path $tools "activity.py") $since $activity
    Pop-Location
    if ($DryRun) {
      $prompt = "試運転です。mtg-sync スキルに従って、MTG の文字起こし $clean (MTG の日付: $date、最近の動き: $activity) を処理してください。GitHub にもリポジトリにも書き込まず、proposal.md だけを書くこと。"
      $tools = @("Bash(gh issue list *)", "Bash(gh issue view *)", "Bash(gh pr list *)", "Read", "Write", "Glob", "Grep")
    } else {
      $prompt = "mtg-sync スキルに従って、MTG の文字起こし $clean (MTG の日付: $date、最近の動き: $activity) を処理してください。確認は取らず、最後まで実行すること。"
      $tools = @("Bash(gh *)", "Bash(git *)", "Bash(python *)", "Read", "Write", "Edit", "Glob", "Grep")
    }
    Push-Location $repo
    try {
      claude -p $prompt --permission-mode acceptEdits --add-dir $Root --allowedTools $tools
      if ($LASTEXITCODE -ne 0) { Write-Warning "claude exited with $LASTEXITCODE for $name" }
    } finally { Pop-Location }
  }
} finally {
  [void][Win.Power]::SetThreadExecutionState([uint32]2147483648)
  Stop-Transcript | Out-Null
}
