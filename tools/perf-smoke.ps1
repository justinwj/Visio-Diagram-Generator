Param(
  [string]$In = 'tests/fixtures/vba/cross_module_calls',
  [string]$Mode = 'callgraph',
  [switch]$IncludeUnknown,
  [int]$TimeoutMs = 0,
  [string]$OutJson = 'out/perf/perf.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Run-Cli {
  param([string[]]$Args)
  $start = [System.Diagnostics.Stopwatch]::StartNew()
  $psi = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
  $psi.ArgumentList.Add('run'); $psi.ArgumentList.Add('--project'); $psi.ArgumentList.Add('src/VDG.VBA.CLI'); $psi.ArgumentList.Add('--')
  foreach ($a in $Args) { $psi.ArgumentList.Add($a) }
  $psi.UseShellExecute = $false; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
  $p = [System.Diagnostics.Process]::Start($psi)
  $out = $p.StandardOutput.ReadToEnd(); $err = $p.StandardError.ReadToEnd(); $p.WaitForExit()
  $elapsed = $start.Elapsed.TotalMilliseconds
  [pscustomobject]@{ ExitCode = $p.ExitCode; Ms = [int]$elapsed; Stdout = $out; Stderr = $err }
}

New-Item -ItemType Directory -Force -Path out/tmp | Out-Null
$outDir = Split-Path -Parent $OutJson
if ([string]::IsNullOrWhiteSpace($outDir)) { $outDir = 'out/perf' }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$ir = 'out/tmp/perf_ir.json'
$dj = 'out/tmp/perf_diag.json'

Write-Host "vba2json: $In" -ForegroundColor Cyan
$r1 = Run-Cli @('vba2json','--in', $In, '--out', $ir)
if ($r1.ExitCode -ne 0) { Write-Error "vba2json failed: $($r1.Stderr)"; exit $r1.ExitCode }
Write-Host ("vba2json: {0} ms" -f $r1.Ms)

$args = @('ir2diagram','--in', $ir, '--out', $dj, '--mode', $Mode)
if ($IncludeUnknown) { $args += '--include-unknown' }
if ($TimeoutMs -gt 0) { $args += @('--timeout', "$TimeoutMs") }

Write-Host "ir2diagram: $Mode" -ForegroundColor Cyan
$r2 = Run-Cli $args
Write-Host ("ir2diagram: {0} ms (exit {1})" -f $r2.Ms, $r2.ExitCode)
if ($r2.ExitCode -ne 0) { Write-Error "ir2diagram failed: $($r2.Stderr)"; exit $r2.ExitCode }

# Rough memory snapshot (process working set)
$proc = Get-Process -Id $PID
$ws = $proc.WorkingSet64
Write-Host ("pwsh working set: {0:N0} bytes" -f $ws)

# Parse summary metrics from ir2diagram stdout if present
$modules = $null; $procedures = $null; $edges = $null; $dynSkipped = $null; $dynIncluded = $null
$progressEmits = $null; $progressLastMs = $null
$m = [regex]::Match(
  $r2.Stdout,
  'modules:(?<m>\d+)\s+procedures:(?<p>\d+)\s+edges:(?<e>\d+)\s+dynamicSkipped:(?<ds>\d+)\s+dynamicIncluded:(?<di>\d+)(?:\s+progressEmits:(?<pe>\d+)\s+progressLastMs:(?<pl>\d+))?',
  'IgnoreCase'
)
if ($m.Success) {
  $modules = [int]$m.Groups['m'].Value
  $procedures = [int]$m.Groups['p'].Value
  $edges = [int]$m.Groups['e'].Value
  $dynSkipped = [int]$m.Groups['ds'].Value
  $dynIncluded = [int]$m.Groups['di'].Value
  if ($m.Groups['pe'].Success) { $progressEmits = [int]$m.Groups['pe'].Value }
  if ($m.Groups['pl'].Success) { $progressLastMs = [int]$m.Groups['pl'].Value }
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
    summary = if ($m.Success) {
      $summary = [ordered]@{
        modules = $modules; procedures = $procedures; edges = $edges; dynamicSkipped = $dynSkipped; dynamicIncluded = $dynIncluded
      }
      if ($progressEmits -ne $null -or $progressLastMs -ne $null) {
        $summary['progress'] = [ordered]@{
          emits = $progressEmits
          lastMs = $progressLastMs
        }
      }
      $summary
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
Write-Host "Done. Outputs in out/tmp" -ForegroundColor Green
