<#
.SYNOPSIS
Generates a review dashboard by aggregating .review.json and diagnostics JSON artifacts.

.DESCRIPTION
Scans one or more directories for review metadata emitted by VDG (either the `.review.json`
produced next to diagrams or the `ReviewSummary` block inside diagnostics files), then
summarises each artifact into a Markdown table. The output highlights warning counts,
records the thresholds used, and links back to the source files so reviewers can drill in.

.PARAMETER ScanPath
One or more directories to search (recursive). Defaults to `out/fixtures`, `tests/fixtures/render`,
and `samples`.

.PARAMETER OutputPath
Destination for the generated Markdown dashboard. Defaults to `plan docs/review_dashboard.md`.

.PARAMETER RecentDays
If set, only include artifacts whose last write time is within the specified number of days.

.PARAMETER WarningHighlightThreshold
How many warnings trigger the ⚠️ highlight glyph (default 0).

.PARAMETER IncludeDiagnosticsOnly
Include diagnostics summaries even when a `.review.json` exists for the same diagram.

.EXAMPLE
pwsh ./tools/summarize-reviews.ps1 -ScanPath out/fixtures -OutputPath "plan docs/review_dashboard.md"

.EXAMPLE
pwsh ./tools/summarize-reviews.ps1 -RecentDays 3 -WarningHighlightThreshold 2
#>
[CmdletBinding()]
param(
    [string[]]$ScanPath = @("out/fixtures", "tests/fixtures/render", "samples"),
    [string]$OutputPath = "plan docs/review_dashboard.md",
    [int]$RecentDays,
    [int]$WarningHighlightThreshold = 0,
    [switch]$IncludeDiagnosticsOnly,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
    $here = $PSScriptRoot
    if (-not $here) {
        $here = Split-Path -Parent (Get-Command $MyInvocation.InvocationName).Path
    }
    return (Resolve-Path (Join-Path $here '..')).ProviderPath
}

$repoRoot = Resolve-RepoRoot

function Resolve-PathSafe {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $combined = Join-Path $repoRoot $Path
    if (Test-Path $combined) {
        return (Resolve-Path $combined).ProviderPath
    }
    return $null
}

function Get-RelativePath {
    param([string]$FullPath)
    if ([string]::IsNullOrWhiteSpace($FullPath)) { return $FullPath }
    return [System.IO.Path]::GetRelativePath($repoRoot, $FullPath)
}

function Get-ArtifactKey {
    param([string]$FullPath)
    $dir = [System.IO.Path]::GetDirectoryName($FullPath)
    $name = [System.IO.Path]::GetFileNameWithoutExtension($FullPath) # remove .json
    $name = $name -replace '\.(review|diagnostics)$', ''
    return [System.IO.Path]::Combine($dir, $name).ToLowerInvariant()
}

function ConvertTo-Array {
    param($Value)
    if ($null -eq $Value) { return @() }
    if ($Value -is [System.Array]) { return $Value }
    return @($Value)
}

function Get-JsonValue {
    param(
        $Object,
        [string]$Name
    )
    if ($null -eq $Object -or [string]::IsNullOrWhiteSpace($Name)) { return $null }

    if ($Object -is [System.Collections.IDictionary]) {
        foreach ($key in $Object.Keys) {
            if ($key -eq $Name) { return $Object[$key] }
        }
    }

    if ($Object -is [psobject]) {
        $prop = $Object.PSObject.Properties |
            Where-Object { $_.Name -eq $Name } |
            Select-Object -First 1
        if ($prop) { return $prop.Value }
    }
    return $null
}

function Get-ItemCount {
    param($Value)
    $array = ConvertTo-Array $Value
    return ($array | Measure-Object).Count
}

function Parse-ReviewSummary {
    param(
        [string]$SourcePath,
        [string]$Type
    )
    try {
        $content = Get-Content -Path $SourcePath -Raw -Encoding UTF8
        $json = $content | ConvertFrom-Json -Depth 64
    }
    catch {
        throw "Failed to parse $SourcePath ($Type): $_"
    }
    if ($null -eq $json) { return $null }

    $summary = $json
    if ($Type -eq 'diagnostics') {
        $reviewNode = Get-JsonValue $json 'ReviewSummary'
        if ($null -eq $reviewNode) {
            return $null
        }
        $summary = $reviewNode
    }

    $settings = Get-JsonValue $summary 'settings'
    if ($null -eq $settings) {
        $settings = [pscustomobject]@{
            minimumSeverity      = 'warning'
            roleConfidenceCutoff = 0.55
            flowResidualCutoff   = 1600
        }
    }

    $lastWrite = (Get-Item $SourcePath).LastWriteTimeUtc
    return [pscustomobject]@{
        DiagramName     = [System.IO.Path]::GetFileNameWithoutExtension($SourcePath) -replace '\.(review|diagnostics)$', ''
        SourcePath      = $SourcePath
        SourceType      = $Type
        LastWrite       = [DateTime]::SpecifyKind($lastWrite, [DateTimeKind]::Utc)
        Severity        = $settings.minimumSeverity
        RoleCutoff      = [double]$settings.roleConfidenceCutoff
        FlowCutoff      = [int]$settings.flowResidualCutoff
        InfoCount       = Get-ItemCount (Get-JsonValue $summary 'info')
        WarningCount    = Get-ItemCount (Get-JsonValue $summary 'warnings')
        SuggestionCount = Get-ItemCount (Get-JsonValue $summary 'suggestions')
        Notes           = (ConvertTo-Array (Get-JsonValue $summary 'notes')) -join '; '
    }
}

$records = @()
$processedKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$cutoffDate = if ($RecentDays -gt 0) { (Get-Date).ToUniversalTime().AddDays(-$RecentDays) } else { $null }

foreach ($path in $ScanPath) {
    $resolved = Resolve-PathSafe -Path $path
    if (-not $resolved) {
        if (-not $Quiet) { Write-Verbose "Scan path not found: $path" }
        continue
    }

    $reviewFiles = Get-ChildItem -Path $resolved -Recurse -Filter '*.review.json' -File -ErrorAction SilentlyContinue
    foreach ($file in $reviewFiles) {
        if ($cutoffDate -and $file.LastWriteTimeUtc -lt $cutoffDate) { continue }
        $key = Get-ArtifactKey -FullPath $file.FullName
        $summary = Parse-ReviewSummary -SourcePath $file.FullName -Type 'review'
        if ($summary) {
            $records += $summary
            $null = $processedKeys.Add($key)
        }
    }

    $diagnosticFiles = Get-ChildItem -Path $resolved -Recurse -Filter '*.diagnostics.json' -File -ErrorAction SilentlyContinue
    foreach ($diag in $diagnosticFiles) {
        if ($cutoffDate -and $diag.LastWriteTimeUtc -lt $cutoffDate) { continue }
        $key = Get-ArtifactKey -FullPath $diag.FullName
        if (-not $IncludeDiagnosticsOnly -and $processedKeys.Contains($key)) {
            continue
        }
        $summary = Parse-ReviewSummary -SourcePath $diag.FullName -Type 'diagnostics'
        if ($summary) {
            $records += $summary
            $null = $processedKeys.Add($key)
        }
    }
}

$records = $records | Sort-Object LastWrite -Descending

$outputFullPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $repoRoot $OutputPath
}
$outputDir = Split-Path -Parent $outputFullPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$timestamp = (Get-Date).ToUniversalTime().ToString("u")
$total = $records.Count
$warned = ($records | Where-Object { $_.WarningCount -gt 0 }).Count
$suggestOnly = ($records | Where-Object { $_.WarningCount -eq 0 -and $_.SuggestionCount -gt 0 }).Count

$sb = [System.Text.StringBuilder]::new()
if ($total -eq 0) {
    $null = $sb.AppendLine("# Review Dashboard")
    $null = $sb.AppendLine()
    $null = $sb.AppendLine("_No review artifacts were found in the scanned paths._")
}
else {
    $null = $sb.AppendLine("# Review Dashboard")
    $null = $sb.AppendLine()
    $null = $sb.AppendLine("Generated: $timestamp")
    if ($RecentDays -gt 0) {
        $null = $sb.AppendLine("Filtered to the last $RecentDays day(s).")
    }
    $null = $sb.AppendLine()
    $null = $sb.AppendLine("- Total artifacts: $total")
    $null = $sb.AppendLine("- With warnings: $warned")
    $null = $sb.AppendLine("- Suggestion-only: $suggestOnly")
    $null = $sb.AppendLine("- Warning highlight threshold: $WarningHighlightThreshold")
    $null = $sb.AppendLine()
    $null = $sb.AppendLine("## Aggregated Results")
    $null = $sb.AppendLine("| Status | Diagram | Updated (UTC) | Severity >= | Role Cutoff | Flow Cutoff | Info | Warnings | Suggestions | Notes | Source |")
    $null = $sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|")

    foreach ($record in $records) {
        $status = if ($record.WarningCount -gt $WarningHighlightThreshold) { "WARN" }
                  elseif ($record.SuggestionCount -gt 0) { "INFO" }
                  else { "OK" }
        $notesText = if ([string]::IsNullOrWhiteSpace($record.Notes)) { "-" } else { $record.Notes.Replace("|", "\|") }
        $relative = Get-RelativePath -FullPath $record.SourcePath
        $sourceLabel = $record.SourceType
        $sourceLink = "[${sourceLabel}]($relative)"
        $diagram = $record.DiagramName.Replace("|", "\|")
        $null = $sb.AppendLine("| $status | $diagram | $($record.LastWrite.ToString('u')) | $($record.Severity) | $($record.RoleCutoff) | $($record.FlowCutoff) | $($record.InfoCount) | $($record.WarningCount) | $($record.SuggestionCount) | $notesText | $sourceLink |")
    }
}

$sb.ToString() | Out-File -FilePath $outputFullPath -Encoding UTF8

if (-not $Quiet) {
    Write-Host "Review dashboard written to $outputFullPath"
}
