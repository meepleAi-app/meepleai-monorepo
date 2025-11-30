# Fix MA0006 warnings: Use string.Equals instead of == or != operators
# Usage: pwsh tools/fix-ma0006.ps1 [-DryRun]
#
# Strategy: Use dotnet format analyzers with specific diagnostic ID (MA0006)
# This is the SAFEST approach - uses official tooling, not regex hacks.

param(
    [switch]$DryRun = $false
)

$projectPath = "apps/api/src/Api/Api.csproj"

Write-Host "Fixing MA0006 warnings using dotnet format analyzers..." -ForegroundColor Cyan

# Count before
Write-Host "`nCounting MA0006 warnings before fix..." -ForegroundColor Yellow
$beforeCount = (dotnet build "$projectPath" --no-incremental 2>&1 | Select-String "warning MA0006").Count
Write-Host "Before: $beforeCount warnings" -ForegroundColor White

if ($beforeCount -eq 0) {
    Write-Host "No MA0006 warnings found!" -ForegroundColor Green
    exit 0
}

# Apply fixes
if ($DryRun) {
    Write-Host "`nDRY RUN - Would apply fixes with:" -ForegroundColor Yellow
    Write-Host "dotnet format analyzers $projectPath --diagnostics MA0006 --verbosity normal" -ForegroundColor Cyan
} else {
    Write-Host "`nApplying MA0006 fixes..." -ForegroundColor Green
    dotnet format analyzers "$projectPath" --diagnostics MA0006 --verbosity normal

    # Count after
    Write-Host "`nCounting MA0006 warnings after fix..." -ForegroundColor Yellow
    $afterCount = (dotnet build "$projectPath" --no-incremental 2>&1 | Select-String "warning MA0006").Count
    Write-Host "After: $afterCount warnings" -ForegroundColor White

    $fixed = $beforeCount - $afterCount
    Write-Host "`nFixed: $fixed warnings" -ForegroundColor Green

    if ($afterCount -gt 0) {
        Write-Host "Warning: $afterCount warnings remain (manual review needed)" -ForegroundColor Yellow
    }
}

Write-Host "`nDone!" -ForegroundColor Green
