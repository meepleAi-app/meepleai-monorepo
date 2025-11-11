# Rebuild and restart API container with JSON configuration fix
Write-Host "Stopping API container..." -ForegroundColor Yellow
docker stop infra-api-1

Write-Host "Rebuilding API image..." -ForegroundColor Yellow
Set-Location -Path "D:\Repositories\meepleai-monorepo\infra"
docker compose build api

Write-Host "Starting API container..." -ForegroundColor Yellow
docker compose up api -d

Write-Host "Waiting for API to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

Write-Host "Testing API health..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "http://localhost:8080/health" -Method Get -ErrorAction SilentlyContinue
if ($response.StatusCode -eq 200) {
    Write-Host "✅ API is healthy!" -ForegroundColor Green
} else {
    Write-Host "⚠️ API health check failed" -ForegroundColor Red
}

Write-Host "Testing login endpoint..." -ForegroundColor Yellow
$loginBody = @{
    email = "editor@meepleai.dev"
    password = "Demo123!"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-WebRequest -Uri "http://localhost:8080/api/v1/auth/login" -Method Post -Body $loginBody -ContentType "application/json" -ErrorAction Stop
    Write-Host "✅ Login API works! Status: $($loginResponse.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "❌ Login API failed: $($_.Exception.Message)" -ForegroundColor Red
}
