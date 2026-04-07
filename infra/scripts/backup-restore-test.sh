#!/usr/bin/env bash
# backup-restore-test.sh — Test backup restorability by loading into a temporary PostgreSQL container
# Runs on Linux (Ubuntu, ARM64)

set -euo pipefail

BACKUP_DIR="/backups/meepleai"
TEMP_CONTAINER="meepleai-restore-test"
TEMP_PORT=5433
DB_USER="meepleai"
DB_NAME="meepleai"
PG_IMAGE="pgvector/pgvector:pg16"
WAIT_TIMEOUT=30

# ─── Cleanup ──────────────────────────────────────────────────────────────────

cleanup() {
  if docker ps -a --format '{{.Names}}' | grep -q "^${TEMP_CONTAINER}$"; then
    echo "Cleaning up temp container: ${TEMP_CONTAINER}"
    docker rm -f "${TEMP_CONTAINER}" > /dev/null 2>&1 || true
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
  docker rm -f "${TEMP_CONTAINER}" > /dev/null
fi

# ─── Start pgvector/pgvector:pg16 container ──────────────────────────────────

echo "Starting temporary PostgreSQL container (${PG_IMAGE}) on port ${TEMP_PORT}..."

docker run -d \
  --name "${TEMP_CONTAINER}" \
  -e POSTGRES_USER="${DB_USER}" \
  -e POSTGRES_PASSWORD="restore_test_pass" \
  -e POSTGRES_DB="${DB_NAME}" \
  -p "${TEMP_PORT}:5432" \
  "${PG_IMAGE}" > /dev/null

# ─── Wait up to 30s for pg_isready ───────────────────────────────────────────

echo "Waiting for PostgreSQL to be ready (timeout: ${WAIT_TIMEOUT}s)..."

ELAPSED=0
until docker exec "${TEMP_CONTAINER}" pg_isready -U "${DB_USER}" -d "${DB_NAME}" -q 2>/dev/null; do
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

gunzip -c "${PG_FILE}" | docker exec -i "${TEMP_CONTAINER}" psql -U "${DB_USER}" -q --set ON_ERROR_STOP=1

RESTORE_END=$(date +%s)
RESTORE_TIME=$((RESTORE_END - RESTORE_START))

echo "Restore completed in ${RESTORE_TIME}s"

# ─── Verify data ─────────────────────────────────────────────────────────────

echo "Verifying restored data..."

# Query Administration.Users count
USERS_COUNT=$(docker exec "${TEMP_CONTAINER}" psql -U "${DB_USER}" -d "${DB_NAME}" -t -c \
  'SELECT COUNT(*) FROM "Administration"."Users";' 2>/dev/null | tr -d '[:space:]' || echo "ERROR")

# Query GameManagement.Games count
GAMES_COUNT=$(docker exec "${TEMP_CONTAINER}" psql -U "${DB_USER}" -d "${DB_NAME}" -t -c \
  'SELECT COUNT(*) FROM "GameManagement"."Games";' 2>/dev/null | tr -d '[:space:]' || echo "ERROR")

# Query schema count (exclude pg_catalog, information_schema, pg_toast, public)
SCHEMA_COUNT=$(docker exec "${TEMP_CONTAINER}" psql -U "${DB_USER}" -d "${DB_NAME}" -t -c \
  "SELECT COUNT(*) FROM information_schema.schemata
   WHERE schema_name NOT IN ('pg_catalog','information_schema','pg_toast','public')
     AND schema_name NOT LIKE 'pg_toast_%'
     AND schema_name NOT LIKE 'pg_temp_%';" 2>/dev/null | tr -d '[:space:]' || echo "0")

echo ""
echo "--- Data Verification ---"
echo "  Administration.Users count : ${USERS_COUNT}"
echo "  GameManagement.Games count : ${GAMES_COUNT}"
echo "  Custom schema count        : ${SCHEMA_COUNT} (expected >= 5, up to 18 bounded contexts)"

# ─── Check schema count >= 5 ─────────────────────────────────────────────────

if [[ "${SCHEMA_COUNT}" =~ ^[0-9]+$ ]]; then
  if [[ ${SCHEMA_COUNT} -lt 5 ]]; then
    echo "ERROR: Schema count ${SCHEMA_COUNT} is less than expected minimum of 5" >&2
    exit 1
  fi
  echo "  Schema count check         : PASSED (${SCHEMA_COUNT} >= 5)"
else
  echo "ERROR: Could not determine schema count" >&2
  exit 1
fi

# ─── Done ─────────────────────────────────────────────────────────────────────

echo ""
echo "=== Restore test PASSED ✅ (${RESTORE_TIME}s) ==="
