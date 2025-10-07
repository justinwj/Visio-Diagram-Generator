Param(
  [Parameter(Mandatory=$true)] [string]$InputPath,
  [string]$SchemaPath = "shared/Config/vbaIr.schema.json"
)

function Fail($msg) { Write-Error $msg; exit 1 }
if (!(Test-Path $InputPath)) { Fail "Input file not found: $InputPath" }

try { $json = Get-Content -Raw -Path $InputPath | ConvertFrom-Json -ErrorAction Stop }
catch { Fail "Invalid JSON: $($_.Exception.Message)" }

# Minimal validation (schema-friendly but not a full JSON Schema check)
if (-not $json.irSchemaVersion) { Fail "Missing 'irSchemaVersion'" }
if ($json.irSchemaVersion -ne "0.1") { Write-Warning "irSchemaVersion '$($json.irSchemaVersion)' != '0.1'" }
if (-not $json.project) { Fail "Missing 'project'" }
if (-not $json.project.name) { Fail "Missing 'project.name'" }
if (-not $json.project.modules) { Fail "Missing 'project.modules'" }

foreach ($m in $json.project.modules) {
  if (-not $m.id) { Fail "Module missing 'id'" }
  if (-not $m.name) { Fail "Module missing 'name'" }
  if (-not $m.kind) { Fail "Module missing 'kind'" }
  if (-not $m.procedures) { Fail "Module $($m.name) missing 'procedures'" }
  foreach ($p in $m.procedures) {
    if (-not $p.id) { Fail "Procedure missing 'id' in module $($m.name)" }
    if (-not $p.name) { Fail "Procedure missing 'name' in module $($m.name)" }
    if (-not $p.kind) { Fail "Procedure missing 'kind' in module $($m.name)" }
  }
}

Write-Host "IR OK: $InputPath" -ForegroundColor Green
exit 0

