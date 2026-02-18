#!/bin/bash
# ============================================================================
# RAG Integration Test Script
# ============================================================================
# Tests the POC agent with VectorDocument integration
# Verifies RAG context injection and professional responses
# ============================================================================

set -e

# Colors
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Configuration
API_URL="http://localhost:8080/api/v1"
AGENT_ID="49365068-d1db-4a66-aff5-f9fadca2763b"
VECTOR_DOC_ID="8b78c72a-b5bc-454e-875b-22754a673c40"
GAME_NAME="Azul"

echo -e "${BLUE}============================================================================${NC}"
echo -e "${BLUE}RAG Integration Test - MeepleAssistant POC${NC}"
echo -e "${BLUE}============================================================================${NC}"
echo ""

# Step 1: Login and get session
echo -e "${YELLOW}Step 1: Authenticating...${NC}"
LOGIN_RESPONSE=$(curl -s -X POST "${API_URL}/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@meepleai.dev",
    "password": "Admin123!ChangeMe"
  }')

SESSION_TOKEN=$(echo $LOGIN_RESPONSE | jq -r '.sessionToken // .token // empty')

if [ -z "$SESSION_TOKEN" ]; then
  echo -e "${RED}✗ Login failed. Response:${NC}"
  echo "$LOGIN_RESPONSE" | jq
  exit 1
fi

echo -e "${GREEN}✓ Login successful${NC}"
echo "  Session Token: ${SESSION_TOKEN:0:20}..."
echo ""

# Step 2: Verify agent configuration
echo -e "${YELLOW}Step 2: Verifying agent configuration...${NC}"
AGENT_CONFIG=$(docker exec meepleai-postgres psql -U postgres -d meepleai -t -c \
  "SELECT selected_document_ids_json FROM agent_configurations WHERE agent_id = '${AGENT_ID}';" \
  | tr -d ' \n')

echo -e "${GREEN}✓ Agent configured with documents:${NC}"
echo "  $AGENT_CONFIG"
echo ""

# Step 3: Test queries
echo -e "${YELLOW}Step 3: Testing RAG-enabled queries...${NC}"
echo ""

# Test 1: Simple rule question
echo -e "${BLUE}Test 1: Simple rule question (should use RAG context)${NC}"
TEST_QUERY="How do you score points in Azul?"

echo "  Query: \"$TEST_QUERY\""
echo "  Sending request..."

# Create temporary file for SSE response
TEMP_RESPONSE=$(mktemp)

curl -s -X POST "${API_URL}/agents/${AGENT_ID}/chat" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${SESSION_TOKEN}" \
  -d "{
    \"message\": \"$TEST_QUERY\",
    \"chatThreadId\": null
  }" > "$TEMP_RESPONSE" 2>&1 &

CURL_PID=$!

# Wait for response (max 30 seconds)
for i in {1..30}; do
  if ! kill -0 $CURL_PID 2>/dev/null; then
    break
  fi
  sleep 1
  echo -n "."
done

echo ""

# Kill curl if still running
kill $CURL_PID 2>/dev/null || true

# Parse SSE response (extract last data event)
if [ -f "$TEMP_RESPONSE" ]; then
  RESPONSE_DATA=$(grep "^data: " "$TEMP_RESPONSE" | tail -1 | sed 's/^data: //')

  if [ ! -z "$RESPONSE_DATA" ]; then
    echo -e "${GREEN}✓ Response received${NC}"
    echo ""

    # Extract key fields
    ANSWER=$(echo "$RESPONSE_DATA" | jq -r '.answer // .content // .message // empty')
    CHUNKS=$(echo "$RESPONSE_DATA" | jq -r '.retrievedChunks // [] | length')
    CONFIDENCE=$(echo "$RESPONSE_DATA" | jq -r '.confidence // empty')

    echo -e "${BLUE}Response Analysis:${NC}"
    echo "  Chunks Retrieved: $CHUNKS"
    echo "  Confidence: $CONFIDENCE"
    echo ""
    echo -e "${BLUE}Agent Answer:${NC}"
    echo "  $ANSWER" | fold -w 70 -s | sed 's/^/  /'
    echo ""

    # Verify RAG usage
    if [ "$CHUNKS" -gt 0 ]; then
      echo -e "${GREEN}✓ RAG Context Used: ${CHUNKS} chunks retrieved${NC}"
    else
      echo -e "${YELLOW}⚠ No chunks retrieved (RAG may not be active)${NC}"
    fi

    # Check for citations (professional prompt requirement)
    if echo "$ANSWER" | grep -qi "source:\|rulebook\|according to"; then
      echo -e "${GREEN}✓ Professional Citations: Sources mentioned${NC}"
    else
      echo -e "${YELLOW}⚠ No explicit citations found${NC}"
    fi

  else
    echo -e "${RED}✗ No valid response data${NC}"
    echo "  Raw response:"
    cat "$TEMP_RESPONSE" | head -20
  fi

  rm "$TEMP_RESPONSE"
else
  echo -e "${RED}✗ No response file created${NC}"
fi

echo ""
echo -e "${BLUE}============================================================================${NC}"
echo -e "${BLUE}Test Complete${NC}"
echo -e "${BLUE}============================================================================${NC}"
echo ""
echo -e "${YELLOW}Manual Verification Checklist:${NC}"
echo "  [ ] Response uses context from Azul rulebook (not generic knowledge)"
echo "  [ ] Professional tone maintained (expert, authoritative)"
echo "  [ ] Structured format (Direct Answer → Explanation → Sources)"
echo "  [ ] Citations present when using RAG context"
echo "  [ ] No fabricated information (only from retrieved chunks)"
echo ""
echo -e "${YELLOW}Next Steps:${NC}"
echo "  1. Review agent logs: docker logs meepleai-api | grep MeepleAssistant"
echo "  2. Check Qdrant vector search: http://localhost:6333/dashboard"
echo "  3. Test more complex queries (strategies, comparisons)"
echo "  4. Upgrade strategy if needed (HybridSearch, IterativeRAG)"
echo ""
