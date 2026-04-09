#!/usr/bin/env bash
# infra/scripts/snapshot-verify.sh
# Compat gate: blocca il restore se snapshot incompatibile col working tree.
# Exit codes: 0=ok, 2=migration drift, 3=model drift, 4=dim drift, 1=altro
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"

log() { echo "[verify] $*" >&2; }

BASENAME=$(cat "$OUT_DIR/.latest" 2>/dev/null || echo "")
[ -n "$BASENAME" ] || { echo "::error:: .latest vuoto o mancante" >&2; exit 1; }
META="$OUT_DIR/$BASENAME.meta.json"
[ -f "$META" ] || { echo "::error:: meta.json mancante: $META" >&2; exit 1; }

# 1. EF migration head
expected_head=$(jq -r '.ef_migration_head' "$META")
working_head=${EXPECTED_EF_HEAD:-}
if [ -z "$working_head" ]; then
    # Resolvi dal working tree — cerca file migration più recente.
    # Le migration EF sono nominate YYYYMMDDHHMMSS_Name.cs ed EF genera una
    # coppia <Name>.cs + <Name>.Designer.cs per ogni migration — escludi
    # i Designer, altrimenti sort -r li ordina prima del file canonico.
    working_head=$(find apps/api/src/Api -name '[0-9]*_*.cs' -not -name '*.Designer.cs' -path '*/Migrations/*' 2>/dev/null \
        | xargs -I{} basename {} .cs \
        | grep -E '^[0-9]{14}_[A-Za-z0-9_]+$' \
        | sort -r | head -1 || echo "")
fi

if [ "$expected_head" != "$working_head" ]; then
    cat >&2 <<EOF
::error:: snapshot disallineato con le migrations del working tree
  snapshot head : $expected_head
  working head  : $working_head
Opzioni:
  1. git checkout del commit compatibile con lo snapshot
  2. make seed-index  (rigenera lo snapshot sul commit corrente)
EOF
    exit 2
fi

# 2. Embedding model
expected_model=$(jq -r '.embedding_model' "$META")
current_model=${EXPECTED_EMBEDDING_MODEL:-}
if [ -z "$current_model" ] && [ -f infra/secrets/embedding.secret ]; then
    current_model=$(grep -E '^EMBEDDING_MODEL=' infra/secrets/embedding.secret | cut -d= -f2- || echo "")
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
if [ -z "$current_dim" ] && [ -f infra/secrets/embedding.secret ]; then
    current_dim=$(grep -E '^EMBEDDING_DIM=' infra/secrets/embedding.secret | cut -d= -f2- || echo "")
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

log "OK — $BASENAME compatibile"
