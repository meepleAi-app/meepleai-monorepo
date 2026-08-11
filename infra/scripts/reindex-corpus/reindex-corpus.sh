#!/usr/bin/env bash
# infra/scripts/reindex-corpus/reindex-corpus.sh
# SP3 (#3269) Slice 3 / D4 — per-env big-bang corpus re-index ORCHESTRATION.
#
# Wraps the admin endpoint `POST /api/v1/admin/queue/reindex-ready` (Slice 1, #3269)
# in a drain loop. That endpoint enqueues Ready PDFs whose IndexerVersion differs from
# the target (coordinate-aware v1.2) but is capped by the processing queue (MaxQueueSize),
# so a large corpus needs REPEATED calls. This script loops until EnqueuedCount == 0
# (corpus drained), pacing between iterations so the Quartz rail can process the queue
# before the next batch is enqueued.
#
# The selector skips already-current docs (IndexerVersion == target), so the loop is
# idempotent / resumable — a crash mid-run is safe to re-run: already-re-indexed PDFs
# are not touched again.
#
# Usage:
#   ./reindex-corpus.sh <path-to-.env.ENV> [--dry-run] [--i-mean-it]
#   make reindex-corpus ENV=staging EXTRA=--dry-run          (from infra/)
#   make reindex-corpus ENV=prod IMEANIT=--i-mean-it         (from infra/)
#
# Exit codes: 0 = clean drain (or dry-run); 64 = usage / prod-guard refusal;
#   65 = auth or HTTP error; 66 = env file missing; 69 = missing jq/curl;
#   75 = MAX_ITERATIONS exceeded (stuck queue).
#
# Runbook: docs/for-developers/operations/2026-07-26-corpus-reindex-runbook.md
# ADR:     docs/for-claude/architecture/adr/adr-086-corpus-reindex-orchestration.md
#
# NOTE: no `set -e` — curl/http failures are handled explicitly so the loop can report
# a clear diagnosis instead of dying on a transient non-zero exit.
set -uo pipefail

# --- logging (mirrors infra/scripts/game-reset/lib/common.sh) ---
log_info()  { printf '\033[0;36m[INFO]\033[0m  %s\n' "$*" >&2; }
log_warn()  { printf '\033[1;33m[WARN]\033[0m  %s\n' "$*" >&2; }
log_error() { printf '\033[0;31m[ERROR]\033[0m %s\n' "$*" >&2; }
log_ok()    { printf '\033[0;32m[OK]\033[0m    %s\n' "$*" >&2; }

# --- args: $1 = env file (required); then --dry-run / --i-mean-it in any order ---
ENV_FILE="${1:-}"
if [ -z "$ENV_FILE" ]; then
  log_error "Usage: $0 <path-to-.env.ENV> [--dry-run] [--i-mean-it]"
  log_error "Example: $0 scripts/reindex-corpus/.env.staging --dry-run"
  exit 64  # EX_USAGE
fi
shift

DRY_RUN=false
I_MEAN_IT=false
for arg in "$@"; do
  case "$arg" in
    --dry-run)   DRY_RUN=true ;;
    --i-mean-it) I_MEAN_IT=true ;;
    "")          : ;;  # tolerate empty EXTRA=/IMEANIT= passthrough from make
    *) log_error "Unknown argument: $arg"; exit 64 ;;
  esac
done

# --- load + validate env ---
if [ ! -f "$ENV_FILE" ]; then
  log_error "Env file not found: $ENV_FILE (copy from scripts/reindex-corpus/.env.example)"
  exit 66  # EX_NOINPUT
fi
# shellcheck disable=SC1090
. "$ENV_FILE"

: "${ENV_NAME:?ENV_NAME must be set in $ENV_FILE}"
: "${API_BASE_URL:?API_BASE_URL must be set in $ENV_FILE}"
: "${ADMIN_EMAIL:?ADMIN_EMAIL must be set in $ENV_FILE}"
: "${ADMIN_PASSWORD:?ADMIN_PASSWORD must be set in $ENV_FILE}"
TARGET_VERSION="${TARGET_VERSION:-}"      # empty → server resolves Current (v1.2)
PACING_SECONDS="${PACING_SECONDS:-5}"
MAX_ITERATIONS="${MAX_ITERATIONS:-200}"

log_info "Loaded env: ENV_NAME=$ENV_NAME API_BASE_URL=$API_BASE_URL"

# --- prod guard (mirrors game-reset guard_prod) ---
if [ "$ENV_NAME" = "prod" ]; then
  if [ "$I_MEAN_IT" != true ]; then
    log_error "ENV_NAME=prod detected. Refusing to run a big-bang re-index without --i-mean-it."
    log_error "Re-run as: make reindex-corpus ENV=prod IMEANIT=--i-mean-it"
    exit 64  # EX_USAGE — deliberate hard stop on prod without the flag
  fi
  log_warn "Big-bang re-index against PROD with --i-mean-it. Ctrl-C now to abort (sleeping 5s)..."
  sleep 5
fi

# --- deps ---
command -v jq   >/dev/null 2>&1 || { log_error "jq is required";   exit 69; }  # EX_UNAVAILABLE
command -v curl >/dev/null 2>&1 || { log_error "curl is required"; exit 69; }

REINDEX_ENDPOINT="$API_BASE_URL/api/v1/admin/queue/reindex-ready"
LOGIN_ENDPOINT="$API_BASE_URL/api/v1/auth/login"

if [ -n "$TARGET_VERSION" ]; then
  BODY=$(jq -n --arg v "$TARGET_VERSION" '{targetVersion:$v}')
else
  BODY='{}'   # omit targetVersion → server uses Current (coordinate-aware v1.2)
fi

COOKIE=$(mktemp)
trap 'rm -f "$COOKIE"' EXIT

# --- login (cookie auth, mirrors rag-smoke-assert.sh) ---
login() {
  local code
  code=$(curl -s -o /dev/null -w '%{http_code}' -c "$COOKIE" -X POST "$LOGIN_ENDPOINT" \
    -H 'Content-Type: application/json' \
    -d "$(jq -n --arg e "$ADMIN_EMAIL" --arg p "$ADMIN_PASSWORD" '{email:$e, password:$p}')") || true
  if [ "$code" != "200" ]; then
    log_error "Admin login failed (HTTP ${code:-000}) for $ADMIN_EMAIL at $LOGIN_ENDPOINT"
    exit 65
  fi
  log_ok "Admin login OK ($ADMIN_EMAIL @ $API_BASE_URL)"
}

# --- dry-run: verify auth + print the plan, but DO NOT enqueue (real side effect) ---
if [ "$DRY_RUN" = true ]; then
  login
  log_info "DRY-RUN plan:"
  log_info "  Environment : $ENV_NAME"
  log_info "  Endpoint    : POST $REINDEX_ENDPOINT"
  log_info "  Target ver  : ${TARGET_VERSION:-<server Current — coordinate-aware v1.2>}"
  log_info "  Pacing      : ${PACING_SECONDS}s between iterations"
  log_info "  Max iters   : $MAX_ITERATIONS (safety cap)"
  log_info "  Plan        : loop POST reindex-ready until EnqueuedCount==0 (corpus drained)."
  log_warn "DRY-RUN: NOT posting reindex-ready — enqueuing is a real side effect and the endpoint has no read-only mode."
  log_ok "Auth verified; dry-run complete. Re-run WITHOUT --dry-run to execute."
  exit 0
fi

# --- real run: login then drain-loop ---
login

log_info "Starting big-bang corpus re-index for ENV=$ENV_NAME (target=${TARGET_VERSION:-<server Current v1.2>})"
total_enqueued=0
iteration=0

while [ "$iteration" -lt "$MAX_ITERATIONS" ]; do
  iteration=$((iteration + 1))

  http_body=$(mktemp)
  code=$(curl -s -o "$http_body" -w '%{http_code}' -b "$COOKIE" -X POST "$REINDEX_ENDPOINT" \
    -H 'Content-Type: application/json' -d "$BODY") || true

  if [ "$code" != "200" ]; then
    log_error "iteration $iteration: reindex-ready returned HTTP ${code:-000}"
    log_error "  response: $(head -c 500 "$http_body" 2>/dev/null)"
    rm -f "$http_body"
    exit 65
  fi

  # Tolerate PascalCase (EnqueuedCount) OR camelCase (enqueuedCount) serialization.
  # jq `//` only falls through on null/false, so a genuine 0 is preserved.
  enqueued=$(jq -r '.EnqueuedCount // .enqueuedCount // empty' "$http_body")
  skipped=$(jq -r '.SkippedCount // .skippedCount // empty' "$http_body")
  errs=$(jq -r '(.Errors // .errors // []) | length' "$http_body")
  rm -f "$http_body"

  if [ -z "$enqueued" ]; then
    log_error "iteration $iteration: could not parse EnqueuedCount from reindex-ready response"
    exit 65
  fi

  log_info "iteration $iteration: enqueued=$enqueued skipped=${skipped:-?} errors=${errs:-0}"
  total_enqueued=$((total_enqueued + enqueued))

  if [ "${errs:-0}" -gt 0 ]; then
    log_warn "iteration $iteration reported ${errs} per-PDF error(s) (skipped, not aborted). Inspect the API logs for the affected job IDs."
  fi

  if [ "$enqueued" -eq 0 ]; then
    log_ok "Corpus drained: EnqueuedCount==0 after $iteration iteration(s). Total enqueued this run: $total_enqueued."
    log_info "Enqueued PDFs still drain through the Quartz rail asynchronously — verify completion via the queue status endpoint + post-reindex SQL (see runbook)."
    exit 0
  fi

  log_info "Pacing ${PACING_SECONDS}s to let the Quartz rail drain the queue before re-enqueuing the rest..."
  sleep "$PACING_SECONDS"
done

log_error "MAX_ITERATIONS ($MAX_ITERATIONS) exceeded without draining (EnqueuedCount never reached 0)."
log_error "This signals a STUCK queue: PDFs are re-enqueued but not progressing to $([ -n "$TARGET_VERSION" ] && echo "$TARGET_VERSION" || echo 'v1.2'). Check:"
log_error "  - the 'unstructured' Docker container is rebuilt with SP1 code (stale → 0 elements → no version bump)"
log_error "  - the Quartz processing rail is running and the queue is not paused"
log_error "  - raise MAX_ITERATIONS only after confirming the queue is actually progressing"
exit 75  # EX_TEMPFAIL
