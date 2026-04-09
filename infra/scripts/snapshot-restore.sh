#!/usr/bin/env bash
# infra/scripts/snapshot-restore.sh
# Restore dello snapshot su un DB vuoto dopo ef update.
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"
BASENAME=$(cat "$OUT_DIR/.latest" 2>/dev/null || echo "")

log() { echo "[restore] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

[ -n "$BASENAME" ] || fail ".latest mancante"
DUMP="$OUT_DIR/$BASENAME.dump"
META="$OUT_DIR/$BASENAME.meta.json"
[ -f "$DUMP" ] || fail "dump mancante: $DUMP"
[ -f "$META" ] || fail "meta mancante: $META"

# Guard: DB non deve contenere tabelle application (evita di sovrascrivere lavoro)
table_count=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c \
    "SELECT COUNT(*) FROM information_schema.tables
     WHERE table_schema='public' AND table_type='BASE TABLE' AND table_name NOT LIKE 'pg_%';")

if [ "$table_count" -gt 0 ]; then
    cat >&2 <<EOF
::error:: DB non vuoto ($table_count tabelle application presenti)
snapshot-restore rifiuta di sovrascrivere dati esistenti.
Usa: make dev-from-snapshot-force   (DISTRUTTIVO, azzera il volume postgres)
EOF
    exit 10
fi

log "applico migrations dal working tree"
( cd apps/api/src/Api && dotnet ef database update )

log "pg_restore --data-only"
docker exec -i meepleai-postgres pg_restore \
    -U postgres -d meepleai \
    --data-only --disable-triggers --no-owner --no-privileges \
    < "$DUMP"

# Smoke test
log "smoke test: chunk count + orphans"
actual_chunks=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c "SELECT COUNT(*) FROM text_chunks;")
expected_chunks=$(jq '.chunk_count' "$META")

if [ "$actual_chunks" != "$expected_chunks" ]; then
    fail "chunk count post-restore ($actual_chunks) ≠ sidecar ($expected_chunks)"
fi

orphan_chunks=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c "
    SELECT COUNT(*) FROM text_chunks tc
    LEFT JOIN pdf_documents p ON p.id = tc.pdf_document_id
    WHERE p.id IS NULL;")
[ "$orphan_chunks" = "0" ] || fail "$orphan_chunks text_chunks orfani"

orphan_embeds=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c "
    SELECT COUNT(*) FROM pgvector_embeddings e
    LEFT JOIN text_chunks tc ON tc.id = e.text_chunk_id
    WHERE tc.id IS NULL;")
[ "$orphan_embeds" = "0" ] || fail "$orphan_embeds pgvector_embeddings orfani"

log "OK — $actual_chunks chunks ripristinati, nessun orfano"
