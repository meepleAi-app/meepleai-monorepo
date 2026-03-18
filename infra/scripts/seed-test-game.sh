#!/usr/bin/env bash
# seed-test-game.sh — Seeds a test game + PDF for US validation
# Usage: ./seed-test-game.sh <path-to-pdf>
#
# Prerequisites:
#   - make dev running (all services healthy)
#   - Admin password in infra/secrets/dev/admin.secret

set -euo pipefail

API_BASE="http://localhost:8080/api/v1"
COOKIE_JAR="/tmp/meepleai-seed-cookies.txt"
PDF_FILE="${1:?Usage: $0 <path-to-pdf-file>}"

if [ ! -f "$PDF_FILE" ]; then
  echo "ERROR: PDF file not found: $PDF_FILE"
  exit 1
fi

# Read admin password from secrets
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ADMIN_SECRET_FILE="$SCRIPT_DIR/../secrets/dev/admin.secret"
if [ ! -f "$ADMIN_SECRET_FILE" ]; then
  echo "ERROR: Admin secret not found at $ADMIN_SECRET_FILE"
  echo "Run: cd infra && make secrets-dev"
  exit 1
fi
ADMIN_PASSWORD=$(grep -oP 'ADMIN_PASSWORD=\K.*' "$ADMIN_SECRET_FILE" || true)
if [ -z "$ADMIN_PASSWORD" ]; then
  echo "ERROR: ADMIN_PASSWORD not found in $ADMIN_SECRET_FILE"
  exit 1
fi

ADMIN_EMAIL="admin@meepleai.dev"

echo "=== Step 1: Admin Login ==="
LOGIN_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" \
  -c "$COOKIE_JAR")
HTTP_CODE=$(echo "$LOGIN_RESPONSE" | tail -1)
BODY=$(echo "$LOGIN_RESPONSE" | sed '$d')

if [ "$HTTP_CODE" != "200" ]; then
  echo "ERROR: Login failed (HTTP $HTTP_CODE): $BODY"
  exit 1
fi
echo "Admin login OK"

echo ""
echo "=== Step 2: Create SharedGame ==="
CREATE_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/admin/shared-games" \
  -H "Content-Type: application/json" \
  -b "$COOKIE_JAR" \
  -d '{
    "title": "I Coloni di Catan",
    "yearPublished": 1995,
    "description": "Costruisci insediamenti, commercia risorse e diventa il dominatore dell isola di Catan. Un classico gioco di strategia per 3-4 giocatori.",
    "minPlayers": 3,
    "maxPlayers": 4,
    "playingTimeMinutes": 75,
    "minAge": 10,
    "complexityRating": 2.3,
    "averageRating": 7.2,
    "imageUrl": "https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__original/img/IEhEYdJsTGlAmFp-KSOAZX5ypPU=/0x0/filters:format(jpeg)/pic2419375.jpg",
    "designers": ["Klaus Teuber"],
    "publishers": ["Kosmos", "Giochi Uniti"],
    "categories": ["Strategy", "Negotiation"],
    "mechanics": ["Dice Rolling", "Trading", "Route Building"],
    "bggId": 13
  }')
HTTP_CODE=$(echo "$CREATE_RESPONSE" | tail -1)
GAME_ID=$(echo "$CREATE_RESPONSE" | sed '$d' | tr -d '"')

if [ "$HTTP_CODE" != "201" ]; then
  echo "ERROR: Create game failed (HTTP $HTTP_CODE): $GAME_ID"
  exit 1
fi
echo "Game created: $GAME_ID"

echo ""
echo "=== Step 3: Quick-Publish Game ==="
PUBLISH_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST \
  "$API_BASE/admin/shared-games/$GAME_ID/quick-publish" \
  -b "$COOKIE_JAR")
HTTP_CODE=$(echo "$PUBLISH_RESPONSE" | tail -1)

if [ "$HTTP_CODE" != "204" ]; then
  BODY=$(echo "$PUBLISH_RESPONSE" | sed '$d')
  echo "ERROR: Quick-publish failed (HTTP $HTTP_CODE): $BODY"
  exit 1
fi
echo "Game published OK"

echo ""
echo "=== Step 4: Upload PDF ==="
UPLOAD_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/ingest/pdf" \
  -b "$COOKIE_JAR" \
  -F "file=@$PDF_FILE" \
  -F "gameId=$GAME_ID")
HTTP_CODE=$(echo "$UPLOAD_RESPONSE" | tail -1)
BODY=$(echo "$UPLOAD_RESPONSE" | sed '$d')

if [ "$HTTP_CODE" != "200" ] && [ "$HTTP_CODE" != "201" ]; then
  echo "ERROR: PDF upload failed (HTTP $HTTP_CODE): $BODY"
  exit 1
fi

# Extract pdfId from response
PDF_ID=$(echo "$BODY" | grep -oP '"documentId"\s*:\s*"\K[^"]+' || echo "$BODY" | grep -oP '"id"\s*:\s*"\K[^"]+' || echo "")
if [ -z "$PDF_ID" ]; then
  echo "WARNING: Could not extract PDF ID from response. Full response:"
  echo "$BODY"
  echo ""
  echo "Game ID: $GAME_ID"
  echo "You may need to check processing status manually."
  exit 0
fi
echo "PDF uploaded: $PDF_ID"

echo ""
echo "=== Step 5: Poll Processing Status ==="
MAX_WAIT=300
ELAPSED=0
INTERVAL=10
CURRENT_STATE="unknown"

while [ $ELAPSED -lt $MAX_WAIT ]; do
  STATUS_RESPONSE=$(curl -s -X GET "$API_BASE/pdfs/$PDF_ID/progress" \
    -b "$COOKIE_JAR")
  CURRENT_STATE=$(echo "$STATUS_RESPONSE" | grep -oP '"currentState"\s*:\s*"\K[^"]+' || echo "unknown")
  PROGRESS=$(echo "$STATUS_RESPONSE" | grep -oP '"overallProgress"\s*:\s*\K[0-9.]+' || echo "0")

  echo "  [$ELAPSED s] State: $CURRENT_STATE | Progress: ${PROGRESS}%"

  if [ "$CURRENT_STATE" = "Ready" ]; then
    echo ""
    echo "PDF Processing Complete!"
    break
  fi

  if [ "$CURRENT_STATE" = "Failed" ]; then
    echo "ERROR: PDF processing failed!"
    echo "$STATUS_RESPONSE"
    exit 1
  fi

  sleep $INTERVAL
  ELAPSED=$((ELAPSED + INTERVAL))
done

if [ $ELAPSED -ge $MAX_WAIT ]; then
  echo "WARNING: Timeout waiting for PDF processing ($MAX_WAIT s). Check status manually."
fi

echo ""
echo "=== Step 6: Register Test User ==="
REG_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"email":"testuser@meepleai.dev","password":"TestUser123!"}')
HTTP_CODE=$(echo "$REG_RESPONSE" | tail -1)
BODY=$(echo "$REG_RESPONSE" | sed '$d')

if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "201" ]; then
  echo "Test user registered: testuser@meepleai.dev / TestUser123!"
elif echo "$BODY" | grep -qi "already exists\|duplicate\|conflict"; then
  echo "Test user already exists (OK): testuser@meepleai.dev / TestUser123!"
else
  echo "WARNING: User registration returned HTTP $HTTP_CODE: $BODY"
  echo "You may need to create a user manually via the frontend."
fi

echo ""
echo "========================================="
echo "  SEED COMPLETE"
echo "========================================="
echo ""
echo "  Game ID:    $GAME_ID"
echo "  Game Title: I Coloni di Catan"
echo "  PDF ID:     $PDF_ID"
echo "  PDF Status: $CURRENT_STATE"
echo ""
echo "  Test User:  testuser@meepleai.dev"
echo "  Password:   TestUser123!"
echo ""
echo "  Next: Open http://localhost:3000"
echo "  Follow: docs/testing/us-add-game-ownership-rag-chat-checklist.md"
echo "========================================="

# Cleanup
rm -f "$COOKIE_JAR"
