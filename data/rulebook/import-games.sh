#!/usr/bin/env bash
set -euo pipefail

# ============================================================================
# MeepleAI — Board Game Import Automation
# Reads manifest.json and imports games into the API (idempotent, resumable).
# ============================================================================

# --- Config -----------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MANIFEST="${SCRIPT_DIR}/manifest.json"
RULEBOOK_DIR="${SCRIPT_DIR}"
COOKIES="${SCRIPT_DIR}/.cookies"
API_BASE="${API_BASE:-http://localhost:8080/api/v1}"

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

# --- Prerequisite check -----------------------------------------------------
for cmd in jq curl; do
  if ! command -v "$cmd" &>/dev/null; then
    err "Required command not found: $cmd"
    exit 1
  fi
done

if [[ ! -f "$MANIFEST" ]]; then
  err "Manifest not found: $MANIFEST"
  exit 1
fi

# --- Manifest helpers -------------------------------------------------------
read_manifest() {
  cat "$MANIFEST"
}

# update_game_field <id> <field> <string_value>
update_game_field() {
  local id="$1" field="$2" value="$3"
  local tmp="${MANIFEST}.tmp"
  jq --argjson gid "$id" --arg f "$field" --arg v "$value" \
    '(.games[] | select(.id == $gid))[$f] = $v' \
    "$MANIFEST" > "$tmp" && mv "$tmp" "$MANIFEST"
}

# update_game_field_raw <id> <field> <raw_json_value>
update_game_field_raw() {
  local id="$1" field="$2" raw="$3"
  local tmp="${MANIFEST}.tmp"
  jq --argjson gid "$id" --arg f "$field" --argjson v "$raw" \
    '(.games[] | select(.id == $gid))[$f] = $v' \
    "$MANIFEST" > "$tmp" && mv "$tmp" "$MANIFEST"
}

# --- API helpers ------------------------------------------------------------
api_get() {
  curl -s "${CURL_TIMEOUTS[@]}" -b "$COOKIES" "${API_BASE}$1"
}

api_post() {
  curl -s "${CURL_TIMEOUTS[@]}" -b "$COOKIES" -c "$COOKIES" \
    -H "Content-Type: application/json" \
    -d "$2" "${API_BASE}$1"
}

# Cleanup trap — remove cookies on exit
trap 'rm -f "$COOKIES"' EXIT

# Default curl timeouts (connect + total)
CURL_TIMEOUTS=(--connect-timeout 10 --max-time 60)

# ============================================================================
# 1. Health check
# ============================================================================
check_health() {
  log "Checking API health..."
  local status
  status=$(curl -s -o /dev/null -w "%{http_code}" "${API_BASE}/health" 2>/dev/null || true)
  if [[ "$status" != "200" ]]; then
    err "API is not healthy (HTTP $status). Is the server running at ${API_BASE}?"
    exit 1
  fi
  log "API is healthy."
}

# ============================================================================
# 2. Login
# ============================================================================
login() {
  if [[ -z "${ADMIN_EMAIL:-}" || -z "${ADMIN_PASSWORD:-}" ]]; then
    err "Set ADMIN_EMAIL and ADMIN_PASSWORD environment variables."
    exit 1
  fi

  log "Logging in as ${ADMIN_EMAIL}..."
  local body
  body=$(jq -n --arg e "$ADMIN_EMAIL" --arg p "$ADMIN_PASSWORD" \
    '{"email":$e,"password":$p}')

  local resp
  resp=$(curl -s "${CURL_TIMEOUTS[@]}" -c "$COOKIES" -b "$COOKIES" \
    -H "Content-Type: application/json" \
    -d "$body" "${API_BASE}/auth/login")

  USER_ID=$(echo "$resp" | jq -r '.user.id // empty')
  if [[ -z "$USER_ID" ]]; then
    err "Login failed. Response: $resp"
    exit 1
  fi
  log "Logged in. User ID: ${USER_ID}"
}

# ============================================================================
# 3. Import games from BGG (batch)
# ============================================================================
import_games_from_bgg() {
  log "Collecting BGG games to import..."
  # Build [{bggId, name}, ...] array of games needing import
  local games_json
  games_json=$(read_manifest | jq '[.games[] | select(.bggFound == true and .sharedGameId == null) | {bggId, name}]')
  local count
  count=$(echo "$games_json" | jq 'length')

  if [[ "$count" -eq 0 ]]; then
    log "No BGG games to import (all already have sharedGameId or bggFound=false)."
    return
  fi

  log "Enqueuing ${count} BGG games via batch-json (with autoPublish)..."
  # batch-json endpoint extracts userId from session and supports autoPublish
  local json_content
  json_content=$(echo "$games_json" | jq -c .)
  local body
  body=$(jq -n --arg jc "$json_content" '{"jsonContent": $jc, "autoPublish": true}')
  local resp
  resp=$(api_post "/admin/bgg-queue/batch-json" "$body")
  log "Batch-json enqueue response: $resp"

  # Poll queue until empty or timeout (5 min)
  log "Polling BGG queue status..."
  local elapsed=0
  local max_wait=300
  while [[ $elapsed -lt $max_wait ]]; do
    local status_resp
    status_resp=$(api_get "/admin/bgg-queue/status")
    local queued processing
    queued=$(echo "$status_resp" | jq '.totalQueued // 0')
    processing=$(echo "$status_resp" | jq '.totalProcessing // 0')
    local pending=$((queued + processing))

    if [[ "$pending" -eq 0 ]]; then
      log "BGG queue is empty. All imports processed."
      return
    fi

    warn "Queue: ${queued} queued, ${processing} processing. Waiting 10s... (${elapsed}s/${max_wait}s)"
    sleep 10
    elapsed=$((elapsed + 10))
  done

  warn "BGG queue did not drain within ${max_wait}s. Continuing anyway."
}

# ============================================================================
# 4. Resolve shared game IDs (search by name)
# ============================================================================
resolve_shared_game_ids() {
  log "Resolving shared game IDs..."
  local games
  games=$(read_manifest | jq -c '.games[] | select(.bggFound == true and .sharedGameId == null)')

  if [[ -z "$games" ]]; then
    log "All BGG games already have sharedGameId."
    return
  fi

  while IFS= read -r game; do
    local id name
    id=$(echo "$game" | jq '.id')
    name=$(echo "$game" | jq -r '.name')

    log "  Searching for \"${name}\"..."
    local encoded_name
    encoded_name=$(printf '%s' "$name" | jq -sRr @uri)

    local resp
    resp=$(api_get "/shared-games?search=${encoded_name}&pageSize=5")
    local shared_id
    shared_id=$(echo "$resp" | jq -r '.items[0].id // empty')

    if [[ -n "$shared_id" ]]; then
      log "    Found: ${shared_id}"
      update_game_field "$id" "sharedGameId" "$shared_id"
    else
      warn "    No shared game found for \"${name}\". Will try manual import if bggFound=false."
    fi
  done <<< "$games"
}

# ============================================================================
# 5. Import games manually (non-BGG)
# ============================================================================
import_games_manual() {
  log "Checking for manual game imports (bggFound=false, no sharedGameId)..."
  local games
  games=$(read_manifest | jq -c '.games[] | select(.bggFound == false and .sharedGameId == null)')

  if [[ -z "$games" ]]; then
    log "No manual imports needed."
    return
  fi

  while IFS= read -r game; do
    local id name slug
    id=$(echo "$game" | jq '.id')
    name=$(echo "$game" | jq -r '.name')
    slug=$(echo "$game" | jq -r '.slug')
    local meta
    meta=$(echo "$game" | jq '.metadata')

    log "  Creating shared game: ${name}..."
    local body
    body=$(jq -n \
      --arg title "$name" \
      --arg slug "$slug" \
      --argjson meta "$meta" \
      '{
        "title": $title,
        "description": ("Board game: " + $title),
        "yearPublished": ($meta.yearPublished // 2020),
        "minPlayers": ($meta.minPlayers // 1),
        "maxPlayers": ($meta.maxPlayers // 4),
        "playingTimeMinutes": ($meta.playingTimeMinutes // 60),
        "minAge": ($meta.minAge // 14),
        "imageUrl": "",
        "thumbnailUrl": "",
        "designers": ($meta.designers // []),
        "publishers": ($meta.publishers // []),
        "categories": ($meta.categories // []),
        "mechanics": ($meta.mechanics // [])
      }')

    local resp
    resp=$(api_post "/admin/shared-games" "$body")
    # Response is a bare GUID string or JSON with id
    local shared_id
    shared_id=$(echo "$resp" | jq -r '. // empty' 2>/dev/null || echo "$resp" | tr -d '"')

    if [[ -z "$shared_id" || "$shared_id" == "null" ]]; then
      err "    Failed to create shared game for ${name}. Response: $resp"
      continue
    fi

    log "    Created: ${shared_id}. Quick-publishing..."
    api_post "/admin/shared-games/${shared_id}/quick-publish" "{}"
    update_game_field "$id" "sharedGameId" "$shared_id"
    log "    Done."
  done <<< "$games"
}

# ============================================================================
# 6. Download PDFs
# ============================================================================
download_pdfs() {
  log "Checking for PDFs to download..."
  local games
  games=$(read_manifest | jq -c '.games[] | select(.pdfStatus == "to_download")')

  if [[ -z "$games" ]]; then
    log "No PDFs to download."
    return
  fi

  while IFS= read -r game; do
    local id slug pdf_url
    id=$(echo "$game" | jq '.id')
    slug=$(echo "$game" | jq -r '.slug')
    pdf_url=$(echo "$game" | jq -r '.pdfUrl // empty')

    if [[ -z "$pdf_url" ]]; then
      warn "  Game ${slug}: pdfStatus=to_download but pdfUrl is empty. Skipping."
      continue
    fi

    local dest="${RULEBOOK_DIR}/${slug}_rulebook.pdf"

    # Skip if already downloaded
    if [[ -f "$dest" ]]; then
      local filetype
      filetype=$(file -b "$dest" | head -c 3)
      if [[ "$filetype" == "PDF" ]]; then
        log "  ${slug}: already downloaded. Marking as local."
        update_game_field "$id" "pdfStatus" "local"
        update_game_field "$id" "pdfLocal" "data/rulebook/${slug}_rulebook.pdf"
        continue
      fi
    fi

    log "  Downloading ${slug} from ${pdf_url}..."
    if curl -sL -f --connect-timeout 15 --max-time 300 -o "$dest" "$pdf_url"; then
      local filetype
      filetype=$(file -b "$dest" | head -c 3)
      if [[ "$filetype" == "PDF" ]]; then
        log "    OK (PDF verified)."
        update_game_field "$id" "pdfStatus" "local"
        update_game_field "$id" "pdfLocal" "data/rulebook/${slug}_rulebook.pdf"
      else
        err "    Downloaded file is not a PDF (${filetype}). Removing."
        rm -f "$dest"
        update_game_field "$id" "pdfStatus" "not_found"
      fi
    else
      err "    Download failed for ${slug}."
      rm -f "$dest"
      update_game_field "$id" "pdfStatus" "not_found"
    fi
  done <<< "$games"
}

# ============================================================================
# 7. Upload PDFs
# ============================================================================
upload_pdfs() {
  log "Checking for PDFs to upload..."
  local games
  games=$(read_manifest | jq -c '.games[] | select(.pdfStatus == "local" and .documentId == null and .sharedGameId != null)')

  if [[ -z "$games" ]]; then
    log "No PDFs to upload."
    return
  fi

  while IFS= read -r game; do
    local id name slug shared_game_id pdf_local is_expansion language
    id=$(echo "$game" | jq '.id')
    name=$(echo "$game" | jq -r '.name')
    slug=$(echo "$game" | jq -r '.slug')
    shared_game_id=$(echo "$game" | jq -r '.sharedGameId')
    pdf_local=$(echo "$game" | jq -r '.pdfLocal // empty')
    is_expansion=$(echo "$game" | jq -r '.isExpansion')
    language=$(echo "$game" | jq -r '.language // "en"')

    if [[ -z "$pdf_local" ]]; then
      warn "  ${slug}: pdfLocal is empty. Skipping."
      continue
    fi

    # Resolve path relative to repo root
    local pdf_path="${SCRIPT_DIR}/../../${pdf_local}"
    if [[ ! -f "$pdf_path" ]]; then
      # Try relative to script dir
      pdf_path="${RULEBOOK_DIR}/${slug}_rulebook.pdf"
    fi

    if [[ ! -f "$pdf_path" ]]; then
      err "  ${slug}: PDF file not found at ${pdf_local}. Skipping."
      continue
    fi

    local version_type="base"
    if [[ "$is_expansion" == "true" ]]; then
      version_type="expansion"
    fi

    log "  Uploading ${slug} (${shared_game_id})..."
    local resp
    resp=$(curl -s --connect-timeout 10 --max-time 600 -b "$COOKIES" \
      -F "file=@${pdf_path}" \
      -F "gameId=${shared_game_id}" \
      -F "gameName=${name}" \
      -F "versionType=${version_type}" \
      -F "language=${language}" \
      -F "versionNumber=1.0" \
      "${API_BASE}/ingest/pdf")

    local doc_id
    doc_id=$(echo "$resp" | jq -r '.documentId // .jobId // empty')

    if [[ -n "$doc_id" ]]; then
      log "    Uploaded. Document/Job ID: ${doc_id}"
      update_game_field "$id" "documentId" "$doc_id"
      update_game_field "$id" "indexingStatus" "Processing"
    else
      err "    Upload failed for ${slug}. Response: $resp"
    fi
  done <<< "$games"
}

# ============================================================================
# 8. Poll indexing status
# ============================================================================
poll_indexing() {
  log "Polling indexing status..."
  local games
  games=$(read_manifest | jq -c '.games[] | select(.documentId != null and .indexingStatus != "Completed")')

  if [[ -z "$games" ]]; then
    log "No games pending indexing."
    return
  fi

  while IFS= read -r game; do
    local id name slug shared_game_id
    id=$(echo "$game" | jq '.id')
    name=$(echo "$game" | jq -r '.name')
    slug=$(echo "$game" | jq -r '.slug')
    shared_game_id=$(echo "$game" | jq -r '.sharedGameId')

    log "  Polling ${name}..."
    local elapsed=0
    local max_wait=300

    while [[ $elapsed -lt $max_wait ]]; do
      local resp
      resp=$(api_get "/knowledge-base/${shared_game_id}/status")
      local status progress error_msg
      status=$(echo "$resp" | jq -r '.status // "Unknown"')
      progress=$(echo "$resp" | jq -r '.progress // 0')
      error_msg=$(echo "$resp" | jq -r '.errorMessage // empty')

      if [[ "$status" == "Completed" ]]; then
        log "    ${name}: Completed!"
        update_game_field "$id" "indexingStatus" "Completed"
        break
      elif [[ "$status" == "Failed" ]]; then
        err "    ${name}: Failed! ${error_msg}"
        update_game_field "$id" "indexingStatus" "Failed"
        break
      fi

      warn "    ${name}: ${status} (${progress}%). Waiting 10s... (${elapsed}s/${max_wait}s)"
      sleep 10
      elapsed=$((elapsed + 10))
    done

    if [[ $elapsed -ge $max_wait ]]; then
      warn "    ${name}: Timed out after ${max_wait}s. Current status preserved."
      update_game_field "$id" "indexingStatus" "Timeout"
    fi
  done <<< "$games"
}

# ============================================================================
# 9. Print report
# ============================================================================
print_report() {
  echo ""
  printf "${BOLD}${CYAN}%-4s %-42s %-38s %-16s %-12s${NC}\n" \
    "ID" "Name" "SharedGameId" "PDF Status" "Indexing"
  printf '%.0s-' {1..115}
  echo ""

  read_manifest | jq -r '.games[] | [
    .id,
    .name,
    (.sharedGameId // "-"),
    (.pdfStatus // "-"),
    (.indexingStatus // "-")
  ] | @tsv' | while IFS=$'\t' read -r gid gname gshared gpdf gindex; do
    printf "%-4s %-42s %-38s %-16s %-12s\n" \
      "$gid" "$gname" "$gshared" "$gpdf" "$gindex"
  done
  echo ""
}

# ============================================================================
# 10. Create a game night
# ============================================================================
create_game_night() {
  log "Looking for a completed game to create a game night..."
  local game
  game=$(read_manifest | jq -c '[.games[] | select(.indexingStatus == "Completed")] | first // empty')

  if [[ -z "$game" || "$game" == "null" ]]; then
    warn "No game with indexingStatus=Completed. Skipping game night creation."
    return
  fi

  local name shared_game_id
  name=$(echo "$game" | jq -r '.name')
  shared_game_id=$(echo "$game" | jq -r '.sharedGameId')
  local max_players
  max_players=$(echo "$game" | jq '.metadata.maxPlayers // 4')

  local scheduled_at
  scheduled_at=$(date -u -d "+7 days" +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null \
    || date -u -v+7d +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null \
    || echo "2026-04-15T19:00:00Z")

  log "Creating game night for \"${name}\"..."
  local body
  body=$(jq -n \
    --arg title "Game Night: ${name}" \
    --arg desc "Let's play ${name}!" \
    --arg date "$scheduled_at" \
    --arg loc "MeepleAI HQ" \
    --argjson mp "$max_players" \
    --arg gid "$shared_game_id" \
    '{"title":$title,"scheduledAt":$date,"description":$desc,"location":$loc,"maxPlayers":$mp,"gameIds":[$gid]}')

  local resp
  resp=$(api_post "/game-nights" "$body")
  local night_id
  night_id=$(echo "$resp" | jq -r '. // empty' 2>/dev/null || echo "$resp" | tr -d '"')

  if [[ -z "$night_id" || "$night_id" == "null" ]]; then
    err "Failed to create game night. Response: $resp"
    return
  fi

  log "Created game night: ${night_id}. Publishing..."
  api_post "/game-nights/${night_id}/publish" "{}"
  log "Game night published!"
}

# ============================================================================
# Main
# ============================================================================
main() {
  echo ""
  printf "${BOLD}${CYAN}=== MeepleAI Game Import Automation ===${NC}\n"
  echo ""

  check_health
  login
  import_games_from_bgg
  resolve_shared_game_ids
  import_games_manual
  download_pdfs
  upload_pdfs
  poll_indexing
  print_report
  create_game_night

  # Cleanup
  rm -f "$COOKIES"
  log "Done. Cookies cleaned up."
}

main "$@"
