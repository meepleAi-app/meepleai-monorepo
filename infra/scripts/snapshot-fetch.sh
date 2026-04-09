#!/usr/bin/env bash
# infra/scripts/snapshot-fetch.sh
# Priorità: SNAPSHOT_FILE env > cache locale più recente > download da bucket.
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"
mkdir -p "$OUT_DIR"

log() { echo "[fetch] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

if [ -n "${SNAPSHOT_FILE:-}" ]; then
    [ -f "$SNAPSHOT_FILE" ] || fail "SNAPSHOT_FILE non esiste: $SNAPSHOT_FILE"
    BASENAME=$(basename "$SNAPSHOT_FILE" .dump)
    log "override esplicito: $BASENAME"
elif ls "$OUT_DIR"/meepleai_seed_*.meta.json >/dev/null 2>&1; then
    BASENAME=$(ls -t "$OUT_DIR"/meepleai_seed_*.meta.json | head -1 | xargs basename | sed 's/\.meta\.json$//')
    log "cache locale: $BASENAME"
else
    log "nessuna cache locale — download dal bucket"
    # shellcheck disable=SC1091
    set -a; source infra/secrets/storage.secret; set +a
    : "${SEED_BLOB_BUCKET:?SEED_BLOB_BUCKET mancante}"
    : "${S3_ENDPOINT:?S3_ENDPOINT mancante}"
    : "${S3_ACCESS_KEY:?S3_ACCESS_KEY mancante}"
    : "${S3_SECRET_KEY:?S3_SECRET_KEY mancante}"
    export AWS_ACCESS_KEY_ID="$S3_ACCESS_KEY"
    export AWS_SECRET_ACCESS_KEY="$S3_SECRET_KEY"
    AWS_S3="aws s3 --endpoint-url $S3_ENDPOINT"
    PREFIX="snapshots"

    BASENAME=$($AWS_S3 cp "s3://$SEED_BLOB_BUCKET/$PREFIX/latest.txt" - | tr -d '[:space:]')
    [ -n "$BASENAME" ] || fail "latest.txt vuoto sul bucket"

    log "scarico $BASENAME (.dump + .sha256 + .meta.json)"
    $AWS_S3 cp "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.dump"        "$OUT_DIR/$BASENAME.dump.partial"
    $AWS_S3 cp "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.dump.sha256" "$OUT_DIR/$BASENAME.dump.sha256"
    $AWS_S3 cp "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.meta.json"   "$OUT_DIR/$BASENAME.meta.json.partial"

    # Verifica checksum contro il .partial
    ( cd "$OUT_DIR" && \
      sed "s|$BASENAME.dump|$BASENAME.dump.partial|" "$BASENAME.dump.sha256" | sha256sum -c - ) \
      || fail "sha256 mismatch"

    # Atomic rename
    mv "$OUT_DIR/$BASENAME.dump.partial"      "$OUT_DIR/$BASENAME.dump"
    mv "$OUT_DIR/$BASENAME.meta.json.partial" "$OUT_DIR/$BASENAME.meta.json"
fi

echo "$BASENAME" > "$OUT_DIR/.latest"
log "OK — basename: $BASENAME"
