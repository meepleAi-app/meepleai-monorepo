#!/bin/bash
# ollama-init.sh - Idempotently pull required models after Ollama is ready
# Usage: docker exec meepleai-ollama bash /scripts/ollama-init.sh
#   or:  Run from host after 'docker compose -f compose.dev.yml up -d'
#
# Override the model list with OLLAMA_MODELS env var (space-separated):
#   OLLAMA_MODELS="qwen2.5:1.5b mxbai-embed-large llama3:8b" bash ollama-init.sh
#
# Post-PR #267 fix: idempotent (skips models already pulled) so it can be safely
# re-run as part of dev setup or troubleshooting.

set -e

OLLAMA_HOST="${OLLAMA_HOST:-http://localhost:11434}"
OLLAMA_MODELS="${OLLAMA_MODELS:-qwen2.5:1.5b mxbai-embed-large}"

echo "Waiting for Ollama to be ready at ${OLLAMA_HOST}..."
until curl -sf "${OLLAMA_HOST}/api/tags" > /dev/null 2>&1; do
  echo "  Ollama not ready yet, retrying in 2s..."
  sleep 2
done

echo "Ollama is ready. Ensuring required models are pulled..."

for MODEL in $OLLAMA_MODELS; do
  if ollama list 2>/dev/null | tail -n +2 | awk '{print $1}' | grep -qx "$MODEL"; then
    echo "  ✓ ${MODEL} already pulled (skipping)"
  else
    echo "  ==> Pulling ${MODEL}..."
    ollama pull "$MODEL"
  fi
done

echo ""
echo "All required models ready."
ollama list
