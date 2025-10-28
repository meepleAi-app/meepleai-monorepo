#!/usr/bin/env pwsh
# Fix CA2000 errors for SqliteConnection - wrap in using statement

$ErrorActionPreference = "Stop"

$files = @(
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\GameServiceTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\N8nConfigServiceTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\AgentFeedbackServiceTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\PdfStorageServiceTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\AiRequestLogServiceTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\Ai04IntegrationTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\Ai04ComprehensiveTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\RagServiceTests.cs"
)

foreach ($file in $files) {
    Write-Host "Processing: $file" -ForegroundColor Cyan

    if (!(Test-Path $file)) {
        Write-Host "  File not found, skipping..." -ForegroundColor Yellow
        continue
    }

    $content = Get-Content $file -Raw

    # Pattern 1: var connection = new SqliteConnection(...);
    $pattern1 = 'var connection = new SqliteConnection\("Filename=:memory:"\);'
    $replacement1 = 'using var connection = new SqliteConnection("Filename=:memory:");'

    if ($content -match $pattern1) {
        $content = $content -replace $pattern1, $replacement1
        Write-Host "  ✓ Fixed: var connection" -ForegroundColor Green
    }

    # Pattern 2: SqliteConnection connection = new SqliteConnection(...);
    $pattern2 = 'SqliteConnection connection = new SqliteConnection\("Filename=:memory:"\);'
    $replacement2 = 'using SqliteConnection connection = new SqliteConnection("Filename=:memory:");'

    if ($content -match $pattern2) {
        $content = $content -replace $pattern2, $replacement2
        Write-Host "  ✓ Fixed: SqliteConnection connection" -ForegroundColor Green
    }

    # Write back
    Set-Content -Path $file -Value $content -NoNewline
}

Write-Host "`n✅ All SqliteConnection CA2000 errors fixed!" -ForegroundColor Green
