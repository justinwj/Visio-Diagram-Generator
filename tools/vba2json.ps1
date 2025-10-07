Param(
  [Parameter(Mandatory=$true)] [Alias('in')] [string]$InputFolder,
  [Alias('out')] [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ModuleKindFromPath([string]$path) {
  switch -Regex ([IO.Path]::GetExtension($path).ToLowerInvariant()) {
    '\\.frm$' { 'Form'; break }
    '\\.cls$' { 'Class'; break }
    default    { 'Module' }
  }
}

function Parse-Procedures([string[]]$lines, [string]$moduleName, [string]$filePath) {
  $procs = @()
  $sigRe = '^(?<indent>\s*)(?<access>Public|Private|Friend)?\s*(?<static>Static\s*)?(?<kind>Sub|Function|Property\s+Get|Property\s+Let|Property\s+Set)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)'
  for ($i = 0; $i -lt $lines.Length; $i++) {
    $m = [Regex]::Match($lines[$i], $sigRe, 'IgnoreCase')
    if (-not $m.Success) { continue }
    $kindRaw = $m.Groups['kind'].Value
    $kind = switch -Regex ($kindRaw.Trim()) {
      'Property\s+Get' { 'PropertyGet' }
      'Property\s+Let' { 'PropertyLet' }
      'Property\s+Set' { 'PropertySet' }
      'Sub' { 'Sub' }
      'Function' { 'Function' }
    }
    $name = $m.Groups['name'].Value
    $access = if ($m.Groups['access'].Success) { $m.Groups['access'].Value } else { $null }
    $isStatic = $m.Groups['static'].Success
    $start = $i + 1 # 1-based
    $end = $start
    $endToken = switch ($kind) {
      'Function' { 'End Function' }
      'Sub' { 'End Sub' }
      default { 'End Property' }
    }
    for ($j = $i + 1; $j -lt $lines.Length; $j++) {
      if ($lines[$j] -match '^\s*' + [Regex]::Escape($endToken) + '\b') { $end = $j + 1; break }
    }
    if ($end -le $start) { $end = [Math]::Min($lines.Length, $start + 1) }

    # Extract naive calls Module.Proc within procedure body
    $calls = @()
    for ($k = $i; $k -lt [Math]::Min($lines.Length, ($end-1)); $k++) {
      $line = $lines[$k]
      foreach ($cm in [Regex]::Matches($line, '([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)', 'IgnoreCase')) {
        $target = $cm.Groups[1].Value + '.' + $cm.Groups[2].Value
        $calls += @{ target=$target; isDynamic=$false; site=@{ module=$moduleName; file=$filePath; line=($k+1) } }
      }
      if ($line -match '\b(CallByName|Application\.Run)\b') {
        $calls += @{ target='~unknown'; isDynamic=$true; site=@{ module=$moduleName; file=$filePath; line=($k+1) } }
      }
    }

    $proc = [ordered]@{
      id        = "$moduleName.$name"
      name      = $name
      kind      = $kind
      access    = $access
      static    = $isStatic
      params    = @()
      locs      = @{ file=$filePath; startLine=$start; endLine=$end }
      calls     = $calls
      metrics   = @{ lines = ($end - $start + 1) }
    }
    $procs += $proc
    $i = $end - 1
  }
  return ,$procs
}

if (-not (Test-Path $InputFolder)) { throw "Input folder not found: $InputFolder" }
$root = (Resolve-Path $InputFolder).Path
$files = Get-ChildItem -Recurse -File -Include *.bas,*.cls,*.frm -Path $root
if ($files.Count -eq 0) { Write-Warning "No .bas/.cls/.frm files under $root" }

$modules = @()
foreach ($f in $files) {
  $lines = Get-Content -Path $f.FullName -Raw | Select-String -Pattern "\r?\n" -AllMatches | Out-Null; $lines = Get-Content -Path $f.FullName
  $nameMatch = ($lines | Where-Object { $_ -match 'Attribute\s+VB_Name\s*=\s*"([^"]+)"' } | Select-Object -First 1)
  if ($nameMatch) {
    $m = [Regex]::Match($nameMatch, 'Attribute\s+VB_Name\s*=\s*"([^"]+)"')
    $modName = $m.Groups[1].Value
  } else {
    $modName = [IO.Path]::GetFileNameWithoutExtension($f.Name)
  }
  $kind = Get-ModuleKindFromPath $f.FullName
  $procs = Parse-Procedures -lines $lines -moduleName $modName -filePath ($f.FullName)
  $modules += [ordered]@{
    id = $modName
    name = $modName
    kind = $kind
    file = $f.FullName.Replace($root, '').TrimStart('\','/')
    procedures = $procs
  }
}

$projectName = Split-Path -Leaf $root
$obj = [ordered]@{
  irSchemaVersion = '0.1'
  generator = @{ name = 'vba2json'; version = '0.1.0' }
  project = @{ name = $projectName; modules = $modules }
}

$json = $obj | ConvertTo-Json -Depth 10
if ([string]::IsNullOrWhiteSpace($OutputPath)) { Write-Output $json }
else { $dir = Split-Path -Parent $OutputPath; if ($dir) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }; Set-Content -Path $OutputPath -Value $json -Encoding UTF8 }

