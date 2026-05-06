# BGG Top Games Seeding & Tracking Export

**Date**: 2026-03-20
**Status**: Draft
**Scope**: EF Core migration seed + Admin tracking Excel endpoint

## Overview

Seed the database with 143 top BGG games (from `data/top_100_bgg_games.json` + `data/top_100_bgg_games_part2.json`) via an EF Core migration, then provide an admin endpoint to export a tracking Excel showing progress per game.

## Part 1: EF Core Migration — Seed Top BGG Games

### Approach

A raw SQL migration that:
1. Relaxes check constraints to allow skeleton games (zero sentinel values)
2. Inserts 143 games with `ON CONFLICT (bgg_id) DO NOTHING` for idempotency
3. Each game is a skeleton: `game_data_status = 0` (Skeleton), `status = 0` (Draft)

### Check Constraint Changes

The existing constraints block skeleton games with zero-filled fields. We relax them using `= 0 OR original_condition` to preserve data quality for enriched games while allowing the skeleton sentinel (0):

**DB constraints (SQL)**:
- `chk_shared_games_players`: `(min_players = 0 AND max_players = 0) OR (min_players > 0 AND max_players >= min_players)`
- `chk_shared_games_playing_time`: `playing_time_minutes = 0 OR playing_time_minutes > 0` (simplified: `playing_time_minutes >= 0`)
- `chk_shared_games_year_published`: `year_published = 0 OR (year_published > 1900 AND year_published <= 2100)`

**EF Core configuration** (`SharedGameEntityConfiguration.cs`) — must match exactly:
```csharp
// Replace existing check constraints:
entity.HasCheckConstraint("chk_shared_games_players",
    "(min_players = 0 AND max_players = 0) OR (min_players > 0 AND max_players >= min_players)");
entity.HasCheckConstraint("chk_shared_games_playing_time",
    "playing_time_minutes >= 0");
entity.HasCheckConstraint("chk_shared_games_year_published",
    "year_published = 0 OR (year_published > 1900 AND year_published <= 2100)");
```

This way:
- Skeleton games (all zeros) pass validation ✅
- Enriched games must still satisfy strict bounds (year > 1900, min_players > 0) ✅
- Values like year=500 or min_players=-1 are rejected ✅

### SQL Pattern

```sql
-- Relax constraints for skeleton support (0 sentinel)
ALTER TABLE shared_games DROP CONSTRAINT IF EXISTS chk_shared_games_players;
ALTER TABLE shared_games ADD CONSTRAINT chk_shared_games_players
    CHECK ((min_players = 0 AND max_players = 0) OR (min_players > 0 AND max_players >= min_players));

ALTER TABLE shared_games DROP CONSTRAINT IF EXISTS chk_shared_games_playing_time;
ALTER TABLE shared_games ADD CONSTRAINT chk_shared_games_playing_time
    CHECK (playing_time_minutes >= 0);

ALTER TABLE shared_games DROP CONSTRAINT IF EXISTS chk_shared_games_year_published;
ALTER TABLE shared_games ADD CONSTRAINT chk_shared_games_year_published
    CHECK (year_published = 0 OR (year_published > 1900 AND year_published <= 2100));

-- Seed games (idempotent via ON CONFLICT)
INSERT INTO shared_games (
    id, bgg_id, title, year_published, description,
    min_players, max_players, playing_time_minutes, min_age,
    complexity_rating, average_rating, image_url, thumbnail_url,
    status, game_data_status, has_uploaded_pdf, is_deleted,
    is_rag_public, created_by, created_at, modified_at
)
VALUES
    (gen_random_uuid(), 178900, 'Codenames', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
    -- ... all 143 games ...
ON CONFLICT (bgg_id) WHERE bgg_id IS NOT NULL DO NOTHING;
```

### Key Decisions

- **`created_by`**: System admin UUID `00000000-0000-0000-0000-000000000001` — well-known sentinel, not a real user FK
- **Deterministic IDs**: Use `gen_random_uuid()` (PostgreSQL) — `ON CONFLICT` on `bgg_id` handles idempotency
- **Down migration**: Delete seeded games by `bgg_id IN (...)` and restore original strict check constraints
- **Idempotency caveat**: `ON CONFLICT DO NOTHING` skips games whose `bgg_id` already exists in DB, including soft-deleted games. This is by design — a soft-deleted game should not be silently un-deleted by re-running the migration.

### BGG Year=0 Handling

BGG returns `yearPublished = 0` for games with unknown publication years. The current `EnrichFromBgg()` calls `ValidateYear()` which rejects year ≤ 1900. During enrichment, if BGG returns year=0, the enrichment handler will catch the exception and transition the game to `Failed` status. The admin can then manually set the year or re-enqueue after the issue is resolved.

This is acceptable because:
- Only ~2-3 of the top 143 games may have year=0 (all are well-known published games)
- The `Failed → EnrichmentQueued` re-enqueue path already exists
- A future improvement could add a `yearPublished ?? game.YearPublished` fallback in `EnrichFromBgg`

### Post-Deploy Workflow

After migration runs on staging/prod:
1. Admin calls `POST /api/v1/admin/bgg-queue/batch` with the BggIds to enqueue enrichment
2. Background service enriches at 1 req/sec (~2.5 min for 143 games)
3. Admin monitors via tracking Excel export endpoint
4. Admin uploads PDFs manually per game

## Part 2: Admin Tracking Excel Export Endpoint

### Endpoint

`GET /api/v1/admin/shared-games/tracking-export`

- **Auth**: Admin/SuperAdmin only
- **Response**: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- **Filename**: `SharedGames_Tracking_{yyyy-MM-dd}.xlsx`

### CQRS Implementation

**Query**: `ExportSharedGamesTrackingQuery` → `ExportSharedGamesTrackingQueryHandler`

The handler returns `byte[]`. The endpoint converts it to a file download:

```csharp
// Endpoint handler pattern:
var bytes = await mediator.Send(new ExportSharedGamesTrackingQuery());
return Results.File(bytes,
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    $"SharedGames_Tracking_{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
```

The handler:
1. Queries all SharedGames (non-deleted) ordered by title
2. Uses `has_uploaded_pdf` flag on `shared_games` for PDF status (no join needed)
3. Left joins `shared_game_documents` → `pdf_documents` → `vector_documents` via `pdf_document_id` to determine RAG readiness (this is the authoritative path — `vector_documents.shared_game_id` may not always be populated per Issue #4921)
4. Generates `.xlsx` using ClosedXML (already a project dependency)

### RAG Ready Join Path

The authoritative join for RAG readiness:
```sql
shared_games sg
LEFT JOIN shared_game_documents sgd ON sgd.shared_game_id = sg.id
LEFT JOIN pdf_documents pd ON pd.id = sgd.pdf_document_id
LEFT JOIN vector_documents vd ON vd.pdf_document_id = pd.id
-- RAG Ready = COUNT(vd.id) > 0
```

This traverses the full document chain and avoids relying on `vector_documents.shared_game_id` which may be null for older documents.

### Excel Columns

| Column | Source | Description |
|--------|--------|-------------|
| Title | `shared_games.title` | Game name |
| BGG ID | `shared_games.bgg_id` | BoardGameGeek identifier |
| Data Status | `shared_games.game_data_status` | Skeleton/EnrichmentQueued/Enriching/Enriched/PdfDownloading/Complete/Failed |
| Game Status | `shared_games.status` | Draft/PendingApproval/Published/Archived |
| Has PDF | `shared_games.has_uploaded_pdf` | Boolean — PDF uploaded |
| RAG Ready | vector_documents count > 0 (via join path above) | Boolean — vectors indexed |
| Created At | `shared_games.created_at` | Timestamp |
| Year | `shared_games.year_published` | 0 = not enriched yet |
| Players | `min_players - max_players` | "0" = not enriched yet |
| Complexity | `shared_games.complexity_rating` | 1.0-5.0 or empty |

### Conditional Formatting

- **Data Status = Skeleton**: Red background
- **Data Status = Enriched**: Yellow background
- **Data Status = Complete**: Green background
- **Data Status = Failed**: Orange background
- **Has PDF = true**: Green check
- **RAG Ready = true**: Green check

### Endpoint Registration

Add to `SharedGameCatalogAdminEndpoints.cs` under the existing admin group.

## File Changes Summary

| File | Change |
|------|--------|
| `Infrastructure/Migrations/{timestamp}_SeedTop100BggGames.cs` | New migration: relax constraints + seed 143 games |
| `Infrastructure/EntityConfigurations/SharedGameCatalog/SharedGameEntityConfiguration.cs` | Update `HasCheckConstraint` expressions to match relaxed constraints |
| `Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs` | Auto-updated by EF Core |
| `Infrastructure/Migrations/{timestamp}_SeedTop100BggGames.Designer.cs` | Auto-generated |
| `Application/Queries/ExportSharedGamesTrackingQuery.cs` | New: query + handler + Excel generation (returns `byte[]`) |
| `Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs` | Add tracking-export endpoint using `Results.File(...)` |

## Testing

### Migration
- Run `dotnet ef database update` on dev
- Verify 139 rows in `shared_games` with `game_data_status = 0`
- Run migration again — idempotent (0 new rows)
- Verify check constraints allow zeros but reject invalid values (e.g., year=500)

### Tracking Export
- Unit test: handler returns byte array with correct column count
- Integration test: endpoint returns 200 with xlsx content-type and Content-Disposition header
- Manual: download and verify Excel opens with correct data and formatting

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Check constraint relaxation affects data quality | `= 0 OR strict_condition` pattern preserves strict bounds for non-skeleton games |
| Duplicate games if run after manual imports | `ON CONFLICT (bgg_id) DO NOTHING` — safe |
| BGG returns year=0 for some games | Enrichment fails gracefully → `Failed` status → admin re-enqueues or fixes manually |
| Soft-deleted games not restored on re-run | By design — `ON CONFLICT DO NOTHING` respects existing rows |
| EF Core snapshot mismatch | Config expressions specified exactly in this spec — must match migration SQL |
| Migration too large for review | SQL is repetitive but simple INSERT VALUES |
| ClosedXML memory on large datasets | 143 games is trivial; no concern |
