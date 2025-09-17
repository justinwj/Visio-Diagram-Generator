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

if (-not (Get-Command Invoke-Exe -ErrorAction SilentlyContinue)) {
    throw "Helper loaded but Invoke-Exe is missing—check _helper.ps1 contents."
}

function Invoke-Stage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$ScriptName,
        [Parameter(Mandatory)][hashtable]$ArgsTable
    )
    Write-Host "== $Name =="
    $argv = @(
        "-NoProfile","-ExecutionPolicy","Bypass","-File",
        (Join-Path $PSScriptRoot $ScriptName)
    )
    foreach ($k in $ArgsTable.Keys) { $argv += @("-$k", $ArgsTable[$k]) }
    $res = Invoke-Exe -FilePath "powershell.exe" -Args $argv -Echo
    Write-Host "$Name exit $($res.ExitCode)"
    return $res.ExitCode
}

$common = @{
    Template    = $Template
    ThemeName   = $ThemeName
    ThemePath   = $ThemePath
    ListMasters = $ListMasters
    VDGPath     = $VDGPath
}

$code1 = Invoke-Stage -Name "Smoke"      -ScriptName 'Run-P8Smoke.ps1'      -ArgsTable $common
$code2 = Invoke-Stage -Name "Functional" -ScriptName 'Run-P8Functional.ps1' -ArgsTable $common
$code3 = Invoke-Stage -Name "Negative"   -ScriptName 'Run-P8Negative.ps1'   -ArgsTable $common

if (($code1 -eq 0) -and ($code2 -eq 0) -and ($code3 -eq 0)) { exit 0 } else { exit 1 }