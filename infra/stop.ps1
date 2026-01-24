# Stop MeepleAI services

$ErrorActionPreference = 'Stop'

Write-Host "🛑 Stopping MeepleAI services..." -ForegroundColor Yellow

Set-Location $PSScriptRoot

docker compose down

Write-Host ""
Write-Host "✅ All services stopped" -ForegroundColor Green
Write-Host ""
Write-Host "💡 To start again: .\start-minimal.ps1, .\start-dev.ps1, or .\start-full.ps1"
