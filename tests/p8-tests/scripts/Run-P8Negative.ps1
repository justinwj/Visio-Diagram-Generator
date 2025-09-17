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

# --- Negative/edge tests ---
# N2: Master collision across different keys (should resolve correctly)
$N2 = @('--template', $Template, '--stencil', 'A=Basic Shapes', '--stencil', 'B=Basic Shapes', '--map', 'A!Rectangle=B!Rectangle')
Invoke-VdgCase -Label 'N2 Master collision across different keys (should resolve correctly)' -ArgList $N2 -ExpectedExit 0

exit 0
