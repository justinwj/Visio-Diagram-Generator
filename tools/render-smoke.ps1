Param(
  [string]$In = 'tests/fixtures/vba/hello_world',
  [string]$Mode = 'callgraph',
  [string]$Cli = 'src/VDG.CLI/bin/Release/net48/VDG.CLI.exe',
  [string]$OutDir = 'out/perf',
  [string]$Baseline = 'tests/baselines/render_diagnostics.json',
  [switch]$UpdateBaseline,
  [switch]$UseVisio
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-CliPath {
  param([string]$Path)
  if (-not [string]::IsNullOrWhiteSpace($Path) -and (Test-Path $Path)) {
    return (Resolve-Path $Path).Path
  }
  $candidate = 'src/VDG.CLI/bin/Debug/net48/VDG.CLI.exe'
  if (Test-Path $candidate) {
    return (Resolve-Path $candidate).Path
  }
  throw "Unable to locate VDG.CLI.exe. Build the project or provide --Cli <path>."
}

function Invoke-Dotnet {
  param(
    [string[]]$Arguments,
    [hashtable]$Environment = @{},
    [int[]]$AcceptExitCodes = @(0)
  )

  $originalEnv = @{}
  foreach ($kvp in $Environment.GetEnumerator()) {
    $key = [string]$kvp.Key
    $originalEnv[$key] = if (Test-Path "Env:$key") { (Get-Item "Env:$key").Value } else { $null }
    Set-Item -Path "Env:$key" -Value $kvp.Value
  }

  try {
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $Arguments -NoNewWindow -Wait -PassThru
  }
  finally {
    foreach ($kvp in $Environment.GetEnumerator()) {
      $key = [string]$kvp.Key
      if ($originalEnv[$key] -ne $null) {
        Set-Item -Path "Env:$key" -Value $originalEnv[$key]
      } else {
        Remove-Item -Path "Env:$key" -ErrorAction SilentlyContinue
      }
    }
  }

  if (-not $AcceptExitCodes.Contains($process.ExitCode)) {
    throw "dotnet $($Arguments -join ' ') exited with code $($process.ExitCode)."
  }

  return $process.ExitCode
}

function Get-Summary {
  param($Diag)

  $lanePages = @()
  if ($Diag.metrics.lanePages) {
    foreach ($lp in ($Diag.metrics.lanePages | Sort-Object tier, page)) {
      $lanePages += [pscustomobject]@{
        tier            = $lp.tier
        page            = [int]$lp.page
        occupancyRatio  = [math]::Round([double]$lp.occupancyRatio, 5)
        nodes           = [int]$lp.nodes
      }
    }
  }

  $containerPages = @()
  if ($Diag.metrics.containers) {
    foreach ($cp in ($Diag.metrics.containers | Sort-Object id, page)) {
      $containerPages += [pscustomobject]@{
        id              = $cp.id
        tier            = $cp.tier
        page            = [int]$cp.page
        occupancyRatio  = [math]::Round([double]$cp.occupancyRatio, 5)
        nodes           = [int]$cp.nodes
      }
    }
  }

  $issueCounts = @()
  if ($Diag.issues) {
    foreach ($group in ($Diag.issues | Group-Object code | Sort-Object Name)) {
      $issueCounts += [pscustomobject]@{
        code  = $group.Name
        count = $group.Count
      }
    }
  }

  return [ordered]@{
    generatedAtUtc        = (Get-Date).ToUniversalTime().ToString('o')
    input                 = $In
    mode                  = $Mode
    connectorCount        = [int]$Diag.metrics.connectorCount
    straightLineCrossings = [int]$Diag.metrics.straightLineCrossings
    lanePages             = $lanePages
    containers            = $containerPages
    issues                = $issueCounts
  }
}

function Test-WithinPercent {
  param(
    [string]$Name,
    [double]$BaselineValue,
    [double]$ActualValue,
    [double]$PercentTolerance = 0.05
  )

  if ([math]::Abs($BaselineValue) -le 1e-9) {
    if ([math]::Abs($ActualValue) -le 1e-6) { return }
    throw "$Name drifted: baseline 0, actual $ActualValue."
  }

  $delta = [math]::Abs($ActualValue - $BaselineValue) / [math]::Abs($BaselineValue)
  if ($delta -gt $PercentTolerance) {
    $formatted = "{0:P2}" -f $delta
    throw "$Name drifted by $formatted (baseline $BaselineValue, actual $ActualValue)."
  }
}

$cliPath = Resolve-CliPath -Path $Cli
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType Directory -Force -Path 'out/tmp' | Out-Null

$vsdxPath = Join-Path 'out/tmp' 'render_smoke.vsdx'
$diagPath = Join-Path 'out/tmp' 'render_smoke.diagnostics.json'
$diagramJsonPath = Join-Path 'out/tmp' 'render_smoke.diagram.json'
$summaryPath = Join-Path $OutDir 'render_diagnostics.json'
$rawPath = Join-Path $OutDir 'render_diagnostics.raw.json'

$renderArgs = @(
  'run', '--configuration', 'Release',
  '--project', 'src/VDG.VBA.CLI',
  '--', 'render',
  '--in', $In,
  '--out', $vsdxPath,
  '--mode', $Mode,
  '--cli', $cliPath,
  '--diag-json', $diagPath,
  '--diagram-json', $diagramJsonPath
)

$previousSkip = $env:VDG_SKIP_RUNNER
try {
  if ($UseVisio) {
    Write-Host "Running render smoke for '$In' (Visio automation enabled)..."
    if ($null -ne $previousSkip) {
      Remove-Item Env:VDG_SKIP_RUNNER -ErrorAction SilentlyContinue
    }
  }
  else {
    Write-Host "Running render smoke for '$In' (VDG_SKIP_RUNNER engaged)..."
    $env:VDG_SKIP_RUNNER = '1'
  }
  Invoke-Dotnet -Arguments $renderArgs
}
finally {
  if ($UseVisio) {
    if ($null -ne $previousSkip) {
      $env:VDG_SKIP_RUNNER = $previousSkip
    }
    else {
      Remove-Item Env:VDG_SKIP_RUNNER -ErrorAction SilentlyContinue
    }
  }
  else {
    if ($null -ne $previousSkip) { $env:VDG_SKIP_RUNNER = $previousSkip } else { Remove-Item Env:VDG_SKIP_RUNNER -ErrorAction SilentlyContinue }
  }
}

if (-not (Test-Path $diagPath)) {
  throw "Expected diagnostics JSON was not produced at $diagPath."
}

if ($UseVisio -and -not (Test-Path $vsdxPath)) {
  throw "Visio automation was requested but the renderer did not produce a VSDX at $vsdxPath."
}
Copy-Item -Path $diagPath -Destination $rawPath -Force

$diag = Get-Content -Raw -Path $diagPath | ConvertFrom-Json
$summary = Get-Summary -Diag $diag
($summary | ConvertTo-Json -Depth 6) | Set-Content -Path $summaryPath -Encoding UTF8

if ($UpdateBaseline) {
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Baseline) | Out-Null
  ($summary | ConvertTo-Json -Depth 6) | Set-Content -Path $Baseline -Encoding UTF8
  Write-Host "Baseline updated -> $Baseline"
  return
}

if (-not (Test-Path $Baseline)) {
  throw "Baseline summary not found: $Baseline. Run with -UpdateBaseline to create one."
}

$summaryObj = Get-Content -Raw -Path $summaryPath | ConvertFrom-Json
$baselineObj = Get-Content -Raw -Path $Baseline | ConvertFrom-Json

$baselineConnector = [double]$baselineObj.connectorCount
$actualConnector = [double]$summaryObj.connectorCount
Test-WithinPercent -Name 'connectorCount' -BaselineValue $baselineConnector -ActualValue $actualConnector

$baselineCrossings = [double]$baselineObj.straightLineCrossings
$actualCrossings = [double]$summaryObj.straightLineCrossings
Test-WithinPercent -Name 'straightLineCrossings' -BaselineValue $baselineCrossings -ActualValue $actualCrossings

$baselineLane = @{}
$baselineLanePages = $baselineObj.lanePages
if ($baselineLanePages) {
  foreach ($lp in $baselineLanePages) {
    $key = "{0}|{1}" -f $lp.tier, $lp.page
    $baselineLane[$key] = $lp
  }
}
$actualLane = @{}
$actualLanePages = $summaryObj.lanePages
if ($actualLanePages) {
  foreach ($lp in $actualLanePages) {
    $key = "{0}|{1}" -f $lp.tier, $lp.page
    $actualLane[$key] = $lp
  }
}

if ($baselineLane.Count -ne $actualLane.Count) {
  throw "Lane page count mismatch: baseline $($baselineLane.Count) vs actual $($actualLane.Count)."
}
foreach ($key in $baselineLane.Keys) {
  if (-not $actualLane.ContainsKey($key)) {
    throw "Lane page '$key' missing from actual diagnostics."
  }
  Test-WithinPercent -Name "lanePages[$key].occupancyRatio" -BaselineValue ([double]$baselineLane[$key].occupancyRatio) -ActualValue ([double]$actualLane[$key].occupancyRatio)
  if ([int]$baselineLane[$key].nodes -ne [int]$actualLane[$key].nodes) {
    throw "lanePages[$key].nodes mismatch: baseline $([int]$baselineLane[$key].nodes) vs actual $([int]$actualLane[$key].nodes)."
  }
}

$baselineContainers = @{}
$baselineContainerPages = $baselineObj.containers
if ($baselineContainerPages) {
  foreach ($cp in $baselineContainerPages) {
    $key = "{0}|{1}" -f $cp.id, $cp.page
    $baselineContainers[$key] = $cp
  }
}
$actualContainers = @{}
$actualContainerPages = $summaryObj.containers
if ($actualContainerPages) {
  foreach ($cp in $actualContainerPages) {
    $key = "{0}|{1}" -f $cp.id, $cp.page
    $actualContainers[$key] = $cp
  }
}

if ($baselineContainers.Count -ne $actualContainers.Count) {
  throw "Container page count mismatch: baseline $($baselineContainers.Count) vs actual $($actualContainers.Count)."
}
foreach ($key in $baselineContainers.Keys) {
  if (-not $actualContainers.ContainsKey($key)) {
    throw "Container page '$key' missing from actual diagnostics."
  }
  Test-WithinPercent -Name "containers[$key].occupancyRatio" -BaselineValue ([double]$baselineContainers[$key].occupancyRatio) -ActualValue ([double]$actualContainers[$key].occupancyRatio)
  if ([int]$baselineContainers[$key].nodes -ne [int]$actualContainers[$key].nodes) {
    throw "containers[$key].nodes mismatch: baseline $([int]$baselineContainers[$key].nodes) vs actual $([int]$actualContainers[$key].nodes)."
  }
}

$baselineIssues = @{}
$baselineIssuesList = $baselineObj.issues
if ($baselineIssuesList) {
  foreach ($issue in $baselineIssuesList) {
    $baselineIssues[[string]$issue.code] = [int]$issue.count
  }
}
$actualIssues = @{}
$actualIssuesList = $summaryObj.issues
if ($actualIssuesList) {
  foreach ($issue in $actualIssuesList) {
    $actualIssues[[string]$issue.code] = [int]$issue.count
  }
}

if ($baselineIssues.Count -ne $actualIssues.Count) {
  throw "Issue code count mismatch: baseline $($baselineIssues.Count) vs actual $($actualIssues.Count)."
}
foreach ($code in $baselineIssues.Keys) {
  if (-not $actualIssues.ContainsKey($code)) {
    throw "Issue code '$code' missing from actual diagnostics."
  }
  if ($baselineIssues[$code] -ne $actualIssues[$code]) {
    throw "issues[$code] mismatch: baseline $($baselineIssues[$code]) vs actual $($actualIssues[$code])."
  }
}

Write-Host "Render diagnostics stable within +/- 5% (summary written to $summaryPath)."

# Validate diagnostics fail-level gating by forcing a lane warning and expecting exit code 65
$failEnv = @{
  VDG_DIAG_LANE_WARN = "0.01"
  VDG_DIAG_LANE_ERR  = "0.90"
  VDG_DIAG_PAGE_WARN = "0.01"
  VDG_DIAG_FAIL_LEVEL = "warning"
}
$exit = Invoke-Dotnet -Arguments $renderArgs -Environment $failEnv -AcceptExitCodes @(65, 70)
if (($exit -ne 65) -and ($exit -ne 70)) {
  throw "Expected diagnostics fail-level gating to exit with 65 or 70, but received $exit."
}

$gatedDiag = Get-Content -Raw -Path $diagPath | ConvertFrom-Json
if (-not ($gatedDiag.issues | Where-Object { $_.code -eq 'LaneCrowding' -or $_.code -eq 'PageCrowding' })) {
  throw "Diagnostics gating test did not emit expected crowding issues."
}

Write-Host "Diagnostics fail-level gating verified (render exited with $exit when thresholds were tightened)."
