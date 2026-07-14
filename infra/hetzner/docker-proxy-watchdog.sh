#!/usr/bin/env bash
# docker-proxy-watchdog.sh — Issue #2772 Option C (interim mitigation)
#
# The intermittent api.meepleai.app 502 (#2772) happens when the host userland
# `docker-proxy` process for the api's published port dies under VPS
# memory/disk pressure: both host bindings (127.0.0.1:8080 + ::1:8080) then
# refuse connections while the container stays healthy on the docker network,
# so cloudflared (VPS-native) gets `connection refused` → 502.
#
# This watchdog detects that specific signature — host :8080 unreachable while
# the container is running — and `docker restart`s the container to recreate
# docker-proxy. It is an INTERIM band-aid; the structural fix is the
# containerized cloudflared cutover (Option A, PR #2909 scaffold). Remove this
# watchdog once that cutover is validated.
#
# Deploy: copy to /usr/local/bin/, chmod +x, schedule via docker-proxy-watchdog.cron.
# Requires: docker + curl on the host (both already present on the VPS).
# Tunables via env: WATCHDOG_CONTAINER, WATCHDOG_PORT, WATCHDOG_COOLDOWN, WATCHDOG_STATE.
set -euo pipefail

CONTAINER="${WATCHDOG_CONTAINER:-meepleai-api}"
PORT="${WATCHDOG_PORT:-8080}"
COOLDOWN="${WATCHDOG_COOLDOWN:-600}"   # min seconds between restarts (loop guard)
STATE_FILE="${WATCHDOG_STATE:-/var/run/meepleai-docker-proxy-watchdog.last}"
RETRIES=3
RETRY_DELAY=5

log() { echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) [docker-proxy-watchdog] $*"; }

# Connection-level probe of one host binding. Returns 0 (up) if curl connects at
# all — even a 404/503 means docker-proxy is alive. Only a refused connection
# (curl rc 7) or a connect/transfer timeout (rc 28) counts as the binding being
# down. A hung API (timeout) is also worth a restart, so rc 28 counts as down.
port_up() {  # $1 = host: 127.0.0.1 | ::1
  local host="$1" url rc=0
  if [ "$host" = "::1" ]; then url="http://[::1]:${PORT}/health/live"; else url="http://${host}:${PORT}/health/live"; fi
  curl -s -o /dev/null --connect-timeout 3 --max-time 5 "$url" || rc=$?
  [ "$rc" -ne 7 ] && [ "$rc" -ne 28 ]
}

# Both dual-stack bindings down = docker-proxy dead (per #2770 diagnosis).
both_down() { ! port_up 127.0.0.1 && ! port_up ::1; }

# Require the failure to persist across RETRIES checks to ignore transient blips.
confirm_down() {
  local i
  for i in $(seq 1 "$RETRIES"); do
    both_down || return 1          # recovered on some check → not down
    [ "$i" -lt "$RETRIES" ] && sleep "$RETRY_DELAY"
  done
  return 0
}

main() {
  # Happy path: bindings reachable → stay quiet (cron logs would spam otherwise).
  confirm_down || exit 0

  log "host :${PORT} unreachable on both 127.0.0.1 and ::1 after ${RETRIES} checks."

  local running
  running=$(docker inspect -f '{{.State.Running}}' "$CONTAINER" 2>/dev/null || echo "missing")
  if [ "$running" != "true" ]; then
    log "container ${CONTAINER} not running (state=${running}); NOT restarting (restart policy / separate alert owns a down container)."
    exit 0
  fi

  local now last
  now=$(date +%s)
  last=$(cat "$STATE_FILE" 2>/dev/null || echo 0)
  if [ $((now - last)) -lt "$COOLDOWN" ]; then
    log "within cooldown ($((now - last))s < ${COOLDOWN}s); NOT restarting. If :${PORT} is still down, the API itself (not docker-proxy) is likely the fault — investigate."
    exit 0
  fi

  log "container ${CONTAINER} running but host :${PORT} down → docker-proxy likely dead. Restarting to recreate it."
  mkdir -p "$(dirname "$STATE_FILE")"
  echo "$now" > "$STATE_FILE"
  if docker restart "$CONTAINER" >/dev/null; then
    log "docker restart ${CONTAINER} issued; will re-verify next run."
  else
    log "ERROR: docker restart ${CONTAINER} failed."
    exit 1
  fi
}

main "$@"
