# Final comprehensive Guid fix for remaining errors

$testDir = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

# File 1: StreamingRagIntegrationTests.cs
Write-Host "Fixing StreamingRagIntegrationTests.cs..."
$file = Join-Path $testDir "StreamingRagIntegrationTests.cs"
$content = Get-Content $file -Raw
# Fix line 325: gameId parameter needs .ToString()
$content = $content -replace 'await AddTestDataAsync\(db, userId, (gameId)\)', 'await AddTestDataAsync(db, userId, $1.ToString())'
# Fix pdfId and gameId variables
$content = $content -replace 'var pdfId = \$"pdf-\{TestRunId\}-\{Guid\.NewGuid\(\):N\}";', 'var pdfId = Guid.NewGuid();'
$content = $content -replace 'var gameId = \$"game-\{TestRunId\}-\{Guid\.NewGuid\(\):N\}";', 'var gameId = Guid.NewGuid();'
# Fix PdfDocumentEntity assignments
$content = $content -replace 'Id = pdfId,', 'Id = pdfId.ToString(),'
$content = $content -replace 'GameId = gameId,', 'GameId = gameId.ToString(),'
Set-Content $file -Value $content -NoNewline

# File 2: SetupGuideEndpointIntegrationTests.cs
Write-Host "Fixing SetupGuideEndpointIntegrationTests.cs..."
$file = Join-Path $testDir "SetupGuideEndpointIntegrationTests.cs"
$content = Get-Content $file -Raw
# Add .ToString() to gameId parameters
$content = $content -replace 'await SeedPdfIndexDataAsync\(\w+, (gameId)\)', 'await SeedPdfIndexDataAsync($0, $1.ToString())'
Set-Content $file -Value $content -NoNewline

# File 3: StreamingQaEndpointIntegrationTests.cs
Write-Host "Fixing StreamingQaEndpointIntegrationTests.cs..."
$file = Join-Path $testDir "StreamingQaEndpointIntegrationTests.cs"
$content = Get-Content $file -Raw
# Add .ToString() to gameId parameters
$content = $content -replace 'await SeedPdfIndexDataAsync\(\w+, (gameId)\)', 'await SeedPdfIndexDataAsync($0, $1.ToString())'
Set-Content $file -Value $content -NoNewline

# File 4: SeedDataTests.cs
Write-Host "Fixing SeedDataTests.cs..."
$file = Join-Path $testDir "SeedDataTests.cs"
$content = Get-Content $file -Raw
# Fix string comparisons with Guid
$content = $content -replace '([a-z]+\.Id) == "([^"]+)"', '$1.ToString() == "$2"'
Set-Content $file -Value $content -NoNewline

# File 5: TempSessionServiceTests.cs (additional fixes)
Write-Host "Fixing TempSessionServiceTests.cs (additional)..."
$file = Join-Path $testDir "TempSessionServiceTests.cs"
$content = Get-Content $file -Raw
# Fix all remaining Id = Guid.NewGuid().ToString()
$content = $content -replace 'Id = Guid\.NewGuid\(\)\.ToString\(\),', 'Id = Guid.NewGuid(),'
Set-Content $file -Value $content -NoNewline

# File 6: QualityReportServiceTests.cs
Write-Host "Fixing QualityReportServiceTests.cs..."
$file = Join-Path $testDir "Services\QualityReportServiceTests.cs"
$content = Get-Content $file -Raw
# Fix Id and GameId in AiRequestLogEntity
$content = $content -replace 'Id = Guid\.NewGuid\(\)\.ToString\(\),', 'Id = Guid.NewGuid(),'
$content = $content -replace 'GameId = Guid\.NewGuid\(\)\.ToString\(\),', 'GameId = Guid.NewGuid(),'
Set-Content $file -Value $content -NoNewline

# File 7: SetupGuideServiceComprehensiveTests.cs (additional)
Write-Host "Fixing SetupGuideServiceComprehensiveTests.cs (additional)..."
$file = Join-Path $testDir "SetupGuideServiceComprehensiveTests.cs"
$content = Get-Content $file -Raw
# Fix cacheService calls that still need gameId
$content = $content -replace '\.InvalidateGameAsync\(gameId\)', '.InvalidateGameAsync(gameId.ToString())'
Set-Content $file -Value $content -NoNewline

# File 8: PromptEvaluationServiceTests.cs (line 721)
Write-Host "Fixing PromptEvaluationServiceTests.cs..."
$file = Join-Path $testDir "Services\PromptEvaluationServiceTests.cs"
$content = Get-Content $file -Raw
# Fix Guid comparison
$content = $content -replace 'e\.Id == evalResult\.EvaluationId\.ToString\(\)', 'e.Id.ToString() == evalResult.EvaluationId.ToString()'
Set-Content $file -Value $content -NoNewline

Write-Host "`nAll files fixed. Building..."
cd "D:\Repositories\meepleai-monorepo\apps\api"
$result = dotnet build tests/Api.Tests/Api.Tests.csproj 2>&1
$errors = $result | Select-String "error CS"
Write-Host "$($errors.Count) errors remaining" -ForegroundColor $(if ($errors.Count -eq 0) { "Green" } else { "Yellow" })

if ($errors.Count -gt 0 -and $errors.Count -lt 40) {
    $errors | ForEach-Object { Write-Host $_ -ForegroundColor Red }
}
