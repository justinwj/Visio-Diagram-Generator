Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Exe {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [string[]] $Args,
        [switch]   $Echo
    )
    $global:LASTEXITCODE = $null
    $allOut = & $FilePath @Args 2>&1
    if ($Echo) { $allOut | Write-Host }
    $code =
        if ($LASTEXITCODE -is [int]) { [int]$LASTEXITCODE }
        elseif ($?) { 0 } else { 1 }
    [pscustomobject]@{
        ExitCode = $code
        Output   = ($allOut -join [Environment]::NewLine)
    }
}

function Get-ExitCode {
    param([Parameter(Mandatory)][object]$Value)
    if ($Value -is [int]) { return [int]$Value }
    if ($Value -and $Value.PSObject.Properties.Match('ExitCode').Count -gt 0) {
        return [int]$Value.ExitCode
    }
    if ($Value -is [array]) {
        $ints = $Value | Where-Object { $_ -is [int] }
        if ($ints.Count -gt 0) { return [int]$ints[-1] }
    }
    if ($LASTEXITCODE -is [int]) { return [int]$LASTEXITCODE }
    return ($(if ($?) { 0 } else { 1 }))
}

function Assert-Exit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Actual,
        [Parameter(Mandatory)][int]   $Expected,
        [string]$Label = ''
    )
    $code = Get-ExitCode $Actual
    $tag  = if ($Label) { "[$Label] " } else { "" }
    if ($code -ne $Expected) {
        throw "${tag}FAIL (exit $code, expected $Expected)"
    } else {
        Write-Host "${tag}OK (exit $code)"
    }
}