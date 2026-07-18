# #3153 Persist Wikidata designers/publishers (M2M) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist Wikidata designers/publishers as M2M join rows when a catalog-seed draft is promoted to a `SharedGame`, via get-or-create in the repository `AddAsync` path.

**Architecture:** Three layers. (1) `SharedGameRepository.AddAsync` resolves the aggregate's designer/publisher names against the unique `game_designers`/`game_publishers` tables (`EF.Functions.ILike` get-or-create) and attaches resolved entities to the graph; `MapToEntity` stays a pure scalar mapper and `Update` is untouched (no interface change). (2) `SharedGame.EnrichFromProvenance` is extended to ingest designer/publisher name lists. (3) `CatalogSeedApprovedEventHandler` reads those names from provenance and passes them through. Read-side `Publishers` hydration is added for round-trip symmetry.

**Tech Stack:** .NET 9, EF Core (Npgsql/pgvector), xUnit + Testcontainers Postgres, FluentAssertions, Moq.

## ⚠️ Revisions after adversarial plan review (supersede stale details below)

Two adversarial reviewers verified the plan against real code. Apply these deltas when executing (spec §4a / §5 carry the authoritative code):

1. **D6 → trailing OPTIONAL params.** `EnrichFromProvenance(int?, int?, int?, int?, Guid modifiedBy, IReadOnlyList<string>? designers = null, IReadOnlyList<string>? publishers = null)`. This makes the 7 existing `SharedGameSkeletonTests` call sites **and** the handler's current call compile unchanged → **no compile-break**. Task 2 Step 2/3's "update 7 call sites" is DROPPED; the signature change + handler wiring land together (fixes the M2 ordering hazard). The new aggregate unit tests pass `designers:`/`publishers:` by name.
2. **Resolver de-dups input** (`.Select(d => d.Name.Trim()).Where(non-blank).Distinct(StringComparer.OrdinalIgnoreCase)`) — fixes the duplicate-insert UNIQUE violation when the same name is supplied twice in one `AddAsync` (reachable via `CreateSharedGameCommand`, which caps at 20 names but does not de-dup). Use the spec §5.3 resolver body, not the Step-3 body below.
3. **`AddAsync` blast radius acknowledged**: only `CreateSharedGameCommandHandler` (besides promotion) builds a designer-carrying aggregate; this change **fixes** its pre-existing silent-drop. Add a repo test `AddAsync_WithDuplicateDesignerNamesInOneCall_PersistsSingleRow` (guards that path).
4. **Add publisher-reuse test** `AddAsync_WithExistingPublisherName_ReusesRowNoDuplicate` (mirror the designer-reuse test) — `ResolvePublishersAsync` is copy-pasted and otherwise unguarded on reuse.
5. **Flip the existing test, don't duplicate the helper**: in `CatalogSeedApprovedEventHandlerTests.cs`, flip `Handle_ProvenanceWithProperties_EnrichesNewSkeleton` (L358-385) to assert `Designers`/`Publishers` are now **populated** (reuse the existing `RichProvenance()` helper; drop the planned `ProvenanceWithDesignersPublishers()` duplicate) and update its `#3154` comment. `Handle_ExistingGameCollision_...` keeps `BeEmpty` (existing-game branch never enriches — D5 guard). The Step-6 wiring unit test uses `RichProvenance()`.
6. **`GetByIdAsync` gains `.AsSplitQuery()`** (two collection `.Include`s).

## Global Constraints

- **No EF migration** — schema (`game_designers`, `game_publishers`, `shared_game_designers`, `shared_game_publishers`, unique indexes `ix_game_designers_name`/`ix_game_publishers_name`) already exists.
- **No `ISharedGameRepository` interface change** — resolution lives inside the already-async `AddAsync`; `void Update` stays sync and untouched.
- **Scope = new-skeleton (`AddAsync`) branch only** — the existing-game (BggId collision) branch does not enrich; leave it alone.
- **Matching = `EF.Functions.ILike`** case-insensitive, mirroring `RelationshipSeeder.GetOrCreateDesignerAsync`. No concurrency/unique-violation retry (single-writer assumption, documented).
- **Leniency** — `EnrichFromProvenance` must not throw on bad designer/publisher names (blank or >200 chars are skipped), preserving its skip-don't-throw contract.
- **Backend test project path** — `apps/api/tests/Api.Tests` (NOT `tests/Api.Tests`). Integration tests: `[Collection("Integration-GroupC")]`, `[Trait("Category", TestCategories.Integration)]`, `[Trait("BoundedContext", "SharedGameCatalog")]`.
- **Kill testhost before running tests** (Windows): `tasklist | grep testhost` → `taskkill //PID <PID> //F`.

---

### Task 1: Repository — get-or-create M2M persistence in `AddAsync` + `Publishers` read-side hydration

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/SharedGameRepository.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/SharedGameRepositoryIntegrationTests.cs` (add a region; reuse the file's existing `_repository`/`_dbContext`/`TestUserId` setup)

**Interfaces:**
- Consumes: existing `SharedGame.AddDesigner(string)` / `AddPublisher(string)` mutators (to populate the aggregate in tests); `DbContext.GameDesigners` / `DbContext.GamePublishers` DbSets; `GameDesignerEntity` / `GamePublisherEntity` (`Api.Infrastructure.Entities.SharedGameCatalog`); domain `GameDesigner`/`GamePublisher` (`Api.BoundedContexts.SharedGameCatalog.Domain.Entities`).
- Produces: `AddAsync` now persists designer/publisher join rows (get-or-create by name); `GetByIdAsync` now hydrates `Publishers` too. Signature of `AddAsync` unchanged.

- [ ] **Step 1: Write the failing integration test (new names persisted)**

Add to `SharedGameRepositoryIntegrationTests.cs` (new `#region Designer/Publisher M2M persistence (#3153)` before the final `}`). Assumes the file's `using` set already imports `Microsoft.EntityFrameworkCore`, `FluentAssertions`, aggregates, entities.

```csharp
#region Designer/Publisher M2M persistence (#3153)

[Fact]
public async Task AddAsync_WithNewDesignerAndPublisher_PersistsJoinRows()
{
    // Arrange — a skeleton carrying one new designer + one new publisher
    var game = SharedGame.CreateSkeleton("Catan", TestUserId, TimeProvider.System);
    game.AddDesigner("Klaus Teuber");
    game.AddPublisher("Kosmos");

    // Act
    await _repository.AddAsync(game);
    await _dbContext.SaveChangesAsync();
    _dbContext.ChangeTracker.Clear();

    // Assert — lookup rows created exactly once + join rows present
    (await _dbContext.GameDesigners.CountAsync(d => d.Name == "Klaus Teuber")).Should().Be(1);
    (await _dbContext.GamePublishers.CountAsync(p => p.Name == "Kosmos")).Should().Be(1);

    var reloaded = await _dbContext.SharedGames
        .Include(g => g.Designers)
        .Include(g => g.Publishers)
        .FirstAsync(g => g.Id == game.Id);
    reloaded.Designers.Select(d => d.Name).Should().ContainSingle().Which.Should().Be("Klaus Teuber");
    reloaded.Publishers.Select(p => p.Name).Should().ContainSingle().Which.Should().Be("Kosmos");
}

#endregion
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "FullyQualifiedName~AddAsync_WithNewDesignerAndPublisher_PersistsJoinRows"`
Expected: FAIL — `reloaded.Designers` empty (join rows never written; `MapToEntity` maps no M2M).

- [ ] **Step 3: Implement `AddAsync` resolution + resolvers + `Publishers` read hydration**

In `SharedGameRepository.cs`. Ensure `using Microsoft.EntityFrameworkCore;`, `using Api.Infrastructure.Entities.SharedGameCatalog;`, and `using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;` are present (add any missing).

Replace `AddAsync` (currently L25-30):

```csharp
public async Task AddAsync(SharedGame sharedGame, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(sharedGame);
    var entity = MapToEntity(sharedGame);
    await ResolveDesignersAsync(entity, sharedGame.Designers, cancellationToken).ConfigureAwait(false);
    await ResolvePublishersAsync(entity, sharedGame.Publishers, cancellationToken).ConfigureAwait(false);
    await DbContext.Set<SharedGameEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
}
```

Add two private resolvers (place them near `MapToEntity`):

```csharp
// Issue #3153 — get-or-create each designer by name against the unique
// game_designers table (ILIKE, mirroring RelationshipSeeder.GetOrCreateDesignerAsync)
// and attach the resolved row to the new SharedGameEntity's M:N navigation so a
// single SaveChanges inserts the join rows. Existing names are reused (no
// duplicate insert / no unique-index violation); new names are inserted as part
// of the aggregate graph. No SaveChanges here — the caller owns the single flush.
private async Task ResolveDesignersAsync(
    SharedGameEntity entity,
    IReadOnlyCollection<GameDesigner> designers,
    CancellationToken cancellationToken)
{
    foreach (var designer in designers)
    {
        var trimmed = designer.Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            continue;
        }

        var existing = await DbContext.GameDesigners
            .FirstOrDefaultAsync(d => EF.Functions.ILike(d.Name, trimmed), cancellationToken)
            .ConfigureAwait(false);

        var resolved = existing
            ?? DbContext.GameDesigners.Local.FirstOrDefault(
                   d => string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            ?? new GameDesignerEntity { Id = Guid.NewGuid(), Name = trimmed, CreatedAt = DateTime.UtcNow };

        if (!entity.Designers.Any(d => ReferenceEquals(d, resolved) || d.Id == resolved.Id))
        {
            entity.Designers.Add(resolved);
        }
    }
}

private async Task ResolvePublishersAsync(
    SharedGameEntity entity,
    IReadOnlyCollection<GamePublisher> publishers,
    CancellationToken cancellationToken)
{
    foreach (var publisher in publishers)
    {
        var trimmed = publisher.Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            continue;
        }

        var existing = await DbContext.GamePublishers
            .FirstOrDefaultAsync(p => EF.Functions.ILike(p.Name, trimmed), cancellationToken)
            .ConfigureAwait(false);

        var resolved = existing
            ?? DbContext.GamePublishers.Local.FirstOrDefault(
                   p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            ?? new GamePublisherEntity { Id = Guid.NewGuid(), Name = trimmed, CreatedAt = DateTime.UtcNow };

        if (!entity.Publishers.Any(p => ReferenceEquals(p, resolved) || p.Id == resolved.Id))
        {
            entity.Publishers.Add(resolved);
        }
    }
}
```

Read-side symmetry — in `GetByIdAsync` add `.Include(g => g.Publishers)` next to the existing `.Include(g => g.Designers)`. In `MapToDomain`, after the existing `foreach (var designer in entity.Designers)` block, add:

```csharp
// Issue #3153 — hydrate publishers symmetrically with designers (only when the
// caller eager-loaded the navigation, e.g. GetByIdAsync).
foreach (var publisher in entity.Publishers)
{
    if (!string.IsNullOrWhiteSpace(publisher.Name))
    {
        sharedGame.AddPublisher(publisher.Name);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "FullyQualifiedName~AddAsync_WithNewDesignerAndPublisher_PersistsJoinRows"`
Expected: PASS.

- [ ] **Step 5: Add the reuse / case-insensitive / read-side tests**

Append to the same region:

```csharp
[Fact]
public async Task AddAsync_WithExistingDesignerName_ReusesRowNoDuplicate()
{
    // Arrange — pre-seed a designer via a first game
    var first = SharedGame.CreateSkeleton("Catan", TestUserId, TimeProvider.System);
    first.AddDesigner("Klaus Teuber");
    await _repository.AddAsync(first);
    await _dbContext.SaveChangesAsync();
    _dbContext.ChangeTracker.Clear();

    // Act — a second game references the SAME designer name
    var second = SharedGame.CreateSkeleton("Catan: Seafarers", TestUserId, TimeProvider.System);
    second.AddDesigner("Klaus Teuber");
    await _repository.AddAsync(second);
    await _dbContext.SaveChangesAsync();
    _dbContext.ChangeTracker.Clear();

    // Assert — exactly ONE designer row, both games linked to it
    (await _dbContext.GameDesigners.CountAsync(d => d.Name == "Klaus Teuber")).Should().Be(1);
    var designer = await _dbContext.GameDesigners
        .Include(d => d.SharedGames)
        .FirstAsync(d => d.Name == "Klaus Teuber");
    designer.SharedGames.Select(g => g.Id).Should().Contain(new[] { first.Id, second.Id });
}

[Fact]
public async Task AddAsync_WithCaseInsensitiveDesignerName_ReusesRow()
{
    var first = SharedGame.CreateSkeleton("A", TestUserId, TimeProvider.System);
    first.AddDesigner("Kosmos");   // (used as a designer name here purely to exercise casing)
    await _repository.AddAsync(first);
    await _dbContext.SaveChangesAsync();
    _dbContext.ChangeTracker.Clear();

    var second = SharedGame.CreateSkeleton("B", TestUserId, TimeProvider.System);
    second.AddDesigner("kosmos"); // different casing → must resolve to the same row via ILIKE
    await _repository.AddAsync(second);
    await _dbContext.SaveChangesAsync();
    _dbContext.ChangeTracker.Clear();

    (await _dbContext.GameDesigners.CountAsync(d => EF.Functions.ILike(d.Name, "kosmos"))).Should().Be(1);
}

[Fact]
public async Task GetByIdAsync_HydratesPublishers()
{
    var game = SharedGame.CreateSkeleton("Catan", TestUserId, TimeProvider.System);
    game.AddPublisher("Kosmos");
    await _repository.AddAsync(game);
    await _dbContext.SaveChangesAsync();
    _dbContext.ChangeTracker.Clear();

    var reloaded = await _repository.GetByIdAsync(game.Id);
    reloaded.Should().NotBeNull();
    reloaded!.Publishers.Select(p => p.Name).Should().ContainSingle().Which.Should().Be("Kosmos");
}
```

- [ ] **Step 6: Run the full region + verify pass**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "FullyQualifiedName~SharedGameRepositoryIntegrationTests"`
Expected: PASS (all AddAsync/GetById tests, including the 4 new ones).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/SharedGameRepository.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/SharedGameRepositoryIntegrationTests.cs
git commit -m "feat(shared-games): #3153 persist designer/publisher M2M in SharedGameRepository.AddAsync"
```

---

### Task 2: Aggregate `EnrichFromProvenance` ingestion + handler wiring + end-to-end pipeline test

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs` (`EnrichFromProvenance` signature + body + XML remarks)
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/CatalogSeedApprovedEventHandler.cs` (read designers/publishers from provenance + pass through + comment)
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameSkeletonTests.cs` (update 7 existing call sites; add designer/publisher unit tests)
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/EventHandlers/CatalogSeedApprovedEventHandlerTests.cs` (add a wiring unit test)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/EventHandlers/CatalogSeedApprovedEventHandlerPersistenceIntegrationTests.cs` (handler-driven real-pipeline test)

**Interfaces:**
- Consumes: Task 1's persisting `AddAsync`; `SharedGame.CreateSkeleton`, `AddDesigner`, `AddPublisher`, domain `GameDesigner.Create`/`GamePublisher.Create`; `CatalogSeedProvenance(IReadOnlyDictionary<string,FieldProvenance>)` ctor + `ToJson()` + `GetValue<List<string>>`; `FieldProvenance(Provider, SourceUrl, SourceField, FetchedAt, Value)`; `CatalogSeedDraftEntity`; real `SharedGameRepository`, `UnitOfWork`.
- Produces: `EnrichFromProvenance(int?, int?, int?, int?, IReadOnlyList<string>? designers, IReadOnlyList<string>? publishers, Guid modifiedBy)` — new required params `designers`/`publishers` inserted before `modifiedBy`.

- [ ] **Step 1: Write failing aggregate unit tests**

Add to `SharedGameSkeletonTests.cs` in the `EnrichFromProvenance` region:

```csharp
[Fact]
public void EnrichFromProvenance_WithDesignersAndPublishers_IngestsAndStampsAudit()
{
    var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

    game.EnrichFromProvenance(
        yearPublished: null, minPlayers: null, maxPlayers: null, playingTimeMinutes: null,
        designers: new[] { "Klaus Teuber" },
        publishers: new[] { "Kosmos" },
        modifiedBy: AdminUserId);

    game.Designers.Select(d => d.Name).Should().ContainSingle().Which.Should().Be("Klaus Teuber");
    game.Publishers.Select(p => p.Name).Should().ContainSingle().Which.Should().Be("Kosmos");
    game.ModifiedBy.Should().Be(AdminUserId, "adding a designer/publisher counts as a change");
}

[Fact]
public void EnrichFromProvenance_DuplicateAndBlankNames_AreDeDupedAndSkippedLeniently()
{
    var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

    game.EnrichFromProvenance(
        yearPublished: null, minPlayers: null, maxPlayers: null, playingTimeMinutes: null,
        designers: new[] { "Klaus Teuber", "  klaus teuber  ", "", "   ", new string('x', 201) },
        publishers: null,
        modifiedBy: AdminUserId);

    // trimmed + case-insensitive dedup → one entry; blank + >200-char skipped (no throw)
    game.Designers.Select(d => d.Name).Should().ContainSingle().Which.Should().Be("Klaus Teuber");
}

[Fact]
public void EnrichFromProvenance_NullNameLists_IsNoOpForCollections()
{
    var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

    game.EnrichFromProvenance(
        yearPublished: 1995, minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 90,
        designers: null, publishers: null,
        modifiedBy: AdminUserId);

    game.Designers.Should().BeEmpty();
    game.Publishers.Should().BeEmpty();
    game.YearPublished.Should().Be(1995, "scalar enrichment is unaffected by null name lists");
}
```

- [ ] **Step 2: Run to verify fail (compile error — old 5-param signature)**

Run: `cd apps/api/tests/Api.Tests && dotnet build`
Expected: FAIL — the new tests (and the 7 existing calls) don't match the current signature until Step 3.

- [ ] **Step 3: Extend `EnrichFromProvenance` + update the 7 existing call sites**

In `SharedGame.cs`, change the signature (insert `designers`/`publishers` before `modifiedBy`):

```csharp
public void EnrichFromProvenance(
    int? yearPublished,
    int? minPlayers,
    int? maxPlayers,
    int? playingTimeMinutes,
    IReadOnlyList<string>? designers,
    IReadOnlyList<string>? publishers,
    Guid modifiedBy)
```

Add, immediately before the closing `if (changed) { … }` block:

```csharp
if (designers is not null)
{
    foreach (var name in designers)
    {
        if (string.IsNullOrWhiteSpace(name)) continue;
        var trimmed = name.Trim();
        if (trimmed.Length > 200) continue; // lenient: skip implausible (mirror scalar leniency; avoid GameDesigner.Create throw)
        if (_designers.Any(d => string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase))) continue;
        _designers.Add(GameDesigner.Create(trimmed));
        changed = true;
    }
}

if (publishers is not null)
{
    foreach (var name in publishers)
    {
        if (string.IsNullOrWhiteSpace(name)) continue;
        var trimmed = name.Trim();
        if (trimmed.Length > 200) continue;
        if (_publishers.Any(p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase))) continue;
        _publishers.Add(GamePublisher.Create(trimmed));
        changed = true;
    }
}
```

Update the XML `<remarks>` above the method (the block stating designers/publishers are excluded per #3154) to state they are now ingested and persisted via the repository (#3153).

In `SharedGameSkeletonTests.cs`, update the **7 existing** `EnrichFromProvenance(…)` calls (the scalar-only tests) to pass the new params — add `designers: null, publishers: null,` immediately before each `modifiedBy:` argument. (Tests: `AllScalarsPresent…`, `AllFieldsAbsent…`, `ImplausibleYear…`, `YearInFuture…`, `InconsistentPlayerPair…`, `NonPositivePlayingTime…`, `EmptyModifiedBy…`.)

- [ ] **Step 4: Run aggregate tests + verify pass**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "FullyQualifiedName~SharedGameSkeletonTests"`
Expected: PASS (10 EnrichFromProvenance tests: 7 updated + 3 new).

- [ ] **Step 5: Wire the handler to read + pass designer/publisher names**

In `CatalogSeedApprovedEventHandler.cs`, after the four scalar reads (L109-119), add:

```csharp
// Issue #3153 — designer/publisher NAMES from provenance (Wikidata P178/P123,
// stored as List<string>). Persisted as M:N join rows by SharedGameRepository.AddAsync.
var provDesigners = provenance.GetValue<List<string>>("designers");
var provPublishers = provenance.GetValue<List<string>>("publishers");
```

Update the preceding comment (which says designers/publishers are deliberately not read). Then extend the new-skeleton `EnrichFromProvenance` call (L178-183):

```csharp
skeleton.EnrichFromProvenance(
    yearPublished: provYear,
    minPlayers: provMinPlayers,
    maxPlayers: provMaxPlayers,
    playingTimeMinutes: provPlayingTime,
    designers: provDesigners,
    publishers: provPublishers,
    modifiedBy: notification.ApprovedByUserId);
```

(The existing-game branch is unchanged.)

- [ ] **Step 6: Add the handler wiring unit test**

In `CatalogSeedApprovedEventHandlerTests.cs`, add a helper + test that proves the handler reads provenance designers/publishers into the aggregate (mocked repo captures the aggregate):

```csharp
private static string ProvenanceWithDesignersPublishers()
{
    var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal)
    {
        ["title"] = new FieldProvenance("wikidata", "https://www.wikidata.org/wiki/Q123", "labels.en", DateTime.UtcNow, "Catan"),
        ["designers"] = new FieldProvenance("wikidata", "https://www.wikidata.org/wiki/Q123", "P178", DateTime.UtcNow, new List<string> { "Klaus Teuber" }),
        ["publishers"] = new FieldProvenance("wikidata", "https://www.wikidata.org/wiki/Q123", "P123", DateTime.UtcNow, new List<string> { "Kosmos" }),
    };
    return new CatalogSeedProvenance(fields).ToJson();
}

[Fact]
public async Task Handle_ProvenanceWithDesignersPublishers_PopulatesAggregate()
{
    var draft = SeedFetchedDraft(provenanceJson: ProvenanceWithDesignersPublishers());
    _drafts.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
    _games.Setup(r => r.GetByBggIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((SharedGame?)null);

    SharedGame? added = null;
    _games.Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
          .Callback<SharedGame, CancellationToken>((g, _) => added = g)
          .Returns(Task.CompletedTask);

    await Handler().Handle(new CatalogSeedApprovedEvent(draft.Id, draft.ResultingSharedGameId!.Value, Guid.NewGuid()), default);

    added.Should().NotBeNull();
    added!.Designers.Select(d => d.Name).Should().ContainSingle().Which.Should().Be("Klaus Teuber");
    added.Publishers.Select(p => p.Name).Should().ContainSingle().Which.Should().Be("Kosmos");
}
```

- [ ] **Step 7: Write the handler-driven real-pipeline integration test**

Create `CatalogSeedApprovedEventHandlerPersistenceIntegrationTests.cs`. This drives the REAL repo + real `UnitOfWork` + real DbContext (only the drafts repo is a Moq lookup returning the fixture draft — the draft is just the input carrier). Verify the exact `UnitOfWork` ctor at implement time (`new UnitOfWork(_dbContext)` expected).

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedApprovedEventHandlerPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private SharedGameRepository _games = null!;
    private UnitOfWork _uow = null!;

    public CatalogSeedApprovedEventHandlerPersistenceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"seedapproved_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var cs = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(cs, o => o.UseVector())
            .Options;
        var eventCollector = new Mock<IDomainEventCollector>();
        eventCollector.Setup(x => x.GetAndClearEvents()).Returns(new List<IDomainEvent>().AsReadOnly());
        _dbContext = new MeepleAiDbContext(options, new Mock<IMediator>().Object, eventCollector.Object);
        await _dbContext.Database.MigrateAsync();
        _games = new SharedGameRepository(_dbContext, eventCollector.Object);
        _uow = new UnitOfWork(_dbContext);
    }

    public async ValueTask DisposeAsync() => await _dbContext.DisposeAsync();

    [Fact]
    public async Task Handle_WikidataDraftWithDesignersPublishers_PersistsJoinRows()
    {
        // Arrange — a draft whose provenance carries designer + publisher names
        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal)
        {
            ["title"] = new FieldProvenance("wikidata", "https://www.wikidata.org/wiki/Q123", "labels.en", DateTime.UtcNow, "Catan"),
            ["designers"] = new FieldProvenance("wikidata", "https://www.wikidata.org/wiki/Q123", "P178", DateTime.UtcNow, new List<string> { "Klaus Teuber" }),
            ["publishers"] = new FieldProvenance("wikidata", "https://www.wikidata.org/wiki/Q123", "P123", DateTime.UtcNow, new List<string> { "Kosmos" }),
        };
        var draft = new CatalogSeedDraftEntity
        {
            Id = Guid.NewGuid(),
            BggId = null, // pure-Wikidata skeleton
            Status = "Approved",
            ProvenanceJson = new CatalogSeedProvenance(fields).ToJson(),
            ResultingSharedGameId = Guid.NewGuid(),
            ApprovedAt = DateTime.UtcNow,
            ApprovedByUserId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

        var drafts = new Mock<ICatalogSeedDraftRepository>();
        drafts.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);

        var handler = new CatalogSeedApprovedEventHandler(
            drafts.Object, _games, _uow, TimeProvider.System,
            NullLogger<CatalogSeedApprovedEventHandler>.Instance);

        // Act — run the real promotion pipeline
        await handler.Handle(
            new CatalogSeedApprovedEvent(draft.Id, draft.ResultingSharedGameId!.Value, draft.ApprovedByUserId!.Value),
            default);
        _dbContext.ChangeTracker.Clear();

        // Assert — the materialised SharedGame has the M:N join rows persisted
        var game = await _dbContext.SharedGames
            .Include(g => g.Designers)
            .Include(g => g.Publishers)
            .SingleAsync(g => g.Title == "Catan");
        game.Designers.Select(d => d.Name).Should().ContainSingle().Which.Should().Be("Klaus Teuber");
        game.Publishers.Select(p => p.Name).Should().ContainSingle().Which.Should().Be("Kosmos");
    }
}
```

- [ ] **Step 8: Run the new unit + integration tests + verify pass**

Run:
```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~CatalogSeedApprovedEventHandler"
```
Expected: PASS (existing unit tests + new wiring unit test + new persistence integration test).

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/CatalogSeedApprovedEventHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameSkeletonTests.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/EventHandlers/CatalogSeedApprovedEventHandlerTests.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/EventHandlers/CatalogSeedApprovedEventHandlerPersistenceIntegrationTests.cs
git commit -m "feat(shared-games): #3153 ingest Wikidata designers/publishers in EnrichFromProvenance + wire promotion handler"
```

---

### Task 3: Full-suite verification for the bounded context

**Files:** none (verification only).

- [ ] **Step 1: Run the whole SharedGameCatalog bounded context**

Kill any stray testhost first (`tasklist | grep testhost` → `taskkill //PID <PID> //F`), then:
```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "BoundedContext=SharedGameCatalog"
```
Expected: PASS, zero regressions. Investigate any failure at root cause (do not skip).

- [ ] **Step 2: Confirm no migration was generated**

Run: `git status --short` — expect ONLY the 5 files from Tasks 1-2 (+ this plan/spec). No files under `Infrastructure/Migrations/`.

---

## Self-Review

**Spec coverage:**
- §5.1 aggregate `EnrichFromProvenance` → Task 2 Steps 1-4. ✓
- §5.2 handler wiring → Task 2 Steps 5-6. ✓
- §5.3 repo `AddAsync` get-or-create + `Publishers` read hydration → Task 1. ✓
- §6 no migration → Task 3 Step 2. ✓
- §7 testing (4 repo-level + handler-driven + aggregate unit) → Task 1 Steps 1/5, Task 2 Steps 1/6/7. ✓
- D1 no interface change (AddAsync internals only, Update untouched) → Task 1 Step 3. ✓
- D2 mirror seeder / no concurrency retry → resolver uses ILIKE + `.Local`, no retry. ✓
- D3 read-side Publishers → Task 1 Step 3 + Task 1 Step 5 (`GetByIdAsync_HydratesPublishers`). ✓
- D4 ILIKE → resolvers. ✓ · D5 new-skeleton only → handler existing branch untouched. ✓ · D6 signature extension → Task 2 Step 3. ✓

**Placeholder scan:** No TBD/TODO. Two "verify at implement time" notes (repo `using` set; `UnitOfWork` ctor) are verification steps, not missing content — the expected shapes are given.

**Type consistency:** `EnrichFromProvenance(…, IReadOnlyList<string>? designers, IReadOnlyList<string>? publishers, Guid modifiedBy)` used identically in Task 2 Steps 1, 3, 5 and the unit test. `ResolveDesignersAsync`/`ResolvePublishersAsync(SharedGameEntity, IReadOnlyCollection<GameDesigner|GamePublisher>, CancellationToken)` consistent between AddAsync (Step 3) and their definitions. `GetValue<List<string>>` matches the provenance `List<string>` storage.
