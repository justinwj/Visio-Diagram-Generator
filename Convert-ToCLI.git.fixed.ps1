
<#
.SYNOPSIS
  Converts the repo to a CLI‑first layout and (optionally) initializes/ pushes to GitHub.

.DESCRIPTION
  - Archives add‑in/installer/plugin‑host folders into /backlog (timestamped, non‑destructive).
  - Backs up any existing *.sln into /backlog/_archived_sln.
  - Creates a fresh solution (defaults to "Visio-Diagram-Generator.sln").
  - Auto‑discovers SDK projects under /src and /tests and adds them (excluding add‑in patterns).
  - Optionally initializes git, writes .gitignore/.gitattributes if missing, commits and pushes.

.PARAMETER Root
  Repo root. Default: "."

.PARAMETER IncludeTests
  Include tests from /tests in the solution. Default: $true

.PARAMETER SolutionName
  Solution name without extension. Default: "Visio-Diagram-Generator"

.PARAMETER DryRun
  Show actions without making changes.

.PARAMETER GitPush
  When set, performs git init/add/commit/branch/remote/push.

.PARAMETER RemoteUrl
  The git remote URL to set as 'origin' (e.g., https://github.com/<you>/Visio-Diagram-Generator.git or git@github.com:<you>/Visio-Diagram-Generator.git).

.PARAMETER DefaultBranch
  Default branch name (used when initializing/renaming). Default: "main".

.PARAMETER ForceRemote
  Overwrite existing 'origin' remote if present.
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
  [Parameter(Position=0)]
  [string]$Root = ".",
  [bool]$IncludeTests = $true,
  [string]$SolutionName = "Visio-Diagram-Generator",
  [switch]$DryRun,
  [switch]$GitPush,
  [string]$RemoteUrl,
  [string]$DefaultBranch = "main",
  [switch]$ForceRemote
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# region: helpers
function Test-CommandAvailable {
  param([Parameter(Mandatory)][string]$Name,[string]$InstallHint)
  $cmd = Get-Command $Name -ErrorAction SilentlyContinue
  if (-not $cmd) {
    $msg = "Required command '$Name' not found on PATH."
    if ($InstallHint) { $msg += " $InstallHint" }
    throw $msg
  }
  return $cmd
}

function New-Directory([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) {
    New-Item -ItemType Directory -Path $Path | Out-Null
  }
}

function Get-Timestamp() {
  # yyyyMMdd-HHmmss
  return (Get-Date).ToString("yyyyMMdd-HHmmss")
}

function Move-ItemSafe {
  param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Destination,
    [switch]$WhatIfOnly
  )
  if (-not (Test-Path -LiteralPath $Source)) { return }
  $destParent = Split-Path -Parent $Destination
  New-Directory $destParent

  $finalDest = $Destination
  if (Test-Path -LiteralPath $finalDest) {
    $finalDest = "$Destination-$((Get-Timestamp))"
  }

  if ($WhatIfOnly) {
    Write-Host "[DRY-RUN] MOVE `"$Source`" -> `"$finalDest`""
  } else {
    Move-Item -LiteralPath $Source -Destination $finalDest -Force
    Write-Host "Moved: `"$Source`" -> `"$finalDest`""
  }
}

function Save-ItemBackup {
  param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$BackupRoot,
    [switch]$WhatIfOnly
  )
  if (-not (Test-Path -LiteralPath $Path)) { return $null }

  New-Directory $BackupRoot
  $name = Split-Path -Leaf $Path
  $dest = Join-Path $BackupRoot "$name.bak-$((Get-Timestamp))"

  if ($WhatIfOnly) {
    Write-Host "[DRY-RUN] BACKUP `"$Path`" -> `"$dest`""
  } else {
    Move-Item -LiteralPath $Path -Destination $dest -Force
    Write-Host "Backed up: `"$Path`" -> `"$dest`""
  }
  return $dest
}

function Set-FileIfMissing {
  param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$Content)
  if (-not (Test-Path -LiteralPath $Path)) {
    $parent = Split-Path -Parent $Path
    if ($parent) { New-Directory $parent }
    Set-Content -Path $Path -Value $Content -NoNewline
    Write-Host "Created: $Path"
  } else {
    Write-Verbose "$Path already exists (left unchanged)."
  }
}

function Add-ProjectsToSolution {
  param([Parameter(Mandatory)][string]$SolutionPath,[bool]$IncludeTests = $true)

  $excludePatterns = @(
    'AddIn', 'Addin', 'VisioAddIn', 'VSTO', 'PluginHost', 'Installer', 'Installers', 'Setup', 'Packaging', 'OfficeAddin'
  )
  $projectGlobs = @("src\**\*.csproj","src\**\*.fsproj","src\**\*.vbproj")
  if ($IncludeTests) {
    $projectGlobs += @("tests\**\*.csproj","tests\**\*.fsproj","tests\**\*.vbproj")
  }

  $allProjects = @()
  foreach ($g in $projectGlobs) {
    $allProjects += Get-ChildItem -Path $g -File -Recurse -ErrorAction SilentlyContinue
  }
  $allProjects = $allProjects | Sort-Object FullName -Unique

  $backlogAbs = $null
  if (Test-Path -LiteralPath "backlog") {
    $backlogAbs = (Resolve-Path "backlog").Path
  }

  $filtered = $allProjects | Where-Object {
    $p = $_.FullName
    ($null -eq $backlogAbs -or ($p -notlike ("*" + $backlogAbs + "*"))) -and
    (-not ($excludePatterns | Where-Object { $p -match $_ }))
  }

  foreach ($proj in $filtered) {
    $pathRel = (Resolve-Path -Relative $proj.FullName)
    & dotnet sln $SolutionPath add $pathRel | Out-Null
    Write-Host "Added to solution: $pathRel"
  }
}

function Initialize-GitRepository {
  param([string]$DefaultBranch = "main")
  if (-not (Test-Path -LiteralPath ".git")) {
    try {
      git init -b $DefaultBranch | Out-Null
    } catch {
      git init | Out-Null
      git branch -M $DefaultBranch | Out-Null
    }
    Write-Host "Initialized git repository on branch '$DefaultBranch'."
  } else {
    git branch -M $DefaultBranch | Out-Null
  }
}

function Publish-GitRepository {
  param([string]$RemoteUrl,[string]$DefaultBranch = "main",[switch]$ForceRemote)

  # .gitignore default
  $gitignore = @"
# Build outputs
bin/
obj/
out/
artifacts/
TestResults/

# IDE
.vs/
.vscode/
*.user
*.suo
*.userosscache
*.sln.docstates
.idea/

# NuGet / packages
*.nupkg
.nuget/
packages/
.packages/

# OS files
.DS_Store
Thumbs.db

# Logs & coverage
*.log
coverage/
.coverage
*.coverage
*.coveragexml

# Local config
*.env
local.settings.json
appsettings.Development.json

# Archives / legacy (created by Convert-ToCLI)
backlog/
installers/
"@

  $gitattributes = @"
* text=auto
*.bat text eol=crlf
*.cmd text eol=crlf
*.ps1 text eol=crlf
*.sh text eol=lf
*.cs text diff=csharp
*.fs text
*.vb text
*.sln text eol=crlf
*.props text
*.targets text
*.json text
*.yml text
*.yaml text
*.md text
"@

  Set-FileIfMissing ".gitignore" $gitignore
  Set-FileIfMissing ".gitattributes" $gitattributes

  git add -A
  try {
    git commit -m "CLI-first conversion; archive add-in projects" | Out-Null
    Write-Host "Committed changes."
  } catch {
    Write-Host "Nothing to commit (working tree clean)."
  }

  if ($RemoteUrl) {
    $hasOrigin = (git remote) -contains "origin"
    if ($hasOrigin -and $ForceRemote) {
      git remote remove origin | Out-Null
      $hasOrigin = $false
    }
    if (-not $hasOrigin) {
      git remote add origin $RemoteUrl | Out-Null
      Write-Host "Added remote 'origin' -> $RemoteUrl"
    } else {
      Write-Host "Remote 'origin' already exists."
    }
    git push -u origin $DefaultBranch
  } else {
    Write-Host "RemoteUrl not provided. Skipping push. You can set it later with:"
    Write-Host "  git remote add origin https://github.com/<you>/Visio-Diagram-Generator.git"
    Write-Host "  git push -u origin $DefaultBranch"
  }
}
# endregion helpers

Push-Location $Root
try {
  # Validate tools
  Test-CommandAvailable dotnet "Install .NET SDK 8+ from https://dotnet.microsoft.com/download" | Out-Null
  if ($GitPush) { Test-CommandAvailable git "Install Git from https://git-scm.com" | Out-Null }

  # Basic structure check
  if (-not (Test-Path -LiteralPath (Join-Path "." "src"))) {
    throw "Expected a 'src' directory under '$Root'. Please run at the repository root."
  }

  # Prepare backlog
  $backlogRoot = Join-Path "." "backlog"
  $archivedSln = Join-Path $backlogRoot "_archived_sln"
  $addinsLegacy = Join-Path $backlogRoot "addins_legacy"
  New-Directory $backlogRoot
  New-Directory $archivedSln
  New-Directory $addinsLegacy

  # Archive legacy add-in content
  $legacyMap = @{
    (Join-Path "src"  "VDG.VisioAddIn")                   = Join-Path $addinsLegacy "VDG.VisioAddIn"
    (Join-Path "tests" "VDG.VisioAddIn.Tests")            = Join-Path $addinsLegacy "VDG.VisioAddIn.Tests"
    (Join-Path "src"  "VisioDiagramGenerator.PluginHost") = Join-Path $addinsLegacy "VisioDiagramGenerator.PluginHost"
    "installers"                                          = Join-Path $backlogRoot "installers"
  }

  foreach ($kv in $legacyMap.GetEnumerator()) {
    $src = $kv.Key
    $dst = $kv.Value
    if ($PSCmdlet.ShouldProcess($src, "Archive to $dst")) {
      Move-ItemSafe -Source $src -Destination $dst -WhatIfOnly:$DryRun
    }
  }

  # Backup existing .sln
  Get-ChildItem -File -Filter "*.sln" -ErrorAction SilentlyContinue | ForEach-Object {
    if ($PSCmdlet.ShouldProcess($_.FullName, "Backup to $archivedSln")) {
      Save-ItemBackup -Path $_.FullName -BackupRoot $archivedSln -WhatIfOnly:$DryRun | Out-Null
    }
  }

  # Create new solution
  $slnPath = Join-Path "." "$SolutionName.sln"
  if ($PSCmdlet.ShouldProcess($slnPath, "Create new solution")) {
    if (-not $DryRun) {
      & dotnet new sln -n $SolutionName | Out-Null
    } else {
      Write-Host "[DRY-RUN] dotnet new sln -n $SolutionName"
    }
  }

  if (-not $DryRun) {
    Add-ProjectsToSolution -SolutionPath $slnPath -IncludeTests:$IncludeTests
  } else {
    Write-Host "[DRY-RUN] Would add projects under /src (and /tests if enabled) to $slnPath"
  }

  Write-Host ""
  Write-Host "✔ CLI-first conversion done. Solution: $slnPath"
  if ($DryRun) { Write-Host "  (Dry-run mode; no changes were made.)" }

  if ($GitPush) {
    if ($DryRun) {
      Write-Host "[DRY-RUN] Would initialize git and push to '$RemoteUrl' on '$DefaultBranch'."
    } else {
      Initialize-GitRepository -DefaultBranch $DefaultBranch
      Publish-GitRepository -RemoteUrl $RemoteUrl -DefaultBranch $DefaultBranch -ForceRemote:$ForceRemote
    }
  }
}
finally {
  Pop-Location
}
