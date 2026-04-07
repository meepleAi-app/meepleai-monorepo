#!/usr/bin/env bash
# backup-verify.sh — MeepleAI backup integrity verification
# Checks freshness, PostgreSQL dump integrity, PDF uploads tar.gz, and R2 sync
# Linux-only (Ubuntu/ARM64): uses stat -c%Y / stat -c%s

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SECRETS_DIR="$(cd "${SCRIPT_DIR}/../secrets" && pwd)"
BACKUP_ROOT="/backups/meepleai"
EXIT_CODE=0

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

log() {
  echo "[$(date '+%Y-%m-%dT%H:%M:%S')] $*"
}

# check <label> <result: 0=pass, non-zero=fail>
check() {
  local label="$1"
  local result="$2"
  if [[ "$result" -eq 0 ]]; then
    log "✅ PASS — ${label}"
  else
    log "❌ FAIL — ${label}"
    EXIT_CODE=1
  fi
}

# ---------------------------------------------------------------------------
# Load secrets
# ---------------------------------------------------------------------------

load_secrets() {
  local file="$1"
  if [[ -f "$file" ]]; then
    # shellcheck disable=SC1090
    set -o allexport
    source "$file"
    set +o allexport
    log "Loaded secrets from ${file}"
  else
    log "WARNING: secrets file not found: ${file}"
  fi
}

load_secrets "${SECRETS_DIR}/backup.secret"
load_secrets "${SECRETS_DIR}/storage.secret"

# Defaults for optional vars
S3_BACKUP_ENABLED="${S3_BACKUP_ENABLED:-false}"

# ---------------------------------------------------------------------------
# Find latest backup directory
# ---------------------------------------------------------------------------

log "Scanning backup root: ${BACKUP_ROOT}"

if [[ ! -d "${BACKUP_ROOT}" ]]; then
  log "❌ FAIL — Backup root directory does not exist: ${BACKUP_ROOT}"
  exit 1
fi

LATEST_DIR=$(ls -1d "${BACKUP_ROOT}"/[0-9]* 2>/dev/null | sort -r | head -1 || true)

if [[ -z "${LATEST_DIR}" ]]; then
  log "❌ FAIL — No backup directories found in ${BACKUP_ROOT}"
  exit 1
fi

BACKUP_DATE="$(basename "${LATEST_DIR}")"
log "Latest backup directory: ${LATEST_DIR} (date: ${BACKUP_DATE})"

# ---------------------------------------------------------------------------
# Check 1: Backup freshness (< 25 hours old)
# ---------------------------------------------------------------------------

DIR_MTIME=$(stat -c%Y "${LATEST_DIR}")
NOW=$(date +%s)
AGE_SECONDS=$(( NOW - DIR_MTIME ))
MAX_AGE_SECONDS=$(( 25 * 3600 ))

log "Backup age: ${AGE_SECONDS}s (max allowed: ${MAX_AGE_SECONDS}s)"

if [[ ${AGE_SECONDS} -lt ${MAX_AGE_SECONDS} ]]; then
  check "Backup freshness (< 25 hours old, age=${AGE_SECONDS}s)" 0
else
  check "Backup freshness (< 25 hours old, age=${AGE_SECONDS}s)" 1
fi

# ---------------------------------------------------------------------------
# Check 2: PostgreSQL dump
# ---------------------------------------------------------------------------

PG_DUMP="${LATEST_DIR}/postgres.sql.gz"

# 2a: File exists
if [[ -n "${PG_DUMP}" && -f "${PG_DUMP}" ]]; then
  check "PostgreSQL dump file exists (${PG_DUMP##*/})" 0
else
  check "PostgreSQL dump file exists" 1
  log "  Skipping further PostgreSQL checks (file not found)"
  # Jump to PDF check
fi

if [[ -n "${PG_DUMP:-}" && -f "${PG_DUMP}" ]]; then
  # 2b: Size > 1KB
  FILE_SIZE=$(stat -c%s "${PG_DUMP}")
  if [[ ${FILE_SIZE} -gt 1024 ]]; then
    check "PostgreSQL dump size > 1KB (size=${FILE_SIZE}B)" 0
  else
    check "PostgreSQL dump size > 1KB (size=${FILE_SIZE}B)" 1
  fi

  # 2c: gzip integrity
  if gzip -t "${PG_DUMP}" 2>/dev/null; then
    check "PostgreSQL dump gzip integrity" 0
  else
    check "PostgreSQL dump gzip integrity" 1
  fi

  # 2d: SQL header contains "PostgreSQL database"
  HEADER=$(zcat "${PG_DUMP}" 2>/dev/null | head -5 || true)
  if echo "${HEADER}" | grep -q "PostgreSQL database"; then
    check "PostgreSQL dump SQL header valid" 0
  else
    check "PostgreSQL dump SQL header valid (expected 'PostgreSQL database' in first 5 lines)" 1
  fi
fi

# ---------------------------------------------------------------------------
# Check 3: PDF uploads tar.gz integrity
# ---------------------------------------------------------------------------

PDF_TAR=$(find "${LATEST_DIR}" -maxdepth 1 \( -name "uploads*.tar.gz" -o -name "pdf_uploads*.tar.gz" -o -name "pdfs*.tar.gz" \) 2>/dev/null | sort | head -1 || true)

if [[ -n "${PDF_TAR}" && -f "${PDF_TAR}" ]]; then
  check "PDF uploads archive exists (${PDF_TAR##*/})" 0

  if tar tzf "${PDF_TAR}" > /dev/null 2>&1; then
    check "PDF uploads tar.gz integrity" 0
  else
    check "PDF uploads tar.gz integrity" 1
  fi
else
  # Non-fatal: PDF archive may not exist if there are no uploads
  log "⚠️  WARN  — PDF uploads archive not found in ${LATEST_DIR} (non-fatal if no uploads exist)"
fi

# ---------------------------------------------------------------------------
# Check 4: R2 / S3 sync verification
# ---------------------------------------------------------------------------

if [[ "${S3_BACKUP_ENABLED}" == "true" ]]; then
  log "S3_BACKUP_ENABLED=true — verifying R2/S3 sync for date prefix: ${BACKUP_DATE}"

  # Required vars
  S3_ENDPOINT="${S3_BACKUP_ENDPOINT:-}"
  S3_BUCKET_NAME="${S3_BACKUP_BUCKET_NAME:-}"
  AWS_ACCESS_KEY_ID="${S3_BACKUP_ACCESS_KEY:-}"
  AWS_SECRET_ACCESS_KEY="${S3_BACKUP_SECRET_KEY:-}"
  S3_REGION="${S3_REGION:-auto}"

  export AWS_ACCESS_KEY_ID
  export AWS_SECRET_ACCESS_KEY

  if [[ -z "${S3_BUCKET_NAME}" ]]; then
    check "R2/S3 sync — S3_BUCKET_NAME is set" 1
  else
    S3_LS_OUTPUT=""
    if [[ -n "${S3_ENDPOINT}" ]]; then
      S3_LS_OUTPUT=$(aws s3 ls "s3://${S3_BUCKET_NAME}/${BACKUP_DATE}/" \
        --endpoint-url "${S3_ENDPOINT}" \
        --region "${S3_REGION}" 2>&1 || true)
    else
      S3_LS_OUTPUT=$(aws s3 ls "s3://${S3_BUCKET_NAME}/${BACKUP_DATE}/" \
        --region "${S3_REGION}" 2>&1 || true)
    fi

    if [[ -n "${S3_LS_OUTPUT}" ]]; then
      check "R2/S3 sync — objects exist for prefix '${BACKUP_DATE}/'" 0
    else
      check "R2/S3 sync — objects exist for prefix '${BACKUP_DATE}/'" 1
    fi
  fi
else
  log "S3_BACKUP_ENABLED=false — skipping R2/S3 sync check"
fi

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

echo ""
if [[ ${EXIT_CODE} -eq 0 ]]; then
  log "✅ All backup checks PASSED"
else
  log "❌ One or more backup checks FAILED"
fi

exit ${EXIT_CODE}
