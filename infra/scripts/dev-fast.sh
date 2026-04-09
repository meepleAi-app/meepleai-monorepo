#!/usr/bin/env bash
# MeepleDev fast dev loop orchestrator.
# Reads .env.dev.local and boots only what's needed.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"
REPO_ROOT="$(dirname "$INFRA_DIR")"
PIDS_FILE="$INFRA_DIR/.dev-fast.pids"
LOG_FILE="$INFRA_DIR/dev-fast.log"

RED='\033[0;31m'
YELLOW='\033[0;33m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

log() { echo -e "${BLUE}[dev-fast]${NC} $*"; }
warn() { echo -e "${YELLOW}[dev-fast]${NC} $*" >&2; }
err() { echo -e "${RED}[dev-fast]${NC} $*" >&2; }

# 1. Env check
log "Checking .env.dev.local..."
bash "$SCRIPT_DIR/dev-env-check.sh"

# 2. Source env
if [ -f "$INFRA_DIR/.env.dev.local" ]; then
  set -a
  # shellcheck disable=SC1090
  source "$INFRA_DIR/.env.dev.local"
  set +a
else
  err ".env.dev.local not found after check — aborting"
  exit 1
fi

# 3. Initialize PID file
: > "$PIDS_FILE"
: > "$LOG_FILE"

cleanup_on_exit() {
  warn "Caught signal — use 'make dev:fast-down' to clean up if needed"
}
trap cleanup_on_exit INT TERM

# 4. Postgres if requested
if [ "${DEV_POSTGRES:-false}" = "true" ]; then
  log "Starting Postgres..."
  (cd "$INFRA_DIR" && docker compose up -d postgres) >> "$LOG_FILE" 2>&1
fi

# 5. Redis if requested
if [ "${DEV_REDIS:-false}" = "true" ]; then
  log "Starting Redis..."
  (cd "$INFRA_DIR" && docker compose up -d redis) >> "$LOG_FILE" 2>&1
fi

# 6. Backend (dotnet watch) if requested
if [ "${DEV_BACKEND:-false}" = "true" ]; then
  log "Starting backend (dotnet watch)..."
  (
    cd "$REPO_ROOT/apps/api/src/Api"
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    ASPNETCORE_ENVIRONMENT=Development \
      dotnet watch run --no-launch-profile \
      >> "$INFRA_DIR/dotnet-watch.log" 2>&1 &
    echo "dotnet:$!" >> "$PIDS_FILE"
  )
  log "Backend starting (logs: $INFRA_DIR/dotnet-watch.log)"
fi

# 7. Frontend (pnpm dev)
log "Starting frontend (Next.js)..."
(
  cd "$REPO_ROOT/apps/web"
  NEXT_PUBLIC_MOCK_MODE="${NEXT_PUBLIC_MOCK_MODE:-true}" \
  NEXT_PUBLIC_MSW_ENABLE="${NEXT_PUBLIC_MSW_ENABLE:-}" \
  NEXT_PUBLIC_MSW_DISABLE="${NEXT_PUBLIC_MSW_DISABLE:-}" \
  NEXT_PUBLIC_DEV_SCENARIO="${NEXT_PUBLIC_DEV_SCENARIO:-small-library}" \
  NEXT_PUBLIC_DEV_AS_ROLE="${NEXT_PUBLIC_DEV_AS_ROLE:-Admin}" \
    pnpm dev >> "$LOG_FILE" 2>&1 &
  echo "next:$!" >> "$PIDS_FILE"
)

log "${GREEN}All services started.${NC}"
log "Frontend: http://localhost:3000"
[ "${DEV_BACKEND:-false}" = "true" ] && log "Backend:  http://localhost:8080"
log "Logs: $LOG_FILE  |  Backend logs: $INFRA_DIR/dotnet-watch.log"
log "Stop: make dev:fast-down"

# Keep script alive so Ctrl+C reaches children
wait
