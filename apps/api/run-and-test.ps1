# Script completo: avvia server e testa rate limiting

Write-Host "`n=== Avvio Server API ===" -ForegroundColor Cyan

# Avvia il server in background
$apiPath = "D:\Repositories\meepleai-monorepo\apps\api\src\Api"
$serverJob = Start-Job -ScriptBlock {
    param($path)
    Set-Location $path
    dotnet run
} -ArgumentList $apiPath

Write-Host "Server avviato in background (Job ID: $($serverJob.Id))"
Write-Host "Attendo 10 secondi per il startup..."
Start-Sleep -Seconds 10

# Verifica che il server sia attivo
$maxRetries = 5
$retryCount = 0
$serverReady = $false

while ($retryCount -lt $maxRetries -and -not $serverReady) {
    try {
        $healthCheck = Invoke-WebRequest -Uri "http://localhost:8080/" -TimeoutSec 2 -ErrorAction Stop
        $serverReady = $true
        Write-Host "[OK] Server pronto!" -ForegroundColor Green
    } catch {
        $retryCount++
        Write-Host "Retry $retryCount/$maxRetries..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }
}

if (-not $serverReady) {
    Write-Host "[ERROR] Server non risponde dopo $maxRetries tentativi" -ForegroundColor Red
    Stop-Job -Job $serverJob
    Remove-Job -Job $serverJob
    exit 1
}

# Esegui i test
Write-Host "`n=== Esecuzione Test Rate Limiting ===" -ForegroundColor Cyan
& "D:\Repositories\meepleai-monorepo\apps\api\test-rate-limit.ps1"

# Cleanup: ferma il server
Write-Host "`n=== Pulizia ===" -ForegroundColor Cyan
Write-Host "Arresto server..."
Stop-Job -Job $serverJob
Remove-Job -Job $serverJob
Write-Host "Server arrestato" -ForegroundColor Green

Write-Host "`n=== Fine ===" -ForegroundColor Cyan
