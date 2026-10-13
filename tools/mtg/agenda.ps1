# Create (or update) the agenda Issue for the next Friday MTG, using the mtg-agenda skill.
# Normally started by the scheduled task from register-agenda-task.ps1 (Wednesday 09:00).
# Usage: .\agenda.ps1 [-Root ~\Github\MouthOfGod_transcripts] [-Ref main]
param(
  [string]$Root = "$HOME\Github\MouthOfGod_transcripts",
  [string]$Ref = "main"
)
$ErrorActionPreference = "Continue"  # git/claude write progress to stderr; check $LASTEXITCODE instead
$logs = Join-Path $Root "logs"
$repo = Join-Path $Root "repo"
New-Item -ItemType Directory -Force $logs | Out-Null
Start-Transcript -Path (Join-Path $logs ("agenda-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")) | Out-Null

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

  Push-Location $repo
  try {
    $prompt = "mtg-agenda スキルに従って、次の金曜 MTG の議題 Issue を作ってください。確認は取らず、最後まで実行すること。"
    claude -p $prompt --permission-mode acceptEdits `
      --allowedTools "Bash(gh *)" "Bash(git *)" "Bash(python *)" "Read" "Write" "Edit" "Glob" "Grep"
    if ($LASTEXITCODE -ne 0) { Write-Warning "claude exited with $LASTEXITCODE" }
  } finally { Pop-Location }
} finally {
  [void][Win.Power]::SetThreadExecutionState([uint32]2147483648)
  Stop-Transcript | Out-Null
}
