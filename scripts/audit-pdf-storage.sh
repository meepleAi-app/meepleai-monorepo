#!/usr/bin/env bash
# Audit R2/S3 PDF storage layout to determine whether rebucket-pdfs-s3.sh is required.
#
# Layouts:
#   Legacy (pre 2026-04-19):  pdfs/{gameId}/{pdfId}.pdf
#   Current (post-migration): pdfs/{pdfId}/{pdfId}.pdf      (uniform — gameId == pdfId)
#
# Decision:
#   - If every top-level prefix under pdfs/ is a pdfId present in pdf_documents.Id
#     → layout is current → rebucket N/A
#   - If any top-level prefix matches a gameId (PrivateGameId/SharedGameId) but not
#     a pdfId → legacy entries exist → rebucket required
#
# Usage:
#   AWS_ACCESS_KEY_ID=... AWS_SECRET_ACCESS_KEY=... \
#   S3_ENDPOINT=https://<acct>.r2.cloudflarestorage.com \
#   BUCKET=meepleai-uploads \
#   [PG_DSN='postgres://user:pass@host:5432/meepleai_staging'] \
#   ./scripts/audit-pdf-storage.sh
#
# If PG_DSN is omitted the script lists R2 prefixes and lets the operator
# cross-reference manually.

set -euo pipefail

BUCKET="${BUCKET:?BUCKET env var required}"
ENDPOINT="${S3_ENDPOINT:-}"
PREFIX="${PREFIX:-pdfs/}"

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

if [[ $matched_game -eq 0 && $unknown -eq 0 ]]; then
    echo "DECISION: rebucket N/A — all R2 prefixes match current pdfId layout."
    exit 0
elif [[ $matched_game -gt 0 ]]; then
    echo "DECISION: rebucket REQUIRED — $matched_game legacy gameId prefixes found."
    echo "         Run: BUCKET=$BUCKET ./scripts/rebucket-pdfs-s3.sh --dry-run"
    exit 2
else
    echo "DECISION: INVESTIGATE — $unknown prefixes match neither pdfId nor gameId."
    exit 3
fi
