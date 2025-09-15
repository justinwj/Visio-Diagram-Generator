param(
  [ValidateSet('Debug','Release')] [string]$Configuration = 'Debug',
  [switch]$Clean,
  [switch]$BinLog,
  [string]$MSBuildPath
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = "$env:ProgramFiles\dotnet"

function Resolve-MSBuild {
  param([string]$Override)
  if ($Override) {
    if (Test-Path -LiteralPath $Override) { return $Override }
    throw "MSBuild override not found: $Override"
  }

  $pf   = $env:ProgramFiles
  $pf86 = ${env:ProgramFiles(x86)}
  $candidates = @(
    # Your exact installs (from screenshot)
    "$pf\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
    "$pf\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "$pf86\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe",
    "$pf86\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
  )
  foreach ($p in ($candidates | Select-Object -Unique)) {
    if (Test-Path -LiteralPath $p) { return $p }
  }
  $list = ($candidates | Select-Object -Unique) -join "`n  "
  throw "MSBuild.exe not found. Checked:`n  $list"
}

$msbuild = Resolve-MSBuild -Override $MSBuildPath
Write-Host "MSBuild -> $msbuild"

if ($Clean) {
  Get-ChildItem -Recurse -Directory -Force |
    Where-Object { $_.Name -in @('bin','obj') } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

$targets = if ($Clean) { 'Clean;Restore;Build' } else { 'Restore;Build' }
$args = @(
  ".\Visio-Diagram-Generator.sln",
  "/t:$targets",
  "/p:Configuration=$Configuration",
  "/m", "/v:m"
)
if ($BinLog) {
  New-Item -ItemType Directory -Force -Path .\out | Out-Null
  $args += "/bl:out\msbuild.binlog"
}

& $msbuild @args
exit $LASTEXITCODE
