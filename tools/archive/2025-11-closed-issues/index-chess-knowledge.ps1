# Index chess knowledge base (CHESS-03)

$BaseUrl = "http://localhost:8080"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Indexing Chess Knowledge Base (CHESS-03)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Login as admin
Write-Host "[1/2] Logging in as admin..." -ForegroundColor Yellow
try {
    $loginBody = @{
        email = "admin@meepleai.dev"
        password = "Demo123!"
    } | ConvertTo-Json

    $loginResponse = Invoke-WebRequest -Uri "$BaseUrl/auth/login" `
        -Method Post `
        -Body $loginBody `
        -ContentType "application/json" `
        -SessionVariable session

    Write-Host "[OK] Admin login successful" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] Admin login failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 2: Index chess knowledge
Write-Host "[2/2] Indexing chess knowledge..." -ForegroundColor Yellow
Write-Host "  (This may take 30-60 seconds)" -ForegroundColor Gray
try {
    $indexResponse = Invoke-WebRequest -Uri "$BaseUrl/chess/index" `
        -Method Post `
        -ContentType "application/json" `
        -WebSession $session

    Write-Host "[OK] Chess knowledge indexed successfully" -ForegroundColor Green

    $indexData = $indexResponse.Content | ConvertFrom-Json
    Write-Host "  Total items: $($indexData.totalItems)" -ForegroundColor Gray
    Write-Host "  Total chunks: $($indexData.totalChunks)" -ForegroundColor Gray

    if ($indexData.categoryCounts) {
        Write-Host "  Categories:" -ForegroundColor Gray
        $indexData.categoryCounts.PSObject.Properties | ForEach-Object {
            Write-Host "    - $($_.Name): $($_.Value)" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "[FAIL] Chess knowledge indexing failed" -ForegroundColor Red
    Write-Host "  Status: $($_.Exception.Response.StatusCode.value__) $($_.Exception.Response.StatusCode)" -ForegroundColor Gray
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray

    # Try to read error body
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host "  Response: $errorBody" -ForegroundColor Gray
    }
    exit 1
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Chess knowledge ready!" -ForegroundColor Green
Write-Host "Now run: .\tools\test-chess-endpoint.ps1" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
