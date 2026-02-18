#!/bin/bash
# Quick API test for POC agent with RAG
set -e

API="http://localhost:8080/api/v1"
AGENT_ID="49365068-d1db-4a66-aff5-f9fadca2763b"

echo "🔐 Step 1: Login..."
LOGIN=$(curl -s -c /tmp/cookies.txt -X POST "$API/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@meepleai.dev","password":"pVKOMQNK0tFNgGlX"}')

USER_ID=$(echo "$LOGIN" | jq -r '.user.id // empty')

if [ -z "$USER_ID" ]; then
  echo "❌ Login failed:"
  echo "$LOGIN" | jq
  exit 1
fi

echo "✅ Logged in as: $(echo "$LOGIN" | jq -r '.user.email') (ID: ${USER_ID:0:8}...)"
echo ""

echo "📋 Step 2: Get Agents List..."
AGENTS=$(curl -s -b /tmp/cookies.txt "$API/agents")
echo "$AGENTS" | jq '.[] | {id, name, type, isActive}' 2>/dev/null || echo "$AGENTS"
echo ""

echo "🤖 Step 3: Test Agent Chat (RAG enabled)..."
echo "Query: 'How do you score points in Azul?'"
echo ""

# Create temp file for streaming response
RESPONSE_FILE=$(mktemp)

timeout 30 curl -s -b /tmp/cookies.txt -N -X POST "$API/agents/$AGENT_ID/chat" \
  -H "Content-Type: application/json" \
  -d '{"message":"How do you score points in Azul?","chatThreadId":null}' \
  > "$RESPONSE_FILE" 2>&1 &

CURL_PID=$!
echo "  Waiting for response (max 30s)..."

# Wait with progress indicator
for i in {1..30}; do
  if ! kill -0 $CURL_PID 2>/dev/null; then
    break
  fi
  sleep 1
  echo -n "."
done
echo ""

wait $CURL_PID 2>/dev/null || true

echo "📥 Response received. Analyzing..."
echo ""

# Extract last event (final answer)
LAST_EVENT=$(grep "^data: " "$RESPONSE_FILE" | tail -1 | sed 's/^data: //')

if [ -z "$LAST_EVENT" ]; then
  echo "⚠️  No SSE events found. Raw response:"
  cat "$RESPONSE_FILE" | head -50
else
  echo "✅ Agent Response:"
  echo "$LAST_EVENT" | jq -r '.answer // .content // .message // .' | head -20
  echo ""

  CHUNKS=$(echo "$LAST_EVENT" | jq -r '.chunksRetrieved // .retrievedChunks // 0')
  echo "📊 RAG Metrics:"
  echo "  Chunks Retrieved: $CHUNKS"
  echo "$LAST_EVENT" | jq '{tokenUsage, confidence, latencyMs}' 2>/dev/null || true
fi

rm -f "$RESPONSE_FILE" /tmp/cookies.txt
echo ""
echo "✅ API Test Complete"
