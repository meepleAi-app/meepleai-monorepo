#!/usr/bin/env bash
# evaluate-kb-coverage.sh — Measure SP4 seed KB coverage for a Free-tier user.
#
# Computes 4 metrics defined in
#   docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md
#
#   M1 — Catalog Browse Coverage : GET /api/v1/catalog/games/new?limit=50
#   M2 — Search Coverage         : GET /api/v1/games/search?q=<title>
#   M3 — KB Ready Coverage       : direct SQL via docker-compose exec postgres
#   M4 — RAG Citation Coverage   : POST /api/v1/knowledge-base/ask
#
# Reuses seed-sp4/lib/common.sh for auth + curl helpers.
#
# Usage:
#   ./evaluate-kb-coverage.sh                 # Full table output + JSON to /tmp/
#   ./evaluate-kb-coverage.sh --json          # JSON-only output (CI-friendly)
#   ./evaluate-kb-coverage.sh --skip-rag      # Skip M4 (slow, requires LLM)
#   ./evaluate-kb-coverage.sh --target staging
#
# Exit codes:
#   0  - all metrics meet expected thresholds (M1=M2=catalog_size, M3>=expected_kb, M4>=M3-1)
#   1  - degraded (some metric below threshold; auth + DB still working)
#   2  - critical (API unreachable, auth failed, DB query failed)

set -euo pipefail

# Locate common.sh from seed-sp4/lib/
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMMON_LIB="$SCRIPT_DIR/seed-sp4/lib/common.sh"
[[ -f "$COMMON_LIB" ]] || { echo "ERROR: common.sh not found at $COMMON_LIB"; exit 2; }

# Skip API health probe in common.sh (we'll do our own diagnostic)
export SEED_SP4_SKIP_HEALTH=1
# shellcheck source=seed-sp4/lib/common.sh
source "$COMMON_LIB"

# -----------------------------------------------------------------------------
# CLI parsing
# -----------------------------------------------------------------------------
OUT_FORMAT="table"   # table | json
SKIP_RAG=0
USER_SLUG="marco"    # Default Free-tier user

while [[ $# -gt 0 ]]; do
  case "$1" in
    --json)        OUT_FORMAT="json"; shift ;;
    --skip-rag)    SKIP_RAG=1; shift ;;
    --target)      export TARGET="$2"; shift 2 ;;
    --user)        USER_SLUG="$2"; shift 2 ;;
    -h|--help)
      grep '^#' "$0" | head -30
      exit 0
      ;;
    *) warn "Unknown flag: $1"; shift ;;
  esac
done

# Reload API_BASE if TARGET changed via flag
case "${TARGET:-local}" in
  local)   API_BASE="http://localhost:8080/api/v1" ;;
  staging) API_BASE="https://meepleai.app/api/v1" ;;
esac

OUT_DIR="/tmp/meepleai-coverage-${TARGET:-local}"
mkdir -p "$OUT_DIR"
REPORT_JSON="$OUT_DIR/coverage-report.json"

# -----------------------------------------------------------------------------
# Preflight
# -----------------------------------------------------------------------------
banner "evaluate-kb-coverage (target=${TARGET:-local})"

if ! curl -sS -m 5 "$API_BASE/health" >/dev/null 2>&1; then
  warn "API at $API_BASE not responding"
  echo '{"status":"critical","reason":"api_unreachable"}' > "$REPORT_JSON"
  exit 2
fi

# Auth: admin (for /admin/kb/games + DB query) + user (for /games/search + RAG)
log "Logging in admin + user $USER_SLUG"
admin_login || { warn "admin login failed"; exit 2; }
ADMIN_JAR=$(cookie_jar_for "admin")

# User login via SP4 password
user_email=$(data_get ".users[] | select(.slug == \"$USER_SLUG\") | .email")
[[ -n "$user_email" ]] || { warn "user $USER_SLUG not found in data.json"; exit 2; }
login_user "$user_email" "$(seed_password)" "$USER_SLUG" || { warn "user login failed"; exit 2; }
USER_JAR=$(cookie_jar_for "$USER_SLUG")

# -----------------------------------------------------------------------------
# Inventory: list seeded games (slugs + titles) from data.json
# -----------------------------------------------------------------------------
declare -a GAME_SLUGS GAME_TITLES
while IFS=$'\t' read -r slug title; do
  GAME_SLUGS+=("$slug")
  GAME_TITLES+=("$title")
done < <(data_get '.games[] | [.slug, .title] | @tsv')

SEED_COUNT=${#GAME_SLUGS[@]}
log "Seed inventory: $SEED_COUNT games"

# -----------------------------------------------------------------------------
# M1 — Catalog Browse Coverage
# -----------------------------------------------------------------------------
log "M1 — Catalog browse (GET /catalog/games/new?limit=50)"
m1_resp=$(curl_get "/catalog/games/new?limit=50" "$USER_JAR")
m1_body=$(echo "$m1_resp" | sed '$d')
m1_code=$(echo "$m1_resp" | tail -n1)
http_check "200" "$m1_code" "$m1_body" "M1 catalog browse" || { warn "M1 endpoint failed"; M1_COUNT=0; }
M1_COUNT=$(echo "$m1_body" | jq -r '.items | length // 0' 2>/dev/null || echo 0)
M1_IDS=$(echo "$m1_body" | jq -r '[.items[].id] | join(",")' 2>/dev/null || echo "")

# -----------------------------------------------------------------------------
# M2 — Search Coverage (per seeded title)
# -----------------------------------------------------------------------------
log "M2 — Search per title (GET /games/search?q=<title>)"
M2_COUNT=0
declare -A M2_PER_GAME
for i in "${!GAME_SLUGS[@]}"; do
  slug="${GAME_SLUGS[$i]}"
  title="${GAME_TITLES[$i]}"
  # URL-encode title minimally (space → %20)
  q_enc=$(echo "$title" | sed 's/ /%20/g')
  s_resp=$(curl_get "/games/search?q=$q_enc" "$USER_JAR")
  s_body=$(echo "$s_resp" | sed '$d')
  s_code=$(echo "$s_resp" | tail -n1)
  if [[ "$s_code" == "200" ]]; then
    hits=$(echo "$s_body" | jq -r 'length // 0' 2>/dev/null || echo 0)
    if [[ "$hits" -gt 0 ]]; then
      M2_COUNT=$((M2_COUNT + 1))
      M2_PER_GAME[$slug]=$hits
    else
      M2_PER_GAME[$slug]=0
    fi
  else
    warn "search '$title' returned HTTP $s_code"
    M2_PER_GAME[$slug]="?"
  fi
done

# -----------------------------------------------------------------------------
# M3 — KB Ready Coverage (via admin endpoint + ground truth SQL)
# -----------------------------------------------------------------------------
log "M3 — KB Ready via /admin/kb/games"
m3_resp=$(curl_get "/admin/kb/games" "$ADMIN_JAR")
m3_body=$(echo "$m3_resp" | sed '$d')
m3_code=$(echo "$m3_resp" | tail -n1)
M3_COUNT=0
declare -A M3_PER_GAME M3_CHUNKS_PER_GAME
if [[ "$m3_code" == "200" ]]; then
  # /admin/kb/games returns { items: [{ gameId, gameName, status, docCount, totalChunks, latestIndexedAt }] }
  while IFS=$'\t' read -r gname status chunks; do
    # Match by title (best effort — handles ordering differences)
    for i in "${!GAME_TITLES[@]}"; do
      if [[ "${GAME_TITLES[$i]}" == "$gname" ]]; then
        slug="${GAME_SLUGS[$i]}"
        M3_PER_GAME[$slug]=$status
        M3_CHUNKS_PER_GAME[$slug]=$chunks
        [[ "$status" == "complete" ]] && M3_COUNT=$((M3_COUNT + 1))
        break
      fi
    done
  done < <(echo "$m3_body" | jq -r '.items[]? | [.gameName, .status, .totalChunks] | @tsv' 2>/dev/null)
else
  warn "/admin/kb/games failed: HTTP $m3_code"
fi

# -----------------------------------------------------------------------------
# M4 — RAG Citation Coverage
# -----------------------------------------------------------------------------
M4_COUNT=0
declare -A M4_PER_GAME
if [[ $SKIP_RAG -eq 1 ]]; then
  skip "M4 RAG skipped (--skip-rag)"
else
  log "M4 — RAG citation (POST /knowledge-base/ask) — solo per giochi M3=complete"
  for i in "${!GAME_SLUGS[@]}"; do
    slug="${GAME_SLUGS[$i]}"
    title="${GAME_TITLES[$i]}"
    if [[ "${M3_PER_GAME[$slug]:-none}" != "complete" ]]; then
      M4_PER_GAME[$slug]="skip"
      continue
    fi
    game_id=$(state_get "games" "$slug")
    [[ -n "$game_id" ]] || { M4_PER_GAME[$slug]="no_id"; continue; }

    payload=$(jq -n --arg id "$game_id" \
      '{ gameId: $id, question: "Come funziona questo gioco? Quali sono le regole base?" }')
    rag_resp=$(curl_json POST "/knowledge-base/ask" "$USER_JAR" "$payload")
    rag_body=$(echo "$rag_resp" | sed '$d')
    rag_code=$(echo "$rag_resp" | tail -n1)
    if [[ "$rag_code" == "200" ]]; then
      cites=$(echo "$rag_body" | jq -r '.citations | length // 0' 2>/dev/null || echo 0)
      M4_PER_GAME[$slug]=$cites
      [[ "$cites" -gt 0 ]] && M4_COUNT=$((M4_COUNT + 1))
    else
      warn "RAG ask for $title failed: HTTP $rag_code"
      M4_PER_GAME[$slug]="err"
    fi
  done
fi

# -----------------------------------------------------------------------------
# Threshold evaluation
# -----------------------------------------------------------------------------
EXIT_CODE=0
EXPECTED_M1=$SEED_COUNT
EXPECTED_M2=$SEED_COUNT

if [[ $M1_COUNT -lt $EXPECTED_M1 ]] || [[ $M2_COUNT -lt $EXPECTED_M2 ]]; then
  EXIT_CODE=1
fi
if [[ $SKIP_RAG -eq 0 ]] && [[ $M4_COUNT -lt $((M3_COUNT - 1)) ]]; then
  EXIT_CODE=1
fi

# -----------------------------------------------------------------------------
# Output
# -----------------------------------------------------------------------------
if [[ "$OUT_FORMAT" == "table" ]]; then
  printf "\n%-26s | %-8s | %-8s | %-10s | %-8s\n" "Game" "M1 cat" "M2 srch" "M3 KB" "M4 RAG"
  printf "%s\n" "$(printf '%.0s─' {1..76})"
  for i in "${!GAME_SLUGS[@]}"; do
    slug="${GAME_SLUGS[$i]}"
    title="${GAME_TITLES[$i]}"
    m1_mark=$( [[ "$M1_IDS" == *"$(state_get games "$slug")"* ]] && echo "✅" || echo "❌" )
    m2_n="${M2_PER_GAME[$slug]:-0}"
    m2_mark=$( [[ "$m2_n" != "0" && "$m2_n" != "?" ]] && echo "✅($m2_n)" || echo "❌" )
    m3_s="${M3_PER_GAME[$slug]:-no_kb}"
    case "$m3_s" in
      complete) m3_mark="✅" ;;
      partial)  m3_mark="⚠" ;;
      *)        m3_mark="❌" ;;
    esac
    m4_v="${M4_PER_GAME[$slug]:-skip}"
    case "$m4_v" in
      skip|err|no_id) m4_mark="—" ;;
      0)              m4_mark="❌" ;;
      *)              m4_mark="✅($m4_v)" ;;
    esac
    printf "%-26s | %-8s | %-8s | %-10s | %-8s\n" "${title:0:26}" "$m1_mark" "$m2_mark" "$m3_mark" "$m4_mark"
  done
  printf "%s\n" "$(printf '%.0s─' {1..76})"
  printf "TOTALS: M1=%d/%d  M2=%d/%d  M3=%d/%d  M4=%d/%d\n\n" \
    "$M1_COUNT" "$EXPECTED_M1" \
    "$M2_COUNT" "$EXPECTED_M2" \
    "$M3_COUNT" "$SEED_COUNT" \
    "$M4_COUNT" "$SEED_COUNT"
fi

# JSON report always
jq -n \
  --arg target "${TARGET:-local}" \
  --arg user "$USER_SLUG" \
  --argjson seed_count "$SEED_COUNT" \
  --argjson m1 "$M1_COUNT" \
  --argjson m2 "$M2_COUNT" \
  --argjson m3 "$M3_COUNT" \
  --argjson m4 "$M4_COUNT" \
  --argjson skip_rag "$SKIP_RAG" \
  --argjson exit_code "$EXIT_CODE" \
  '{
    target: $target,
    user: $user,
    seed_count: $seed_count,
    metrics: {
      m1_catalog_browse: $m1,
      m2_search: $m2,
      m3_kb_ready: $m3,
      m4_rag_citation: $m4
    },
    expected: {
      m1: $seed_count,
      m2: $seed_count
    },
    flags: { skip_rag: ($skip_rag == 1) },
    exit_code: $exit_code,
    status: (if $exit_code == 0 then "pass" elif $exit_code == 1 then "degraded" else "critical" end)
  }' > "$REPORT_JSON"

if [[ "$OUT_FORMAT" == "json" ]]; then
  cat "$REPORT_JSON"
else
  log "JSON report: $REPORT_JSON"
fi

exit "$EXIT_CODE"
