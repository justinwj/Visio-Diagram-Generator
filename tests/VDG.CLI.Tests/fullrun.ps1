param(
    [string]$Configuration = 'Release',
    [string]$Sample        = 'samples/invSys',
    [string]$Mode          = 'callgraph'
)

$ErrorActionPreference = 'Stop'

if ($PSScriptRoot) {
    $repoRoot = Resolve-Path "$PSScriptRoot\..\.."
} else {
    $repoRoot = (Get-Location).ProviderPath
}

Push-Location $repoRoot

Write-Host "Building solution ($Configuration)..."
dotnet build Visio-Diagram-Generator.sln -c $Configuration

# Allow the Visio runner to spin up so we can inspect the diagram visually.
$oldSkipRunner = $env:VDG_SKIP_RUNNER
$env:VDG_SKIP_RUNNER = '0'

try {
    $stamp   = Get-Date -Format 'yyyyMMdd-HHmmss'
    $outDir  = Join-Path $repoRoot "out\runs\invSys-$stamp"
    New-Item $outDir -ItemType Directory -Force | Out-Null

    $vsdxPath      = Join-Path $outDir 'invSys.vsdx'
    $diagramJson   = Join-Path $outDir 'invSys.diagram.json'
    $diagnosticsJson = Join-Path $outDir 'invSys.diagnostics.json'

    Write-Host ''
    Write-Host "Rendering $Sample ($Mode) -> $vsdxPath"
    dotnet run --project src/VDG.VBA.CLI/VDG.VBA.CLI.csproj -- render `
        --in $Sample `
        --mode $Mode `
        --out $vsdxPath `
        --diagram-json $diagramJson `
        --diag-json $diagnosticsJson

    Write-Host ''
    Write-Host "Diagram:        $vsdxPath"
    Write-Host "Diagram JSON:   $diagramJson"
    Write-Host "Diagnostics:    $diagnosticsJson"
    Write-Host ''
    Write-Host "The diagram JSON now includes layout.view.pageLayouts.json with page origins and bounds."
}
finally {
    # put the skip-runner flag back the way we found it
    if ($null -ne $oldSkipRunner) { $env:VDG_SKIP_RUNNER = $oldSkipRunner }
    else { Remove-Item Env:\VDG_SKIP_RUNNER -ErrorAction SilentlyContinue }
    Pop-Location
}
