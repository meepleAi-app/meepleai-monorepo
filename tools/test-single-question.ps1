# Test single question to debug
$ApiBaseUrl = "http://localhost:8080"
$GameId = "30706e12-4c77-4a52-9118-8d48c94f6d9c"

# Login
$loginBody = @{ email = "admin@meepleai.dev"; password = "pVKOMQNK0tFNgGlX" } | ConvertTo-Json
$login = Invoke-WebRequest -Uri "$ApiBaseUrl/api/v1/auth/login" -Method Post -ContentType "application/json" -Body $loginBody -UseBasicParsing
$cookie = ($login.Headers['Set-Cookie'] | Where-Object { $_ -match 'meepleai_session=' }) -replace ';.*',''

# Test Q001
$body = @{
    query = "How do pawns move in chess?"
    gameId = $GameId
    language = "en"
    bypassCache = $true
} | ConvertTo-Json

$resp = Invoke-RestMethod -Uri "$ApiBaseUrl/api/v1/knowledge-base/ask" -Method Post -Headers @{ "Cookie" = $cookie; "Content-Type" = "application/json" } -Body $body

Write-Host "═════ RAG RESPONSE ═════" -ForegroundColor Cyan
Write-Host "Answer: $($resp.answer)" -ForegroundColor White
Write-Host ""
Write-Host "Overall Confidence: $($resp.overallConfidence)" -ForegroundColor Yellow
Write-Host "Sources Count: $($resp.sources.Count)" -ForegroundColor Yellow
Write-Host "Is Low Quality: $($resp.isLowQuality)" -ForegroundColor Yellow
Write-Host ""

if ($resp.sources -and $resp.sources.Count -gt 0) {
    Write-Host "First Source:" -ForegroundColor Cyan
    Write-Host "  Page: $($resp.sources[0].pageNumber)"
    Write-Host "  Score: $($resp.sources[0].relevanceScore)"
    Write-Host "  Text: $($resp.sources[0].textContent.Substring(0, [Math]::Min(200, $resp.sources[0].textContent.Length)))..."
}

# Test keyword matching
$expectedKeywords = @("forward", "one square", "capture", "diagonally")
Write-Host ""
Write-Host "Keyword Matching:" -ForegroundColor Cyan
foreach ($kw in $expectedKeywords) {
    $found = $resp.answer -match [regex]::Escape($kw)
    Write-Host "  '$kw': $(if ($found) { '✅' } else { '❌' })" -ForegroundColor $(if ($found) { 'Green' } else { 'Red' })
}
