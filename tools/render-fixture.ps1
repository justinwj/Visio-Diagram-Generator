Param(
  [string[]]$FixtureName,
  [string]$OutputRoot = 'out/fixtures',
  [string]$Cli,
  [switch]$Update,
  [string]$Note = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

$sanitizedNote = $Note
if (-not [string]::IsNullOrWhiteSpace($sanitizedNote)) {
  $sanitizedNote = $sanitizedNote -replace '\|', '/'
}

$fixtureMatrix = @(
  [pscustomobject]@{
    Name   = 'hello_world'
    Source = 'tests/fixtures/vba/hello_world'
    Modes  = @('callgraph')
  },
  [pscustomobject]@{
    Name   = 'cross_module_calls'
    Source = 'tests/fixtures/vba/cross_module_calls'
    Modes  = @('callgraph', 'module-structure')
  },
  [pscustomobject]@{
    Name   = 'events_and_forms'
    Source = 'tests/fixtures/vba/events_and_forms'
    Modes  = @('callgraph')
  }
)

if ($FixtureName -and $FixtureName.Length -gt 0) {
  $selectedNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
  foreach ($name in $FixtureName) { $null = $selectedNames.Add($name) }
  $fixtureMatrix = $fixtureMatrix | Where-Object { $selectedNames.Contains($_.Name) }
  if ($fixtureMatrix.Count -eq 0) {
    throw "No fixtures matched selection: $FixtureName"
  }
}

function Invoke-Dotnet {
  param(
    [string[]]$Arguments,
    [int[]]$AcceptExitCodes = @(0)
  )
  Write-Host ("Running: dotnet {0}" -f ($Arguments -join ' '))
  Push-Location $repoRoot
  try {
    & dotnet @Arguments
    $exitCode = $LASTEXITCODE
  }
  finally {
    Pop-Location
  }
  if (-not $AcceptExitCodes.Contains($exitCode)) {
    throw "dotnet $($Arguments -join ' ') exited with code $exitCode."
  }
}

function Resolve-CliPath {
  param([string]$Path)
  if (-not [string]::IsNullOrWhiteSpace($Path) -and (Test-Path $Path)) {
    return (Resolve-Path $Path).Path
  }
  $candidates = @(
    'src/VDG.CLI/bin/Release/net48/VDG.CLI.exe',
    'src/VDG.CLI/bin/Debug/net48/VDG.CLI.exe'
  )
  foreach ($candidate in $candidates) {
    $full = Join-Path $repoRoot $candidate
    if (Test-Path $full) {
      return (Resolve-Path $full).Path
    }
  }
  throw "Unable to locate VDG.CLI.exe. Build the project or pass -Cli <path>."
}

function Ensure-Directory {
  param([string]$Path)
  if (-not (Test-Path $Path)) {
    $null = New-Item -ItemType Directory -Path $Path -Force
  }
}

function Get-RelativePath {
  param([string]$Path)
  if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
  $full = [System.IO.Path]::GetFullPath($Path, $repoRoot)
  return [System.IO.Path]::GetRelativePath($repoRoot, $full)
}

function Get-FileSha256 {
  param([string]$Path)
  return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Write-LedgerEntries {
  param(
    [System.Collections.Generic.List[object]]$Entries,
    [string]$Note
  )
  if ($Entries.Count -eq 0) { return }
  $ledgerPath = Join-Path $repoRoot 'plan docs/fixtures_log.md'
  if (-not (Test-Path $ledgerPath)) {
    throw "Ledger file not found: $ledgerPath"
  }
  $timestamp = (Get-Date).ToUniversalTime().ToString('o')
  $noteField = if ([string]::IsNullOrWhiteSpace($Note)) { '' } else { $Note }
  foreach ($entry in $Entries) {
    $line = "| $timestamp | $($entry.Fixture) | $($entry.Mode) | $($entry.IR) | $($entry.Diagram) | $($entry.Diagnostics) | $($entry.Vsdx) | $noteField |"
    Add-Content -Path $ledgerPath -Value $line
  }
}

function Write-MetadataSnapshot {
  param(
    [System.Collections.Generic.List[object]]$Entries,
    [System.Collections.IEnumerable]$FixtureMatrix,
    [string]$Note
  )
  if ($Entries.Count -eq 0) { return }

  $metadataPath = Join-Path $repoRoot 'plan docs/fixtures_metadata.json'
  $timestamp = (Get-Date).ToUniversalTime().ToString('o')

  Push-Location $repoRoot
  try {
    $commit = (& git rev-parse HEAD 2>$null).Trim()
  }
  catch {
    $commit = ''
  }
  finally {
    Pop-Location
  }

  $fixtures = @()
  foreach ($group in ($Entries | Group-Object Fixture | Sort-Object Name)) {
    $matrixEntry = $FixtureMatrix | Where-Object { $_.Name -eq $group.Name } | Select-Object -First 1
    $fixtureMeta = [ordered]@{
      name   = $group.Name
      source = if ($matrixEntry) { $matrixEntry.Source } else { $group.Name }
      modes  = @()
    }
    foreach ($item in ($group.Group | Sort-Object Mode)) {
      $modeMeta = [ordered]@{
        mode    = $item.Mode
        hashes  = [ordered]@{
          ir          = $item.IR
          diagram     = $item.Diagram
          diagnostics = $item.Diagnostics
          vsdx        = $item.Vsdx
        }
      }
      if ($item.Paths) { $modeMeta.paths = $item.Paths }
      if ($item.Commands) { $modeMeta.commands = $item.Commands }
      $fixtureMeta.modes += $modeMeta
    }
    $fixtures += $fixtureMeta
  }

  $metadata = [ordered]@{
    generatedAtUtc = $timestamp
    commit         = $commit
    note           = $Note
    fixtures       = $fixtures
  }

  $json = $metadata | ConvertTo-Json -Depth 8
  Set-Content -Path $metadataPath -Value $json -Encoding UTF8
}

$cliPath = Resolve-CliPath -Path $Cli
$outputRootFull = Join-Path $repoRoot $OutputRoot
Ensure-Directory -Path $outputRootFull

$cliRelative = Get-RelativePath $cliPath

$results = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[object]]::new()

foreach ($fixtureEntry in $fixtureMatrix) {
  $sourcePath = Join-Path $repoRoot $fixtureEntry.Source
  if (-not (Test-Path $sourcePath)) {
    throw "Fixture source not found: $sourcePath"
  }

  foreach ($mode in $fixtureEntry.Modes) {
    $workDir = Join-Path $outputRootFull "$($fixtureEntry.Name)/$mode"
    Ensure-Directory -Path $workDir

    $baseName = "{0}.{1}" -f $fixtureEntry.Name, $mode
    $irPath = Join-Path $workDir "$baseName.ir.json"
    $diagramPath = Join-Path $workDir "$baseName.diagram.json"
    $diagnosticsPath = Join-Path $workDir "$baseName.diagnostics.json"
    $vsdxPath = Join-Path $workDir "$baseName.vsdx"

    $sourceRel = Get-RelativePath $sourcePath
    $irRel = Get-RelativePath $irPath
    $diagramRel = Get-RelativePath $diagramPath
    $diagnosticsRel = Get-RelativePath $diagnosticsPath
    $vsdxRel = Get-RelativePath $vsdxPath

    $goldenDir = Join-Path $repoRoot "tests/fixtures/render/$($fixtureEntry.Name)/$mode"
    Ensure-Directory -Path $goldenDir

    $goldenFiles = @{
      IR = Join-Path $goldenDir "$baseName.ir.json"
      Diagram = Join-Path $goldenDir "$baseName.diagram.json"
      Diagnostics = Join-Path $goldenDir "$baseName.diagnostics.json"
      Vsdx = Join-Path $goldenDir "$baseName.vsdx"
    }

    $goldenRel = [ordered]@{
      IR = Get-RelativePath $goldenFiles.IR
      Diagram = Get-RelativePath $goldenFiles.Diagram
      Diagnostics = Get-RelativePath $goldenFiles.Diagnostics
      Vsdx = Get-RelativePath $goldenFiles.Vsdx
    }

    $commands = @(
      [pscustomobject]@{
        name    = 'vba2json'
        command = "dotnet run --project src/VDG.VBA.CLI -- vba2json --in $sourceRel --out $irRel"
      },
      [pscustomobject]@{
        name    = 'ir2diagram'
        command = "dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in $irRel --out $diagramRel --mode $mode"
      },
      [pscustomobject]@{
        name        = 'render'
        command     = "$cliRelative --diag-json $diagnosticsRel $diagramRel $vsdxRel"
        environment = [ordered]@{ VDG_SKIP_RUNNER = '1' }
      }
    )

    foreach ($file in @($irPath, $diagramPath, $diagnosticsPath, $vsdxPath)) {
      if (Test-Path $file) {
        Remove-Item -Path $file -Force
      }
    }

    Invoke-Dotnet -Arguments @('run', '--project', 'src/VDG.VBA.CLI', '--', 'vba2json', '--in', $sourcePath, '--out', $irPath)
    Invoke-Dotnet -Arguments @('run', '--project', 'src/VDG.VBA.CLI', '--', 'ir2diagram', '--in', $irPath, '--out', $diagramPath, '--mode', $mode)

    $previousSkip = [System.Environment]::GetEnvironmentVariable('VDG_SKIP_RUNNER', [System.EnvironmentVariableTarget]::Process)
    try {
      [System.Environment]::SetEnvironmentVariable('VDG_SKIP_RUNNER', '1', [System.EnvironmentVariableTarget]::Process)
      $psi = New-Object System.Diagnostics.ProcessStartInfo
      $psi.FileName = $cliPath
      $psi.ArgumentList.Add('--diag-json')
      $psi.ArgumentList.Add($diagnosticsPath)
      $psi.ArgumentList.Add($diagramPath)
      $psi.ArgumentList.Add($vsdxPath)
      $psi.WorkingDirectory = $repoRoot
      $psi.UseShellExecute = $false
      $process = [System.Diagnostics.Process]::Start($psi)
      $process.WaitForExit()
      if ($process.ExitCode -ne 0) {
        throw "VDG.CLI exited with code $($process.ExitCode) while rendering $($fixtureEntry.Name) ($mode)."
      }
    }
    finally {
      [System.Environment]::SetEnvironmentVariable('VDG_SKIP_RUNNER', $previousSkip, [System.EnvironmentVariableTarget]::Process)
    }

    $irHash = Get-FileSha256 -Path $irPath
    $diagramHash = Get-FileSha256 -Path $diagramPath
    $diagnosticsHash = Get-FileSha256 -Path $diagnosticsPath
    $vsdxHash = Get-FileSha256 -Path $vsdxPath

    $paths = [ordered]@{
      temp = [ordered]@{
        ir          = $irRel
        diagram     = $diagramRel
        diagnostics = $diagnosticsRel
        vsdx        = $vsdxRel
      }
      golden = [ordered]@{
        ir          = $goldenRel.IR
        diagram     = $goldenRel.Diagram
        diagnostics = $goldenRel.Diagnostics
        vsdx        = $goldenRel.Vsdx
      }
    }

    $results.Add([pscustomobject]@{
      Fixture      = $fixtureEntry.Name
      Mode         = $mode
      IR           = $irHash
      Diagram      = $diagramHash
      Diagnostics  = $diagnosticsHash
      Vsdx         = $vsdxHash
      WorkDir      = $workDir
      Paths        = $paths
      Commands     = $commands
    })

    if ($Update) {
      Copy-Item -Path $irPath -Destination $goldenFiles.IR -Force
      Copy-Item -Path $diagramPath -Destination $goldenFiles.Diagram -Force
      Copy-Item -Path $diagnosticsPath -Destination $goldenFiles.Diagnostics -Force
      Copy-Item -Path $vsdxPath -Destination $goldenFiles.Vsdx -Force
      continue
    }

    foreach ($entry in @(
        @{ Kind = 'IR';           Generated = $irPath;          Golden = $goldenFiles.IR;          Hash = $irHash },
        @{ Kind = 'Diagram';      Generated = $diagramPath;     Golden = $goldenFiles.Diagram;     Hash = $diagramHash },
        @{ Kind = 'Diagnostics';  Generated = $diagnosticsPath; Golden = $goldenFiles.Diagnostics; Hash = $diagnosticsHash },
        @{ Kind = 'VSDX';         Generated = $vsdxPath;        Golden = $goldenFiles.Vsdx;        Hash = $vsdxHash }
      )) {
      $goldenPath = $entry.Golden
      if (-not (Test-Path $goldenPath)) {
        $failures.Add([pscustomobject]@{
          Fixture = $fixtureEntry.Name
          Mode    = $mode
          Kind    = $entry.Kind
          Reason  = "Golden file missing ($goldenPath). Run with -Update to create it."
        })
        continue
      }
      $goldenHash = Get-FileSha256 -Path $goldenPath
      if ($goldenHash -ne $entry.Hash) {
        $failures.Add([pscustomobject]@{
          Fixture = $fixtureEntry.Name
          Mode    = $mode
          Kind    = $entry.Kind
          Reason  = "Hash mismatch (expected $goldenHash, actual $($entry.Hash))."
          Golden  = $goldenPath
          Actual  = $entry.Generated
        })
      }
    }
  }
}

if ($Update) {
  Write-LedgerEntries -Entries $results -Note $sanitizedNote
  Write-MetadataSnapshot -Entries $results -FixtureMatrix $fixtureMatrix -Note $sanitizedNote
  foreach ($result in $results) {
    Write-Host ("Updated fixture {0} ({1}) -> IR {2} | Diagram {3} | Diagnostics {4} | VSDX {5}" -f `
      $result.Fixture, $result.Mode, $result.IR, $result.Diagram, $result.Diagnostics, $result.Vsdx)
  }
  Write-Host "Ledger updated at plan docs/fixtures_log.md."
  Write-Host "Metadata snapshot written to plan docs/fixtures_metadata.json."
  return
}

if ($failures.Count -gt 0) {
  foreach ($failure in $failures) {
    if ($failure.Golden -and $failure.Actual) {
      Write-Error ("{0}/{1} {2}: {3}`nDiff hint: git diff --no-index -- {4} {5}" -f `
        $failure.Fixture, $failure.Mode, $failure.Kind, $failure.Reason, $failure.Golden, $failure.Actual)
    }
    else {
      Write-Error ("{0}/{1} {2}: {3}" -f $failure.Fixture, $failure.Mode, $failure.Kind, $failure.Reason)
    }
  }
  throw "Fixture drift detected. Inspect the diff hints above."
}

foreach ($result in $results) {
  Write-Host ("Verified fixture {0} ({1}) hashes -> IR {2}, Diagram {3}, Diagnostics {4}, VSDX {5}" -f `
    $result.Fixture, $result.Mode, $result.IR, $result.Diagram, $result.Diagnostics, $result.Vsdx)
}
Write-Host "All fixtures stable."
