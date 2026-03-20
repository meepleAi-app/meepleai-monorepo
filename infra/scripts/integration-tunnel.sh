#!/usr/bin/env bash
# Integration Tunnel — connects local dev to staging services via SSH
#
# Usage:
#   bash infra/scripts/integration-tunnel.sh [start|stop|status]
#
# Opens SSH tunnels to staging server (meepleai.app) for:
#   - PostgreSQL  :15432 -> staging container (dynamic IP)
#   - Redis       :16379 -> staging container (dynamic IP)
#   - AI/monitoring services -> staging host ports
#
# Then run:
#   bash infra/scripts/integration-start.sh   (API + Web)
#   OR manually:
#     Terminal 1: cd apps/api/src/Api && dotnet run --launch-profile Integration
#     Terminal 2: cd apps/web && pnpm dev

set -e

SSH_KEY="${HOME}/.ssh/meepleai-staging"
STAGING_HOST="deploy@204.168.135.69"
CONTROL_SOCKET="${HOME}/.ssh/meepleai-tunnel.sock"

ACTION="${1:-start}"

resolve_container_ip() {
    local container="$1"
    ssh -i "$SSH_KEY" "$STAGING_HOST" \
        "docker inspect $container --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'" 2>/dev/null
}

print_ports() {
    echo ""
    echo "  -- Core ------------------------------------"
    echo "  PostgreSQL    localhost:15432 -> staging postgres (Docker IP)"
    echo "  Redis         localhost:16379 -> staging redis (Docker IP)"
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

    echo "Resolving staging Docker container IPs..."

    # Postgres/Redis don't expose ports on the staging host — tunnel directly to container IPs
    POSTGRES_IP=$(resolve_container_ip meepleai-postgres)
    REDIS_IP=$(resolve_container_ip meepleai-redis)

    if [ -z "$POSTGRES_IP" ]; then
        echo "ERROR: Cannot resolve meepleai-postgres container IP. Is staging running?"
        exit 1
    fi
    if [ -z "$REDIS_IP" ]; then
        echo "ERROR: Cannot resolve meepleai-redis container IP. Is staging running?"
        exit 1
    fi

    echo "  PostgreSQL container IP: $POSTGRES_IP"
    echo "  Redis container IP:      $REDIS_IP"
    echo ""
    echo "Opening SSH tunnels to staging..."

    ssh -fN \
      -o ControlMaster=auto \
      -o ControlPath="$CONTROL_SOCKET" \
      -o ControlPersist=yes \
      -o ExitOnForwardFailure=yes \
      -o ServerAliveInterval=30 \
      -o ServerAliveCountMax=3 \
      -i "$SSH_KEY" \
      -L 15432:${POSTGRES_IP}:5432 \
      -L 16379:${REDIS_IP}:6379 \
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
    echo "  bash infra/scripts/integration-start.sh"
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
