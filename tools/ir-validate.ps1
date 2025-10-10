Param(
  [Parameter(Mandatory = $true)] [string]$InputPath,
  [string]$SchemaPath = "shared/Config/vbaIr.schema.json"
)

function Fail($msg) { Write-Error $msg; exit 1 }
if (-not (Test-Path -Path $InputPath)) { Fail "Input file not found: $InputPath" }
if (-not (Test-Path -Path $SchemaPath)) { Fail "Schema file not found: $SchemaPath" }

$rawJson = $null
try { $rawJson = Get-Content -Raw -Path $InputPath }
catch { Fail "Unable to read JSON: $($_.Exception.Message)" }

function Convert-ToStructuredObject {
  param($Value)
  if ($null -eq $Value) { return $null }
  if ($Value -is [System.Collections.IDictionary]) {
    $map = @{}
    foreach ($entry in $Value.GetEnumerator()) {
      $map[$entry.Key] = Convert-ToStructuredObject $entry.Value
    }
    return $map
  }
  if ($Value -is [System.Management.Automation.PSCustomObject]) {
    $map = @{}
    foreach ($prop in $Value.PSObject.Properties) {
      $map[$prop.Name] = Convert-ToStructuredObject $prop.Value
    }
    return $map
  }
  if ($Value -is [System.Array] -or ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string]) -and -not ($Value -is [System.Collections.IDictionary]))) {
    $list = New-Object System.Collections.ArrayList
    foreach ($item in $Value) {
      [void]$list.Add((Convert-ToStructuredObject $item))
    }
    return $list.ToArray()
  }
  return $Value
}

$schemaText = Get-Content -Raw -Path $SchemaPath
$schemaObject = Convert-ToStructuredObject (ConvertFrom-Json -InputObject $schemaText)
$schemaDefs = $null
if ($schemaObject -is [System.Collections.IDictionary] -and $schemaObject.ContainsKey('$defs')) {
  $schemaDefs = $schemaObject['$defs']
} else { $schemaDefs = @{} }

$hasTestJson = Get-Command -Name Test-Json -ErrorAction SilentlyContinue
if ($hasTestJson) {
  try {
    $null = $rawJson | Test-Json -Schema $schemaText -ErrorAction Stop
  }
  catch {
    Fail ("Schema validation failed for '{0}': {1}" -f $InputPath, $_.Exception.Message)
  }
}
else {
  $validationErrors = New-Object System.Collections.Generic.List[string]

  function Add-ValidationError([string]$Message) { $script:validationErrors.Add($Message) | Out-Null }

  function Resolve-SchemaNode($Node) {
    if ($null -eq $Node) { return $null }
    while ($Node -is [System.Collections.IDictionary] -and $Node.ContainsKey('$ref')) {
      $ref = $Node['$ref']
      if ($ref -isnot [string] -or -not $ref.StartsWith('#/$defs/')) {
        Add-ValidationError("Unsupported \$ref '$ref'.")
        return $null
      }
      $key = $ref.Substring(8)
      if (-not ($script:schemaDefs.ContainsKey($key))) {
        Add-ValidationError("Schema reference '#/\$defs/$key' not found.")
        return $null
      }
      $Node = $script:schemaDefs[$key]
    }
    return $Node
  }

  function Test-TypeMatch([string]$Type, $Value) {
    switch ($Type) {
      'object' { return ($Value -is [System.Collections.IDictionary]) -or ($Value -is [System.Management.Automation.PSCustomObject]) }
      'array'  { return ($Value -is [System.Collections.IList]) -or ($Value -is [System.Array]) }
      'string' { return $Value -is [string] }
      'boolean' { return $Value -is [bool] }
      'integer' { return ($Value -is [int] -or $Value -is [long]) }
      'number' { return ($Value -is [double] -or $Value -is [float] -or $Value -is [decimal] -or $Value -is [int] -or $Value -is [long]) }
      'null' { return $null -eq $Value }
      default { return $true }
    }
  }

  function Try-GetPropertyValue {
    param($Instance, [string]$Name, [ref]$Value)
    if ($Instance -is [System.Collections.IDictionary]) {
      if ($Instance.ContainsKey($Name)) {
        $Value.Value = $Instance[$Name]
        return $true
      }
      $Value.Value = $null
      return $false
    }
    if ($Instance -is [System.Management.Automation.PSCustomObject]) {
      $prop = $Instance.PSObject.Properties[$Name]
      if ($null -ne $prop) {
        $Value.Value = $prop.Value
        return $true
      }
    }
    $Value.Value = $null
    return $false
  }

  function Get-EnumerableCount($Value) {
    if ($Value -is [System.Array]) { return $Value.Length }
    if ($Value -is [System.Collections.ICollection]) { return $Value.Count }
    return 0
  }

  function As-IList($Value) {
    if ($Value -is [System.Collections.IList]) { return $Value }
    if ($Value -is [System.Array]) { return [System.Collections.ArrayList]::new($Value) }
    return $null
  }

  function Validate-Node($SchemaNode, $DataNode, [string]$Path) {
    if ($null -eq $SchemaNode) { return }
    $SchemaNode = Resolve-SchemaNode $SchemaNode
    if ($null -eq $SchemaNode) { return }

    $types = @()
    if ($SchemaNode.ContainsKey('type')) {
      $declaredType = $SchemaNode['type']
      if ($declaredType -is [System.Collections.IEnumerable] -and -not ($declaredType -is [string])) {
        $types = @($declaredType)
      }
      else { $types = @($declaredType) }
      $matches = $false
      foreach ($t in $types) {
        if (Test-TypeMatch $t $DataNode) { $matches = $true; break }
      }
      if (-not $matches) {
        $actualType = if ($null -eq $DataNode) { 'null' } else { $DataNode.GetType().Name }
        Add-ValidationError("$Path expected type [$($types -join ', ')], actual '$actualType'.")
        return
      }
    }

    if ($SchemaNode.ContainsKey('const')) {
      if ($SchemaNode['const'] -ne $DataNode) {
        Add-ValidationError("$Path expected constant '$($SchemaNode['const'])'.")
      }
    }

    if ($SchemaNode.ContainsKey('enum')) {
      $enumValues = $SchemaNode['enum']
      if (-not ($enumValues -contains $DataNode)) {
        Add-ValidationError("$Path value '$DataNode' is not one of '$($enumValues -join ', ')'.")
      }
    }

    if ($SchemaNode.ContainsKey('minLength') -and $DataNode -is [string]) {
      if ($DataNode.Length -lt [int]$SchemaNode['minLength']) {
        Add-ValidationError("$Path expected minimum length $($SchemaNode['minLength']).")
      }
    }

    if ($SchemaNode.ContainsKey('minimum') -and ($DataNode -is [int] -or $DataNode -is [long] -or $DataNode -is [double])) {
      if ($DataNode -lt [double]$SchemaNode['minimum']) {
        Add-ValidationError("$Path expected minimum value $($SchemaNode['minimum']).")
      }
    }

    if ($SchemaNode.ContainsKey('type') -and ($types -contains 'object')) {
      $properties = $SchemaNode['properties']
      $required = @()
      if ($SchemaNode.ContainsKey('required')) { $required = @($SchemaNode['required']) }
      foreach ($req in $required) {
        $tmp = $null
        if (-not (Try-GetPropertyValue $DataNode $req ([ref]$tmp))) {
          Add-ValidationError("$Path missing required property '$req'.")
        }
      }
      if ($properties -is [System.Collections.IDictionary]) {
        foreach ($propName in $properties.Keys) {
          $subSchema = $properties[$propName]
          $propVal = $null
          if (Try-GetPropertyValue $DataNode $propName ([ref]$propVal)) {
            Validate-Node $subSchema $propVal "$Path.$propName"
          }
        }
      }
    }
    elseif ($SchemaNode.ContainsKey('type') -and ($types -contains 'array')) {
      if ($SchemaNode.ContainsKey('minItems')) {
        $count = Get-EnumerableCount $DataNode
        if ($count -lt [int]$SchemaNode['minItems']) {
          Add-ValidationError("$Path expected at least $($SchemaNode['minItems']) item(s).")
        }
      }
      if ($SchemaNode.ContainsKey('items')) {
        $list = As-IList $DataNode
        if ($null -eq $list) {
          Add-ValidationError("$Path is not an array instance.")
        }
        else {
          for ($i = 0; $i -lt $list.Count; $i++) {
            Validate-Node $SchemaNode['items'] $list[$i] "$Path[$i]"
          }
        }
      }
    }
  }

  try { $dataObject = ConvertFrom-Json -InputObject $rawJson }
  catch { Fail "Invalid JSON: $($_.Exception.Message)" }

  Validate-Node $schemaObject $dataObject '$'

  if ($validationErrors.Count -gt 0) {
    $msg = "Schema validation failed for '$InputPath':`n - " + ($validationErrors -join "`n - ")
    Fail $msg
  }
}

# Secondary invariants that the schema cannot easily express today.
try { $json = $rawJson | ConvertFrom-Json -ErrorAction Stop }
catch { Fail "Invalid JSON: $($_.Exception.Message)" }

if (-not $json.project.modules -or $json.project.modules.Count -eq 0) {
  Fail "Expected project.modules to contain at least one module."
}

foreach ($module in $json.project.modules) {
  if (-not $module.procedures -or $module.procedures.Count -eq 0) {
    Fail "Module '$($module.name)' must contain at least one procedure."
  }
}

Write-Host "IR OK: $InputPath" -ForegroundColor Green
exit 0

