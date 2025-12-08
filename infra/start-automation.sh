#!/usr/bin/env bash
# Start MeepleAI with automation profile (workflow automation)
# Profile: n8n

set -e

echo "🚀 Starting MeepleAI with automation profile (workflow automation)..."
echo "   Services: n8n"
echo ""

cd "$(dirname "$0")"

docker compose --profile automation up -d

echo ""
echo "✅ Automation stack started successfully!"
echo "   n8n: http://localhost:5678"
echo ""
echo "💡 To view logs: docker compose logs -f"
echo "💡 To stop: docker compose down"
