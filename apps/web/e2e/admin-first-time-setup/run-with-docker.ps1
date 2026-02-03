# E2E Admin Setup Tests - Docker Mode
# Run from: apps/web directory

param(
    [switch]$UIMode,
    [string]$TestFile = ""
)

Write-Host "`n=== E2E Admin Setup Tests - Docker Mode ===`n"

# Get repository root (2 levels up from apps/web)
$repoRoot = Split-Path -Parent (Split-Path -Parent (Get-Location))
$infraPath = Join-Path $repoRoot "infra"

# Step 1: Rebuild API container
Write-Host "Step 1: Rebuilding API container..."
Push-Location $infraPath
docker compose build api
$buildResult = $LASTEXITCODE
Pop-Location

if ($buildResult -ne 0) {
    Write-Host "ERROR: API build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "SUCCESS: API container rebuilt`n"

# Step 2: Restart API service
Write-Host "Step 2: Restarting API service..."
Push-Location $infraPath
docker compose restart api
Pop-Location

# Wait for API health
Write-Host "Waiting for API health check..."
for ($i = 0; $i -lt 30; $i++) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/health" -TimeoutSec 2 -ErrorAction Stop
        Write-Host "SUCCESS: API is healthy`n"
        break
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

# Step 3: Generate test PDFs if missing
if (-not (Test-Path "e2e/test-data/pandemic_rulebook.pdf")) {
    Write-Host "Generating mock PDFs..."
    node e2e/scripts/generate-mock-pdfs.js
}

# Step 4: Run tests
Write-Host "Step 3: Running E2E tests against Docker services...`n"

$env:PLAYWRIGHT_SKIP_WEB_SERVER = "1"

$testArgs = @("test")
if ($TestFile) {
    $testArgs += "e2e/admin-first-time-setup/$TestFile.spec.ts"
} else {
    $testArgs += "e2e/admin-first-time-setup"
}
$testArgs += "--project=admin-first-time-setup"

if ($UIMode) {
    $testArgs += "--ui"
} else {
    $testArgs += "--reporter=list"
}

pnpm exec playwright @testArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nSUCCESS: All tests passed!" -ForegroundColor Green
} else {
    Write-Host "`nFAILURE: Some tests failed (code: $LASTEXITCODE)" -ForegroundColor Red
}

exit $LASTEXITCODE
