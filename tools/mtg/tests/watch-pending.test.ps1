# Test of the "minutes pending" markers in watch.ps1 (the cloud job leaves <date>\<name>.minutes.pending; a PC makes the minutes).
# Runs on Linux, macOS or Windows with PowerShell 7+ and git. Run: pwsh tools/mtg/tests/watch-pending.test.ps1
$ErrorActionPreference = "Stop"
$watch = Join-Path $PSScriptRoot "..\watch.ps1"
$tokens = $null; $errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path $watch), [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw "watch.ps1 does not parse: $($errors[0].Message)" }
# take the real function definitions out of watch.ps1 (so the test cannot drift from the script)
$ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -in "Update-PrivateRepo", "Get-PendingMinutes", "Save-MinutesResult" }, $true) |
  ForEach-Object { Invoke-Expression $_.Extent.Text }
$MaxMinutesAttempts = 3
$HostName = "PC-A"

$fails = 0
function Check($name, $cond) { if ($cond) { "ok   $name" } else { "FAIL $name"; $script:fails++ } }

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("pend-" + [guid]::NewGuid()); New-Item -ItemType Directory $tmp | Out-Null
git init -q --bare "$tmp/remote.git" 2>&1 | Out-Null
git clone -q "$tmp/remote.git" "$tmp/root" 2>&1 | Out-Null
$Root = "$tmp/root"
git -C $Root config user.name t; git -C $Root config user.email t@t
git -C $Root commit -q --allow-empty -m init 2>&1 | Out-Null; git -C $Root push -q origin HEAD 2>&1 | Out-Null

# a commit from "the cloud job": two transcripts with pending markers, and one old transcript without a marker
git clone -q "$tmp/remote.git" "$tmp/cloud" 2>&1 | Out-Null
git -C "$tmp/cloud" config user.name c; git -C "$tmp/cloud" config user.email c@c
New-Item -ItemType Directory "$tmp/cloud/2026-10-16", "$tmp/cloud/2026-10-13" | Out-Null
foreach ($n in "a", "b") {
  Set-Content "$tmp/cloud/2026-10-16/$n.clean.txt" "x"
  Set-Content "$tmp/cloud/2026-10-16/$n.minutes.pending" '{"created":"2026-10-16T00:00:00Z","attempts":0}'
}
Set-Content "$tmp/cloud/2026-10-13/old.clean.txt" "z"
git -C "$tmp/cloud" add -A 2>&1 | Out-Null; git -C "$tmp/cloud" commit -q -m "2026-10-16 (cloud)" 2>&1 | Out-Null; git -C "$tmp/cloud" push -q origin HEAD 2>&1 | Out-Null

Check "before the update nothing is pending locally" ((Get-PendingMinutes).Count -eq 0)
Update-PrivateRepo
$p = @(Get-PendingMinutes)
Check "after Update-PrivateRepo two are pending (a, b)" ($p.Count -eq 2)
Check "an old transcript without a marker is NOT picked up" (-not ($p | Where-Object { $_.Name -like "old*" }))

Save-MinutesResult $p[0] $true
Check "success: .pending is replaced by .done" ((Test-Path "$Root/2026-10-16/a.minutes.done") -and -not (Test-Path "$Root/2026-10-16/a.minutes.pending"))
Check "success: pushed to the remote" ((git -C "$tmp/remote.git" log --oneline -1) -match "a: minutes done")

for ($i = 1; $i -le 3; $i++) {
  $pb = @(Get-PendingMinutes | Where-Object { $_.Name -like "b*" })
  if ($pb.Count -eq 0) { break }
  Save-MinutesResult $pb[0] $false
  if ($i -lt 3) { Check "failure ${i}: still pending, attempts = $i" ((Get-Content "$Root/2026-10-16/b.minutes.pending" -Raw | ConvertFrom-Json).attempts -eq $i) }
}
Check "failure 3: .minutes.failed, no longer pending" ((Test-Path "$Root/2026-10-16/b.minutes.failed") -and -not (Test-Path "$Root/2026-10-16/b.minutes.pending"))
Check "nothing is left to do" ((Get-PendingMinutes).Count -eq 0)

Remove-Item $tmp -Recurse -Force
if ($fails -gt 0) { "`n$fails FAILED"; exit 1 } else { "`nALL PASSED" }
