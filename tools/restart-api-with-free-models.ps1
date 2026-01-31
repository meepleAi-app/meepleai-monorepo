# Restart API with free OpenRouter models configured
$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   Restart API with Free Models         ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Kill existing processes
Write-Host "[1/3] Stopping existing API processes..." -ForegroundColor Yellow
Get-Process -Name Api,dotnet -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -eq 'Api' -or $_.MainWindowTitle -like '*Api*'
} | Stop-Process -Force -ErrorAction SilentlyContinue

Start-Sleep -Seconds 2
Write-Host "      ✅ Processes stopped" -ForegroundColor Green

# Build
Write-Host "[2/3] Building API with free models..." -ForegroundColor Yellow
Write-Host "      Models: Llama 3.3 70B + Gemini 2.0 Flash" -ForegroundColor Gray

cd apps/api/src/Api
$buildResult = dotnet build --no-restore 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "      ❌ Build failed!" -ForegroundColor Red
    $buildResult | Select-String "error" | Select-Object -First 5
    exit 1
}

Write-Host "      ✅ Build successful" -ForegroundColor Green

# Start API
Write-Host "[3/3] Starting API..." -ForegroundColor Yellow

Start-Process pwsh -ArgumentList '-NoExit', '-Command', 'dotnet run' -WorkingDirectory (Get-Location)

Write-Host "      ✅ API starting in new window" -ForegroundColor Green
Write-Host ""
Write-Host "[INFO] Waiting for API to be ready (15 seconds)..." -ForegroundColor Cyan

Start-Sleep -Seconds 15

# Verify API
try {
    $health = Invoke-RestMethod -Uri 'http://localhost:8080/health/ready' -TimeoutSec 5
    Write-Host "[OK] ✅ API is ready!" -ForegroundColor Green
    Write-Host ""
    Write-Host "[NEXT] Run validation: pwsh ./tools/run-rag-validation-20q.ps1" -ForegroundColor Yellow
} catch {
    Write-Host "[WARN] ⚠️  API not responding yet" -ForegroundColor Yellow
    Write-Host "       Wait a bit longer and check the API window" -ForegroundColor Gray
}
