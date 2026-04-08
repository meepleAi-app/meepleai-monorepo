# Bulk Rulebook Import — Design Spec

**Date**: 2026-04-08
**Status**: Draft
**Branch**: `feature/game-night-experience-v2`

## Objective

Import 19 board games into MeepleAI: create SharedGame entries, upload rulebook PDFs, run embedding/indexing, and create a game night session with an indexed game. The workflow splits into two phases: research (offline) and execution (API-dependent).

## Game List

| # | Game | BGG Search Term | Local PDF |
|---|------|----------------|-----------|
| 1 | Townsfolk Tussle | Townsfolk Tussle | no |
| 2 | Blueprints | Blueprints board game | no |
| 3 | Massive Darkness | Massive Darkness | no |
| 4 | ISS Vanguard | ISS Vanguard | no |
| 5 | Mage Knight | Mage Knight Board Game | `mage-knight_rulebook.pdf` |
| 6 | Nanolith | Nanolith | no |
| 7 | Dark Souls: The Board Game | Dark Souls Board Game | no |
| 8 | RONE: Invasion | RONE Invasion | no |
| 9 | Frostpunk: The Board Game | Frostpunk Board Game | no |
| 10 | Rise of Sovereigns | Rise of Sovereigns | no |
| 11 | Street Fighter V: The Miniatures Game | Street Fighter V Board Game | no |
| 12a | Battlestar Galactica | Battlestar Galactica board game | no |
| 12b | BSG: Exodus Expansion | Battlestar Galactica Exodus | no |
| 13 | Dominion | Dominion | `dominion_rulebook.pdf` |
| 14 | Marvel Champions: The Card Game | Marvel Champions | `marvel-champions_rulebook.pdf` |
| 15 | Paleo | Paleo board game | no |
| 16 | Leviathan Wilds | Leviathan Wilds | no |
| 17 | Marvel United | Marvel United | no |
| 18 | Barrage | Barrage | `barrage_rulebook.pdf` |
| 19 | Masters of the Universe: Fields of Eternia | Masters of the Universe Fields of Eternia | no |

**Total**: 20 entries (19 games + 1 expansion counted separately)

## Phase 1: Research (Offline)

### 1.1 BGG ID Lookup

For each game, search BoardGameGeek to find the exact BggId. Disambiguate by publisher/year when needed (e.g., "Blueprints" vs "Blueprint" — different games).

**Output per game**:
- `bggId: number | null`
- `bggFound: boolean`
- Basic metadata as fallback if BGG import fails

### 1.2 PDF URL Search

For each game without a local PDF, search for the rulebook:

**Search priority**:
1. Publisher's official website (downloads section)
2. BGG files section (community uploads)
3. Known rulebook repositories

**Language priority**: English first, Italian as fallback.

**Validation**: Verify URL returns a PDF (Content-Type check or `.pdf` extension).

### 1.3 Manifest Output

File: `data/rulebook/manifest.json`

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
      "pdfUrl": "https://example.com/rulebook.pdf",
      "pdfLocal": null,
      "pdfStatus": "to_download",
      "isExpansion": false,
      "baseGameId": null,
      "metadata": {
        "yearPublished": 2021,
        "minPlayers": 1,
        "maxPlayers": 4,
        "playingTimeMinutes": 90,
        "designers": ["Designer Name"],
        "publishers": ["Publisher Name"],
        "categories": ["Adventure"],
        "mechanics": ["Dice Rolling"]
      }
    }
  ]
}
```

**PDF status values**:
- `local` — Already in `data/rulebook/` with matching filename
- `to_download` — URL found, ready for download
- `not_found` — No PDF found online, requires manual upload

## Phase 2: Execution Script

### 2.1 Prerequisites

- API running (`dotnet run` from `apps/api/src/Api/`)
- PostgreSQL + Redis running (Docker or local)
- Embedding service running (for PDF indexing)
- SuperAdmin credentials available

### 2.2 Script: `data/rulebook/import-games.sh`

Bash script that reads `manifest.json` and executes all API operations.

#### Step 1: Health Check & Authentication

```bash
# Verify API is up
GET http://localhost:8080/health

# Login as SuperAdmin (credentials from env vars or script args)
# ADMIN_EMAIL and ADMIN_PASSWORD must be set before running
POST http://localhost:8080/api/v1/auth/login
Body: { "email": "$ADMIN_EMAIL", "password": "$ADMIN_PASSWORD" }
# Store JWT token for subsequent requests
```

#### Step 2: Per-Game Processing (sequential)

For each game in manifest:

**a) Create SharedGame**
- If `bggFound=true`: `POST /api/v1/admin/shared-games/import-bgg` with BggId
- If `bggFound=false`: `POST /api/v1/admin/shared-games` with metadata from manifest
- Then: `POST /api/v1/admin/shared-games/{id}/quick-publish`
- Save `sharedGameId` back to manifest

**b) Download PDF** (if `pdfStatus=to_download`)
- `curl -L -o data/rulebook/{slug}_rulebook.pdf {pdfUrl}`
- Update manifest: `pdfLocal` path, `pdfStatus=local`
- Skip if file already exists (idempotent)

**c) Upload PDF** (if `pdfLocal` exists)
- `POST /api/v1/ingest/pdf` multipart form:
  - `file`: the PDF
  - `GameName`: game name
  - `VersionType`: "base" (or "expansion" for BSG Exodus)
  - `Language`: from manifest
  - `VersionNumber`: "1.0"
- Save `documentId` back to manifest

**d) Poll Indexing Status**
- `GET /api/v1/knowledge-base/{gameId}/status`
- Poll every 10 seconds, timeout after 5 minutes per game
- Log progress: `[GameName] Extracting → Chunking → Embedding → Indexing → Completed`
- On failure: log error, mark in manifest, continue to next game

#### Step 3: Final Report

Print summary table:

```
| Game                | SharedGameId | PDF     | Indexing   |
|---------------------|-------------|---------|------------|
| Mage Knight         | abc-123     | uploaded| Completed  |
| Barrage             | def-456     | uploaded| Completed  |
| Townsfolk Tussle    | ghi-789     | failed  | -          |
```

#### Step 4: Create Game Night

- Pick first game with `indexing=Completed`
- `POST /api/v1/game-nights` with:
  - `Title`: "Game Night - {GameName}"
  - `ScheduledAt`: today 20:00 UTC+2
  - `GameIds`: [sharedGameId]
- `POST /api/v1/game-nights/{id}/publish`

### 2.3 Idempotency

The script updates `manifest.json` after each successful operation. If interrupted:
- Games with `sharedGameId` set: skip import step
- Games with `pdfStatus=local`: skip download step
- Games with `documentId` set: skip upload step
- Games with `indexingStatus=Completed`: skip polling

This allows safe re-runs without duplicating data.

### 2.4 Error Handling

| Error | Action |
|-------|--------|
| BGG import fails | Fall back to manual SharedGame creation |
| PDF download fails (404/timeout) | Mark `pdfStatus=not_found`, continue |
| PDF upload fails | Log error, mark in manifest, continue |
| Indexing timeout (>5min) | Mark `indexingStatus=timeout`, continue |
| API health check fails | Exit with error, do not proceed |
| Auth fails | Exit with error, prompt for credentials |

## File Layout

```
data/rulebook/
  manifest.json              # Research output + execution state
  import-games.sh            # Automation script
  mage-knight_rulebook.pdf   # Existing
  dominion_rulebook.pdf      # Existing
  marvel-champions_rulebook.pdf  # Existing
  barrage_rulebook.pdf       # Existing
  townsfolk-tussle_rulebook.pdf  # Downloaded by script
  ...                        # Other downloaded PDFs
```

## Naming Convention

PDF files follow the existing pattern in `data/rulebook/`:
- Lowercase, hyphen-separated: `{game-slug}_rulebook.pdf`
- Expansions: `{base-game}-{expansion}_rulebook.pdf`
- Example: `battlestar-galactica-exodus_rulebook.pdf`

## Out of Scope

- Adding games to user library (can be done later via UI)
- Configuring AI agents per game
- Italian translations of game metadata
- Multiple rulebook versions per game
- Processing queue priority management

## Success Criteria

1. All 19 games exist as published SharedGames in the catalog
2. At least 4 PDFs uploaded and indexed (the local ones)
3. Best-effort PDF download for the remaining 15
4. One game night created with a KB-indexed game
5. Manifest reflects final state of all operations
