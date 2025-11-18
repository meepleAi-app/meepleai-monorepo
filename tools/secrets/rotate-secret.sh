#!/usr/bin/env bash
# Rotate a specific Docker Secret
# SEC-708: Docker Secrets implementation
#
# Usage:
#   ./rotate-secret.sh <secret-name>
#
# Example:
#   ./rotate-secret.sh postgres-password

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SECRETS_DIR="$PROJECT_ROOT/infra/secrets"

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

if [ $# -ne 1 ]; then
    echo -e "${RED}Error: Missing secret name${NC}"
    echo "Usage: $0 <secret-name>"
    echo ""
    echo "Available secrets:"
    ls -1 "$SECRETS_DIR"/*.txt 2>/dev/null | xargs -n 1 basename | sed 's/\.txt$//' || echo "  (none)"
    exit 1
fi

SECRET_NAME=$1
SECRET_FILE="$SECRETS_DIR/$SECRET_NAME.txt"

echo "🔄 Secret Rotation: $SECRET_NAME"
echo "================================="
echo ""

if [ ! -f "$SECRET_FILE" ]; then
    echo -e "${RED}Error: Secret file not found: $SECRET_FILE${NC}"
    exit 1
fi

# Backup old secret
BACKUP_FILE="$SECRET_FILE.backup.$(date +%Y%m%d_%H%M%S)"
cp "$SECRET_FILE" "$BACKUP_FILE"
echo -e "${GREEN}✅ Backed up old secret to: $(basename "$BACKUP_FILE")${NC}"

# Get new secret value
echo ""
echo "Enter new secret value:"
echo -e "${YELLOW}(or type 'random' to auto-generate)${NC}"
read -r -s new_value

if [ "$new_value" = "random" ]; then
    new_value=$(openssl rand -base64 24 | tr -d '=+/' | cut -c1-24)
    echo -e "${GREEN}✅ Generated random value${NC}"
fi

if [ -z "$new_value" ]; then
    echo -e "${RED}Error: New value cannot be empty!${NC}"
    exit 1
fi

# Write new secret
echo "$new_value" > "$SECRET_FILE"
chmod 600 "$SECRET_FILE"
echo -e "${GREEN}✅ Updated secret file${NC}"

echo ""
echo -e "${YELLOW}⚠️  Important: Restart affected services for changes to take effect${NC}"
echo ""
echo "Service restart commands:"

case "$SECRET_NAME" in
    postgres-password)
        echo "  docker compose restart meepleai-postgres meepleai-api meepleai-n8n"
        ;;
    openrouter-api-key)
        echo "  docker compose restart meepleai-api"
        ;;
    n8n-encryption-key|n8n-basic-auth-password)
        echo "  docker compose restart meepleai-n8n"
        ;;
    gmail-app-password)
        echo "  docker compose restart meepleai-alertmanager"
        ;;
    grafana-admin-password)
        echo "  docker compose restart meepleai-grafana"
        ;;
    initial-admin-password)
        echo "  docker compose restart meepleai-api"
        ;;
    *)
        echo "  docker compose restart <affected-service>"
        ;;
esac

echo ""
echo -e "${GREEN}✅ Secret rotation complete!${NC}"
echo ""
echo "Backup location: $BACKUP_FILE"
echo -e "${YELLOW}⚠️  Remember to delete backup after verifying rotation: rm $BACKUP_FILE${NC}"
echo ""

