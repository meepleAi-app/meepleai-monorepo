#!/usr/bin/env bash
# Integration Tunnel — connects local dev to staging services via SSH
#
# Usage:
#   bash infra/scripts/integration-tunnel.sh [start|stop|status]
#
# Opens SSH tunnels to staging server (meepleai.app) for:
#   - PostgreSQL  :15432 -> staging:5432
#   - Redis       :16379 -> staging:6379
#   - Qdrant      :16333 -> staging:6333
#   - Embedding   :18000 -> staging:8000
#   - Reranker    :18003 -> staging:8003
#   - Unstructured:18001 -> staging:8001
#   - SmolDocling :18002 -> staging:8002
#   - Ollama      :21434 -> staging:11434
#   - n8n         :15678 -> staging:5678
#   - Grafana     :13001 -> staging:3001
#   - Prometheus  :19090 -> staging:9090
#   - Orchestrator:18004 -> staging:8004
#
# Then run API with: cd apps/api/src/Api && dotnet run --launch-profile Integration
# And frontend with: cd apps/web && pnpm dev

set -e

SSH_KEY="${HOME}/.ssh/meepleai-staging"
STAGING_HOST="deploy@204.168.135.69"
CONTROL_SOCKET="${HOME}/.ssh/meepleai-tunnel.sock"

ACTION="${1:-start}"

print_ports() {
    echo ""
    echo "  -- Core ------------------------------------"
    echo "  PostgreSQL    localhost:15432 -> staging:5432"
    echo "  Redis         localhost:16379 -> staging:6379"
    echo "  Qdrant        localhost:16333 -> staging:6333"
    echo ""
    echo "  -- AI Services -----------------------------"
    echo "  Embedding     localhost:18000 -> staging:8000"
    echo "  Reranker      localhost:18003 -> staging:8003"
    echo "  Unstructured  localhost:18001 -> staging:8001"
    echo "  SmolDocling   localhost:18002 -> staging:8002"
    echo "  Ollama        localhost:21434 -> staging:11434"
    echo "  Orchestrator  localhost:18004 -> staging:8004"
    echo ""
    echo "  -- Monitoring & Automation -----------------"
    echo "  n8n           localhost:15678 -> staging:5678"
    echo "  Grafana       localhost:13001 -> staging:3001"
    echo "  Prometheus    localhost:19090 -> staging:9090"
    echo ""
}

do_start() {
    # Check if tunnel already active
    if ssh -O check -S "$CONTROL_SOCKET" "$STAGING_HOST" 2>/dev/null; then
        echo "Tunnel already active."
        print_ports
        return 0
    fi

    echo "Opening SSH tunnels to staging..."

    ssh -fN \
      -o ControlMaster=auto \
      -o ControlPath="$CONTROL_SOCKET" \
      -o ControlPersist=yes \
      -o ExitOnForwardFailure=yes \
      -o ServerAliveInterval=30 \
      -o ServerAliveCountMax=3 \
      -i "$SSH_KEY" \
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

    echo "Tunnels established:"
    print_ports
    echo "Now run:"
    echo "  Terminal 1: cd apps/api/src/Api && dotnet run --launch-profile Integration"
    echo "  Terminal 2: cd apps/web && pnpm dev"
    echo ""
}

do_stop() {
    echo "Closing SSH tunnels..."
    ssh -O exit -S "$CONTROL_SOCKET" "$STAGING_HOST" 2>/dev/null || true
    rm -f "$CONTROL_SOCKET"
    echo "Tunnels closed."
}

do_status() {
    if ssh -O check -S "$CONTROL_SOCKET" "$STAGING_HOST" 2>/dev/null; then
        echo "Tunnels ACTIVE"
        print_ports
    else
        echo "No active tunnels."
    fi
}

case "$ACTION" in
    start)  do_start ;;
    stop)   do_stop ;;
    status) do_status ;;
    *)
        echo "Usage: $0 [start|stop|status]"
        echo "  start   Open SSH tunnels (default)"
        echo "  stop    Close SSH tunnels"
        echo "  status  Check tunnel status"
        exit 1
        ;;
esac
