# Direct indexing without separate login
$BaseUrl = "http://localhost:8080"

Write-Host "Indexing Chess Knowledge..." -ForegroundColor Cyan

# Login and get session
$loginBody = @{
    email = "admin@meepleai.dev"
    password = "Demo123!"
} | ConvertTo-Json

$session = $null
try {
    $loginResponse = Invoke-WebRequest -Uri "$BaseUrl/auth/login" `
        -Method Post `
        -Body $loginBody `
        -ContentType "application/json" `
        -SessionVariable session `
        -UseBasicParsing

    Write-Host "[OK] Logged in" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] Login failed: $_" -ForegroundColor Red
    exit 1
}

# Index immediately after login using the same session
try {
    $indexResponse = Invoke-WebRequest -Uri "$BaseUrl/chess/index" `
        -Method Post `
        -WebSession $session `
        -ContentType "application/json" `
        -UseBasicParsing `
        -TimeoutSec 180

    $indexData = $indexResponse.Content | ConvertFrom-Json
    Write-Host "[OK] Indexed: $($indexData.totalItems) items, $($indexData.totalChunks) chunks" -ForegroundColor Green

} catch {
    Write-Host "[FAIL] Indexing failed: $_" -ForegroundColor Red
    Write-Host "Response: $($_.Exception.Response)" -ForegroundColor Gray
    exit 1
}
