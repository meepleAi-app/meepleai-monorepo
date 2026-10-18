#!/usr/bin/env bash
# infra/scripts/snapshot-verify.sh
# Compat gate: blocca il restore se snapshot incompatibile col working tree.
# Exit codes:
#   0  ok
#   1  altro (file mancanti, jq missing, ...)
#   2  EF migration drift  — la migration dello snapshot non e' fra quelle del
#      working tree: snapshot piu' nuovo, o di un altro branch. Uno snapshot piu'
#      VECCHIO del working tree non e' drift (#4489): snapshot-restore.sh carica i
#      dati allo schema del bake e applica dopo le migrations successive.
#   3  embedding model drift
#   4  embedding dim drift
#   5  seed-table schema-version drift (#2126 D9) — una tabella seedata e'
#      stata rinominata o ristrutturata dopo il bake (e.g. embeddings →
#      vector_documents) e lo snapshot non e' piu' ricostruibile a partire dal
#      working tree corrente. Bump del file infra/seed-schema.version in PR
#      che introducono il rename.
#   6  sidecar invariant violation (#2126 D7) — chunk_count != embedding_count
#      indica un bake parzialmente fallito che non e' stato intercettato dalla
#      lista failed_pdf_ids. Lo snapshot e' inutilizzabile.
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"

log() { echo "[verify] $*" >&2; }

BASENAME=$(cat "$OUT_DIR/.latest" 2>/dev/null || echo "")
[ -n "$BASENAME" ] || { echo "::error:: .latest vuoto o mancante" >&2; exit 1; }
META="$OUT_DIR/$BASENAME.meta.json"
[ -f "$META" ] || { echo "::error:: meta.json mancante: $META" >&2; exit 1; }

# 1. EF migration head
expected_head=$(jq -r '.ef_migration_head' "$META")
# Le migrations del working tree, una per riga e in ordine. Nei test le sostituisce
# EXPECTED_EF_MIGRATIONS; EXPECTED_EF_HEAD da solo vale come elenco di una migration.
working_migrations=${EXPECTED_EF_MIGRATIONS:-${EXPECTED_EF_HEAD:-}}
if [ -z "$working_migrations" ]; then
    # Resolvi dal working tree. Le migration EF sono nominate
    # YYYYMMDDHHMMSS_Name.cs ed EF genera una coppia <Name>.cs + <Name>.Designer.cs
    # per ogni migration — escludi i Designer, che sono lo stesso nome due volte.
    working_migrations=$(find ../apps/api/src/Api apps/api/src/Api -name '[0-9]*_*.cs' -not -name '*.Designer.cs' -path '*/Migrations/*' 2>/dev/null \
        | xargs -I{} basename {} .cs \
        | grep -E '^[0-9]{14}_[A-Za-z0-9_]+$' || echo "")
fi
working_migrations=$(sed '/^$/d' <<<"$working_migrations" | LC_ALL=C sort -u)
working_head=${EXPECTED_EF_HEAD:-$(tail -1 <<<"$working_migrations")}

if [ "$expected_head" != "$working_head" ]; then
    if [ -n "$expected_head" ] && grep -Fxq -- "$expected_head" <<<"$working_migrations"; then
        # Snapshot piu' vecchio del working tree (#4489): non e' drift. Prima la
        # regola era l'uguaglianza, e con il bake completo una volta a settimana
        # ogni migration mergiata fermava il gate RAG su tutti i branch fino al
        # bake successivo. snapshot-restore.sh porta lo schema alla migration del
        # bake, carica i dati, e solo dopo applica queste: girano sui dati dello
        # snapshot, come su un deploy.
        pending=$(awk -v h="$expected_head" '$0 > h' <<<"$working_migrations")
        echo "::warning:: snapshot piu' vecchio del working tree: snapshot-restore.sh applichera' dopo il caricamento dei dati $(grep -c . <<<"$pending") migration" >&2
        log "snapshot head : $expected_head"
        log "da applicare dopo il caricamento:"
        sed 's/^/[verify]   /' <<<"$pending" >&2
    else
        cat >&2 <<EOF
::error:: snapshot disallineato con le migrations del working tree
  snapshot head : $expected_head
  working head  : $working_head
La migration dello snapshot non e' fra quelle del working tree: lo snapshot e'
piu' nuovo del checkout, o viene da un altro branch.
Opzioni:
  1. aggiorna il checkout (git pull), o git checkout del commit compatibile
  2. make seed-index  (rigenera lo snapshot sul commit corrente)
EOF
        exit 2
    fi
fi

# 2. Embedding model
expected_model=$(jq -r '.embedding_model' "$META")
current_model=${EXPECTED_EMBEDDING_MODEL:-}
if [ -z "$current_model" ]; then
    for f in infra/secrets/embedding-service.secret secrets/embedding-service.secret \
             infra/secrets/embedding.secret secrets/embedding.secret; do
        [ -f "$f" ] && current_model=$(grep -E '^EMBEDDING_MODEL=' "$f" | cut -d= -f2- || echo "")
        [ -n "$current_model" ] && break
    done
fi
if [ -z "$current_model" ]; then
    for f in compose.dev.yml ../infra/compose.dev.yml; do
        [ -f "$f" ] && current_model=$(grep -oP 'EMBEDDING_MODEL:\s*\K\S+' "$f" | head -1 || echo "")
        [ -n "$current_model" ] && break
    done
fi

if [ "$expected_model" != "$current_model" ]; then
    cat >&2 <<EOF
::error:: embedding model mismatch
  snapshot : $expected_model
  current  : $current_model
I vettori non sono confrontabili col model corrente.
EOF
    exit 3
fi

# 3. Embedding dim
expected_dim=$(jq -r '.embedding_dim' "$META")
current_dim=${EXPECTED_EMBEDDING_DIM:-}
if [ -z "$current_dim" ]; then
    for f in infra/secrets/embedding-service.secret secrets/embedding-service.secret \
             infra/secrets/embedding.secret secrets/embedding.secret; do
        [ -f "$f" ] && current_dim=$(grep -E '^EMBEDDING_DIM=' "$f" | cut -d= -f2- || echo "")
        [ -n "$current_dim" ] && break
    done
fi
if [ -z "$current_dim" ]; then
    for f in compose.dev.yml ../infra/compose.dev.yml; do
        [ -f "$f" ] && current_dim=$(grep -oP 'EMBEDDING_DIM(ENSIONS)?:\s*\K\S+' "$f" | head -1 || echo "")
        [ -n "$current_dim" ] && break
    done
fi

if [ "$expected_dim" != "$current_dim" ]; then
    echo "::error:: embedding_dim mismatch ($expected_dim vs $current_dim)" >&2
    exit 4
fi

# 4. dev.yml drift — warning non bloccante
expected_sha=$(jq -r '.dev_yml_sha256' "$META")
current_sha=$(sha256sum apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml 2>/dev/null | awk '{print $1}' || echo "")
if [ "$expected_sha" != "$current_sha" ]; then
    echo "::warning:: dev.yml è cambiato dopo lo snapshot — eventuali giochi nuovi NON sono indicizzati" >&2
fi

failed_count=$(jq '.failed_pdf_ids | length' "$META")
if [ "$failed_count" -gt 0 ]; then
    echo "::warning:: snapshot contiene $failed_count PDF falliti" >&2
fi

# 5. seed-table schema-version (#2126 D9)
# Difende dal pattern «embeddings → vector_documents» del 2026-05: una
# tabella seedata viene rinominata/dropata dopo il bake, lo snapshot
# diventa irrecuperabile. La fonte di verita' e' infra/seed-schema.version
# (contiene un counter). Va bumpato nella stessa PR che introduce il
# rename. Sidecar senza il field viene trattato come version=0 — primo
# bake dopo l'introduzione di questo gate fallisce con exit 5, costringendo
# l'autore del rename a rigenerare lo snapshot.
expected_seed_version=$(jq -r '.seed_table_schema_version // 0' "$META")
current_seed_version=0
for f in infra/seed-schema.version ../infra/seed-schema.version seed-schema.version; do
    if [ -f "$f" ]; then
        current_seed_version=$(tr -d '[:space:]' <"$f")
        break
    fi
done

if [ "$expected_seed_version" != "$current_seed_version" ]; then
    cat >&2 <<EOF
::error:: seed-table schema-version drift
  snapshot      : $expected_seed_version
  working tree  : $current_seed_version  (infra/seed-schema.version)
Una tabella seedata e' cambiata struttura dopo il bake; lo snapshot non e'
piu' ricostruibile dalla shape corrente.
  ${C_BLU:-}make seed-index${C_RST:-}  rigenera lo snapshot sul commit corrente.
EOF
    exit 5
fi

# 6. sidecar invariant chunk_count == embedding_count (#2126 D7)
# Un bake parzialmente fallito puo' lasciare chunks senza embedding (es. job
# embedding-service in errore dopo chunking riuscito). failed_pdf_ids lo
# intercetta solo se il pdf_documents row finisce in Failed state; bake
# silent-partial scappa. Questo check rende esplicito l'invariante.
chunk_count=$(jq -r '.chunk_count // 0' "$META")
embedding_count=$(jq -r '.embedding_count // 0' "$META")
if [ "$chunk_count" != "$embedding_count" ]; then
    cat >&2 <<EOF
::error:: sidecar invariant violated
  chunk_count     : $chunk_count
  embedding_count : $embedding_count
Lo snapshot e' bake-partial. Investiga embedding-service logs e rigenera con
make seed-index.
EOF
    exit 6
fi

log "OK — $BASENAME compatibile"
