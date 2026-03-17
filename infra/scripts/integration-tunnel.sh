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
  "$STAGING_HOST"

echo "✅ Tunnels established:"
echo ""
echo "  PostgreSQL   localhost:15432 → staging:5432"
echo "  Redis        localhost:16379 → staging:6379"
echo "  Qdrant       localhost:16333 → staging:6333"
echo "  Embedding    localhost:18000 → staging:8000"
echo "  Reranker     localhost:18003 → staging:8003"
echo "  Unstructured localhost:18001 → staging:8001"
echo "  SmolDocling  localhost:18002 → staging:8002"
echo "  Ollama       localhost:21434 → staging:11434"
echo ""
echo "🚀 Now run:"
echo "  Terminal 1: cd apps/api/src/Api && dotnet run --launch-profile Integration"
echo "  Terminal 2: cd apps/web && pnpm dev"
echo ""
echo "🛑 To close tunnels: pkill -f 'ssh.*15432.*204.168'"
