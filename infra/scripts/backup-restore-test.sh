#!/usr/bin/env bash
# backup-restore-test.sh — Test backup restorability by loading into a temporary PostgreSQL container
# Runs on Linux (Ubuntu, ARM64)

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
SECRETS_DIR="${SECRETS_DIR:-${SCRIPT_DIR}/../secrets}"

# This script had no notification path at all. It ran monthly from cron, failed
# on 2026-06-01, 2026-07-01 and 2026-08-01, and wrote each failure to a log file
# nobody opens — so "the backups are restorable" stayed an assumption for three
# months while the only thing that could have checked it was red (#3669).
# shellcheck source=lib/notify.sh
source "${SCRIPT_DIR}/lib/notify.sh"

for f in backup.secret monitoring.secret; do
  if [[ -f "${SECRETS_DIR}/${f}" ]]; then
    set -o allexport
    # shellcheck disable=SC1090
    source "${SECRETS_DIR}/${f}"
    set +o allexport
  fi
done

BACKUP_DIR="${BACKUP_DIR:-/backups/meepleai}"
TEMP_CONTAINER="meepleai-restore-test"
TEMP_PORT=5433
DB_USER="meepleai"
PG_IMAGE="pgvector/pgvector:pg16"
WAIT_TIMEOUT=30

# DB_NAME is NOT hardcoded any more. It used to be "meepleai"; the database on
# staging is "meepleai_staging", so every verification query ran against a
# database that does not exist and returned ERROR. That single wrong literal is
# why all three monthly runs failed — and the DR runbook repeated the same claim,
# which is the command an operator would paste during an actual disaster.
# The name is discovered from the restored cluster instead: asking the dump what
# it contains is the honest question for a restore test, and it survives the next
# environment that names its database differently.
DB_NAME=""

fail() {
  echo "ERROR: $*" >&2
  notify_webhook "failure" "backup-restore-test FAILED on $(hostname): $*"
  exit 1
}

# ─── Cleanup ──────────────────────────────────────────────────────────────────

cleanup() {
  if docker ps -a --format '{{.Names}}' | grep -q "^${TEMP_CONTAINER}$"; then
    echo "Cleaning up temp container: ${TEMP_CONTAINER}"
    # -v, not just -f: the pgvector image declares a VOLUME for PGDATA, so a
    # container started without an explicit mount gets an ANONYMOUS volume that
    # `docker rm -f` leaves behind. Seven of them had accumulated on staging,
    # 3.84 GB, on a host whose disk gate is 15 GB free (#3669).
    docker rm -fv "${TEMP_CONTAINER}" > /dev/null 2>&1 || true
  fi
}

trap cleanup EXIT

# ─── Find latest backup ───────────────────────────────────────────────────────

echo "=== MeepleAI Backup Restore Test ==="
echo "Backup directory: ${BACKUP_DIR}"

if [[ ! -d "${BACKUP_DIR}" ]]; then
  echo "ERROR: Backup directory ${BACKUP_DIR} does not exist" >&2
  exit 1
fi

LATEST_BACKUP=$(ls -1d "${BACKUP_DIR}"/*/ 2>/dev/null | sort -r | head -1)

if [[ -z "${LATEST_BACKUP}" ]]; then
  echo "ERROR: No backup directories found in ${BACKUP_DIR}" >&2
  exit 1
fi

LATEST_BACKUP="${LATEST_BACKUP%/}"
echo "Latest backup: ${LATEST_BACKUP}"

# ─── Check postgres.sql.gz exists ────────────────────────────────────────────

PG_FILE="${LATEST_BACKUP}/postgres.sql.gz"

if [[ ! -f "${PG_FILE}" ]]; then
  echo "ERROR: postgres.sql.gz not found in ${LATEST_BACKUP}" >&2
  exit 1
fi

echo "Found backup file: ${PG_FILE} ($(du -sh "${PG_FILE}" | cut -f1))"

# ─── Remove any previous temp container ──────────────────────────────────────

if docker ps -a --format '{{.Names}}' | grep -q "^${TEMP_CONTAINER}$"; then
  echo "Removing previous temp container: ${TEMP_CONTAINER}"
  docker rm -fv "${TEMP_CONTAINER}" > /dev/null
fi

# ─── Start pgvector/pgvector:pg16 container ──────────────────────────────────

echo "Starting temporary PostgreSQL container (${PG_IMAGE}) on port ${TEMP_PORT}..."

docker run -d \
  --name "${TEMP_CONTAINER}" \
  -e POSTGRES_USER="${DB_USER}" \
  -e POSTGRES_PASSWORD="restore_test_pass" \
  -e POSTGRES_DB=postgres \
  -p "${TEMP_PORT}:5432" \
  "${PG_IMAGE}" > /dev/null

# ─── Wait up to 30s for pg_isready ───────────────────────────────────────────

echo "Waiting for PostgreSQL to be ready (timeout: ${WAIT_TIMEOUT}s)..."

ELAPSED=0
until docker exec "${TEMP_CONTAINER}" pg_isready -U "${DB_USER}" -d postgres -q 2>/dev/null; do
  if [[ ${ELAPSED} -ge ${WAIT_TIMEOUT} ]]; then
    echo "ERROR: PostgreSQL not ready after ${WAIT_TIMEOUT} seconds" >&2
    exit 1
  fi
  sleep 1
  ELAPSED=$((ELAPSED + 1))
done

echo "PostgreSQL ready after ${ELAPSED}s"


# ─── Restore backup ──────────────────────────────────────────────────────────

echo "Restoring backup from ${PG_FILE}..."

RESTORE_START=$(date +%s)

# Captured, not discarded. psql without ON_ERROR_STOP exits 0 even when
# individual statements fail, so the previous version could not tell a clean
# restore from one that dropped half its rows — and the 2026-08-01 run did log
# foreign-key violations while still reporting "Restore completed".
RESTORE_LOG="$(mktemp)"
gunzip -c "${PG_FILE}" | docker exec -i "${TEMP_CONTAINER}" psql -U "${DB_USER}" -d postgres -q \
  > "${RESTORE_LOG}" 2>&1

RESTORE_END=$(date +%s)
RESTORE_TIME=$((RESTORE_END - RESTORE_START))

echo "Restore completed in ${RESTORE_TIME}s"

# ─── Restore error analysis ──────────────────────────────────────────────────
#
# Three classes, because they mean three different things and used to be one
# undifferentiated silence:
#
#   benign  — the temp container is created with POSTGRES_USER=meepleai, so the
#             dump's own CREATE ROLE collides with it. Expected, always 1.
#   fk      — "ADD CONSTRAINT ... FOREIGN KEY" refused because the referenced
#             rows are missing. The backup is NOT at fault here: the dump
#             faithfully reproduces a SOURCE database that already violates its
#             own constraints. Measured on staging 2026-08-25: 13 constraints,
#             11,446 orphaned rows, against constraints pg_constraint reports as
#             convalidated=true — only possible if the parents were removed with
#             FK triggers disabled (session_replication_role, a bulk relink).
#             It still matters: a database restored from this dump comes back
#             MISSING those 13 foreign keys. Hence "degraded", not "failure" —
#             failing here would blame the backup pipeline for a data defect and
#             leave a permanently red monthly gate, which is how the previous
#             three red runs stopped being read.
#   other   — anything else. The dump did not load; that IS a backup failure.

eval "$(awk '
  /^ERROR:/                                                                     { t++ }
  /^ERROR:.*role ".*" already exists/                                           { b++; next }
  /^ERROR:[[:space:]]+insert or update on table .* violates foreign key constraint/ { f++; next }
  /^ERROR:/                                                                     { o++ }
  END { printf "TOTAL_ERRORS=%d\nBENIGN_ERRORS=%d\nFK_ERRORS=%d\nOTHER_ERRORS=%d\n", t+0, b+0, f+0, o+0 }
' "${RESTORE_LOG}")"

echo "  psql errors during restore : ${TOTAL_ERRORS} total (${BENIGN_ERRORS} benign, ${FK_ERRORS} FK-validation, ${OTHER_ERRORS} other)"

if [[ "${OTHER_ERRORS}" -gt 0 ]]; then
  echo ""
  echo "--- first 20 unexpected errors ---"
  grep '^ERROR:' "${RESTORE_LOG}" \
    | grep -vE 'role ".*" already exists' \
    | grep -vE '^ERROR:[[:space:]]+insert or update on table .* violates foreign key constraint' \
    | head -20
  echo ""
  rm -f "${RESTORE_LOG}"
  fail "restore produced ${OTHER_ERRORS} unexpected psql errors — the dump did not load cleanly"
fi

RESTORE_DEGRADED=0
DEGRADED_REASON=""

if [[ "${FK_ERRORS}" -gt 0 ]]; then
  RESTORE_DEGRADED=1
  DEGRADED_REASON="${FK_ERRORS} foreign-key constraints could not be applied — the SOURCE database contains rows that violate them, so a restored copy comes back without those constraints"
  echo ""
  echo "⚠️  ${FK_ERRORS} foreign-key constraints were NOT created during restore."
  echo "    The data loaded; the constraints did not. This is a defect in the SOURCE"
  echo "    database, faithfully reproduced by the backup — not a backup failure."
  echo "    Affected constraints:"
  grep -E '^ERROR:[[:space:]]+insert or update on table .* violates foreign key constraint' "${RESTORE_LOG}" \
    | sed -E 's/.*violates foreign key constraint "([^"]+)".*/      - \1/' | sort -u | head -20
  echo "    Find the orphans with the query in infra/hetzner/disaster-recovery.md"
  echo "    (§ Foreign-key drift between the source database and a restored copy)."
  echo ""
fi

rm -f "${RESTORE_LOG}"

# ─── Discover the restored application database ──────────────────────────────
#
# Ask the cluster instead of assuming a name. The largest non-template database
# other than "postgres" is the application one; pg_dumpall recreates it under
# whatever name the source used — "meepleai_staging" on the only deployed
# environment there is, and whatever a future one happens to call it.

DB_NAME=$(docker exec "${TEMP_CONTAINER}" psql -U "${DB_USER}" -d postgres -t -A -c \
  "SELECT datname FROM pg_database
    WHERE datistemplate = false AND datname <> 'postgres'
    ORDER BY pg_database_size(datname) DESC LIMIT 1;" 2>/dev/null | tr -d '[:space:]' || true)

if [[ -z "${DB_NAME}" ]]; then
  fail "the dump restored no application database — only 'postgres' exists in the cluster"
fi

echo "Restored application database: ${DB_NAME}"

# ─── Verify data ─────────────────────────────────────────────────────────────

echo "Verifying restored data..."

q() {
  docker exec "${TEMP_CONTAINER}" psql -U "${DB_USER}" -d "${DB_NAME}" -t -A -c "$1" 2>/dev/null \
    | tr -d '[:space:]' || echo "ERROR"
}

SCHEMA_COUNT=$(q "SELECT COUNT(*) FROM information_schema.schemata
   WHERE schema_name NOT IN ('pg_catalog','information_schema','pg_toast','public')
     AND schema_name NOT LIKE 'pg_toast_%'
     AND schema_name NOT LIKE 'pg_temp_%';")

TABLE_COUNT=$(q "SELECT COUNT(*) FROM information_schema.tables
   WHERE table_schema = 'public' AND table_type = 'BASE TABLE';")

USERS_COUNT=$(q "SELECT COUNT(*) FROM users;")

echo ""
echo "--- Data Verification ---"
echo "  database                   : ${DB_NAME}"
echo "  custom schema count        : ${SCHEMA_COUNT} (expected >= 5)"
echo "  public base tables         : ${TABLE_COUNT} (expected >= 50)"
echo "  users count                : ${USERS_COUNT} (expected > 0)"

# Structural assertions first: they describe the shape of any MeepleAI database
# and do not rot when one table is renamed. The previous version asserted a table
# called "games", which exists in no schema — the catalog table is "shared_games".
# A check naming an object that never existed cannot pass, and nobody noticed
# because nothing read its result.

[[ "${SCHEMA_COUNT}" =~ ^[0-9]+$ ]] || fail "could not determine the custom schema count"
[[ ${SCHEMA_COUNT} -ge 5 ]] || fail "custom schema count ${SCHEMA_COUNT} is below the expected minimum of 5"

[[ "${TABLE_COUNT}" =~ ^[0-9]+$ ]] || fail "could not determine the public table count"
[[ ${TABLE_COUNT} -ge 50 ]] || fail "public base table count ${TABLE_COUNT} is below the expected minimum of 50"

echo "  structural checks          : PASSED"

# ─── Smoke read-back (only with --with-smoke-readback flag) ──────────────────

if [[ " $* " == *" --with-smoke-readback "* ]]; then
  echo ""
  echo "--- Smoke read-back ---"

  SMOKE_FAIL=0

  # users is the one table that cannot legitimately be empty in a deployed
  # environment: without a row here nobody can log in.
  if [[ "${USERS_COUNT}" =~ ^[0-9]+$ ]] && [ "${USERS_COUNT}" -gt 0 ]; then
    echo "  ✅ users: ${USERS_COUNT} rows"
  else
    echo "  ❌ users: got '${USERS_COUNT}' (expected a positive integer)"
    SMOKE_FAIL=1
  fi

  # Tables that may legitimately be empty are read for queryability, not volume:
  # the assertion is that the relation restored and answers, not that it has rows.
  for t in 'shared_games' '"GameSessions"'; do
    n=$(q "SELECT COUNT(*) FROM ${t};")
    if [[ "$n" =~ ^[0-9]+$ ]]; then
      echo "  ✅ ${t}: ${n} rows (>=0 accepted, table may be empty)"
    else
      echo "  ❌ ${t}: query failed (got '$n')"
      SMOKE_FAIL=1
    fi
  done

  if [ "$SMOKE_FAIL" -ne 0 ]; then
    echo ""
    fail "restore OK but smoke read-back FAILED"
  fi

  echo "  ✅ Restore + smoke read-back PASSED"
fi

# ─── Offsite copy read-back ──────────────────────────────────────────────────
#
# #3669 DoD 5 asks for a restore test of the copy on the second provider, and
# this script only ever read the LOCAL plaintext backup — the one copy that does
# not survive losing the Hetzner account.
#
# What is checkable from here is bounded by design: the age PRIVATE key is
# deliberately not on this host, so the object cannot be decrypted here. A ranged
# GET of the first 64 bytes proves the object exists, that these credentials can
# read it, and that it really is an age file — for a few bytes of egress instead
# of 174 MB. It does NOT prove the ciphertext decrypts; only the manual drill in
# the DR runbook, run with the key from the password manager, proves that.

if [[ "${S3_BACKUP_ENABLED:-false}" == "true" ]]; then
  echo ""
  echo "--- Offsite copy read-back ---"

  OFFSITE_PREFIX="$(basename "${LATEST_BACKUP}")"
  OFFSITE_KEY="${OFFSITE_PREFIX}/postgres.sql.gz.age"
  OFFSITE_BUCKET="${S3_BACKUP_BUCKET_NAME:-meepleai-backups}"
  OFFSITE_REGION="${S3_BACKUP_REGION:-${S3_REGION:-auto}}"

  if ! command -v aws >/dev/null 2>&1; then
    fail "S3_BACKUP_ENABLED=true but the aws CLI is missing — the offsite copy cannot be read back"
  fi

  export AWS_ACCESS_KEY_ID="${S3_BACKUP_ACCESS_KEY:-}"
  export AWS_SECRET_ACCESS_KEY="${S3_BACKUP_SECRET_KEY:-}"

  HEAD_FILE="$(mktemp)"
  AWS_ERR="$(mktemp)"
  GET_RC=0
  aws s3api get-object \
    --bucket "${OFFSITE_BUCKET}" \
    --key "${OFFSITE_KEY}" \
    --range "bytes=0-63" \
    ${S3_BACKUP_ENDPOINT:+--endpoint-url "${S3_BACKUP_ENDPOINT}"} \
    --region "${OFFSITE_REGION}" \
    "${HEAD_FILE}" > /dev/null 2>"${AWS_ERR}" || GET_RC=$?

  if [[ ${GET_RC} -ne 0 ]]; then
    echo "  aws error: $(cat "${AWS_ERR}")"
    rm -f "${HEAD_FILE}" "${AWS_ERR}"
    fail "could not read s3://${OFFSITE_BUCKET}/${OFFSITE_KEY} — the offsite copy of this backup is unreadable"
  fi

  MAGIC="$(head -c 21 "${HEAD_FILE}")"
  rm -f "${HEAD_FILE}" "${AWS_ERR}"

  if [[ "${MAGIC}" != "age-encryption.org/v1" ]]; then
    fail "s3://${OFFSITE_BUCKET}/${OFFSITE_KEY} is not an age file (header: '${MAGIC}') — uploaded unencrypted, or truncated"
  fi

  echo "  ✅ s3://${OFFSITE_BUCKET}/${OFFSITE_KEY} — readable, valid age header"
  echo "  ℹ️  this does NOT prove the ciphertext decrypts: the private key is off-host by design."
  echo "     The decrypt+restore drill is in infra/hetzner/disaster-recovery.md and is manual."
else
  echo ""
  echo "--- Offsite copy read-back: SKIPPED (S3_BACKUP_ENABLED != true) ---"
  echo "  ⚠️  backups exist only on this host/provider."
fi

# ─── Done ─────────────────────────────────────────────────────────────────────

echo ""
if [[ "${RESTORE_DEGRADED}" -eq 1 ]]; then
  echo "=== Restore test DEGRADED ⚠️  (${RESTORE_TIME}s) ==="
  echo "    ${DEGRADED_REASON}"
  notify_webhook "degraded" "backup-restore-test on $(hostname): ${DB_NAME} restored from $(basename "${LATEST_BACKUP}") in ${RESTORE_TIME}s, but ${DEGRADED_REASON}"
else
  echo "=== Restore test PASSED ✅ (${RESTORE_TIME}s) ==="
  notify_webhook "success" "backup-restore-test PASSED on $(hostname): ${DB_NAME} restored from $(basename "${LATEST_BACKUP}") in ${RESTORE_TIME}s"
fi
