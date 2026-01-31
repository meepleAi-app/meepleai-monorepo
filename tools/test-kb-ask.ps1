# Test /knowledge-base/ask endpoint after GIN index fix
$loginBody = @{
    email = "admin@meepleai.dev"
    password = "pVKOMQNK0tFNgGlX"
} | ConvertTo-Json

$null = Invoke-WebRequest -Uri "http://localhost:8080/api/v1/auth/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body $loginBody `
    -SessionVariable session

$askBody = @{
    gameId = "30706e12-4c77-4a52-9118-8d48c94f6d9c"
    query = "How many players?"
} | ConvertTo-Json

Write-Host "[TEST] POST /api/v1/knowledge-base/ask"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

try {
    $response = Invoke-RestMethod `
        -Uri "http://localhost:8080/api/v1/knowledge-base/ask" `
        -Method POST `
        -ContentType "application/json" `
        -Body $askBody `
        -WebSession $session `
        -TimeoutSec 30

    $sw.Stop()

    Write-Host "[SUCCESS] Response in $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
    Write-Host "Answer: $($response.answer)"
    Write-Host "Confidence: $($response.overallConfidence)"
    Write-Host "Sources: $($response.sources.Count)"
    Write-Host "Low Quality: $($response.isLowQuality)"

} catch {
    $sw.Stop()
    Write-Host "[FAIL] After $($sw.ElapsedMilliseconds)ms" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)"
}
