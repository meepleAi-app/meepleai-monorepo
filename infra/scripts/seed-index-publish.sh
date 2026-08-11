#!/usr/bin/env bash
# infra/scripts/seed-index-publish.sh
# Upload ultimo snapshot a seed blob bucket + rotation (tieni 3).
set -euo pipefail

# Resolve infra/ relative to THIS script, not the cwd. Both `make
# seed-index-publish` and the bake workflows invoke this from infra/ (not the
# repo root), so a hardcoded `infra/secrets/...` resolved to infra/infra/...
# and the publish always failed at the storage.secret source (#2516).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"
LATEST="$OUT_DIR/.latest"

log() { echo "[publish] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

[ -f "$LATEST" ] || fail ".latest non trovato in $OUT_DIR — lancia prima make seed-index"
BASENAME=$(cat "$LATEST")
[ -f "$OUT_DIR/$BASENAME.dump" ] || fail "dump mancante: $OUT_DIR/$BASENAME.dump"
[ -f "$OUT_DIR/$BASENAME.meta.json" ] || fail "meta mancante: $OUT_DIR/$BASENAME.meta.json"

# Carica credenziali da storage.secret
# shellcheck disable=SC1091
set -a; source "$INFRA_DIR/secrets/storage.secret"; set +a
: "${SEED_BLOB_BUCKET:?SEED_BLOB_BUCKET mancante in storage.secret}"
: "${S3_ENDPOINT:?S3_ENDPOINT mancante}"
: "${S3_ACCESS_KEY:?S3_ACCESS_KEY mancante}"
: "${S3_SECRET_KEY:?S3_SECRET_KEY mancante}"

export AWS_ACCESS_KEY_ID="$S3_ACCESS_KEY"
export AWS_SECRET_ACCESS_KEY="$S3_SECRET_KEY"
AWS_S3="aws s3 --endpoint-url $S3_ENDPOINT"

PREFIX="snapshots"

log "uploading $BASENAME.dump"
$AWS_S3 cp "$OUT_DIR/$BASENAME.dump" "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.dump"
# Generated-columns supplement (pgvector_embeddings et al.) — REQUIRED. The main
# .dump excludes generated-column tables' data (seed-index-dump.sh), so without
# this file the restored snapshot has 0 embeddings (RAG cold) and the .dump.sha256
# — which covers BOTH files — fails verification on the consumer. Always exists
# (dump writes an empty placeholder when no generated tables). snapshot-fetch.sh
# fetches it; it MUST be on the bucket before the meta.json atomic marker.
log "uploading $BASENAME.generated-tables.sql (embeddings supplement)"
$AWS_S3 cp "$OUT_DIR/$BASENAME.generated-tables.sql" "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.generated-tables.sql"
log "uploading $BASENAME.dump.sha256"
$AWS_S3 cp "$OUT_DIR/$BASENAME.dump.sha256" "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.dump.sha256"
# META PER ULTIMO — atomic marker
log "uploading $BASENAME.meta.json (atomic marker last)"
$AWS_S3 cp "$OUT_DIR/$BASENAME.meta.json" "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.meta.json"

# latest.txt pointer
log "updating latest.txt pointer"
echo "$BASENAME" | $AWS_S3 cp - "s3://$SEED_BLOB_BUCKET/$PREFIX/latest.txt"

# Retention: tieni ultimi 3
log "rotation: retain last 3 snapshots"
$AWS_S3 ls "s3://$SEED_BLOB_BUCKET/$PREFIX/" \
    | awk '{print $4}' \
    | grep -E '^meepleai_seed_[0-9]{8}T[0-9]{6}Z.*\.meta\.json$' \
    | sort -r \
    | tail -n +4 \
    | while read -r old_meta; do
        old_base=${old_meta%.meta.json}
        log "rimuovo obsoleto: $old_base"
        $AWS_S3 rm "s3://$SEED_BLOB_BUCKET/$PREFIX/$old_base.dump"                 || true
        $AWS_S3 rm "s3://$SEED_BLOB_BUCKET/$PREFIX/$old_base.generated-tables.sql" || true
        $AWS_S3 rm "s3://$SEED_BLOB_BUCKET/$PREFIX/$old_base.dump.sha256"          || true
        $AWS_S3 rm "s3://$SEED_BLOB_BUCKET/$PREFIX/$old_base.meta.json"            || true
      done

# ───────────────────────────────────────────────────────────────────────
# Audit trail (#2126 D6).
#
# Append-only markdown row per successful publish. Lives in
# `data/snapshots/AUDIT.md` and is committable — git blame + git log answer
# "who published what, when, against which commit, with what counts" in
# 30 seconds. Written here (AFTER the upload + retention rotation) so the
# trail tracks what is REALLY in the bucket: a failed upload exits earlier
# via `set -e` and no entry is appended → no divergence between markdown
# and bucket.
#
# The header is created on the first call so a fresh data/snapshots/
# directory bootstraps without ceremony.
# ───────────────────────────────────────────────────────────────────────
AUDIT_FILE="$OUT_DIR/AUDIT.md"
META_PATH="$OUT_DIR/$BASENAME.meta.json"

if [ ! -f "$AUDIT_FILE" ]; then
    log "creating audit trail header at $AUDIT_FILE"
    cat > "$AUDIT_FILE" <<'HEADER'
# Seed snapshot publish audit (#2126 D6)

Append-only trail of `make seed-index-publish` runs that completed an upload
to the seed blob bucket. Each row is written by
`infra/scripts/seed-index-publish.sh` *after* the dump, sha, sidecar, and
`latest.txt` are all in the bucket (failure exits earlier and writes no row,
so this trail matches the bucket).

Use `git blame` to answer "who published this snapshot" and `git log
data/snapshots/AUDIT.md` for the chronological sequence.

| Published at | Basename | App commit | EF migration | seed_schema | PDFs | Chunks | Embeddings | Model | Published by |
|---|---|---|---|---|---|---|---|---|---|
HEADER
fi

# Resolve "who" — in CI we have GITHUB_ACTOR / GITHUB_RUN_ID; locally we fall
# back to git user.email, then to `whoami`. Never expose tokens; this is just
# a human-readable label.
PUBLISHED_BY="${GITHUB_ACTOR:-}"
if [ -z "$PUBLISHED_BY" ]; then
    PUBLISHED_BY=$(git config user.email 2>/dev/null || echo "")
fi
if [ -z "$PUBLISHED_BY" ]; then
    PUBLISHED_BY=$(whoami 2>/dev/null || echo "unknown")
fi
if [ -n "${GITHUB_RUN_ID:-}" ]; then
    PUBLISHED_BY="$PUBLISHED_BY (GHA run $GITHUB_RUN_ID)"
fi

# Read sidecar fields for the row; default values stay friendly when an old
# sidecar predates the field (e.g. seed_table_schema_version on snapshots
# baked before #2126 D9).
PUBLISHED_AT=$(date -u +%Y-%m-%dT%H:%M:%SZ)
APP_COMMIT=$(jq -r '.app_commit // "?"' "$META_PATH")
EF_HEAD=$(jq -r '.ef_migration_head // "?"' "$META_PATH")
SEED_SCHEMA=$(jq -r '.seed_table_schema_version // 0' "$META_PATH")
PDF_COUNT=$(jq -r '.pdf_count // 0' "$META_PATH")
CHUNK_COUNT=$(jq -r '.chunk_count // 0' "$META_PATH")
EMB_COUNT=$(jq -r '.embedding_count // 0' "$META_PATH")
EMB_MODEL=$(jq -r '.embedding_model // "?"' "$META_PATH")

log "appending audit row → $AUDIT_FILE"
printf '| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |\n' \
    "$PUBLISHED_AT" "$BASENAME" "$APP_COMMIT" "$EF_HEAD" "$SEED_SCHEMA" \
    "$PDF_COUNT" "$CHUNK_COUNT" "$EMB_COUNT" "$EMB_MODEL" "$PUBLISHED_BY" \
    >> "$AUDIT_FILE"

log "OK"
