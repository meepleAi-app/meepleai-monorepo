#!/usr/bin/env bash
# Start MeepleAI with minimal profile (core services only)
# Profile: postgres, redis, qdrant, api, web

set -e

echo "🚀 Starting MeepleAI with minimal profile (core services only)..."
echo "   Services: postgres, redis, qdrant, api, web"
echo ""

cd "$(dirname "$0")"

docker compose --profile minimal up -d

echo ""
echo "✅ Minimal stack started successfully!"
echo "   API: http://localhost:8080"
echo "   Web: http://localhost:3000"
echo ""
echo "💡 To view logs: docker compose logs -f"
echo "💡 To stop: docker compose down"
