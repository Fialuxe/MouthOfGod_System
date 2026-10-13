# Register a Windows scheduled task that runs agenda.ps1 every Wednesday 09:00.
# Usage: .\register-agenda-task.ps1 [-Root ~\Github\MouthOfGod_transcripts] [-Time 09:00] [-Day Wednesday]
# Re-run to update (it copies agenda.ps1 to <Root>\work and replaces the task).
param(
  [string]$Root = "$HOME\Github\MouthOfGod_transcripts",
  [string]$Time = "09:00",
  [string]$Day = "Wednesday",
  [string]$TaskName = "MouthOfGod-MTG-Agenda"
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force $Root | Out-Null
# the task runs a copy under <Root>, so it does not depend on where this repo is checked out
$script = Join-Path $Root "work\agenda.ps1"  # work\ is git-ignored in the transcripts repo
New-Item -ItemType Directory -Force (Split-Path $script) | Out-Null
Copy-Item (Join-Path $PSScriptRoot "agenda.ps1") $script -Force

$action = New-ScheduledTaskAction -Execute "powershell.exe" `
  -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`" -Root `"$Root`""
$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek $Day -At $Time
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -WakeToRun -AllowStartIfOnBatteries `
  -DontStopIfGoingOnBatteries -RunOnlyIfNetworkAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 30)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
  -Description "Create the agenda Issue for the next Friday MTG (mtg-agenda skill)" -Force | Out-Null
Get-ScheduledTask -TaskName $TaskName | Get-ScheduledTaskInfo | Select-Object TaskName, NextRunTime
