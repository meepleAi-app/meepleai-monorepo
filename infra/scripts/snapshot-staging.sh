#!/usr/bin/env bash
# snapshot-staging.sh — Snapshot staging DB state for reproducible seeds
# Usage: ./snapshot-staging.sh [output-dir]
# Creates: full dump + selective game data seed

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${1:-$SCRIPT_DIR/../seeds}"
TIMESTAMP=$(date +%Y-%m-%d)
SSH_KEY="$HOME/.ssh/meepleai-staging"
SSH_HOST="deploy@204.168.135.69"

mkdir -p "$OUTPUT_DIR"

echo "=== Staging DB Snapshot ($TIMESTAMP) ==="

# Full dump (schema + data)
echo "Creating full dump..."
ssh -i "$SSH_KEY" -o ConnectTimeout=10 "$SSH_HOST" \
  "docker exec meepleai-postgres pg_dump -U meepleai -d meepleai_staging \
   --no-owner --no-privileges --if-exists --clean \
   -f /tmp/meepleai-full-dump.sql && \
   docker cp meepleai-postgres:/tmp/meepleai-full-dump.sql /tmp/"

scp -i "$SSH_KEY" "$SSH_HOST:/tmp/meepleai-full-dump.sql" \
  "$OUTPUT_DIR/staging-snapshot-$TIMESTAMP.sql"
echo "  Full dump: $OUTPUT_DIR/staging-snapshot-$TIMESTAMP.sql"

# Selective seed (game data only, INSERT format)
echo "Creating game catalog seed..."
ssh -i "$SSH_KEY" -o ConnectTimeout=10 "$SSH_HOST" \
  "docker exec meepleai-postgres pg_dump -U meepleai -d meepleai_staging \
   --no-owner --no-privileges --data-only --inserts \
   -t shared_games -t shared_game_designers -t shared_game_publishers \
   -t shared_game_categories -t shared_game_mechanics \
   -t pdf_documents -t vector_documents -t processing_jobs \
   -t users -t system_configurations \
   -f /tmp/meepleai-seed.sql && \
   docker cp meepleai-postgres:/tmp/meepleai-seed.sql /tmp/"

scp -i "$SSH_KEY" "$SSH_HOST:/tmp/meepleai-seed.sql" \
  "$OUTPUT_DIR/game-catalog-seed.sql"
echo "  Game seed: $OUTPUT_DIR/game-catalog-seed.sql"

echo ""
echo "=== Snapshot complete ==="
ls -lh "$OUTPUT_DIR"/*.sql
