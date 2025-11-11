#!/usr/bin/env bash
# Initialize Docker Secrets from templates
# SEC-708: Docker Secrets implementation
#
# Usage:
#   cd tools/secrets
#   ./init-secrets.sh
#
# This script creates secret files in infra/secrets/ directory
# based on user input or secure random generation.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SECRETS_DIR="$PROJECT_ROOT/infra/secrets"

echo "🔐 MeepleAI Docker Secrets Initialization"
echo "=========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if secrets directory exists
if [ ! -d "$SECRETS_DIR" ]; then
    echo -e "${RED}Error: Secrets directory not found: $SECRETS_DIR${NC}"
    echo "Run this script from the project root after setting up the repository."
    exit 1
fi

# Function to generate random password
generate_password() {
    openssl rand -base64 24 | tr -d '=+/' | cut -c1-24
}

# Function to create secret file
create_secret() {
    local secret_name=$1
    local secret_file="$SECRETS_DIR/$secret_name.txt"
    local prompt=$2
    local default_value=$3
    local generate_random=${4:-false}

    if [ -f "$secret_file" ]; then
        echo -e "${YELLOW}⚠️  $secret_name.txt already exists. Skipping...${NC}"
        return
    fi

    echo ""
    echo -e "${GREEN}📝 Setting up: $secret_name${NC}"

    if [ "$generate_random" = true ]; then
        local value=$(generate_password)
        echo "$value" > "$secret_file"
        chmod 600 "$secret_file"
        echo -e "${GREEN}✅ Generated random value for $secret_name${NC}"
    else
        echo "$prompt"
        if [ -n "$default_value" ]; then
            echo -e "${YELLOW}Default: $default_value${NC}"
        fi
        read -r -p "Enter value (or press Enter for default): " value

        if [ -z "$value" ] && [ -n "$default_value" ]; then
            value="$default_value"
        fi

        if [ -z "$value" ]; then
            echo -e "${RED}Error: Value cannot be empty!${NC}"
            exit 1
        fi

        echo "$value" > "$secret_file"
        chmod 600 "$secret_file"
        echo -e "${GREEN}✅ Created $secret_name.txt${NC}"
    fi
}

echo "This script will create secret files in: $SECRETS_DIR"
echo ""
echo -e "${YELLOW}⚠️  Warning: Secret files will be created with secure permissions (600)${NC}"
echo -e "${YELLOW}⚠️  These files are gitignored and should NEVER be committed${NC}"
echo ""
read -r -p "Continue? (y/N): " confirm

if [[ ! "$confirm" =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 0
fi

# PostgreSQL Password
create_secret \
    "postgres-password" \
    "PostgreSQL database password (used by postgres, api, n8n)" \
    "meeplepass" \
    false

# OpenRouter API Key
create_secret \
    "openrouter-api-key" \
    "OpenRouter API key for LLM (get from https://openrouter.ai/)" \
    "" \
    false

# n8n Encryption Key (auto-generate)
create_secret \
    "n8n-encryption-key" \
    "n8n encryption key (auto-generated secure random)" \
    "" \
    true

# n8n Basic Auth Password
create_secret \
    "n8n-basic-auth-password" \
    "n8n UI basic auth password" \
    "admin123" \
    false

# Gmail App Password
create_secret \
    "gmail-app-password" \
    "Gmail App Password for Alertmanager (see docs/guide/secrets-management.md)" \
    "" \
    false

# Grafana Admin Password
create_secret \
    "grafana-admin-password" \
    "Grafana admin UI password" \
    "admin" \
    false

# Initial Admin Password
create_secret \
    "initial-admin-password" \
    "API bootstrap initial admin password (min 8 chars, 1 uppercase, 1 digit)" \
    "Admin123!ChangeMe" \
    false

echo ""
echo -e "${GREEN}=========================================="
echo "✅ Secret initialization complete!"
echo "==========================================${NC}"
echo ""
echo "Created secrets:"
ls -1 "$SECRETS_DIR"/*.txt 2>/dev/null || echo "  (no secrets created)"
echo ""
echo "Next steps:"
echo "1. Review secret values in: $SECRETS_DIR"
echo "2. Start services: docker compose up -d"
echo "3. Verify services can read secrets: docker compose logs"
echo ""
echo -e "${YELLOW}⚠️  Remember to NEVER commit secret files to git!${NC}"
echo "   They are protected by .gitignore"
echo ""
