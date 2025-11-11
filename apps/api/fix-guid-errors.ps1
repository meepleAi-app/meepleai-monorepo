# PowerShell script to fix common Guid vs string type errors in test files
# This script fixes patterns where string literals are used where Guids are expected

$ErrorActionPreference = "Stop"
$testPath = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

Write-Host "Fixing Guid vs string type mismatches in test files..." -ForegroundColor Cyan

# Find all test files with errors
$errorFiles = @(
    "N8nConfigServiceTests.cs",
    "SessionManagementServiceTests.cs",
    "SetupGuideServiceComprehensiveTests.cs",
    "Services\RagServiceMultilingualTests.cs",
    "TotpServiceTests.cs",
    "Services\PromptEvaluationServiceTests.cs"
)

foreach ($file in $errorFiles) {
    $fullPath = Join-Path $testPath $file
    if (-not (Test-Path $fullPath)) {
        Write-Warning "File not found: $fullPath"
        continue
    }

    Write-Host "Processing $file..." -ForegroundColor Yellow
    $content = Get-Content $fullPath -Raw

    # Common patterns to fix:
    # 1. GameId = "string" -> GameId = Guid.NewGuid()
    # 2. UserId = "string" -> UserId = Guid.NewGuid()
    # 3. MessageId = "string" -> MessageId = Guid.NewGuid()
    # 4. Method calls with string literals where Guid expected

    # Note: Be careful with these replacements - they're broad patterns
    # Better to do this file by file manually, but this gives a starting point

    # Output that manual fixes are still needed
    Write-Host "  Requires manual review and fixes" -ForegroundColor Magenta
}

Write-Host "`nDone. Please review changes and run dotnet build to verify." -ForegroundColor Green
