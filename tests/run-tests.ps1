# run-tests.ps1 — run all unit tests (p9–11 included)
# Usage:  .\run-tests.ps1 [-Configuration Debug|Release]

param(
  [ValidateSet('Debug','Release')]
  [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Resolve repo root (works whether you're in subfolders or the root)
try {
  $repoRoot = (git rev-parse --show-toplevel) 2>$null
  if (-not $repoRoot) { $repoRoot = (Resolve-Path ".").Path }
} catch { $repoRoot = (Resolve-Path ".").Path }
Set-Location $repoRoot

Write-Host "Repo root: $repoRoot"
dotnet --info | Out-Null

# Prefer solution if present; otherwise test all projects under /tests
$solution = Join-Path $repoRoot "Visio-Diagram-Generator.sln"
$testsDir = Join-Path $repoRoot "tests"

# Restore & build once
if (Test-Path $solution) {
  dotnet restore $solution
  dotnet build $solution -c $Configuration -v minimal
} else {
  dotnet restore
  dotnet build -c $Configuration -v minimal
}

# Run tests (all test projects). Results saved under artifacts\test-results
$resultsDir = Join-Path $repoRoot "artifacts\test-results"
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

$testArgs = @(
  '--no-build',
  '-c', $Configuration,
  '--logger', 'trx;LogFileName=TestResults.trx',
  '--results-directory', $resultsDir
)

if (Test-Path $solution) {
  dotnet test $solution @testArgs
} elseif (Test-Path $testsDir) {
  dotnet test $testsDir @testArgs
} else {
  Write-Error "Couldn't find a solution or the 'tests' folder."
  exit 2
}

Write-Host "Done. Test results: $resultsDir" -ForegroundColor Green
