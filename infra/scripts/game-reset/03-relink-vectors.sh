#!/usr/bin/env bash
# Phase 3 / Step 3 of spec: re-link pgvector_embeddings.game_id from legacy Game.Id → SharedGame.Id.
# MUST run BEFORE applying EF migration DropGameAggregate_Issue1320 (which drops the games table).
#
# Pre-condition: 01-backup.sh AND 02-export-mapping.sh have been run today.
# Idempotent: safe to re-run; rows already updated are skipped via `WHERE game_id != target_id`.
#
# Usage: ./03-relink-vectors.sh <env-file> [--dry-run] [--i-mean-it]

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

load_env "${1:-}"

# Optional flags (order-independent)
dry_run=false
prod_confirm=""
for arg in "${@:2}"; do
  case "$arg" in
    --dry-run) dry_run=true ;;
    --i-mean-it) prod_confirm="--i-mean-it" ;;
    *) log_error "Unknown flag: $arg"; exit 64 ;;
  esac
done
guard_prod "$prod_confirm"

# Pre-condition: backup + mapping CSV from today must exist
expected_dump=$(artefact_path "pre-game-reset" "dump")
expected_csv=$(artefact_path "game-mapping" "csv")
if [[ ! -f "$expected_dump" ]]; then
  log_error "No backup dump found: $expected_dump. Run 01-backup.sh first."
  exit 72
fi
if [[ ! -f "$expected_csv" ]]; then
  log_error "No mapping CSV found: $expected_csv. Run 02-export-mapping.sh first."
  exit 72
fi
log_ok "Pre-flight artefacts present"

# Count rows that will be affected across BOTH tables (pgvector_embeddings + vector_documents)
count_sql="
SELECT
  (SELECT COUNT(*) FROM pgvector_embeddings pe
   JOIN games g ON g.\"Id\" = pe.game_id
   WHERE g.\"SharedGameId\" IS NOT NULL
     AND pe.game_id <> g.\"SharedGameId\")
  +
  (SELECT COUNT(*) FROM vector_documents vd
   JOIN games g ON g.\"Id\" = vd.\"GameId\"
   WHERE g.\"SharedGameId\" IS NOT NULL);
"

affected=$(psql --dbname="$DATABASE_URL" -t -A -c "$count_sql")
log_info "Rows to be re-linked (pgvector_embeddings + vector_documents): $affected"

if [[ "$affected" == "0" ]]; then
  log_ok "No re-link needed (already done or no matching rows). Exiting."
  exit 0
fi

if [[ "$dry_run" == "true" ]]; then
  log_warn "DRY RUN — no changes applied."
  log_info "Would execute:"
  cat <<SQL
UPDATE pgvector_embeddings pe
SET game_id = g."SharedGameId"
FROM games g
WHERE pe.game_id = g."Id"
  AND g."SharedGameId" IS NOT NULL
  AND pe.game_id <> g."SharedGameId";
SQL
  exit 0
fi

# Real run: transactional update
log_info "Re-linking $affected vectors (transactional UPDATE)..."

psql --dbname="$DATABASE_URL" -v ON_ERROR_STOP=1 <<'SQL'
BEGIN;

-- Relink pgvector_embeddings.game_id (Game.Id -> SharedGame.Id)
UPDATE pgvector_embeddings pe
SET game_id = g."SharedGameId"
FROM games g
WHERE pe.game_id = g."Id"
  AND g."SharedGameId" IS NOT NULL
  AND pe.game_id <> g."SharedGameId";

-- Relink vector_documents: set shared_game_id from games.SharedGameId, null out legacy "GameId".
-- Necessary for Gate 5 (vector_documents migrated to shared_game_id) to PASS.
UPDATE vector_documents vd
SET shared_game_id = g."SharedGameId",
    "GameId" = NULL
FROM games g
WHERE vd."GameId" = g."Id"
  AND g."SharedGameId" IS NOT NULL;

-- Null out orphan vector_documents.GameId pointing to deleted games (data integrity cleanup).
-- These rows have stale FKs that would block Gate 5 otherwise.
UPDATE vector_documents
SET "GameId" = NULL
WHERE "GameId" IS NOT NULL
  AND "GameId" NOT IN (SELECT "Id" FROM games);

COMMIT;
SQL

# Post-relink verification: count remaining dangling
dangling=$(psql --dbname="$DATABASE_URL" -t -A -c "
SELECT COUNT(*) FROM pgvector_embeddings pe
WHERE pe.game_id NOT IN (SELECT \"Id\" FROM games)
  AND pe.game_id NOT IN (SELECT id FROM shared_games);
")
log_info "Vectors with game_id resolving to neither games nor shared_games: $dangling"

if [[ "$dangling" != "0" ]]; then
  log_warn "Post-relink: $dangling vectors still orphaned. These will be lost when games table is dropped."
  log_warn "Per spec §4 ORPHANING strategy: accept the loss OR delete these rows before drop."
  log_warn "Decision required before Step 5 (apply migration)."
fi

log_ok "Relink complete. Next step: apply EF migration to drop games table."
