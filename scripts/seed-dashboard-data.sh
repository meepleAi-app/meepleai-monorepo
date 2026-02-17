#!/usr/bin/env bash
# Seed dashboard test data - Issue #4576
#
# Usage:
#   ./seed-dashboard-data.sh [connection-string]
#   ./seed-dashboard-data.sh --docker

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SQL_SCRIPT="$SCRIPT_DIR/seed-dashboard-data.sql"

# Default connection
PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGDATABASE="${PGDATABASE:-meepleai}"
PGUSER="${PGUSER:-postgres}"
PGPASSWORD="${PGPASSWORD:-postgres}"

echo "🎲 Gaming Hub Dashboard - Data Seeder"
echo ""

if [ ! -f "$SQL_SCRIPT" ]; then
    echo "❌ SQL script not found: $SQL_SCRIPT"
    exit 1
fi

if [ "${1:-}" = "--docker" ]; then
    echo "🐳 Using Docker container..."

    # Check if container exists
    if ! docker ps --filter "name=meepleai-postgres" --format "{{.Names}}" | grep -q "meepleai-postgres"; then
        echo "❌ Docker container 'meepleai-postgres' not found"
        echo "Start it with: docker compose up -d postgres"
        exit 1
    fi

    echo "📡 Executing seed script in container..."
    docker exec -i meepleai-postgres psql -U postgres -d meepleai < "$SQL_SCRIPT"
else
    echo "📡 Using local PostgreSQL..."

    # Use connection string if provided
    if [ -n "${1:-}" ]; then
        export PGHOST PGPORT PGDATABASE PGUSER PGPASSWORD
    fi

    echo "📡 Executing seed script..."
    psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" -f "$SQL_SCRIPT"
fi

echo ""
echo "✅ Done!"
