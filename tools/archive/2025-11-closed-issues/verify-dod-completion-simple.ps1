<#
.SYNOPSIS
    Simplified DoD verification script - generates JSON data only

.DESCRIPTION
    This script analyzes closed issues and outputs JSON data for further processing
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyy-MM-dd-HHmmss"

Write-Host "`n=== DoD Verification (Simplified) ===" -ForegroundColor Cyan
Write-Host "Timestamp: $timestamp`n" -ForegroundColor Gray

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

try {
    $null = gh --version
    Write-Host "OK GitHub CLI found" -ForegroundColor Green
} catch {
    Write-Host "ERROR GitHub CLI not found" -ForegroundColor Red
    exit 1
}

# Get list of closed issues with DoD
Write-Host "`nFetching closed issues with DoD sections..." -ForegroundColor Yellow

$issuesJson = gh issue list --state closed --limit 100 --json number,title,body,state,labels | ConvertFrom-Json

$issuesWithDod = $issuesJson | Where-Object {
    $_.body -match "## DoD|## Definition of Done|## Acceptance Criteria"
}

Write-Host "Found $($issuesWithDod.Count) closed issues with DoD sections" -ForegroundColor Green

# Prepare results structure
$results = @{
    timestamp = $timestamp
    total_issues = $issuesWithDod.Count
    issues = @()
}

# Analyze each issue
Write-Host "`nAnalyzing issues..." -ForegroundColor Yellow

$issueCount = 0
foreach ($issue in $issuesWithDod) {
    $issueCount++
    $percent = [math]::Round(($issueCount / $issuesWithDod.Count) * 100)
    Write-Host "  [$percent%] Issue #$($issue.number): $($issue.title)" -ForegroundColor Cyan

    # Extract unchecked DoD items
    $dodItems = @()
    $lines = $issue.body -split "`n"
    $inDodSection = $false

    foreach ($line in $lines) {
        if ($line -match "^##\s+(DoD|Definition of Done|Acceptance Criteria|Testing Requirements?)") {
            $inDodSection = $true
            continue
        }

        if ($inDodSection -and $line -match "^##\s+") {
            $inDodSection = $false
        }

        if ($inDodSection -and $line -match "^-\s+\[\s*\]\s+(.+)$") {
            $dodItems += $Matches[1].Trim()
        }
    }

    if ($dodItems.Count -eq 0) {
        Write-Host "    No unchecked DoD items" -ForegroundColor Gray
        continue
    }

    Write-Host "    Found $($dodItems.Count) unchecked items" -ForegroundColor Yellow

    $results.issues += @{
        number = $issue.number
        title = $issue.title
        state = $issue.state
        labels = @($issue.labels | ForEach-Object { $_.name })
        unchecked_dod_items = $dodItems
        total_unchecked = $dodItems.Count
    }
}

# Save JSON results
$outputDir = "docs/issue"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$jsonPath = Join-Path $outputDir "dod-analysis-$timestamp.json"
$results | ConvertTo-Json -Depth 10 | Out-File -FilePath $jsonPath -Encoding UTF8

Write-Host "`n=== Results ===" -ForegroundColor Cyan
Write-Host "Total issues analyzed: $($results.issues.Count)" -ForegroundColor White
$totalUnchecked = 0
foreach ($iss in $results.issues) {
    $totalUnchecked += $iss.total_unchecked
}
Write-Host "Total unchecked DoD items: $totalUnchecked" -ForegroundColor Yellow
Write-Host "`nJSON output: $jsonPath" -ForegroundColor Green

if ($DryRun) {
    Write-Host "`nDRY RUN MODE - No changes applied" -ForegroundColor Yellow
}

Write-Host "`nNext: Run generate-dod-reports.ps1 to create detailed reports`n" -ForegroundColor Cyan
