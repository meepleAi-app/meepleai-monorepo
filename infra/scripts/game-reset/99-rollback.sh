#!/usr/bin/env bash
# Phase 1 / Step 6 of spec: restore DB from a pre-flight backup.
# Drops and recreates the target database, then pg_restore from the dump.
#
# Usage: ./99-rollback.sh <env-file> <dump-file> [--i-mean-it]

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

load_env "${1:-}"
dump_file="${2:-}"
guard_prod "${3:-}"

if [[ -z "$dump_file" ]]; then
  log_error "Usage: $0 <env-file> <dump-file> [--i-mean-it]"
  exit 64
fi
if [[ ! -f "$dump_file" ]]; then
  log_error "Dump file not found: $dump_file"
  exit 66
fi

: "${DATABASE_URL_ADMIN:?DATABASE_URL_ADMIN must be set in env file for rollback}"

log_warn "About to DROP DATABASE $DATABASE_NAME and restore from $dump_file"
log_warn "All current data in $DATABASE_NAME will be lost."

# Confirmation prompt (skipped if --i-mean-it passed, see guard_prod)
if [[ "${3:-}" != "--i-mean-it" && "${ENV_NAME:-}" != "prod" ]]; then
  read -r -p "Type the database name ($DATABASE_NAME) to confirm: " confirm
  if [[ "$confirm" != "$DATABASE_NAME" ]]; then
    log_error "Confirmation mismatch. Aborting."
    exit 65  # EX_DATAERR
  fi
fi

log_info "Terminating active connections to $DATABASE_NAME..."
psql --dbname="$DATABASE_URL_ADMIN" -c "
  SELECT pg_terminate_backend(pid)
  FROM pg_stat_activity
  WHERE datname = '$DATABASE_NAME' AND pid <> pg_backend_pid();
" > /dev/null

log_info "Dropping database $DATABASE_NAME..."
psql --dbname="$DATABASE_URL_ADMIN" -c "DROP DATABASE IF EXISTS \"$DATABASE_NAME\";"

log_info "Recreating database $DATABASE_NAME..."
psql --dbname="$DATABASE_URL_ADMIN" -c "CREATE DATABASE \"$DATABASE_NAME\";"

log_info "Restoring from $dump_file..."
pg_restore --dbname="$DATABASE_URL" --no-owner --no-acl "$dump_file"

restored_count=$(psql --dbname="$DATABASE_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;" 2>/dev/null || echo "?")
log_ok "Restore complete. pgvector_embeddings rows: $restored_count"

table_count=$(psql --dbname="$DATABASE_URL" -t -A -c "
  SELECT COUNT(*) FROM pg_tables WHERE schemaname='public';
")
log_ok "Public tables in restored DB: $table_count"
