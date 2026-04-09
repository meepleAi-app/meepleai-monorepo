#!/usr/bin/env bash
# infra/scripts/seed-index-publish.sh
# Upload ultimo snapshot a seed blob bucket + rotation (tieni 3).
set -euo pipefail

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
set -a; source infra/secrets/storage.secret; set +a
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
        $AWS_S3 rm "s3://$SEED_BLOB_BUCKET/$PREFIX/$old_base.dump"        || true
        $AWS_S3 rm "s3://$SEED_BLOB_BUCKET/$PREFIX/$old_base.dump.sha256" || true
        $AWS_S3 rm "s3://$SEED_BLOB_BUCKET/$PREFIX/$old_base.meta.json"   || true
      done

log "OK"
