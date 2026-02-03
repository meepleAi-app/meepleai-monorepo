# Quick test for /agents/qa endpoint
$ErrorActionPreference = "Stop"

$ApiBaseUrl = "http://localhost:8080"

# Login
Write-Host "[INFO] Logging in..." -ForegroundColor Cyan
$loginBody = @{
    email = "admin@meepleai.dev"
    password = "pVKOMQNK0tFNgGlX"
} | ConvertTo-Json

$loginResponse = Invoke-WebRequest -Uri "$ApiBaseUrl/api/v1/auth/login" -Method Post -ContentType "application/json" -Body $loginBody -UseBasicParsing
$cookie = ($loginResponse.Headers['Set-Cookie'] | Where-Object { $_ -match 'meepleai_session=' }) -replace ';.*',''

Write-Host "[OK] Cookie: $cookie" -ForegroundColor Green

# Test QA endpoint
Write-Host "[INFO] Testing /agents/qa endpoint..." -ForegroundColor Cyan

$testBody = @{
    query = "How do pawns move in chess?"
    gameId = "30706e12-4c77-4a52-9118-8d48c94f6d9c"
    searchMode = "Hybrid"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/v1/agents/qa" `
        -Method Post `
        -Headers @{
            "Cookie" = $cookie
            "Content-Type" = "application/json"
        } `
        -Body $testBody `
        -TimeoutSec 30 `
        -Verbose

    Write-Host "[OK] Response received!" -ForegroundColor Green
    Write-Host "Answer: $($response.answer.Substring(0, [Math]::Min(200, $response.answer.Length)))..." -ForegroundColor White
    Write-Host "Snippets: $($response.snippets.Count)" -ForegroundColor Gray
    Write-Host "Confidence: $($response.confidence)" -ForegroundColor Gray

} catch {
    Write-Host "[ERROR] Request failed" -ForegroundColor Red
    Write-Host "Message: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Response: $($_.Exception.Response)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}
