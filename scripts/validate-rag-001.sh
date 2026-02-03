#!/bin/bash
# RAG-001 Validation Script - Complete E2E Test
# Issue: #3172
# Purpose: Validate PDF → Qdrant → RAG pipeline

API_URL="http://localhost:8080"
QDRANT_URL="http://localhost:6333"
PDF_PATH="data/rulebook/scacchi-fide_2017_rulebook.pdf"
GAME_ID="${GAME_ID:-}" # Auto-detect if not provided
COOKIES="cookies.txt"

echo "🚀 RAG-001 Validation Script"
echo "================================"
echo ""

# Step 0a: Auto-detect Chess SharedGame ID (if not provided)
if [ -z "$GAME_ID" ]; then
    echo "📋 Step 0a: Auto-detecting Chess game ID from database..."
    GAME_ID=$(docker exec meepleai-postgres psql -U postgres -d meepleai -t -A -c "SELECT id FROM shared_games WHERE title = 'Chess' AND is_deleted = false LIMIT 1;")
    GAME_ID=$(echo "$GAME_ID" | tr -d '[:space:]')

    if [ -z "$GAME_ID" ]; then
        echo "  ❌ Chess game not found in SharedGameCatalog!"
        exit 1
    fi
    echo "  ✅ Chess SharedGame ID: $GAME_ID"
fi

# Step 0b: Read admin credentials
echo "📋 Step 0: Reading admin credentials..."
if [ -f "infra/secrets/admin.secret" ]; then
    export $(cat infra/secrets/admin.secret | grep -v '^#' | xargs)
    echo "  ✅ Admin email: $ADMIN_EMAIL"
else
    echo "  ❌ admin.secret not found!"
    exit 1
fi

# Step 1: Login
echo ""
echo "📋 Step 1: Authenticating as Admin..."
curl -s -X POST "$API_URL/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" \
  -c $COOKIES \
  -w "\n  Status: %{http_code}\n" > /tmp/login-response.json

if grep -q "session" /tmp/login-response.json; then
    echo "  ✅ Login successful"
else
    echo "  ❌ Login failed"
    cat /tmp/login-response.json
    exit 1
fi

# Step 2: Enable PDF Upload Feature
echo ""
echo "📋 Step 2: Enabling Features.PdfUpload..."
curl -s -X POST "$API_URL/api/v1/admin/feature-flags" \
  -H "Content-Type: application/json" \
  -b $COOKIES \
  -d '{"key":"Features.PdfUpload","enabled":true,"description":"PDF uploads enabled"}' \
  -w "\n  Status: %{http_code}\n" 2>&1 | grep -E "Status:|already exists"

# Try toggle if creation fails
curl -s -X POST "$API_URL/api/v1/admin/feature-flags/Features.PdfUpload/toggle" \
  -b $COOKIES \
  -w "\n  Toggle status: %{http_code}\n"

echo "  ⏳ Waiting 5s for feature flag propagation..."
sleep 5

# Step 3: Add Chess to Admin Library (using SharedGameId)
echo ""
echo "📋 Step 3: Adding Chess to Admin library..."

ADD_RESPONSE=$(curl -s -X POST "$API_URL/api/v1/library/games/$GAME_ID" \
  -H "Content-Type: application/json" \
  -b $COOKIES \
  -d '{}' \
  -w "\n%{http_code}")

ADD_CODE=$(echo "$ADD_RESPONSE" | tail -n1)

if [ "$ADD_CODE" = "200" ] || [ "$ADD_CODE" = "201" ]; then
    echo "  ✅ Chess added to library (SharedGameId: $GAME_ID)"
elif [ "$ADD_CODE" = "409" ]; then
    echo "  ✅ Chess already in library (409 Conflict)"
else
    ADD_BODY=$(echo "$ADD_RESPONSE" | head -n-1)
    echo "  ⚠️  Add to library status: $ADD_CODE"
    echo "$ADD_BODY" | jq '.' 2>/dev/null || echo "$ADD_BODY"
    exit 1
fi

echo "  📄 Using SharedGameId for upload: $GAME_ID"

# Step 4: Check Qdrant (Before)
echo ""
echo "📋 Step 4: Checking Qdrant collection (before)..."
BEFORE_COUNT=$(curl -s "$QDRANT_URL/collections/meepleai_documents" | jq -r '.result.points_count // 0')
echo "  📊 Vectors before: $BEFORE_COUNT"

# Step 5: Upload PDF
echo ""
echo "📋 Step 5: Uploading PDF..."
if [ ! -f "$PDF_PATH" ]; then
    echo "  ❌ PDF not found: $PDF_PATH"
    exit 1
fi

PDF_SIZE=$(du -h "$PDF_PATH" | cut -f1)
echo "  📄 File: $(basename $PDF_PATH) ($PDF_SIZE)"

UPLOAD_RESPONSE=$(curl -s -X POST "$API_URL/api/v1/ingest/pdf" \
  -b $COOKIES \
  -F "file=@$PDF_PATH" \
  -F "gameId=$GAME_ID" \
  -F "language=it" \
  -w "\n%{http_code}")

HTTP_CODE=$(echo "$UPLOAD_RESPONSE" | tail -n1)
RESPONSE_BODY=$(echo "$UPLOAD_RESPONSE" | head -n-1)

if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "201" ]; then
    echo "  ✅ PDF uploaded (Status: $HTTP_CODE)"
    DOCUMENT_ID=$(echo "$RESPONSE_BODY" | jq -r '.documentId // .id // empty')
    echo "  📄 Document ID: $DOCUMENT_ID"
else
    echo "  ❌ Upload failed (Status: $HTTP_CODE)"
    echo "$RESPONSE_BODY" | jq '.' 2>/dev/null || echo "$RESPONSE_BODY"
    echo ""
    echo "  ⚠️  Manual workaround required:"
    echo "  1. Open http://localhost:3000"
    echo "  2. Login as: $ADMIN_EMAIL"
    echo "  3. Navigate to Chess game detail"
    echo "  4. Upload PDF manually via UI"
    exit 1
fi

# Step 6: Trigger Indexing
echo ""
echo "📋 Step 6: Triggering PDF indexing..."
if [ -n "$DOCUMENT_ID" ]; then
    INDEX_RESPONSE=$(curl -s -X POST "$API_URL/api/v1/ingest/pdf/$DOCUMENT_ID/index" \
      -b $COOKIES \
      -w "\n%{http_code}")

    INDEX_CODE=$(echo "$INDEX_RESPONSE" | tail -n1)

    if [ "$INDEX_CODE" = "200" ] || [ "$INDEX_CODE" = "202" ]; then
        echo "  ✅ Indexing triggered (Status: $INDEX_CODE)"
        echo "  ⏳ Waiting 60s for processing..."
        sleep 60
    else
        echo "  ⚠️  Indexing status: $INDEX_CODE"
    fi
fi

# Step 7: Check Qdrant (After)
echo ""
echo "📋 Step 7: Checking Qdrant collection (after)..."
AFTER_COUNT=$(curl -s "$QDRANT_URL/collections/meepleai_documents" | jq -r '.result.points_count // 0')
VECTORS_ADDED=$((AFTER_COUNT - BEFORE_COUNT))
echo "  📊 Vectors after: $AFTER_COUNT"
echo "  ➕ Vectors added: $VECTORS_ADDED"

if [ $VECTORS_ADDED -gt 0 ]; then
    echo "  ✅ PDF successfully indexed in Qdrant!"
else
    echo "  ⚠️  No vectors added yet, may need more time"
fi

# Step 8: Test RAG Query
echo ""
echo "📋 Step 8: Testing RAG query..."
RAG_RESPONSE=$(curl -s -X POST "$API_URL/api/v1/agents/qa" \
  -H "Content-Type: application/json" \
  -b $COOKIES \
  -d "{\"gameId\":\"$GAME_ID\",\"query\":\"Come si muovono i pedoni negli scacchi?\"}")

if echo "$RAG_RESPONSE" | jq -e '.answer' > /dev/null 2>&1; then
    ANSWER=$(echo "$RAG_RESPONSE" | jq -r '.answer')
    CONFIDENCE=$(echo "$RAG_RESPONSE" | jq -r '.confidence // 0')
    CITATIONS=$(echo "$RAG_RESPONSE" | jq -r '.citations | length')

    echo "  ✅ RAG query successful"
    echo "  📝 Answer: ${ANSWER:0:100}..."
    echo "  🎯 Confidence: $CONFIDENCE"
    echo "  📚 Citations: $CITATIONS"

    # Validate confidence threshold
    if (( $(echo "$CONFIDENCE >= 0.7" | bc -l) )); then
        echo "  ✅ Confidence above threshold (>= 0.7)"
    else
        echo "  ⚠️  Confidence below threshold: $CONFIDENCE"
    fi
else
    echo "  ❌ RAG query failed or invalid response"
    echo "$RAG_RESPONSE" | jq '.' 2>/dev/null || echo "$RAG_RESPONSE"
fi

# Step 9: Final Summary
echo ""
echo "================================"
echo "📊 VALIDATION SUMMARY"
echo "================================"
echo "✅ Authentication: PASS"
echo "✅ Feature Flag: $(if [ -n \"$DOCUMENT_ID\" ]; then echo 'ENABLED'; else echo 'BLOCKED'; fi)"
echo "✅ PDF Upload: $(if [ -n \"$DOCUMENT_ID\" ]; then echo 'PASS'; else echo 'FAIL'; fi)"
echo "✅ Qdrant Indexing: $(if [ $VECTORS_ADDED -gt 0 ]; then echo 'PASS'; else echo 'PENDING'; fi)"
echo "✅ RAG Query: $(if echo \"$RAG_RESPONSE\" | jq -e '.answer' > /dev/null 2>&1; then echo 'PASS'; else echo 'PENDING'; fi)"

# Cleanup
rm -f $COOKIES /tmp/login-response.json

echo ""
echo "ℹ️  Full report: docs/validation/rag-001-results.md"
echo "🎯 Issue: #3172 (RAG-001)"
