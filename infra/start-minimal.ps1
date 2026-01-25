# Start MeepleAI with minimal profile (core services only)
# Profile: postgres, redis, qdrant, api, web

$ErrorActionPreference = 'Stop'

Write-Host "🚀 Starting MeepleAI with minimal profile (core services only)..." -ForegroundColor Blue
Write-Host "   Services: postgres, redis, qdrant, api, web"
Write-Host ""

Set-Location $PSScriptRoot

docker compose --profile minimal up -d

Write-Host ""
Write-Host "✅ Minimal stack started successfully!" -ForegroundColor Green
Write-Host "   API: http://localhost:8080"
Write-Host "   Web: http://localhost:3000"
Write-Host ""
Write-Host "💡 To view logs: docker compose logs -f"
Write-Host "💡 To stop: docker compose down"
