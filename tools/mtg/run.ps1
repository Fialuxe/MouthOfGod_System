# Process every recording in <Root>\inbox: transcribe -> clean -> Claude (mtg-sync).
# Usage: .\tools\mtg\run.ps1 [-Root ~\Github\MouthOfGod_transcripts] [-Ref main] [-NoClaude]
#        .\tools\mtg\run.ps1 -FromTranscript <Root>\YYYY-MM-DD\<name>.clean.txt   # only the minutes (mtg-sync), from a transcript made in the cloud
# Exit code: 0 = ok, 1 = the minutes step (claude) failed for at least one recording.
# Put audio in <Root>\inbox, run this, and leave. The PC must stay on (sleep is blocked while running).
param(
  [string]$Root = "$HOME\Github\MouthOfGod_transcripts",
  [string]$Ref = "main",
  [string]$Inbox = "",  # recordings folder; default <Root>\inbox. A cloud-synced folder (Google Drive / OneDrive) works
  [string]$Model = "large-v3",
  [switch]$NoClaude,
  [switch]$DryRun,  # trial: Claude writes <name>.proposal.md only; nothing is written to GitHub or the repo
  [switch]$NoArchive,  # tests: do not push the transcript to the private repo
  [string]$FromTranscript = ""  # skip the recording and the transcription: make only the minutes from this cleaned transcript (made by the cloud job)
)
$ErrorActionPreference = "Continue"  # git/claude write progress to stderr; check $LASTEXITCODE instead
$claudeFailed = $false
# the work folder is the private transcripts repo itself (config\, dated transcript folders); big files are git-ignored there
if (-not (Test-Path $Root)) { gh repo clone Fialuxe/MouthOfGod_transcripts $Root }
# local-only settings (member names etc.): <Root>\config\ first, then <Root>\
function Find-Local($name) {
  foreach ($d in @((Join-Path $Root "config"), $Root)) { $p = Join-Path $d $name; if (Test-Path $p) { return $p } }
  return (Join-Path $Root "config\$name")
}
$inbox = if ($Inbox) { $Inbox } else { Join-Path $Root "inbox" }
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
  if (-not (Test-Path $repo)) { git -c core.longpaths=true clone https://github.com/Fialuxe/MouthOfGod_System.git $repo; git -C $repo config core.longpaths true }  # the repo has deep asset paths (Windows MAX_PATH)
  git -C $repo fetch origin
  git -C $repo checkout -f $Ref
  git -C $repo reset --hard "origin/$Ref"
  if ($LASTEXITCODE -ne 0) { throw "git reset failed: origin/$Ref not found" }
  $tools = Join-Path $repo "tools\mtg"

  # the minutes step: activity -> claude (mtg-sync). Returns $true when claude exited with 0.
  function Invoke-MtgSync($name, $date, $mtgTime) {
    $clean = Join-Path $tx "$name.clean.txt"
    # what was already written / changed around the MTG (the file time = end of the recording)
    $activity = Join-Path $tx "$name.activity.md"
    $since = $mtgTime.AddDays(-1).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    Push-Location $repo
    python -I (Join-Path $tools "activity.py") $since $activity | Out-Host  # Out-Host: only $ok may leave this function as its result
    Pop-Location
    if ($DryRun) {
      $prompt = "試運転です。mtg-sync スキルに従って、MTG の文字起こし $clean (MTG の日付: $date、最近の動き: $activity、担当者の対応表: $(Find-Local 'members.local.tsv')) を処理してください。GitHub にもリポジトリにも書き込まず、proposal.md だけを書くこと。"
      $allowed = @("Bash(gh issue list *)", "Bash(gh issue view *)", "Bash(gh pr list *)", "Bash(gh pr view *)", "Bash(gh api repos/*/*/issues/*/dependencies/blocked_by)", "Read", "Write", "Glob", "Grep")
    } else {
      $prompt = "mtg-sync スキルに従って、MTG の文字起こし $clean (MTG の日付: $date、最近の動き: $activity、担当者の対応表: $(Find-Local 'members.local.tsv')) を処理してください。確認は取らず、最後まで実行すること。"
      $allowed = @("Bash(gh *)", "Bash(git *)", "Bash(python *)", "Read", "Write", "Edit", "Glob", "Grep")
    }
    Push-Location $repo
    $ok = $true
    try {
      claude -p $prompt --permission-mode acceptEdits --add-dir $Root --allowedTools $allowed | Out-Host
      if ($LASTEXITCODE -ne 0) { Write-Warning "claude exited with $LASTEXITCODE for $name"; $ok = $false }
    } finally { Pop-Location }
    return $ok
  }


  if ($FromTranscript) {
    # only the minutes, from a transcript that is already in the private repo (the cloud job made it)
    if (-not (Test-Path $FromTranscript)) { throw "transcript not found: $FromTranscript" }
    $name = (Split-Path $FromTranscript -Leaf) -replace '\.clean\.txt$', ''
    $date = Split-Path (Split-Path $FromTranscript -Parent) -Leaf
    if ($date -notmatch '^\d{4}-\d{2}-\d{2}$') { throw "the transcript must be in a YYYY-MM-DD folder: $FromTranscript" }
    Copy-Item $FromTranscript (Join-Path $tx "$name.clean.txt") -Force
    $mtgTime = [datetime]::ParseExact("$date 23:00", "yyyy-MM-dd HH:mm", $null)
    Write-Host "=== minutes only: $name ($date) ==="
    if (-not (Invoke-MtgSync $name $date $mtgTime)) { $claudeFailed = $true }
    return
  }

  $audio = Get-ChildItem $inbox -File | Where-Object { $_.Extension -in ".mp3", ".m4a", ".wav", ".aac", ".flac", ".mp4", ".ogg" }
  if (-not $audio) { Write-Host "inbox is empty: $inbox"; return }

  foreach ($f in $audio) {
    $name = $f.BaseName
    # the MTG date: a date in the file name (2026-10-16 / 20261016) wins over the file time (a recording uploaded from a phone later has a later file time)
    $mtgTime = $f.LastWriteTime
    if ($name -match '(20\d{2})[-_.]?(\d{2})[-_.]?(\d{2})') {
      try { $mtgTime = [datetime]::ParseExact("$($Matches[1])-$($Matches[2])-$($Matches[3]) 23:00", "yyyy-MM-dd HH:mm", $null) } catch { }
    }
    $date = $mtgTime.ToString("yyyy-MM-dd")
    Write-Host "=== $($f.Name) ($date) ==="
    & (Join-Path $tools "transcribe.ps1") -Audio $f.FullName -OutDir $tx -Model $Model -ExtraGlossary (Find-Local "glossary.local.txt")
    python -I (Join-Path $tools "clean.py") (Join-Path $tx "$name.srt")
    Move-Item $f.FullName (Join-Path $done $f.Name) -Force
    # keep the transcript in the private repo (not in the public system repo)
    if (-not $NoArchive) { & (Join-Path $tools "archive.ps1") -Name $name -Date $date -Root $Root }

    if ($NoClaude) { continue }
    if (-not (Invoke-MtgSync $name $date $mtgTime)) { $claudeFailed = $true }
  }
} finally {
  [void][Win.Power]::SetThreadExecutionState([uint32]2147483648)
  Stop-Transcript | Out-Null
}
if ($claudeFailed) { exit 1 } else { exit 0 }
