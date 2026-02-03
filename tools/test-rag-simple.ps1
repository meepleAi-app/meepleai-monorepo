# Minimal RAG test
$ApiBaseUrl = "http://localhost:8080"
$GameId = "30706e12-4c77-4a52-9118-8d48c94f6d9c"

# Login
$loginBody = '{"email":"admin@meepleai.dev","password":"pVKOMQNK0tFNgGlX"}'
$login = Invoke-WebRequest -Uri "$ApiBaseUrl/api/v1/auth/login" -Method Post -ContentType "application/json" -Body $loginBody -UseBasicParsing
$cookie = ($login.Headers['Set-Cookie'] | Where-Object { $_ -match 'meepleai_session=' }) -replace ';.*',''

Write-Host "Cookie: $cookie" -ForegroundColor Green

# Simple test
$testBody = '{"query":"test","gameId":"' + $GameId + '","language":"en","bypassCache":true}'

Write-Host "Testing /knowledge-base/ask..." -ForegroundColor Cyan
Write-Host "Body: $testBody" -ForegroundColor Gray

try {
    $response = Invoke-WebRequest `
        -Uri "$ApiBaseUrl/api/v1/knowledge-base/ask" `
        -Method Post `
        -Headers @{
            "Cookie" = $cookie
            "Content-Type" = "application/json"
        } `
        -Body $testBody `
        -TimeoutSec 60 `
        -Verbose `
        -ErrorAction Stop

    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Content: $($response.Content)" -ForegroundColor White

} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Inner: $($_.Exception.InnerException.Message)" -ForegroundColor Yellow

    # Get detailed error
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody" -ForegroundColor Red
    }
}
