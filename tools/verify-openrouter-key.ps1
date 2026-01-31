# Verify OpenRouter API key validity
$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  OpenRouter API Key Verification      ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Read key from .env.development
$envContent = Get-Content '.env.development' -Raw
if ($envContent -match 'OPENROUTER_API_KEY=(.+)') {
    $key = $matches[1].Trim()
    Write-Host "[OK] Key found in .env.development" -ForegroundColor Green
    Write-Host "    Prefix: $($key.Substring(0, 20))..." -ForegroundColor Gray
    Write-Host "    Length: $($key.Length) chars" -ForegroundColor Gray
} else {
    Write-Host "[ERROR] OPENROUTER_API_KEY not found in .env.development" -ForegroundColor Red
    exit 1
}

# Test key with OpenRouter API
Write-Host ""
Write-Host "[INFO] Testing key with OpenRouter API..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod `
        -Uri 'https://openrouter.ai/api/v1/auth/key' `
        -Headers @{ 'Authorization' = "Bearer $key" } `
        -TimeoutSec 10

    Write-Host "[OK] ✅ Key is VALID!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Key Info:" -ForegroundColor Cyan
    Write-Host "  Label: $($response.data.label)" -ForegroundColor White
    Write-Host "  Limit: `$$($response.data.limit)" -ForegroundColor White
    Write-Host "  Usage: `$$($response.data.usage)" -ForegroundColor White
    Write-Host "  Remaining: `$$($response.data.limit - $response.data.usage)" -ForegroundColor Green

} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__

    Write-Host "[ERROR] ❌ Key is INVALID!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Status Code: $statusCode" -ForegroundColor Red

    if ($statusCode -eq 401) {
        Write-Host "Error: Unauthorized - User not found" -ForegroundColor Red
        Write-Host ""
        Write-Host "Possible causes:" -ForegroundColor Yellow
        Write-Host "  1. API key revoked or expired" -ForegroundColor Gray
        Write-Host "  2. API key format invalid" -ForegroundColor Gray
        Write-Host "  3. OpenRouter account suspended" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Solution:" -ForegroundColor Cyan
        Write-Host "  1. Go to: https://openrouter.ai/keys" -ForegroundColor White
        Write-Host "  2. Generate new API key" -ForegroundColor White
        Write-Host "  3. Update .env.development: OPENROUTER_API_KEY=sk-or-v1-..." -ForegroundColor White
        Write-Host "  4. Restart API: cd apps/api/src/Api && dotnet run" -ForegroundColor White
    } else {
        Write-Host "Error Details: $($_.Exception.Message)" -ForegroundColor Red
    }

    exit 1
}
