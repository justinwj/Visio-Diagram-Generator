param(
    [switch]$Sign
)

<#
.Synopsis
    Builds and packages the Visio Diagram Generator CLI and runner into versioned ZIP archives.
.Description
    This script publishes the F# CLI project and the C# runner project, produces zipped
    artifacts under the artifacts directory and optionally signs executables when the
    -Sign switch is provided (not yet implemented).
#>

set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Version {
    # Attempts to read the version from a Directory.Build.props file or returns a default.
    $defaultVersion = '0.1.0'
    $propsPath = Join-Path $PSScriptRoot "..\Directory.Build.props"
    if (Test-Path $propsPath) {
        [xml]$xml = Get-Content -Raw -Path $propsPath
        $verNode = $xml.Project.PropertyGroup.Version
        if ($verNode) { return $verNode } else { return $defaultVersion }
    }
    return $defaultVersion
}

function Publish-Project {
    param(
        [string]$ProjectPath,
        [string]$Output,
        [string]$Runtime,
        [string]$Configuration = 'Release',
        [switch]$SelfContained = $false
    )
    $scArg = if ($SelfContained) { '/p:SelfContained=true /p:PublishSingleFile=true' } else { '' }
    $cmd = "dotnet publish `"$ProjectPath`" -c $Configuration -o `"$Output`" -r $Runtime /p:PublishTrimmed=false $scArg"
    Write-Host "Publishing $ProjectPath to $Output..." -ForegroundColor Cyan
    Write-Host $cmd
    & dotnet publish "$ProjectPath" -c $Configuration -o "$Output" -r $Runtime /p:PublishTrimmed=false $scArg
}

try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
    Set-Location $repoRoot
    $version = Get-Version
    $artifacts = Join-Path $repoRoot 'artifacts'
    if (Test-Path $artifacts) { Remove-Item -Recurse -Force $artifacts }
    New-Item -ItemType Directory -Path $artifacts | Out-Null

    # Publish F# CLI (net8.0-windows)
    $cliOutput = Join-Path $artifacts 'cli'
    Publish-Project -ProjectPath 'src/VisioDiagramGenerator.CliFs/VisioDiagramGenerator.CliFs.fsproj' -Output $cliOutput -Runtime 'win-x64'

    # Build C# runner (currently stub; using VDG.CLI)
    $runnerOutput = Join-Path $artifacts 'runner'
    Publish-Project -ProjectPath 'src/VDG.CLI/VDG.CLI.csproj' -Output $runnerOutput -Runtime 'win-x86'

    # Create zip archives
    $cliZip = Join-Path $artifacts "VDG_CLI_${version}_win-x64.zip"
    $runnerZip = Join-Path $artifacts "VDG_Runner_${version}_net48.zip"
    if (Test-Path $cliZip) { Remove-Item $cliZip }
    if (Test-Path $runnerZip) { Remove-Item $runnerZip }
    Compress-Archive -Path (Join-Path $cliOutput '*') -DestinationPath $cliZip
    Compress-Archive -Path (Join-Path $runnerOutput '*') -DestinationPath $runnerZip

    Write-Host "Packaging complete. Artifacts located in $artifacts" -ForegroundColor Green
    Write-Host "CLI: $cliZip"
    Write-Host "Runner: $runnerZip"
    
    if ($Sign) {
        Write-Host "Code signing artifacts..." -ForegroundColor Yellow
        # TODO: Implement actual signing logic
        # signtool sign /f cert.pfx /p password /t timestamp $cliOutput\*.exe
        # signtool sign /f cert.pfx /p password /t timestamp $runnerOutput\*.exe
        Write-Warning "Signing is not implemented yet in this version."
    }
}
catch {
    Write-Error $_
    exit 1
}
