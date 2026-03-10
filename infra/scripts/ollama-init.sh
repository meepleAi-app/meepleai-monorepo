#!/bin/bash
# ollama-init.sh - Pull required models after Ollama is ready
# Usage: docker exec meepleai-ollama bash /scripts/ollama-init.sh
#   or:  Run from host after 'docker compose -f compose.mvp.yml up -d'

set -e

OLLAMA_HOST="${OLLAMA_HOST:-http://localhost:11434}"

echo "Waiting for Ollama to be ready at ${OLLAMA_HOST}..."
until curl -sf "${OLLAMA_HOST}/api/tags" > /dev/null 2>&1; do
  echo "  Ollama not ready yet, retrying in 2s..."
  sleep 2
done

echo "Ollama is ready. Pulling required models..."

echo ""
echo "==> Pulling qwen2.5:1.5b (LLM for FAST/BALANCED queries)..."
ollama pull qwen2.5:1.5b

echo ""
echo "==> Pulling mxbai-embed-large (embedding model, 1024 dimensions)..."
ollama pull mxbai-embed-large

echo ""
echo "All models pulled successfully."
ollama list
