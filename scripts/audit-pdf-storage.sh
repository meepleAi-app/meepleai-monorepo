#!/usr/bin/env bash
# Audit R2/S3 PDF storage layout. Classifies top-level prefixes under
# pdf_uploads/ as pdfId-matched, gameId-matched, or unknown (orphan).
#
# Layouts:
#   Current (S3BlobStorageService.cs:271 post-PR #517):
#     pdf_uploads/{gameId}/{fileId}_{filename}
#     where gameId == PrivateGameId OR SharedGameId, fileId == pdf_documents.Id
#
# Decision matrix:
#   - matched_pdf  = top-level prefix found in pdf_documents.Id        → ignore
#   - matched_game = top-level prefix found in {PrivateGameId,SharedGameId} → live
#   - unknown      = neither                                           → orphan (cleanup candidate)
#
# Usage:
#   AWS_ACCESS_KEY_ID=... AWS_SECRET_ACCESS_KEY=... \
#   S3_ENDPOINT=https://<acct>.r2.cloudflarestorage.com \
#   BUCKET=meepleai-uploads \
#   [PG_DSN='postgres://user:pass@host:5432/meepleai_staging'] \
#   ./scripts/audit-pdf-storage.sh
#
# Override PREFIX (default pdf_uploads/) if storage layout changes in future
# (see issue #520 deferred refactor for {gameId} → {pdfId} epic).

set -euo pipefail

BUCKET="${BUCKET:?BUCKET env var required}"
ENDPOINT="${S3_ENDPOINT:-}"
PREFIX="${PREFIX:-pdf_uploads/}"

AWS_ARGS=()
[[ -n "$ENDPOINT" ]] && AWS_ARGS+=(--endpoint-url "$ENDPOINT")

echo "=== R2/S3 audit — bucket=$BUCKET prefix=$PREFIX ==="
echo

echo "[1/3] Top-level prefixes under $PREFIX:"
# Delimiter '/' lists only CommonPrefixes (no recursion). Output is
# "PRE <prefix>/" lines; strip prefix+trailing slash.
mapfile -t R2_IDS < <(
    aws "${AWS_ARGS[@]}" s3 ls "s3://${BUCKET}/${PREFIX}" \
        | awk '/^[[:space:]]+PRE /{print $2}' \
        | sed 's|/$||'
)

count="${#R2_IDS[@]}"
echo "  Found $count entries"
printf '  %s\n' "${R2_IDS[@]:0:10}"
[[ $count -gt 10 ]] && echo "  … ($((count - 10)) more)"
echo

if [[ -z "${PG_DSN:-}" ]]; then
    echo "[2/3] Skipped (PG_DSN not set) — manual cross-check required."
    echo "[3/3] Skipped."
    echo
    echo "To complete audit, export PG_DSN pointing at the matching environment"
    echo "and rerun this script."
    exit 0
fi

# Pull pdfId and gameId sets from DB in one round-trip.
echo "[2/3] Querying DB for pdf_documents ids + game ids…"
DB_DUMP=$(
    psql "$PG_DSN" -t -A -F '|' <<'SQL'
SELECT
    REPLACE("Id"::text, '-', ''),
    REPLACE(COALESCE("PrivateGameId"::text, "SharedGameId"::text, ''), '-', '')
FROM pdf_documents
WHERE "FilePath" IS NOT NULL;
SQL
)

declare -A PDF_IDS GAME_IDS
while IFS='|' read -r pid gid; do
    [[ -z "$pid" ]] && continue
    PDF_IDS["$pid"]=1
    [[ -n "$gid" ]] && GAME_IDS["$gid"]=1
done <<< "$DB_DUMP"

echo "  pdf_documents rows: ${#PDF_IDS[@]}"
echo "  distinct game ids:  ${#GAME_IDS[@]}"
echo

echo "[3/3] Classifying R2 prefixes:"
matched_pdf=0
matched_game=0
unknown=0
for id in "${R2_IDS[@]}"; do
    idN="${id//-/}"
    if [[ -n "${PDF_IDS[$idN]:-}" ]]; then
        ((matched_pdf++))
    elif [[ -n "${GAME_IDS[$idN]:-}" ]]; then
        ((matched_game++))
        echo "  LEGACY gameId prefix: $id"
    else
        ((unknown++))
        echo "  UNKNOWN prefix: $id"
    fi
done

echo
echo "=== Summary ==="
echo "  current-layout (pdfId):  $matched_pdf"
echo "  legacy-layout (gameId):  $matched_game"
echo "  unknown:                 $unknown"
echo

if [[ $unknown -eq 0 ]]; then
    echo "DECISION: storage clean — zero orphan prefixes under $PREFIX."
    exit 0
elif [[ $unknown -gt 0 ]]; then
    echo "DECISION: orphan cleanup AVAILABLE — $unknown prefixes have no matching pdf_documents row."
    echo "         Dry-run: BUCKET=$BUCKET PG_DSN=... ./scripts/cleanup-orphan-pdfs.sh"
    echo "         Apply:   BUCKET=$BUCKET PG_DSN=... ./scripts/cleanup-orphan-pdfs.sh --apply"
    exit 2
else
    echo "DECISION: INVESTIGATE — unexpected classification state."
    exit 3
fi
