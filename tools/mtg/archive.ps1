# Save the cleaned transcript to the PRIVATE repo (the system repo is public: never put transcripts there).
# Usage: .\archive.ps1 -Name 1013_second -Date 2026-10-13 [-Root ~\Documents\mtg] [-Repo Fialuxe/MouthOfGod_transcripts]
param(
  [Parameter(Mandatory = $true)][string]$Name,
  [Parameter(Mandatory = $true)][string]$Date,
  [string]$Root = "$HOME\Documents\mtg",
  [string]$Repo = "Fialuxe/MouthOfGod_transcripts"
)
$ErrorActionPreference = "Continue"  # git/gh write progress to stderr; check $LASTEXITCODE instead
$clone = Join-Path $Root "transcripts-repo"
$src = Join-Path $Root "transcripts"

# refuse to archive into a public repo
$vis = gh repo view $Repo --json visibility -q .visibility
if ($vis -ne "PRIVATE") { throw "$Repo is not private ($vis); not archiving" }

if (-not (Test-Path $clone)) { gh repo clone $Repo $clone }
git -C $clone pull -q --ff-only 2>&1 | Out-Null

$dest = Join-Path $clone $Date
New-Item -ItemType Directory -Force $dest | Out-Null
foreach ($ext in ".clean.txt", ".clean.srt") {
  $f = Join-Path $src "$Name$ext"
  if (Test-Path $f) { Copy-Item $f $dest -Force }
}
git -C $clone add -A
git -C $clone diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
  git -C $clone commit -q -m "$Date $Name"
  git -C $clone push -q
  if ($LASTEXITCODE -ne 0) { throw "git push to $Repo failed" }
  Write-Host "archived: $Date/$Name -> $Repo"
} else { Write-Host "archive: nothing new for $Name" }
