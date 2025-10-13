Param(
  [string]$In = 'tests/fixtures/vba/cross_module_calls',
  [string]$Mode = 'callgraph',
  [switch]$IncludeUnknown,
  [int]$TimeoutMs = 0
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
$ws = (Get-Process -Id $PID).WorkingSet64
Write-Host ("pwsh working set: {0:N0} bytes" -f $ws)
Write-Host "Done. Outputs in out/tmp" -ForegroundColor Green

