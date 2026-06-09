# Priority Issues Resolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve 3 priority issues (#2022 search_vector migration, #2035 designers field, #2043 bug #1 public catalog visibility) discovered during 2026-06-08 session.

**Architecture:** Each issue is independent and lands as a focused change with TDD coverage. Issue #2022 is a raw-SQL migration patch (no schema regen). #2035 extends an existing CQRS handler+DTO without touching the read model. #2043 #1 is a single-line filter relaxation on a published query handler, verified with an integration test that asserts catalog cardinality > 0.

**Tech Stack:** ASP.NET 9 Minimal APIs + EF Core (Npgsql) + xUnit + Testcontainers (BE) · Next.js 16 + TypeScript + Zod + Vitest (FE) · PostgreSQL 16 + pgvector.

---

## Pre-flight (run once before Task 1)

- [ ] **Step 0.1: Confirm working branch + clean tree**

Run: `git status`
Expected: `On branch feature/squash-migrations` (parent of these changes — the squash already shipped on this branch), or `main-dev` if the squash was merged in the interim. Working tree clean.

- [ ] **Step 0.2: Confirm test infra reachable**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MeepleAiDbContext" --nologo -v q --no-build`

If build is needed first: `cd apps/api/src/Api && dotnet build -c Debug --nologo -v q`

Expected: at least one test name printed (any pass/fail OK — we only verify the runner spins up).

---

## Task 1: #2022 — Add `search_vector` to `InitialCreate` migration

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Migrations/20260608133755_InitialCreate.cs` (append raw SQL inside `Up()` after the `pgvector_embeddings` `CreateTable` block)
- Create: `apps/api/tests/Api.Tests/Integration/Migrations/SearchVectorColumnIntegrationTests.cs`
- Reference (DO NOT modify): `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs:481` — the production code that requires the column.

**Context:** During the migration squash, EF generated `InitialCreate` from the entity model. The model does NOT declare `search_vector` (it's a GENERATED ALWAYS computed column that the runtime adapter creates with raw DDL via `CREATE TABLE IF NOT EXISTS`). Result on a fresh DB: EF creates `pgvector_embeddings` without the column, the adapter's `CREATE INDEX ... USING gin (search_vector)` then fails with `42703: column "search_vector" does not exist`. The fix is a single `migrationBuilder.Sql(...)` after the create-table so search_vector is always present when migrations are applied.

- [ ] **Step 1.1: Write the failing integration test**

Create `apps/api/tests/Api.Tests/Integration/Migrations/SearchVectorColumnIntegrationTests.cs`:

```csharp
using System.Threading.Tasks;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.Migrations;

[Collection("PostgresDb")]
[Trait("Category", "Integration")]
public sealed class SearchVectorColumnIntegrationTests
{
    private readonly PostgresDbFixture _fixture;

    public SearchVectorColumnIntegrationTests(PostgresDbFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task InitialCreate_AddsSearchVectorColumnToPgvectorEmbeddings()
    {
        await using var conn = _fixture.OpenConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_name = 'pgvector_embeddings'
              AND column_name = 'search_vector';
        ";

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "search_vector column should exist after migration");
        Assert.Equal("tsvector", reader["data_type"]);
    }

    [Fact]
    public async Task InitialCreate_CreatesGinIndexOnSearchVector()
    {
        await using var conn = _fixture.OpenConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 1
            FROM pg_indexes
            WHERE tablename = 'pgvector_embeddings'
              AND indexname = 'idx_pgvector_embeddings_search_vector';
        ";

        var result = await cmd.ExecuteScalarAsync();
        Assert.NotNull(result);
    }
}
```

If `PostgresDbFixture` does not exist with this exact name, search `apps/api/tests/Api.Tests/` for the existing Testcontainers Postgres fixture (likely `PostgresFixture` or `TestcontainersPostgresFixture`) and substitute the constructor argument + the `[Collection("...")]` attribute accordingly. Do NOT invent a fixture.

- [ ] **Step 1.2: Run the test to verify it fails**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SearchVectorColumnIntegrationTests" --nologo -v normal`

Expected: 2 tests, BOTH FAIL with `Assert.True(False)` for the column test, `Assert.NotNull(null)` for the index test — proving the migration as-shipped does not create the column.

- [ ] **Step 1.3: Patch the migration**

Open `apps/api/src/Api/Infrastructure/Migrations/20260608133755_InitialCreate.cs`. Locate the `CreateTable` call that creates `pgvector_embeddings` (search for `name: "pgvector_embeddings"`). Immediately after that `CreateTable` call closes (the `);` line), insert:

```csharp
            // #2022 — search_vector is a Postgres-side GENERATED column the production
            // runtime expects (PgVectorStoreAdapter.cs CREATE INDEX USING gin uses it).
            // EF can't model GENERATED ALWAYS AS STORED, so we add it via raw SQL right
            // after the table is created and immediately back it with the GIN index the
            // adapter would otherwise create itself.
            migrationBuilder.Sql(@"
                ALTER TABLE pgvector_embeddings
                ADD COLUMN search_vector tsvector
                GENERATED ALWAYS AS (to_tsvector('english', text_content)) STORED;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_pgvector_embeddings_search_vector
                ON pgvector_embeddings USING gin (search_vector);
            ");
```

Then locate the `Down()` method (same file) and add the inverse before any other drops:

```csharp
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_pgvector_embeddings_search_vector;");
            migrationBuilder.Sql("ALTER TABLE pgvector_embeddings DROP COLUMN IF EXISTS search_vector;");
```

- [ ] **Step 1.4: Build, then re-run the test to verify it passes**

Run: `cd apps/api/src/Api && dotnet build --nologo -v q && cd ../.. && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SearchVectorColumnIntegrationTests" --nologo -v normal`

Expected: 2/2 PASS.

- [ ] **Step 1.5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/20260608133755_InitialCreate.cs apps/api/tests/Api.Tests/Integration/Migrations/SearchVectorColumnIntegrationTests.cs
git commit -m "fix(db): add search_vector tsvector + GIN index to InitialCreate (#2022)"
```

---

## Task 2: #2035 — Add `Designers` to `GameDetailDto` + populate in handler (BE)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/GameDetailDto.cs` (the response record that lands on `/api/v1/library/games/{id}`)
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetGameDetailQueryHandler.cs` (include + map designers)
- Test: `apps/api/tests/Api.Tests/Unit/UserLibrary/GetGameDetailQueryHandlerDesignersTests.cs` (new — unit test the handler picks designers out of EF)

**Context:** The TS `LibraryGameDetail` interface already references `designers` indirectly via mockup-aligned UI strips (`GameDetailDesktop.tsx:78-81`), but the BE response never emits the field. `GameDesignerEntity` is modeled as `SharedGameEntity.Designers : ICollection<GameDesignerEntity>` already (M:N table `game_designers_shared_games`). The handler must `Include(g => g.Designers)` and project names into the DTO. This is additive — adding the field with default `Array.Empty<string>()` keeps existing FE callers compiling.

- [ ] **Step 2.1: Write the failing unit test**

Create `apps/api/tests/Api.Tests/Unit/UserLibrary/GetGameDetailQueryHandlerDesignersTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Unit.UserLibrary;

[Trait("Category", "Unit")]
public sealed class GetGameDetailQueryHandlerDesignersTests
{
    [Fact]
    public async Task Handle_ReturnsDesigners_WhenSharedGameHasDesigners()
    {
        // Arrange — in-memory EF context with one SharedGame, one GameDesigner, one UserLibraryEntry
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new MeepleAiDbContext(options);

        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var designer = new GameDesignerEntity { Id = Guid.NewGuid(), Name = "Klaus Teuber", CreatedAt = DateTime.UtcNow };
        var sharedGame = new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Catan",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Designers = new List<GameDesignerEntity> { designer },
        };
        var libraryEntry = new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = sharedGameId,
            AddedAt = DateTime.UtcNow,
        };
        db.SharedGames.Add(sharedGame);
        db.UserLibraryEntries.Add(libraryEntry);
        await db.SaveChangesAsync();

        var handler = new GetGameDetailQueryHandler(db);
        var query = new GetGameDetailQuery(userId, sharedGameId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result!.Designers);
        Assert.Equal("Klaus Teuber", result.Designers.Single());
    }

    [Fact]
    public async Task Handle_ReturnsEmptyDesigners_WhenSharedGameHasNoDesigners()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new MeepleAiDbContext(options);

        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Codenames",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Designers = new List<GameDesignerEntity>(),
        });
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = sharedGameId,
            AddedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var handler = new GetGameDetailQueryHandler(db);
        var query = new GetGameDetailQuery(userId, sharedGameId);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Designers);
    }
}
```

If the handler constructor takes more than just `MeepleAiDbContext` (e.g. an `ITimeProvider` or `ILogger<>`), open `GetGameDetailQueryHandler.cs` and pass matching mocks — do not refactor the constructor.

- [ ] **Step 2.2: Run the test to verify it fails**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetGameDetailQueryHandlerDesignersTests" --nologo -v normal`

Expected: 2 tests, both FAIL with `'GameDetailDto' does not contain a definition for 'Designers'` (compile error) before the test even runs.

- [ ] **Step 2.3: Extend the DTO**

Open `apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/GameDetailDto.cs`. The record currently ends at the last positional parameter (around line 55, `string? CustomCoverR2Key = null`). Add a new positional parameter at the end (additive — last position is the safe slot for the optional default):

```csharp
internal record GameDetailDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    // ... all existing parameters unchanged ...
    string? CustomCoverR2Key = null,

    // #2035 — Designer names extracted from the shared game catalog M:N relation.
    // Empty list when the game has no designer rows (e.g. user-added private games).
    IReadOnlyList<string>? Designers = null
);
```

Order matters: keep `Designers` AFTER `CustomCoverR2Key` so the existing positional constructor calls in tests/seeds don't shift. Default `null` so existing call sites that build `new GameDetailDto(...)` without designers still compile; the handler will pass a real list.

- [ ] **Step 2.4: Update the handler to load + map designers**

Open `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetGameDetailQueryHandler.cs`.

Locate the EF query that loads the `SharedGameEntity` (search for `_db.SharedGames` or `db.SharedGames`). Add `.Include(g => g.Designers)` to the chain. Example diff (line numbers approximate — adjust to the real file):

```csharp
// Before
var game = await _db.SharedGames
    .AsNoTracking()
    .FirstOrDefaultAsync(g => g.Id == request.GameId, ct);

// After
var game = await _db.SharedGames
    .AsNoTracking()
    .Include(g => g.Designers)
    .FirstOrDefaultAsync(g => g.Id == request.GameId, ct);
```

Then locate the `new GameDetailDto(...)` construction (search for `new GameDetailDto`). Pass the designer name projection at the end:

```csharp
return new GameDetailDto(
    // ... all existing positional arguments unchanged ...
    CustomCoverR2Key: libraryEntry.CustomCoverR2Key,
    Designers: game.Designers
        .Select(d => d.Name)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .ToList()
);
```

If the call site uses positional (no `: name`) syntax, switch to named arguments for the last few — this is the recommended pattern when adding optional trailing parameters and keeps the diff readable.

- [ ] **Step 2.5: Run the test to verify it passes**

Run: `cd apps/api/src/Api && dotnet build --nologo -v q && cd ../.. && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetGameDetailQueryHandlerDesignersTests" --nologo -v normal`

Expected: 2/2 PASS.

- [ ] **Step 2.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/GameDetailDto.cs apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetGameDetailQueryHandler.cs apps/api/tests/Api.Tests/Unit/UserLibrary/GetGameDetailQueryHandlerDesignersTests.cs
git commit -m "feat(library): expose designers on /library/games/{id} (#2035 BE)"
```

---

## Task 3: #2035 — Wire `designers` through TS schema + FE consumer

**Files:**
- Modify: `apps/web/src/lib/api/schemas/library.schemas.ts:287-333` (`GameDetailDtoSchema`)
- Modify: `apps/web/src/hooks/queries/useLibrary.ts:773-810` (`LibraryGameDetail` interface — exact line range of the interface body)
- Reference (already correct, no change needed): `apps/web/src/components/game-detail/GameDetailDesktop.tsx:78-81` — already reads `game?.designers?.[0]?.name`. It will start populating the breadcrumb the moment the API surfaces the field.
- Test: `apps/web/src/lib/api/schemas/__tests__/library.designers.test.ts`

**Context:** The C# handler now emits `designers: string[]`. We declare it on the Zod schema (FE input validation) and on the `LibraryGameDetail` TS surface (consumer shape). `GameDetailDesktop` already pushes the first designer to `heroMetadata` — once the data arrives, the strip reads "Klaus Teuber · 1995 · 120 min · 3-4 giocatori · Complessità 2.3" without any further FE change.

- [ ] **Step 3.1: Write the failing schema test**

Create `apps/web/src/lib/api/schemas/__tests__/library.designers.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { GameDetailDtoSchema } from '../library.schemas';

describe('GameDetailDtoSchema #2035 designers', () => {
  const validBase = {
    id: '00000000-0000-0000-0000-000000000001',
    userId: '00000000-0000-0000-0000-000000000002',
    gameId: '00000000-0000-0000-0000-000000000003',
    gameTitle: 'Catan',
    gamePublisher: '',
    gameYearPublished: 1995,
    gameDescription: 'Settlers',
    gameIconUrl: null,
    gameImageUrl: null,
    minPlayers: 3,
    maxPlayers: 4,
    playTimeMinutes: 120,
    complexityRating: 2.28,
    averageRating: 7.09,
    addedAt: '2026-06-08T14:37:26.056526Z',
    notes: null,
    isFavorite: false,
    currentState: 'Owned',
    stateChangedAt: '2026-06-08T18:03:48.813Z',
    stateNotes: null,
    isAvailableForPlay: true,
    timesPlayed: 0,
    lastPlayed: null,
    winRate: 'N/A',
    avgDuration: 'N/A',
  };

  it('accepts designers as a string array', () => {
    const parsed = GameDetailDtoSchema.parse({
      ...validBase,
      designers: ['Klaus Teuber'],
    });
    expect(parsed.designers).toEqual(['Klaus Teuber']);
  });

  it('accepts designers omitted (backward compat)', () => {
    const parsed = GameDetailDtoSchema.parse(validBase);
    // designers should be undefined/null — explicitly NOT throw
    expect(parsed.designers).toBeFalsy();
  });

  it('accepts designers null (BE may emit null for empty)', () => {
    const parsed = GameDetailDtoSchema.parse({ ...validBase, designers: null });
    expect(parsed.designers).toBeNull();
  });
});
```

- [ ] **Step 3.2: Run the test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/lib/api/schemas/__tests__/library.designers.test.ts`

Expected: 3 tests, the first FAILS with `Property 'designers' does not exist on type ...` (TS error) or runtime `unrecognized_keys` if the schema uses strict mode. The second/third may pass — that's OK; we'll re-run after the fix.

- [ ] **Step 3.3: Extend the Zod schema**

Open `apps/web/src/lib/api/schemas/library.schemas.ts`. Locate `GameDetailDtoSchema` (around line 287). Inside the `z.object({ ... })` body, after `customCoverR2Key`, add:

```ts
  // Issue #1824 L3: user-custom cover R2 key (null if no custom cover)
  customCoverR2Key: z.string().nullable().optional(),

  // #2035 — Designer names from the shared game catalog. Optional and
  // nullable because legacy BE versions don't surface the field.
  designers: z.array(z.string()).nullable().optional(),
});
```

- [ ] **Step 3.4: Extend the TS `LibraryGameDetail` interface**

Open `apps/web/src/hooks/queries/useLibrary.ts`. Locate the `export interface LibraryGameDetail {` block (around line 773). After `complexityRating: number | null;` (around line 805 — the existing field next to where designers logically belongs), add:

```ts
  complexityRating: number | null;

  // #2035 — Designer names from the shared game catalog. Empty/undefined
  // when the BE has not populated the join yet or the game has none.
  designers?: string[] | null;
```

If the same file has a `mapPrivateGameToLibraryGameDetail` (around line 843), also include `designers: undefined,` in the mapping object so the type-checker is satisfied without behavior change.

- [ ] **Step 3.5: Verify the schema test now passes**

Run: `cd apps/web && pnpm vitest run src/lib/api/schemas/__tests__/library.designers.test.ts`

Expected: 3/3 PASS.

- [ ] **Step 3.6: Verify the FE smoke build typechecks**

Run: `cd apps/web && pnpm typecheck`

Expected: no NEW errors (baseline errors unrelated to this diff may remain — record their count before this task if uncertain).

- [ ] **Step 3.7: Commit**

```bash
git add apps/web/src/lib/api/schemas/library.schemas.ts apps/web/src/hooks/queries/useLibrary.ts apps/web/src/lib/api/schemas/__tests__/library.designers.test.ts
git commit -m "feat(library): consume designers field from /library/games/{id} (#2035 FE)"
```

---

## Task 4: #2043 bug #1 — Public catalog `/hub/games` returns 0

**Files:**
- Investigate: which endpoint backs `/hub/games`. Likely `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/SearchSharedGamesQueryHandler.cs` (or `GetAllSharedGamesQueryHandler.cs` / `GetFilteredSharedGamesQueryHandler.cs`). The visible bug: live response shows `total: 0` for an authenticated superadmin against a DB with 159 `shared_games` rows.
- Modify (after Step 4.1 confirms target): the handler that filters out non-public rows by default.
- Test: `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/PublicCatalogVisibilityIntegrationTests.cs` (new — integration test that asserts the catalog returns rows even when `is_rag_public` is false on every row).

**Context:** The DB has 159 `shared_games` (admin Overview KPI confirms). The public Hub catalog (`/hub/games`) shows 0. The shared_games table has a column `is_rag_public BOOLEAN NOT NULL DEFAULT false` and the SearchSharedGames handler very likely filters on it (it's a RAG search-permission flag, not a catalog-visibility flag). The fix is to remove the filter from the catalog read path while keeping it on RAG-search code paths.

- [ ] **Step 4.1: Identify the handler that backs `/hub/games`**

Run from the repo root:

```bash
grep -r "MapGet.*hub/games\|MapGet.*shared-games\|hub.games.*MapGet" apps/api/src/Api/Routing/ --include="*.cs" -l
```

Open the file that matches and find the route registered at `/api/v1/...` whose path corresponds to the FE call (FE makes the request from `apps/web/src/app/(public)/hub/games/page.tsx` — open this file to read the exact endpoint URL it calls).

Note the **handler class name** and the **filter expression** that mentions `is_rag_public` or `IsRagPublic`. If the filter uses `g.IsRagPublic == true`, that's the line we'll relax. **Write the handler class name + filter line range here before continuing** (it's needed for Step 4.3).

- [ ] **Step 4.2: Write the failing integration test**

Create `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/PublicCatalogVisibilityIntegrationTests.cs`. Replace `SearchSharedGamesQueryHandler` with the real class name confirmed in Step 4.1:

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

[Trait("Category", "Integration")]
public sealed class PublicCatalogVisibilityIntegrationTests
{
    [Fact]
    public async Task PublicCatalog_ReturnsGames_EvenWhenIsRagPublicFalse()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new MeepleAiDbContext(options);

        var creatorId = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            db.SharedGames.Add(new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = $"Game {i}",
                CreatedBy = creatorId,
                CreatedAt = DateTime.UtcNow,
                IsRagPublic = false, // ALL rows are "RAG private" — the bug condition
                IsDeleted = false,
            });
        }
        await db.SaveChangesAsync();

        // Replace with the real query type confirmed in Step 4.1 — e.g.
        //   var handler = new SearchSharedGamesQueryHandler(db);
        //   var query = new SearchSharedGamesQuery(page: 1, pageSize: 20);
        var handler = new SearchSharedGamesQueryHandler(db);
        var query = new SearchSharedGamesQuery(page: 1, pageSize: 20);

        var result = await handler.Handle(query, CancellationToken.None);

        // Whatever shape the result has, it must surface ALL 5 rows. Adapt the
        // assertion to the actual response type — if it's a paged result with
        // `.Items` use `Assert.Equal(5, result.Items.Count)`.
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
    }
}
```

If the query takes a different constructor shape (e.g. requires a search term), pass `searchTerm: null` or `""`. If the result type doesn't expose `.Items` / `.TotalCount`, mirror the property names you find on the actual record.

- [ ] **Step 4.3: Run the test to verify it fails**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PublicCatalogVisibilityIntegrationTests" --nologo -v normal`

Expected: FAIL. `Assert.Equal(5, 0)` or similar — `TotalCount` is 0 because every row was filtered out by the `IsRagPublic` predicate.

- [ ] **Step 4.4: Remove the `IsRagPublic` filter from the catalog query**

Open the handler file identified in Step 4.1. Locate the LINQ chain that filters on `IsRagPublic`. Remove that line (the `.Where(g => g.IsRagPublic)` or equivalent). Keep all OTHER filters intact (`IsDeleted == false`, soft-delete, search term, pagination).

Add a comment on the SAME line position explaining why this is intentional:

```csharp
// #2043 bug #1 — IsRagPublic gates RAG search reachability, NOT catalog visibility.
// The public Hub catalog must show every non-deleted shared game so users can
// browse the full library before deciding to install. RAG access is gated
// separately in the search endpoint, not here.
```

If the SAME handler is also used by the authenticated /api/v1/shared-games endpoint AND another caller (e.g. a "RAG public games only" admin list), check that the OTHER call sites still pass their own filter through query parameters. If not, lift the filter into the query DTO as an OPTIONAL parameter (`bool? requireRagPublic = null`) so the public catalog passes `null` and the RAG-only caller passes `true`. Default behavior of `null` MUST be "no filter".

- [ ] **Step 4.5: Re-run the test to verify it passes**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PublicCatalogVisibilityIntegrationTests" --nologo -v normal`

Expected: PASS.

- [ ] **Step 4.6: Run the full Unit + Integration suites for the bounded contexts touched**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=SharedGameCatalog|BoundedContext=UserLibrary" --nologo -v q`

Expected: no NEW failures vs the pre-task baseline. Pre-existing flakies (memory MEMORY.md lists known ones) are OK.

- [ ] **Step 4.7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/ apps/api/tests/Api.Tests/Integration/SharedGameCatalog/PublicCatalogVisibilityIntegrationTests.cs
git commit -m "fix(catalog): drop is_rag_public filter from public Hub catalog query (#2043 bug 1)"
```

If the file paths under `git add` shift after Step 4.4 (e.g. a different handler file), adjust the `add` command to match the actual modified files.

---

## Acceptance & wrap-up

- [ ] **Step 5.1: Smoke verify against running stack (tunnel-mode)**

Pre-req: API + Web up under `make integration` with SSH tunnel to staging. If not running, follow the runbook from `docs/for-developers/workflows/snapshot-seed-workflow.md` or the session memory.

Run from repo root:

```bash
# Catan detail — should now include designers in the JSON
curl -sS -b /tmp/badsworm_cookies.txt \
  http://localhost:8080/api/v1/library/games/0b2a31a2-3f44-43c2-94aa-1860cf1a2c19 \
  | python -c "import json,sys; d=json.load(sys.stdin); print('designers:', d.get('designers'))"

# Hub catalog — should return >0 (not exact value, just non-empty)
curl -sS -b /tmp/badsworm_cookies.txt \
  "http://localhost:8080/api/v1/shared-games?page=1&pageSize=5" \
  | python -c "import json,sys; d=json.load(sys.stdin); print('totalCount:', d.get('totalCount') or d.get('total'))"
```

Expected:
- First curl: `designers: ['Klaus Teuber']` (or actual designer list)
- Second curl: positive integer (likely 159 or 32 depending on production seed).

If `designers` returns `None`, the BE handler is not loading the M:N relation — re-check Task 2 step 2.4. If `totalCount` is still 0, the wrong handler was patched — go back to Task 4 step 4.1.

- [ ] **Step 5.2: Visual smoke on the Catan detail page**

Open in browser at `http://localhost:3000/library/0b2a31a2-3f44-43c2-94aa-1860cf1a2c19`. The hero breadcrumb should now read **"Klaus Teuber · 1995 · 120 min · 3-4 giocatori · Complessità 2.3"** (designer prepended). Compare against `admin-mockups/design_files/sp3-shared-game-detail.html` for parity confidence.

- [ ] **Step 5.3: Close the resolved issues**

```bash
gh issue close 2022 -c "Fixed in branch feature/squash-migrations: search_vector + GIN index added to InitialCreate via raw SQL. Integration tests SearchVectorColumnIntegrationTests cover the column + index. See commit on this PR."
gh issue close 2035 -c "Fixed: BE exposes Designers on GameDetailDto via Include(g => g.Designers), FE schema + interface widened, GameDetailDesktop already consumes the field. Live Catan breadcrumb now shows 'Klaus Teuber'."
# #2043 bug #1 only — leave #2043 open with a comment noting bug #2 (/hub direct 404) and bug #3 (/hub/games/{uuid} 404) are still pending.
gh issue comment 2043 -c "Bug #1 (public catalog showing 0 games) fixed in this PR — dropped is_rag_public filter from SharedGameCatalog query handler. Bug #2 (/hub direct URL 404) and bug #3 (/hub/games/{uuid} detail 404) remain open."
```

- [ ] **Step 5.4: Push the branch (if working in a feature branch)**

```bash
git push -u origin "$(git branch --show-current)"
```

Then open a PR via `gh pr create` targeting the parent branch (`main-dev` for fresh feature branches, or `feature/squash-migrations` if these patches land on top of the squash work). Body should reference #2022, #2035, #2043, and the changed file scope.

---

## Self-review checklist (run before declaring done)

- [ ] Spec coverage: every issue (#2022, #2035, #2043 bug #1) has a Task that implements the fix end-to-end with a test.
- [ ] No placeholders: every code block contains complete code. No "TODO", "TBD", "fill in details", or "similar to Task N".
- [ ] Type consistency: `Designers` (C#) ↔ `designers` (TS/JSON) naming pattern matches everywhere. `IsRagPublic` (C#) ↔ `is_rag_public` (DB) is consistent.
- [ ] Tests fail-then-pass: every test step has an explicit "verify it fails" before the implementation step.
- [ ] Commits are small and focused: 3 separate commits (one per task), plus a 4th for any FE typecheck cleanup if needed.
