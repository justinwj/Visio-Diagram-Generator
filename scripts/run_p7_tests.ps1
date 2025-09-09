param(
  [ValidateSet("Debug","Release")][string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Join-Path $Root "..")

# Build gate
$buildSw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet build -c $Configuration | Out-Host
$buildSw.Stop()

# Unit gate
$unitSw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet test ".\tests\VDG.Core.Tests" -c $Configuration --nologo --logger "trx;LogFileName=Unit.trx" | Out-Host
$unitSw.Stop()

# Smoke gate (core, no COM)
$smokeSw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet test ".\tests\VDG.Core.Tests" -c $Configuration --nologo --filter "Category=Smoke" --logger "trx;LogFileName=Smoke.trx" | Out-Host
$smokeSw.Stop()

# Perf log
$artifactDir = "artifacts"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$report = Join-Path $artifactDir "p7-test-report.txt"
$ts = [DateTime]::UtcNow.ToString("o")

@(
  "$ts`tBUILD`t$($buildSw.ElapsedMilliseconds)ms"
  "$ts`tUNIT`t$($unitSw.ElapsedMilliseconds)ms"
  "$ts`tSMOKE`t$($smokeSw.ElapsedMilliseconds)ms"
) | Out-File -FilePath $report -Append -Encoding utf8

Write-Host "Wrote $report"
