param(
    [Parameter(Mandatory = $true)][string]$Template,
    [string]$ThemeName   = "",
    [string]$ThemePath   = "",
    [int]   $ListMasters = 10,
    [string]$VDGPath     = ""
)

# Load helper (supports either filename so we don't get tripped up)
$helperPath = @(
    (Join-Path $PSScriptRoot '_helper.ps1'),
    (Join-Path $PSScriptRoot '_helpers.ps1')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $helperPath) { throw "Helper not found under $PSScriptRoot." }
. $helperPath

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-VdgCase {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]   $Label,
        [Parameter(Mandatory)][string[]] $ArgList,
        [Parameter(Mandatory)][int]      $ExpectedExit
    )
    Write-Host "[$Label]"
    $global:LASTEXITCODE = $null
    & $VDGPath @ArgList | Out-Host
    $code = if ($LASTEXITCODE -is [int]) { [int]$LASTEXITCODE } elseif ($?) { 0 } else { 1 }
    Assert-Exit -Actual $code -Expected $ExpectedExit -Label $Label
}

# --- Functional tests ---
# Adjust stencil names/master NameU to match your environment as needed.

# F1: Built-in stencil by friendly name
$F1 = @('--template', $Template, '--stencil', 'Basic Shapes')
Invoke-VdgCase -Label 'F1 Built-in stencil by friendly name' -ArgList $F1 -ExpectedExit 0

# F4: Cache reuse (same key used twice) — simulated by resolving the same stencil twice
$F4 = @('--template', $Template, '--stencil', 'Basic Shapes', '--stencil', 'Basic Shapes')
Invoke-VdgCase -Label 'F4 Cache reuse (same key used twice)' -ArgList $F4 -ExpectedExit 0

# F5: Strict NameU mapping (Rectangle expected to exist)
$F5 = @('--template', $Template, '--stencil', 'Basic Shapes', '--map', 'Node=Basic Shapes!Rectangle')
Invoke-VdgCase -Label 'F5 Strict NameU mapping' -ArgList $F5 -ExpectedExit 0

# F6: NameU not found -> error
$F6 = @('--template', $Template, '--stencil', 'Basic Shapes', '--map', 'Node=Basic Shapes!DoesNotExist')
Invoke-VdgCase -Label 'F6 NameU not found -> error' -ArgList $F6 -ExpectedExit 2

# F7: Lenient name match (--allow-name)
$F7 = @('--template', $Template, '--stencil', 'Basic Shapes', '--allow-name', '--map', 'Node=Basic Shapes!rectangle')  # lower-case name
Invoke-VdgCase -Label 'F7 Lenient name match (--allow-name)' -ArgList $F7 -ExpectedExit 0

# F8: Case-sensitive strict NameU
$F8 = @('--template', $Template, '--stencil', 'Basic Shapes', '--map', 'Node=Basic Shapes!rectangle')  # no --allow-name
Invoke-VdgCase -Label 'F8 Case-sensitive strict NameU' -ArgList $F8 -ExpectedExit 2

# F9: Theme by name
$F9 = @('--template', $Template, '--theme-name', $ThemeName)
Invoke-VdgCase -Label 'F9 Theme by name' -ArgList $F9 -ExpectedExit 0

# F10: Theme path wins over name
$F10 = @('--template', $Template, '--theme-name', $ThemeName, '--theme-path', $ThemePath)
Invoke-VdgCase -Label 'F10 Theme path wins over name' -ArgList $F10 -ExpectedExit 0

# F11: Theme variant surfaced (2 as example)
$F11 = @('--template', $Template, '--theme-path', $ThemePath, '--theme-variant', '2')
Invoke-VdgCase -Label 'F11 Theme variant surfaced' -ArgList $F11 -ExpectedExit 0

# F12: Duplicate stencil keys -> validation error (simulate with the same key alias twice)
$F12 = @('--template', $Template, '--stencil', 'KeyA=Basic Shapes', '--stencil', 'KeyA=Arrows')
Invoke-VdgCase -Label 'F12 Duplicate stencil keys -> validation error' -ArgList $F12 -ExpectedExit 2

# F13: Missing stencil file -> friendly error
$F13 = @('--template', $Template, '--stencil-path', 'C:\_definitely_missing_\missing.vssx')
Invoke-VdgCase -Label 'F13 Missing stencil file -> friendly error' -ArgList $F13 -ExpectedExit 2

exit 0
