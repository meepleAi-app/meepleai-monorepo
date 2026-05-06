# BGG Top Games Seeding & Tracking Export — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Seed 143 top BGG board games into the database via EF Core migration and provide an admin Excel tracking export endpoint.

**Architecture:** Raw SQL migration relaxes check constraints then inserts skeleton games. A CQRS query handler generates an Excel file via ClosedXML with game progress data. Both follow existing project patterns exactly.

**Tech Stack:** .NET 9, EF Core, PostgreSQL, MediatR, ClosedXML, FluentValidation

**Spec:** `docs/superpowers/specs/2026-03-20-bgg-top-games-seeding-design.md`

---

## File Structure

| File | Responsibility |
|------|---------------|
| `apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/SharedGameEntityConfiguration.cs` | Update check constraint expressions (3 constraints) |
| `apps/api/src/Api/Infrastructure/Migrations/{timestamp}_SeedTop143BggGames.cs` | Migration: relax constraints + INSERT 143 skeleton games |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/ExportSharedGamesTracking/ExportSharedGamesTrackingQuery.cs` | Query record |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/ExportSharedGamesTracking/ExportSharedGamesTrackingQueryHandler.cs` | Handler: DB query + ClosedXML generation |
| `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs` | Register tracking-export endpoint |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/ExportSharedGamesTrackingQueryHandlerTests.cs` | Unit tests for handler |

---

### Task 1: Update EF Core Check Constraints in Configuration

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/SharedGameEntityConfiguration.cs:143-157`

- [ ] **Step 1: Update the 3 check constraint expressions**

In `SharedGameEntityConfiguration.cs`, replace lines 143-157:

```csharp
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_shared_games_year_published",
                "year_published = 0 OR (year_published > 1900 AND year_published <= 2100)");
            t.HasCheckConstraint("chk_shared_games_players",
                "(min_players = 0 AND max_players = 0) OR (min_players > 0 AND max_players >= min_players)");
            t.HasCheckConstraint("chk_shared_games_playing_time",
                "playing_time_minutes >= 0");
            t.HasCheckConstraint("chk_shared_games_min_age",
                "min_age >= 0");
            t.HasCheckConstraint("chk_shared_games_complexity",
                "complexity_rating IS NULL OR (complexity_rating >= 1.0 AND complexity_rating <= 5.0)");
            t.HasCheckConstraint("chk_shared_games_rating",
                "average_rating IS NULL OR (average_rating >= 1.0 AND average_rating <= 10.0)");
        });
```

- [ ] **Step 2: Verify build succeeds**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/SharedGameEntityConfiguration.cs
git commit -m "chore(shared-games): relax check constraints for skeleton game support"
```

---

### Task 2: Create EF Core Migration — Seed 144 BGG Games

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/{timestamp}_SeedTop143BggGames.cs`
- Auto-generated: `apps/api/src/Api/Infrastructure/Migrations/{timestamp}_SeedTop143BggGames.Designer.cs`
- Auto-updated: `apps/api/src/Api/Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs`

- [ ] **Step 1: Generate the empty migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add SeedTop143BggGames`

This will detect the check constraint changes from Task 1 and create a migration scaffold. The Designer.cs and Snapshot will be auto-generated.

- [ ] **Step 2: Replace the migration Up/Down with raw SQL**

Replace the generated `Up` method content with the complete SQL below. The migration must:
1. Drop and re-create the 3 relaxed check constraints
2. INSERT 143 skeleton games with `ON CONFLICT DO NOTHING`

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedTop143BggGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Relax check constraints to support skeleton games (zero sentinel values)
            migrationBuilder.Sql("""
                ALTER TABLE shared_games DROP CONSTRAINT IF EXISTS chk_shared_games_players;
                ALTER TABLE shared_games ADD CONSTRAINT chk_shared_games_players
                    CHECK ((min_players = 0 AND max_players = 0) OR (min_players > 0 AND max_players >= min_players));

                ALTER TABLE shared_games DROP CONSTRAINT IF EXISTS chk_shared_games_playing_time;
                ALTER TABLE shared_games ADD CONSTRAINT chk_shared_games_playing_time
                    CHECK (playing_time_minutes >= 0);

                ALTER TABLE shared_games DROP CONSTRAINT IF EXISTS chk_shared_games_year_published;
                ALTER TABLE shared_games ADD CONSTRAINT chk_shared_games_year_published
                    CHECK (year_published = 0 OR (year_published > 1900 AND year_published <= 2100));
                """);

            // Step 2: Seed 144 top BGG games as skeleton entries
            migrationBuilder.Sql("""
                INSERT INTO shared_games (
                    id, bgg_id, title, year_published, description,
                    min_players, max_players, playing_time_minutes, min_age,
                    complexity_rating, average_rating, image_url, thumbnail_url,
                    status, game_data_status, has_uploaded_pdf, is_deleted,
                    is_rag_public, created_by, created_at, modified_at
                ) VALUES
                (gen_random_uuid(), 178900, 'Codenames', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 266192, 'Wingspan', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 167791, 'Terraforming Mars', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 68448, '7 Wonders', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 9209, 'Ticket to Ride', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 148228, 'Splendor', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 14996, 'Ticket to Ride: Europe', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 36218, 'Dominion', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 169786, 'Scythe', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 163412, 'Patchwork', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 129622, 'Love Letter', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 70323, 'King of Tokyo', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 39856, 'Dixit', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 174430, 'Gloomhaven', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 40692, 'Small World', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 199792, 'Everdell', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 1927, 'Munchkin', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 65244, 'Forbidden Island', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 237182, 'Root', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 133473, 'Sushi Go!', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 162886, 'Spirit Island', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 54043, 'Jaipur', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 244521, 'The Quacks of Quedlinburg', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 284083, 'The Crew: Quest for Planet Nine', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 131357, 'Coup', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 172225, 'Exploding Kittens', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 31260, 'Agricola', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 204583, 'Kingdomino', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 98778, 'Hanabi', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 161936, 'Pandemic Legacy: Season 1', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 295947, 'Cascadia', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 291457, 'Gloomhaven: Jaws of the Lion', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2651, 'Power Grid', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 10547, 'Betrayal at House on the Hill', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 342942, 'Ark Nova', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 3076, 'Puerto Rico', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 312484, 'Lost Ruins of Arnak', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 84876, 'The Castles of Burgundy', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 205637, 'Arkham Horror: The Card Game', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 181304, 'Mysterium', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 66690, 'Dominion: Prosperity', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 209685, 'Century: Spice Road', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 12692, 'Gloom', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 154597, 'Hive Pocket', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 170042, 'Raiders of the North Sea', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 221107, 'Pandemic Legacy: Season 2', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 154203, 'Imperial Settlers', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 116, 'Guillotine', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 414317, 'Harmonies', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 92828, 'Dixit: Odyssey', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 158600, 'Hanamikoji', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 255984, 'Sleeping Gods', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2452, 'Jenga', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 93, 'El Grande', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 191189, 'Aeon''s End', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 262712, 'Res Arcana', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 198994, 'Hero Realms', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 169426, 'Roll Player', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2243, 'Yahtzee', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2223, 'UNO', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 822, 'Carcassonne', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 181, 'Risk', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 171, 'Chess', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2083, 'Checkers', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 30549, 'Pandemic', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 1406, 'Monopoly', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 320, 'Scrabble', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2394, 'Dominoes', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 188, 'Go', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2397, 'Backgammon', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 1294, 'Clue', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 74, 'Apples to Apples', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 163, 'Balderdash', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 1293, 'Boggle', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2381, 'Scattergories', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 27225, 'Bananagrams', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2653, 'Survive: Escape from Atlantis!', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 63268, 'Spot it!', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2719, 'Connect Four', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 4143, 'Guess Who?', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 5432, 'Chutes and Ladders', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 2407, 'Sorry', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 224517, 'Brass: Birmingham', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 316554, 'Dune: Imperium', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 233078, 'Twilight Imperium: Fourth Edition', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 397598, 'Dune: Imperium - Uprising', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 115746, 'War of the Ring: Second Edition', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 187645, 'Star Wars: Rebellion', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 220308, 'Gaia Project', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 12333, 'Twilight Struggle', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 182028, 'Through the Ages: A New Story of Civilization', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 193738, 'Great Western Trail', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 418059, 'SETI: Search for Extraterrestrial Intelligence', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 295770, 'Frosthaven', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 246900, 'Eclipse: Second Dawn for the Galaxy', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 338960, 'Slay the Spire: The Board Game', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 28720, 'Brass: Lancashire', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 173346, '7 Wonders Duel', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 167355, 'Nemesis', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 177736, 'A Feast for Odin', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                -- Part 2
                (gen_random_uuid(), 421006, 'The Lord of the Rings: The Card Game', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 266507, 'Clank! Legacy: Acquisitions Incorporated', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 124361, 'Concordia', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 341169, 'Great Western Trail: New Zealand', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 373106, 'Skytear: Arena of Legends', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 120677, 'Terra Mystica', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 192135, 'Too Many Bones', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 164928, 'Orleans', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 96848, 'Mage Knight Board Game', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 251247, 'Barrage', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 321608, 'Hegemony: Lead Your Class to Victory', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 183394, 'Viticulture Essential Edition', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 521, 'Crokinole', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 366013, 'Heat: Pedal to the Metal', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 284378, 'Kanban EV', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 285774, 'Marvel Champions: The Card Game', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 247763, 'Underwater Cities', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 175914, 'Food Chain Magnate', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 256960, 'Pax Pamir: Second Edition', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 253344, 'Cthulhu: Death May Die', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 383179, 'Age of Innovation', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 201808, 'Clank!: A Deck-Building Adventure', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 371942, 'The White Castle', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 266810, 'Paladins of the West Kingdom', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 35677, 'Le Havre', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 125153, 'The Gallerist', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 124742, 'Android: Netrunner', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 164153, 'Star Wars: Imperial Assault', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 380607, 'Great Western Trail: Argentina', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 200680, 'Agricola (Revised Edition)', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 276025, 'Maracaibo', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 209010, 'Mechs vs. Minions', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 55690, 'Kingdom Death: Monster', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 322289, 'Darwin''s Journey', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 332772, 'Revive', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 28143, 'Race for the Galaxy', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 338111, 'Voidfall', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 366161, 'Wingspan: Asia', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 230802, 'Azul', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 157354, 'Five Tribes', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 277659, 'Final Girl', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 159675, 'Fields of Arle', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW()),
                (gen_random_uuid(), 72125, 'Eclipse: New Dawn for the Galaxy', 0, '', 0, 0, 0, 0, NULL, NULL, '', '', 0, 0, false, false, false, '00000000-0000-0000-0000-000000000001', NOW(), NOW())
                ON CONFLICT (bgg_id) WHERE bgg_id IS NOT NULL DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seeded games
            migrationBuilder.Sql("""
                DELETE FROM shared_games
                WHERE bgg_id IN (
                    178900,266192,167791,68448,9209,148228,14996,36218,169786,163412,
                    129622,70323,39856,174430,40692,199792,1927,65244,237182,133473,
                    162886,54043,244521,284083,131357,172225,31260,204583,98778,161936,
                    295947,291457,2651,10547,342942,3076,312484,84876,205637,181304,
                    66690,209685,12692,154597,170042,221107,154203,116,414317,92828,
                    158600,255984,2452,93,191189,262712,198994,169426,2243,2223,
                    822,181,171,2083,30549,1406,320,2394,188,2397,
                    1294,74,163,1293,2381,27225,2653,63268,2719,4143,
                    5432,2407,224517,316554,233078,397598,115746,187645,220308,12333,
                    182028,193738,418059,295770,246900,338960,28720,173346,167355,177736,
                    421006,266507,124361,341169,373106,120677,192135,164928,96848,251247,
                    321608,183394,521,366013,284378,285774,247763,175914,256960,253344,
                    383179,201808,371942,266810,35677,125153,124742,164153,380607,200680,
                    276025,209010,55690,322289,332772,28143,338111,366161,230802,157354,
                    277659,159675,72125
                )
                AND created_by = '00000000-0000-0000-0000-000000000001'
                AND game_data_status = 0;
                """);

            // Restore original strict check constraints
            migrationBuilder.Sql("""
                ALTER TABLE shared_games DROP CONSTRAINT IF EXISTS chk_shared_games_players;
                ALTER TABLE shared_games ADD CONSTRAINT chk_shared_games_players
                    CHECK (min_players > 0 AND max_players >= min_players);

                ALTER TABLE shared_games DROP CONSTRAINT IF EXISTS chk_shared_games_playing_time;
                ALTER TABLE shared_games ADD CONSTRAINT chk_shared_games_playing_time
                    CHECK (playing_time_minutes > 0);

                ALTER TABLE shared_games DROP CONSTRAINT IF EXISTS chk_shared_games_year_published;
                ALTER TABLE shared_games ADD CONSTRAINT chk_shared_games_year_published
                    CHECK (year_published > 1900 AND year_published <= 2100);
                """);
        }
    }
}
```

**IMPORTANT**: The `dotnet ef migrations add` will auto-generate the Designer.cs. The generated `Up`/`Down` methods will contain the constraint changes detected from Task 1. Replace the auto-generated content with the full SQL above (which includes both the constraint changes AND the seed data).

- [ ] **Step 3: Verify build succeeds**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(shared-games): add migration to seed top 143 BGG games as skeletons"
```

---

### Task 3: Create ExportSharedGamesTracking Query and Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/ExportSharedGamesTracking/ExportSharedGamesTrackingQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/ExportSharedGamesTracking/ExportSharedGamesTrackingQueryHandler.cs`

- [ ] **Step 1: Create the query record**

Create `ExportSharedGamesTrackingQuery.cs`:

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.ExportSharedGamesTracking;

/// <summary>
/// Query to export all shared games as an Excel tracking spreadsheet.
/// Returns raw bytes of the .xlsx file.
/// </summary>
internal sealed record ExportSharedGamesTrackingQuery : IQuery<byte[]>;
```

- [ ] **Step 2: Create the query handler**

Create `ExportSharedGamesTrackingQueryHandler.cs`:

```csharp
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Interfaces;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.ExportSharedGamesTracking;

/// <summary>
/// Handler for ExportSharedGamesTrackingQuery.
/// Queries all shared games with PDF/RAG status and generates an Excel tracking spreadsheet.
/// </summary>
internal sealed class ExportSharedGamesTrackingQueryHandler
    : IRequestHandler<ExportSharedGamesTrackingQuery, byte[]>
{
    private readonly MeepleAiDbContext _context;

    public ExportSharedGamesTrackingQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<byte[]> Handle(
        ExportSharedGamesTrackingQuery query, CancellationToken cancellationToken)
    {
        // Step 1: Get IDs of games that have completed RAG indexing
        // Uses authoritative join path: shared_game_documents → vector_documents via pdf_document_id
        var ragReadyGameIds = await (
            from sgd in _context.Set<SharedGameDocumentEntity>()
            join vd in _context.VectorDocuments on sgd.PdfDocumentId equals vd.PdfDocumentId
            where vd.IndexingStatus == "completed"
            select sgd.SharedGameId
        ).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);

        var ragReadySet = ragReadyGameIds.ToHashSet();

        // Step 2: Query all non-deleted shared games
        var games = await _context.SharedGames
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Title)
            .Select(g => new
            {
                g.Id,
                g.Title,
                g.BggId,
                g.GameDataStatus,
                g.Status,
                g.HasUploadedPdf,
                g.YearPublished,
                g.MinPlayers,
                g.MaxPlayers,
                g.ComplexityRating,
                g.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Generate Excel
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Tracking");

        // Header row
        var headers = new[]
        {
            "Title", "BGG ID", "Data Status", "Game Status",
            "Has PDF", "RAG Ready", "Created At",
            "Year", "Players", "Complexity"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Data rows
        var dataStatusNames = new Dictionary<int, string>
        {
            [0] = "Skeleton", [1] = "EnrichmentQueued", [2] = "Enriching",
            [3] = "Enriched", [4] = "PdfDownloading", [5] = "Complete", [6] = "Failed"
        };

        var gameStatusNames = new Dictionary<int, string>
        {
            [0] = "Draft", [1] = "PendingApproval", [2] = "Published", [3] = "Archived"
        };

        for (var row = 0; row < games.Count; row++)
        {
            var g = games[row];
            var r = row + 2; // Excel row (1-based, skip header)

            worksheet.Cell(r, 1).Value = g.Title;
            worksheet.Cell(r, 2).Value = g.BggId ?? 0;
            worksheet.Cell(r, 3).Value = dataStatusNames.GetValueOrDefault(g.GameDataStatus, "Unknown");
            worksheet.Cell(r, 4).Value = gameStatusNames.GetValueOrDefault(g.Status, "Unknown");
            worksheet.Cell(r, 5).Value = g.HasUploadedPdf ? "Yes" : "No";
            var isRagReady = ragReadySet.Contains(g.Id);
            worksheet.Cell(r, 6).Value = isRagReady ? "Yes" : "No";
            worksheet.Cell(r, 7).Value = g.CreatedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            worksheet.Cell(r, 8).Value = g.YearPublished == 0 ? "-" : g.YearPublished.ToString(System.Globalization.CultureInfo.InvariantCulture);
            worksheet.Cell(r, 9).Value = g.MinPlayers == 0 ? "-" : $"{g.MinPlayers}-{g.MaxPlayers}";
            worksheet.Cell(r, 10).Value = g.ComplexityRating?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "-";

            // Conditional formatting: Data Status color
            var statusCell = worksheet.Cell(r, 3);
            statusCell.Style.Fill.BackgroundColor = g.GameDataStatus switch
            {
                0 => XLColor.LightCoral,      // Skeleton — red
                3 => XLColor.LightYellow,      // Enriched — yellow
                5 => XLColor.LightGreen,       // Complete — green
                6 => XLColor.Orange,           // Failed — orange
                _ => XLColor.NoColor
            };

            // Conditional formatting: Has PDF
            if (g.HasUploadedPdf)
                worksheet.Cell(r, 5).Style.Fill.BackgroundColor = XLColor.LightGreen;

            // Conditional formatting: RAG Ready
            if (isRagReady)
                worksheet.Cell(r, 6).Style.Fill.BackgroundColor = XLColor.LightGreen;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Write to byte array
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
```

- [ ] **Step 3: Verify build succeeds**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/ExportSharedGamesTracking/
git commit -m "feat(shared-games): add ExportSharedGamesTracking query handler with ClosedXML"
```

---

### Task 4: Register the Tracking Export Endpoint

**Files:**
- Modify: `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs`

- [ ] **Step 1: Add the endpoint registration and handler method**

In `SharedGameCatalogAdminEndpoints.cs`, inside the `Map` method, add after the existing admin endpoints:

```csharp
        // Export tracking spreadsheet (all shared games with progress status)
        group.MapGet("/admin/shared-games/tracking-export", HandleTrackingExport)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("ExportSharedGamesTracking")
            .WithSummary("Export shared games tracking spreadsheet (Admin/Editor)")
            .WithDescription("Downloads an Excel file with all shared games and their progress status (enrichment, PDF, RAG).")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
```

Add the handler method at the bottom of the class:

```csharp
    private static async Task<IResult> HandleTrackingExport(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var bytes = await mediator.Send(
            new Application.Queries.ExportSharedGamesTracking.ExportSharedGamesTrackingQuery(),
            cancellationToken).ConfigureAwait(false);

        var filename = $"SharedGames_Tracking_{DateTime.UtcNow:yyyy-MM-dd}.xlsx";
        return Results.File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }
```

Add the using at the top if not already present:
```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.ExportSharedGamesTracking;
```

- [ ] **Step 2: Verify build succeeds**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs
git commit -m "feat(shared-games): register GET tracking-export admin endpoint"
```

---

### Task 5: Write Unit Tests for Tracking Export Handler

**Files:**
- Create: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/ExportSharedGamesTrackingQueryHandlerTests.cs`

- [ ] **Step 1: Write the test class**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.ExportSharedGamesTracking;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ExportSharedGamesTrackingQueryHandlerTests : IAsyncDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly ExportSharedGamesTrackingQueryHandler _handler;

    public ExportSharedGamesTrackingQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"TrackingExport_{Guid.NewGuid()}")
            .Options;

        _context = new MeepleAiDbContext(options);
        _handler = new ExportSharedGamesTrackingQueryHandler(_context);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsValidExcelWithHeaderOnly()
    {
        // Act
        var result = await _handler.Handle(
            new ExportSharedGamesTrackingQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);

        using var stream = new MemoryStream(result);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();

        Assert.Equal("Title", worksheet.Cell(1, 1).GetString());
        Assert.Equal("BGG ID", worksheet.Cell(1, 2).GetString());
        Assert.Equal("RAG Ready", worksheet.Cell(1, 6).GetString());
        Assert.Equal(1, worksheet.LastRowUsed()?.RowNumber() ?? 0); // Header only
    }

    [Fact]
    public async Task Handle_WithSkeletonGames_ReturnsCorrectStatusAndFormatting()
    {
        // Arrange
        _context.SharedGames.Add(new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            BggId = 178900,
            Title = "Codenames",
            YearPublished = 0,
            Description = "",
            MinPlayers = 0,
            MaxPlayers = 0,
            PlayingTimeMinutes = 0,
            MinAge = 0,
            ImageUrl = "",
            ThumbnailUrl = "",
            Status = 0, // Draft
            GameDataStatus = 0, // Skeleton
            HasUploadedPdf = false,
            IsDeleted = false,
            CreatedBy = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new ExportSharedGamesTrackingQuery(), CancellationToken.None);

        // Assert
        using var stream = new MemoryStream(result);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.First();

        Assert.Equal("Codenames", ws.Cell(2, 1).GetString());
        Assert.Equal(178900, ws.Cell(2, 2).GetValue<int>());
        Assert.Equal("Skeleton", ws.Cell(2, 3).GetString());
        Assert.Equal("Draft", ws.Cell(2, 4).GetString());
        Assert.Equal("No", ws.Cell(2, 5).GetString());
        Assert.Equal("No", ws.Cell(2, 6).GetString());
        Assert.Equal("-", ws.Cell(2, 8).GetString()); // Year = 0 → "-"
        Assert.Equal("-", ws.Cell(2, 9).GetString()); // Players = 0 → "-"

        // Verify red background for Skeleton status
        Assert.Equal(XLColor.LightCoral, ws.Cell(2, 3).Style.Fill.BackgroundColor);
    }

    [Fact]
    public async Task Handle_ExcludesSoftDeletedGames()
    {
        // Arrange
        _context.SharedGames.Add(new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            BggId = 999999,
            Title = "Deleted Game",
            Description = "",
            ImageUrl = "",
            ThumbnailUrl = "",
            IsDeleted = true,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new ExportSharedGamesTrackingQuery(), CancellationToken.None);

        // Assert
        using var stream = new MemoryStream(result);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.First();

        Assert.Equal(1, ws.LastRowUsed()?.RowNumber() ?? 0); // Header only, no data
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ExportSharedGamesTrackingQueryHandlerTests" --no-restore -v n`
Expected: 3 tests pass

- [ ] **Step 3: Commit**

```bash
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/ExportSharedGamesTrackingQueryHandlerTests.cs
git commit -m "test(shared-games): add unit tests for ExportSharedGamesTracking handler"
```

---

### Task 6: Run Full Build and Test Suite

- [ ] **Step 1: Build the entire solution**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 2: Run SharedGameCatalog tests**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "BoundedContext=SharedGameCatalog" --no-restore -v n`
Expected: All tests pass (existing + new)

- [ ] **Step 3: Final commit with all changes**

Verify all changes are committed:
```bash
git status
```
Expected: clean working tree
