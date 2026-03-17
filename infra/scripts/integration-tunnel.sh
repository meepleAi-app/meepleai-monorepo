#!/usr/bin/env bash
# Integration Tunnel — connects local dev to staging services via SSH
#
# Usage: bash infra/scripts/integration-tunnel.sh
#
# Opens SSH tunnels to staging server (meepleai.app) for:
#   - PostgreSQL  :15432 → staging:5432
#   - Redis       :16379 → staging:6379
#   - Qdrant      :16333 → staging:6333
#   - Embedding   :18000 → staging:8000
#   - Reranker    :18003 → staging:8003
#   - Unstructured:18001 → staging:8001
#   - SmolDocling :18002 → staging:8002
#   - Ollama      :21434 → staging:11434
#   - n8n         :15678 → staging:5678
#   - Grafana     :13001 → staging:3001
#   - Prometheus  :19090 → staging:9090
#   - Orchestrator:18004 → staging:8004
#
# Then run API with: cd apps/api/src/Api && dotnet run --launch-profile Integration
# And frontend with: cd apps/web && pnpm dev

set -e

SSH_KEY="${HOME}/.ssh/meepleai-staging"
STAGING_HOST="deploy@204.168.135.69"

echo "🔌 Opening SSH tunnels to staging..."
echo ""

ssh -i "$SSH_KEY" -f -N \
  -L 15432:localhost:5432 \
  -L 16379:localhost:6379 \
  -L 16333:localhost:6333 \
  -L 18000:localhost:8000 \
  -L 18001:localhost:8001 \
  -L 18002:localhost:8002 \
  -L 18003:localhost:8003 \
  -L 21434:localhost:11434 \
  -L 15678:localhost:5678 \
  -L 13001:localhost:3001 \
  -L 19090:localhost:9090 \
  -L 18004:localhost:8004 \
  "$STAGING_HOST"

echo "✅ Tunnels established:"
echo ""
echo "  ── Core ──────────────────────────────────────"
echo "  PostgreSQL    localhost:15432 → staging:5432"
echo "  Redis         localhost:16379 → staging:6379"
echo "  Qdrant        localhost:16333 → staging:6333"
echo ""
echo "  ── AI Services ─────────────────────────────"
echo "  Embedding     localhost:18000 → staging:8000"
echo "  Reranker      localhost:18003 → staging:8003"
echo "  Unstructured  localhost:18001 → staging:8001"
echo "  SmolDocling   localhost:18002 → staging:8002"
echo "  Ollama        localhost:21434 → staging:11434"
echo "  Orchestrator  localhost:18004 → staging:8004"
echo ""
echo "  ── Monitoring & Automation ─────────────────"
echo "  n8n           localhost:15678 → staging:5678"
echo "  Grafana       localhost:13001 → staging:3001"
echo "  Prometheus    localhost:19090 → staging:9090"
echo ""
echo "🚀 Now run:"
echo "  Terminal 1: pwsh -Command \"cd apps/api/src/Api; dotnet run --launch-profile Integration\""
echo "  Terminal 2: cd apps/web && pnpm dev"
echo ""
echo "🛑 To close tunnels: pkill -f 'ssh.*15432.*204.168'"
