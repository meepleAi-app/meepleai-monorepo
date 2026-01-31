#!/usr/bin/env bash
# Start MeepleAI with observability profile (full monitoring stack)
# Profile: minimal + prometheus, grafana, alertmanager, hyperdx

set -e

echo "🚀 Starting MeepleAI with observability profile (full monitoring stack)..."
echo "   Services: minimal + prometheus, grafana, alertmanager, hyperdx"
echo ""

cd "$(dirname "$0")"

# Start with observability profile and include HyperDX
docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml --profile observability up -d

echo ""
echo "✅ Observability stack started successfully!"
echo "   API: http://localhost:8080"
echo "   Web: http://localhost:3000"
echo "   Prometheus: http://localhost:9090"
echo "   Grafana: http://localhost:3001"
echo "   Alertmanager: http://localhost:9093"
echo "   HyperDX: http://localhost:8180"
echo ""
echo "💡 To view logs: docker compose logs -f"
echo "💡 To stop: docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml down"
