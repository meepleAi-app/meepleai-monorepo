# Fix xUnit1051 warnings: Add TestContext.Current.CancellationToken to async calls
# Usage: pwsh tools/fix-xunit1051.ps1

param(
    [switch]$DryRun = $false
)

$testPath = "apps/api/tests/Api.Tests"
$files = Get-ChildItem -Path $testPath -Recurse -Filter "*.cs"

$patterns = @(
    @{Pattern = '\.SaveChangesAsync\(\)'; Replacement = '.SaveChangesAsync(TestContext.Current.CancellationToken)'},
    @{Pattern = '\.AddAsync\(([^)]+)\)(?!.*CancellationToken)'; Replacement = '.AddAsync($1, TestContext.Current.CancellationToken)'},
    @{Pattern = '\.AddRangeAsync\(([^)]+)\)(?!.*CancellationToken)'; Replacement = '.AddRangeAsync($1, TestContext.Current.CancellationToken)'},
    @{Pattern = '\.FirstOrDefaultAsync\(\)'; Replacement = '.FirstOrDefaultAsync(TestContext.Current.CancellationToken)'},
    @{Pattern = '\.ToListAsync\(\)'; Replacement = '.ToListAsync(TestContext.Current.CancellationToken)'},
    @{Pattern = '\.AnyAsync\(\)'; Replacement = '.AnyAsync(TestContext.Current.CancellationToken)'},
    @{Pattern = '\.CountAsync\(\)'; Replacement = '.CountAsync(TestContext.Current.CancellationToken)'}
)

$totalChanges = 0
$filesModified = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    $fileChanges = 0

    foreach ($patternInfo in $patterns) {
        $regex = [regex]$patternInfo.Pattern
        $matches = $regex.Matches($content)

        if ($matches.Count -gt 0) {
            $content = $regex.Replace($content, $patternInfo.Replacement)
            $fileChanges += $matches.Count
        }
    }

    if ($content -ne $originalContent) {
        $filesModified++
        $totalChanges += $fileChanges

        Write-Host "  $($file.Name): $fileChanges changes" -ForegroundColor Cyan

        if (-not $DryRun) {
            Set-Content -Path $file.FullName -Value $content -NoNewline
        }
    }
}

Write-Host "`nSummary:" -ForegroundColor Green
Write-Host "   Files modified: $filesModified" -ForegroundColor White
Write-Host "   Total changes: $totalChanges" -ForegroundColor White

if ($DryRun) {
    Write-Host "`nDRY RUN - No files modified. Remove -DryRun to apply changes." -ForegroundColor Yellow
} else {
    Write-Host "`nChanges applied successfully!" -ForegroundColor Green
}
