param(
  [ValidateSet('x64','x86')]
  [string]$Platform = 'x64',
  [string]$Configuration = 'Release',
  [string]$VisioLib = ''
)

# Ensure desktop MSBuild can see the .NET SDK resolver
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = "${env:ProgramFiles}\dotnet"

# Prefer Community MSBuild; fallback to BuildTools; last resort: Rider tools
$msbuildCandidates = @(
  "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
  "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe",
  "${env:LOCALAPPDATA}\JetBrains\Toolbox\apps\Rider\tools\MSBuild\Current\Bin\amd64\MSBuild.exe"
) | Where-Object { Test-Path $_ }

if (-not $msbuildCandidates) { throw "MSBuild.exe not found. Please install VS 2022 Community or Build Tools." }
$msbuild = $msbuildCandidates[0]

# Optional VISLIB override
$visioProperty = @()
if ($VisioLib) { $visioProperty = @("/p:VISIO_LIB_PATH=$VisioLib") }

# Clean stale outputs so cross-targeting picks correctly
Get-ChildItem -Recurse -Force -Filter bin, obj | Remove-Item -Recurse -Force -ErrorAction Ignore

# Build Contracts (multi-target). Using /t:Restore;Build must be quoted in PowerShell!
& $msbuild ".\src\VDG.Core.Contracts\VDG.Core.Contracts.csproj" `
  "/t:Restore;Build" "/p:Configuration=$Configuration"

# Build the runner (must use desktop MSBuild because of COM interop)
& $msbuild ".\src\VDG.VisioRuntime\VDG.VisioRuntime.csproj" `
  "/t:Restore;Build" "/p:Configuration=$Configuration" "/p:Platform=$Platform" `
  @visioProperty
