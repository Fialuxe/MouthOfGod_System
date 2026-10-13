# Register a Windows scheduled task that watches a recordings folder every few minutes.
# Usage: .\register-watch-task.ps1 -Inbox "C:\Users\me\OneDrive\mtg-inbox" [-Real] [-Minutes 10] [-Root ~\Github\MouthOfGod_transcripts]
# Default is trial mode (no GitHub changes). -Real = full run. Re-run to change settings; use -Remove to delete the task.
param(
  [string]$Inbox,
  [string]$Root = "$HOME\Github\MouthOfGod_transcripts",
  [int]$Minutes = 10,
  [switch]$Real,
  [switch]$Remove,
  [string]$TaskName = "MouthOfGod-MTG-Watch"
)
$ErrorActionPreference = "Stop"
if ($Remove) { Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false; Write-Host "removed: $TaskName"; return }
if (-not $Inbox) { throw "-Inbox is required" }
New-Item -ItemType Directory -Force $Inbox | Out-Null

# the task runs a copy under <Root>\work (git-ignored), so it does not depend on where this repo is checked out
$script = Join-Path $Root "work\watch.ps1"
New-Item -ItemType Directory -Force (Split-Path $script) | Out-Null
Copy-Item (Join-Path $PSScriptRoot "watch.ps1") $script -Force

$argText = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$script`" -Inbox `"$Inbox`" -Root `"$Root`"" + $(if ($Real) { " -Real" } else { "" })
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $argText
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes $Minutes) -RepetitionDuration (New-TimeSpan -Days 3650)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
  -RunOnlyIfNetworkAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 6)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
  -Description "Watch the MTG recordings folder and process new recordings (run.ps1)" -Force | Out-Null
Write-Host ("registered: {0}  inbox={1}  every {2} min  mode={3}" -f $TaskName, $Inbox, $Minutes, $(if ($Real) { "REAL" } else { "trial" }))
Get-ScheduledTask -TaskName $TaskName | Get-ScheduledTaskInfo | Select-Object TaskName, NextRunTime
