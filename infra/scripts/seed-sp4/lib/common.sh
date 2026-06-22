#!/usr/bin/env bash
# common.sh — shared helpers for seed-sp4/* step scripts.
# Source pattern (in each step script):
#   source "$(dirname "$(readlink -f "$0")")/lib/common.sh"
#
# Conventions:
#   - All step scripts are idempotent: re-running them is safe (lookup-then-create).
#   - All step scripts are tolerant: a step's failure should not block the
#     orchestrator from running subsequent steps if the user passes `--continue-on-error`.
#   - Cookie jars are stored in /tmp/meepleai-sp4-*.txt and reused across step scripts
#     within the same session, but invalidated if older than 30 minutes.
#   - State (created IDs slug→guid) is persisted in $STATE_FILE so subsequent
#     steps can resolve references without re-fetching.

set -euo pipefail

# -----------------------------------------------------------------------------
# Logging
# -----------------------------------------------------------------------------
log()  { printf "\033[1;36m[seed-sp4]\033[0m %s\n" "$*"; }
warn() { printf "\033[1;33m[seed-sp4][WARN]\033[0m %s\n" "$*" >&2; }
fail() { printf "\033[1;31m[seed-sp4][FAIL]\033[0m %s\n" "$*" >&2; exit 1; }
ok()   { printf "\033[1;32m[seed-sp4][OK]\033[0m %s\n" "$*"; }
skip() { printf "\033[2;37m[seed-sp4][SKIP]\033[0m %s\n" "$*"; }

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || fail "missing dependency: $1 (install via your package manager)"
}

# -----------------------------------------------------------------------------
# Paths + configuration
# -----------------------------------------------------------------------------
SCRIPT_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SEED_DIR="$(cd "$SCRIPT_LIB_DIR/.." && pwd)"
REPO_ROOT="$(cd "$SEED_DIR/../../.." && pwd)"

DATA_FILE="$SEED_DIR/data.json"
RULEBOOK_DIR="$REPO_ROOT/data/rulebook"
SECRETS_DIR="$REPO_ROOT/infra/secrets"

# -----------------------------------------------------------------------------
# Target selection (local vs staging)
# -----------------------------------------------------------------------------
# Required env: TARGET=local|staging (default local)
TARGET="${TARGET:-local}"
case "$TARGET" in
  local)   API_BASE="http://localhost:8080/api/v1" ;;
  staging) API_BASE="https://meepleai.app/api/v1" ;;
  *) fail "TARGET must be local|staging (got: $TARGET)" ;;
esac

# Cookie jar root keyed by target so local + staging don't collide.
COOKIE_DIR="/tmp/meepleai-sp4-${TARGET}-cookies"
mkdir -p "$COOKIE_DIR"

# State file (per target) — slug → guid map persisted as JSON.
# Each step appends; subsequent steps look up via state_get.
STATE_FILE="${STATE_FILE:-/tmp/meepleai-sp4-${TARGET}-state.json}"
if [[ ! -f "$STATE_FILE" ]]; then
  echo '{}' > "$STATE_FILE"
fi

# -----------------------------------------------------------------------------
# HTTP helpers
# -----------------------------------------------------------------------------
# http_check expected_code actual_code response_body description
# Fails with the body printed if codes mismatch. Tolerates a list of expected
# codes separated by '|' (e.g. "200|201|204").
http_check() {
  local expected="$1" actual="$2" body="$3" desc="$4"
  if ! grep -qE "(^|\|)${actual}(\$|\|)" <<< "$expected"; then
    warn "[$desc] HTTP $actual (expected $expected)"
    echo "Response body:" >&2
    echo "$body" | head -50 >&2
    return 1
  fi
}

# curl_json METHOD PATH COOKIE_JAR JSON_BODY
# Returns "BODY\nHTTP_CODE" on stdout; caller splits.
curl_json() {
  local method="$1" path="$2" jar="$3" data="${4:-}"
  local args=(-sS -X "$method" -H "Content-Type: application/json" -b "$jar" -c "$jar"
              -w "\n%{http_code}" "$API_BASE$path")
  if [[ -n "$data" ]]; then args+=(-d "$data"); fi
  curl "${args[@]}"
}

# curl_get PATH COOKIE_JAR  →  body + code
curl_get() {
  local path="$1" jar="$2"
  curl -sS -X GET -b "$jar" -c "$jar" -w "\n%{http_code}" "$API_BASE$path"
}

# Split "BODY\nHTTP_CODE" into BODY_OUT / CODE_OUT variables.
# Usage: split_response "$resp" body code
split_response() {
  local resp="$1" body_var="$2" code_var="$3"
  printf -v "$code_var" '%s' "$(echo "$resp" | tail -n1)"
  printf -v "$body_var" '%s' "$(echo "$resp" | sed '$d')"
}

# -----------------------------------------------------------------------------
# Auth helpers
# -----------------------------------------------------------------------------
# Cookie jar path for a given user slug. Bootstrap admin uses slug 'admin'.
cookie_jar_for() {
  echo "$COOKIE_DIR/$1.txt"
}

# Password used for all SP4 dev seed users. Kept out of data.json (secret scanners
# flag committed passwords). Reads from $SEED_SP4_PASSWORD env var; defaults to a
# dev placeholder satisfying BE validators (≥8 chars, mixed case, digit, symbol).
seed_password() {
  echo "${SEED_SP4_PASSWORD:-Sp4-Seed-Pwd!2026}" # gitguardian:ignore — dev placeholder, not a real credential
}

# Login a user; populates cookie jar. Idempotent (returns 200 even if already logged in).
# Args: email password slug
login_user() {
  local email="$1" password="$2" slug="$3"
  local jar=$(cookie_jar_for "$slug")
  local resp=$(curl_json POST "/auth/login" "$jar" \
    "$(jq -n --arg e "$email" --arg p "$password" '{email:$e, password:$p}')")
  local body code; split_response "$resp" body code
  if ! http_check "200" "$code" "$body" "login $slug"; then
    return 1
  fi
  ok "Logged in as $slug ($email)"
}

# Bootstrap admin login.
# Reads ADMIN_EMAIL / ADMIN_PASSWORD (or INITIAL_ADMIN_EMAIL fallback) from
# $SECRETS_DIR/admin.secret. Required first step before user/game creation.
admin_login() {
  local secret_file="$SECRETS_DIR/admin.secret"
  [[ -f "$secret_file" ]] || fail "missing $secret_file — bootstrap admin credentials"

  local admin_email admin_pw
  # Email: ADMIN_EMAIL preferred, INITIAL_ADMIN_EMAIL fallback (both present in repo template)
  admin_email=$(grep -E '^ADMIN_EMAIL=' "$secret_file"        | head -1 | cut -d= -f2- | tr -d '"' | tr -d "'" | tr -d '\r')
  [[ -n "$admin_email" ]] || admin_email=$(grep -E '^INITIAL_ADMIN_EMAIL=' "$secret_file" | head -1 | cut -d= -f2- | tr -d '"' | tr -d "'" | tr -d '\r')
  # Password: ADMIN_PASSWORD canonical (validated by Program.cs); no INITIAL_ADMIN_PASSWORD exists.
  admin_pw=$(grep -E '^ADMIN_PASSWORD=' "$secret_file"        | head -1 | cut -d= -f2- | tr -d '"' | tr -d "'" | tr -d '\r')

  [[ -n "$admin_email" ]] || fail "ADMIN_EMAIL (or INITIAL_ADMIN_EMAIL) missing in $secret_file"
  [[ -n "$admin_pw"    ]] || fail "ADMIN_PASSWORD missing in $secret_file"

  login_user "$admin_email" "$admin_pw" "admin"
}

# -----------------------------------------------------------------------------
# State file (slug → guid map, persisted across steps)
# -----------------------------------------------------------------------------
# state_set "$kind" "$slug" "$guid"
# kind ∈ users | games | kbDocs | agents | toolkits | sessions | events | chats
state_set() {
  local kind="$1" slug="$2" guid="$3"
  local tmp=$(mktemp)
  jq --arg k "$kind" --arg s "$slug" --arg g "$guid" \
    '.[$k] //= {} | .[$k][$s] = $g' "$STATE_FILE" > "$tmp"
  mv "$tmp" "$STATE_FILE"
}

# state_get "$kind" "$slug" → guid (empty if not found)
state_get() {
  local kind="$1" slug="$2"
  jq -r --arg k "$kind" --arg s "$slug" '.[$k][$s] // ""' "$STATE_FILE"
}

# state_clear "$kind"  — remove entire kind (used by reset)
state_clear() {
  local kind="$1"
  local tmp=$(mktemp)
  jq --arg k "$kind" 'del(.[$k])' "$STATE_FILE" > "$tmp"
  mv "$tmp" "$STATE_FILE"
}

# -----------------------------------------------------------------------------
# Dataset accessors (jq over data.json)
# -----------------------------------------------------------------------------
# data_get '.users[] | .slug'  → newline-separated values
# Strip CR — on Git Bash mingw, jq emits CRLF on stdout, which corrupts `read -r` loops
# (trailing \r becomes part of the var, breaking file paths / jq --arg matches).
data_get() {
  jq -r "$1" "$DATA_FILE" | tr -d '\r'
}

# data_get_compact '.users[]'  → newline-separated compact JSON objects
data_get_compact() {
  jq -c "$1" "$DATA_FILE" | tr -d '\r'
}

# -----------------------------------------------------------------------------
# Polling (used by KB upload step)
# -----------------------------------------------------------------------------
# poll_until COND_FN MAX_SEC INTERVAL_SEC DESCRIPTION
# COND_FN is a function name returning 0 (done) or 1 (keep polling).
poll_until() {
  local cond_fn="$1" max="$2" interval="$3" desc="$4"
  local elapsed=0
  while [[ $elapsed -lt $max ]]; do
    if "$cond_fn"; then
      ok "[poll] $desc done after ${elapsed}s"
      return 0
    fi
    sleep "$interval"
    elapsed=$((elapsed + interval))
    log "[poll] $desc ... ${elapsed}/${max}s"
  done
  warn "[poll] $desc TIMEOUT after ${max}s"
  return 1
}

# -----------------------------------------------------------------------------
# Banner (used by step scripts on entry)
# -----------------------------------------------------------------------------
banner() {
  printf "\n\033[1;35m═══ %s ═══\033[0m\n" "$*"
}

# -----------------------------------------------------------------------------
# Sanity checks (early-fail if env not ready)
# -----------------------------------------------------------------------------
require_cmd jq
require_cmd curl

if [[ "${SEED_SP4_SKIP_HEALTH:-0}" != "1" ]]; then
  # Quick liveness probe of API
  if ! curl -sS -m 5 "$API_BASE/health" >/dev/null 2>&1; then
    warn "API at $API_BASE not responding to /health (may still work for some endpoints)"
  fi
fi

# Export common vars for step scripts
export API_BASE COOKIE_DIR STATE_FILE DATA_FILE RULEBOOK_DIR REPO_ROOT
