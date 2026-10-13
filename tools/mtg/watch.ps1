# Watch a recordings folder (e.g. a Google Drive / OneDrive synced folder) and process new recordings with run.ps1.
# Started every few minutes by the scheduled task from register-watch-task.ps1.
# Default is TRIAL mode: nothing is written to GitHub, a proposal is made. -Real runs everything (Issues, labels, PR).
# Results (proposal.md, run log) are copied to <Inbox>\results so they can be read from anywhere via the synced folder.
param(
  [Parameter(Mandatory = $true)][string]$Inbox,
  [string]$Root = "$HOME\Github\MouthOfGod_transcripts",
  [switch]$Real,
  [switch]$NoArchive,  # tests: do not push the transcript to the private repo
  [string]$RunScript = "",  # tests: use this run.ps1 instead of the one in the dedicated clone (main)
  [string]$HostName = [Environment]::MachineName,  # tests: pretend to be another PC
  [int]$ClaimHours = 3,  # a claim older than this is ignored (the PC that made it is assumed dead)
  [int]$ClaimWaitSeconds = 90  # wait for the synced folder to show the other PC's claim
)
$ErrorActionPreference = "Continue"
$exts = ".mp3", ".m4a", ".wav", ".aac", ".flac", ".mp4", ".ogg"

# Several PCs can watch the same folder (so the work does not depend on one person's PC).
# The PC that wants to process writes a claim file in the folder; the others skip while a fresh claim of another PC exists.
# A claim older than $ClaimHours hours is ignored, so another PC takes over if the first one died.
function Read-Claim($path) {
  if (-not (Test-Path $path)) { return $null }
  try { return (Get-Content $path -Raw | ConvertFrom-Json) } catch { return $null }
}
function Test-OtherClaim($path) {
  $c = Read-Claim $path
  if (-not $c) { return $false }
  if ($c.host -eq $HostName) { return $false }
  try { $age = (Get-Date).ToUniversalTime() - ([datetime]$c.time).ToUniversalTime() } catch { return $false }
  return ($age.TotalHours -lt $ClaimHours)
}
function New-Claim($path) {
  $nonce = [guid]::NewGuid().ToString()
  @{ host = $HostName; time = (Get-Date).ToUniversalTime().ToString("o"); nonce = $nonce } | ConvertTo-Json | Set-Content -Path $path -Encoding UTF8
  return $nonce
}

# Transcripts made by the cloud job wait in the private repo as <date>\<name>.minutes.pending.
# A PC makes the minutes (mtg-sync) from them, then replaces the marker with .minutes.done (or .minutes.failed after 3 failures).
$MaxMinutesAttempts = 3
function Update-PrivateRepo {
  if (-not (Test-Path (Join-Path $Root ".git"))) { return }
  git -C $Root fetch -q origin 2>&1 | Out-Null
  git -C $Root merge -q --ff-only "@{u}" 2>&1 | Out-Null
}
function Get-PendingMinutes {
  if (-not (Test-Path $Root)) { return @() }
  return @(Get-ChildItem $Root -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}$' } |
    ForEach-Object { Get-ChildItem $_.FullName -Filter "*.minutes.pending" -File -ErrorAction SilentlyContinue } | Sort-Object FullName)
}
function Save-MinutesResult($pending, [bool]$ok) {
  $info = $null
  try { $info = Get-Content $pending.FullName -Raw | ConvertFrom-Json } catch { }
  $attempts = if ($info -and $info.attempts) { [int]$info.attempts } else { 0 }
  $base = $pending.FullName -replace '\.minutes\.pending$', ''
  $now = (Get-Date).ToUniversalTime().ToString("o")
  if ($ok) {
    Remove-Item $pending.FullName -Force
    Set-Content -Path "$base.minutes.done" -Value (@{ done = $now; host = $HostName } | ConvertTo-Json) -Encoding UTF8
    $what = "minutes done"
  } else {
    $attempts++
    if ($attempts -ge $MaxMinutesAttempts) {
      Remove-Item $pending.FullName -Force
      Set-Content -Path "$base.minutes.failed" -Value (@{ failed = $now; attempts = $attempts; host = $HostName } | ConvertTo-Json) -Encoding UTF8
      $what = "minutes FAILED $attempts times (not retried; delete the .minutes.failed file to retry)"
    } else {
      Set-Content -Path $pending.FullName -Value (@{ created = if ($info) { $info.created } else { $now }; attempts = $attempts } | ConvertTo-Json) -Encoding UTF8
      $what = "minutes failed (attempt $attempts of $MaxMinutesAttempts)"
    }
  }
  $dir = $pending.Directory.FullName
  $name = Split-Path $base -Leaf
  git -C $Root add -A -- $dir 2>&1 | Out-Null
  git -C $Root diff --cached --quiet
  if ($LASTEXITCODE -ne 0) {
    git -C $Root commit -q -m "$($pending.Directory.Name) ${name}: $what" 2>&1 | Out-Null
    for ($i = 0; $i -lt 3; $i++) {
      git -C $Root push -q 2>&1 | Out-Null
      if ($LASTEXITCODE -eq 0) { break }
      git -C $Root pull -q --rebase 2>&1 | Out-Null
    }
  }
}

# one run at a time (a run takes from minutes to an hour)
$mutex = New-Object System.Threading.Mutex($false, "MouthOfGodMtgWatch")
if (-not $mutex.WaitOne(0)) { exit 0 }
$claimed = $false
try {
  if (-not (Test-Path $Inbox)) { exit 0 }
  Update-PrivateRepo
  $files = @(Get-ChildItem $Inbox -File | Where-Object { $_.Extension.ToLower() -in $exts })
  $pending = @(Get-PendingMinutes)
  if ($files.Count -eq 0 -and $pending.Count -eq 0) { exit 0 }

  # a synced file may still be downloading: wait until size and time stop changing and the file can be opened
  if ($files.Count -gt 0) {
    $before = $files | ForEach-Object { "$($_.FullName)|$($_.Length)|$($_.LastWriteTimeUtc.Ticks)" }
    Start-Sleep -Seconds 60
    foreach ($f in $files) {
      $g = Get-Item $f.FullName -ErrorAction SilentlyContinue
      if (-not $g -or ("$($g.FullName)|$($g.Length)|$($g.LastWriteTimeUtc.Ticks)" -notin $before)) { exit 0 }  # still changing: next time
      try { $fs = [IO.File]::Open($g.FullName, "Open", "Read", "None"); $fs.Close() } catch { exit 0 }
    }
  }

  # the cloud job (GitHub Actions) moves a recording into <Inbox>\processing while it works: leave the folder alone then
  $procDir = Join-Path $Inbox "processing"
  if ((Test-Path $procDir) -and (Get-ChildItem $procDir -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension.ToLower() -in $exts })) { exit 0 }

  # claim the folder (another PC may be watching it too)
  $claimPath = Join-Path $Inbox ".mtg-processing.claim"
  if (Test-OtherClaim $claimPath) { exit 0 }
  $nonce = New-Claim $claimPath
  Start-Sleep -Seconds $ClaimWaitSeconds
  $c = Read-Claim $claimPath
  if (-not $c -or $c.nonce -ne $nonce) { exit 0 }  # another PC claimed it at the same time and won
  $claimed = $true

  # always use the latest run.ps1 from the dedicated clone (main)
  $repo = Join-Path $Root "repo"
  if (-not (Test-Path $repo)) { git -c core.longpaths=true clone https://github.com/Fialuxe/MouthOfGod_System.git $repo; git -C $repo config core.longpaths true }
  git -C $repo fetch origin 2>&1 | Out-Null
  git -C $repo checkout -q -f main 2>&1 | Out-Null
  git -C $repo reset -q --hard origin/main 2>&1 | Out-Null
  $runPs1 = Join-Path $repo "tools\mtg\run.ps1"
  if ($RunScript) { $runPs1 = $RunScript }

  $start = Get-Date
  $runArgs = @{ Root = $Root; Inbox = $Inbox }
  if (-not $Real) { $runArgs.DryRun = $true }
  if ($NoArchive) { $runArgs.NoArchive = $true }
  if ($files.Count -gt 0) { & $runPs1 @runArgs }

  # the minutes (mtg-sync) for transcripts that the cloud job made. Trial mode: only a proposal, and only once per transcript.
  foreach ($p in (Get-PendingMinutes)) {
    $name = $p.Name -replace '\.minutes\.pending$', ''
    $clean = Join-Path $p.Directory.FullName "$name.clean.txt"
    if (-not (Test-Path $clean)) { if ($Real) { Save-MinutesResult $p $false }; continue }
    if (-not $Real -and (Test-Path (Join-Path $Root "transcripts\$name.proposal.md"))) { continue }
    $fromArgs = @{ Root = $Root; FromTranscript = $clean }
    if (-not $Real) { $fromArgs.DryRun = $true }
    & $runPs1 @fromArgs
    $okMinutes = ($LASTEXITCODE -eq 0)
    if ($Real) { Save-MinutesResult $p $okMinutes }
  }

  # copy the results next to the recordings (readable from home via the synced folder)
  $out = Join-Path $Inbox "results"
  New-Item -ItemType Directory -Force $out | Out-Null
  Get-ChildItem (Join-Path $Root "transcripts") -Filter "*.proposal.md" -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -ge $start } | Copy-Item -Destination $out -Force
  Get-ChildItem (Join-Path $Root "logs") -Filter "run-*.log" -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -ge $start } | Sort-Object LastWriteTime | Select-Object -Last 1 | Copy-Item -Destination $out -Force
} finally {
  if ($claimed) { $c2 = Read-Claim $claimPath; if ($c2 -and $c2.nonce -eq $nonce) { Remove-Item $claimPath -Force -ErrorAction SilentlyContinue } }
  $mutex.ReleaseMutex()
}
