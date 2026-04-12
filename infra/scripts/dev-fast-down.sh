#!/usr/bin/env bash
# Stops everything started by dev-fast.sh.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"
PIDS_FILE="$INFRA_DIR/.dev-fast.pids"

BLUE='\033[0;34m'
YELLOW='\033[0;33m'
GREEN='\033[0;32m'
NC='\033[0m'

log() { echo -e "${BLUE}[dev-fast-down]${NC} $*"; }

if [ ! -f "$PIDS_FILE" ]; then
  log "No PID file found at $PIDS_FILE — nothing to stop"
  exit 0
fi

while IFS=: read -r label pid; do
  if [ -z "${pid:-}" ]; then continue; fi
  if kill -0 "$pid" 2>/dev/null; then
    log "Stopping $label (pid $pid)"
    kill "$pid" 2>/dev/null || true
  else
    log "$label (pid $pid) already gone"
  fi
done < "$PIDS_FILE"

# Graceful window
sleep 1

# Force-kill survivors
while IFS=: read -r label pid; do
  if [ -z "${pid:-}" ]; then continue; fi
  if kill -0 "$pid" 2>/dev/null; then
    log "Force-killing $label (pid $pid)"
    kill -9 "$pid" 2>/dev/null || true
  fi
done < "$PIDS_FILE"

rm -f "$PIDS_FILE"

# Stop Docker services (idempotent)
log "Stopping Docker services (postgres, redis)..."
(cd "$INFRA_DIR" && docker compose stop postgres redis 2>/dev/null || true)

echo -e "${GREEN}[dev-fast-down] Done${NC}"
