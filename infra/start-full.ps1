# Start MeepleAI with full profile (all services)
# Profile: everything including HyperDX observability

$ErrorActionPreference = 'Stop'

Write-Host "🚀 Starting MeepleAI with full profile (all services)..." -ForegroundColor Blue
Write-Host "   Services: All services including HyperDX"
Write-Host ""

Set-Location $PSScriptRoot

# Start with full profile and include HyperDX
docker compose -f docker-compose.yml -f compose.hyperdx.yml --profile full up -d

Write-Host ""
Write-Host "✅ Full stack started successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "🌐 Core Services:"
Write-Host "   API: http://localhost:8080"
Write-Host "   Web: http://localhost:3000"
Write-Host ""
Write-Host "🤖 AI/ML Services:"
Write-Host "   Ollama: http://localhost:11434"
Write-Host "   Embedding: http://localhost:8000"
Write-Host "   Unstructured: http://localhost:8001"
Write-Host "   SmolDocling: http://localhost:8002"
Write-Host "   Reranker: http://localhost:8003"
Write-Host ""
Write-Host "📊 Observability:"
Write-Host "   Prometheus: http://localhost:9090"
Write-Host "   Grafana: http://localhost:3001"
Write-Host "   Alertmanager: http://localhost:9093"
Write-Host "   HyperDX: http://localhost:8180"
Write-Host ""
Write-Host "⚙️  Automation:"
Write-Host "   n8n: http://localhost:5678"
Write-Host ""
Write-Host "⚠️  Note: Full stack may take 2-3 minutes to fully initialize" -ForegroundColor Yellow
Write-Host ""
Write-Host "💡 To view logs: docker compose logs -f"
Write-Host "💡 To stop: docker compose -f docker-compose.yml -f compose.hyperdx.yml down"
