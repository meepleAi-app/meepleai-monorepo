#!/usr/bin/env bash
# infra/scripts/seed-index-dump.sh
# pg_dump + sidecar .meta.json + sha256.
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
META_FILE="$OUT_DIR/$BASENAME.meta.json"
SHA_FILE="$OUT_DIR/$BASENAME.dump.sha256"

log "dumping DB → $DUMP_FILE"
docker exec meepleai-postgres pg_dump -U postgres -d meepleai \
    -Fc --no-owner --no-privileges \
    --exclude-table-data='__EFMigrationsHistory' \
    > "$DUMP_FILE"

log "raccolgo stats per sidecar"
STATS_JSON=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c "
SELECT json_build_object(
  'ef_migration_head', (SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1),
  'pdf_count',         (SELECT COUNT(*) FROM pdf_documents WHERE processing_state='Completed'),
  'chunk_count',       (SELECT COUNT(*) FROM text_chunks),
  'embedding_count',   (SELECT COUNT(*) FROM pgvector_embeddings),
  'failed_pdf_ids',    COALESCE((SELECT json_agg(pdf_document_id) FROM processing_jobs WHERE status IN ('Failed','DeadLettered')), '[]'::json)
);")

MANIFEST_SHA=$(sha256sum apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml | awk '{print $1}')

jq -n \
    --argjson stats "$STATS_JSON" \
    --arg model "$EMBEDDING_MODEL" \
    --argjson dim "$EMBEDDING_DIM" \
    --arg commit "$COMMIT" \
    --arg created "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    --arg manifest_sha "$MANIFEST_SHA" \
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
       failed_pdf_ids: $stats.failed_pdf_ids
     }' > "$META_FILE"

log "calcolo sha256"
( cd "$OUT_DIR" && sha256sum "$BASENAME.dump" > "$BASENAME.dump.sha256" )

# Aggiorna .latest (usato dal consume flow come cache locale)
echo "$BASENAME" > "$OUT_DIR/.latest"

log "snapshot pronto: $BASENAME"
log "  dump:  $DUMP_FILE ($(du -h "$DUMP_FILE" | awk '{print $1}'))"
log "  meta:  $META_FILE"
log "  sha:   $SHA_FILE"

echo "$BASENAME"
