#!/usr/bin/env bash
# restore-local.sh — Restore local dev DB from a snapshot (for make dev)
# Usage: ./restore-local.sh [snapshot-file]
# Requires: make dev running (postgres container up)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SEEDS_DIR="$SCRIPT_DIR/../seeds"

# Find snapshot file
if [ -n "${1:-}" ]; then
  SNAPSHOT="$1"
else
  SNAPSHOT=$(ls -t "$SEEDS_DIR"/staging-snapshot-*.sql 2>/dev/null | head -1)
fi

if [ -z "$SNAPSHOT" ] || [ ! -f "$SNAPSHOT" ]; then
  echo "ERROR: No snapshot file found."
  echo "Usage: $0 [snapshot-file]"
  exit 1
fi

# Read local DB credentials
DB_SECRET="$SCRIPT_DIR/../secrets/database.secret"
if [ -f "$DB_SECRET" ]; then
  PG_USER=$(grep -oP 'POSTGRES_USER=\K.*' "$DB_SECRET" || echo "meepleai")
  PG_DB=$(grep -oP 'POSTGRES_DB=\K.*' "$DB_SECRET" || echo "meepleai")
else
  PG_USER="meepleai"
  PG_DB="meepleai"
fi

echo "=== Restoring local DB from: $(basename "$SNAPSHOT") ==="
echo "  Target: $PG_USER@postgres:5432/$PG_DB"
echo "WARNING: This will OVERWRITE all data in the local database!"
read -p "Continue? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
  echo "Aborted."
  exit 0
fi

# Copy and restore
CONTAINER=$(docker ps --format '{{.Names}}' | grep postgres | head -1)
if [ -z "$CONTAINER" ]; then
  echo "ERROR: No postgres container running. Start with: make dev"
  exit 1
fi

echo "Restoring..."
docker cp "$SNAPSHOT" "$CONTAINER:/tmp/restore.sql"
docker exec "$CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -f /tmp/restore.sql 2>&1 | tail -5

# Restart API if running
API_CONTAINER=$(docker ps --format '{{.Names}}' | grep api | head -1)
if [ -n "$API_CONTAINER" ]; then
  echo "Restarting API..."
  docker restart "$API_CONTAINER"
fi

echo ""
echo "=== Local restore complete ==="
