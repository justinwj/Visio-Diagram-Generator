Param(
    [int]$Limit = 20
)

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "GitHub CLI (gh) is required for this audit script. Install it from https://cli.github.com/."
    exit 1
}

$json = gh pr list --state merged --limit $Limit --json number,title,body,author,mergedAt,url 2>$null
if (-not $json) {
    Write-Error "No PR data returned. Ensure you are authenticated with 'gh auth login'."
    exit 1
}

$prs = $json | ConvertFrom-Json
$results = @()

foreach ($pr in $prs) {
    $body = if ($pr.body) { $pr.body } else { '' }
    $match = [regex]::Match($body, '## IR Checklist Exception Rationale\s*(?<text>[\s\S]*?)(?:\n##|\Z)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) { continue }

    $text = $match.Groups['text'].Value
    $text = [System.Text.RegularExpressions.Regex]::Replace($text, '<!--[\s\S]*?-->', '')
    $text = $text.Trim()
    if ($text.Length -eq 0) { continue }

    $normalized = $text.ToLowerInvariant()
    if ($normalized -in @('none', 'n/a', 'na', 'pending', 'tbd')) { continue }
    if ($text.Length -lt 20) { continue }

    $results += [pscustomobject]@{
        Number   = $pr.number
        Title    = $pr.title
        Author   = if ($pr.author) { $pr.author.login } else { 'unknown' }
        MergedAt = $pr.mergedAt
        Url      = $pr.url
        RationaleSnippet = ($text -replace '\s+', ' ').Substring(0, [Math]::Min(120, $text.Length))
    }
}

if ($results.Count -eq 0) {
    Write-Host "No recent PRs contained an IR checklist exception rationale."
} else {
    $results | Format-Table -AutoSize
}
