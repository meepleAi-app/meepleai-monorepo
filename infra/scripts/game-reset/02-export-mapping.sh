#!/usr/bin/env bash
# Phase 1 / Step 1 of spec: export mapping CSV bridging old game IDs → PDF identity.
# Produces:
#   - <date>-<env>-game-mapping.csv (one row per game-pdf pair, UNION over 3 sources)
#
# Pre-condition: 01-backup.sh has been run (so we have a recovery point).
#
# Usage: ./02-export-mapping.sh <env-file> [--i-mean-it]

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

load_env "${1:-}"
guard_prod "${2:-}"

csv_file=$(artefact_path "game-mapping" "csv")
sanity_file=$(artefact_path "game-mapping-sanity" "txt")

# Verify pre-flight backup was run today for this env (presence of dump file)
expected_dump=$(artefact_path "pre-game-reset" "dump")
if [[ ! -f "$expected_dump" ]]; then
  log_error "No backup dump found for today/$ENV_NAME. Run 01-backup.sh first."
  log_error "Expected: $expected_dump"
  exit 72  # EX_OSFILE
fi
log_ok "Pre-flight backup present: $expected_dump"

if [[ -f "$csv_file" ]]; then
  log_error "Refusing to overwrite existing CSV: $csv_file"
  exit 73
fi

log_info "Running pre-export sanity check (vectors without re-link source)..."
# NB: vector_documents uses PascalCase columns ("GameId", "Id", "PdfDocumentId") from EF defaults;
# newer columns added by migrations (shared_game_id) are snake_case. Quote PascalCase identifiers.
orphans=$(psql --dbname="$DATABASE_URL" -t -A -c "
  SELECT COUNT(DISTINCT pe.game_id)
  FROM pgvector_embeddings pe
  WHERE pe.game_id NOT IN (
    SELECT vd.\"GameId\" FROM vector_documents vd WHERE vd.\"GameId\" IS NOT NULL
    UNION
    SELECT vd.shared_game_id FROM vector_documents vd WHERE vd.shared_game_id IS NOT NULL
  );
")

echo "orphan_game_ids_in_vectors=$orphans" > "$sanity_file"
if [[ "$orphans" -gt 0 ]]; then
  log_warn "$orphans distinct game_id values in pgvector_embeddings cannot be re-linked via vector_documents."
  log_warn "They will be captured under 'orphan' source_kind in the CSV."
fi
log_ok "Sanity check recorded: $sanity_file"

log_info "Exporting mapping CSV → $csv_file"
# Schema notes (real DB schema, mixed casing):
#   games:            "Id", "Name", "BggId", "SharedGameId" (PascalCase)
#   shared_games:     id, title, bgg_id (snake_case)
#   vector_documents: "Id", "GameId", "PdfDocumentId", "Metadata" (PascalCase) + shared_game_id (snake_case)
#   pdf_documents:    "Id", "FileName", "FilePath", "Metadata" (PascalCase)
#   pgvector_embeddings: all snake_case
psql --dbname="$DATABASE_URL" -c "\copy (
  SELECT
    'game'                       AS source_kind,
    vd.\"GameId\"                AS old_game_id,
    g.\"Name\"                   AS game_title,
    g.\"BggId\"                  AS bgg_id,
    vd.\"Id\"                    AS vector_document_id,
    vd.\"PdfDocumentId\"         AS pdf_document_id,
    pd.\"FileName\"              AS pdf_filename,
    pd.\"FilePath\"              AS pdf_path,
    pd.\"Metadata\"::jsonb->>'sha256' AS pdf_sha256
  FROM vector_documents vd
  LEFT JOIN games g          ON g.\"Id\" = vd.\"GameId\"
  LEFT JOIN pdf_documents pd ON pd.\"Id\" = vd.\"PdfDocumentId\"
  WHERE vd.\"GameId\" IS NOT NULL

  UNION ALL

  SELECT
    'shared'                     AS source_kind,
    vd.shared_game_id            AS old_game_id,
    sg.title                     AS game_title,
    sg.bgg_id                    AS bgg_id,
    vd.\"Id\"                    AS vector_document_id,
    vd.\"PdfDocumentId\"         AS pdf_document_id,
    pd.\"FileName\"              AS pdf_filename,
    pd.\"FilePath\"              AS pdf_path,
    pd.\"Metadata\"::jsonb->>'sha256' AS pdf_sha256
  FROM vector_documents vd
  LEFT JOIN shared_games sg  ON sg.id = vd.shared_game_id
  LEFT JOIN pdf_documents pd ON pd.\"Id\" = vd.\"PdfDocumentId\"
  WHERE vd.shared_game_id IS NOT NULL

  UNION ALL

  SELECT
    'orphan'                     AS source_kind,
    pe.game_id                   AS old_game_id,
    NULL                         AS game_title,
    NULL                         AS bgg_id,
    NULL                         AS vector_document_id,
    NULL                         AS pdf_document_id,
    NULL, NULL, NULL
  FROM pgvector_embeddings pe
  LEFT JOIN vector_documents vd ON vd.\"GameId\" = pe.game_id OR vd.shared_game_id = pe.game_id
  WHERE vd.\"Id\" IS NULL
  GROUP BY pe.game_id
) TO STDOUT WITH CSV HEADER" > "$csv_file"

rows=$(wc -l < "$csv_file")
log_ok "CSV exported: $csv_file ($rows lines including header)"
