param([switch]$NoBuild)

$ErrorActionPreference = "Stop"
$root   = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo   = Resolve-Path "$root\.."
$report = Join-Path $root ".last-run.txt"

# Force the 64-bit dotnet host—ignores PATH and DOTNET_ROOT
$dotnet = Join-Path ${env:ProgramFiles} "dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { throw "dotnet host not found at $dotnet" }

# Fail fast if the SDK can’t be loaded
& $dotnet --info | Out-Null


$start = Get-Date

Write-Host "=== VDG Test Runner ==="
Write-Host "Repo root: $repo"
Write-Host "Skip build: $NoBuild"

if (-not $NoBuild) {
    Write-Host "`n[BUILD] dotnet build..."
    dotnet build "$repo\VisioDiagramGenerator.sln" -c Debug --nologo
}

Write-Host "`n[UNIT] Running VDG.Core.Tests..."
$unitStart = Get-Date
dotnet test "$repo\tests\VDG.Core.Tests\VDG.Core.Tests.csproj" -c Debug --no-build --nologo
$unitDur = (Get-Date) - $unitStart
Write-Host "[UNIT] Duration: $($unitDur.TotalSeconds)s"

Write-Host "`n[SMOKE] Running smoke.ps1..."
$smokeStart = Get-Date
& "$root\smoke.ps1"
$smokeDur = (Get-Date) - $smokeStart
Write-Host "[SMOKE] Duration: $($smokeDur.TotalSeconds)s"

$totalDur = (Get-Date) - $start

"Build: OK"                                   | Out-File $report
"Unit: Pass ($($unitDur.TotalSeconds)s)"      | Out-File $report -Append
"Smoke: OK ($($smokeDur.TotalSeconds)s)"      | Out-File $report -Append
"Total Duration: $($totalDur.TotalSeconds)s"  | Out-File $report -Append

Write-Host "`n=== Done. Report at $report ==="


