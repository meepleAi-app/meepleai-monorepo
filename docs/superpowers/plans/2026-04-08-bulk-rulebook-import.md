# Bulk Rulebook Import — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import 19 board games with rulebook PDFs into MeepleAI, run embedding/indexing, and create a game night session.

**Architecture:** Two-phase approach — Phase 1 researches BGG IDs and PDF URLs offline into a manifest.json. Phase 2 is a bash script that reads the manifest, calls the API to import games, upload PDFs, poll indexing, and create a game night. Auth uses session cookies via `curl -c/-b`.

**Tech Stack:** Bash (curl, jq), MeepleAI REST API, BoardGameGeek XML API

---

## File Structure

```
data/rulebook/
  manifest.json              # CREATE: Research data + execution state for all 20 entries
  import-games.sh            # CREATE: Bash automation script (~200 lines)
```

Existing PDFs (no changes):
- `data/rulebook/mage-knight_rulebook.pdf`
- `data/rulebook/dominion_rulebook.pdf`
- `data/rulebook/marvel-champions_rulebook.pdf`
- `data/rulebook/barrage_rulebook.pdf`

---

## Task 1: Research BGG IDs for All Games

**Files:**
- Create: `data/rulebook/manifest.json`

Use WebSearch to find the exact BoardGameGeek ID for each of the 20 entries. The BGG ID is the number in URLs like `boardgamegeek.com/boardgame/248562/mage-knight-ultimate-edition`.

- [ ] **Step 1: Search BGG IDs in parallel batches**

Search BoardGameGeek for each game. Disambiguate by year/publisher when multiple results exist.

Games to search:
1. Townsfolk Tussle (Panic Roll Games, 2022)
2. Blueprints (Z-Man Games, 2013)
3. Massive Darkness (CMON, 2017)
4. ISS Vanguard (Awaken Realms, 2023)
5. Mage Knight (WizKids, 2011)
6. Nanolith (board game — verify publisher/year)
7. Dark Souls: The Board Game (Steamforged Games, 2017)
8. RONE: Invasion (Greiferwarenhaus, verify year)
9. Frostpunk: The Board Game (Glass Cannon Unplugged, 2022)
10. Rise of Sovereigns (verify publisher/year)
11. Street Fighter V: The Miniatures Game (Jasco Games, verify year)
12a. Battlestar Galactica (FFG, 2008)
12b. Battlestar Galactica: Exodus (FFG, 2010)
13. Dominion (Rio Grande Games, 2008)
14. Marvel Champions: The Card Game (FFG, 2019)
15. Paleo (Hans im Gluck, 2020)
16. Leviathan Wilds (Moon Crab Games, verify year)
17. Marvel United (CMON, 2020)
18. Barrage (Cranio Creations, 2019)
19. Masters of the Universe: Fields of Eternia (CMON, verify year)

- [ ] **Step 2: Record BGG IDs in manifest.json**

Create `data/rulebook/manifest.json` with all found BGG IDs. For games not found on BGG, set `bggFound: false` and populate metadata manually from web research.

```json
{
  "version": "1.0",
  "createdAt": "2026-04-08",
  "languagePriority": ["en", "it"],
  "games": [
    {
      "id": 1,
      "name": "Townsfolk Tussle",
      "slug": "townsfolk-tussle",
      "bggId": null,
      "bggFound": false,
      "language": "en",
      "pdfUrl": null,
      "pdfLocal": null,
      "pdfStatus": "not_found",
      "isExpansion": false,
      "baseGameSlug": null,
      "sharedGameId": null,
      "documentId": null,
      "indexingStatus": null,
      "metadata": {
        "yearPublished": 2022,
        "minPlayers": 1,
        "maxPlayers": 4,
        "playingTimeMinutes": 90,
        "designers": [],
        "publishers": ["Panic Roll Games"],
        "categories": [],
        "mechanics": []
      }
    }
  ]
}
```

- [ ] **Step 3: Commit manifest with BGG IDs**

```bash
git add data/rulebook/manifest.json
git commit -m "feat(rulebook): add manifest.json with BGG IDs for 20 games"
```

---

## Task 2: Research PDF URLs for Games Without Local PDFs

**Files:**
- Modify: `data/rulebook/manifest.json`

For 16 entries without local PDFs, search for downloadable rulebook PDFs.

- [ ] **Step 1: Search PDF URLs in parallel batches**

For each game without a local PDF, search:
1. Publisher's official website (e.g., `cmon.com/products`, `fantasyflightgames.com`)
2. BGG files section: `boardgamegeek.com/boardgame/{bggId}/files`
3. Known repositories

Language priority: English first, Italian as fallback.

Games needing PDF search:
| # | Game | Publisher | Likely Source |
|---|------|-----------|---------------|
| 1 | Townsfolk Tussle | Panic Roll | panic-roll.com |
| 2 | Blueprints | Z-Man | zmangames.com |
| 3 | Massive Darkness | CMON | cmon.com |
| 4 | ISS Vanguard | Awaken Realms | awakenrealms.com |
| 6 | Nanolith | TBD | BGG files |
| 7 | Dark Souls | Steamforged | steamforged.com |
| 8 | RONE: Invasion | TBD | BGG files |
| 9 | Frostpunk | Glass Cannon | glasscannonunplugged.com |
| 10 | Rise of Sovereigns | TBD | BGG files |
| 11 | Street Fighter V | Jasco | jascogames.com |
| 12a | BSG | FFG (OOP) | BGG files |
| 12b | BSG Exodus | FFG (OOP) | BGG files |
| 15 | Paleo | Hans im Gluck | BGG files |
| 16 | Leviathan Wilds | Moon Crab | BGG files |
| 17 | Marvel United | CMON | cmon.com |
| 19 | MOTU Fields | CMON | cmon.com |

- [ ] **Step 2: Update manifest with PDF URLs and local paths**

For each game, update `manifest.json`:
- If PDF URL found: set `pdfUrl`, `pdfStatus: "to_download"`
- If local PDF exists: set `pdfLocal: "data/rulebook/{slug}_rulebook.pdf"`, `pdfStatus: "local"`
- If not found: set `pdfStatus: "not_found"`

The 4 games with existing local PDFs:
```json
{ "slug": "mage-knight", "pdfLocal": "data/rulebook/mage-knight_rulebook.pdf", "pdfStatus": "local" }
{ "slug": "dominion", "pdfLocal": "data/rulebook/dominion_rulebook.pdf", "pdfStatus": "local" }
{ "slug": "marvel-champions", "pdfLocal": "data/rulebook/marvel-champions_rulebook.pdf", "pdfStatus": "local" }
{ "slug": "barrage", "pdfLocal": "data/rulebook/barrage_rulebook.pdf", "pdfStatus": "local" }
```

- [ ] **Step 3: Commit manifest with PDF URLs**

```bash
git add data/rulebook/manifest.json
git commit -m "feat(rulebook): add PDF URLs for 16 games to manifest"
```

---

## Task 3: Write import-games.sh — Setup & Auth

**Files:**
- Create: `data/rulebook/import-games.sh`

- [ ] **Step 1: Write script header with config and helpers**

```bash
#!/usr/bin/env bash
set -euo pipefail

# ─── Config ───
API_BASE="http://localhost:8080/api/v1"
MANIFEST="$(dirname "$0")/manifest.json"
COOKIES="$(dirname "$0")/.cookies.txt"
RULEBOOK_DIR="$(dirname "$0")"

# Colors
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'

log()  { echo -e "${GREEN}[INFO]${NC} $*"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
err()  { echo -e "${RED}[ERROR]${NC} $*"; }

# Requires: jq, curl
for cmd in jq curl; do
  command -v "$cmd" &>/dev/null || { err "$cmd is required but not installed"; exit 1; }
done

# ─── Manifest helpers ───
read_manifest() { cat "$MANIFEST"; }

update_game_field() {
  local game_id="$1" field="$2" value="$3"
  local tmp="${MANIFEST}.tmp"
  jq --argjson id "$game_id" --arg field "$field" --arg val "$value" \
    '(.games[] | select(.id == $id))[$field] = $val' "$MANIFEST" > "$tmp" \
    && mv "$tmp" "$MANIFEST"
}

update_game_field_raw() {
  local game_id="$1" field="$2" value="$3"
  local tmp="${MANIFEST}.tmp"
  jq --argjson id "$game_id" --arg field "$field" --argjson val "$value" \
    '(.games[] | select(.id == $id))[$field] = $val' "$MANIFEST" > "$tmp" \
    && mv "$tmp" "$MANIFEST"
}
```

- [ ] **Step 2: Write health check function**

```bash
# ─── Health Check ───
check_health() {
  log "Checking API health..."
  local status
  status=$(curl -s -o /dev/null -w "%{http_code}" "${API_BASE}/health")
  if [[ "$status" != "200" ]]; then
    err "API health check failed (HTTP $status). Is the API running?"
    err "Start with: cd apps/api/src/Api && dotnet run"
    exit 1
  fi
  log "API is healthy"
}
```

- [ ] **Step 3: Write login function**

```bash
# ─── Authentication ───
login() {
  if [[ -z "${ADMIN_EMAIL:-}" || -z "${ADMIN_PASSWORD:-}" ]]; then
    err "ADMIN_EMAIL and ADMIN_PASSWORD env vars must be set"
    err "Usage: ADMIN_EMAIL=admin@example.com ADMIN_PASSWORD=secret ./import-games.sh"
    exit 1
  fi

  log "Logging in as $ADMIN_EMAIL..."
  local response
  response=$(curl -s -c "$COOKIES" -X POST "${API_BASE}/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"${ADMIN_EMAIL}\",\"password\":\"${ADMIN_PASSWORD}\"}")

  if echo "$response" | jq -e '.user.id' &>/dev/null; then
    local user_id
    user_id=$(echo "$response" | jq -r '.user.id')
    log "Logged in as $(echo "$response" | jq -r '.user.displayName') (role: $(echo "$response" | jq -r '.user.role'))"
    echo "$user_id"
  else
    err "Login failed: $response"
    exit 1
  fi
}
```

- [ ] **Step 4: Verify script syntax**

```bash
bash -n data/rulebook/import-games.sh
```

Expected: No output (no syntax errors).

- [ ] **Step 5: Commit setup script**

```bash
git add data/rulebook/import-games.sh
git commit -m "feat(rulebook): add import-games.sh setup, health check, login"
```

---

## Task 4: Write import-games.sh — BGG Import Logic

**Files:**
- Modify: `data/rulebook/import-games.sh`

- [ ] **Step 1: Write BGG batch import function**

Append to `import-games.sh`:

```bash
# ─── BGG Import ───
import_games_from_bgg() {
  log "=== Phase: BGG Import ==="

  # Collect BGG IDs for games not yet imported (no sharedGameId)
  local bgg_ids
  bgg_ids=$(jq -r '[.games[] | select(.bggFound == true and .sharedGameId == null) | .bggId] | @json' "$MANIFEST")

  if [[ "$bgg_ids" == "[]" ]]; then
    log "No BGG games to import (all already imported or no BGG IDs)"
    return
  fi

  log "Importing $(echo "$bgg_ids" | jq 'length') games from BGG..."

  local response
  response=$(curl -s -b "$COOKIES" -X POST "${API_BASE}/admin/bgg-queue/batch" \
    -H "Content-Type: application/json" \
    -d "{\"bggIds\": $bgg_ids}")

  log "BGG batch enqueued: $response"

  # Poll queue until all are processed
  poll_bgg_queue
}

poll_bgg_queue() {
  log "Polling BGG import queue..."
  local max_wait=300  # 5 minutes
  local elapsed=0

  while [[ $elapsed -lt $max_wait ]]; do
    local status
    status=$(curl -s -b "$COOKIES" "${API_BASE}/admin/bgg-queue/status")
    local queued processing
    queued=$(echo "$status" | jq '.totalQueued')
    processing=$(echo "$status" | jq '.totalProcessing')

    if [[ "$queued" == "0" && "$processing" == "0" ]]; then
      log "BGG import queue empty — all processed"
      return
    fi

    log "  Queue: $queued queued, $processing processing... (${elapsed}s)"
    sleep 10
    elapsed=$((elapsed + 10))
  done

  warn "BGG queue poll timeout after ${max_wait}s"
}
```

- [ ] **Step 2: Write manual game creation fallback**

```bash
# ─── Manual SharedGame Creation (non-BGG) ───
import_games_manual() {
  log "=== Phase: Manual Game Import ==="

  local games
  games=$(jq -c '.games[] | select(.bggFound == false and .sharedGameId == null)' "$MANIFEST")

  if [[ -z "$games" ]]; then
    log "No manual games to import"
    return
  fi

  echo "$games" | while IFS= read -r game; do
    local name id
    name=$(echo "$game" | jq -r '.name')
    id=$(echo "$game" | jq -r '.id')

    log "Creating SharedGame manually: $name"

    local meta
    meta=$(echo "$game" | jq '.metadata')

    local body
    body=$(jq -n \
      --arg title "$name" \
      --argjson year "$(echo "$meta" | jq '.yearPublished // 2020')" \
      --arg desc "Imported from manifest" \
      --argjson minP "$(echo "$meta" | jq '.minPlayers // 1')" \
      --argjson maxP "$(echo "$meta" | jq '.maxPlayers // 4')" \
      --argjson time "$(echo "$meta" | jq '.playingTimeMinutes // 60')" \
      --argjson age "$(echo "$meta" | jq '.minAge // 14')" \
      '{
        title: $title,
        yearPublished: $year,
        description: $desc,
        minPlayers: $minP,
        maxPlayers: $maxP,
        playingTimeMinutes: $time,
        minAge: $age,
        imageUrl: "",
        thumbnailUrl: ""
      }')

    local response
    response=$(curl -s -b "$COOKIES" -X POST "${API_BASE}/admin/shared-games" \
      -H "Content-Type: application/json" \
      -d "$body")

    if [[ -n "$response" && "$response" != "null" ]]; then
      local game_id
      game_id=$(echo "$response" | jq -r '. // empty' | tr -d '"')
      if [[ -n "$game_id" ]]; then
        update_game_field "$id" "sharedGameId" "$game_id"
        log "  Created: $name → $game_id"

        # Quick-publish
        curl -s -b "$COOKIES" -X POST "${API_BASE}/admin/shared-games/${game_id}/quick-publish" >/dev/null
        log "  Published: $name"
      fi
    else
      warn "  Failed to create: $name — $response"
    fi
  done
}
```

- [ ] **Step 3: Write function to resolve sharedGameIds after BGG import**

```bash
# ─── Resolve SharedGame IDs ───
resolve_shared_game_ids() {
  log "=== Phase: Resolve SharedGame IDs ==="

  local games
  games=$(jq -c '.games[] | select(.bggFound == true and .sharedGameId == null)' "$MANIFEST")

  if [[ -z "$games" ]]; then
    log "All BGG games already have sharedGameIds"
    return
  fi

  echo "$games" | while IFS= read -r game; do
    local name id bgg_id
    name=$(echo "$game" | jq -r '.name')
    id=$(echo "$game" | jq -r '.id')
    bgg_id=$(echo "$game" | jq -r '.bggId')

    # Search shared games by name to find the imported one
    local encoded_name
    encoded_name=$(python3 -c "import urllib.parse; print(urllib.parse.quote('$name'))" 2>/dev/null || echo "$name")

    local response
    response=$(curl -s -b "$COOKIES" "${API_BASE}/shared-games?search=${encoded_name}&pageSize=5")

    local game_id
    game_id=$(echo "$response" | jq -r ".items[0].id // empty")

    if [[ -n "$game_id" ]]; then
      update_game_field "$id" "sharedGameId" "$game_id"
      log "  Resolved: $name → $game_id"
    else
      warn "  Could not resolve sharedGameId for: $name (bggId: $bgg_id)"
    fi
  done
}
```

- [ ] **Step 4: Verify script syntax**

```bash
bash -n data/rulebook/import-games.sh
```

- [ ] **Step 5: Commit BGG import logic**

```bash
git add data/rulebook/import-games.sh
git commit -m "feat(rulebook): add BGG import + manual fallback to import script"
```

---

## Task 5: Write import-games.sh — PDF Download & Upload

**Files:**
- Modify: `data/rulebook/import-games.sh`

- [ ] **Step 1: Write PDF download function**

Append to `import-games.sh`:

```bash
# ─── PDF Download ───
download_pdfs() {
  log "=== Phase: PDF Download ==="

  local games
  games=$(jq -c '.games[] | select(.pdfStatus == "to_download")' "$MANIFEST")

  if [[ -z "$games" ]]; then
    log "No PDFs to download"
    return
  fi

  echo "$games" | while IFS= read -r game; do
    local name id slug url
    name=$(echo "$game" | jq -r '.name')
    id=$(echo "$game" | jq -r '.id')
    slug=$(echo "$game" | jq -r '.slug')
    url=$(echo "$game" | jq -r '.pdfUrl')

    local dest="${RULEBOOK_DIR}/${slug}_rulebook.pdf"

    if [[ -f "$dest" ]]; then
      log "  Already exists: $dest — skipping download"
      update_game_field "$id" "pdfStatus" "local"
      update_game_field "$id" "pdfLocal" "$dest"
      continue
    fi

    log "  Downloading: $name → $dest"
    if curl -L -s -f -o "$dest" "$url"; then
      # Verify it's actually a PDF
      if file "$dest" | grep -q "PDF"; then
        update_game_field "$id" "pdfStatus" "local"
        update_game_field "$id" "pdfLocal" "$dest"
        log "  Downloaded: $name ($(du -h "$dest" | cut -f1))"
      else
        rm -f "$dest"
        warn "  Not a valid PDF: $name — removed"
        update_game_field "$id" "pdfStatus" "not_found"
      fi
    else
      warn "  Download failed: $name ($url)"
      update_game_field "$id" "pdfStatus" "not_found"
    fi
  done
}
```

- [ ] **Step 2: Write PDF upload function**

```bash
# ─── PDF Upload ───
upload_pdfs() {
  log "=== Phase: PDF Upload ==="

  local games
  games=$(jq -c '.games[] | select(.pdfStatus == "local" and .documentId == null and .sharedGameId != null)' "$MANIFEST")

  if [[ -z "$games" ]]; then
    log "No PDFs to upload"
    return
  fi

  echo "$games" | while IFS= read -r game; do
    local name id slug lang pdf_path shared_game_id is_expansion
    name=$(echo "$game" | jq -r '.name')
    id=$(echo "$game" | jq -r '.id')
    slug=$(echo "$game" | jq -r '.slug')
    lang=$(echo "$game" | jq -r '.language // "en"')
    pdf_path=$(echo "$game" | jq -r '.pdfLocal')
    shared_game_id=$(echo "$game" | jq -r '.sharedGameId')
    is_expansion=$(echo "$game" | jq -r '.isExpansion // false')

    local version_type="base"
    if [[ "$is_expansion" == "true" ]]; then
      version_type="expansion"
    fi

    if [[ ! -f "$pdf_path" ]]; then
      warn "  PDF file not found: $pdf_path — skipping"
      continue
    fi

    log "  Uploading: $name ($pdf_path)"

    local response
    response=$(curl -s -b "$COOKIES" -X POST "${API_BASE}/ingest/pdf" \
      -F "file=@${pdf_path}" \
      -F "gameId=${shared_game_id}" \
      -F "gameName=${name}" \
      -F "versionType=${version_type}" \
      -F "language=${lang}" \
      -F "versionNumber=1.0")

    local job_id
    job_id=$(echo "$response" | jq -r '.jobId // .documentId // empty')

    if [[ -n "$job_id" ]]; then
      update_game_field "$id" "documentId" "$job_id"
      log "  Uploaded: $name → jobId: $job_id"
    else
      warn "  Upload failed: $name — $response"
    fi
  done
}
```

- [ ] **Step 3: Verify script syntax**

```bash
bash -n data/rulebook/import-games.sh
```

- [ ] **Step 4: Commit PDF download/upload logic**

```bash
git add data/rulebook/import-games.sh
git commit -m "feat(rulebook): add PDF download and upload to import script"
```

---

## Task 6: Write import-games.sh — Indexing Poll, Report & Game Night

**Files:**
- Modify: `data/rulebook/import-games.sh`

- [ ] **Step 1: Write indexing poll function**

Append to `import-games.sh`:

```bash
# ─── Indexing Poll ───
poll_indexing() {
  log "=== Phase: Poll Indexing ==="

  local games
  games=$(jq -c '.games[] | select(.documentId != null and .indexingStatus != "Completed")' "$MANIFEST")

  if [[ -z "$games" ]]; then
    log "No documents to poll for indexing"
    return
  fi

  echo "$games" | while IFS= read -r game; do
    local name id shared_game_id
    name=$(echo "$game" | jq -r '.name')
    id=$(echo "$game" | jq -r '.id')
    shared_game_id=$(echo "$game" | jq -r '.sharedGameId')

    log "  Polling indexing: $name"
    local max_wait=300  # 5 minutes per game
    local elapsed=0

    while [[ $elapsed -lt $max_wait ]]; do
      local status_response status
      status_response=$(curl -s -b "$COOKIES" "${API_BASE}/knowledge-base/${shared_game_id}/status")
      status=$(echo "$status_response" | jq -r '.status // "Unknown"')

      case "$status" in
        "Completed"|"Indexed")
          update_game_field "$id" "indexingStatus" "Completed"
          log "  ✅ $name — Indexing complete"
          break
          ;;
        "Failed")
          local error_msg
          error_msg=$(echo "$status_response" | jq -r '.errorMessage // "Unknown error"')
          update_game_field "$id" "indexingStatus" "Failed"
          warn "  ❌ $name — Indexing failed: $error_msg"
          break
          ;;
        *)
          local progress
          progress=$(echo "$status_response" | jq -r '.progress // 0')
          log "    $name: $status (${progress}%) — ${elapsed}s elapsed"
          sleep 10
          elapsed=$((elapsed + 10))
          ;;
      esac
    done

    if [[ $elapsed -ge $max_wait ]]; then
      update_game_field "$id" "indexingStatus" "Timeout"
      warn "  ⏳ $name — Indexing timeout after ${max_wait}s"
    fi
  done
}
```

- [ ] **Step 2: Write final report function**

```bash
# ─── Final Report ───
print_report() {
  log ""
  log "════════════════════════════════════════════════════"
  log "  IMPORT REPORT"
  log "════════════════════════════════════════════════════"

  printf "%-35s %-12s %-12s %-12s\n" "Game" "SharedGame" "PDF" "Indexing"
  printf "%-35s %-12s %-12s %-12s\n" "---" "---" "---" "---"

  jq -r '.games[] | [.name, (.sharedGameId // "—"), .pdfStatus, (.indexingStatus // "—")] | @tsv' "$MANIFEST" \
    | while IFS=$'\t' read -r name sgid pdf idx; do
      printf "%-35s %-12s %-12s %-12s\n" \
        "$(echo "$name" | cut -c1-34)" \
        "$(echo "$sgid" | cut -c1-11)" \
        "$pdf" \
        "$idx"
    done

  log ""
  local total completed failed
  total=$(jq '.games | length' "$MANIFEST")
  completed=$(jq '[.games[] | select(.indexingStatus == "Completed")] | length' "$MANIFEST")
  failed=$(jq '[.games[] | select(.pdfStatus == "not_found")] | length' "$MANIFEST")
  log "Total: $total | Indexed: $completed | PDF not found: $failed"
  log "════════════════════════════════════════════════════"
}
```

- [ ] **Step 3: Write game night creation function**

```bash
# ─── Create Game Night ───
create_game_night() {
  log "=== Phase: Create Game Night ==="

  # Pick first game with completed indexing
  local game
  game=$(jq -c '[.games[] | select(.indexingStatus == "Completed")] | first // empty' "$MANIFEST")

  if [[ -z "$game" || "$game" == "null" ]]; then
    warn "No games with completed indexing — skipping game night creation"
    return
  fi

  local name shared_game_id
  name=$(echo "$game" | jq -r '.name')
  shared_game_id=$(echo "$game" | jq -r '.sharedGameId')

  local scheduled_at
  scheduled_at=$(date -u -d "today 18:00" +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null \
    || date -u +"%Y-%m-%dT18:00:00Z")

  log "Creating game night with: $name"

  local body
  body=$(jq -n \
    --arg title "Game Night - $name" \
    --arg scheduled "$scheduled_at" \
    --arg desc "Auto-created from bulk rulebook import" \
    --arg loc "Home" \
    --arg gid "$shared_game_id" \
    '{
      title: $title,
      scheduledAt: $scheduled,
      description: $desc,
      location: $loc,
      maxPlayers: 6,
      gameIds: [$gid]
    }')

  local response
  response=$(curl -s -b "$COOKIES" -X POST "${API_BASE}/game-nights" \
    -H "Content-Type: application/json" \
    -d "$body")

  local night_id
  night_id=$(echo "$response" | tr -d '"')

  if [[ -n "$night_id" && "$night_id" != "null" ]]; then
    log "Game night created: $night_id"

    # Publish
    curl -s -b "$COOKIES" -X POST "${API_BASE}/game-nights/${night_id}/publish" >/dev/null
    log "Game night published: Game Night - $name"
  else
    warn "Failed to create game night: $response"
  fi
}
```

- [ ] **Step 4: Write main execution flow**

```bash
# ─── Main ───
main() {
  log "╔══════════════════════════════════════╗"
  log "║  MeepleAI Bulk Rulebook Import       ║"
  log "╚══════════════════════════════════════╝"

  if [[ ! -f "$MANIFEST" ]]; then
    err "Manifest not found: $MANIFEST"
    exit 1
  fi

  check_health
  local user_id
  user_id=$(login)

  import_games_from_bgg   # includes poll_bgg_queue internally
  resolve_shared_game_ids
  import_games_manual

  download_pdfs
  upload_pdfs
  poll_indexing

  print_report
  create_game_night

  # Cleanup
  rm -f "$COOKIES"
  log "Done!"
}

main "$@"
```

- [ ] **Step 5: Make script executable and verify syntax**

```bash
chmod +x data/rulebook/import-games.sh
bash -n data/rulebook/import-games.sh
```

Expected: No output (no syntax errors).

- [ ] **Step 6: Commit complete script**

```bash
git add data/rulebook/import-games.sh
git commit -m "feat(rulebook): complete import-games.sh with indexing, report, game night"
```

---

## Task 7: Validate & Dry Run

**Files:**
- None modified (validation only)

- [ ] **Step 1: Validate manifest.json is valid JSON**

```bash
jq '.' data/rulebook/manifest.json > /dev/null && echo "Valid JSON" || echo "Invalid JSON"
jq '.games | length' data/rulebook/manifest.json
```

Expected: `Valid JSON` and `20` (total game entries).

- [ ] **Step 2: Validate all local PDFs exist**

```bash
jq -r '.games[] | select(.pdfStatus == "local") | .pdfLocal' data/rulebook/manifest.json \
  | while read -r path; do
    if [[ -f "$path" ]]; then echo "✅ $path"; else echo "❌ MISSING: $path"; fi
  done
```

Expected: 4 lines, all with checkmark.

- [ ] **Step 3: Validate script with shellcheck (if available)**

```bash
shellcheck data/rulebook/import-games.sh 2>/dev/null || echo "shellcheck not installed — manual review OK"
```

- [ ] **Step 4: Summary commit**

```bash
git add data/rulebook/
git commit -m "feat(rulebook): bulk rulebook import manifest + automation script"
```

---

## Execution Order Summary

```
Task 1 → Task 2 → Task 3 → Task 4 → Task 5 → Task 6 → Task 7
(research)  (PDFs)   (script)  (script)  (script)  (script)  (validate)

Parallelizable: Task 1 + Task 2 (research can run in parallel)
Sequential: Task 3 → 4 → 5 → 6 (script builds incrementally)
```

## Post-Plan: Running the Import

When the API environment is ready:

```bash
cd data/rulebook
ADMIN_EMAIL="admin@meepleai.com" ADMIN_PASSWORD="yourpassword" ./import-games.sh
```
