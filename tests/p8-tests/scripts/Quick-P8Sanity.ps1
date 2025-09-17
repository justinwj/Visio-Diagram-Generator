param(
    [Parameter(Mandatory = $true)][string]$Template,        # .vstx full path
    [string]$ThemeName   = "Office",
    [string]$ThemePath   = "",                              # optional .thmx
    [int]   $ListMasters = 10,
    [string]$VDGPath     = ""                               # optional, will auto-resolve if empty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-VDG {
    param([string]$Override)
    if ($Override) { return $Override }

    # Try typical repo build outputs
    $here = Split-Path -Parent $PSCommandPath
    $repo = Resolve-Path (Join-Path $here "..\..") -ErrorAction SilentlyContinue
    $candidates = @()
    if ($repo) {
        $root = $repo.Path
        $candidates += @(
            (Join-Path $root "src\VisioDiagramGenerator.CliFs\bin\Debug\net8.0-windows\VisioDiagramGenerator.CliFs.exe"),
            (Join-Path $root "src\VisioDiagramGenerator.CliFs\bin\Debug\net8.0\VisioDiagramGenerator.CliFs.exe"),
            (Join-Path $root "src\VDG.CLI\bin\Debug\net8.0-windows\VDG.CLI.exe"),
            (Join-Path $root "src\VDG.CLI\bin\Debug\net8.0\VDG.CLI.exe")
        )
    }
    $candidates += @(
        (Join-Path (Get-Location) "VisioDiagramGenerator.CliFs.exe"),
        (Join-Path (Get-Location) "VDG.CLI.exe")
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    throw "VDG executable not found. Build the CLI or pass -VDGPath."
}

function Invoke-Cli {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$ArgList)
    $global:LASTEXITCODE = $null
    & $script:Exe @ArgList | Out-Host
    if ($LASTEXITCODE -is [int]) { return [int]$LASTEXITCODE }
    if ($?) { 0 } else { 1 }
}

$script:Pass = 0
$script:Fail = 0
function Test-Case {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]  $Id,
        [Parameter(Mandatory)][string]  $Name,
        [Parameter(Mandatory)][string[]]$CliArgs,
        [int]$Expect = 0
    )
    Write-Host ("[{0}] {1}" -f $Id, $Name) -ForegroundColor Cyan
    $code = Invoke-Cli -ArgList $CliArgs
    if ($code -ne $Expect) {
        Write-Host ("    FAIL (exit {0}, expected {1})" -f $code, $Expect) -ForegroundColor Red
        $script:Fail++
    } else {
        Write-Host "    PASS" -ForegroundColor Green
        $script:Pass++
    }
}

# ---- temp configs -------------------------------------------------------------
$OutDir = Join-Path $env:TEMP ("vdg-p8-" + (Get-Date -Format "yyyyMMddHHmmss"))
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# S1
$s1 = Join-Path $OutDir "p8-s1.json"
@"
{
  "template": "$Template",
  "stencils": [],
  "shapeMapping": {}
}
"@ | Set-Content -Path $s1 -Encoding UTF8

# S2
$s2 = Join-Path $OutDir "p8-s2.json"
@"
{
  "template": "$Template",
  "stencils": [{ "key": "Basic", "path": "Basic Shapes.vssx" }],
  "shapeMapping": {}
}
"@ | Set-Content -Path $s2 -Encoding UTF8

# ---- run ---------------------------------------------------------------------
$script:Exe = Resolve-VDG -Override $VDGPath
Write-Host ("Using CLI: {0}" -f $script:Exe) -ForegroundColor DarkGray

# SMOKE
Test-Case -Id 'S1' -Name 'Template loads (config)'               -CliArgs @('--config', $s1, '--diag')                                      -Expect 0
Test-Case -Id 'S2' -Name 'Stencil loads & list masters (config)' -CliArgs @('--config', $s2, '--diag', '--list-masters', "$ListMasters")    -Expect 0

# FUNCTIONAL (flags, no config)
Test-Case -Id 'F1'  -Name 'Built-in stencil by friendly name'     -CliArgs @('--template', $Template, '--stencil', 'Basic Shapes')                               -Expect 0
Test-Case -Id 'F4'  -Name 'Cache reuse (same name twice)'         -CliArgs @('--template', $Template, '--stencil', 'Basic Shapes', '--stencil', 'Basic Shapes')  -Expect 0
Test-Case -Id 'F5'  -Name 'Strict NameU mapping'                  -CliArgs @('--template', $Template, '--stencil', 'Basic Shapes', '--map', 'Node=Basic Shapes!Rectangle') -Expect 0
Test-Case -Id 'F6'  -Name 'NameU not found -> error'              -CliArgs @('--template', $Template, '--stencil', 'Basic Shapes', '--map', 'Node=Basic Shapes!DoesNotExist') -Expect 2
Test-Case -Id 'F7'  -Name 'Lenient name match (--allow-name)'     -CliArgs @('--template', $Template, '--stencil', 'Basic Shapes', '--allow-name', '--map', 'Node=Basic Shapes!rectangle') -Expect 0
Test-Case -Id 'F8'  -Name 'Case-sensitive strict NameU'           -CliArgs @('--template', $Template, '--stencil', 'Basic Shapes', '--map', 'Node=Basic Shapes!rectangle') -Expect 2
Test-Case -Id 'F9'  -Name 'Theme by name'                         -CliArgs @('--template', $Template, '--theme-name', $ThemeName)                                 -Expect 0
if ($ThemePath) {
    Test-Case -Id 'F10' -Name 'Theme path wins over name'         -CliArgs @('--template', $Template, '--theme-name', $ThemeName, '--theme-path', $ThemePath)     -Expect 0
    Test-Case -Id 'F11' -Name 'Theme variant surfaced (2)'        -CliArgs @('--template', $Template, '--theme-path', $ThemePath, '--theme-variant', '2')         -Expect 0
    Test-Case -Id 'F11b'-Name 'Theme variant out of range'        -CliArgs @('--template', $Template, '--theme-path', $ThemePath, '--theme-variant', '9')         -Expect 2
} else {
    Write-Host "Skipping F10/F11: no -ThemePath provided." -ForegroundColor DarkYellow
}
Test-Case -Id 'F12' -Name 'Duplicate stencil keys -> error'       -CliArgs @('--template', $Template, '--stencil', 'KeyA=Basic Shapes', '--stencil', 'KeyA=Arrows') -Expect 2
Test-Case -Id 'F13' -Name 'Missing stencil file -> error'         -CliArgs @('--template', $Template, '--stencil-path', 'C:\_definitely_missing_\missing.vssx')   -Expect 2

# NEGATIVE / EDGE
Test-Case -Id 'N2'  -Name 'Master collision across different keys resolves' `
          -CliArgs @('--template', $Template, '--stencil', 'A=Basic Shapes', '--stencil', 'B=Basic Shapes', '--map', 'A!Rectangle=B!Rectangle') -Expect 0

Write-Host ""
Write-Host ("Summary: PASS={0}  FAIL={1}" -f $script:Pass, $script:Fail) -ForegroundColor White
if ($script:Fail -gt 0) { exit 1 } else { exit 0 }
