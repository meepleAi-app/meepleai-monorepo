#!/usr/bin/env bash
# MeepleAI Automated Backup Script
# Backs up PostgreSQL, PDF uploads, and Redis — optionally uploads to R2/S3.
#
# Cron (run daily at 03:00 on the staging server):
#   0 3 * * * cd /opt/meepleai/repo/infra && bash scripts/backup.sh >> /var/log/meepleai-backup.log 2>&1

# -E (errtrace) is load-bearing, not decoration: without it bash does NOT inherit the
# ERR trap into shell functions, so every `return 1` inside backup_postgres,
# upload_to_s3 & co. unwound the script with the trap below never firing — no webhook,
# no notification, and clean_local_backups never reached. A backup run that failed
# looked exactly like one that succeeded, from the outside (#3669).
set -Eeuo pipefail

# cron gives user jobs a minimal PATH (/usr/bin:/bin on Debian/Ubuntu) and this
# crontab sets none. The AWS CLI v2 installer puts `aws` in /usr/local/bin, which is
# therefore invisible at 03:00 while working perfectly from an interactive shell —
# the offsite upload would fail nightly and only in the dark. Verified on the staging
# VPS (2026-08-12): `env -i PATH=/usr/bin:/bin bash -c 'command -v aws'` finds nothing.
# Fixed here rather than in the crontab so it holds for every host regardless of how
# the cron entry was installed.
# BACKUP_CLI_PATH exists so a host that installs the CLIs elsewhere can say so,
# and so the bats suite can simulate a host without them: prepending a fixed
# /usr/local/bin would otherwise make "aws is missing" untestable on any runner
# that ships the AWS CLI there — which ubuntu-latest does.
export PATH="${BACKUP_CLI_PATH:-/usr/local/bin:/usr/local/sbin}:${PATH}"

# ─────────────────────────────────────────────
# Error trap — notify on unexpected failure
# ─────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SECRETS_DIR="${SCRIPT_DIR}/../secrets"

# Sourced BEFORE the ERR trap is armed, not merely before first use: on_error()
# calls notify_webhook(), so a failure between the trap and a later source would
# run the handler against an undefined function and lose the notification —
# breaking the failure path is the one bug this file cannot afford (#3669).
# shellcheck source=lib/notify.sh
source "${SCRIPT_DIR}/lib/notify.sh"

trap 'on_error $LINENO' ERR

on_error() {
  local exit_code=$?
  local line="${1:-unknown}"
  log "ERROR" "Backup failed at line ${line} — exit code: ${exit_code}"
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
# Provides SLACK_WEBHOOK_URL — the fallback channel. Requiring a second copy of
# the same URL under a backup-specific name is how the first one stayed empty.
load_secret_file "${SECRETS_DIR}/monitoring.secret"

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

# S3/R2 — map from backup.secret variable names
S3_ENDPOINT="${S3_BACKUP_ENDPOINT:-}"
S3_BUCKET_NAME="${S3_BACKUP_BUCKET_NAME:-meepleai-backups}"
export AWS_ACCESS_KEY_ID="${S3_BACKUP_ACCESS_KEY:-}"
export AWS_SECRET_ACCESS_KEY="${S3_BACKUP_SECRET_KEY:-}"
# Accept S3_BACKUP_REGION for consistency with the variables above; S3_REGION stays
# supported because existing secret files may already use it. Default "auto" is a
# Cloudflare R2 convention — AWS S3 rejects it, so it is validated before use.
S3_REGION="${S3_BACKUP_REGION:-${S3_REGION:-auto}}"

# Tracks whether the cross-provider copy actually happened, so the final summary can
# say so. Before #3669 the script logged "All backups completed successfully" even
# when the upload had been skipped — the offsite copy had been off for months and
# every run still reported success.
OFFSITE_STATUS="not-attempted"

# age public key ("age1...") used to encrypt the offsite copy. The matching PRIVATE
# key must NOT live on this host — storing it here would make the encryption
# pointless — and without it the offsite backups cannot be restored. Keep it in the
# password manager alongside the DR runbook.
BACKUP_AGE_RECIPIENT="${BACKUP_AGE_RECIPIENT:-}"

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

  # Trigger a synchronous save (use REDISCLI_AUTH env var to avoid password in process list)
  docker exec ${REDIS_PASSWORD:+-e REDISCLI_AUTH="$REDIS_PASSWORD"} "${REDIS_CONTAINER}" redis-cli BGSAVE
  log "INFO" "Redis BGSAVE triggered — waiting for completion..."

  # Poll until save completes (max 60 seconds)
  local max_wait=60
  local waited=0
  while true; do
    local last_save_status
    last_save_status=$(docker exec ${REDIS_PASSWORD:+-e REDISCLI_AUTH="$REDIS_PASSWORD"} "${REDIS_CONTAINER}" redis-cli LASTSAVE 2>/dev/null || echo "0")
    local in_progress
    in_progress=$(docker exec ${REDIS_PASSWORD:+-e REDISCLI_AUTH="$REDIS_PASSWORD"} "${REDIS_CONTAINER}" redis-cli INFO persistence \
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
    # WARN, not INFO: this is the difference between "backups exist" and "backups
    # survive losing this provider". Both the backups and production live in the same
    # Hetzner account, so with the upload off there is no cross-provider redundancy
    # at all — an incident on the account loses them together (#3669).
    log "WARN" "OFFSITE COPY NOT MADE — S3_BACKUP_ENABLED != true. Backups exist only on this host/provider."
    OFFSITE_STATUS="disabled"
    return 0
  fi

  if [[ -z "${S3_ENDPOINT}" ]]; then
    log "ERROR" "S3_BACKUP_ENABLED=true but S3_BACKUP_ENDPOINT is empty — cannot upload"
    OFFSITE_STATUS="failed"
    return 1
  fi

  if ! command -v aws >/dev/null 2>&1; then
    log "ERROR" "S3_BACKUP_ENABLED=true but the aws CLI is not installed. Local backups were written; the offsite copy was NOT. Install it (e.g. apt-get install -y awscli) and re-run."
    OFFSITE_STATUS="failed"
    return 1
  fi

  # "auto" is a Cloudflare R2 convention. AWS S3 rejects it, and the resulting error
  # names neither the variable nor the file it comes from.
  if [[ "${S3_REGION}" == "auto" && "${S3_ENDPOINT}" == *"amazonaws.com"* ]]; then
    log "ERROR" "S3_REGION=auto is not valid for AWS S3. Set S3_BACKUP_REGION to the bucket's real region (e.g. eu-central-1) in backup.secret."
    OFFSITE_STATUS="failed"
    return 1
  fi

  # Client-side encryption before the copy leaves the host (#3669).
  #
  # Only the OFFSITE copy is encrypted; the local one stays as-is. That is deliberate:
  # backup-restore-test.sh reads the local backups monthly, so encrypting them would
  # either need the private key on this host — which defeats the point, since anyone
  # who takes the host takes the key — or break the only restore test there is. Local
  # backups share a trust boundary with the database; the offsite copy does not.
  #
  # Fails closed: with no recipient configured we do NOT fall back to uploading
  # plaintext. S3 server-side encryption protects the disks, not the bucket's contents
  # from whoever holds a key to it.
  if [[ -z "${BACKUP_AGE_RECIPIENT}" ]]; then
    log "ERROR" "S3_BACKUP_ENABLED=true but BACKUP_AGE_RECIPIENT is empty. Refusing to upload unencrypted database dumps — set the age public key in backup.secret."
    OFFSITE_STATUS="failed"
    return 1
  fi

  if ! command -v age >/dev/null 2>&1; then
    log "ERROR" "BACKUP_AGE_RECIPIENT is set but the age binary is missing. Local backups were written; nothing was uploaded. Install it (apt-get install -y age) and re-run."
    OFFSITE_STATUS="failed"
    return 1
  fi

  local offsite_dir="${BACKUP_BASE_DIR}/.offsite-${TIMESTAMP}"
  # Staged outside BACKUP_DIR so the encrypted copies never land among the local
  # artifacts, where the next run's sync or the restore test would trip over them.
  mkdir -p "${offsite_dir}"
  # EXIT, not RETURN: a RETURN trap does not fire when errexit unwinds the script (e.g.
  # age failing on one file), which left the staging copy on disk.
  # shellcheck disable=SC2064
  trap "rm -rf '${offsite_dir}'" EXIT

  log "INFO" "Encrypting backup for offsite copy (age)..."

  local expected
  expected=$(find "${BACKUP_DIR}" -type f | wc -l)
  if [[ "${expected}" -eq 0 ]]; then
    log "ERROR" "No files found under ${BACKUP_DIR} — refusing to report an empty offsite copy as complete."
    OFFSITE_STATUS="failed"
    return 1
  fi

  local f rel
  while IFS= read -r -d '' f; do
    rel="${f#"${BACKUP_DIR}/"}"
    mkdir -p "${offsite_dir}/$(dirname "${rel}")"
    age -r "${BACKUP_AGE_RECIPIENT}" -o "${offsite_dir}/${rel}.age" "${f}"
  done < <(find "${BACKUP_DIR}" -type f -print0)

  # Count what was actually produced instead of trusting the loop to have run.
  # errexit is supposed to abort on a failed `age`, but relying on that makes the
  # guarantee implicit: a loop that iterates zero times (or partially) would upload
  # an empty or truncated tree and still report "upload complete" — the worst possible
  # outcome here, because you would believe an encrypted offsite copy exists. Assert
  # the invariant rather than inferring it from control flow (#3669).
  local produced
  produced=$(find "${offsite_dir}" -type f -name '*.age' | wc -l)
  if [[ "${produced}" -ne "${expected}" ]]; then
    log "ERROR" "Encryption produced ${produced} of ${expected} expected files — refusing to upload a partial offsite copy."
    OFFSITE_STATUS="failed"
    return 1
  fi
  log "INFO" "Encrypted ${produced}/${expected} files."

  log "INFO" "Uploading encrypted backup to S3/R2 bucket: ${S3_BUCKET_NAME} (region ${S3_REGION})..."

  local s3_prefix="s3://${S3_BUCKET_NAME}/${TIMESTAMP}/"

  aws s3 sync "${offsite_dir}/" "${s3_prefix}" \
    --endpoint-url "${S3_ENDPOINT}" \
    --region "${S3_REGION}" \
    --storage-class STANDARD \
    --no-progress

  OFFSITE_STATUS="uploaded"
  log "INFO" "S3/R2 upload complete: ${s3_prefix}"
}

# ─────────────────────────────────────────────
# 5. Clean old R2/S3 backups
# ─────────────────────────────────────────────
clean_s3_backups() {
  if [[ "${S3_BACKUP_ENABLED}" != "true" ]]; then
    return 0
  fi

  if [[ -z "${S3_ENDPOINT}" ]]; then
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

  # The summary names the destinations actually written. "Successfully" used to be
  # unconditional, so a run that had silently skipped the offsite copy read exactly
  # like a healthy one — which is how the copy stayed off for months without anyone
  # noticing (#3669). Absence of a step must not look like success.
  if [[ "${OFFSITE_STATUS}" == "uploaded" ]]; then
    log "INFO" "Backup complete — local: ${BACKUP_DIR} | offsite: ${S3_BUCKET_NAME}"
    notify_webhook "success" "Backup completed (local + offsite): ${TIMESTAMP}"
  else
    log "WARN" "Backup complete WITHOUT offsite copy — local only: ${BACKUP_DIR} (offsite: ${OFFSITE_STATUS})"
    notify_webhook "degraded" "Backup completed but offsite copy is ${OFFSITE_STATUS}: ${TIMESTAMP}"
  fi
}

main
