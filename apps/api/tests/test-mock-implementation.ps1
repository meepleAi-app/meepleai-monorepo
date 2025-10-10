# Test script to build and run specific tests for mock implementation verification

Write-Host "Building API test project..." -ForegroundColor Cyan
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nRunning PDF indexing test (SearchIndexedPdf_FilteredByGame_ReturnsOnlyGameResults)..." -ForegroundColor Cyan
dotnet test --filter "FullyQualifiedName~SearchIndexedPdf_FilteredByGame_ReturnsOnlyGameResults" --logger "console;verbosity=detailed"

Write-Host "`nRunning ExplainEndpoint tests..." -ForegroundColor Cyan
dotnet test --filter "FullyQualifiedName~ExplainEndpointTests" --logger "console;verbosity=normal"

Write-Host "`nTest run complete!" -ForegroundColor Green
