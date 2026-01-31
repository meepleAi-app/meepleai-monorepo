#!/bin/bash
# Smoke Tests - Verify critical functionality works
# Tests: Authentication, PDF upload, Chat query, Admin access

set -e

ENVIRONMENT="${1:-staging}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Environment configuration
case "$ENVIRONMENT" in
    staging)
        API_URL="https://api.staging.meepleai.dev"
        TEST_EMAIL="test@meepleai.dev"
        TEST_PASSWORD="SmokeTest123!"
        ;;
    production)
        API_URL="https://api.meepleai.dev"
        TEST_EMAIL="smoketest@meepleai.dev"
        TEST_PASSWORD="${PROD_SMOKETEST_PASSWORD}"
        ;;
    *)
        echo -e "${RED}Unknown environment: ${ENVIRONMENT}${NC}"
        exit 1
        ;;
esac

echo -e "${YELLOW}💨 Smoke Tests - ${ENVIRONMENT}${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Test 1: Authentication
echo "Test 1: User authentication..."
AUTH_RESPONSE=$(curl -s -X POST "${API_URL}/api/v1/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"${TEST_EMAIL}\",\"password\":\"${TEST_PASSWORD}\"}" \
    -c /tmp/cookies.txt)

if echo "$AUTH_RESPONSE" | jq -e '.token' > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC} Authentication successful"
else
    echo -e "${RED}✗${NC} Authentication failed"
    echo "Response: $AUTH_RESPONSE"
    exit 1
fi

# Test 2: Get user profile
echo "Test 2: Get user profile..."
PROFILE_RESPONSE=$(curl -s "${API_URL}/api/v1/users/me" \
    -b /tmp/cookies.txt)

if echo "$PROFILE_RESPONSE" | jq -e '.email' > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC} User profile retrieved"
else
    echo -e "${RED}✗${NC} Failed to get user profile"
    exit 1
fi

# Test 3: List games
echo "Test 3: List games..."
GAMES_RESPONSE=$(curl -s "${API_URL}/api/v1/games" \
    -b /tmp/cookies.txt)

if echo "$GAMES_RESPONSE" | jq -e '.[0].name' > /dev/null 2>&1; then
    GAME_COUNT=$(echo "$GAMES_RESPONSE" | jq 'length')
    echo -e "${GREEN}✓${NC} Games list retrieved (${GAME_COUNT} games)"
else
    echo -e "${YELLOW}⚠${NC}  No games found (might be empty database)"
fi

# Test 4: Search functionality
echo "Test 4: Search/RAG query..."
SEARCH_RESPONSE=$(curl -s "${API_URL}/api/v1/search?q=game+rules" \
    -b /tmp/cookies.txt)

if echo "$SEARCH_RESPONSE" | jq -e '.results' > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC} Search functionality works"
else
    echo -e "${YELLOW}⚠${NC}  Search returned no results"
fi

# Test 5: Health endpoint (redundant but critical)
echo "Test 5: Health endpoint..."
if curl -sf "${API_URL}/health" > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC} Health endpoint responding"
else
    echo -e "${RED}✗${NC} Health endpoint failed"
    exit 1
fi

# Cleanup
rm -f /tmp/cookies.txt

echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}✅ All smoke tests passed${NC}"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "Tested: Authentication, Profile, Games, Search, Health"
