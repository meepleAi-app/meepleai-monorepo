#!/usr/bin/env bash
# Rebucket script for S3/R2: copies pdfs/{gameId}/{pdfId}.pdf -> pdfs/{pdfId}/{pdfId}.pdf
# Usage: BUCKET=my-bucket ./scripts/rebucket-pdfs-s3.sh [--dry-run]

set -euo pipefail
DRY_RUN="${1:-}"

docker exec meepleai-postgres psql -U meepleai -d meepleai -t -A -F "," \
  -c 'SELECT "Id"::text, COALESCE("PrivateGameId"::text, "SharedGameId"::text) FROM pdf_documents WHERE "FilePath" IS NOT NULL;' \
  | while IFS=, read -r pdfId gameId; do
    [[ -z "$gameId" ]] && continue
    pdfIdN="${pdfId//-/}"
    gameIdN="${gameId//-/}"
    src="s3://${BUCKET}/pdfs/${gameIdN}/${pdfIdN}.pdf"
    dst="s3://${BUCKET}/pdfs/${pdfIdN}/${pdfIdN}.pdf"

    if aws s3 ls "$dst" >/dev/null 2>&1; then continue; fi
    if ! aws s3 ls "$src" >/dev/null 2>&1; then continue; fi

    if [[ "$DRY_RUN" == "--dry-run" ]]; then
        echo "[DRY] $src -> $dst"
    else
        aws s3 cp "$src" "$dst" --only-show-errors
    fi
done

echo "Rebucket complete. Verify then run cleanup (s3 rm) separately."
