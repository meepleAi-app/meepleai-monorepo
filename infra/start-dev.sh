#!/usr/bin/env bash
# Start MeepleAI with dev profile (development + basic observability)
# Profile: minimal + prometheus, grafana

set -e

echo "🚀 Starting MeepleAI with dev profile (development + basic observability)..."
echo "   Services: minimal + prometheus, grafana"
echo ""

cd "$(dirname "$0")"

docker compose --profile dev up -d

echo ""
echo "✅ Dev stack started successfully!"
echo "   API: http://localhost:8080"
echo "   Web: http://localhost:3000"
echo "   Prometheus: http://localhost:9090"
echo "   Grafana: http://localhost:3001"
echo ""
echo "💡 To view logs: docker compose logs -f"
echo "💡 To stop: docker compose down"
