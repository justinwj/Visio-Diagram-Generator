param(
    [string]$Config = "",
    [string]$SourceRoot = "",
    [string]$OutFile = ""
)

$ErrorActionPreference = "Stop"
$report = Join-Path $PSScriptRoot ".last-smoke.txt"

Write-Host "=== VDG Smoke Test Stub ==="
Write-Host "Config:      $Config"
Write-Host "SourceRoot:  $SourceRoot"
Write-Host "OutFile:     $OutFile"

$start = Get-Date

# Prompt 2 stub: no real smoke, just a friendly placeholder
Write-Host "[SMOKE] No-op check (Prompt 2)"
Start-Sleep -Seconds 1

$dur = (Get-Date) - $start

"Smoke: OK (stub)"              | Out-File $report
"Duration: $($dur.TotalSeconds)s" | Out-File $report -Append

Write-Host "`n=== Done. Report at $report ==="
