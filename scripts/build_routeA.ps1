# Route A build (desktop MSBuild). Run from repo root.
$ErrorActionPreference = 'Stop'

# Ensure the desktop MSBuild can find Microsoft.NET.Sdk
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = "C:\Program Files\dotnet"

$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"

# Clean bin/obj to avoid stale net8.0 artifacts
Get-ChildItem -Path .\src -Directory -Recurse -Filter bin | Remove-Item -Recurse -Force -ErrorAction Ignore
Get-ChildItem -Path .\src -Directory -Recurse -Filter obj | Remove-Item -Recurse -Force -ErrorAction Ignore

# Build contracts for all TFMs (note the quoted target list to avoid PowerShell interpreting the semicolon)
& $msbuild ".\src\VDG.Core.Contracts\VDG.Core.Contracts.csproj" "/t:Restore;Build" "/p:Configuration=Release" | Write-Host

# Build the runner, forcing x64 (switch to x86 if your Visio is 32-bit)
$vislib = "C:\Program Files\Microsoft Office\root\Office16\VISLIB.DLL"
& $msbuild ".\src\VDG.VisioRuntime\VDG.VisioRuntime.csproj" "/t:Restore;Build" `
  "/p:Configuration=Release" "/p:Platform=x64" "/p:VISIO_LIB_PATH=$vislib" | Write-Host