# Issues #2305 + #2306 — RowVersion → xmin migration

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the `bytea NOT NULL` + `IsRowVersion()` pattern with Postgres `xmin xid` + `IsConcurrencyToken()` on 3 entities (`LiveGameSession`, `GameNightPlaylist`, `MechanicDraft`) to eliminate (a) the `clock_timestamp()` trigger collision risk on `live_game_sessions` (Issue #2305 / I-1 from PR #2301) and (b) silently-disabled optimistic concurrency on `game_night_playlists` + `mechanic_drafts` (Issue #2306 / I-2 from PR #2301).

**Architecture:** Adopt the codebase exemplar `MechanicAnalysisEntity.Xmin` pattern: `uint Xmin` property + `.HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()` config. Drop the legacy `row_version bytea` columns (and the `ef_update_row_version()` trigger function for `live_game_sessions`). Per-entity EF migration applied via `dotnet ef migrations add` — 3 migrations total, 1 per entity, so each can be reverted independently.

**Tech Stack:** .NET 9 · EF Core 9 · Npgsql provider · PostgreSQL 16 · xUnit + Testcontainers.

**Branch:** `feature/issue-2305-rowversion-xmin-migration` (parent: `main-dev`).

**Effort estimate:** 1–1.5 days (3 entities × ~0.4gg + integration test for #2306 concurrency proof).

---

## Pre-flight: Branch safety check

Run from the repo root (`D:\Repositories\meepleai-monorepo-main`):

```pwsh
git branch --show-current   # MUST print "main-dev"
git status                  # MUST be clean
git pull --ff-only          # MUST succeed
git checkout -b feature/issue-2305-rowversion-xmin-migration
git config branch.feature/issue-2305-rowversion-xmin-migration.parent main-dev
```

If `git branch --show-current` prints anything other than `main-dev`, STOP and run `git checkout main-dev && git pull` first.

---

## File Structure Overview

### Created files

| Path | Responsibility |
|---|---|
| `apps/api/src/Api/Infrastructure/Migrations/{ts}_LiveSessionRowVersionToXmin.cs` | EF migration: drop `ef_update_row_version()` trigger + function, drop `row_version` column from `live_game_sessions`. Postgres `xmin` is implicit, no DDL needed for the new column. |
| `apps/api/src/Api/Infrastructure/Migrations/{ts}_GameNightPlaylistRowVersionToXmin.cs` | EF migration: drop `row_version` column from `game_night_playlists`. |
| `apps/api/src/Api/Infrastructure/Migrations/{ts}_MechanicDraftRowVersionToXmin.cs` | EF migration: drop `row_version` column from `mechanic_drafts`. |
| `apps/api/tests/Api.Tests/Integration/GameToolbox/GameNightPlaylistRowVersionConcurrencyTests.cs` | Integration test proving `DbUpdateConcurrencyException` is now thrown on concurrent updates (was silently last-write-wins before this PR). |
| `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/MechanicDraftRowVersionConcurrencyTests.cs` | Same for MechanicDraft. |

### Modified files

| Path | Change |
|---|---|
| `apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs` | `byte[] RowVersion` → `uint Xmin`. |
| `apps/api/src/Api/Infrastructure/Entities/GameManagement/GameNightPlaylistEntity.cs` | `byte[] RowVersion` → `uint Xmin`. |
| `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicDraftEntity.cs` | `byte[] RowVersion` → `uint Xmin`. |
| `apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs:117-119` | `.HasColumnName("row_version").IsRowVersion()` → xmin xid pattern. |
| `apps/api/src/Api/Infrastructure/Configurations/GameManagement/GameNightPlaylistEntityConfiguration.cs` | Same. |
| `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicDraftEntityConfiguration.cs` | Same. |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs:67` | `byte[] RowVersion` → `uint Xmin`. Update `Reconstitute(...)` signature. |
| `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/Mappers/LiveGameSessionMapper.cs` | Drop `Array.Empty<byte>()` fallback for RowVersion; round-trip `Xmin` direct. |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/Mappers/LiveGameSessionMapperTests.cs` | Update RowVersion round-trip assertion to Xmin. |
| `apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionRepositoryIntegrationTests.cs:265` | AC-4 (concurrent updates) — confirm it still passes against xmin. |
| `docs/for-claude/architecture/adr/adr-060-live-session-persistence.md` | Update implementation notes: trigger replaced with xmin. |
| `CLAUDE.md:387` (Known Pitfalls ADR-060 row) | Update to reflect xmin pattern (no longer mentions `ef_update_row_version()` trigger). |

### Pattern reference (read before starting)

- `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicAnalysisEntityConfiguration.cs:101-107` — exemplar xmin pattern in config (`Xmin xid ValueGeneratedOnAddOrUpdate IsConcurrencyToken`).
- `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicAnalysisEntity.cs` — exemplar `uint Xmin` property.

---

## Phase summary table

| Phase | Priority | Deliverable | Effort |
|---|---|---|---|
| 1 | P2 | `LiveGameSession` migration to xmin (closes #2305) | ~0.4gg |
| 2 | P2 | `GameNightPlaylist` migration to xmin (closes #2306 half-1) | ~0.4gg |
| 3 | P2 | `MechanicDraft` migration to xmin (closes #2306 half-2) | ~0.4gg |
| 4 | P3 | ADR-060 + CLAUDE.md note + PR | ~0.1gg |

---

# Phase 1 — `LiveGameSession` xmin migration (closes #2305)

### Task 1.1: Property rename in Entity

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs`

- [ ] **Step 1: Find the existing RowVersion property and replace**

Open the entity file and locate (around line 56 — search for `byte[] RowVersion`):

```csharp
    // Concurrency
    public byte[] RowVersion { get; set; } = default!;
```

Replace with:

```csharp
    // Optimistic concurrency via PostgreSQL's xmin system column (Issue #2305).
    // Postgres assigns xmin = transaction-id-of-last-write per row; EF reads back via the
    // xid type-mapped uint property. Server-owned: NO mapper assignment, NO client default,
    // NO trigger maintenance.
    public uint Xmin { get; set; }
```

- [ ] **Step 2: Build (expect errors in dependents)**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: build errors in `LiveGameSession.cs`, `LiveGameSessionMapper.cs`, and possibly tests that reference `RowVersion`. These are fixed in subsequent steps.

### Task 1.2: Property rename in Domain aggregate

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs:67`

- [ ] **Step 1: Replace RowVersion property**

Locate line 67 (`public byte[] RowVersion { get; private set; } = Array.Empty<byte>();`) and replace with:

```csharp
    public uint Xmin { get; private set; }
```

- [ ] **Step 2: Update `Reconstitute(...)` factory signature**

Locate the `Reconstitute(...)` factory (around line 151). Find the parameter:

```csharp
        byte[] rowVersion,
```

Replace with:

```csharp
        uint xmin,
```

And inside the object initializer (around line 213), replace:

```csharp
            RowVersion = rowVersion ?? Array.Empty<byte>(),
```

with:

```csharp
            Xmin = xmin,
```

### Task 1.3: EntityConfiguration update

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs:117-119`

- [ ] **Step 1: Replace the IsRowVersion block**

Locate (around line 117):

```csharp
        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();
```

Replace with:

```csharp
        // Optimistic concurrency via PostgreSQL's xmin system column (Issue #2305).
        // Replaces the legacy bytea row_version + ef_update_row_version() trigger.
        // Server-owned, collision-safe (xmin = unique transaction id per row UPDATE).
        builder.Property(e => e.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
```

### Task 1.4: Update the Mapper

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/Mappers/LiveGameSessionMapper.cs`

- [ ] **Step 1: ToEntity — drop the manual RowVersion line**

Locate the line in `ToEntity` (around line 73):

```csharp
            // RowVersion is ValueGeneratedOnAddOrUpdate backed by a Postgres trigger...
            RowVersion = domain.RowVersion.Length > 0 ? domain.RowVersion : Array.Empty<byte>()
```

Replace the whole block (including the multi-line comment above) with:

```csharp
            // Xmin is Postgres-system-owned (Issue #2305); EF round-trips it via xid mapping.
            // Mapper passes the current domain value back so EF emits WHERE xmin = @original.
            Xmin = domain.Xmin
```

- [ ] **Step 2: ToDomain — pass xmin through Reconstitute**

Locate the `Reconstitute(...)` call inside `ToDomain` and find the parameter:

```csharp
            rowVersion: entity.RowVersion,
```

Replace with:

```csharp
            xmin: entity.Xmin,
```

### Task 1.5: Update the Mapper test

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/Mappers/LiveGameSessionMapperTests.cs`

- [ ] **Step 1: Update the RowVersion test**

The existing test asserts `entity.RowVersion.Should().NotBeNull(...)`. Locate it and replace the test method body. Search for `RoundTrip_PreservesRowVersion` or similar. Replace whichever test asserts on `RowVersion`:

```csharp
    [Fact]
    public void ToEntity_PreservesXmin_ForOptimisticConcurrency()
    {
        var session = LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            gameName: "X");

        var entity = LiveGameSessionMapper.ToEntity(session);

        // Xmin is server-owned (Postgres assigns on INSERT/UPDATE). For a fresh domain
        // aggregate the value is 0; EF will overwrite it after SaveChangesAsync. The
        // contract here is that the mapper round-trips whatever value the domain currently
        // holds — not that the value is non-zero at this point.
        entity.Xmin.Should().Be(session.Xmin);
    }
```

- [ ] **Step 2: Run the Mapper tests**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveGameSessionMapper"`
Expected: 2 tests PASS.

### Task 1.6: Update the Reconstitute test

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/LiveGameSessionReconstituteTests.cs`

- [ ] **Step 1: Update the test for new signature**

Locate the `Reconstitute(...)` call inside the test. Change `rowVersion: new byte[] { 1, 2, 3, 4 }` to `xmin: 42u` and update any assertion on `session.RowVersion` to `session.Xmin.Should().Be(42u)`.

- [ ] **Step 2: Run the test**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveGameSessionReconstitute"`
Expected: PASS.

### Task 1.7: EF migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/{auto}_LiveSessionRowVersionToXmin.cs`

- [ ] **Step 1: Generate the migration**

```pwsh
cd apps/api/src/Api
dotnet ef migrations add LiveSessionRowVersionToXmin
```

Expected: a new migration file appears with `DropColumn(name: "row_version", ...)` + `AddColumn(name: "xmin", ...)`. EF Core does NOT auto-detect the system column — you must manually edit the `Up`/`Down` to handle the trigger and the implicit nature of xmin.

- [ ] **Step 2: Manually edit the migration to drop the trigger + function + column**

Open the generated migration file and replace the entire `Up()` body with:

```csharp
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the trigger and helper function added by LiveSessionRowVersionTrigger (#2097).
            // xmin is a system column managed by Postgres — no trigger needed.
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_live_game_sessions_row_version ON live_game_sessions;
            ");

            // Drop the function ONLY if no other table currently uses it. As of this PR no
            // other table does — verify with the comment query below if reapplied later.
            // SELECT proname FROM pg_proc WHERE proname='ef_update_row_version';
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS ef_update_row_version();
            ");

            // Drop the legacy bytea column. EF's xmin/xid mapping handles concurrency via
            // the Postgres system column directly — no schema change needed for that side.
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "live_game_sessions");
        }
```

And the `Down()` body with:

```csharp
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the legacy bytea column + trigger if rolling back.
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "live_game_sessions",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION ef_update_row_version()
                RETURNS trigger AS $$
                BEGIN
                    NEW.row_version := clock_timestamp()::text::bytea;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_live_game_sessions_row_version ON live_game_sessions;
                CREATE TRIGGER trg_live_game_sessions_row_version
                BEFORE INSERT OR UPDATE ON live_game_sessions
                FOR EACH ROW EXECUTE FUNCTION ef_update_row_version();
            ");
        }
```

- [ ] **Step 3: Build to confirm**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: Build succeeded with 0 errors.

### Task 1.8: Verify integration tests pass

- [ ] **Step 1: Run the LiveSession integration suite**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionRepositoryIntegrationTests" --nologo
```

Expected: all 5 AC tests PASS. AC-4 (concurrent → `DbUpdateConcurrencyException`) specifically proves the xmin pattern works end-to-end. If AC-4 fails, the EF Postgres provider may not be auto-recognizing `xid` as a concurrency token — re-check the EntityConfiguration matches `MechanicAnalysisEntityConfiguration.cs:101-107` exactly.

> **Pitfall:** Postgres `xmin` is read-only from the EF perspective. The mapper / repository must NOT attempt to write it. The `ValueGeneratedOnAddOrUpdate()` declaration handles this — but if you accidentally call `SetValues(snapshot)` with an `Xmin` of `0` it will be ignored, NOT zeroed in the DB. Verify the integration tests pass before assuming the contract holds.

### Task 1.9: Commit Phase 1

```pwsh
git add apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs `
        apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs `
        apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs `
        apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/Mappers/LiveGameSessionMapper.cs `
        apps/api/src/Api/Infrastructure/Migrations/ `
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/
git commit -m "feat(live-session): #2305 migrate live_game_sessions row_version to xmin"
```

---

# Phase 2 — `GameNightPlaylist` xmin migration (closes #2306 half-1)

### Task 2.1: Entity property

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/GameManagement/GameNightPlaylistEntity.cs`

- [ ] **Step 1: Locate the RowVersion property (around line 35)**

```csharp
public byte[] RowVersion { get; set; } = Array.Empty<byte>();
```

Replace with:

```csharp
// Optimistic concurrency via PostgreSQL's xmin system column (Issue #2306).
// Replaces the silently-disabled byte[] RowVersion (no trigger populated it).
public uint Xmin { get; set; }
```

### Task 2.2: EntityConfiguration

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Configurations/GameManagement/GameNightPlaylistEntityConfiguration.cs`

- [ ] **Step 1: Replace the IsRowVersion block**

Locate:

```csharp
        builder.Property(p => p.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();
```

Replace with:

```csharp
        builder.Property(p => p.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
```

### Task 2.3: Check repository and other consumers

- [ ] **Step 1: Grep for any other code referencing GameNightPlaylist.RowVersion**

```pwsh
grep -rn "GameNightPlaylist.*RowVersion\|playlist.RowVersion" apps/api/src/Api apps/api/tests/Api.Tests
```

If matches found: update each usage. Domain aggregate may need the same property rename. Common locations:
- `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Entities/GameNightPlaylist.cs` (if it has a Domain aggregate)
- `apps/api/src/Api/BoundedContexts/GameToolkit/Infrastructure/Persistence/GameNightPlaylistRepository.cs`

Apply the same `byte[] RowVersion` → `uint Xmin` rename in those files.

### Task 2.4: EF migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/{auto}_GameNightPlaylistRowVersionToXmin.cs`

- [ ] **Step 1: Generate the migration**

```pwsh
cd apps/api/src/Api
dotnet ef migrations add GameNightPlaylistRowVersionToXmin
```

- [ ] **Step 2: Manually replace `Up()` body**

```csharp
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the silently-unused bytea column (Issue #2306). xmin system column
            // (managed by Postgres) replaces it via the EntityConfiguration mapping.
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "game_night_playlists");
        }
```

- [ ] **Step 3: Replace `Down()` body**

```csharp
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "game_night_playlists",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }
```

### Task 2.5: Concurrency integration test

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/GameToolbox/GameNightPlaylistRowVersionConcurrencyTests.cs`

- [ ] **Step 1: Find a sibling test that already touches GameNightPlaylist over the real DB**

```pwsh
grep -rln "GameNightPlaylist" apps/api/tests/Api.Tests/Integration | head -5
```

Use that test's fixture pattern (SharedTestcontainersFixture or self-contained Postgres container) as the starting point.

- [ ] **Step 2: Write the failing test**

Skeleton (adjust namespaces / fixture pattern to match what the sibling test uses):

```csharp
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameToolbox;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "GameToolbox")]
[Collection("Integration-GroupC")]
public sealed class GameNightPlaylistRowVersionConcurrencyTests
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _dbName;

    public GameNightPlaylistRowVersionConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _dbName = $"playlist_xmin_{Guid.NewGuid():N}";
    }

    [Fact(DisplayName = "Concurrent updates throw DbUpdateConcurrencyException via xmin")]
    public async Task ConcurrentUpdates_ThrowDbUpdateConcurrencyException()
    {
        var connStr = await _fixture.CreateIsolatedDatabaseAsync(_dbName);
        await using var factory = IntegrationWebApplicationFactory.Create(connStr);
        using (var setup = factory.Services.CreateScope())
        {
            await setup.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync();
        }

        // Arrange — seed a playlist + load it from two independent scopes
        // (replace SeedPlaylistAsync with whatever test helper exists in the BC; if none
        //  exists, insert a minimal entity manually).
        var playlistId = await SeedPlaylistAsync(factory);

        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();

        var dbA = scopeA.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var playlistA = await dbA.GameNightPlaylists.FirstAsync(p => p.Id == playlistId);
        var playlistB = await dbB.GameNightPlaylists.FirstAsync(p => p.Id == playlistId);

        playlistA.Should().NotBeSameAs(playlistB);
        playlistA.Xmin.Should().Be(playlistB.Xmin, "both scopes loaded the same row");

        // Act — both update the same row
        playlistA.Notes = "From A";   // adjust field to whatever the playlist has
        playlistB.Notes = "From B";

        await dbA.SaveChangesAsync();

        // Assert — second save throws optimistic concurrency
        Func<Task> act = async () => await dbB.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await _fixture.DropIsolatedDatabaseAsync(_dbName);
    }

    private static async Task<Guid> SeedPlaylistAsync(WebApplicationFactory<Program> factory)
    {
        // Implement using the entity's minimal-valid shape — copy from a sibling integration
        // test if available, otherwise insert via DbContext.GameNightPlaylists.Add(...) +
        // SaveChangesAsync. Return the new Id.
        throw new NotImplementedException("Fill in based on sibling test pattern");
    }
}
```

> **Don't try to be clever about Notes field**: if `GameNightPlaylist` has a different mutable scalar property suitable for the test (e.g. `Name`), use it. The test is about RowVersion concurrency, not about exercising business logic.

- [ ] **Step 3: Implement `SeedPlaylistAsync`** by looking at a sibling integration test that already creates a `GameNightPlaylistEntity` and replicating that minimal-valid setup.

- [ ] **Step 4: Run the test**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameNightPlaylistRowVersionConcurrency"
```

Expected: PASS — confirms `xmin` now enforces optimistic concurrency.

### Task 2.6: Commit Phase 2

```pwsh
git add apps/api/src/Api/Infrastructure/Entities/GameManagement/GameNightPlaylistEntity.cs `
        apps/api/src/Api/Infrastructure/Configurations/GameManagement/GameNightPlaylistEntityConfiguration.cs `
        apps/api/src/Api/Infrastructure/Migrations/ `
        apps/api/src/Api/BoundedContexts/GameToolkit/ `
        apps/api/tests/Api.Tests/Integration/GameToolbox/
git commit -m "fix(rowversion): #2306 migrate game_night_playlists to xmin (enables real optimistic concurrency)"
```

---

# Phase 3 — `MechanicDraft` xmin migration (closes #2306 half-2)

### Task 3.1: Entity property

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicDraftEntity.cs`

- [ ] **Step 1: Locate `byte[] RowVersion` and replace with `uint Xmin`**

Same change pattern as Phase 2, Task 2.1.

### Task 3.2: EntityConfiguration

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicDraftEntityConfiguration.cs`

- [ ] **Step 1: Replace the IsRowVersion block with the xmin pattern**

Same as Phase 2, Task 2.2.

### Task 3.3: Check repository and other consumers

- [ ] **Step 1: Grep**

```pwsh
grep -rn "MechanicDraft.*RowVersion\|draft.RowVersion" apps/api/src/Api apps/api/tests/Api.Tests
```

Apply same rename in any matches.

### Task 3.4: EF migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/{auto}_MechanicDraftRowVersionToXmin.cs`

- [ ] **Step 1: Generate + edit `Up()` and `Down()` bodies** identical to Phase 2 Task 2.4 but on `mechanic_drafts` table name.

### Task 3.5: Concurrency integration test

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/MechanicDraftRowVersionConcurrencyTests.cs`

- [ ] **Step 1: Copy the Phase 2 Task 2.5 test, adapt entity name + namespace.**
- [ ] **Step 2: Use a sibling integration test under `Integration/SharedGameCatalog/` to find the existing `MechanicDraft` seed pattern.**
- [ ] **Step 3: Run the test — expect PASS.**

### Task 3.6: Commit Phase 3

```pwsh
git add apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicDraftEntity.cs `
        apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicDraftEntityConfiguration.cs `
        apps/api/src/Api/Infrastructure/Migrations/ `
        apps/api/src/Api/BoundedContexts/KnowledgeBase/ `
        apps/api/tests/Api.Tests/Integration/SharedGameCatalog/
git commit -m "fix(rowversion): #2306 migrate mechanic_drafts to xmin (enables real optimistic concurrency)"
```

---

# Phase 4 — Documentation + PR

### Task 4.1: Update ADR-060

**Files:**
- Modify: `docs/for-claude/architecture/adr/adr-060-live-session-persistence.md`

- [ ] **Step 1: Append an "Update" section at the end**

```markdown
## Update 2026-06-14 — Trigger replaced with xmin

Per code-review finding I-1 of PR #2301 and follow-up issue #2305, the `clock_timestamp()`
trigger pattern initially shipped with this ADR was replaced with the codebase-standard
`xmin` system-column mapping. Same column behavior (Postgres-managed concurrency token),
better collision safety (xmin is a unique transaction id), zero trigger maintenance.

The `LiveSessionRowVersionTrigger` migration is reverted by `LiveSessionRowVersionToXmin`.
Implementation matches `MechanicAnalysisEntityConfiguration` exemplar.
```

### Task 4.2: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md` (Known Pitfalls table, ADR-060 row)

- [ ] **Step 1: Find and update the ADR-060 row**

Locate the row mentioning `ef_update_row_version()` trigger and replace its rule text with:

```markdown
| [ADR-060](./docs/for-claude/architecture/adr/adr-060-live-session-persistence.md) | LiveSession is EF-backed. Every Command handler that calls `_sessionRepository.AddAsync`/`UpdateAsync` MUST also call `await _unitOfWork.SaveChangesAsync(ct)`. Domain events dispatch post-SaveChanges only. Optimistic concurrency uses Postgres `xmin` system column (Issue #2305) — no trigger, no client-side RowVersion. |
```

### Task 4.3: Final verification

- [ ] **Step 1: Build the API project**

```pwsh
dotnet build apps/api/src/Api/Api.csproj
```
Expected: 0 errors.

- [ ] **Step 2: Run all affected unit tests**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSession|FullyQualifiedName~LiveGameSession|FullyQualifiedName~GameNightPlaylist|FullyQualifiedName~MechanicDraft&Category!=Integration"
```
Expected: All PASS.

- [ ] **Step 3: Run the 3 affected integration tests**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionRepositoryIntegrationTests|FullyQualifiedName~GameNightPlaylistRowVersionConcurrency|FullyQualifiedName~MechanicDraftRowVersionConcurrency"
```
Expected: All PASS (5 AC live + 1 playlist + 1 draft = 7 tests).

### Task 4.4: Commit Phase 4 + push + PR

```pwsh
git add docs/for-claude/architecture/adr/adr-060-live-session-persistence.md CLAUDE.md
git commit -m "docs: #2305 #2306 ADR-060 + CLAUDE.md note for xmin migration"
git push -u origin feature/issue-2305-rowversion-xmin-migration
```

Then:

```pwsh
gh pr create --base main-dev --title "feat(rowversion): #2305 + #2306 migrate 3 entities to xmin pattern" --body "$(cat <<'EOF'
## Summary

Replaces the `bytea NOT NULL` + `IsRowVersion()` pattern with Postgres `xmin xid` + `IsConcurrencyToken()` on 3 entities to eliminate two real bugs surfaced by PR #2301 code review.

### Closes
- #2305 — live_game_sessions trigger collision risk (I-1)
- #2306 — game_night_playlists + mechanic_drafts silently-disabled optimistic concurrency (I-2)

### Changes per entity (3 commits)
1. **LiveGameSession** — drop `ef_update_row_version()` trigger + function + `row_version` column. Switch to xmin/xid.
2. **GameNightPlaylist** — drop `row_version` column. Switch to xmin/xid. Add concurrency integration test (was silently last-write-wins before).
3. **MechanicDraft** — same as 2.

### Why xmin over trigger
- Collision-safe: xmin = unique transaction id per row UPDATE, no microsecond collision window.
- Zero maintenance: Postgres system column, no PL/pgSQL function.
- Codebase consistency: matches `MechanicAnalysis`, `MechanicGoldenClaim`, `CertificationThresholdsConfig`.

## Test plan
- [x] AC-4 LiveSessionRepositoryIntegrationTests (concurrent → DbUpdateConcurrencyException) green against xmin
- [x] New GameNightPlaylistRowVersionConcurrencyTests green
- [x] New MechanicDraftRowVersionConcurrencyTests green
- [x] All existing LiveSession / GameNightPlaylist / MechanicDraft unit tests green
- [ ] CI green

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Out of scope

- ❌ Other entities with bytea RowVersion (`AppBudgetEntity`, `GameToolkitEntity`, `ToolkitVersionEntity`, `AlertChannelEntity`): all need similar audit, but each is its own follow-up issue. This PR strictly addresses #2305 + #2306.
- ❌ `AlertChannelEntity`: actually already uses `xmin` mapping in its EntityConfiguration despite the `byte[] RowVersion` C# property. It's an existing hybrid that works but is inconsistent with the codebase. Separate cleanup.

---

## Self-review checklist (run before opening PR)

- [ ] All 3 affected entities use `uint Xmin` not `byte[] RowVersion`.
- [ ] All 3 EntityConfigurations use the exact pattern from `MechanicAnalysisEntityConfiguration:101-107`.
- [ ] All 3 migrations drop their `row_version` column AND (for LiveGameSession only) drop the trigger + function.
- [ ] All 3 integration concurrency tests pass — proving xmin actually enforces optimistic concurrency.
- [ ] ADR-060 + CLAUDE.md updated.
- [ ] No `// TODO` or `// FIXME` introduced (memory `sonar-s1135-todo-blocks-build`).
