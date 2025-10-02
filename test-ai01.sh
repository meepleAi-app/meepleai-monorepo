#!/bin/bash

# AI-01 Integration Test Script
# Tests: Registration -> PDF Upload -> Vector Indexing -> RAG Query

API_URL="http://localhost:8080"
COOKIES_FILE="/tmp/meeple_test_cookies.txt"

echo "=== AI-01 Integration Test ==="
echo

# Test 1: Health check
echo "1. Testing API health..."
HEALTH=$(curl -s $API_URL)
echo "Response: $HEALTH"
echo

# Test 2: Register user
echo "2. Registering test user..."
REGISTER_RESPONSE=$(curl -s -X POST $API_URL/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "dev",
    "tenantName": "Dev Tenant",
    "email": "test@dev.com",
    "password": "TestPass123!",
    "displayName": "Test User",
    "role": "admin"
  }' \
  -c $COOKIES_FILE)

echo "Response: $REGISTER_RESPONSE"
echo

# Test 3: Check auth status
echo "3. Checking authentication..."
AUTH_CHECK=$(curl -s $API_URL/auth/me -b $COOKIES_FILE)
echo "Response: $AUTH_CHECK"
echo

# Test 4: Seed demo data (creates game)
echo "4. Seeding demo data..."
SEED_RESPONSE=$(curl -s -X POST $API_URL/admin/seed \
  -H "Content-Type: application/json" \
  -d '{"tenantId": "dev", "gameId": "catan"}' \
  -b $COOKIES_FILE)

echo "Response: $SEED_RESPONSE"
echo

echo "=== Manual Test Required ==="
echo "To test PDF upload, run:"
echo "  curl -X POST $API_URL/ingest/pdf \\"
echo "    -b $COOKIES_FILE \\"
echo "    -F 'gameId=catan' \\"
echo "    -F 'file=@/path/to/your.pdf'"
echo
echo "To test RAG query after upload:"
echo "  curl -X POST $API_URL/agents/qa \\"
echo "    -H 'Content-Type: application/json' \\"
echo "    -b $COOKIES_FILE \\"
echo "    -d '{\"tenantId\":\"dev\",\"gameId\":\"catan\",\"query\":\"How do I win the game?\"}'"
