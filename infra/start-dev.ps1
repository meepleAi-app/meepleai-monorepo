# Start MeepleAI with dev profile (development + basic observability)
# Profile: minimal + prometheus, grafana

$ErrorActionPreference = 'Stop'

Write-Host "🚀 Starting MeepleAI with dev profile (development + basic observability)..." -ForegroundColor Blue
Write-Host "   Services: minimal + prometheus, grafana"
Write-Host ""

Set-Location $PSScriptRoot

docker compose --profile dev up -d

Write-Host ""
Write-Host "✅ Dev stack started successfully!" -ForegroundColor Green
Write-Host "   API: http://localhost:8080"
Write-Host "   Web: http://localhost:3000"
Write-Host "   Prometheus: http://localhost:9090"
Write-Host "   Grafana: http://localhost:3001"
Write-Host ""
Write-Host "💡 To view logs: docker compose logs -f"
Write-Host "💡 To stop: docker compose down"
