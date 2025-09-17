
param(
  [Parameter(Mandatory=$true)][string]$Template,
  [string]$ThemeName = "",
  [string]$ThemePath = "",
  [int]$ListMasters = 10,
  [string]$VDGPath = ""     # <— add this
)
. "$PSScriptRoot\_helper.ps1"

# After:  . "$PSScriptRoot\_helper.ps1"
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Run-And-Assert {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]  $Label,
        [Parameter(Mandatory)][object]  $Args,     # whatever you already build (array of args)
        [Parameter(Mandatory)][int]     $Expected
    )
    $global:LASTEXITCODE = $null

    # Run CLI with your existing argument array; avoid returning pipeline objects
    & $VDGPath @Args | Out-Host

    # Normalize to an integer exit code
    $code =
        if ($LASTEXITCODE -is [int]) { [int]$LASTEXITCODE }
        elseif ($?) { 0 }
        else { 1 }

    Assert-Exit -Actual $code -Expected $Expected -Label $Label
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-VDG {
  param([string]$VDGOverride)
  if ($VDGOverride) { return $VDGOverride }
  $candidates = @(
    (Join-Path $RepoRoot "bin\Debug\vdg.exe"),
    (Join-Path $RepoRoot "src\VDG.CLI\bin\Debug\net8.0\VDG.CLI.exe"),
    (Join-Path $RepoRoot "src\VisioDiagramGenerator.CliFs\bin\Debug\net8.0-windows\VisioDiagramGenerator.CliFs.exe")
  )
  foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
  throw "VDG executable not found. Build the CLI or provide -VDGPath."
}

function Invoke-VDG {
  param([string[]]$ArgsArray)
  $exe = Resolve-VDG -VDGOverride $VDGPath
  & $exe @ArgsArray
  return $LASTEXITCODE
}

function New-ConfigFromTemplate {
  param([string]$TemplateName) # filename in config-templates

  $tmplPath = Join-Path $PSScriptRoot ("..\config-templates\" + $TemplateName)
  if (!(Test-Path $tmplPath)) { throw "Missing template: $TemplateName" }
  $content = Get-Content -Raw $tmplPath

  # Safe string replacements (no regex)
  $content = $content.Replace("{{TEMPLATE}}", $Template)
  $content = $content.Replace("{{THEME_PATH}}", $ThemePath)
  $content = $content.Replace("{{THEME_NAME}}", $ThemeName)

  $outDir = Join-Path $PSScriptRoot "..\generated"
  New-Item -ItemType Directory -Force -Path $outDir | Out-Null
  $outPath = Join-Path $outDir ($TemplateName -replace '\.tmpl$', '')
  Set-Content -Path $outPath -Value $content -Encoding UTF8
  return $outPath
}

$script:Failures = 0
function Run-Test {
  param(
    [string]$Id,
    [string]$Name,
    [string[]]$Args,
    [int]$ExpectExit = 0
  )
  Write-Host "[$Id] $Name" -ForegroundColor Cyan
  $code = Invoke-VDG -ArgsArray $Args
  if ($code -ne $ExpectExit) {
    Write-Host ("    FAIL (exit {0}, expected {1})" -f $code, $ExpectExit) -ForegroundColor Red
    $script:Failures++
  } else {
    Write-Host "    PASS" -ForegroundColor Green
  }
}

# Smoke
$configS1 = New-ConfigFromTemplate -TemplateName 'p8-s1-template.json.tmpl'
Run-Test -Id 'S1' -Name 'Template loads' -Args @('--config', $configS1, '--diag')

$configS2 = New-ConfigFromTemplate -TemplateName 'p8-s2-stencil-masters.json.tmpl'
Run-Test -Id 'S2' -Name 'Stencil loads & list masters' -Args @('--config', $configS2, '--diag', '--list-masters', $ListMasters)

if ($Failures -gt 0) { exit 1 } else { exit 0 }
