#!/usr/bin/env bash
# restore-staging.sh — Restore staging DB from a snapshot
# Usage: ./restore-staging.sh [snapshot-file]
# Default: uses latest staging-snapshot-*.sql from seeds/

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SEEDS_DIR="$SCRIPT_DIR/../seeds"
SSH_KEY="$HOME/.ssh/meepleai-staging"
SSH_HOST="deploy@204.168.135.69"

# Find snapshot file
if [ -n "${1:-}" ]; then
  SNAPSHOT="$1"
else
  SNAPSHOT=$(ls -t "$SEEDS_DIR"/staging-snapshot-*.sql 2>/dev/null | head -1)
fi

if [ -z "$SNAPSHOT" ] || [ ! -f "$SNAPSHOT" ]; then
  echo "ERROR: No snapshot file found."
  echo "Usage: $0 [snapshot-file]"
  echo "Or place a staging-snapshot-*.sql in $SEEDS_DIR"
  exit 1
fi

echo "=== Restoring staging from: $(basename "$SNAPSHOT") ==="
echo "WARNING: This will OVERWRITE all data in the staging database!"
read -p "Continue? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
  echo "Aborted."
  exit 0
fi

# Upload snapshot to server
echo "Uploading snapshot..."
scp -i "$SSH_KEY" "$SNAPSHOT" "$SSH_HOST:/tmp/meepleai-restore.sql"

# Restore
echo "Restoring database..."
ssh -i "$SSH_KEY" -o ConnectTimeout=10 "$SSH_HOST" \
  "docker cp /tmp/meepleai-restore.sql meepleai-postgres:/tmp/ && \
   docker exec meepleai-postgres psql -U meepleai -d meepleai_staging \
   -f /tmp/meepleai-restore.sql 2>&1 | tail -5"

# Restart API to clear caches
echo "Restarting API..."
ssh -i "$SSH_KEY" -o ConnectTimeout=10 "$SSH_HOST" "docker restart meepleai-api"

echo ""
echo "=== Restore complete ==="
echo "Wait ~45s for API to become healthy, then verify at https://meepleai.app"
