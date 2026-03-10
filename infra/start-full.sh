#!/usr/bin/env bash
# Start MeepleAI with full profile (all services)
# Profile: everything including HyperDX observability

set -e

echo "🚀 Starting MeepleAI with full profile (all services)..."
echo "   Services: All services including HyperDX"
echo ""

cd "$(dirname "$0")"

# Start with full profile and include HyperDX
docker compose -f docker-compose.yml -f compose.hyperdx.yml --profile full up -d

echo ""
echo "✅ Full stack started successfully!"
echo ""
echo "🌐 Core Services:"
echo "   API: http://localhost:8080"
echo "   Web: http://localhost:3000"
echo ""
echo "🤖 AI/ML Services:"
echo "   Ollama: http://localhost:11434"
echo "   Embedding: http://localhost:8000"
echo "   Unstructured: http://localhost:8001"
echo "   SmolDocling: http://localhost:8002"
echo "   Reranker: http://localhost:8003"
echo ""
echo "📊 Observability:"
echo "   Prometheus: http://localhost:9090"
echo "   Grafana: http://localhost:3001"
echo "   Alertmanager: http://localhost:9093"
echo "   HyperDX: http://localhost:8180"
echo ""
echo "⚙️  Automation:"
echo "   n8n: http://localhost:5678"
echo ""
echo "⚠️  Note: Full stack may take 2-3 minutes to fully initialize"
echo ""
echo "💡 To view logs: docker compose logs -f"
echo "💡 To stop: docker compose -f docker-compose.yml -f compose.hyperdx.yml down"
