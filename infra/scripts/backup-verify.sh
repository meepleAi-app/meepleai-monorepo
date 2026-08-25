#!/usr/bin/env bash
# backup-verify.sh — MeepleAI backup integrity verification
# Checks freshness, PostgreSQL dump integrity, PDF uploads tar.gz, and R2 sync
# Linux-only (Ubuntu/ARM64): uses stat -c%Y / stat -c%s

set -euo pipefail

# cron gives user jobs a minimal PATH (/usr/bin:/bin) and this crontab sets none.
# The AWS CLI v2 installer puts `aws` in /usr/local/bin, so at 03:30 it is simply
# absent — while working perfectly from an interactive shell. backup.sh exports
# this already (#3693); this script did not, and the omission was invisible
# because the old offsite check read stderr as success: every nightly
# "PASS - R2/S3 sync" was produced by the string "bash: aws: command not found".
# Verified on staging 2026-08-25 with `env -i PATH=/usr/bin:/bin` (#3669).
# BACKUP_CLI_PATH exists so a host that installs the CLIs elsewhere can say so,
# and so the bats suite can simulate a host without them: prepending a fixed
# /usr/local/bin would otherwise make "aws is missing" untestable on any runner
# that ships the AWS CLI there — which ubuntu-latest does.
export PATH="${BACKUP_CLI_PATH:-/usr/local/bin:/usr/local/sbin}:${PATH}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Both overridable so the bats suite can point them at a temp tree. Without the
# SECRETS_DIR override the tests would source whatever backup.secret the machine
# happens to have — green on a dev laptop, different in CI (#3669).
SECRETS_DIR="${SECRETS_DIR:-$(cd "${SCRIPT_DIR}/../secrets" 2>/dev/null && pwd || echo "${SCRIPT_DIR}/../secrets")}"
BACKUP_ROOT="${BACKUP_ROOT:-/backups/meepleai}"
EXIT_CODE=0

# shellcheck source=lib/notify.sh
source "${SCRIPT_DIR}/lib/notify.sh"

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
# Holds SLACK_WEBHOOK_URL, the channel Alertmanager already uses.
load_secrets "${SECRETS_DIR}/monitoring.secret"
# SMTP for the email fallback: the Slack webhook on staging is revoked, so the
# webhook alone is not a channel.
load_secrets "${SECRETS_DIR}/email.secret"

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
# Check 4: offsite (R2/S3) copy
#
# Rewritten for #3669. The previous version was:
#
#     S3_LS_OUTPUT=$(aws s3 ls "s3://..." 2>&1 || true)
#     if [[ -n "${S3_LS_OUTPUT}" ]]; then check "... objects exist" 0
#
# `2>&1` folds stderr into the variable the success test reads, so ANY error
# message is a non-empty string and the check reported PASS. Proved against the
# staging bucket with a revoked key: `InvalidAccessKeyId` scored a PASS. Rotate
# the IAM key and the daily green survives the loss of the cross-provider copy —
# the exact failure mode this issue is about, one layer above the one it fixed.
#
# Three separate assertions now, because each catches a distinct real failure:
#   1. the command SUCCEEDED (exit code, not output length)
#   2. the postgres dump is there, encrypted (`.age`) — not "some object exists"
#   3. it is not truncated (size >= the local dump)
# ---------------------------------------------------------------------------

if [[ "${S3_BACKUP_ENABLED}" == "true" ]]; then
  log "S3_BACKUP_ENABLED=true — verifying the offsite copy for prefix: ${BACKUP_DATE}"

  # A backup nobody can be told about is a backup nobody acts on. On staging
  # BACKUP_WEBHOOK_URL was empty for the whole life of the offsite copy, so the
  # "degraded" webhook added by PR #3690 delivered nothing (#3669 DoD 3).
  # Configured is not the same as working. The webhook URL in monitoring.secret
  # was present, non-empty and dead: Slack answers HTTP 404 "no_service" because
  # it had been revoked. Probing costs one request and posts no message, so it
  # can run daily — and it catches the revocation on day one instead of on the
  # night the backup fails (#3669).
  if notify_has_channel; then
    PROBE_RC=0
    notify_probe_channel || PROBE_RC=$?
    case "${PROBE_RC}" in
      0) check "notification channel configured and reachable" 0 ;;
      1)
        # A dead webhook with a working email fallback is a configuration smell,
        # not a broken alerting path — and a check that goes red daily for
        # something that still delivers is how a check stops being read.
        if notify_email_configured; then
          log "⚠️  WARN  — the webhook is REVOKED (answered 404/410); alerts fall back to email ($(resolve_notify_email)). Remove or replace the dead URL."
          check "notification channel deliverable (via the email fallback)" 0
        else
          check "notification channel is REVOKED (webhook answered 404/410) and there is no email fallback — alerts would go nowhere" 1
        fi
        ;;
      *) check "notification channel configured (reachability unknown — transient network error)" 0 ;;
    esac
  else
    check "notification channel configured (BACKUP_WEBHOOK_URL, SLACK_WEBHOOK_URL, or SMTP + BACKUP_ALERT_EMAIL_TO) — failures would be invisible" 1
  fi

  S3_ENDPOINT="${S3_BACKUP_ENDPOINT:-}"
  S3_BUCKET_NAME="${S3_BACKUP_BUCKET_NAME:-}"
  AWS_ACCESS_KEY_ID="${S3_BACKUP_ACCESS_KEY:-}"
  AWS_SECRET_ACCESS_KEY="${S3_BACKUP_SECRET_KEY:-}"
  # backup.sh reads S3_BACKUP_REGION; this script used to read only S3_REGION and
  # fall back to "auto". Staging sets just the former, so the two scripts
  # addressed the same bucket with different regions — harmless only because the
  # endpoint carries the region today.
  S3_REGION="${S3_BACKUP_REGION:-${S3_REGION:-auto}}"

  export AWS_ACCESS_KEY_ID
  export AWS_SECRET_ACCESS_KEY

  # The local dump this offsite copy is supposed to mirror. FILE_SIZE is set by
  # check 2 when the dump exists; 0 disables the size comparison rather than
  # inventing a threshold.
  PG_LOCAL_SIZE="${FILE_SIZE:-0}"
  OFFSITE_OBJECT="postgres.sql.gz.age"

  if [[ -z "${S3_BUCKET_NAME}" ]]; then
    check "offsite copy — S3_BACKUP_BUCKET_NAME is set" 1
  elif ! command -v aws >/dev/null 2>&1; then
    # Used to be fatal-but-silent: `aws` missing killed the script under errexit
    # part-way through, so the summary never ran.
    check "offsite copy — the aws CLI is installed (offsite copy cannot be verified without it)" 1
  else
    S3_ERR_FILE="$(mktemp)"
    S3_LS_RC=0
    if [[ -n "${S3_ENDPOINT}" ]]; then
      S3_LS_OUTPUT=$(aws s3 ls "s3://${S3_BUCKET_NAME}/${BACKUP_DATE}/"         --endpoint-url "${S3_ENDPOINT}"         --region "${S3_REGION}" 2>"${S3_ERR_FILE}") || S3_LS_RC=$?
    else
      S3_LS_OUTPUT=$(aws s3 ls "s3://${S3_BUCKET_NAME}/${BACKUP_DATE}/"         --region "${S3_REGION}" 2>"${S3_ERR_FILE}") || S3_LS_RC=$?
    fi
    S3_STDERR="$(cat "${S3_ERR_FILE}")"
    rm -f "${S3_ERR_FILE}"

    if [[ ${S3_LS_RC} -ne 0 ]]; then
      # Print the CLI's own words: "no objects" and "your key was revoked" are
      # different incidents and used to look identical from here.
      check "offsite copy — listing succeeded (aws exited ${S3_LS_RC})" 1
      log "  aws error: ${S3_STDERR:-<no stderr>}"
    else
      OFFSITE_SIZE=$(echo "${S3_LS_OUTPUT}" | awk -v n="${OFFSITE_OBJECT}" '$NF == n { print $(NF-1); exit }')

      if [[ -z "${OFFSITE_SIZE}" ]]; then
        check "offsite copy — ${OFFSITE_OBJECT} present under '${BACKUP_DATE}/'" 1
        log "  listing returned: ${S3_LS_OUTPUT:-<empty>}"
        log "  note: an object named postgres.sql.gz WITHOUT .age would mean the dump was uploaded unencrypted"
      else
        check "offsite copy — ${OFFSITE_OBJECT} present (${OFFSITE_SIZE}B)" 0

        # age never shrinks its input (no compression, ~200B of header), so an
        # object smaller than the local dump is a partial transfer.
        if [[ "${PG_LOCAL_SIZE}" -gt 0 && "${OFFSITE_SIZE}" -lt "${PG_LOCAL_SIZE}" ]]; then
          check "offsite copy — ${OFFSITE_OBJECT} is ${OFFSITE_SIZE}B, smaller than the local dump (${PG_LOCAL_SIZE}B) — truncated upload" 1
        else
          check "offsite copy — size consistent with the local dump (${OFFSITE_SIZE}B >= ${PG_LOCAL_SIZE}B)" 0
        fi
      fi
    fi
  fi
else
  log "S3_BACKUP_ENABLED=false — skipping the offsite copy check"
  log "⚠️  WARN  — backups exist only on this host/provider; losing the account loses them together"
fi

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

echo ""
if [[ ${EXIT_CODE} -eq 0 ]]; then
  log "✅ All backup checks PASSED"
else
  log "❌ One or more backup checks FAILED"
  # cron writes this to a file nobody opens. Three monthly restore-test failures
  # went unnoticed exactly that way (#3669).
  notify_webhook "failure" "backup-verify FAILED on $(hostname) — see /var/log/meepleai-backup-verify.log"
fi

exit ${EXIT_CODE}
