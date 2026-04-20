#!/usr/bin/env bash
set -euo pipefail

# ============================================================================
# MeepleAI — Group Import Script
# Processes a subset of games from manifest.json by ID range.
# Usage: ./import-group.sh <from_id> <to_id>
# Example: ./import-group.sh 20 35
# ============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MANIFEST="${SCRIPT_DIR}/manifest.json"

# --- Colors -----------------------------------------------------------------
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

# --- Logging ----------------------------------------------------------------
log()  { printf "${GREEN}[+]${NC} %s\n" "$*"; }
warn() { printf "${YELLOW}[!]${NC} %s\n" "$*"; }
err()  { printf "${RED}[x]${NC} %s\n" "$*" >&2; }

# --- Argument validation ----------------------------------------------------
if [[ $# -ne 2 ]]; then
  err "Usage: $0 <from_id> <to_id>"
  err "Example: $0 20 35"
  exit 1
fi

FROM_ID="$1"
TO_ID="$2"

if ! [[ "$FROM_ID" =~ ^[0-9]+$ ]] || ! [[ "$TO_ID" =~ ^[0-9]+$ ]]; then
  err "Both arguments must be positive integers."
  exit 1
fi

if [[ "$FROM_ID" -gt "$TO_ID" ]]; then
  err "FROM_ID ($FROM_ID) must be <= TO_ID ($TO_ID)."
  exit 1
fi

if [[ ! -f "$MANIFEST" ]]; then
  err "Manifest not found: $MANIFEST"
  exit 1
fi

# --- Prerequisite check -----------------------------------------------------
for cmd in jq bash; do
  if ! command -v "$cmd" &>/dev/null; then
    err "Required command not found: $cmd"
    exit 1
  fi
done

# --- Step 1: Backup manifest ------------------------------------------------
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP="${MANIFEST}.bak-${TIMESTAMP}"
cp "$MANIFEST" "$BACKUP"
log "Manifest backed up to: $(basename "$BACKUP")"

# --- Step 2: Show games in range --------------------------------------------
echo ""
printf "${BOLD}${CYAN}=== Games in range [%d .. %d] ===${NC}\n" "$FROM_ID" "$TO_ID"
echo ""
printf "${BOLD}%-4s %-42s %-16s %-12s${NC}\n" "ID" "Name" "PDF Status" "Indexing"
printf '%.0s-' {1..80}
echo ""

GAME_COUNT=$(jq --argjson from "$FROM_ID" --argjson to "$TO_ID" \
  '[.games[] | select(.id >= $from and .id <= $to)] | length' "$MANIFEST")

jq -r --argjson from "$FROM_ID" --argjson to "$TO_ID" \
  '.games[] | select(.id >= $from and .id <= $to) |
   [.id, .name, (.pdfStatus // "-"), (.indexingStatus // "-")] | @tsv' "$MANIFEST" |
  while IFS=$'\t' read -r gid gname gpdf gindex; do
    printf "%-4s %-42s %-16s %-12s\n" "$gid" "$gname" "$gpdf" "$gindex"
  done

echo ""
log "Found ${GAME_COUNT} games in range."

if [[ "$GAME_COUNT" -eq 0 ]]; then
  err "No games found in ID range [$FROM_ID .. $TO_ID]. Aborting."
  rm -f "$BACKUP"
  exit 1
fi

# --- Step 3: Prompt for confirmation ----------------------------------------
echo ""
warn "Press Enter to start import, Ctrl+C to cancel..."
read -r

# --- Step 4: Create temp manifest with only games in range ------------------
TEMP_MANIFEST="${SCRIPT_DIR}/manifest_group_${FROM_ID}_${TO_ID}.json"

jq --argjson from "$FROM_ID" --argjson to "$TO_ID" \
  '. | .games = [.games[] | select(.id >= $from and .id <= $to)]' \
  "$MANIFEST" > "$TEMP_MANIFEST"

log "Temp manifest created: $(basename "$TEMP_MANIFEST") (${GAME_COUNT} games)"

# --- Step 5: Run import-games.sh with manifest override --------------------
echo ""
printf "${BOLD}${CYAN}=== Starting import pipeline ===${NC}\n"
echo ""

MANIFEST_OVERRIDE="$TEMP_MANIFEST" bash "${SCRIPT_DIR}/import-games.sh" || {
  IMPORT_EXIT=$?
  warn "import-games.sh exited with code ${IMPORT_EXIT}. Attempting to merge results anyway."
}

# --- Step 6: Atomic merge results back into main manifest -------------------
log "Merging results back into main manifest..."

MERGE_TMP="${MANIFEST}.merge_tmp"

jq -s '
  .[0] as $main | .[1].games as $updates |
  $main | .games = [.games[] | . as $g |
    ($updates | map(select(.id == $g.id)) | first) // $g
  ]
' "$MANIFEST" "$TEMP_MANIFEST" > "$MERGE_TMP" && mv "$MERGE_TMP" "$MANIFEST"

log "Merge complete."

# --- Step 7: Clean up temp manifest ----------------------------------------
rm -f "$TEMP_MANIFEST"
log "Temp manifest removed."

# --- Step 8: Print group report ---------------------------------------------
echo ""
printf "${BOLD}${CYAN}=== Group Import Report [%d .. %d] ===${NC}\n" "$FROM_ID" "$TO_ID"
echo ""
printf "${BOLD}%-4s %-42s %-38s %-16s %-12s${NC}\n" \
  "ID" "Name" "SharedGameId" "PDF Status" "Indexing"
printf '%.0s-' {1..115}
echo ""

jq -r --argjson from "$FROM_ID" --argjson to "$TO_ID" \
  '.games[] | select(.id >= $from and .id <= $to) |
   [.id, .name, (.sharedGameId // "-"), (.pdfStatus // "-"), (.indexingStatus // "-")] | @tsv' "$MANIFEST" |
  while IFS=$'\t' read -r gid gname gshared gpdf gindex; do
    printf "%-4s %-42s %-38s %-16s %-12s\n" \
      "$gid" "$gname" "$gshared" "$gpdf" "$gindex"
  done

echo ""

# Summary counts
COMPLETED=$(jq --argjson from "$FROM_ID" --argjson to "$TO_ID" \
  '[.games[] | select(.id >= $from and .id <= $to and .indexingStatus == "Completed")] | length' "$MANIFEST")
FAILED=$(jq --argjson from "$FROM_ID" --argjson to "$TO_ID" \
  '[.games[] | select(.id >= $from and .id <= $to and .indexingStatus == "Failed")] | length' "$MANIFEST")
PENDING=$(jq --argjson from "$FROM_ID" --argjson to "$TO_ID" \
  '[.games[] | select(.id >= $from and .id <= $to and (.indexingStatus == "Processing" or .indexingStatus == "Timeout"))] | length' "$MANIFEST")

printf "${GREEN}Completed: ${COMPLETED}${NC} | ${RED}Failed: ${FAILED}${NC} | ${YELLOW}Pending: ${PENDING}${NC} | Total: ${GAME_COUNT}\n"
echo ""
log "Group import finished. Backup at: $(basename "$BACKUP")"
