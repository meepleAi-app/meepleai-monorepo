# BGG Search Integration on /admin/shared-games/new

**Date**: 2026-03-12
**Status**: Draft
**Bounded Context**: SharedGameCatalog

## Problem

The admin page `/admin/shared-games/new` requires manual data entry for every field. BGG search exists in the import wizard (`/admin/shared-games/import`, Step 3) but is tightly coupled to the PDF import flow via `useGameImportWizardStore`. Admins who want to create a game from BGG data without uploading a PDF must type everything by hand.

## Solution

Add a BGG search bar at the top of the `/new` page. When the admin selects a BGG game, all form fields auto-fill. The admin can edit any field before saving. The search is optional — skipping it preserves the current manual-entry flow.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| UX pattern | Search bar above form, form always visible | Admin sees both search and form at once |
| BGG data scope | All available metadata (ratings, categories, mechanics, designers, publishers) | Maximizes value of BGG integration |
| Auto-fill behavior | Populate all fields, admin edits before submit | Hybrid: speed of import + control of manual |
| Duplicate handling | Warning with choice: go to existing or create anyway | Supports editions/variants while preventing accidental duplicates |
| Tag editing | Editable chips with autocomplete from existing DB values | Admin can remove BGG tags and add custom ones |
| Throttle UX | Spinner + message after 3s; fallback to manual if BGG down | Form remains functional regardless of BGG status |

## Architecture

### Component Extraction

Extract `BggSearchPanel` from `Step3BggMatch.tsx`:

```
components/admin/shared-games/BggSearchPanel.tsx
```

**Props**:
- `onSelect: (data: BggFullGameData) => void` — callback when admin selects a game
- `initialQuery?: string` — pre-fill the search input
- `showManualIdInput?: boolean` — show/hide manual BGG ID section (default: true)

**New type `BggFullGameData`** (extends current `BggGameData` with metadata):
- All fields from `BggGameData` (id, name, yearPublished, players, time, description, images)
- `categories: string[]`
- `mechanics: string[]`
- `designers: string[]`
- `publishers: string[]`
- `complexityRating?: number`
- `averageRating?: number`

**Backend model pipeline** — no parsing changes needed:
- `BggApiService.ParseGameDetails()` already extracts categories, mechanics, designers, and publishers via `ExtractLinks`.
- `BggGameDetailsDto` (in `Contracts.cs`) already has `Categories`, `Mechanics`, `Designers`, `Publishers` fields.
- The separate `BggXmlParser.cs` / `BggGameDetails` model (used by `BggApiClient`) is a different code path — not in scope.

**Frontend type**: Sync `BggGameDetailsDto` in `apps/web/src/types/bgg.ts` to match the backend DTO. Add missing fields: `designers`, `publishers`, `complexityRating`, `averageRating`.

**Internal behavior**:
- Uses `useSearchBggGames` hook (existing)
- On selection, fetches full details via `api.bgg.getGameDetails(bggId)` to get metadata
- Debounce 300ms (existing pattern)
- Match scoring via `calculateStringSimilarity` (existing)
- No dependency on `useGameImportWizardStore`

**Step3BggMatch refactor**: becomes a thin wrapper around `BggSearchPanel` that bridges to the wizard store.

### Data Flow

```
1. Admin types query           → BggSearchPanel
2. Debounce 300ms              → useSearchBggGames hook
3. GET /admin/shared-games/bgg/search?q=...
4. BggApiService               → rate limit (2 req/sec) + cache (7d) → BGG XML API v2
5. Results + match scoring     → result list UI
6. Admin clicks result         → onSelect(bggGameData)
7. NewGameClient               → setValue() on all form fields
8. GET /admin/shared-games/bgg/check-duplicate/{bggId}  (parallel)
9. If duplicate                → warning with "Go to existing" / "Create anyway"
10. Admin edits + submits      → CreateSharedGameCommand via IMediator
```

### Backend Changes

**`CreateSharedGameCommand`** — field status:

| Field | Type | Validation | Status |
|-------|------|------------|--------|
| `BggId` | `int?` | > 0 when present | Already exists |
| `ComplexityRating` | `decimal?` | 0–5 range | Already exists |
| `AverageRating` | `decimal?` | 0–10 range | Already exists |
| `Categories` | `List<string>` | Max 50 items | **New** |
| `Mechanics` | `List<string>` | Max 50 items | **New** |
| `Designers` | `List<string>` | Max 20 items | **New** |
| `Publishers` | `List<string>` | Max 20 items | **New** |

**`CreateSharedGameCommandHandler`** — extend to:
- Create/associate `GameCategory`, `GameMechanic`, `GameDesigner`, `GamePublisher` entities
- Look up existing entities by name before creating new ones (avoid duplicates)

**`CreateSharedGameCommandValidator`** — add rules for new fields.

No new endpoints required. All BGG search endpoints already exist.

### Frontend Changes

**`NewGameClient.tsx`**:
- Add `BggSearchPanel` above the form card
- Extend `NewGameSchema` (Zod) with: `bggId`, `complexityRating`, `averageRating`, `categories`, `mechanics`, `designers`, `publishers`
- `onBggSelect` handler: calls `setValue()` for each field, triggers duplicate check
- Add `TagInput` components (already exists at `components/admin/shared-games/TagInput.tsx`) for categories, mechanics, designers, publishers
- Add complexity/average rating fields
- Add "Linked to BGG #X" badge when BGG game selected

**`sharedGamesClient.ts`**:
- Update `create()` call to pass new fields

**Zod schema** (`shared-games.schemas.ts`):
- Extend `CreateSharedGameSchema` with new optional fields

## UI Design

### Layout

Search bar (blue accent, BGG section) sits above the form card (orange accent, game entity). The form is always visible. Fields auto-filled from BGG get a subtle warm background tint.

### Tag Colors

| Tag type | Color | Entity mapping |
|----------|-------|----------------|
| Categories | Purple (hsl 262) | Player entity color |
| Mechanics | Rose (hsl 350) | Event entity color |
| Designers | Green (hsl 142) | — |
| Publishers | Amber (hsl 38) | Agent entity color |

### States

- **Empty**: Search bar + empty form (current behavior)
- **Searching**: Spinner in search input, results loading
- **Results**: Scrollable list with thumbnails, match scores, BGG links
- **Selected**: Result highlighted with orange glow ring, form auto-filled
- **Duplicate**: Amber warning with two action buttons
- **Throttled**: Blue notice "BGG is responding slowly..." after 3s wait
- **BGG unavailable**: Alert "BGG unavailable, fill manually" — form stays functional

### Mockup

See `.superpowers/brainstorm/385469-1773342457/new-game-meeple.html` for the full MeepleCard-styled mockup.

## Error Handling

| Scenario | Behavior |
|----------|----------|
| BGG search timeout (>5s) | Show "BGG is responding slowly..." message |
| BGG API down | Alert: "BGG unavailable. Fill the form manually." Form remains functional |
| BGG rate limited (429/202) | Backend returns cached results or empty; frontend retries silently |
| Invalid manual BGG ID | "Game with BGG ID X not found" inline error |
| Duplicate BggId in catalog | Amber warning: "Go to existing game" or "Create anyway" |
| Network error | Generic "Search failed" alert with retry button |

## Testing

### Frontend Tests

All tests mock BGG API calls. No real BGG requests.

**`BggSearchPanel.test.tsx`** (new):
- Renders with and without `initialQuery`
- Debounces search input (no API call on every keystroke)
- Displays results with match score badges
- Calls `onSelect` with correct data on result click
- Shows empty state when no results
- Shows search error with retry
- Manual BGG ID: fetch, preview, confirm

**`NewGameClient.test.tsx`** (update existing):
- Auto-fills all form fields from BGG selection
- Edits auto-filled fields before submit
- Submits with new fields (bggId, ratings, categories, etc.)
- Duplicate warning: displays correctly, both actions work
- Manual flow without BGG (regression — current behavior preserved)
- Throttle notice appears after 3s of loading
- Tag inputs: add and remove for each tag type

**`Step3BggMatch.test.tsx`** (update existing):
- Verify it delegates to `BggSearchPanel` (no duplicated logic)

### Backend Tests

All tests mock `IBggApiClient`. No real BGG requests.

**`CreateSharedGameCommandHandlerTests`** (update):
- Creates game with BggId + ratings + categories/mechanics/designers/publishers
- Creates game without BGG data (regression)
- Associates existing categories/mechanics by name (no duplicates)
- Creates new categories/mechanics when not found in DB

**`CreateSharedGameCommandValidatorTests`** (update):
- Accepts new optional fields
- Rejects BggId <= 0
- Rejects ratings outside valid range
- Rejects lists exceeding max items

## Constraints

- **BGG API rate limit**: ~30 req/min. Backend enforces 2 req/sec via token bucket + 7-day HybridCache. Frontend adds 300ms debounce.
- **BGG API token**: Required since Jan 2026 (`BGG_API_TOKEN` env var). Missing token logs warning at startup.
- **BGG 202 responses**: BGG returns HTTP 202 when throttling. Backend treats this as rate limiting and retries.

## Out of Scope

- Background BGG rating sync (periodic refresh)
- BGG image download/caching (uses BGG URLs directly)
- Bulk create from BGG search results
- BGG search in the public-facing catalog
