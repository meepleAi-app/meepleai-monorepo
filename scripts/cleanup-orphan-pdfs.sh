#!/usr/bin/env bash
# Cleanup orphan PDF prefixes on S3/R2 (issue #520 Opt. C).
#
# Moves top-level prefixes under pdf_uploads/ that do NOT match any
# pdf_documents.Id, PrivateGameId, or SharedGameId in the DB to a dated
# _trash/ prefix (reversible move, NOT hard delete). Wait the grace period
# documented in the runbook before permanent purge.
#
# Layout (matches S3BlobStorageService.cs:271 post-PR #517):
#   pdf_uploads/{gameId}/{fileId}_{filename}
#     gameId can be PrivateGameId OR SharedGameId
#
# Trash output:
#   _trash/YYYY-MM-DD/pdf_uploads/{orphanPrefix}/
#   Restore: aws s3 mv s3://<bucket>/_trash/<date>/pdf_uploads/<prefix>/ \
#                       s3://<bucket>/pdf_uploads/<prefix>/ --recursive
#   Purge after grace period (default 7 days; see runbook).
#
# Default mode is dry-run (no S3 writes). Use --apply to execute.
#
# Usage:
#   AWS_ACCESS_KEY_ID=... AWS_SECRET_ACCESS_KEY=... \
#   S3_ENDPOINT=https://<acct>.r2.cloudflarestorage.com \
#   BUCKET=meepleai-uploads \
#   PG_DSN='postgres://user:pass@host:5432/meepleai_staging' \
#   ./scripts/cleanup-orphan-pdfs.sh [--apply] [--manifest PATH] \
#                                    [--prefix pdf_uploads/] [--trash-root _trash/]
#
# Exit codes:
#   0 → no orphans found OR apply succeeded (full)
#   1 → missing env / config error
#   2 → orphans listed in dry-run (no S3 writes performed)
#   3 → AWS or DB error during execution

set -euo pipefail

APPLY=0
PREFIX="${PREFIX:-pdf_uploads/}"
TRASH_ROOT="${TRASH_ROOT:-_trash/}"
MANIFEST_PATH=""
DATE_TAG="$(date -u +%Y-%m-%d)"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --apply) APPLY=1; shift ;;
        --manifest)
            [[ $# -lt 2 ]] && { echo "--manifest needs a path" >&2; exit 1; }
            MANIFEST_PATH="$2"; shift 2 ;;
        --prefix)
            [[ $# -lt 2 ]] && { echo "--prefix needs a value" >&2; exit 1; }
            PREFIX="$2"; shift 2 ;;
        --trash-root)
            [[ $# -lt 2 ]] && { echo "--trash-root needs a value" >&2; exit 1; }
            TRASH_ROOT="$2"; shift 2 ;;
        -h|--help)
            sed -n '1,40p' "$0"; exit 0 ;;
        *)
            echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

# Normalize trailing slashes
PREFIX="${PREFIX%/}/"
TRASH_ROOT="${TRASH_ROOT%/}/"

# Required env
BUCKET="${BUCKET:?BUCKET env var required}"
PG_DSN="${PG_DSN:?PG_DSN env var required}"
ENDPOINT="${S3_ENDPOINT:-}"

AWS_ARGS=()
[[ -n "$ENDPOINT" ]] && AWS_ARGS+=(--endpoint-url "$ENDPOINT")

MODE_LABEL="DRY-RUN"
[[ $APPLY -eq 1 ]] && MODE_LABEL="APPLY"

echo "=== cleanup-orphan-pdfs ($MODE_LABEL) ==="
echo "  bucket=$BUCKET prefix=$PREFIX trash=$TRASH_ROOT$DATE_TAG/"
echo

# 1. Fetch DB sets (pdfIds + gameIds)
echo "[1/4] Querying DB for pdf_documents…"
DB_DUMP=$(
    psql "$PG_DSN" -t -A -F '|' <<'SQL'
SELECT
    REPLACE("Id"::text, '-', ''),
    REPLACE(COALESCE("PrivateGameId"::text, "SharedGameId"::text, ''), '-', '')
FROM pdf_documents
WHERE "FilePath" IS NOT NULL;
SQL
) || { echo "psql query failed" >&2; exit 3; }

declare -A PDF_IDS GAME_IDS
while IFS='|' read -r pid gid; do
    [[ -z "$pid" ]] && continue
    PDF_IDS["$pid"]=1
    [[ -n "$gid" ]] && GAME_IDS["$gid"]=1
done <<< "$DB_DUMP"

echo "  pdf_documents rows: ${#PDF_IDS[@]}"
echo "  distinct game ids:  ${#GAME_IDS[@]}"
echo

# 2. List S3 top-level prefixes
echo "[2/4] Listing S3 top-level prefixes under $PREFIX…"
mapfile -t R2_IDS < <(
    aws "${AWS_ARGS[@]}" s3 ls "s3://${BUCKET}/${PREFIX}" \
        | awk '/^[[:space:]]+PRE /{print $2}' \
        | sed 's|/$||'
) || { echo "aws s3 ls failed" >&2; exit 3; }

echo "  Found ${#R2_IDS[@]} top-level prefixes"
echo

# 3. Classify
echo "[3/4] Classifying…"
ORPHANS=()
for id in "${R2_IDS[@]}"; do
    idN="${id//-/}"
    if [[ -n "${PDF_IDS[$idN]:-}" ]]; then
        :
    elif [[ -n "${GAME_IDS[$idN]:-}" ]]; then
        :
    else
        ORPHANS+=("$id")
    fi
done

orphan_count="${#ORPHANS[@]}"
echo "  Orphan prefixes: $orphan_count"
echo

if [[ $orphan_count -eq 0 ]]; then
    echo "Nothing to do — storage is clean."
    exit 0
fi

# 4. Move (or dry-run)
echo "[4/4] $MODE_LABEL — processing $orphan_count orphan prefix(es)…"

# Manifest header
MANIFEST_LINES=()
MANIFEST_LINES+=("# cleanup-orphan-pdfs manifest")
MANIFEST_LINES+=("# bucket=$BUCKET prefix=$PREFIX trash=$TRASH_ROOT$DATE_TAG/")
MANIFEST_LINES+=("# mode=$MODE_LABEL utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)")
MANIFEST_LINES+=("prefix	object_count	total_size_bytes	status")

total_objects=0
total_size=0
failures=0

for prefix in "${ORPHANS[@]}"; do
    src="s3://${BUCKET}/${PREFIX}${prefix}/"
    dst="s3://${BUCKET}/${TRASH_ROOT}${DATE_TAG}/${PREFIX}${prefix}/"

    # Discover size + object count for the prefix
    summary=$(aws "${AWS_ARGS[@]}" s3 ls "$src" --recursive --summarize 2>/dev/null \
        | tail -n 2 || echo "")
    obj_count=$(echo "$summary" | awk '/Total Objects:/{print $3}')
    obj_count="${obj_count:-0}"
    size_bytes=$(echo "$summary" | awk '/Total Size:/{print $3}')
    size_bytes="${size_bytes:-0}"

    total_objects=$((total_objects + obj_count))
    total_size=$((total_size + size_bytes))

    if [[ $APPLY -eq 1 ]]; then
        if aws "${AWS_ARGS[@]}" s3 mv "$src" "$dst" --recursive --only-show-errors; then
            status="moved"
        else
            status="ERROR"
            failures=$((failures + 1))
        fi
    else
        echo "  [DRY] mv $src -> $dst  ($obj_count objects, $size_bytes bytes)"
        status="dry-run"
    fi

    MANIFEST_LINES+=("$prefix	$obj_count	$size_bytes	$status")
done

echo
echo "=== Summary ==="
echo "  orphan prefixes: $orphan_count"
echo "  total objects:   $total_objects"
echo "  total size:      $total_size bytes"
[[ $failures -gt 0 ]] && echo "  failures:        $failures"
echo

# Write manifest
if [[ -n "$MANIFEST_PATH" ]]; then
    printf '%s\n' "${MANIFEST_LINES[@]}" > "$MANIFEST_PATH"
    echo "Manifest written: $MANIFEST_PATH"
fi

if [[ $APPLY -eq 0 ]]; then
    echo
    echo "DRY-RUN complete. Re-run with --apply to move orphans to $TRASH_ROOT$DATE_TAG/."
    exit 2
fi

if [[ $failures -gt 0 ]]; then
    echo "APPLY completed with $failures failure(s); inspect logs." >&2
    exit 3
fi

echo "APPLY OK — $orphan_count prefix(es) moved to $TRASH_ROOT$DATE_TAG/."
echo "Restore: aws s3 mv s3://$BUCKET/$TRASH_ROOT$DATE_TAG/$PREFIX<prefix>/ \\"
echo "             s3://$BUCKET/$PREFIX<prefix>/ --recursive"
echo "Purge after grace window — see docs/for-developers/operations/2026-05-19-r2-orphan-cleanup-runbook.md"
exit 0
