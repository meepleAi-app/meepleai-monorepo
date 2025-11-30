# Fix MA0015 warnings: Specify parameter name in ArgumentException
# Usage: pwsh tools/fix-ma0015.ps1 [-DryRun]

param(
    [switch]$DryRun = $false
)

$projectPath = "apps/api/src/Api/Api.csproj"

Write-Host "Fixing MA0015 warnings using dotnet format analyzers..." -ForegroundColor Cyan

# Count before
Write-Host "`nCounting MA0015 warnings before fix..." -ForegroundColor Yellow
$beforeCount = (dotnet build "$projectPath" --no-incremental 2>&1 | Select-String "warning MA0015").Count
Write-Host "Before: $beforeCount warnings" -ForegroundColor White

if ($beforeCount -eq 0) {
    Write-Host "No MA0015 warnings found!" -ForegroundColor Green
    exit 0
}

# Apply fixes
if ($DryRun) {
    Write-Host "`nDRY RUN - Would apply fixes with:" -ForegroundColor Yellow
    Write-Host "dotnet format analyzers $projectPath --diagnostics MA0015 --verbosity normal" -ForegroundColor Cyan
} else {
    Write-Host "`nApplying MA0015 fixes..." -ForegroundColor Green
    dotnet format analyzers "$projectPath" --diagnostics MA0015 --verbosity normal

    # Count after
    Write-Host "`nCounting MA0015 warnings after fix..." -ForegroundColor Yellow
    $afterCount = (dotnet build "$projectPath" --no-incremental 2>&1 | Select-String "warning MA0015").Count
    Write-Host "After: $afterCount warnings" -ForegroundColor White

    $fixed = $beforeCount - $afterCount
    Write-Host "`nFixed: $fixed warnings" -ForegroundColor Green

    if ($afterCount -gt 0) {
        Write-Host "Warning: $afterCount warnings remain (manual review needed)" -ForegroundColor Yellow
    }
}

Write-Host "`nDone!" -ForegroundColor Green
