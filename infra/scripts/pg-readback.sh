#!/usr/bin/env bash
# pg-readback.sh — DB-direct smoke read-back for migration validation (G1.C2)
#
# Validates that the latest EF Core migration has been applied to the staging DB.
# Requires SSH tunnel to staging postgres (default: localhost:25432).
#
# Usage:
#   make work          # opens tunnel
#   bash infra/scripts/pg-readback.sh
#
# Or with explicit connection:
#   PG_HOST=localhost PG_PORT=25432 PG_USER=meepleai PG_DB=meepleai bash pg-readback.sh
#
# Exit code: 0 on success, 1 on failure.
#
# Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G1.C2
# Added per Fix #1 from spec-panel review (2026-05-05).

set -euo pipefail

PG_HOST="${PG_HOST:-localhost}"
PG_PORT="${PG_PORT:-25432}"
PG_USER="${PG_USER:-meepleai}"
PG_DB="${PG_DB:-meepleai}"
PGPASSWORD="${PGPASSWORD:-${POSTGRES_PASSWORD:-}}"

log() { echo "[$(date '+%H:%M:%S')] $*"; }
fail() { echo "❌ $*" >&2; exit 1; }

if ! command -v psql >/dev/null; then
  fail "psql not found in PATH (install postgresql-client)"
fi

if [ -z "$PGPASSWORD" ]; then
  fail "PGPASSWORD or POSTGRES_PASSWORD must be set (read from infra/secrets/postgres.secret)"
fi

export PGPASSWORD

# ─────────────────────────────────────────────
# Connectivity check
# ─────────────────────────────────────────────
log "🔗 Connecting to ${PG_USER}@${PG_HOST}:${PG_PORT}/${PG_DB}"
if ! psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -tAc "SELECT 1" >/dev/null 2>&1; then
  fail "Cannot connect — verify tunnel is open (make tunnel-status) and credentials"
fi
log "✅ Connected"

# ─────────────────────────────────────────────
# Migration history check (G1.C2)
# ─────────────────────────────────────────────
log "📋 Checking __EFMigrationsHistory..."
MIGRATION_COUNT=$(psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -tAc \
  'SELECT COUNT(*) FROM "__EFMigrationsHistory"' 2>/dev/null || echo "0")

if ! [[ "$MIGRATION_COUNT" =~ ^[0-9]+$ ]] || [ "$MIGRATION_COUNT" -lt 1 ]; then
  fail "__EFMigrationsHistory has 0 rows — migrations have not been applied"
fi

log "✅ ${MIGRATION_COUNT} migrations applied"

LATEST_MIGRATION=$(psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -tAc \
  'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1' 2>/dev/null || echo "")

if [ -z "$LATEST_MIGRATION" ]; then
  fail "Could not read latest migration ID"
fi

log "✅ Latest migration: $LATEST_MIGRATION"

# ─────────────────────────────────────────────
# Smoke read-back queries (sanity, optional)
# Identifiers are lowercase per EF Core migrations
# (MeepleAiDbContextModelSnapshot: ToTable("users"), ToTable("games")).
# ─────────────────────────────────────────────
log "📊 Smoke read-back queries..."
for table in users games; do
  COUNT=$(psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -tAc \
    "SELECT COUNT(*) FROM $table" 2>/dev/null || echo "ERROR")
  if [[ "$COUNT" =~ ^[0-9]+$ ]] && [ "$COUNT" -gt 0 ]; then
    log "  ✅ $table: $COUNT rows"
  else
    log "  ⚠️  $table: got '$COUNT' (expected positive integer)"
  fi
done

# Sessions accepted >=0 per Fix #3 (legitimate empty on fresh single-tester)
# Table is "GameSessions" (PascalCase quoted) per EF migration
SESSIONS_COUNT=$(psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -tAc \
  'SELECT COUNT(*) FROM "GameSessions"' 2>/dev/null || echo "ERROR")
if [[ "$SESSIONS_COUNT" =~ ^[0-9]+$ ]]; then
  log "  ✅ GameSessions: $SESSIONS_COUNT rows (>=0 accepted)"
else
  fail "GameSessions query failed (got '$SESSIONS_COUNT')"
fi

log "✅ pg-readback PASSED"
