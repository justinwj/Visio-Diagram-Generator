param(
  [string]$Config = ".\shared\Config\samples\diagramConfig.sample.json",
  [ValidateSet("Debug","Release")][string]$Configuration = "Release",
  [string]$CliProject = ".\src\VisioDiagramGenerator.CliFs\VisioDiagramGenerator.CliFs.fsproj",
  [string]$Runner = $env:VDG_RUNNER  # optional; falls back to resolver logic
)

$ErrorActionPreference = "Stop"
Write-Host "== Prompt 7 Checkpoint Smoke (COM) =="

dotnet build -c $Configuration | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

New-Item -ItemType Directory -Force -Path artifacts | Out-Null

$runnerArg = @()
if ($Runner) { $runnerArg = @("--runner", $Runner) }

dotnet run --project $CliProject -c $Configuration -- generate --config $Config `
  @runnerArg -- --saveAs artifacts\p7-smoke.vsdx | Out-Host

if (Test-Path artifacts\p7-smoke.vsdx) {
  Write-Host "OK: artifacts\p7-smoke.vsdx created."
  exit 0
} else {
  Write-Error "FAIL: expected artifacts\p7-smoke.vsdx to exist."
  exit 70
}