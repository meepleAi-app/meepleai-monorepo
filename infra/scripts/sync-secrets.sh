#!/usr/bin/env bash
# sync-secrets.sh — Pull secrets from staging server to local
# Usage: ./sync-secrets.sh
# Requires: SSH access to meepleai.app

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SECRETS_DIR="$SCRIPT_DIR/../secrets"
REMOTE="meepleai.app"
REMOTE_PATH="/opt/meepleai/repo/infra/secrets"

echo "Syncing secrets from $REMOTE..."
scp "$REMOTE:$REMOTE_PATH/*.secret" "$SECRETS_DIR/"
echo "Done. Secrets synced to $SECRETS_DIR/"
