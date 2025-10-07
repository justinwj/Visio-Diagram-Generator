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

foreach ($m in $ir.project.modules) {
  $tier = TierForKind $m.kind
  $containers += [ordered]@{
    id = $m.id
    label = $m.name
    tier = $tier
  }
  foreach ($p in $m.procedures) {
    $nodes += [ordered]@{
      id = $p.id
      label = $p.id
      tier = $tier
      containerId = $m.id
      metadata = @{ 'code.module'=$m.name; 'code.proc'=$p.name; 'code.kind'=$p.kind; 'code.access'=$p.access }
    }
    foreach ($c in $p.calls) {
      if (-not [string]::IsNullOrWhiteSpace($c.target) -and $c.target -ne '~unknown') {
        $edges += [ordered]@{ sourceId=$p.id; targetId=$c.target; label='call'; metadata=@{ 'code.edge'='call' } }
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

