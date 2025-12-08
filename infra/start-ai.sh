#!/usr/bin/env bash
# Start MeepleAI with ai profile (AI/ML services)
# Profile: ollama, embedding-service, unstructured-service, smoldocling-service, reranker-service

set -e

echo "🚀 Starting MeepleAI with ai profile (AI/ML services)..."
echo "   Services: ollama, embedding-service, unstructured-service, smoldocling-service, reranker-service"
echo ""

cd "$(dirname "$0")"

docker compose --profile ai up -d

echo ""
echo "✅ AI stack started successfully!"
echo "   Ollama: http://localhost:11434"
echo "   Embedding Service: http://localhost:8000"
echo "   Unstructured Service: http://localhost:8001"
echo "   SmolDocling Service: http://localhost:8002"
echo "   Reranker Service: http://localhost:8003"
echo ""
echo "⚠️  Note: AI services may take 1-2 minutes to download models on first start"
echo ""
echo "💡 To view logs: docker compose logs -f"
echo "💡 To stop: docker compose down"
