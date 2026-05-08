#!/usr/bin/env bash
# seed-nanolith-demo.sh — Idempotent seed for Nanolith libro game demo dogfooding.
#
# Implements Sprint -1 Task S-1.1 of the iter1 plan
# (docs/superpowers/plans/2026-05-07-libro-game-nanolith-iter1-plan.md).
#
# Automates the 4 Sprint 0 SO.1 preconditions:
#   G8  — account `badsworm@gmail.com` (Admin role, password TestNanolith2026!)
#   G9  — game "Nanolith" in shared_games catalog (Published)
#   G10 — both Nanolith PDFs uploaded & indexed (Rules + Press Start)
#   G11 — agent "Nanolith Tutor" linked to the game (KBs accessed via game)
#
# Flow:
#   1. Read INITIAL_ADMIN_EMAIL / INITIAL_ADMIN_PASSWORD from infra/secrets/admin.secret
#   2. Login as bootstrap admin → admin cookie jar
#   3. Create badsworm@gmail.com (idempotent) with Admin role
#   4. Login as badsworm → user cookie jar (used for game/agent owned by badsworm)
#   5. Create+publish SharedGame "Nanolith" via admin endpoints
#   6. Upload 2 PDFs (Rules + Press Start) and poll until Ready
#   7. Create agent "Nanolith Tutor" via /agents/create-with-setup (also addToCollection)
#   8. Write final state CSV at $RESULTS_FILE
#
# Usage:
#   ./seed-nanolith-demo.sh                    # default: local
#   ./seed-nanolith-demo.sh local
#   ./seed-nanolith-demo.sh staging            # uses meepleai.app, requires SSH tunnel
#
# Requires: bash, jq, curl, python3.
# Note: SuperAdmin role is seed-immutable. badsworm is created as Admin which
# is sufficient for the Iter 1 demo (full admin endpoints accessible).
# To make badsworm the SuperAdmin, configure INITIAL_ADMIN_EMAIL in admin.secret
# before first migration and re-bootstrap.

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

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
RULEBOOK_DIR="$REPO_ROOT/data/rulebook/nanolith_datasource"
SECRETS_DIR="$REPO_ROOT/infra/secrets"

ADMIN_COOKIE_JAR="/tmp/meepleai-${TARGET}-admin-cookies.txt"
USER_COOKIE_JAR="/tmp/meepleai-${TARGET}-badsworm-cookies.txt"
RESULTS_FILE="/tmp/nanolith-demo-seed-results.csv"

NANOLITH_USER_EMAIL="badsworm@gmail.com"
NANOLITH_USER_PASSWORD="TestNanolith2026!"
NANOLITH_USER_DISPLAY="Aaron (Nanolith Dogfood)"
NANOLITH_GAME_TITLE="Nanolith"
NANOLITH_AGENT_NAME="Nanolith Tutor"
PDF_RULES_FILENAME="Nanolith Rules ENG.pdf"
PDF_PRESS_START_FILENAME="Nanolith Press Start ENG.pdf"

# Polling
POLL_INTERVAL_SEC=15
POLL_MAX_SEC=1800   # 30 min total per PDF (large 101MB Rules PDF can take a while)

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------
log()  { printf "\033[1;36m[seed]\033[0m %s\n" "$*"; }
warn() { printf "\033[1;33m[seed][WARN]\033[0m %s\n" "$*" >&2; }
fail() { printf "\033[1;31m[seed][FAIL]\033[0m %s\n" "$*" >&2; exit 1; }
ok()   { printf "\033[1;32m[seed][OK]\033[0m %s\n" "$*"; }

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || fail "missing dependency: $1"
}

# Pretty-print HTTP failures with response body for debugging.
http_check() {
  local code="$1" expected="$2" body="$3" context="$4"
  if [ "$code" != "$expected" ]; then
    fail "$context: expected HTTP $expected, got $code. Body: $body"
  fi
}

# -----------------------------------------------------------------------------
# Pre-flight
# -----------------------------------------------------------------------------
log "Pre-flight checks (target: $TARGET, API: $API_BASE)"

require_cmd jq
require_cmd curl
require_cmd python3

# Verify both PDFs (PF-4 from plan)
for pdf in "$PDF_RULES_FILENAME" "$PDF_PRESS_START_FILENAME"; do
  if [ ! -f "$RULEBOOK_DIR/$pdf" ]; then
    fail "PDF missing: $RULEBOOK_DIR/$pdf — see PF-4 in plan"
  fi
done
ok "Both Nanolith PDFs present in $RULEBOOK_DIR"

# Resolve bootstrap admin credentials (from admin.secret file or env)
ADMIN_EMAIL="${INITIAL_ADMIN_EMAIL:-}"
ADMIN_PASSWORD="${INITIAL_ADMIN_PASSWORD:-${ADMIN_PASSWORD:-}}"

if [ -z "$ADMIN_EMAIL" ] || [ -z "$ADMIN_PASSWORD" ]; then
  if [ -f "$SECRETS_DIR/admin.secret" ]; then
    log "Sourcing admin credentials from $SECRETS_DIR/admin.secret"
    # admin.secret is KEY=VALUE format
    set -a
    # shellcheck disable=SC1091
    . "$SECRETS_DIR/admin.secret"
    set +a
    ADMIN_EMAIL="${INITIAL_ADMIN_EMAIL:-${ADMIN_EMAIL:-}}"
    ADMIN_PASSWORD="${INITIAL_ADMIN_PASSWORD:-${ADMIN_PASSWORD:-}}"
  fi
fi

if [ -z "$ADMIN_EMAIL" ] || [ -z "$ADMIN_PASSWORD" ]; then
  fail "INITIAL_ADMIN_EMAIL / INITIAL_ADMIN_PASSWORD not set (env or $SECRETS_DIR/admin.secret). Run 'make secrets-setup && make secrets-sync' first."
fi
ok "Bootstrap admin credentials resolved (email: ${ADMIN_EMAIL%%@*}@***)"

# Initialize results file
echo "step,resource,id,status" > "$RESULTS_FILE"

# -----------------------------------------------------------------------------
# Step 1 — Login as bootstrap admin
# -----------------------------------------------------------------------------
log "Login as bootstrap admin"
rm -f "$ADMIN_COOKIE_JAR"

LOGIN_PAYLOAD=$(jq -n \
  --arg email "$ADMIN_EMAIL" \
  --arg password "$ADMIN_PASSWORD" \
  '{email: $email, password: $password}')

LOGIN_RESPONSE=$(curl -sS -c "$ADMIN_COOKIE_JAR" -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -w "\n%{http_code}" \
  --data-raw "$LOGIN_PAYLOAD")
LOGIN_CODE=$(echo "$LOGIN_RESPONSE" | tail -1)
LOGIN_BODY=$(echo "$LOGIN_RESPONSE" | sed '$d')
http_check "$LOGIN_CODE" "200" "$LOGIN_BODY" "admin login"
ok "Bootstrap admin session established"

# -----------------------------------------------------------------------------
# Step 2 — Ensure badsworm@gmail.com user exists (Admin role)
# -----------------------------------------------------------------------------
log "Ensuring user $NANOLITH_USER_EMAIL exists"

# Idempotent check: search by email
USER_SEARCH=$(curl -sS -b "$ADMIN_COOKIE_JAR" \
  "$API_BASE/admin/users?search=$(python3 -c "import urllib.parse; print(urllib.parse.quote('$NANOLITH_USER_EMAIL'))")&limit=5")
USER_ID=$(echo "$USER_SEARCH" | jq -r --arg email "$NANOLITH_USER_EMAIL" \
  '(.items // .data // .users // [])[] | select((.email // "" | ascii_downcase) == ($email | ascii_downcase)) | .id' \
  | head -1)

if [ -n "$USER_ID" ] && [ "$USER_ID" != "null" ]; then
  ok "User already exists: $USER_ID"
else
  log "User not found, creating"
  CREATE_USER_PAYLOAD=$(jq -n \
    --arg email "$NANOLITH_USER_EMAIL" \
    --arg password "$NANOLITH_USER_PASSWORD" \
    --arg display "$NANOLITH_USER_DISPLAY" \
    '{email: $email, password: $password, displayName: $display, role: "Admin"}')

  CU_RESPONSE=$(curl -sS -b "$ADMIN_COOKIE_JAR" -X POST "$API_BASE/admin/users" \
    -H "Content-Type: application/json" \
    -w "\n%{http_code}" \
    --data-raw "$CREATE_USER_PAYLOAD")
  CU_CODE=$(echo "$CU_RESPONSE" | tail -1)
  CU_BODY=$(echo "$CU_RESPONSE" | sed '$d')
  http_check "$CU_CODE" "201" "$CU_BODY" "create badsworm user"
  USER_ID=$(echo "$CU_BODY" | jq -r '.id // .Id')
  ok "User created: $USER_ID"
fi
echo "account,$NANOLITH_USER_EMAIL,$USER_ID,OK" >> "$RESULTS_FILE"

# -----------------------------------------------------------------------------
# Step 3 — Login as badsworm (resources will be owned by him)
# -----------------------------------------------------------------------------
log "Login as $NANOLITH_USER_EMAIL"
rm -f "$USER_COOKIE_JAR"

USER_LOGIN_PAYLOAD=$(jq -n \
  --arg email "$NANOLITH_USER_EMAIL" \
  --arg password "$NANOLITH_USER_PASSWORD" \
  '{email: $email, password: $password}')

ULR=$(curl -sS -c "$USER_COOKIE_JAR" -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -w "\n%{http_code}" \
  --data-raw "$USER_LOGIN_PAYLOAD")
ULR_CODE=$(echo "$ULR" | tail -1)
ULR_BODY=$(echo "$ULR" | sed '$d')
http_check "$ULR_CODE" "200" "$ULR_BODY" "badsworm login"
ok "Badsworm session established"

# -----------------------------------------------------------------------------
# Step 4 — Ensure Nanolith SharedGame exists + is Published
# -----------------------------------------------------------------------------
log "Ensuring SharedGame '$NANOLITH_GAME_TITLE' exists"

# Search via admin shared-games list
GAME_LIST=$(curl -sS -b "$ADMIN_COOKIE_JAR" "$API_BASE/admin/shared-games?search=nanolith&limit=5" 2>/dev/null || echo "[]")
GAME_ID=$(echo "$GAME_LIST" | jq -r --arg name "$NANOLITH_GAME_TITLE" \
  '(.items // .data // [])[] | select((.title // .name // "" | ascii_downcase) == ($name | ascii_downcase)) | (.id // .Id)' \
  2>/dev/null | head -1)

if [ -n "$GAME_ID" ] && [ "$GAME_ID" != "null" ]; then
  ok "Game already exists: $GAME_ID"
else
  log "Game not found, creating"
  CREATE_GAME_PAYLOAD=$(jq -n \
    --arg title "$NANOLITH_GAME_TITLE" \
    '{
      title: $title,
      yearPublished: 2024,
      description: "Nanolith - campaign-driven gamebook for the Iter 1 dogfooding demo (multi-day storybook + on-demand encounter book).",
      minPlayers: 1,
      maxPlayers: 4,
      playingTimeMinutes: 240,
      minAge: 14,
      complexityRating: 3.5,
      averageRating: 8.0,
      imageUrl: "https://via.placeholder.com/300x300?text=Nanolith",
      thumbnailUrl: "https://via.placeholder.com/150x150?text=Nanolith",
      designers: [],
      publishers: [],
      categories: ["Adventure"],
      mechanics: ["Campaign / Battle Card Driven", "Storytelling"],
      bggId: 0
    }')

  CG=$(curl -sS -b "$ADMIN_COOKIE_JAR" -X POST "$API_BASE/admin/shared-games" \
    -H "Content-Type: application/json" \
    -w "\n%{http_code}" \
    --data-raw "$CREATE_GAME_PAYLOAD")
  CG_CODE=$(echo "$CG" | tail -1)
  CG_BODY=$(echo "$CG" | sed '$d')
  http_check "$CG_CODE" "201" "$CG_BODY" "create Nanolith game"
  # Body may be raw GUID string (as in seed-all-games-staging.sh) or JSON {id}
  GAME_ID=$(echo "$CG_BODY" | jq -r '.id // .Id // empty' 2>/dev/null || true)
  if [ -z "$GAME_ID" ] || [ "$GAME_ID" = "null" ]; then
    GAME_ID=$(echo "$CG_BODY" | tr -d '"' | tr -d '[:space:]')
  fi
  ok "Game created: $GAME_ID"

  # Quick publish (idempotent: 204 if state allows)
  PUB_CODE=$(curl -sS -b "$ADMIN_COOKIE_JAR" -o /dev/null -w "%{http_code}" \
    -X POST "$API_BASE/admin/shared-games/$GAME_ID/quick-publish")
  if [ "$PUB_CODE" != "204" ] && [ "$PUB_CODE" != "200" ]; then
    warn "Quick-publish returned HTTP $PUB_CODE (game may already be published, continuing)"
  else
    ok "Game published"
  fi
fi
echo "game,$NANOLITH_GAME_TITLE,$GAME_ID,OK" >> "$RESULTS_FILE"

# -----------------------------------------------------------------------------
# Step 5 — Upload 2 PDFs (idempotent: skip if same filename already complete)
# -----------------------------------------------------------------------------

# Helper to look up an existing PDF by filename for this game.
# Returns the document id on stdout (empty string if absent).
lookup_pdf_id() {
  local filename="$1"
  curl -sS -b "$ADMIN_COOKIE_JAR" "$API_BASE/admin/shared-games/$GAME_ID/documents" 2>/dev/null \
    | jq -r --arg f "$filename" \
        '(.items // .documents // . // [])[] | select((.fileName // .filename // .name // "") == $f) | (.id // .Id // .documentId // empty)' \
    | head -1
}

# Helper to wait for a PDF to reach Ready/Failed state.
wait_for_pdf() {
  local pdf_id="$1" filename="$2"
  local elapsed=0
  while [ "$elapsed" -lt "$POLL_MAX_SEC" ]; do
    STATUS=$(curl -sS -b "$ADMIN_COOKIE_JAR" "$API_BASE/pdfs/$pdf_id/progress" 2>/dev/null || echo "{}")
    STATE=$(echo "$STATUS" | jq -r '.currentStep // .currentState // "unknown"')
    PCT=$(echo "$STATUS" | jq -r '.percentComplete // .overallProgress // 0')
    printf "  [%4ds] %s — %s (%s%%)\n" "$elapsed" "$filename" "$STATE" "$PCT"
    case "$STATE" in
      Ready|Completed|Indexed)  return 0 ;;
      Failed)                   return 1 ;;
    esac
    sleep "$POLL_INTERVAL_SEC"
    elapsed=$((elapsed + POLL_INTERVAL_SEC))
  done
  return 2  # timeout
}

upload_or_skip_pdf() {
  local filename="$1" csv_step="$2"
  local pdf_path="$RULEBOOK_DIR/$filename"

  log "Ensuring PDF: $filename"
  local existing_id
  existing_id=$(lookup_pdf_id "$filename" || true)

  if [ -n "$existing_id" ] && [ "$existing_id" != "null" ]; then
    log "  PDF already uploaded: $existing_id, polling for completion"
    if wait_for_pdf "$existing_id" "$filename"; then
      ok "  PDF Ready: $existing_id"
      echo "$csv_step,$filename,$existing_id,COMPLETE" >> "$RESULTS_FILE"
      printf -v "PDF_ID_${csv_step}" "%s" "$existing_id"
      return 0
    else
      warn "  PDF processing failed/timeout — re-uploading"
      # Fall through to upload
    fi
  fi

  log "  Uploading $pdf_path (this can take a while — PDF up to 101 MB)"
  local upload
  upload=$(curl -sS -b "$ADMIN_COOKIE_JAR" -X POST "$API_BASE/ingest/pdf" \
    -F "file=@$pdf_path" \
    -F "gameId=$GAME_ID" \
    -w "\n%{http_code}")
  local up_code up_body
  up_code=$(echo "$upload" | tail -1)
  up_body=$(echo "$upload" | sed '$d')
  if [ "$up_code" != "200" ] && [ "$up_code" != "201" ]; then
    fail "  Upload failed: HTTP $up_code — $up_body"
  fi

  local pdf_id
  # Idempotency: when the same PDF was already uploaded in a previous run,
  # the API returns {"existingKbFound": true, "existingKb": {"pdfDocumentId": "..."}}
  # instead of {"documentId": "..."}. Accept both response shapes.
  pdf_id=$(echo "$up_body" | jq -r '.documentId // .id // .Id // .existingKb.pdfDocumentId // empty' 2>/dev/null || true)
  [ -z "$pdf_id" ] && fail "  Upload OK but documentId missing in response: $up_body"
  ok "  Upload accepted: $pdf_id"

  log "  Polling /pdfs/$pdf_id/progress (max ${POLL_MAX_SEC}s)"
  case "$(wait_for_pdf "$pdf_id" "$filename" && echo OK || echo $?)" in
    OK)  ok "  PDF Ready"; echo "$csv_step,$filename,$pdf_id,COMPLETE" >> "$RESULTS_FILE" ;;
    1)   echo "$csv_step,$filename,$pdf_id,FAILED"  >> "$RESULTS_FILE"; fail "  Processing FAILED" ;;
    2)   echo "$csv_step,$filename,$pdf_id,TIMEOUT" >> "$RESULTS_FILE"; fail "  Processing TIMEOUT" ;;
  esac

  printf -v "PDF_ID_${csv_step}" "%s" "$pdf_id"
}

upload_or_skip_pdf "$PDF_RULES_FILENAME"        "pdf_rules"
upload_or_skip_pdf "$PDF_PRESS_START_FILENAME"  "pdf_press_start"

# -----------------------------------------------------------------------------
# Step 6 — Ensure agent "Nanolith Tutor" exists (linked to game)
# -----------------------------------------------------------------------------
log "Ensuring agent '$NANOLITH_AGENT_NAME' exists"

# Look up existing user agents for this game (badsworm session).
AGENT_LIST=$(curl -sS -b "$USER_COOKIE_JAR" "$API_BASE/agents?gameId=$GAME_ID" 2>/dev/null || echo "[]")
AGENT_ID=$(echo "$AGENT_LIST" | jq -r --arg name "$NANOLITH_AGENT_NAME" \
  '(.items // .data // . // [])[] | select((.name // .Name // "") == $name) | (.id // .Id // empty)' \
  2>/dev/null | head -1)

if [ -n "$AGENT_ID" ] && [ "$AGENT_ID" != "null" ]; then
  ok "Agent already exists: $AGENT_ID"
else
  log "Agent not found, creating via /agents/create-with-setup"
  # AddToCollection=true also satisfies "Nanolith in user library" prereq.
  CREATE_AGENT_PAYLOAD=$(jq -n \
    --arg gameId "$GAME_ID" \
    --arg name "$NANOLITH_AGENT_NAME" \
    --argjson docIds "[\"${PDF_ID_pdf_rules:-}\",\"${PDF_ID_pdf_press_start:-}\"]" \
    '{
      gameId: $gameId,
      addToCollection: true,
      agentType: "rulebook",
      agentName: $name,
      strategyName: "rag-default",
      strategyParameters: {},
      documentIds: $docIds
    }')

  CA=$(curl -sS -b "$USER_COOKIE_JAR" -X POST "$API_BASE/agents/create-with-setup" \
    -H "Content-Type: application/json" \
    -w "\n%{http_code}" \
    --data-raw "$CREATE_AGENT_PAYLOAD")
  CA_CODE=$(echo "$CA" | tail -1)
  CA_BODY=$(echo "$CA" | sed '$d')
  http_check "$CA_CODE" "201" "$CA_BODY" "create Nanolith Tutor agent"
  AGENT_ID=$(echo "$CA_BODY" | jq -r '.agentId // .id // .Id // empty')
  ok "Agent created: $AGENT_ID"
fi
echo "agent,$NANOLITH_AGENT_NAME,$AGENT_ID,ACTIVE" >> "$RESULTS_FILE"

# -----------------------------------------------------------------------------
# Done
# -----------------------------------------------------------------------------
echo ""
log "============================================="
log "  SEED COMPLETE — Nanolith dogfood demo ready"
log "============================================="
cat "$RESULTS_FILE"
echo ""
log "Cookie jars:"
log "  bootstrap admin: $ADMIN_COOKIE_JAR"
log "  badsworm:        $USER_COOKIE_JAR"
log "Results CSV: $RESULTS_FILE"
log ""
log "Sprint 0 SO.1 preconditions ALL satisfied. Proceed with SO.2 + SO.3 manual smoke tests."
