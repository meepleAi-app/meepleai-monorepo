#!/usr/bin/env bash
# infra/scripts/seed-index-dump.sh
# pg_dump + sidecar .meta.json + sha256.
#
# PostgreSQL COPY cannot target generated stored columns (e.g. search_vector).
# This script auto-detects tables with generated columns and dumps them
# separately as INSERT statements (--column-inserts), which PostgreSQL
# handles correctly by omitting the generated columns.
# The main dump excludes those tables' DATA (schema is still included).
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"
mkdir -p "$OUT_DIR"

log() { echo "[dump] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

command -v jq >/dev/null || fail "jq non installato"

TS=$(date -u +%Y%m%dT%H%M%SZ)
COMMIT=$(git rev-parse --short=9 HEAD)

EMBEDDING_MODEL=$(docker exec meepleai-api printenv EMBEDDING_MODEL 2>/dev/null || echo "unknown")
EMBEDDING_DIM=$(docker exec meepleai-api printenv EMBEDDING_DIM 2>/dev/null || echo "0")
MODEL_SLUG=$(echo "$EMBEDDING_MODEL" | tr '/' '_' | tr -cd 'A-Za-z0-9_.-')

BASENAME="meepleai_seed_${TS}_${MODEL_SLUG}_${COMMIT}"
DUMP_FILE="$OUT_DIR/$BASENAME.dump"
GENERATED_FILE="$OUT_DIR/$BASENAME.generated-tables.sql"
META_FILE="$OUT_DIR/$BASENAME.meta.json"

# Resolve target DB credentials
if [ -f infra/secrets/database.secret ]; then
    # shellcheck disable=SC1091
    set -a; source infra/secrets/database.secret; set +a
fi
PG_USER="${POSTGRES_USER:-meepleai}"
PG_DB="${POSTGRES_DB:-meepleai_staging}"
log "source: user=$PG_USER db=$PG_DB"

# --- Auto-detect tables with generated columns ---
GENERATED_TABLES=$(docker exec meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" -At -c "
    SELECT DISTINCT table_name
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND is_generated = 'ALWAYS'
    ORDER BY table_name;")

EXCLUDE_FLAGS="--exclude-table-data=__EFMigrationsHistory"
TABLE_FLAGS=""
if [ -n "$GENERATED_TABLES" ]; then
    log "tabelle con colonne generate: $GENERATED_TABLES"
    for t in $GENERATED_TABLES; do
        EXCLUDE_FLAGS="$EXCLUDE_FLAGS --exclude-table-data=$t"
        TABLE_FLAGS="$TABLE_FLAGS --table=$t"
    done
else
    log "nessuna tabella con colonne generate"
fi

# --- Main dump: schema + data (excluding generated-column tables' data) ---
log "main dump → $DUMP_FILE"
docker exec meepleai-postgres pg_dump -U "$PG_USER" -d "$PG_DB" \
    -Fc --no-owner --no-privileges \
    $EXCLUDE_FLAGS \
    > "$DUMP_FILE"

# --- Supplement: generated-column tables as INSERT statements ---
# --column-inserts automatically omits generated columns from INSERT lists.
if [ -n "$TABLE_FLAGS" ]; then
    log "supplement dump (INSERT format) → $GENERATED_FILE"
    docker exec meepleai-postgres pg_dump -U "$PG_USER" -d "$PG_DB" \
        --data-only --column-inserts --no-owner --no-privileges \
        $TABLE_FLAGS \
        > "$GENERATED_FILE"
    log "  $(wc -l < "$GENERATED_FILE") righe INSERT generate"
else
    # No supplement needed — create empty file for consistent contract
    echo "-- No tables with generated columns" > "$GENERATED_FILE"
fi

# --- Sidecar .meta.json ---
log "raccolgo stats per sidecar"
STATS_JSON=$(docker exec meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" -At -c "
SELECT json_build_object(
  'ef_migration_head', (SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1),
  'pdf_count',         (SELECT COUNT(*) FROM pdf_documents WHERE processing_state IN ('Ready','Completed')),
  'chunk_count',       (SELECT COUNT(*) FROM text_chunks),
  'embedding_count',   (SELECT COUNT(*) FROM pgvector_embeddings),
  'failed_pdf_ids',    COALESCE((SELECT json_agg(\"Id\") FROM pdf_documents WHERE processing_state='Failed'), '[]'::json)
);")

MANIFEST_SHA=$(sha256sum apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml | awk '{print $1}')

jq -n \
    --argjson stats "$STATS_JSON" \
    --arg model "$EMBEDDING_MODEL" \
    --argjson dim "$EMBEDDING_DIM" \
    --arg commit "$COMMIT" \
    --arg created "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    --arg manifest_sha "$MANIFEST_SHA" \
    --arg generated_tables "${GENERATED_TABLES:-none}" \
    '{
       schema_version: $stats.ef_migration_head,
       ef_migration_head: $stats.ef_migration_head,
       embedding_model: $model,
       embedding_dim: $dim,
       app_commit: $commit,
       created_at: $created,
       dev_yml_sha256: $manifest_sha,
       pdf_count: $stats.pdf_count,
       chunk_count: $stats.chunk_count,
       embedding_count: $stats.embedding_count,
       failed_pdf_ids: $stats.failed_pdf_ids,
       generated_tables_supplement: $generated_tables
     }' > "$META_FILE"

# --- Checksums ---
log "calcolo sha256"
( cd "$OUT_DIR" && sha256sum "$BASENAME.dump" "$BASENAME.generated-tables.sql" > "$BASENAME.dump.sha256" )

echo "$BASENAME" > "$OUT_DIR/.latest"

log "snapshot pronto: $BASENAME"
log "  dump:  $DUMP_FILE ($(du -h "$DUMP_FILE" | awk '{print $1}'))"
log "  supplement: $GENERATED_FILE ($(wc -l < "$GENERATED_FILE") righe)"
log "  meta:  $META_FILE"

echo "$BASENAME"
