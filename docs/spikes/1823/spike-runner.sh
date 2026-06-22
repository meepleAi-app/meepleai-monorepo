#!/usr/bin/env bash
#
# #1823 M0 — Wikidata QID + Commons license spike runner
#
# Queries Wikidata SPARQL for each sample game's QID + wdt:P18 image,
# then fetches Commons extmetadata.LicenseShortName per image.
#
# Inputs:
#   - SAMPLE_FILE: docs/spikes/1823/sample-list.json (default)
#   - OUTPUT_FILE: docs/spikes/1823/spike-results.json (default)
#
# Outputs:
#   - per-game JSON record with: qid_found, image_p18, license_code, license_whitelist_match
#   - per-bucket aggregate at end
#
# Rate limit: 200ms sleep between SPARQL + Commons requests (5 RPS Wikidata published limit).
# User-Agent: MeepleAI-Spike/1.0 (contact@meepleai.app)
#

set -euo pipefail

SAMPLE_FILE="${SAMPLE_FILE:-docs/spikes/1823/sample-list.json}"
OUTPUT_FILE="${OUTPUT_FILE:-docs/spikes/1823/spike-results.json}"
USER_AGENT="MeepleAI-Spike/1.0 (https://github.com/meepleAi-app/meepleai-monorepo/issues/1823; contact@meepleai.app)"
SPARQL_ENDPOINT="https://query.wikidata.org/sparql"
COMMONS_API="https://commons.wikimedia.org/w/api.php"
RATE_LIMIT_SLEEP="0.21"  # 210ms = under 5 RPS

# License whitelist per DEC-3c
LICENSE_WHITELIST_REGEX='^(public domain|PD|CC0|CC[ -]BY([ -][0-9.]+)?|CC[ -]BY[ -]SA([ -][0-9.]+)?)$'

if ! command -v curl >/dev/null 2>&1 || ! command -v jq >/dev/null 2>&1; then
  echo "ERROR: curl and jq required" >&2
  exit 1
fi

if [ ! -f "$SAMPLE_FILE" ]; then
  echo "ERROR: sample file not found: $SAMPLE_FILE" >&2
  exit 1
fi

mkdir -p "$(dirname "$OUTPUT_FILE")"
echo "[" > "$OUTPUT_FILE"

TOTAL=$(jq 'length' "$SAMPLE_FILE")
echo "Processing $TOTAL games (rate-limited 5 RPS, ~$((TOTAL * 2 / 5))s estimated)..." >&2

FIRST=1
INDEX=0
while IFS= read -r game; do
  INDEX=$((INDEX + 1))
  TITLE=$(echo "$game" | jq -r '.title')
  YEAR=$(echo "$game" | jq -r '.year')
  BUCKET=$(echo "$game" | jq -r '.bucket')

  echo "[$INDEX/$TOTAL] $TITLE ($YEAR) [$BUCKET]" >&2

  # SPARQL: find boardgame QID by exact label match
  SPARQL_QUERY=$(cat <<EOF
SELECT ?game ?gameLabel ?image WHERE {
  ?game wdt:P31/wdt:P279* wd:Q131436 .
  ?game rdfs:label "$TITLE"@en .
  OPTIONAL { ?game wdt:P18 ?image . }
  SERVICE wikibase:label { bd:serviceParam wikibase:language "en" . }
}
LIMIT 5
EOF
)

  SPARQL_RESULT=$(curl -s -G \
    -H "User-Agent: $USER_AGENT" \
    -H "Accept: application/sparql-results+json" \
    --data-urlencode "query=$SPARQL_QUERY" \
    "$SPARQL_ENDPOINT" || echo '{"results":{"bindings":[]}}')

  QID_FOUND="false"
  IMAGE_P18="null"
  DISAMBIGUATION_REQUIRED="false"
  BINDINGS_COUNT=$(echo "$SPARQL_RESULT" | jq '.results.bindings | length' 2>/dev/null || echo "0")

  if [ "$BINDINGS_COUNT" -gt 0 ]; then
    QID_FOUND="true"
    if [ "$BINDINGS_COUNT" -gt 1 ]; then
      DISAMBIGUATION_REQUIRED="true"
    fi
    # Extract first image if present
    IMAGE_URL=$(echo "$SPARQL_RESULT" | jq -r '.results.bindings[0].image.value // empty' 2>/dev/null || echo "")
    if [ -n "$IMAGE_URL" ]; then
      # Convert wikimedia.org URL to Commons filename
      COMMONS_FILENAME=$(echo "$IMAGE_URL" | sed 's|^http.*/Special:FilePath/||' | sed 's|^http.*/wiki/||')
      IMAGE_P18=$(printf '%s' "$COMMONS_FILENAME" | jq -Rs '.')
    fi
  fi

  sleep "$RATE_LIMIT_SLEEP"

  # Commons API: fetch extmetadata.LicenseShortName for the image
  LICENSE_CODE="null"
  LICENSE_MACHINE_READABLE="false"
  LICENSE_WHITELIST_MATCH="false"

  if [ "$IMAGE_P18" != "null" ]; then
    FILENAME_RAW=$(echo "$IMAGE_P18" | jq -r '.')
    # Wikidata returns URL-encoded filenames (e.g. "7%20Wonders%20game.jpg");
    # Commons API expects the decoded raw filename. curl --data-urlencode would
    # double-encode percent escapes, so decode via python3 (always available
    # in dev/CI runners) before passing.
    # PYTHONIOENCODING=utf-8 avoids cp1252 charmap codec errors on Windows for
    # filenames containing non-ASCII chars (e.g. "Deskohraní" with U+011B).
    FILENAME_DECODED=$(PYTHONIOENCODING=utf-8 python3 -X utf8 -c "import urllib.parse, sys; print(urllib.parse.unquote(sys.argv[1]))" "$FILENAME_RAW")
    COMMONS_RESULT=$(curl -s -G \
      -H "User-Agent: $USER_AGENT" \
      --data-urlencode "action=query" \
      --data-urlencode "prop=imageinfo" \
      --data-urlencode "iiprop=extmetadata" \
      --data-urlencode "titles=File:$FILENAME_DECODED" \
      --data-urlencode "format=json" \
      "$COMMONS_API" || echo '{}')

    LICENSE_SHORT_NAME=$(echo "$COMMONS_RESULT" | jq -r '.query.pages | to_entries[0].value.imageinfo[0].extmetadata.LicenseShortName.value // empty' 2>/dev/null || echo "")

    if [ -n "$LICENSE_SHORT_NAME" ]; then
      LICENSE_MACHINE_READABLE="true"
      LICENSE_CODE=$(printf '%s' "$LICENSE_SHORT_NAME" | jq -Rs '.')
      # Match whitelist regex (case-insensitive)
      if echo "$LICENSE_SHORT_NAME" | grep -Ei "$LICENSE_WHITELIST_REGEX" >/dev/null 2>&1; then
        LICENSE_WHITELIST_MATCH="true"
      fi
    fi

    sleep "$RATE_LIMIT_SLEEP"
  fi

  # Emit per-game JSON record
  if [ "$FIRST" -eq 0 ]; then
    echo "," >> "$OUTPUT_FILE"
  fi
  FIRST=0

  cat >> "$OUTPUT_FILE" <<EOF
{
  "bucket": "$BUCKET",
  "title": $(printf '%s' "$TITLE" | jq -Rs '.'),
  "year": $YEAR,
  "qid_found": $QID_FOUND,
  "disambiguation_required": $DISAMBIGUATION_REQUIRED,
  "image_p18": $IMAGE_P18,
  "license_machine_readable": $LICENSE_MACHINE_READABLE,
  "license_code": $LICENSE_CODE,
  "license_whitelist_match": $LICENSE_WHITELIST_MATCH
}
EOF

done < <(jq -c '.[]' "$SAMPLE_FILE")

echo "]" >> "$OUTPUT_FILE"

echo "" >&2
echo "Spike complete. Results in $OUTPUT_FILE" >&2
echo "" >&2

# Aggregate
TOTAL=$(jq 'length' "$OUTPUT_FILE")
QID_FOUND=$(jq '[.[] | select(.qid_found == true)] | length' "$OUTPUT_FILE")
IMAGE_PRESENT=$(jq '[.[] | select(.image_p18 != null)] | length' "$OUTPUT_FILE")
LICENSE_READABLE=$(jq '[.[] | select(.license_machine_readable == true)] | length' "$OUTPUT_FILE")
LICENSE_WHITELIST=$(jq '[.[] | select(.license_whitelist_match == true)] | length' "$OUTPUT_FILE")

echo "=== Aggregate metrics ==="
echo "Sample size: $TOTAL"
echo "QID hit-rate: $QID_FOUND / $TOTAL = $(awk "BEGIN { printf \"%.1f\", ($QID_FOUND / $TOTAL) * 100 }")%"
echo "P18 image present: $IMAGE_PRESENT / $QID_FOUND"
echo "License machine-readable: $LICENSE_READABLE / $IMAGE_PRESENT"
echo "License whitelist match: $LICENSE_WHITELIST / $LICENSE_READABLE"
echo ""
echo "Per-bucket QID hit-rate:"
jq -r 'group_by(.bucket) | .[] | "  \(.[0].bucket): \([.[] | select(.qid_found == true)] | length) / \(length)"' "$OUTPUT_FILE"
