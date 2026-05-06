#!/usr/bin/env bash
# work.sh — Hybrid dev loop: SSH tunnel + local API+Web + watchdog
#
# Usage:
#   bash infra/scripts/work.sh         # Start everything
#   bash infra/scripts/work.sh stop    # Stop everything (also via Ctrl+C in foreground)
#
# Pre-flight checks SSH key permissions, then:
#   1. Opens SSH tunnels to staging (postgres+redis+AI services)
#   2. Starts API + Web locally in Integration mode
#   3. Watchdog loop: re-opens tunnel if SSH socket dies
#   4. Trap on SIGINT/SIGTERM: clean teardown of all processes

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SSH_KEY="${HOME}/.ssh/meepleai-staging"
WATCHDOG_PID_FILE="/tmp/meepleai-work-watchdog.pid"
MAX_WATCHDOG_RETRIES=5

ACTION="${1:-start}"

log() { echo "[$(date '+%H:%M:%S')] $*"; }
fail() { log "❌ $*"; exit 1; }

# ─────────────────────────────────────────────
# Pre-flight checks
# ─────────────────────────────────────────────
preflight() {
  log "🔍 Pre-flight checks..."

  [ -f "$SSH_KEY" ] || fail "SSH key not found: $SSH_KEY"

  local perms
  perms=$(stat -c '%a' "$SSH_KEY" 2>/dev/null || stat -f '%A' "$SSH_KEY" 2>/dev/null)
  if [ "$perms" != "600" ] && [ "$perms" != "400" ]; then
    fail "SSH key permissions: chmod 600 required (current: $perms)"
  fi

  command -v dotnet >/dev/null || fail "dotnet not found in PATH"
  command -v pnpm >/dev/null || fail "pnpm not found in PATH"

  # Quick connectivity check
  ssh -o ConnectTimeout=5 -o BatchMode=yes -i "$SSH_KEY" deploy@204.168.135.69 'echo ok' >/dev/null 2>&1 \
    || fail "SSH to staging failed (check VPN/network)"

  log "✅ Pre-flight OK"
}

# ─────────────────────────────────────────────
# Start tunnel + watchdog + API + Web
# ─────────────────────────────────────────────
do_start() {
  preflight

  log "🚇 Opening SSH tunnels..."
  bash "$SCRIPT_DIR/integration-tunnel.sh" start || fail "tunnel start failed"

  log "🚀 Starting API + Web (integration mode)..."
  bash "$SCRIPT_DIR/integration-start.sh" all &
  local INT_PID=$!

  # Watchdog loop: re-opens tunnel if it dies
  (
    local retries=0
    while true; do
      sleep 10
      if ! ssh -O check -S "${HOME}/.ssh/meepleai-tunnel.sock" deploy@204.168.135.69 2>/dev/null; then
        retries=$((retries+1))
        if [ "$retries" -gt "$MAX_WATCHDOG_RETRIES" ]; then
          log "❌ Watchdog: max retries ($MAX_WATCHDOG_RETRIES) exceeded, giving up"
          break
        fi
        log "⚠️  Tunnel dropped, restarting (retry $retries/$MAX_WATCHDOG_RETRIES)..."
        bash "$SCRIPT_DIR/integration-tunnel.sh" start && log "✅ Tunnel restored" \
          || log "❌ Tunnel restart failed"
      fi
    done
  ) &
  echo $! > "$WATCHDOG_PID_FILE"

  # Trap on EXIT too — ensures cleanup if integration-start.sh exits naturally
  # (e.g., dotnet/pnpm crash) and not via Ctrl+C. do_stop is idempotent.
  trap 'do_stop; exit 0' INT TERM EXIT

  log "✅ make work running"
  log "   API: http://localhost:8080"
  log "   Web: http://localhost:3000"
  log "   Stop with Ctrl+C or 'make work-stop'"

  wait $INT_PID
}

# ─────────────────────────────────────────────
# Stop everything cleanly
# ─────────────────────────────────────────────
do_stop() {
  log "🛑 Stopping make work..."

  if [ -f "$WATCHDOG_PID_FILE" ]; then
    local wpid
    wpid=$(cat "$WATCHDOG_PID_FILE")
    kill "$wpid" 2>/dev/null || true
    rm -f "$WATCHDOG_PID_FILE"
    log "  Watchdog stopped (PID $wpid)"
  fi

  bash "$SCRIPT_DIR/integration-start.sh" stop || true
  bash "$SCRIPT_DIR/integration-tunnel.sh" stop || true

  # Force-kill any orphan processes on integration ports
  if command -v lsof >/dev/null; then
    lsof -ti:8080 2>/dev/null | xargs -r kill -9 2>/dev/null || true
    lsof -ti:3000 2>/dev/null | xargs -r kill -9 2>/dev/null || true
  fi

  log "✅ All stopped"
}

case "$ACTION" in
  start) do_start ;;
  stop)  do_stop ;;
  *) fail "Usage: $0 [start|stop]" ;;
esac
