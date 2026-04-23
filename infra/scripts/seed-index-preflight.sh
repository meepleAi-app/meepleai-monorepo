#!/usr/bin/env bash
# infra/scripts/seed-index-preflight.sh
# Fail-fast checks prima di lanciare seed-index.
set -euo pipefail

log() { echo "[preflight] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

# 1. docker + compose
command -v docker >/dev/null || fail "docker non installato"
docker compose version >/dev/null || fail "docker compose non disponibile"

# 2. jq (usato da dump/verify/fetch)
command -v jq >/dev/null || fail "jq non installato"

# 3. Manifest dev.yml presente (supporta esecuzione da repo root o da infra/)
MANIFEST=""
for f in apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml \
         ../apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml; do
    [ -f "$f" ] && { MANIFEST="$f"; break; }
done
[ -n "$MANIFEST" ] || fail "manifest non trovato: apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml"

# 4. Seed bucket configurato (opzionale ma senza di esso PdfSeeder è silenzioso)
STORAGE_SECRET=""
for f in infra/secrets/storage.secret secrets/storage.secret; do
    [ -f "$f" ] && { STORAGE_SECRET="$f"; break; }
done
if [ -z "$STORAGE_SECRET" ] || ! grep -qE "^SEED_(BLOB|BUCKET)_" "$STORAGE_SECRET" 2>/dev/null; then
    log "WARNING: seed bucket non configurato in storage.secret — PdfSeeder non seederà PDF"
    log "Procedo comunque (il bake può essere legittimo per testare il flusso su 0 PDF)"
fi

# 5. Se postgres gira già, controlla che processing_jobs sia vuota o solo terminali
if docker ps --format '{{.Names}}' | grep -q '^meepleai-postgres$'; then
    pending=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c \
        "SELECT COUNT(*) FROM processing_jobs WHERE status IN ('Queued','Running','Retrying');" 2>/dev/null || echo "0")
    if [ "$pending" -gt 0 ]; then
        fail "processing_jobs contiene $pending job non terminali — ferma il run precedente o svuota la tabella"
    fi
fi

log "OK"
