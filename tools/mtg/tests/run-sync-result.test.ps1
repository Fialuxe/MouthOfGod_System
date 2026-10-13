# Test: Invoke-MtgSync in run.ps1 returns exactly $true when claude exits with 0, and $false otherwise (no other output leaks into the result).
# Run: pwsh tools/mtg/tests/run-sync-result.test.ps1   (uses a fake "claude" and "python" on PATH)
$ErrorActionPreference = "Stop"
$run = Join-Path $PSScriptRoot "..\run.ps1"
$tokens = $null; $errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path $run), [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw "run.ps1 does not parse: $($errors[0].Message)" }
$ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq "Invoke-MtgSync" }, $true) | ForEach-Object { Invoke-Expression $_.Extent.Text }

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("sync-" + [guid]::NewGuid()); New-Item -ItemType Directory $tmp, "$tmp/bin" | Out-Null
foreach ($cmd in "claude", "python") {
  Set-Content "$tmp/bin/$cmd" "#!/bin/sh`necho `"$cmd output`"`nexit `${FAKE_RC:-0}`n"
  chmod +x "$tmp/bin/$cmd"
}
$env:PATH = "$tmp/bin" + [IO.Path]::PathSeparator + $env:PATH
function Find-Local($n) { return "$tmp/config/$n" }
$tx = $tmp; $repo = $tmp; $tools = $tmp; $Root = $tmp; $DryRun = $false

$fails = 0
function Check($name, $cond) { if ($cond) { "ok   $name" } else { "FAIL $name"; $script:fails++ } }
$env:FAKE_RC = "0"
$r = Invoke-MtgSync "n" "2026-10-16" ([datetime]"2026-10-16 23:00")
Check "claude exit 0 -> `$true (a single Boolean)" ($r -is [bool] -and $r -eq $true)
$env:FAKE_RC = "3"
$r = Invoke-MtgSync "n" "2026-10-16" ([datetime]"2026-10-16 23:00")
Check "claude exit 3 -> `$false (a single Boolean)" ($r -is [bool] -and $r -eq $false)
Remove-Item $tmp -Recurse -Force
if ($fails -gt 0) { "`n$fails FAILED"; exit 1 } else { "`nALL PASSED" }
