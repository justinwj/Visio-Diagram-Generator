Param(
  [string]$In = 'tests/fixtures/vba/cross_module_calls',
  [string]$Mode = 'callgraph',
  [switch]$IncludeUnknown,
  [int]$TimeoutMs = 0,
  [string]$OutJson = 'out/perf/perf.json',
  [switch]$KeepHistory,
  [switch]$VerboseExtras,
  [ValidateSet('cross-module', 'massive-callgraph')]
  [string]$Preset
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotnetCommand {
  param(
    [string[]]$Arguments
  )

  $stdoutFile = [System.IO.Path]::GetTempFileName()
  $stderrFile = [System.IO.Path]::GetTempFileName()
  $sw = [System.Diagnostics.Stopwatch]::StartNew()

  try {
    $process = Start-Process -FilePath 'dotnet' `
                             -ArgumentList $Arguments `
                             -RedirectStandardOutput $stdoutFile `
                             -RedirectStandardError $stderrFile `
                             -NoNewWindow `
                             -Wait `
                             -PassThru
    $sw.Stop()
    $stdout = Get-Content -Path $stdoutFile -Raw
    $stderr = Get-Content -Path $stderrFile -Raw

    return [pscustomobject]@{
      ExitCode = $process.ExitCode
      Ms       = [int][math]::Round($sw.Elapsed.TotalMilliseconds)
      Stdout   = $stdout
      Stderr   = $stderr
    }
  }
  finally {
    Remove-Item -ErrorAction SilentlyContinue $stdoutFile, $stderrFile
  }
}

if ($Preset) {
  switch ($Preset) {
    'cross-module' { $In = 'tests/fixtures/vba/cross_module_calls' }
    'massive-callgraph' { $In = 'benchmarks/vba/massive_callgraph' }
  }
}

if ($KeepHistory) {
  $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd_HHmmssfff')
  $baseDir = if ([string]::IsNullOrWhiteSpace($OutJson)) { 'out/perf' } else { Split-Path -Parent $OutJson }
  if ([string]::IsNullOrWhiteSpace($baseDir)) { $baseDir = 'out/perf' }
  New-Item -ItemType Directory -Force -Path $baseDir | Out-Null
  $OutJson = Join-Path $baseDir "perf_$stamp.json"
} else {
  if ([string]::IsNullOrWhiteSpace($OutJson)) { $OutJson = 'out/perf/perf.json' }
  $targetDir = Split-Path -Parent $OutJson
  if ([string]::IsNullOrWhiteSpace($targetDir)) { $targetDir = 'out/perf' }
  New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
}

New-Item -ItemType Directory -Force -Path out/tmp | Out-Null
$ir = 'out/tmp/perf_ir.json'
$dj = 'out/tmp/perf_diag.json'

Write-Host "vba2json: $In" -ForegroundColor Cyan
$vbaArgs = @('run', '--project', 'src/VDG.VBA.CLI', '--', 'vba2json', '--in', $In, '--out', $ir)
$r1 = Invoke-DotnetCommand -Arguments $vbaArgs
if ($r1.ExitCode -ne 0) { Write-Error "vba2json failed: $($r1.Stderr)"; exit $r1.ExitCode }
Write-Host ("vba2json: {0} ms" -f $r1.Ms)

$ir2Args = @('run', '--project', 'src/VDG.VBA.CLI', '--', 'ir2diagram', '--in', $ir, '--out', $dj, '--mode', $Mode)
if ($IncludeUnknown) { $ir2Args += '--include-unknown' }
if ($TimeoutMs -gt 0) { $ir2Args += @('--timeout', "$TimeoutMs") }

Write-Host "ir2diagram: $Mode" -ForegroundColor Cyan
$r2 = Invoke-DotnetCommand -Arguments $ir2Args
Write-Host ("ir2diagram: {0} ms (exit {1})" -f $r2.Ms, $r2.ExitCode)
if ($r2.ExitCode -ne 0) { Write-Error "ir2diagram failed: $($r2.Stderr)"; exit $r2.ExitCode }

$shellProc = Get-Process -Id $PID
$ws = $shellProc.WorkingSet64
$priv = $shellProc.PrivateMemorySize64
Write-Host ("pwsh working set: {0:N0} bytes" -f $ws)

# Parse summary metrics from ir2diagram stdout if present
$modules = $null; $procedures = $null; $edges = $null; $dynSkipped = $null; $dynIncluded = $null
$progressEmits = $null; $progressLastMs = $null
$summaryMatch = [regex]::Match(
  $r2.Stdout,
  'modules:(?<m>\d+)\s+procedures:(?<p>\d+)\s+edges:(?<e>\d+)\s+dynamicSkipped:(?<ds>\d+)\s+dynamicIncluded:(?<di>\d+)(?:\s+progressEmits:(?<pe>\d+)\s+progressLastMs:(?<pl>\d+))?',
  'IgnoreCase'
)
if ($summaryMatch.Success) {
  $modules = [int]$summaryMatch.Groups['m'].Value
  $procedures = [int]$summaryMatch.Groups['p'].Value
  $edges = [int]$summaryMatch.Groups['e'].Value
  $dynSkipped = [int]$summaryMatch.Groups['ds'].Value
  $dynIncluded = [int]$summaryMatch.Groups['di'].Value
  if ($summaryMatch.Groups['pe'].Success) { $progressEmits = [int]$summaryMatch.Groups['pe'].Value }
  if ($summaryMatch.Groups['pl'].Success) { $progressLastMs = [int]$summaryMatch.Groups['pl'].Value }
}

# File sizes and diagram counts
$irSize = (Get-Item $ir).Length
$djSize = (Get-Item $dj).Length
$nodesCount = $null; $edgesCount = $null
try {
  $djObj = Get-Content -Raw -Path $dj | ConvertFrom-Json
  $nodesCount = @($djObj.nodes).Count
  $edgesCount = @($djObj.edges).Count
} catch { }

$payload = [ordered]@{
  timestampUtc = (Get-Date).ToUniversalTime().ToString('o')
  input = $In
  mode = $Mode
  includeUnknown = [bool]$IncludeUnknown
  timeoutMs = if ($TimeoutMs -gt 0) { $TimeoutMs } else { $null }
  vba2json = [ordered]@{ ms = $r1.Ms; exit = $r1.ExitCode; outBytes = $irSize }
  ir2diagram = [ordered]@{
    ms = $r2.Ms; exit = $r2.ExitCode; outBytes = $djSize
    summary = if ($summaryMatch.Success) {
      [ordered]@{
        modules = $modules
        procedures = $procedures
        edges = $edges
        dynamicSkipped = $dynSkipped
        dynamicIncluded = $dynIncluded
      }
    } else { $null }
    counts = if ($nodesCount -ne $null -or $edgesCount -ne $null) { [ordered]@{ nodes = $nodesCount; edges = $edgesCount } } else { $null }
  }
  process = [ordered]@{
    pid = $PID
    workingSetBytes = $ws
    machine = $env:COMPUTERNAME
    os = $env:OS
    pwshVersion = $PSVersionTable.PSVersion.ToString()
  }
}

$json = ($payload | ConvertTo-Json -Depth 6)
Set-Content -Path $OutJson -Value $json -Encoding UTF8
Write-Host "Wrote metrics JSON: $OutJson"

if ($VerboseExtras -or $progressEmits -ne $null -or $progressLastMs -ne $null) {
  $extra = [ordered]@{
    summaryProgress = if ($progressEmits -ne $null -or $progressLastMs -ne $null) {
      [ordered]@{
        emits = $progressEmits
        lastMs = $progressLastMs
      }
    } else { $null }
    shellProcess = [ordered]@{
      workingSetBytes = $ws
      privateBytes = $priv
    }
    stdout = if ($VerboseExtras) { $r2.Stdout } else { $null }
    stderr = if ($VerboseExtras) { $r2.Stderr } else { $null }
  }

  $extraPath = [System.IO.Path]::ChangeExtension($OutJson, '.extra.json')
  $extra | ConvertTo-Json -Depth 6 | Set-Content -Path $extraPath -Encoding UTF8
  Write-Host "Wrote extra metrics JSON: $extraPath"
}

$summaryPath = [System.Environment]::GetEnvironmentVariable('GITHUB_STEP_SUMMARY')
if (-not [string]::IsNullOrWhiteSpace($summaryPath)) {
  $lines = @()
  $lines += "### IR->Diagram Perf (`$Mode`)"
  $lines += ""
  $lines += (' - ' + [string]::Format('Input: `{0}`', $In))
  $lines += (' - ' + [string]::Format('vba2json: {0} ms (exit {1})', $r1.Ms, $r1.ExitCode))
  $lines += (' - ' + [string]::Format('ir2diagram: {0} ms (exit {1})', $r2.Ms, $r2.ExitCode))
  if ($summaryMatch.Success) {
    $lines += (' - ' + [string]::Format('Diagram summary: modules={0}, procedures={1}, edges={2}', $modules, $procedures, $edges))
  }
  $lines += (' - ' + [string]::Format('Diagram bytes: {0}', $djSize))
  $lines += (' - ' + [string]::Format('Shell working set: {0} MB', [math]::Round($ws / 1MB, 2)))
  $lines += ""
  $lines += ([string]::Format('_metrics written to {0}_', $OutJson))
  Add-Content -Path $summaryPath -Value ($lines -join [Environment]::NewLine)
}

Write-Host "Done. Outputs in out/tmp" -ForegroundColor Green



