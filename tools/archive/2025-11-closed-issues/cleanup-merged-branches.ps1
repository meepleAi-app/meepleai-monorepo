# Cleanup script for merged branches
# Deletes local branches that have merged PRs

$branchesToCheck = @(
    "edit-04-visual-diff-viewer",
    "edit-05-enhanced-comments",
    "admin-02-analytics-dashboard",
    "config-02",
    "config-05",
    "config-07-testing-documentation",
    "sec-11-phase3-justification-comments",
    "meepleAi-app/issue428",
    "meepleAi-app/issue420",
    "meepleAi-app/issue418"
)

Write-Host "Checking branches for merged PRs..." -ForegroundColor Cyan

foreach ($branch in $branchesToCheck) {
    # Check if branch exists locally
    $branchExists = git show-ref --verify --quiet "refs/heads/$branch"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ⊘ $branch - not found locally" -ForegroundColor Gray
        continue
    }

    # Check for merged PR
    $prInfo = gh pr list --state merged --head $branch --json number,state,title 2>&1 | ConvertFrom-Json

    if ($prInfo -and $prInfo.Count -gt 0) {
        $prNumber = $prInfo[0].number
        $prTitle = $prInfo[0].title

        Write-Host "  ✓ $branch - PR #$prNumber MERGED: $prTitle" -ForegroundColor Green
        Write-Host "    Deleting branch..." -ForegroundColor Yellow

        git branch -D $branch 2>&1 | Out-Null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "    ✓ Deleted" -ForegroundColor Green
        } else {
            Write-Host "    ✗ Failed to delete" -ForegroundColor Red
        }
    } else {
        Write-Host "  ⚠ $branch - No merged PR found (keeping branch)" -ForegroundColor Yellow
    }
}

Write-Host "`nBranch cleanup complete!" -ForegroundColor Cyan
