#!/bin/bash

# Test OAuth Configuration via Health Check API
# Validates that all OAuth providers are correctly configured

set -e

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

API_URL="${API_URL:-http://localhost:8080}"

echo "==========================================="
echo "   OAuth Health Check Test"
echo "==========================================="
echo ""

# Test /health/config endpoint
echo -e "${BLUE}Testing:${NC} GET $API_URL/health/config"
echo ""

RESPONSE=$(curl -s "$API_URL/health/config")

if [ -z "$RESPONSE" ]; then
    echo -e "${RED}❌ ERROR${NC} No response from health check endpoint"
    echo "   Is the API running? Try: docker compose up -d api"
    exit 1
fi

# Parse response
STATUS=$(echo "$RESPONSE" | jq -r '.status' 2>/dev/null || echo "error")
OAUTH_CONFIGURED=$(echo "$RESPONSE" | jq -r '.checks[0].data.oauth_configured_providers[]?' 2>/dev/null | tr '\n' ',' | sed 's/,$//')
OAUTH_PLACEHOLDERS=$(echo "$RESPONSE" | jq -r '.checks[0].data.oauth_placeholder_providers[]?' 2>/dev/null | tr '\n' ',' | sed 's/,$//')
OAUTH_MISCONFIGURED=$(echo "$RESPONSE" | jq -r '.checks[0].data.oauth_misconfigured_providers[]?' 2>/dev/null | tr '\n' ',' | sed 's/,$//')

# Display results
echo "=== Health Check Status ==="
if [ "$STATUS" = "Healthy" ]; then
    echo -e "Overall Status: ${GREEN}✅ Healthy${NC}"
elif [ "$STATUS" = "Degraded" ]; then
    echo -e "Overall Status: ${YELLOW}⚠️  Degraded${NC}"
else
    echo -e "Overall Status: ${RED}❌ Unhealthy${NC}"
fi
echo ""

echo "=== OAuth Providers ==="
if [ -n "$OAUTH_CONFIGURED" ]; then
    echo -e "${GREEN}✅ Configured:${NC} $OAUTH_CONFIGURED"
else
    echo -e "${RED}❌ No providers configured${NC}"
fi

if [ -n "$OAUTH_PLACEHOLDERS" ]; then
    echo -e "${YELLOW}⚠️  Placeholders:${NC} $OAUTH_PLACEHOLDERS"
fi

if [ -n "$OAUTH_MISCONFIGURED" ]; then
    echo -e "${RED}❌ Misconfigured:${NC} $OAUTH_MISCONFIGURED"
fi
echo ""

# Display masked client IDs
echo "=== OAuth Client IDs (Masked) ==="
GOOGLE_ID=$(echo "$RESPONSE" | jq -r '.checks[0].data.oauth_google_client_id?' 2>/dev/null)
DISCORD_ID=$(echo "$RESPONSE" | jq -r '.checks[0].data.oauth_discord_client_id?' 2>/dev/null)
GITHUB_ID=$(echo "$RESPONSE" | jq -r '.checks[0].data.oauth_github_client_id?' 2>/dev/null)

if [ -n "$GOOGLE_ID" ] && [ "$GOOGLE_ID" != "null" ]; then
    echo -e "Google:  ${GREEN}$GOOGLE_ID${NC}"
fi

if [ -n "$DISCORD_ID" ] && [ "$DISCORD_ID" != "null" ]; then
    echo -e "Discord: ${GREEN}$DISCORD_ID${NC}"
fi

if [ -n "$GITHUB_ID" ] && [ "$GITHUB_ID" != "null" ]; then
    echo -e "GitHub:  ${GREEN}$GITHUB_ID${NC}"
fi
echo ""

# Display warnings if any
WARNINGS=$(echo "$RESPONSE" | jq -r '.checks[0].data.warnings[]?' 2>/dev/null)
if [ -n "$WARNINGS" ]; then
    echo "=== Warnings ==="
    echo "$WARNINGS" | while IFS= read -r warning; do
        echo -e "${YELLOW}⚠️${NC}  $warning"
    done
    echo ""
fi

# Full JSON response (optional)
if [ "$1" = "--verbose" ] || [ "$1" = "-v" ]; then
    echo "=== Full Response ==="
    echo "$RESPONSE" | jq '.'
    echo ""
fi

# Summary
echo "==========================================="
echo "   Summary"
echo "==========================================="

OAUTH_COUNT=$(echo "$OAUTH_CONFIGURED" | tr ',' '\n' | grep -v '^$' | wc -l)
PLACEHOLDER_COUNT=$(echo "$OAUTH_PLACEHOLDERS" | tr ',' '\n' | grep -v '^$' | wc -l)

if [ "$OAUTH_COUNT" -eq 3 ] && [ "$PLACEHOLDER_COUNT" -eq 0 ]; then
    echo -e "${GREEN}✅ All OAuth providers (3/3) are properly configured${NC}"
    exit 0
elif [ "$OAUTH_COUNT" -gt 0 ]; then
    echo -e "${YELLOW}⚠️  Partial configuration: $OAUTH_COUNT/3 providers configured${NC}"
    exit 0
else
    echo -e "${RED}❌ No OAuth providers configured${NC}"
    exit 1
fi
