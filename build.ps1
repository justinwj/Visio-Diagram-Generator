\
[CmdletBinding()]
param(
  [ValidateSet('x86','x64')] [string] $Platform = 'x64',
  [ValidateSet('Debug','Release')] [string] $Configuration = 'Release',
  [string] $VisioLibPath = $env:VISIO_LIB_PATH
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# If the script was downloaded, clear the MOTW so it runs without the "digitally signed" error.
try { Unblock-File -Path $MyInvocation.MyCommand.Path -ErrorAction Stop } catch { }

# Prefer Community MSBuild; fall back to BuildTools if needed.
$msbuild = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
  $msbuild = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe"
}
if (-not (Test-Path $msbuild)) { throw "MSBuild.exe not found. Install VS 2022 Community or Build Tools." }

# Desktop MSBuild needs to see the .NET SDK resolver so it understands SDK-style projects.
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = "${env:ProgramFiles}\dotnet"

# Pick a default VISLIB.DLL if none was supplied.
if (-not $VisioLibPath) {
  if ($Platform -eq 'x86') {
    $VisioLibPath = "${env:ProgramFiles(x86)}\Microsoft Office\root\Office16\VISLIB.DLL"
  } else {
    $VisioLibPath = "${env:ProgramFiles}\Microsoft Office\root\Office16\VISLIB.DLL"
  }
}

Write-Host "MSBuild: $msbuild"
Write-Host "Platform: $Platform | Configuration: $Configuration"
Write-Host "VISIO_LIB_PATH: $VisioLibPath"

# 1) Build contracts for both TFMs (net8.0 + netstandard2.0)
& $msbuild ".\src\VDG.Core.Contracts\VDG.Core.Contracts.csproj" /restore "/p:Configuration=$Configuration"
if ($LASTEXITCODE) { exit $LASTEXITCODE }

# 2) Build the Visio runner (x64 or x86)
& $msbuild ".\src\VDG.VisioRuntime\VDG.VisioRuntime.csproj" /restore `
  "/p:Configuration=$Configuration" "/p:Platform=$Platform" `
  "/p:VISIO_LIB_PATH=$VisioLibPath"
exit $LASTEXITCODE
