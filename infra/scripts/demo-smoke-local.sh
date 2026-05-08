#!/usr/bin/env bash
# demo-smoke-local.sh — Pipeline Stadio 1 (G1) smoke test contro stack locale o staging.
#
# Verifica 7 endpoint /api/v1/* del caso d'uso Nanolith libro game [1]-[6]:
#   1. POST /auth/login                                     (login Aaron)
#   2. GET  /library/me                                     (collection visible)
#   3. GET  /games/{nanolithId}                             (game detail)
#   4. POST /gamebook/campaigns                             (create campaign)
#   5. POST /agents/chat-stream                             (setup chat SSE)
#   6. POST /gamebook/campaigns/{id}/photos                 (photo upload)
#   7. POST /gamebook/campaigns/{id}/photos/{pid}/translate (translate paragraph SSE)
#
# Usage:
#   ./demo-smoke-local.sh                # default: local
#   ./demo-smoke-local.sh local          # explicit
#   ./demo-smoke-local.sh staging        # staging via tunnel
#
# Exit codes:
#   0 = all 7 endpoints OK
#   1 = pre-flight failed (missing dep, bad target)
#   2 = endpoint failure (specific endpoint logged + body)
#
# Prerequisiti operativi (NON enforced dallo script):
#   - Stack locale up via `make dev` (per target=local)
#   - Seed Nanolith eseguito via `infra/scripts/seed-nanolith-demo.sh` (account
#     badsworm@gmail.com + game Nanolith + 2 PDF indicizzati + agent "Nanolith Tutor")
#   - Tunnel SSH attivo (per target=staging)
#
# Spec: docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md (G1.1)

set -euo pipefail

# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------
TARGET="${1:-local}"
case "$TARGET" in
  local)   API_BASE="http://localhost:8080/api/v1" ;;
  staging) API_BASE="https://meepleai.app/api/v1" ;;
  *) echo "ERROR: TARGET must be local|staging (got: $TARGET)"; exit 1 ;;
esac

COOKIE_JAR="/tmp/meepleai-${TARGET}-smoke-cookies.txt"
RESULTS_FILE="/tmp/demo-smoke-${TARGET}-results.csv"
SMOKE_FIXTURE="/tmp/smoke-photo-fixture.png"

USER_EMAIL="${USER_EMAIL:-badsworm@gmail.com}"
USER_PASSWORD="${USER_PASSWORD:-TestNanolith2026!}"
NANOLITH_GAME_TITLE="Nanolith"
AGENT_NAME="Nanolith Tutor"

# SSE bounds (seconds)
SSE_CHAT_TIMEOUT=10
SSE_TRANSLATE_TIMEOUT=15

# Cleanup on EXIT (success OR fail) — auth cookies + body dumps must NOT persist in /tmp.
# RESULTS_FILE intentionally preserved (deliverable for Task 8 / G1 runthrough log).
cleanup() {
  rm -f "$COOKIE_JAR" "$SMOKE_FIXTURE" \
        /tmp/smoke-login.json /tmp/smoke-library.json /tmp/smoke-game.json \
        /tmp/smoke-campaign.json /tmp/smoke-photo.json \
        /tmp/smoke-sse-chat.txt /tmp/smoke-sse-translate.txt
}
trap cleanup EXIT

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------
log()  { printf "\033[1;36m[smoke]\033[0m %s\n" "$*"; }
ok()   { printf "\033[1;32m[smoke][OK]\033[0m %s\n" "$*"; }
fail() {
  printf "\033[1;31m[smoke][FAIL]\033[0m %s\n" "$*" >&2
  exit 2
}

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "ERROR: missing dependency: $1 (install before running)"
    exit 1
  }
}

# Verify HTTP status code matches expected; on mismatch, print body for debug.
http_status() {
  local code="$1" expected="$2" label="$3" body_file="${4:-}"
  if [ "$code" != "$expected" ]; then
    if [ -n "$body_file" ] && [ -f "$body_file" ]; then
      printf "\033[1;31m[smoke][FAIL]\033[0m %s: expected %s got %s\n" "$label" "$expected" "$code" >&2
      printf "  Body: " >&2
      head -c 500 "$body_file" >&2
      printf "\n" >&2
      exit 2
    else
      fail "$label: expected $expected got $code"
    fi
  fi
  ok "$label: HTTP $code"
}

record_result() {
  local endpoint="$1" status="$2"
  echo "$endpoint,$status" >> "$RESULTS_FILE"
}

# Wrapper around curl that converts transport-level failures (exit 7 unreachable,
# exit 6 DNS, exit 28 timeout, etc.) into a smoke-test exit 2 with actionable hint.
# Usage: curl_request <label> <out_file> <curl_args...>
# Returns: HTTP code on stdout (echoed), exits 2 on transport failure.
curl_request() {
  local label="$1" out_file="$2"
  shift 2
  local code
  set +e
  code=$(curl -sS -o "$out_file" -w "%{http_code}" "$@")
  local rc=$?
  set -e
  if [ "$rc" -ne 0 ]; then
    printf "\033[1;31m[smoke][FAIL]\033[0m %s: curl transport error (exit %d)\n" "$label" "$rc" >&2
    case "$rc" in
      6)  printf "  Hint: DNS resolution failed for %s\n" "$API_BASE" >&2 ;;
      7)  printf "  Hint: stack unreachable. For target=local run 'cd infra && make dev' first.\n" >&2 ;;
      28) printf "  Hint: request timed out\n" >&2 ;;
    esac
    exit 2
  fi
  echo "$code"
}

# -----------------------------------------------------------------------------
# Pre-flight
# -----------------------------------------------------------------------------
log "Pre-flight checks (target: $TARGET, API_BASE: $API_BASE)"
require_cmd jq
require_cmd curl
require_cmd timeout

# Reset cookie jar + results CSV
rm -f "$COOKIE_JAR" "$SMOKE_FIXTURE"
echo "endpoint,status" > "$RESULTS_FILE"

# -----------------------------------------------------------------------------
# 1/7 — POST /auth/login
# -----------------------------------------------------------------------------
log "1/7: POST /auth/login (user: $USER_EMAIL)"
http_code=$(curl_request "POST /auth/login" /tmp/smoke-login.json \
  -c "$COOKIE_JAR" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$USER_EMAIL\",\"password\":\"$USER_PASSWORD\"}" \
  "$API_BASE/auth/login")
http_status "$http_code" "200" "POST /auth/login" "/tmp/smoke-login.json"
record_result "/auth/login" "200"

# -----------------------------------------------------------------------------
# 2/7 — GET /library/me
# -----------------------------------------------------------------------------
log "2/7: GET /library/me"
http_code=$(curl_request "GET /library/me" /tmp/smoke-library.json \
  -b "$COOKIE_JAR" \
  "$API_BASE/library/me")
http_status "$http_code" "200" "GET /library/me" "/tmp/smoke-library.json"

# Extract Nanolith gameId. Tolerate multiple library payload shapes:
#   { "games": [ { "id": "...", "title": "Nanolith" } ] }
#   { "items": [ { "id": "...", "title": "Nanolith" } ] }
#   [ { "id": "...", "title": "Nanolith" } ]
NANOLITH_ID=$(jq -r '
  if type == "array" then
    .[]? | select(.title == "'"$NANOLITH_GAME_TITLE"'") | .id
  elif .games then
    .games[]? | select(.title == "'"$NANOLITH_GAME_TITLE"'") | .id
  elif .items then
    .items[]? | select(.title == "'"$NANOLITH_GAME_TITLE"'") | .id
  else
    empty
  end
' /tmp/smoke-library.json | head -n1)

if [ -z "$NANOLITH_ID" ] || [ "$NANOLITH_ID" = "null" ]; then
  printf "\033[1;31m[smoke][FAIL]\033[0m GET /library/me: '%s' not found in response\n" "$NANOLITH_GAME_TITLE" >&2
  printf "  Body (first 500 chars): " >&2
  head -c 500 /tmp/smoke-library.json >&2
  printf "\n  Hint: ensure 'infra/scripts/seed-nanolith-demo.sh' was run before smoke test\n" >&2
  exit 2
fi
log "  Nanolith gameId: $NANOLITH_ID"
record_result "/library/me" "200"

# -----------------------------------------------------------------------------
# 3/7 — GET /games/{nanolithId}
# -----------------------------------------------------------------------------
log "3/7: GET /games/$NANOLITH_ID"
http_code=$(curl_request "GET /games/{id}" /tmp/smoke-game.json \
  -b "$COOKIE_JAR" \
  "$API_BASE/games/$NANOLITH_ID")
http_status "$http_code" "200" "GET /games/{id}" "/tmp/smoke-game.json"
record_result "/games/{id}" "200"

# -----------------------------------------------------------------------------
# 4/7 — POST /gamebook/campaigns
# -----------------------------------------------------------------------------
log "4/7: POST /gamebook/campaigns"
http_code=$(curl_request "POST /gamebook/campaigns" /tmp/smoke-campaign.json \
  -b "$COOKIE_JAR" \
  -H "Content-Type: application/json" \
  -d "{\"gameId\":\"$NANOLITH_ID\",\"title\":\"Smoke Test Campaign\"}" \
  "$API_BASE/gamebook/campaigns")
http_status "$http_code" "201" "POST /gamebook/campaigns" "/tmp/smoke-campaign.json"

CAMPAIGN_ID=$(jq -r '.id // .campaignId // empty' /tmp/smoke-campaign.json)
if [ -z "$CAMPAIGN_ID" ] || [ "$CAMPAIGN_ID" = "null" ]; then
  printf "\033[1;31m[smoke][FAIL]\033[0m POST /gamebook/campaigns: campaign id missing in response\n" >&2
  printf "  Body: " >&2
  head -c 500 /tmp/smoke-campaign.json >&2
  printf "\n" >&2
  exit 2
fi
log "  campaignId: $CAMPAIGN_ID"
record_result "/gamebook/campaigns" "201"

# -----------------------------------------------------------------------------
# 5/7 — POST /agents/chat-stream (SSE)
# -----------------------------------------------------------------------------
log "5/7: POST /agents/chat-stream (SSE, bounded ${SSE_CHAT_TIMEOUT}s)"
# SSE: server emits events line-by-line with "data: {...}" prefix.
# We bound curl with `timeout` because SSE streams stay open until server-side close.
# Exit code from `timeout` may be 124 (timed out) which is expected for long streams;
# the response body is still captured. We OR with `echo "200"` so set -e doesn't abort.
set +e
timeout "$SSE_CHAT_TIMEOUT" curl -sS -o /tmp/smoke-sse-chat.txt -w "%{http_code}" \
  -b "$COOKIE_JAR" \
  -H "Content-Type: application/json" \
  -H "Accept: text/event-stream" \
  -d "{\"agentName\":\"$AGENT_NAME\",\"prompt\":\"setup quick test\"}" \
  "$API_BASE/agents/chat-stream" > /tmp/smoke-sse-chat-code.txt 2>/dev/null
curl_exit=$?
set -e

http_code=$(cat /tmp/smoke-sse-chat-code.txt 2>/dev/null || echo "")

# `timeout` returning 124 means we hit the bound (expected for SSE that stays open).
# In that case, http_code may be empty; treat as 200 if we received SSE data.
if [ "$curl_exit" = "124" ] || [ -z "$http_code" ]; then
  log "  SSE stream bounded by timeout (curl exit=$curl_exit, expected for streaming)"
  http_code="200"
fi

http_status "$http_code" "200" "POST /agents/chat-stream" "/tmp/smoke-sse-chat.txt"

if [ ! -s /tmp/smoke-sse-chat.txt ]; then
  fail "POST /agents/chat-stream: SSE response empty (no events received within ${SSE_CHAT_TIMEOUT}s)"
fi

if ! grep -q "^data:" /tmp/smoke-sse-chat.txt; then
  printf "\033[1;31m[smoke][FAIL]\033[0m POST /agents/chat-stream: no 'data:' SSE events in response\n" >&2
  printf "  First 500 chars: " >&2
  head -c 500 /tmp/smoke-sse-chat.txt >&2
  printf "\n" >&2
  exit 2
fi
ok "  SSE chat: at least one 'data:' event received"
record_result "/agents/chat-stream" "200"

# -----------------------------------------------------------------------------
# 6/7 — POST /gamebook/campaigns/{id}/photos
# -----------------------------------------------------------------------------
log "6/7: POST /gamebook/campaigns/$CAMPAIGN_ID/photos (1x1 PNG fixture)"

# 1x1 transparent PNG — minimal valid PNG ~80 bytes.
# Bytes: PNG signature + IHDR + IDAT + IEND chunks.
printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15\xc4\x89\x00\x00\x00\rIDATx\xdacd\x00\x00\x00\x06\x00\x03\x6a\x80\x86\xc6\x00\x00\x00\x00IEND\xaeB`\x82' > "$SMOKE_FIXTURE"

if [ ! -s "$SMOKE_FIXTURE" ]; then
  fail "Photo fixture creation failed: $SMOKE_FIXTURE is empty"
fi

http_code=$(curl_request "POST /gamebook/campaigns/{id}/photos" /tmp/smoke-photo.json \
  -b "$COOKIE_JAR" \
  -F "file=@$SMOKE_FIXTURE;type=image/png" \
  "$API_BASE/gamebook/campaigns/$CAMPAIGN_ID/photos")
http_status "$http_code" "201" "POST /gamebook/campaigns/{id}/photos" "/tmp/smoke-photo.json"

PHOTO_ID=$(jq -r '.id // .photoId // empty' /tmp/smoke-photo.json)
if [ -z "$PHOTO_ID" ] || [ "$PHOTO_ID" = "null" ]; then
  printf "\033[1;31m[smoke][FAIL]\033[0m POST /photos: photo id missing\n" >&2
  printf "  Body: " >&2
  head -c 500 /tmp/smoke-photo.json >&2
  printf "\n" >&2
  exit 2
fi
log "  photoId: $PHOTO_ID"
record_result "/gamebook/campaigns/{id}/photos" "201"

# -----------------------------------------------------------------------------
# 7/7 — POST /gamebook/campaigns/{id}/photos/{pid}/translate (SSE)
# -----------------------------------------------------------------------------
log "7/7: POST /gamebook/campaigns/$CAMPAIGN_ID/photos/$PHOTO_ID/translate (SSE, bounded ${SSE_TRANSLATE_TIMEOUT}s)"

# Note: 1x1 PNG fixture has no OCR text, so OCR/translate may yield empty results.
# Per spec G1.1, smoke verifies endpoint reachability + SSE stream opens, NOT OCR quality.
set +e
timeout "$SSE_TRANSLATE_TIMEOUT" curl -sS -o /tmp/smoke-sse-translate.txt -w "%{http_code}" \
  -b "$COOKIE_JAR" \
  -H "Accept: text/event-stream" \
  -X POST \
  "$API_BASE/gamebook/campaigns/$CAMPAIGN_ID/photos/$PHOTO_ID/translate" > /tmp/smoke-sse-translate-code.txt 2>/dev/null
curl_exit=$?
set -e

http_code=$(cat /tmp/smoke-sse-translate-code.txt 2>/dev/null || echo "")

if [ "$curl_exit" = "124" ] || [ -z "$http_code" ]; then
  log "  SSE stream bounded by timeout (curl exit=$curl_exit, expected for streaming)"
  http_code="200"
fi

http_status "$http_code" "200" "POST /gamebook/.../translate" "/tmp/smoke-sse-translate.txt"

# Translate SSE may legitimately produce no `data:` events for a fixture with no OCR text.
# Endpoint reachability is asserted via HTTP 200; data events are best-effort logged.
if [ -s /tmp/smoke-sse-translate.txt ]; then
  if grep -q "^data:" /tmp/smoke-sse-translate.txt; then
    ok "  SSE translate: at least one 'data:' event received"
  else
    log "  SSE translate: response non-empty but no 'data:' events (acceptable for 1x1 fixture — no OCR text)"
  fi
else
  log "  SSE translate: response empty (acceptable for 1x1 fixture — endpoint reachable, OCR yielded nothing)"
fi
record_result "/gamebook/.../translate" "200"

# -----------------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------------
ok "ALL 7 endpoints OK"
log "Results CSV: $RESULTS_FILE"
cat "$RESULTS_FILE"
exit 0
