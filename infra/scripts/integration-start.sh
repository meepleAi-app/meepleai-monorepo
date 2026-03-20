#!/usr/bin/env bash
# Integration Start — launches API + Web locally against staging services
#
# Prerequisites:
#   bash infra/scripts/integration-tunnel.sh start
#
# Usage:
#   bash infra/scripts/integration-start.sh          # Start both API + Web
#   bash infra/scripts/integration-start.sh api       # Start API only
#   bash infra/scripts/integration-start.sh web       # Start Web only
#   bash infra/scripts/integration-start.sh stop      # Stop all

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
INFRA_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_DIR="$(cd "$INFRA_DIR/.." && pwd)"
API_DIR="$REPO_DIR/apps/api/src/Api"
WEB_DIR="$REPO_DIR/apps/web"
SECRETS_DIR="$INFRA_DIR/secrets/integration"

ACTION="${1:-all}"

# Load secrets from .secret files
load_secrets() {
    for f in "$SECRETS_DIR"/*.secret; do
        [ -f "$f" ] || continue
        while IFS= read -r line; do
            # Skip comments and empty lines
            [[ "$line" =~ ^[[:space:]]*# ]] && continue
            [[ -z "$line" ]] && continue
            local key="${line%%=*}"
            local value="${line#*=}"
            export "$key=$value"
        done < "$f"
    done
}

start_api() {
    echo "Starting API in integration mode..."

    # Load secrets
    load_secrets

    # Override for integration
    export ASPNETCORE_ENVIRONMENT=Integration
    export ASPNETCORE_URLS="http://+:8080"
    export POSTGRES_HOST=localhost
    export POSTGRES_PORT=15432
    export POSTGRES_SSL_MODE=Disable
    export REDIS_HOST=localhost
    export REDIS_PORT=16379
    export SkipMigrations=true
    export EMBEDDING_PROVIDER=external
    export Embedding__Provider=External
    export EMBEDDING_MODEL=intfloat/multilingual-e5-base
    export EMBEDDING_DIMENSIONS=768
    export Embedding__Dimensions=768
    export LOCAL_EMBEDDING_URL=https://meepleai.app/services/embedding
    export Embedding__LocalServiceUrl=https://meepleai.app/services/embedding
    export RERANKER_URL=https://meepleai.app/services/reranker
    export EMBEDDING_FALLBACK_ENABLED=false
    export Embedding__FallbackEnabled=false

    # Build connection string with SSL Mode=Disable (tunnel already encrypts)
    export ConnectionStrings__Postgres="Host=localhost;Port=15432;Database=${POSTGRES_DB:-meepleai_staging};Username=${POSTGRES_USER:-meepleai};Password=${POSTGRES_PASSWORD};SSL Mode=Disable"

    cd "$API_DIR"
    dotnet run &
    API_PID=$!
    echo "API started (PID: $API_PID) on http://localhost:8080"
    echo "$API_PID" > /tmp/meepleai-integration-api.pid
}

start_web() {
    echo "Starting Web in integration mode..."
    cd "$WEB_DIR"
    NEXT_PUBLIC_API_BASE=http://localhost:8080 pnpm dev &
    WEB_PID=$!
    echo "Web started (PID: $WEB_PID) on http://localhost:3000"
    echo "$WEB_PID" > /tmp/meepleai-integration-web.pid
}

do_stop() {
    echo "Stopping integration services..."
    if [ -f /tmp/meepleai-integration-api.pid ]; then
        kill "$(cat /tmp/meepleai-integration-api.pid)" 2>/dev/null || true
        rm -f /tmp/meepleai-integration-api.pid
        echo "API stopped."
    fi
    if [ -f /tmp/meepleai-integration-web.pid ]; then
        kill "$(cat /tmp/meepleai-integration-web.pid)" 2>/dev/null || true
        rm -f /tmp/meepleai-integration-web.pid
        echo "Web stopped."
    fi
    # Also kill any dotnet/next processes on integration ports
    lsof -ti:8080 2>/dev/null | xargs kill 2>/dev/null || true
    lsof -ti:3000 2>/dev/null | xargs kill 2>/dev/null || true
    echo "Done."
}

case "$ACTION" in
    all)
        start_api
        sleep 2
        start_web
        echo ""
        echo "Integration mode running:"
        echo "  API: http://localhost:8080"
        echo "  Web: http://localhost:3000"
        echo ""
        echo "Stop with: bash infra/scripts/integration-start.sh stop"
        wait
        ;;
    api) start_api; wait ;;
    web) start_web; wait ;;
    stop) do_stop ;;
    *)
        echo "Usage: $0 [all|api|web|stop]"
        exit 1
        ;;
esac
