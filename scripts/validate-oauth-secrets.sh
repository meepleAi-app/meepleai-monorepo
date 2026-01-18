#!/bin/bash

# OAuth Secrets Validation Script
# Validates that OAuth provider credentials are properly configured

set -e

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

SECRETS_DIR="infra/secrets"

echo "==========================================="
echo "   OAuth Configuration Validation"
echo "==========================================="
echo ""

# Function to validate secret file
validate_secret() {
    local provider=$1
    local secret_type=$2
    local filename=$3
    local min_length=$4

    if [ ! -f "$SECRETS_DIR/$filename" ]; then
        echo -e "${RED}❌ MISSING${NC} $provider $secret_type: File not found ($SECRETS_DIR/$filename)"
        return 1
    fi

    local value=$(cat "$SECRETS_DIR/$filename" | tr -d '\n\r' | xargs)

    if [ -z "$value" ]; then
        echo -e "${RED}❌ EMPTY${NC} $provider $secret_type: File is empty"
        return 1
    fi

    # Check for placeholder values
    if echo "$value" | grep -qi "placeholder\|your-\|\${"; then
        echo -e "${YELLOW}⚠️  PLACEHOLDER${NC} $provider $secret_type: Contains placeholder text"
        return 1
    fi

    # Check minimum length
    local length=${#value}
    if [ $length -lt $min_length ]; then
        echo -e "${YELLOW}⚠️  TOO SHORT${NC} $provider $secret_type: Length $length < $min_length"
        return 1
    fi

    # Check for whitespace
    if echo "$value" | grep -q " "; then
        echo -e "${YELLOW}⚠️  WHITESPACE${NC} $provider $secret_type: Contains spaces"
        return 1
    fi

    # Mask value for display (show first 4 and last 4 characters)
    local masked
    if [ $length -gt 8 ]; then
        masked="${value:0:4}...${value: -4}"
    else
        masked="***"
    fi

    echo -e "${GREEN}✅ VALID${NC} $provider $secret_type: $masked (length: $length)"
    return 0
}

# Validate OAuth providers
providers_valid=0
providers_invalid=0

echo "=== Google OAuth ==="
if validate_secret "Google" "Client ID" "google-oauth-client-id.txt" 20 && \
   validate_secret "Google" "Client Secret" "google-oauth-client-secret.txt" 20; then
    ((providers_valid++))
else
    ((providers_invalid++))
fi
echo ""

echo "=== Discord OAuth ==="
if validate_secret "Discord" "Client ID" "discord-oauth-client-id.txt" 10 && \
   validate_secret "Discord" "Client Secret" "discord-oauth-client-secret.txt" 20; then
    ((providers_valid++))
else
    ((providers_invalid++))
fi
echo ""

echo "=== GitHub OAuth ==="
if validate_secret "GitHub" "Client ID" "github-oauth-client-id.txt" 10 && \
   validate_secret "GitHub" "Client Secret" "github-oauth-client-secret.txt" 20; then
    ((providers_valid++))
else
    ((providers_invalid++))
fi
echo ""

# Summary
echo "==========================================="
echo "   Summary"
echo "==========================================="
echo ""
echo "Valid OAuth Providers:   $providers_valid / 3"
echo "Invalid/Missing:         $providers_invalid / 3"
echo ""

if [ $providers_valid -eq 3 ]; then
    echo -e "${GREEN}✅ All OAuth providers are properly configured${NC}"
    exit 0
elif [ $providers_valid -eq 0 ]; then
    echo -e "${RED}❌ No OAuth providers configured - social login will not work${NC}"
    exit 1
else
    echo -e "${YELLOW}⚠️  Partial OAuth configuration - some social login methods will not work${NC}"
    exit 0
fi
