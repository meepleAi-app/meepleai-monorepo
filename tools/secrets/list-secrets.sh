#!/usr/bin/env bash
# List all Docker Secrets and their status
# SEC-708: Docker Secrets implementation

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SECRETS_DIR="$PROJECT_ROOT/infra/secrets"

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo "🔐 MeepleAI Docker Secrets Status"
echo "================================="
echo ""
echo "Secrets directory: $SECRETS_DIR"
echo ""

# Expected secrets
declare -a EXPECTED_SECRETS=(
    "postgres-password"
    "openrouter-api-key"
    "n8n-encryption-key"
    "n8n-basic-auth-password"
    "gmail-app-password"
    "grafana-admin-password"
    "initial-admin-password"
)

echo "Secret Status:"
echo ""
printf "%-30s %-10s %s\n" "Secret Name" "Status" "Size"
echo "----------------------------------------------------------------"

for secret in "${EXPECTED_SECRETS[@]}"; do
    secret_file="$SECRETS_DIR/$secret.txt"

    if [ -f "$secret_file" ]; then
        size=$(wc -c < "$secret_file" | tr -d ' ')
        if [ "$size" -gt 0 ]; then
            echo -e "$(printf "%-30s" "$secret") ${GREEN}✅ Present${NC}  $size bytes"
        else
            echo -e "$(printf "%-30s" "$secret") ${YELLOW}⚠️  Empty${NC}   0 bytes"
        fi
    else
        echo -e "$(printf "%-30s" "$secret") ${RED}❌ Missing${NC}"
    fi
done

echo ""
echo "----------------------------------------------------------------"

# Count status
total=${#EXPECTED_SECRETS[@]}
present=$(find "$SECRETS_DIR" -name "*.txt" -type f 2>/dev/null | wc -l)
missing=$((total - present))

echo "Summary: $present/$total secrets present"

if [ "$missing" -gt 0 ]; then
    echo ""
    echo -e "${YELLOW}⚠️  $missing secret(s) missing!${NC}"
    echo "Run: tools/secrets/init-secrets.sh"
    exit 1
fi

echo ""
echo -e "${GREEN}✅ All secrets configured!${NC}"
echo ""
