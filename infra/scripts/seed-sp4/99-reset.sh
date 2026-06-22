#!/usr/bin/env bash
# 99-reset.sh — Delete SP4 seeded data via API (reverse-order, best-effort).
#
# WARNING: deletes ALL entities tracked in $STATE_FILE for the current target.
# Use with TARGET=local for safety; --confirm required for staging.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

if [[ "$TARGET" == "staging" && "${1:-}" != "--confirm" ]]; then
  fail "STAGING reset requires --confirm flag (you don't want to nuke staging by accident)"
fi

banner "99 — Reset SP4 (delete all seeded entities)"

ADMIN_JAR=$(cookie_jar_for "admin")
[[ -s "$ADMIN_JAR" ]] || admin_login

# Helper: DELETE with cookie + tolerant status check.
delete_each() {
  local kind="$1" path_template="$2" jar="$3"
  local n=0 ok_n=0 skipped_n=0
  while IFS= read -r entry; do
    local slug=$(echo "$entry" | jq -r '.key')
    local id=$(echo "$entry" | jq -r '.value')
    local path="${path_template/\{id\}/$id}"
    local resp=$(curl -sS -X DELETE -b "$jar" -c "$jar" -w "\n%{http_code}" "$API_BASE$path")
    local code=$(echo "$resp" | tail -n1)
    n=$((n + 1))
    case "$code" in
      200|204|404) ok_n=$((ok_n + 1)) ;;
      *) skipped_n=$((skipped_n + 1)); warn "  $kind/$slug DELETE HTTP $code" ;;
    esac
  done < <(jq -r --arg k "$kind" '.[$k] // {} | to_entries[] | @json' "$STATE_FILE" | tr -d '\r')
  log "  $kind: deleted=$ok_n  skipped/failed=$skipped_n  total=$n"
  state_clear "$kind"
}

# Reverse-order cleanup (respect FK dependencies):
#   chats → events → playRecords → sessions → toolkits → agents → kbDocs → games → users
log "Reverse-order delete via API…"
delete_each "chats"        "/chat/sessions/{id}"     "$ADMIN_JAR"
delete_each "events"       "/game-nights/{id}"       "$ADMIN_JAR"
delete_each "playRecords"  "/play-records/{id}"      "$ADMIN_JAR"
delete_each "sessions"     "/live-sessions/{id}"     "$ADMIN_JAR"
delete_each "toolkits"     "/game-toolkits/{id}"     "$ADMIN_JAR"
delete_each "agents"       "/agents/user/{id}"       "$ADMIN_JAR"
delete_each "kbDocs"       "/admin/pdfs/{id}"        "$ADMIN_JAR"
delete_each "games"        "/admin/shared-games/{id}" "$ADMIN_JAR"
delete_each "users"        "/admin/users/{id}"       "$ADMIN_JAR"

# Drop state file + cookies
log "Removing state file + cookie jars…"
rm -f "$STATE_FILE"
rm -rf "$COOKIE_DIR"

ok "99-reset complete (some endpoints may not support DELETE — those entities stay in DB)"
log "Note: re-running seed steps after reset is safe (idempotent lookup-first)."
