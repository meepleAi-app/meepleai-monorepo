# Lock all closed GitHub issues
# Prevents further comments/activity on resolved issues

param(
    [switch]$DryRun,
    [int]$BatchSize = 50
)

Write-Host "🔒 Locking Closed GitHub Issues" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Get all closed issues
Write-Host "📋 Fetching closed issues..." -ForegroundColor Yellow
$closedIssues = gh issue list --state closed --limit 1000 --json number | ConvertFrom-Json

$totalIssues = $closedIssues.Count
Write-Host "✓ Found $totalIssues closed issues" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No issues will be locked" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Would lock issues:" -ForegroundColor Yellow
    $closedIssues | Select-Object -First 10 | ForEach-Object {
        Write-Host "  - Issue #$($_.number)" -ForegroundColor Gray
    }
    if ($totalIssues -gt 10) {
        Write-Host "  ... and $($totalIssues - 10) more" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Run without -DryRun to execute" -ForegroundColor Yellow
    exit 0
}

# Lock issues in batches
$locked = 0
$failed = 0
$failedIssues = @()

Write-Host "🔒 Locking issues (batch size: $BatchSize)..." -ForegroundColor Yellow
Write-Host ""

foreach ($issue in $closedIssues) {
    $issueNumber = $issue.number

    try {
        # Lock with "resolved" reason
        gh issue lock $issueNumber --reason "resolved" 2>$null
        $locked++

        # Progress indicator every 10 issues
        if ($locked % 10 -eq 0) {
            $percent = [math]::Round(($locked / $totalIssues) * 100, 1)
            Write-Host "  Progress: $locked/$totalIssues ($percent%)" -ForegroundColor Cyan
        }

        # Rate limiting: pause every batch
        if ($locked % $BatchSize -eq 0) {
            Write-Host "  ⏸️  Batch complete, pausing 5s..." -ForegroundColor Gray
            Start-Sleep -Seconds 5
        }
    }
    catch {
        $failed++
        $failedIssues += $issueNumber
        Write-Host "  ❌ Failed to lock issue #$issueNumber : $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║   Lock Operation Complete              ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "✅ Successfully locked: $locked issues" -ForegroundColor Green
if ($failed -gt 0) {
    Write-Host "❌ Failed to lock: $failed issues" -ForegroundColor Red
    Write-Host ""
    Write-Host "Failed issues:" -ForegroundColor Yellow
    $failedIssues | ForEach-Object {
        Write-Host "  - Issue #$_" -ForegroundColor Gray
    }
}
Write-Host ""
Write-Host "🔒 All closed issues are now locked (no new comments allowed)" -ForegroundColor Cyan
