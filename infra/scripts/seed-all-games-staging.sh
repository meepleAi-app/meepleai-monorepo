#!/usr/bin/env bash
# seed-all-games-staging.sh — Seeds all games from data/rulebook/ to staging
# Usage: ./seed-all-games-staging.sh
# Requires: jq, curl, cookies already set in /tmp/meepleai-staging-cookies.txt

set -euo pipefail

API_BASE="https://meepleai.app/api/v1"
COOKIE_JAR="/tmp/meepleai-staging-cookies.txt"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
METADATA_FILE="$SCRIPT_DIR/games-metadata.json"
RULEBOOK_DIR="$SCRIPT_DIR/../../data/rulebook"
RESULTS_FILE="/tmp/meepleai-seed-results.csv"

if [ ! -f "$COOKIE_JAR" ]; then
  echo "ERROR: No cookie jar. Login first."
  exit 1
fi

if [ ! -f "$METADATA_FILE" ]; then
  echo "ERROR: Metadata file not found: $METADATA_FILE"
  exit 1
fi

# Initialize results file
echo "game,game_id,pdf_id,status" > "$RESULTS_FILE"

GAME_COUNT=$(python3 -c "import json; print(len(json.load(open('$METADATA_FILE'))))")
echo "=== Seeding $GAME_COUNT games ==="
echo ""

for i in $(seq 0 $((GAME_COUNT - 1))); do
  # Extract game metadata
  GAME_JSON=$(python3 -c "
import json
games = json.load(open('$METADATA_FILE'))
g = games[$i]
payload = {
    'title': g['title'],
    'yearPublished': g['year'],
    'description': g['desc'],
    'minPlayers': g['min'],
    'maxPlayers': g['max'],
    'playingTimeMinutes': g['time'],
    'minAge': g['age'],
    'complexityRating': g['complexity'],
    'averageRating': g['rating'],
    'imageUrl': 'https://via.placeholder.com/300x300?text=' + g['title'].replace(' ', '+'),
    'thumbnailUrl': 'https://via.placeholder.com/150x150?text=' + g['title'].replace(' ', '+'),
    'designers': g['designers'],
    'publishers': g['publishers'],
    'categories': g['categories'],
    'mechanics': g['mechanics'],
    'bggId': g['bggId']
}
print(json.dumps(payload))
")

  PDF_FILE=$(python3 -c "import json; print(json.load(open('$METADATA_FILE'))[$i]['pdf'])")
  TITLE=$(python3 -c "import json; print(json.load(open('$METADATA_FILE'))[$i]['title'])")
  PDF_PATH="$RULEBOOK_DIR/$PDF_FILE"

  echo "--- [$((i+1))/$GAME_COUNT] $TITLE ---"

  if [ ! -f "$PDF_PATH" ]; then
    echo "  SKIP: PDF not found: $PDF_FILE"
    echo "$TITLE,,,$PDF_FILE not found" >> "$RESULTS_FILE"
    continue
  fi

  # Create game
  RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/admin/shared-games" \
    -H "Content-Type: application/json" \
    -b "$COOKIE_JAR" \
    --data-raw "$GAME_JSON" 2>/dev/null)
  HTTP_CODE=$(echo "$RESPONSE" | tail -1)
  GAME_ID=$(echo "$RESPONSE" | sed '$d' | tr -d '"')

  if [ "$HTTP_CODE" != "201" ]; then
    echo "  FAIL Create: HTTP $HTTP_CODE"
    echo "$TITLE,,,create_failed_$HTTP_CODE" >> "$RESULTS_FILE"
    continue
  fi
  echo "  Created: $GAME_ID"

  # Quick-publish
  PUB_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
    "$API_BASE/admin/shared-games/$GAME_ID/quick-publish" \
    -b "$COOKIE_JAR" 2>/dev/null)

  if [ "$PUB_CODE" != "204" ]; then
    echo "  FAIL Publish: HTTP $PUB_CODE"
    echo "$TITLE,$GAME_ID,,publish_failed_$PUB_CODE" >> "$RESULTS_FILE"
    continue
  fi
  echo "  Published"

  # Upload PDF
  UPLOAD=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/ingest/pdf" \
    -b "$COOKIE_JAR" \
    -F "file=@$PDF_PATH" \
    -F "gameId=$GAME_ID" 2>/dev/null)
  UP_CODE=$(echo "$UPLOAD" | tail -1)
  UP_BODY=$(echo "$UPLOAD" | sed '$d')

  if [ "$UP_CODE" != "200" ] && [ "$UP_CODE" != "201" ]; then
    echo "  FAIL Upload: HTTP $UP_CODE - $UP_BODY"
    echo "$TITLE,$GAME_ID,,upload_failed_$UP_CODE" >> "$RESULTS_FILE"
    continue
  fi

  PDF_ID=$(echo "$UP_BODY" | grep -oP '"documentId"\s*:\s*"\K[^"]+' || echo "unknown")
  echo "  Uploaded: $PDF_ID"
  echo "$TITLE,$GAME_ID,$PDF_ID,uploaded" >> "$RESULTS_FILE"

  # Wait for processing (max 5 min per game)
  echo "  Waiting for processing..."
  MAX_WAIT=300
  ELAPSED=0
  FINAL_STATE="unknown"

  while [ $ELAPSED -lt $MAX_WAIT ]; do
    sleep 15
    ELAPSED=$((ELAPSED + 15))

    STATUS=$(curl -s "$API_BASE/pdfs/$PDF_ID/progress" -b "$COOKIE_JAR" 2>/dev/null)
    STATE=$(echo "$STATUS" | grep -oP '"currentStep"\s*:\s*"\K[^"]+' || \
            echo "$STATUS" | grep -oP '"currentState"\s*:\s*"\K[^"]+' || echo "unknown")
    PCT=$(echo "$STATUS" | grep -oP '"percentComplete"\s*:\s*\K[0-9.]+' || \
          echo "$STATUS" | grep -oP '"overallProgress"\s*:\s*\K[0-9.]+' || echo "0")

    printf "  [%3ds] %s (%s%%)\n" "$ELAPSED" "$STATE" "$PCT"

    if [ "$STATE" = "Ready" ] || [ "$STATE" = "Completed" ]; then
      FINAL_STATE="Ready"
      break
    fi

    if [ "$STATE" = "Failed" ]; then
      FINAL_STATE="Failed"
      break
    fi
  done

  if [ "$FINAL_STATE" = "Ready" ]; then
    echo "  OK: Processing complete"
    # Update results
    sed -i "s|$TITLE,$GAME_ID,$PDF_ID,uploaded|$TITLE,$GAME_ID,$PDF_ID,ready|" "$RESULTS_FILE"
  elif [ "$FINAL_STATE" = "Failed" ]; then
    echo "  FAIL: Processing failed"
    sed -i "s|$TITLE,$GAME_ID,$PDF_ID,uploaded|$TITLE,$GAME_ID,$PDF_ID,failed|" "$RESULTS_FILE"
  else
    echo "  TIMEOUT: Still processing after ${MAX_WAIT}s"
    sed -i "s|$TITLE,$GAME_ID,$PDF_ID,uploaded|$TITLE,$GAME_ID,$PDF_ID,timeout|" "$RESULTS_FILE"
  fi

  echo ""
done

echo "========================================="
echo "  SEED COMPLETE — Results:"
echo "========================================="
cat "$RESULTS_FILE"
echo ""
echo "Results saved to: $RESULTS_FILE"
