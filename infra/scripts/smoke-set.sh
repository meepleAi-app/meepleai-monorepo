#!/usr/bin/env bash
# smoke-set.sh — Run automated G1 smoke scenarios against an endpoint
#
# Usage:
#   bash infra/scripts/smoke-set.sh [BASE_URL] [TEST_EMAIL] [TEST_PASSWORD]
#   bash infra/scripts/smoke-set.sh http://localhost:8080 user@test.local pass123
#   bash infra/scripts/smoke-set.sh https://meepleai.app
#
# Env overrides:
#   BASE_URL, TEST_EMAIL, TEST_PASSWORD (alternative to positional args)
#   TEST_GAME_ID (opt-in for A3 KB-status scenario)
#
# Coverage (post spec-panel review Fix #1):
#   AUTOMATED here   → A1, A2, A3, A5, C1, C3-endpoint, C5-perf-proxy
#   SEPARATE script  → C2 migration validation via pg-readback.sh (DB direct, requires tunnel)
#   MANUAL checklist → A4 SSE chat citations, C5 RAG real query <10s
#                      (see docs/operations/smoke-manual-ui-checklist.md)
#
# Exit code: 0 if all automated PASS, 1 if any FAIL.
#
# Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G1

set -uo pipefail

BASE_URL="${1:-${BASE_URL:-http://localhost:8080}}"
TEST_EMAIL="${2:-${TEST_EMAIL:-}}"
TEST_PASSWORD="${3:-${TEST_PASSWORD:-}}"

PASS=0
FAIL=0
COOKIE_JAR="$(mktemp)"
trap 'rm -f "$COOKIE_JAR"' EXIT

log() { echo "[$(date '+%H:%M:%S')] $*"; }
ok() { echo "  ✅ PASS — $1"; PASS=$((PASS+1)); }
ko() { echo "  ❌ FAIL — $1 ($2)"; FAIL=$((FAIL+1)); }

# ─────────────────────────────────────────────
# Helpers
# ─────────────────────────────────────────────
check_status() {
  local name="$1" url="$2" expected="$3"
  local status
  status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 -b "$COOKIE_JAR" -c "$COOKIE_JAR" "$url" 2>/dev/null || echo "000")
  if [ "$status" = "$expected" ]; then
    ok "$name (HTTP $status)"
  else
    ko "$name" "got $status, expected $expected"
  fi
}

# ─────────────────────────────────────────────
# Smoke scenarios
# ─────────────────────────────────────────────
log "🧪 Smoke set against: $BASE_URL"

# A1 — Login → Library mostra giochi (skipped if no creds)
if [ -n "$TEST_EMAIL" ] && [ -n "$TEST_PASSWORD" ]; then
  log "A1: Login + GET /api/v1/library/me"
  LOGIN_RES=$(curl -sf -X POST "$BASE_URL/api/v1/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"$TEST_EMAIL\",\"password\":\"$TEST_PASSWORD\"}" \
    -c "$COOKIE_JAR" -w "\n%{http_code}" 2>/dev/null || echo "000")
  STATUS=$(echo "$LOGIN_RES" | tail -1)
  if [ "$STATUS" = "200" ]; then
    ok "A1.login (HTTP 200)"
    check_status "A1.library_me" "$BASE_URL/api/v1/library/me" "200"
  else
    ko "A1.login" "got $STATUS"
  fi
else
  log "A1: SKIPPED (TEST_EMAIL/TEST_PASSWORD not set)"
fi

# A2 — Search BGG (admin-only endpoint per BggEndpoints.cs:49 — RequireAdminSession,
# BGG licensing constraint). Accept 200 (admin authenticated via cookie) OR
# 401 (unauthenticated/non-admin) — both prove endpoint is reachable and auth works.
# Same pattern as C3.
log "A2: GET /api/v1/bgg/search?query=Catan"
RES_A2=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 -b "$COOKIE_JAR" -c "$COOKIE_JAR" "$BASE_URL/api/v1/bgg/search?query=Catan" 2>/dev/null || echo "000")
if [ "$RES_A2" = "200" ] || [ "$RES_A2" = "401" ]; then
  ok "A2.bgg_search (HTTP $RES_A2)"
else
  ko "A2.bgg_search" "got $RES_A2"
fi

# A3 — KB status of test game (skipped if no test game ID)
if [ -n "${TEST_GAME_ID:-}" ]; then
  log "A3: GET /api/v1/games/$TEST_GAME_ID/kb-status"
  check_status "A3.kb_status" "$BASE_URL/api/v1/games/$TEST_GAME_ID/kb-status" "200"
else
  log "A3: SKIPPED (TEST_GAME_ID env not set — set to validate KB indexing)"
fi

# A4 — Chat citations (manual UI — see docs/operations/smoke-manual-ui-checklist.md)
log "A4: SKIPPED here (manual UI — see smoke-manual-ui-checklist.md)"

# A5 — Logout (POST-only endpoint per AuthenticationEndpoints.cs)
if [ -n "$TEST_EMAIL" ]; then
  log "A5: POST /api/v1/auth/logout"
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 \
    -X POST -b "$COOKIE_JAR" -c "$COOKIE_JAR" "$BASE_URL/api/v1/auth/logout" 2>/dev/null || echo "000")
  if [ "$STATUS" = "200" ]; then
    ok "A5.logout (HTTP 200)"
  else
    ko "A5.logout" "got $STATUS"
  fi
fi

# C1 — Deploy senza rotture (validated by health endpoint)
log "C1: GET /health (deploy proxy)"
check_status "C1.health" "$BASE_URL/health" "200"

# C2 — Migration applicata: separate validation via pg-readback.sh (requires tunnel)
log "C2: SKIPPED here (use pg-readback.sh for DB-direct migration validation)"

# C3 — Backup: validated by backup-verify cron. Smoke checks the list endpoint
# (avoids 404 on /snapshots/latest where "latest" is treated as a Guid id).
log "C3: GET /api/v1/admin/rag-backup/snapshots"
# May 401 if not admin — accept 200 OR 401 (endpoint reachable)
RES_C3=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 -b "$COOKIE_JAR" "$BASE_URL/api/v1/admin/rag-backup/snapshots" 2>/dev/null || echo "000")
if [ "$RES_C3" = "200" ] || [ "$RES_C3" = "401" ]; then
  ok "C3.backup_endpoint (HTTP $RES_C3)"
else
  ko "C3.backup_endpoint" "got $RES_C3"
fi

# C5 — RAG perf <10s (light heuristic: shared-games endpoint <2s as proxy)
# Real RAG query measurement is in manual UI checklist (see smoke-manual-ui-checklist.md)
log "C5: GET /api/v1/shared-games?pageNumber=1&pageSize=1 (perf proxy — real RAG via manual)"
START=$(date +%s%N)
check_status "C5.perf_proxy" "$BASE_URL/api/v1/shared-games?pageNumber=1&pageSize=1" "200"
END=$(date +%s%N)
ELAPSED_MS=$(( (END - START) / 1000000 ))
if [ "$ELAPSED_MS" -lt 2000 ]; then
  ok "C5.perf_under_2s (${ELAPSED_MS}ms)"
else
  ko "C5.perf_under_2s" "${ELAPSED_MS}ms (expected <2000ms)"
fi

# ─────────────────────────────────────────────
# Summary
# ─────────────────────────────────────────────
echo ""
log "📊 Summary: $PASS PASS / $FAIL FAIL"
log "ℹ️  Manual checklist (A4 chat, C5 real RAG) — see docs/operations/smoke-manual-ui-checklist.md"
log "ℹ️  C2 migration validation — run pg-readback.sh"
if [ "$FAIL" -eq 0 ]; then
  log "✅ Automated smoke set passed"
  exit 0
else
  log "❌ Automated smoke set FAILED"
  exit 1
fi
