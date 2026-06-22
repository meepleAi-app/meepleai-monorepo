#!/usr/bin/env bash
# infra/scripts/seed-status.sh
#
# Tier 1 D1 di Issue #2126 — Seed snapshot freshness pipeline.
#
# Reports the state of the seed PDF pre-indexed snapshot system:
#   * does a snapshot exist locally?
#   * how old is the most recent one?
#   * does its embedding model / EF migration head match the working tree?
#   * what should the developer do next (consume / re-bake / fallback)?
#
# Modes:
#   default            full report on stderr (run via `make seed-status`)
#   --brief            single line, suitable as a banner before `make dev`
#   --strict           exit 1 if the snapshot is missing or stale
#                      (used by CI workflows; D4)
#
# This script is intentionally read-only: it never starts containers, never
# pulls files. It only inspects `data/snapshots/` + the running postgres if
# present. Safe to call in any hook.
#
# Refs:
#   - docs/for-developers/specs/2026-04-10-seed-pdf-pre-indexed-design.md
#   - docs/for-developers/workflows/snapshot-seed-workflow.md
set -euo pipefail

BRIEF=false
STRICT=false
for arg in "$@"; do
  case "$arg" in
    --brief)  BRIEF=true  ;;
    --strict) STRICT=true ;;
    -h|--help)
      cat <<HELP
seed-status.sh — report on the local seed snapshot freshness.

  --brief    one-line banner mode (used by 'make dev' hook)
  --strict   exit 1 on missing / stale snapshot (used by CI)
HELP
      exit 0
      ;;
    *) echo "seed-status.sh: unknown arg: $arg" >&2; exit 2 ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
INFRA_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_DIR="$(cd "$INFRA_DIR/.." && pwd)"
SNAPSHOTS_DIR="$REPO_DIR/data/snapshots"

# ── ANSI colours (off when stderr is not a tty, NO_COLOR set, or in brief) ──
if [[ -t 2 && -z "${NO_COLOR:-}" ]]; then
  C_DIM=$'\033[2m'; C_RED=$'\033[31m'; C_YEL=$'\033[33m'
  C_GRN=$'\033[32m'; C_BLU=$'\033[34m'; C_RST=$'\033[0m'
else
  C_DIM=''; C_RED=''; C_YEL=''; C_GRN=''; C_BLU=''; C_RST=''
fi

# Thresholds — keep aligned with the spec's R-SNAP-FRESH-02 SLO.
WARNING_AGE_DAYS=7
STALE_AGE_DAYS=30

# ── Find the most recent snapshot meta sidecar ──────────────────────────────
LATEST_META=""
if [[ -d "$SNAPSHOTS_DIR" ]]; then
  # Sort by mtime (proxy for created_at), exclude baseline if a timestamped one exists
  for candidate in $(ls -t "$SNAPSHOTS_DIR"/*.meta.json 2>/dev/null); do
    if [[ "$(basename "$candidate")" != "meepleai_seed_baseline_"* ]]; then
      LATEST_META="$candidate"; break
    fi
  done
  # Fall back to baseline if nothing timestamped exists yet
  if [[ -z "$LATEST_META" ]]; then
    LATEST_META=$(ls -t "$SNAPSHOTS_DIR"/*.meta.json 2>/dev/null | head -1 || true)
  fi
fi

if [[ -z "$LATEST_META" ]]; then
  if $BRIEF; then
    printf "%s[seed-status]%s no snapshot found in %s — \`make dev\` will run runtime indexing (hours)\n" \
      "$C_DIM" "$C_RST" "$SNAPSHOTS_DIR" >&2
  else
    cat >&2 <<MSG
${C_YEL}⚠  No snapshot found in $SNAPSHOTS_DIR${C_RST}

  ${C_BLU}make seed-index${C_RST} → produce one (can take hours on CPU)
  ${C_BLU}make dev${C_RST}        → fall back to runtime indexing
MSG
  fi
  $STRICT && exit 1
  exit 0
fi

# ── Read the sidecar (jq required — preflight enforces this) ───────────────
if ! command -v jq >/dev/null 2>&1; then
  printf "%s[seed-status]%s jq not installed — cannot parse %s\n" \
    "$C_DIM" "$C_RST" "$LATEST_META" >&2
  exit 0
fi

CREATED_AT=$(jq -r '.created_at // empty' "$LATEST_META")
EF_HEAD_SNAPSHOT=$(jq -r '.ef_migration_head // empty' "$LATEST_META")
EMB_MODEL_SNAPSHOT=$(jq -r '.embedding_model // empty' "$LATEST_META")
EMB_DIM_SNAPSHOT=$(jq -r '.embedding_dim // empty' "$LATEST_META")
PDF_COUNT=$(jq -r '.pdf_count // 0' "$LATEST_META")
CHUNK_COUNT=$(jq -r '.chunk_count // 0' "$LATEST_META")
EMB_COUNT=$(jq -r '.embedding_count // 0' "$LATEST_META")

# ── Age in days ─────────────────────────────────────────────────────────────
AGE_DAYS=-1
if [[ -n "$CREATED_AT" ]]; then
  if CREATED_EPOCH=$(date -d "$CREATED_AT" +%s 2>/dev/null); then
    NOW_EPOCH=$(date +%s)
    AGE_DAYS=$(( (NOW_EPOCH - CREATED_EPOCH) / 86400 ))
  fi
fi

# ── Compare against local migration head (if postgres is running) ──────────
EF_HEAD_LOCAL=""
SCHEMA_DRIFT=false
if command -v docker >/dev/null 2>&1; then
  EF_HEAD_LOCAL=$(docker exec meepleai-postgres \
    psql -U meepleai -d meepleai_staging -tAc \
    'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1 DESC LIMIT 1;' \
    2>/dev/null | tr -d '[:space:]' || true)
  if [[ -n "$EF_HEAD_LOCAL" && -n "$EF_HEAD_SNAPSHOT" && "$EF_HEAD_LOCAL" != "$EF_HEAD_SNAPSHOT" ]]; then
    SCHEMA_DRIFT=true
  fi
fi

# ── Status determination ───────────────────────────────────────────────────
STATUS_TAG="$C_GRN" STATUS_LABEL="fresh" STATUS_HARD=false
if (( AGE_DAYS < 0 )); then
  STATUS_TAG="$C_DIM"; STATUS_LABEL="age unknown"
elif (( AGE_DAYS > STALE_AGE_DAYS )); then
  STATUS_TAG="$C_RED"; STATUS_LABEL="stale (${AGE_DAYS} d)"; STATUS_HARD=true
elif (( AGE_DAYS > WARNING_AGE_DAYS )); then
  STATUS_TAG="$C_YEL"; STATUS_LABEL="warning (${AGE_DAYS} d)"
else
  STATUS_LABEL="fresh (${AGE_DAYS} d)"
fi
if $SCHEMA_DRIFT; then
  STATUS_TAG="$C_RED"; STATUS_LABEL="stale (schema drift)"; STATUS_HARD=true
fi

# ── Brief mode ─────────────────────────────────────────────────────────────
if $BRIEF; then
  printf "%s[seed-status]%s snapshot %s%s%s · model=%s · created=%s\n" \
    "$C_DIM" "$C_RST" "$STATUS_TAG" "$STATUS_LABEL" "$C_RST" \
    "${EMB_MODEL_SNAPSHOT:-?}" "${CREATED_AT:-?}" >&2
  if $STATUS_HARD; then
    printf "%s[seed-status]%s consider %smake seed-index%s before resuming dev.\n" \
      "$C_DIM" "$C_RST" "$C_BLU" "$C_RST" >&2
  fi
  $STRICT && $STATUS_HARD && exit 1
  exit 0
fi

# ── Full report ────────────────────────────────────────────────────────────
cat >&2 <<MSG
${C_BLU}━━ Seed snapshot status ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${C_RST}

File:             ${LATEST_META}
Created:          ${CREATED_AT:-?}
Age:              ${AGE_DAYS} day(s)
Status:           ${STATUS_TAG}${STATUS_LABEL}${C_RST}

Snapshot contents:
  PDF documents:  ${PDF_COUNT}
  Text chunks:    ${CHUNK_COUNT}
  Embeddings:     ${EMB_COUNT}
  Model:          ${EMB_MODEL_SNAPSHOT:-?} (dim=${EMB_DIM_SNAPSHOT:-?})
  Migration head: ${EF_HEAD_SNAPSHOT:-?}
MSG

if [[ -n "$EF_HEAD_LOCAL" ]]; then
  if $SCHEMA_DRIFT; then
    printf "\nLocal DB migration: %sDRIFT%s (local=%s, snapshot=%s)\n" \
      "$C_RED" "$C_RST" "$EF_HEAD_LOCAL" "$EF_HEAD_SNAPSHOT" >&2
  else
    printf "\nLocal DB migration: %smatch%s (%s)\n" \
      "$C_GRN" "$C_RST" "$EF_HEAD_LOCAL" >&2
  fi
else
  printf "\nLocal DB migration: %s(postgres not reachable — skipped)%s\n" \
    "$C_DIM" "$C_RST" >&2
fi

echo "" >&2
case "$STATUS_LABEL" in
  fresh*)
    printf "%s✓%s Run %smake dev-from-snapshot%s to consume.\n" \
      "$C_GRN" "$C_RST" "$C_BLU" "$C_RST" >&2
    ;;
  warning*)
    printf "%s⚠%s %d-day-old snapshot — consider %smake seed-index%s for a fresh bake.\n" \
      "$C_YEL" "$C_RST" "$AGE_DAYS" "$C_BLU" "$C_RST" >&2
    ;;
  *)
    printf "%s✗%s Snapshot is unusable. Run %smake seed-index%s to rebake, or %smake dev%s (runtime indexing fallback).\n" \
      "$C_RED" "$C_RST" "$C_BLU" "$C_RST" "$C_BLU" "$C_RST" >&2
    ;;
esac

$STRICT && $STATUS_HARD && exit 1
exit 0
