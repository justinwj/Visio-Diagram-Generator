Param(
  [Parameter(Mandatory=$true)] [Alias('in')] [string]$InputIr,
  [Alias('out')] [string]$OutputDiagram
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $InputIr)) { throw "IR file not found: $InputIr" }
$ir = Get-Content -Raw -Path $InputIr | ConvertFrom-Json

function TierForKind([string]$k) {
  switch ($k) {
    'Form' { 'Forms' }
    'Class' { 'Classes' }
    default { 'Modules' }
  }
}

$tiers = @('Forms','Classes','Modules')
$nodes = @()
$edges = @()
$containers = @()

$orderedModules = @($ir.project.modules | Sort-Object -Property `
  @{ Expression = { ([string]$_.name).ToLowerInvariant() } }, `
  @{ Expression = { ([string]$_.id).ToLowerInvariant() } })

foreach ($m in $orderedModules) {
  $tier = TierForKind $m.kind
  $containers += [ordered]@{
    id = $m.id
    label = $m.name
    tier = $tier
  }
  $orderedProcedures = @($m.procedures | Sort-Object -Property `
    @{ Expression = { ([string]$_.name).ToLowerInvariant() } }, `
    @{ Expression = { ([string]$_.id).ToLowerInvariant() } })

  foreach ($p in $orderedProcedures) {
    $nodeMeta = [ordered]@{}
    if ($m.name) { $nodeMeta['code.module'] = $m.name }
    if ($p.name) { $nodeMeta['code.proc'] = $p.name }
    if ($p.kind) { $nodeMeta['code.kind'] = $p.kind }
    if ($p.access) { $nodeMeta['code.access'] = $p.access }
    if ($p.locs) {
      if ($p.locs.file) { $nodeMeta['code.locs.file'] = $p.locs.file }
      if ($p.locs.startLine) { $nodeMeta['code.locs.startLine'] = [string]$p.locs.startLine }
      if ($p.locs.endLine) { $nodeMeta['code.locs.endLine'] = [string]$p.locs.endLine }
    }
    $label = if ($p.name) { $p.name } else { $p.id }
    $nodes += [ordered]@{
      id = $p.id
      label = $label
      tier = $tier
      containerId = $m.id
      metadata = $nodeMeta
    }
    foreach ($c in ($p.calls ?? @())) {
      if (-not [string]::IsNullOrWhiteSpace($c.target) -and $c.target -ne '~unknown') {
        $edgeMeta = [ordered]@{ 'code.edge' = 'call' }
        if ($c.branch) { $edgeMeta['code.branch'] = $c.branch }
        if ($c.site) {
          if ($c.site.module) { $edgeMeta['code.site.module'] = $c.site.module }
          if ($c.site.file) { $edgeMeta['code.site.file'] = $c.site.file }
          if ($c.site.line) { $edgeMeta['code.site.line'] = [string]$c.site.line }
        }
        $edges += [ordered]@{ sourceId = $p.id; targetId = $c.target; label = 'call'; metadata = $edgeMeta }
      }
    }
  }
}

$diagram = [ordered]@{
  schemaVersion = '1.2'
  layout = @{ tiers = $tiers; page = @{ heightIn = 8.5; marginIn = 0.5 }; spacing = @{ horizontal = 1.2; vertical = 0.6 } }
  nodes = $nodes
  edges = $edges
  containers = $containers
}

$json = $diagram | ConvertTo-Json -Depth 10
if ([string]::IsNullOrWhiteSpace($OutputDiagram)) { Write-Output $json }
else { $dir = Split-Path -Parent $OutputDiagram; if ($dir) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }; Set-Content -Path $OutputDiagram -Value $json -Encoding UTF8 }

