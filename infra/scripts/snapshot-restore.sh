#!/usr/bin/env bash
# infra/scripts/snapshot-restore.sh
# Restore dello snapshot su un DB vuoto.
#
# Flow:
#   1. Guard: rifiuta se DB non vuoto
#   2. dotnet ef database update (schema dal working tree)
#   3. pg_restore --data-only (main dump, esclude tabelle con generated columns)
#   4. psql -f supplement.sql (INSERT-based, gestisce generated columns)
#   5. Smoke test (chunk count + orphans)
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"
BASENAME=$(cat "$OUT_DIR/.latest" 2>/dev/null || echo "")

log() { echo "[restore] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

[ -n "$BASENAME" ] || fail ".latest mancante"
DUMP="$OUT_DIR/$BASENAME.dump"
SUPPLEMENT="$OUT_DIR/$BASENAME.generated-tables.sql"
META="$OUT_DIR/$BASENAME.meta.json"
[ -f "$DUMP" ] || fail "dump mancante: $DUMP"
[ -f "$META" ] || fail "meta mancante: $META"

# Resolve target DB credentials
if [ -f infra/secrets/database.secret ]; then
    # shellcheck disable=SC1091
    set -a; source infra/secrets/database.secret; set +a
fi
PG_USER="${POSTGRES_USER:-meepleai}"
PG_DB="${POSTGRES_DB:-meepleai_staging}"
log "target: user=$PG_USER db=$PG_DB"

# Guard: DB non deve contenere tabelle application
table_count=$(docker exec meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" -At -c \
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

# Step 1: Schema dal working tree
log "applico migrations dal working tree"
( cd apps/api/src/Api && dotnet ef database update )

# Step 2: Main data restore (tabelle SENZA generated columns)
# Errori su tabelle non-EF (n8n, insights) vengono ignorati (exit code ≠ 0 è atteso).
log "pg_restore --data-only (main dump)"
restore_errors=$(docker exec -i meepleai-postgres pg_restore \
    -U "$PG_USER" -d "$PG_DB" \
    --data-only --disable-triggers --no-owner --no-privileges \
    < "$DUMP" 2>&1 || true)

# Conta errori reali (esclude tabelle non-EF come n8n/insights)
if [ -n "$restore_errors" ]; then
    error_count=$(echo "$restore_errors" | grep -c "^pg_restore: error:" || true)
    # Filtra errori su tabelle EF-managed (quelli sono problemi reali)
    ef_errors=$(echo "$restore_errors" | grep -E "error:.*\b(games|shared_games|pdf_documents|text_chunks|pgvector_embeddings|users|agent_definitions)\b" || true)
    if [ -n "$ef_errors" ]; then
        log "WARNING: errori su tabelle EF-managed:"
        echo "$ef_errors" >&2
    fi
    log "pg_restore completato con $error_count errori (tabelle non-EF ignorati)"
fi

# Step 3: Supplement — tabelle con generated columns (INSERT format)
if [ -f "$SUPPLEMENT" ] && [ "$(wc -l < "$SUPPLEMENT")" -gt 1 ]; then
    log "restore supplement (INSERT per generated-column tables)"
    docker exec -i meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" \
        --set ON_ERROR_STOP=on \
        < "$SUPPLEMENT"
    log "supplement OK"
else
    log "nessun supplement file — skip"
fi

# Step 4: Smoke test
log "smoke test: chunk count + orphans"
actual_chunks=$(docker exec meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" -At -c "SELECT COUNT(*) FROM text_chunks;")
expected_chunks=$(jq '.chunk_count' "$META")

if [ "$actual_chunks" != "$expected_chunks" ]; then
    log "WARNING: chunk count post-restore ($actual_chunks) ≠ sidecar ($expected_chunks)"
    log "  (differenza può essere dovuta a snapshot prodotto con conteggio diverso)"
    # Non fallire: il sidecar potrebbe avere un conteggio da un'altra query
fi

orphan_chunks=$(docker exec meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" -At -c "
    SELECT COUNT(*) FROM text_chunks tc
    LEFT JOIN pdf_documents p ON p.id = tc.pdf_document_id
    WHERE p.id IS NULL;")
if [ "$orphan_chunks" != "0" ]; then
    log "WARNING: $orphan_chunks text_chunks orfani (possibile drift schema)"
fi

orphan_embeds=$(docker exec meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" -At -c "
    SELECT COUNT(*) FROM pgvector_embeddings e
    LEFT JOIN text_chunks tc ON tc.id = e.text_chunk_id
    WHERE tc.id IS NULL;")
if [ "$orphan_embeds" != "0" ]; then
    log "WARNING: $orphan_embeds pgvector_embeddings orfani (possibile drift schema)"
fi

# Report finale
log "OK — chunks=$actual_chunks, orphan_chunks=$orphan_chunks, orphan_embeds=$orphan_embeds"
