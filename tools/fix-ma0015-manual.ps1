# Fix MA0015 warnings: Add parameter name to ArgumentException
# Usage: pwsh tools/fix-ma0015-manual.ps1 [-DryRun]
#
# Strategy: Parse warnings, identify ArgumentException without paramName, add it carefully
# This requires context analysis - semi-automated with manual review

param(
    [switch]$DryRun = $false
)

$projectPath = "apps/api/src/Api/Api.csproj"

Write-Host "Collecting MA0015 warnings..." -ForegroundColor Cyan
$buildOutput = dotnet build "$projectPath" --no-incremental 2>&1
$warnings = $buildOutput | Select-String "warning MA0015"

if ($warnings.Count -eq 0) {
    Write-Host "No MA0015 warnings found!" -ForegroundColor Green
    exit 0
}

Write-Host "Found $($warnings.Count) MA0015 warnings" -ForegroundColor Yellow
Write-Host "`nWARNING: MA0015 requires careful manual review!" -ForegroundColor Red
Write-Host "This script will generate suggested fixes, but you MUST review each one.`n" -ForegroundColor Yellow

# Parse warnings with file:line info
$warningList = @()
foreach ($warning in $warnings) {
    if ($warning -match '(.+\.cs)\((\d+),(\d+)\): warning MA0015: (.+) \(') {
        $warningList += @{
            File = $matches[1]
            Line = [int]$matches[2]
            Column = [int]$matches[3]
            Message = $matches[4]
        }
    }
}

# Group by file
$warningsByFile = $warningList | Group-Object -Property File

Write-Host "Warnings in $($warningsByFile.Count) files:" -ForegroundColor Cyan
foreach ($fileGroup in $warningsByFile) {
    $fileName = Split-Path $fileGroup.Name -Leaf
    Write-Host "  $fileName`: $($fileGroup.Count) warnings" -ForegroundColor Yellow
}

Write-Host "`nTo fix these warnings, you need to:" -ForegroundColor White
Write-Host "1. For 'Use an overload with parameter name':" -ForegroundColor White
Write-Host "   throw new ArgumentException(`"msg`") → throw new ArgumentException(`"msg`", nameof(param))" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. For 'X is not a valid parameter name':" -ForegroundColor White
Write-Host "   Review context - may be FALSE POSITIVE if in Command Handler!" -ForegroundColor Yellow
Write-Host "   ArgumentException(`"msg`", nameof(command.Property)) might be CORRECT" -ForegroundColor Cyan

if (-not $DryRun) {
    Write-Host "`nGenerating fix suggestions to 'ma0015-fixes.txt'..." -ForegroundColor Green
    $warningList | Format-Table -Property File, Line, Message -AutoSize | Out-File "ma0015-fixes.txt"
    Write-Host "Review ma0015-fixes.txt and apply fixes manually or with IDE refactoring." -ForegroundColor Cyan
}

Write-Host "`nDone! MA0015 requires manual review due to false positives." -ForegroundColor Green
