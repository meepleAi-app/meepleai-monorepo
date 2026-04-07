#!/usr/bin/env bash
# MeepleAI Automated Backup Script
# Backs up PostgreSQL, PDF uploads, and Redis — optionally uploads to R2/S3.
#
# Cron (run daily at 03:00 on the staging server):
#   0 3 * * * cd /opt/meepleai/repo/infra && bash scripts/backup.sh >> /var/log/meepleai-backup.log 2>&1

set -euo pipefail

# ─────────────────────────────────────────────
# Error trap — notify on unexpected failure
# ─────────────────────────────────────────────
trap 'on_error $LINENO' ERR

on_error() {
  local line="${1:-unknown}"
  log "ERROR" "Backup failed at line ${line} — exit code: $?"
  notify_webhook "failure" "Backup failed at line ${line}"
  exit 1
}

# ─────────────────────────────────────────────
# Logging helpers
# ─────────────────────────────────────────────
log() {
  local level="$1"
  local msg="$2"
  echo "[$(date '+%Y-%m-%dT%H:%M:%S')] [${level}] ${msg}"
}

# ─────────────────────────────────────────────
# Load secrets (non-interactive, no export leakage)
# ─────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SECRETS_DIR="${SCRIPT_DIR}/../secrets"

load_secret_file() {
  local file="$1"
  if [[ -f "$file" ]]; then
    set -a
    # shellcheck source=/dev/null
    source "$file"
    set +a
    log "INFO" "Loaded secrets from $(basename "$file")"
  else
    log "WARN" "Secret file not found: $file — skipping"
  fi
}

load_secret_file "${SECRETS_DIR}/backup.secret"
load_secret_file "${SECRETS_DIR}/database.secret"
load_secret_file "${SECRETS_DIR}/redis.secret"
load_secret_file "${SECRETS_DIR}/storage.secret"

# ─────────────────────────────────────────────
# Configuration defaults (override via secret files)
# ─────────────────────────────────────────────
: "${PG_USER:=${POSTGRES_USER:-meepleai}}"
: "${PG_CONTAINER:=meepleai-postgres}"
: "${REDIS_CONTAINER:=meepleai-redis}"
: "${PDF_VOLUME:=meepleai_pdf_uploads}"
: "${BACKUP_BASE_DIR:=/backups/meepleai}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-7}"
: "${S3_BACKUP_ENABLED:=false}"
WEBHOOK_URL="${BACKUP_WEBHOOK_URL:-}"

# S3/R2 — map from backup.secret variable names
S3_ENDPOINT="${S3_BACKUP_ENDPOINT:-}"
S3_BUCKET_NAME="${S3_BACKUP_BUCKET_NAME:-meepleai-backups}"
export AWS_ACCESS_KEY_ID="${S3_BACKUP_ACCESS_KEY:-}"
export AWS_SECRET_ACCESS_KEY="${S3_BACKUP_SECRET_KEY:-}"
S3_REGION="${S3_REGION:-auto}"

# ─────────────────────────────────────────────
# Webhook notification
# ─────────────────────────────────────────────
notify_webhook() {
  local status="$1"
  local message="$2"

  if [[ -z "${WEBHOOK_URL}" ]]; then
    return 0
  fi

  local payload
  payload=$(printf '{"status":"%s","message":"%s","timestamp":"%s","host":"%s"}' \
    "$status" "$message" "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "$(hostname)")

  curl --silent --max-time 10 \
    -H "Content-Type: application/json" \
    -d "$payload" \
    "$WEBHOOK_URL" \
    || log "WARN" "Webhook notification failed (non-fatal)"
}

# ─────────────────────────────────────────────
# Prepare backup directory
# ─────────────────────────────────────────────
TIMESTAMP="$(date '+%Y%m%d-%H%M%S')"
BACKUP_DIR="${BACKUP_BASE_DIR}/${TIMESTAMP}"

log "INFO" "Starting MeepleAI backup — destination: ${BACKUP_DIR}"
mkdir -p "${BACKUP_DIR}"

# ─────────────────────────────────────────────
# 1. PostgreSQL full dump
# ─────────────────────────────────────────────
backup_postgres() {
  log "INFO" "Starting PostgreSQL backup..."

  local pg_dump_file="${BACKUP_DIR}/postgres.sql.gz"

  docker exec "${PG_CONTAINER}" \
    pg_dumpall -U "${PG_USER}" \
    | gzip -6 > "${pg_dump_file}"

  local dump_size
  dump_size=$(stat -c%s "${pg_dump_file}")

  if [[ "${dump_size}" -lt 1024 ]]; then
    log "ERROR" "PostgreSQL dump is suspiciously small (${dump_size} bytes < 1KB) — aborting"
    rm -f "${pg_dump_file}"
    exit 1
  fi

  log "INFO" "PostgreSQL backup complete: ${pg_dump_file} (${dump_size} bytes)"
}

# ─────────────────────────────────────────────
# 2. PDF uploads volume
# ─────────────────────────────────────────────
backup_pdf_uploads() {
  log "INFO" "Starting PDF uploads backup..."

  if ! docker volume inspect "${PDF_VOLUME}" > /dev/null 2>&1; then
    log "INFO" "PDF volume ${PDF_VOLUME} not found — skipping (S3 storage?)"
    return 0
  fi

  local pdf_archive="${BACKUP_DIR}/pdf_uploads.tar.gz"

  docker run --rm \
    -v "${PDF_VOLUME}:/data:ro" \
    -v "${BACKUP_DIR}:/backup" \
    alpine \
    tar czf "/backup/$(basename "${pdf_archive}")" -C /data .

  log "INFO" "PDF uploads backup complete: ${pdf_archive}"
}

# ─────────────────────────────────────────────
# 3. Redis snapshot
# ─────────────────────────────────────────────
backup_redis() {
  log "INFO" "Starting Redis backup..."

  local redis_dir="${BACKUP_DIR}/redis"
  mkdir -p "${redis_dir}"

  # Trigger a synchronous save
  docker exec "${REDIS_CONTAINER}" redis-cli ${REDIS_PASSWORD:+-a "$REDIS_PASSWORD"} BGSAVE
  log "INFO" "Redis BGSAVE triggered — waiting for completion..."

  # Poll until save completes (max 60 seconds)
  local max_wait=60
  local waited=0
  while true; do
    local last_save_status
    last_save_status=$(docker exec "${REDIS_CONTAINER}" redis-cli ${REDIS_PASSWORD:+-a "$REDIS_PASSWORD"} LASTSAVE 2>/dev/null || echo "0")
    local in_progress
    in_progress=$(docker exec "${REDIS_CONTAINER}" redis-cli ${REDIS_PASSWORD:+-a "$REDIS_PASSWORD"} INFO persistence \
      | grep "rdb_bgsave_in_progress" | tr -d '[:space:]' | cut -d: -f2 | tr -d '\r')

    if [[ "${in_progress}" == "0" ]]; then
      log "INFO" "Redis BGSAVE complete"
      break
    fi

    if [[ "${waited}" -ge "${max_wait}" ]]; then
      log "WARN" "Redis BGSAVE did not complete within ${max_wait}s — copying available dump"
      break
    fi

    sleep 2
    waited=$((waited + 2))
  done

  # Copy RDB dump from container
  docker cp "${REDIS_CONTAINER}:/data/dump.rdb" "${redis_dir}/dump_${TIMESTAMP}.rdb" \
    || log "WARN" "Could not copy Redis dump.rdb (non-fatal — Redis may have no persistence)"

  log "INFO" "Redis backup complete: ${redis_dir}/"
}

# ─────────────────────────────────────────────
# 4. Upload to R2/S3
# ─────────────────────────────────────────────
upload_to_s3() {
  if [[ "${S3_BACKUP_ENABLED}" != "true" ]]; then
    log "INFO" "S3 upload disabled (S3_BACKUP_ENABLED != true) — skipping"
    return 0
  fi

  log "INFO" "Uploading backup to S3/R2 bucket: ${S3_BUCKET_NAME}..."

  local s3_prefix="s3://${S3_BUCKET_NAME}/${TIMESTAMP}/"

  aws s3 sync "${BACKUP_DIR}/" "${s3_prefix}" \
    --endpoint-url "${S3_ENDPOINT}" \
    --region "${S3_REGION}" \
    --storage-class STANDARD \
    --no-progress

  log "INFO" "S3/R2 upload complete: ${s3_prefix}"
}

# ─────────────────────────────────────────────
# 5. Clean old R2/S3 backups
# ─────────────────────────────────────────────
clean_s3_backups() {
  if [[ "${S3_BACKUP_ENABLED}" != "true" ]]; then
    return 0
  fi

  log "INFO" "Pruning S3/R2 backups older than ${RETENTION_DAYS} days..."

  local cutoff_epoch
  cutoff_epoch=$(date -d "${RETENTION_DAYS} days ago" '+%s')

  # List top-level backup prefixes (date-stamped directories)
  local prefixes
  prefixes=$(aws s3 ls "s3://${S3_BUCKET_NAME}/" \
               --endpoint-url "${S3_ENDPOINT}" \
               --region "${S3_REGION}" \
             | awk '{print $NF}' | grep -E '^[0-9]{8}-[0-9]{6}/$' || true)

  while IFS= read -r prefix; do
    [[ -z "$prefix" ]] && continue
    local dir_name="${prefix%/}"   # e.g. 20260101-030000
    local dir_date="${dir_name%%-*}"  # e.g. 20260101
    local dir_epoch
    dir_epoch=$(date -d "${dir_date}" '+%s' 2>/dev/null || echo "0")

    if [[ "${dir_epoch}" -lt "${cutoff_epoch}" ]]; then
      log "INFO" "Deleting old S3/R2 backup: ${prefix}"
      aws s3 rm "s3://${S3_BUCKET_NAME}/${prefix}" \
        --endpoint-url "${S3_ENDPOINT}" \
        --region "${S3_REGION}" \
        --recursive
    fi
  done <<< "${prefixes}"

  log "INFO" "S3/R2 cleanup complete"
}

# ─────────────────────────────────────────────
# 6. Clean old local backups
# ─────────────────────────────────────────────
clean_local_backups() {
  log "INFO" "Pruning local backups older than ${RETENTION_DAYS} days in ${BACKUP_BASE_DIR}..."

  find "${BACKUP_BASE_DIR}" \
    -maxdepth 1 \
    -mindepth 1 \
    -type d \
    -mtime "+${RETENTION_DAYS}" \
    -exec rm -rf {} + \
    && log "INFO" "Local backup cleanup complete" \
    || log "WARN" "Local backup cleanup encountered an issue (non-fatal)"
}

# ─────────────────────────────────────────────
# Main execution
# ─────────────────────────────────────────────
main() {
  backup_postgres
  backup_pdf_uploads
  backup_redis
  upload_to_s3
  clean_s3_backups
  clean_local_backups

  log "INFO" "All backups completed successfully — ${BACKUP_DIR}"
  notify_webhook "success" "Backup completed successfully: ${TIMESTAMP}"
}

main
