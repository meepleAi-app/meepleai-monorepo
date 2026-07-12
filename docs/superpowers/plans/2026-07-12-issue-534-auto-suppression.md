# #534 ME-M3.2 Auto-suppression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A recurring Quartz job that aggregates raw `mechanic_card_feedback` (#533) into per-card counters and auto-suppresses published mechanic cards that breach admin-tunable thresholds, with an audit trail and a suppression domain event for downstream review.

**Architecture:** Quartz `IJob` → `IMediator.Send(RunMechanicCardAutoSuppressionCommand)` (batch). The handler reads 3 config keys (SystemConfiguration BC), aggregates feedback per active card via a new repository query, applies the recomputed counters to the `MechanicCard` aggregate, and calls the existing `MechanicCard.Suppress(...)` when a card breaches thresholds. Persistence mirrors the existing `SuppressMechanicAnalysisCommandHandler` (detached-load → mutate → `repo.Update` → `SaveChangesAsync`). Reprocess-with-bumped-prompt is deferred (no v2 prompt exists); the raised `MechanicCardSuppressedEvent` signals manual review (#535 wires the admin notification).

**Tech Stack:** .NET 9, MediatR (custom `ICommand`/`ICommandHandler` wrappers), EF Core + PostgreSQL 16 (`xmin` optimistic concurrency), Quartz.NET, xUnit + Testcontainers, Moq.

**Spec:** `docs/superpowers/specs/2026-07-12-issue-534-auto-suppression.md`

## Global Constraints

- **CQRS**: the job is NOT an endpoint, so it MAY use `IMediator.Send`. Business logic lives in a command handler, not the job.
- **PERF-06 NoTracking default**: repo reads return **detached** domain aggregates via `Reconstitute`. Mutations MUST be persisted through `IMechanicCardRepository.Update(card)` (attach + `State=Modified`), else the write is a silent no-op (lesson `feedback_repo_updateasync_astracking_notracking`).
- **Suppression reason** MUST be 20..500 chars (domain invariant). The AC's literal `'auto_feedback'` (13 chars) is embedded as a prefix inside a ≥20-char human-readable reason; the machine tag also goes into the audit metadata.
- **System actor Guid**: `00000000-0000-0000-0000-000000000001` (already seeded in `users`; `suppressed_by` has no FK). Defined as a local `SystemActorId` const in the handler.
- **Config Environment field** = `"All"` for these global threshold keys (ADR-062 Idiom 1).
- **`system_configurations` columns are PascalCase** (`"Key"`, `"Value"`, `"ValueType"`, `"Category"`, `"Environment"`, `"Version"`, `"RequiresRestart"`, `"CreatedByUserId"`, `"CreatedAt"`, `"UpdatedAt"`, `"IsActive"`) — NOT snake_case. Use `migrationBuilder.InsertData` so EF handles quoting.
- **Backend test path**: `apps/api/tests/Api.Tests`. Run backend tests with an explicit csproj path and kill `testhost` first if a prior run hangs.
- **Build/test commands** (from `apps/api/src/Api`): `dotnet build`; tests: `dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "<expr>"`.

---

### Task 1: Seed the 3 admin-tunable config keys — ❌ DROPPED durante l'esecuzione

> **Esito**: rimosso. `system_configurations.CreatedByUserId` ha una FK **Restrict** a `users` e **nessun utente
> è seeded dalle migration** → un seed a migration-time viola la FK (`FK_system_configurations_users_CreatedByUserId`,
> 23503). Nessuna riga `system_configurations` è mai seeded via migration nel repo: il pattern è la creazione a runtime
> via admin config CRUD (fornisce un `CreatedByUserId` reale). L'auto-suppression usa i **default nel codice**
> (`GetValueAsync(key, default)`) finché un admin non tuna le soglie. Vedi spec §1 aggiornata. Il test di seed è stato
> eliminato; le soglie di default e l'override sono coperti dai test dell'handler (Task 4).

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddMechanicCardAutoSuppressionConfigSeeds.cs` (via `dotnet ef migrations add`)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/MechanicCardAutoSuppressionConfigSeedTests.cs`

**Interfaces:**
- Consumes: `IConfigurationService.GetValueAsync<T>(string key, T? defaultValue = default, string? environment = null)` (namespace `Api.Services`).
- Produces: 3 seeded rows in `system_configurations` (`Environment="All"`, `Category="MechanicCard"`): `MechanicCard:ErrorReportsThreshold`=`5` (int), `MechanicCard:FeedbackScoreThreshold`=`0.5` (decimal), `MechanicCard:AutoSuppressionEnabled`=`true` (bool).

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/MechanicCardAutoSuppressionConfigSeedTests.cs`:

```csharp
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardAutoSuppressionConfigSeedTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;

    public MechanicCardAutoSuppressionConfigSeedTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me534_configseed_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task Seeds_ThreeMechanicCardThresholdKeys_AsAllEnvironment()
    {
        using var scope = _factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        (await config.GetValueAsync<int>("MechanicCard:ErrorReportsThreshold", 0)).Should().Be(5);
        (await config.GetValueAsync<decimal>("MechanicCard:FeedbackScoreThreshold", 0m)).Should().Be(0.5m);
        (await config.GetValueAsync<bool>("MechanicCard:AutoSuppressionEnabled", false)).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Kill any stale test host, then run:
```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MechanicCardAutoSuppressionConfigSeedTests"
```
Expected: FAIL — `GetValueAsync` returns the passed defaults (0 / 0 / false), not the seeded 5 / 0.5 / true.

- [ ] **Step 3: Generate the migration and write the seed body**

From `apps/api/src/Api`:
```bash
dotnet ef migrations add AddMechanicCardAutoSuppressionConfigSeeds
```
Replace the generated `Up`/`Down` bodies with (keep the class/namespace EF generated):

```csharp
/// <inheritdoc />
protected override void Up(MigrationBuilder migrationBuilder)
{
    // #534 ME-M3.2: admin-tunable auto-suppression thresholds. Environment="All" (ADR-062 Idiom 1,
    // global keys). InsertData (not raw SQL) so it survives a future migration squash (#2785) and EF
    // handles the PascalCase column quoting. Keys are brand-new → no ON CONFLICT needed.
    var seededAt = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
    var systemUser = new Guid("00000000-0000-0000-0000-000000000001");

    migrationBuilder.InsertData(
        table: "system_configurations",
        columns: new[] { "Id", "Key", "Value", "ValueType", "Description", "Category", "IsActive", "RequiresRestart", "Environment", "Version", "CreatedAt", "UpdatedAt", "CreatedByUserId" },
        values: new object[,]
        {
            { new Guid("2f9a1d10-0001-4534-9a00-000000000001"), "MechanicCard:ErrorReportsThreshold", "5", "int", "Auto-suppress a mechanic card when its error-report count reaches this value (AND feedback score below threshold).", "MechanicCard", true, false, "All", 1, seededAt, seededAt, systemUser },
            { new Guid("2f9a1d10-0002-4534-9a00-000000000002"), "MechanicCard:FeedbackScoreThreshold", "0.5", "decimal", "Auto-suppress a mechanic card when its feedback score (positive/total) is below this value (AND error-report count at/above threshold).", "MechanicCard", true, false, "All", 1, seededAt, seededAt, systemUser },
            { new Guid("2f9a1d10-0003-4534-9a00-000000000003"), "MechanicCard:AutoSuppressionEnabled", "true", "bool", "Kill-switch for the mechanic-card feedback auto-suppression job.", "MechanicCard", true, false, "All", 1, seededAt, seededAt, systemUser },
        });
}

/// <inheritdoc />
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DeleteData(table: "system_configurations", keyColumn: "Id",
        keyValue: new Guid("2f9a1d10-0001-4534-9a00-000000000001"));
    migrationBuilder.DeleteData(table: "system_configurations", keyColumn: "Id",
        keyValue: new Guid("2f9a1d10-0002-4534-9a00-000000000002"));
    migrationBuilder.DeleteData(table: "system_configurations", keyColumn: "Id",
        keyValue: new Guid("2f9a1d10-0003-4534-9a00-000000000003"));
}
```

> If `dotnet ef migrations add` produced an EMPTY `Up()` (stale build), delete both the `.cs` and `.Designer.cs`, run `dotnet build`, then re-run the `migrations add` command (lesson from #533).

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MechanicCardAutoSuppressionConfigSeedTests"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/MechanicCardAutoSuppressionConfigSeedTests.cs
git commit -m "feat(mechanic-extractor): #534 seed auto-suppression config thresholds"
```

---

### Task 2: Domain — `MechanicCard.ApplyFeedbackAggregates`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicCard.cs` (add method after `MarkErrorReport`, ~line 207)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/MechanicCardApplyFeedbackAggregatesTests.cs`

**Interfaces:**
- Produces: `void MechanicCard.ApplyFeedbackAggregates(int errorReportsCount, decimal? feedbackScore, DateTime utcNow)` — sets `ErrorReportsCount`, `FeedbackScore`, `UpdatedAt`; throws `ArgumentOutOfRangeException` if `errorReportsCount < 0`.

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/MechanicCardApplyFeedbackAggregatesTests.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardApplyFeedbackAggregatesTests
{
    private static MechanicCard NewCard() => MechanicCard.Reconstitute(
        id: Guid.NewGuid(), sharedGameId: Guid.NewGuid(), originAnalysisId: Guid.NewGuid(),
        origin: MechanicCardOrigin.AiReviewed, title: "Catan — Comprehension Card", content: "{}",
        version: 1, isSuppressed: false, suppressedReason: null, suppressedAt: null, suppressedBy: null,
        errorReportsCount: 0, feedbackScore: null,
        publishedAt: DateTime.UtcNow, publishedBy: Guid.NewGuid(),
        createdAt: DateTime.UtcNow, updatedAt: DateTime.UtcNow, xminVersion: 0);

    [Fact]
    public void ApplyFeedbackAggregates_SetsCountScoreAndUpdatedAt()
    {
        var card = NewCard();
        var now = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);

        card.ApplyFeedbackAggregates(errorReportsCount: 5, feedbackScore: 0.42m, utcNow: now);

        card.ErrorReportsCount.Should().Be(5);
        card.FeedbackScore.Should().Be(0.42m);
        card.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void ApplyFeedbackAggregates_AllowsNullScore_WhenNoFeedback()
    {
        var card = NewCard();
        card.ApplyFeedbackAggregates(0, null, DateTime.UtcNow);
        card.FeedbackScore.Should().BeNull();
        card.ErrorReportsCount.Should().Be(0);
    }

    [Fact]
    public void ApplyFeedbackAggregates_Throws_WhenCountNegative()
    {
        var card = NewCard();
        var act = () => card.ApplyFeedbackAggregates(-1, null, DateTime.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
```

> Verify the `MechanicCardOrigin` enum member name (`AiReviewed`) against `Domain/Enums/MechanicCardOrigin.cs`; the seed string in tests elsewhere is `"ai_reviewed"`. Adjust the enum literal if the member differs.

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MechanicCardApplyFeedbackAggregatesTests"
```
Expected: FAIL — compile error: `MechanicCard` has no `ApplyFeedbackAggregates`.

- [ ] **Step 3: Add the domain method**

In `MechanicCard.cs`, immediately after the `MarkErrorReport(DateTime utcNow)` method (~line 207), add:

```csharp
/// <summary>
/// Overwrites the derived feedback aggregates with values recomputed from the raw
/// <c>mechanic_card_feedback</c> rows (#534 ME-M3.2). Pure state update; the suppression
/// decision (threshold evaluation) is an application concern that calls <see cref="Suppress"/>.
/// </summary>
public void ApplyFeedbackAggregates(int errorReportsCount, decimal? feedbackScore, DateTime utcNow)
{
    ArgumentOutOfRangeException.ThrowIfNegative(errorReportsCount);
    ErrorReportsCount = errorReportsCount;
    FeedbackScore = feedbackScore;
    UpdatedAt = utcNow;
}
```

> If `ErrorReportsCount` / `FeedbackScore` / `UpdatedAt` have `private set`, this compiles because the method is on the aggregate itself. If any has no setter at all, add `private set;` to that property.

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MechanicCardApplyFeedbackAggregatesTests"
```
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicCard.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/MechanicCardApplyFeedbackAggregatesTests.cs
git commit -m "feat(mechanic-extractor): #534 MechanicCard.ApplyFeedbackAggregates"
```

---

### Task 3: Repository — feedback aggregate query + tracked `Update`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/MechanicCardFeedbackAggregate.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/IMechanicCardRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicCardRepository.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/MechanicCardRepositoryFeedbackTests.cs`

**Interfaces:**
- Consumes: `MechanicCard` aggregate (Task 2), `MechanicCardEntity`, `MechanicCardFeedbackEntity`, `MeepleAiDbContext.MechanicCards` / `.MechanicCardFeedback`.
- Produces:
  - `sealed record MechanicCardFeedbackAggregate(Guid CardId, Guid SharedGameId, int NegativeCount, int PositiveCount)`
  - `Task<IReadOnlyList<MechanicCardFeedbackAggregate>> IMechanicCardRepository.GetActiveCardFeedbackAggregatesAsync(CancellationToken ct = default)` — active (non-suppressed) cards with ≥1 feedback row.
  - `void IMechanicCardRepository.Update(MechanicCard card)` — attach + `State=Modified`, xmin left unmodified, collects domain events.

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/MechanicCardRepositoryFeedbackTests.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardRepositoryFeedbackTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;
    private readonly Guid _userId = new("00000000-0000-0000-0000-000000000001");

    public MechanicCardRepositoryFeedbackTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me534_repo_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task GetActiveCardFeedbackAggregates_CountsPosNeg_AndExcludesSuppressed()
    {
        Guid activeCard, suppressedCard;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            activeCard = await SeedCardWithFeedbackAsync(db, isSuppressed: false, negatives: 3, positives: 2);
            suppressedCard = await SeedCardWithFeedbackAsync(db, isSuppressed: true, negatives: 4, positives: 0);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IMechanicCardRepository>();
            var aggs = await repo.GetActiveCardFeedbackAggregatesAsync();

            aggs.Should().ContainSingle(a => a.CardId == activeCard)
                .Which.Should().BeEquivalentTo(new { NegativeCount = 3, PositiveCount = 2 },
                    o => o.ExcludingMissingMembers());
            aggs.Should().NotContain(a => a.CardId == suppressedCard);
        }
    }

    [Fact]
    public async Task Update_PersistsMutatedAggregates()
    {
        Guid cardId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            cardId = await SeedCardWithFeedbackAsync(db, isSuppressed: false, negatives: 0, positives: 0);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IMechanicCardRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork>();
            var card = await repo.GetByIdIgnoringFiltersAsync(cardId);
            card!.ApplyFeedbackAggregates(7, 0.30m, DateTime.UtcNow);
            repo.Update(card);
            await uow.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var row = await db.MechanicCards.AsNoTracking().SingleAsync(c => c.Id == cardId);
            row.ErrorReportsCount.Should().Be(7);
            row.FeedbackScore.Should().Be(0.30m);
        }
    }

    private async Task<Guid> SeedCardWithFeedbackAsync(MeepleAiDbContext db, bool isSuppressed, int negatives, int positives)
    {
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId, Title = "Catan", Description = "t", ImageUrl = "", ThumbnailUrl = "",
            YearPublished = 1995, MinPlayers = 3, MaxPlayers = 4, PlayingTimeMinutes = 90, MinAge = 10,
            Status = 1, CreatedBy = _userId, CreatedAt = DateTime.UtcNow
        });
        var analysisId = Guid.NewGuid();
        db.MechanicAnalyses.Add(new MechanicAnalysisEntity
        {
            Id = analysisId, SharedGameId = gameId, PdfDocumentId = Guid.NewGuid(),
            PromptVersion = "v1.0.0",
            Status = (int)Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicAnalysisStatus.Published,
            CreatedBy = _userId, CreatedAt = DateTime.UtcNow, TotalTokensUsed = 0, EstimatedCostUsd = 0m,
            ModelUsed = "test", Provider = "test", CostCapUsd = 1.00m
        });
        var cardId = Guid.NewGuid();
        db.Set<MechanicCardEntity>().Add(new MechanicCardEntity
        {
            Id = cardId, SharedGameId = gameId, OriginAnalysisId = analysisId, Origin = "ai_reviewed",
            Title = "Catan — Comprehension Card", Content = "{}", Version = 1,
            IsSuppressed = isSuppressed,
            SuppressedReason = isSuppressed ? "seed suppressed for exclusion test coverage" : null,
            SuppressedAt = isSuppressed ? DateTime.UtcNow : null,
            SuppressedBy = isSuppressed ? _userId : (Guid?)null,
            ErrorReportsCount = 0, FeedbackScore = null,
            PublishedAt = DateTime.UtcNow, PublishedBy = _userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        for (var i = 0; i < negatives; i++)
            db.MechanicCardFeedback.Add(NewFeedback(cardId, isPositive: false));
        for (var i = 0; i < positives; i++)
            db.MechanicCardFeedback.Add(NewFeedback(cardId, isPositive: true));
        await db.SaveChangesAsync();
        return cardId;
    }

    private MechanicCardFeedbackEntity NewFeedback(Guid cardId, bool isPositive) => new()
    {
        Id = Guid.NewGuid(), CardId = cardId, UserId = Guid.NewGuid(), ClaimId = Guid.NewGuid(),
        IsPositive = isPositive, ErrorType = isPositive ? null : "factual",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
```

> The feedback unique index is `(card_id, user_id, claim_id)`; the helper uses a fresh `UserId`+`ClaimId` per row so multiple negatives/positives on one card never collide.

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MechanicCardRepositoryFeedbackTests"
```
Expected: FAIL — compile error: `MechanicCardFeedbackAggregate` and the two new repo members don't exist.

- [ ] **Step 3: Add the record**

Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/MechanicCardFeedbackAggregate.cs`:

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Per-card rollup of raw <c>mechanic_card_feedback</c> rows (#534 ME-M3.2). Negative = error reports.
/// </summary>
public sealed record MechanicCardFeedbackAggregate(Guid CardId, Guid SharedGameId, int NegativeCount, int PositiveCount);
```

- [ ] **Step 4: Extend the repository interface**

In `IMechanicCardRepository.cs`, add inside the interface (keep existing members):

```csharp
    /// <summary>Attaches the mutated aggregate as Modified so a subsequent SaveChanges persists it (#534).</summary>
    void Update(MechanicCard card);

    /// <summary>
    /// Per-card feedback rollup for all ACTIVE (non-suppressed) cards that have at least one feedback row (#534).
    /// </summary>
    Task<IReadOnlyList<MechanicCardFeedbackAggregate>> GetActiveCardFeedbackAggregatesAsync(
        CancellationToken cancellationToken = default);
```

- [ ] **Step 5: Implement in the repository**

In `MechanicCardRepository.cs`, add these methods (using directives at top: ensure `Microsoft.EntityFrameworkCore` is imported — it already is). The `Update` mirrors `MechanicAnalysisRepository.Update` (card has no in-aggregate child graph):

```csharp
    public void Update(MechanicCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var entity = MapToEntity(card);
        DbContext.MechanicCards.Attach(entity);
        DbContext.Entry(entity).State = EntityState.Modified;

        // Server-managed concurrency token — don't dirty it; EF compares WHERE xmin = @loaded.
        DbContext.Entry(entity).Property(e => e.Xmin).IsModified = false;

        CollectDomainEvents(card);
    }

    public async Task<IReadOnlyList<MechanicCardFeedbackAggregate>> GetActiveCardFeedbackAggregatesAsync(
        CancellationToken cancellationToken = default)
    {
        // MechanicCards reads apply the !IsSuppressed query filter → only active cards.
        // Inner join to feedback → only cards with ≥1 row are returned.
        return await DbContext.MechanicCards
            .AsNoTracking()
            .Join(
                DbContext.MechanicCardFeedback,
                card => card.Id,
                fb => fb.CardId,
                (card, fb) => new { card.Id, card.SharedGameId, fb.IsPositive })
            .GroupBy(x => new { x.Id, x.SharedGameId })
            .Select(g => new MechanicCardFeedbackAggregate(
                g.Key.Id,
                g.Key.SharedGameId,
                g.Count(x => !x.IsPositive),
                g.Count(x => x.IsPositive)))
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<MechanicCardFeedbackAggregate>)t.Result, cancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }
```

> If the `.ContinueWith` cast reads awkwardly against the file's style, instead write `var list = await …ToListAsync(ct).ConfigureAwait(false); return list;` and change the return type usage accordingly — `List<T>` satisfies `IReadOnlyList<T>` directly, so `return await …ToListAsync(ct)` compiles once the method returns `Task<IReadOnlyList<…>>`. Prefer the simpler form:
>
> ```csharp
> var list = await DbContext.MechanicCards
>     .AsNoTracking()
>     .Join(DbContext.MechanicCardFeedback, card => card.Id, fb => fb.CardId,
>           (card, fb) => new { card.Id, card.SharedGameId, fb.IsPositive })
>     .GroupBy(x => new { x.Id, x.SharedGameId })
>     .Select(g => new MechanicCardFeedbackAggregate(g.Key.Id, g.Key.SharedGameId,
>           g.Count(x => !x.IsPositive), g.Count(x => x.IsPositive)))
>     .ToListAsync(cancellationToken)
>     .ConfigureAwait(false);
> return list;
> ```

- [ ] **Step 6: Run test to verify it passes**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MechanicCardRepositoryFeedbackTests"
```
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/MechanicCardRepositoryFeedbackTests.cs
git commit -m "feat(mechanic-extractor): #534 card feedback aggregate query + tracked Update"
```

---

### Task 4: Application — batch command + handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/RunMechanicCardAutoSuppressionCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/RunMechanicCardAutoSuppressionCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/RunMechanicCardAutoSuppressionHandlerTests.cs`

**Interfaces:**
- Consumes: `IConfigurationService`, `IMechanicCardRepository` (Task 3: `GetActiveCardFeedbackAggregatesAsync`, `GetByIdIgnoringFiltersAsync`, `AddAuditLog`, `Update`), `IUnitOfWork`, `TimeProvider`, `MechanicCard.ApplyFeedbackAggregates` / `.Suppress` (Task 2 / existing), `MechanicCardAuditLog.Create`, `MechanicCardAuditAction.Suppressed`.
- Produces: `RunMechanicCardAutoSuppressionCommand : ICommand<AutoSuppressionResult>` (empty), `sealed record AutoSuppressionResult(int Evaluated, int Suppressed)`.

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/RunMechanicCardAutoSuppressionHandlerTests.cs`. Seeds are analogous to Task 3's helper; the test sends the command through the **real MediatR pipeline** (`IMediator`) per the "acceptance tests must exercise real pipeline" lesson.

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class RunMechanicCardAutoSuppressionHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;
    private readonly Guid _userId = new("00000000-0000-0000-0000-000000000001");

    public RunMechanicCardAutoSuppressionHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me534_handler_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private async Task<AutoSuppressionResult> RunAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new RunMechanicCardAutoSuppressionCommand());
    }

    [Fact]
    public async Task Breaching_Card_IsSuppressed_WithAudit_AndCounters()
    {
        Guid cardId;
        using (var scope = _factory.Services.CreateScope())
            cardId = await Seed.CardAsync(scope, _userId, negatives: 5, positives: 0);

        var result = await RunAsync();

        result.Suppressed.Should().Be(1);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var card = await db.MechanicCards.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == cardId);
            card.IsSuppressed.Should().BeTrue();
            card.SuppressedReason.Should().Contain("auto_feedback");
            card.ErrorReportsCount.Should().Be(5);
            (await db.Set<MechanicCardAuditLogEntity>().CountAsync(a => a.CardId == cardId && a.Action == 1))
                .Should().Be(1); // Suppressed = 1
        }
    }

    [Fact]
    public async Task HighScore_Card_IsNotSuppressed_ButCountersUpdated()
    {
        Guid cardId;
        using (var scope = _factory.Services.CreateScope())
            cardId = await Seed.CardAsync(scope, _userId, negatives: 5, positives: 10); // score 0.667 ≥ 0.5

        var result = await RunAsync();

        result.Suppressed.Should().Be(0);
        using var s = _factory.Services.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var card = await db.MechanicCards.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == cardId);
        card.IsSuppressed.Should().BeFalse();
        card.ErrorReportsCount.Should().Be(5);
    }

    [Fact]
    public async Task BelowCountThreshold_IsNotSuppressed()
    {
        using (var scope = _factory.Services.CreateScope())
            await Seed.CardAsync(scope, _userId, negatives: 4, positives: 0); // 4 < 5

        (await RunAsync()).Suppressed.Should().Be(0);
    }

    [Fact]
    public async Task KillSwitchDisabled_SuppressesNothing()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await Seed.CardAsync(scope, _userId, negatives: 5, positives: 0);
            await Seed.SetConfigAsync(db, "MechanicCard:AutoSuppressionEnabled", "false", "bool");
        }

        (await RunAsync()).Should().BeEquivalentTo(new AutoSuppressionResult(0, 0));
    }

    [Fact]
    public async Task ConfigOverride_LowersThreshold()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await Seed.CardAsync(scope, _userId, negatives: 3, positives: 0);
            await Seed.SetConfigAsync(db, "MechanicCard:ErrorReportsThreshold", "3", "int");
        }

        (await RunAsync()).Suppressed.Should().Be(1);
    }
}
```

Add a shared seed helper `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/MechanicCardAutoSuppressionSeed.cs`:

```csharp
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.SystemConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

internal static class Seed
{
    public static async Task<Guid> CardAsync(IServiceScope scope, Guid userId, int negatives, int positives)
    {
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId, Title = "Catan", Description = "t", ImageUrl = "", ThumbnailUrl = "",
            YearPublished = 1995, MinPlayers = 3, MaxPlayers = 4, PlayingTimeMinutes = 90, MinAge = 10,
            Status = 1, CreatedBy = userId, CreatedAt = DateTime.UtcNow
        });
        var analysisId = Guid.NewGuid();
        db.MechanicAnalyses.Add(new MechanicAnalysisEntity
        {
            Id = analysisId, SharedGameId = gameId, PdfDocumentId = Guid.NewGuid(), PromptVersion = "v1.0.0",
            Status = (int)Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicAnalysisStatus.Published,
            CreatedBy = userId, CreatedAt = DateTime.UtcNow, TotalTokensUsed = 0, EstimatedCostUsd = 0m,
            ModelUsed = "test", Provider = "test", CostCapUsd = 1.00m
        });
        var cardId = Guid.NewGuid();
        db.Set<MechanicCardEntity>().Add(new MechanicCardEntity
        {
            Id = cardId, SharedGameId = gameId, OriginAnalysisId = analysisId, Origin = "ai_reviewed",
            Title = "Catan — Comprehension Card", Content = "{}", Version = 1, IsSuppressed = false,
            ErrorReportsCount = 0, FeedbackScore = null,
            PublishedAt = DateTime.UtcNow, PublishedBy = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        for (var i = 0; i < negatives; i++)
            db.MechanicCardFeedback.Add(new MechanicCardFeedbackEntity
            { Id = Guid.NewGuid(), CardId = cardId, UserId = Guid.NewGuid(), ClaimId = Guid.NewGuid(), IsPositive = false, ErrorType = "factual", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        for (var i = 0; i < positives; i++)
            db.MechanicCardFeedback.Add(new MechanicCardFeedbackEntity
            { Id = Guid.NewGuid(), CardId = cardId, UserId = Guid.NewGuid(), ClaimId = Guid.NewGuid(), IsPositive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return cardId;
    }

    public static async Task SetConfigAsync(MeepleAiDbContext db, string key, string value, string valueType)
    {
        var existing = await db.Set<SystemConfigurationEntity>().FirstOrDefaultAsync(c => c.Key == key && c.Environment == "All");
        if (existing is null)
        {
            db.Set<SystemConfigurationEntity>().Add(new SystemConfigurationEntity
            {
                Id = Guid.NewGuid(), Key = key, Value = value, ValueType = valueType, Category = "MechanicCard",
                Environment = "All", IsActive = true, RequiresRestart = false, Version = 1,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedByUserId = new("00000000-0000-0000-0000-000000000001")
            });
        }
        else { existing.Value = value; existing.ValueType = valueType; }
        await db.SaveChangesAsync();
    }
}
```

> The config-cache: `IConfigurationService` uses a HybridCache (5-min TTL). Each test uses an **isolated database + fresh WebApplicationFactory**, so the cache starts empty; the first `GetValueAsync` after `SetConfigAsync` reads the DB value. No cache-eviction needed. Verify the `MechanicCardAuditLogEntity` type name/namespace and `Action` numeric mapping (`Suppressed`) against `Infrastructure/Entities/SharedGameCatalog/` before running; adjust the count query if the audit log DbSet is accessed differently.

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RunMechanicCardAutoSuppressionHandlerTests"
```
Expected: FAIL — compile error: command/handler/result types don't exist.

- [ ] **Step 3: Create the command + result**

Create `RunMechanicCardAutoSuppressionCommand.cs` (copy the `using` for `ICommand<>` from `SuppressMechanicAnalysisCommand.cs`):

```csharp
using Api.SharedKernel.Application.Messaging; // adjust to the namespace of ICommand<> in SuppressMechanicAnalysisCommand.cs

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Batch command (#534 ME-M3.2): aggregate mechanic-card feedback and auto-suppress cards breaching thresholds.
/// Invoked by <c>MechanicCardAutoSuppressionJob</c>.
/// </summary>
internal sealed record RunMechanicCardAutoSuppressionCommand : ICommand<AutoSuppressionResult>;

/// <summary>Outcome of one auto-suppression run.</summary>
internal sealed record AutoSuppressionResult(int Evaluated, int Suppressed);
```

> Open `SuppressMechanicAnalysisCommand.cs` and copy its exact `using` line for `ICommand<>` (the `Api.SharedKernel.Application.Messaging` above is a placeholder to be replaced with the real namespace).

- [ ] **Step 4: Create the handler**

Create `RunMechanicCardAutoSuppressionCommandHandler.cs`:

```csharp
using System.Text.Json;

using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Messaging; // same ICommandHandler<> namespace as SuppressMechanicAnalysisCommandHandler
using Api.SharedKernel.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

internal sealed class RunMechanicCardAutoSuppressionCommandHandler
    : ICommandHandler<RunMechanicCardAutoSuppressionCommand, AutoSuppressionResult>
{
    // Seeded system user (00000000-…-001); suppressed_by has no FK, honest audit actor for a system job.
    private static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000001");

    private readonly IMechanicCardRepository _cardRepository;
    private readonly IConfigurationService _configuration;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RunMechanicCardAutoSuppressionCommandHandler> _logger;

    public RunMechanicCardAutoSuppressionCommandHandler(
        IMechanicCardRepository cardRepository,
        IConfigurationService configuration,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<RunMechanicCardAutoSuppressionCommandHandler> logger)
    {
        _cardRepository = cardRepository;
        _configuration = configuration;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AutoSuppressionResult> Handle(RunMechanicCardAutoSuppressionCommand request, CancellationToken cancellationToken)
    {
        var enabled = await _configuration.GetValueAsync("MechanicCard:AutoSuppressionEnabled", true).ConfigureAwait(false);
        if (!enabled)
        {
            _logger.LogInformation("Mechanic-card auto-suppression disabled via config; skipping run.");
            return new AutoSuppressionResult(0, 0);
        }

        var errorThreshold = await _configuration.GetValueAsync("MechanicCard:ErrorReportsThreshold", 5).ConfigureAwait(false);
        var scoreThreshold = await _configuration.GetValueAsync("MechanicCard:FeedbackScoreThreshold", 0.5m).ConfigureAwait(false);

        var aggregates = await _cardRepository.GetActiveCardFeedbackAggregatesAsync(cancellationToken).ConfigureAwait(false);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var evaluated = 0;
        var suppressed = 0;

        foreach (var agg in aggregates)
        {
            var total = agg.NegativeCount + agg.PositiveCount;
            decimal? score = total > 0 ? (decimal)agg.PositiveCount / total : null;

            var card = await _cardRepository.GetByIdIgnoringFiltersAsync(agg.CardId, cancellationToken).ConfigureAwait(false);
            if (card is null || card.IsSuppressed)
            {
                continue; // race: suppressed between the aggregate scan and this load
            }

            evaluated++;
            card.ApplyFeedbackAggregates(agg.NegativeCount, score, utcNow);

            var breach = agg.NegativeCount >= errorThreshold && score.HasValue && score.Value < scoreThreshold;
            if (breach)
            {
                var reason = $"auto_feedback: {agg.NegativeCount} error reports, feedback score {score!.Value:0.00} below {scoreThreshold:0.00} threshold";
                card.Suppress(SystemActorId, reason, utcNow);

                var metadata = JsonSerializer.Serialize(new
                {
                    source = "auto_feedback",
                    errorReports = agg.NegativeCount,
                    feedbackScore = score.Value,
                    errorThreshold,
                    scoreThreshold
                });
                _cardRepository.AddAuditLog(
                    MechanicCardAuditLog.Create(card.Id, MechanicCardAuditAction.Suppressed, SystemActorId, utcNow, metadata));
                suppressed++;
            }

            _cardRepository.Update(card);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict auto-processing mechanic card {CardId}; skipping.", agg.CardId);
            }
        }

        _logger.LogInformation("Mechanic-card auto-suppression complete: evaluated={Evaluated}, suppressed={Suppressed}.", evaluated, suppressed);
        return new AutoSuppressionResult(evaluated, suppressed);
    }
}
```

> `GetValueAsync("...", 5)` infers `T=int`, `("...", 0.5m)` infers `decimal`, `("...", true)` infers `bool` — no explicit generic needed. Confirm `MechanicCardAuditLog` namespace is `Api.BoundedContexts.SharedGameCatalog.Domain.Entities` and `MechanicCardAuditAction` is `…Domain.Enums` (from the subsystem map). The handler is auto-registered by the MediatR assembly scan (same as `SuppressMechanicAnalysisCommandHandler`) — no manual DI entry.

- [ ] **Step 5: Run test to verify it passes**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RunMechanicCardAutoSuppressionHandlerTests"
```
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/RunMechanicCardAutoSuppressionHandlerTests.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/MechanicCardAutoSuppressionSeed.cs
git commit -m "feat(mechanic-extractor): #534 auto-suppression batch command + handler"
```

---

### Task 5: Quartz job + registration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/MechanicCardAutoSuppressionJob.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs` (add `RegisterMechanicCardAutoSuppressionJob(services);` next to `RegisterWikidataCoverEnrichmentJob(services);`, and the private method near line ~486)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Jobs/MechanicCardAutoSuppressionJobTests.cs`

**Interfaces:**
- Consumes: `IMediator`, `RunMechanicCardAutoSuppressionCommand` / `AutoSuppressionResult` (Task 4).
- Produces: `MechanicCardAutoSuppressionJob : IJob` sending the command; a durable Quartz job+trigger on an hourly cron.

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Jobs/MechanicCardAutoSuppressionJobTests.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardAutoSuppressionJobTests
{
    [Fact]
    public async Task Execute_SendsRunCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<RunMechanicCardAutoSuppressionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoSuppressionResult(2, 1));

        var services = new ServiceCollection();
        services.AddSingleton(mediator.Object);
        var provider = services.BuildServiceProvider();

        var job = new MechanicCardAutoSuppressionJob(provider, NullLogger<MechanicCardAutoSuppressionJob>.Instance);

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(context.Object);

        mediator.Verify(m => m.Send(It.IsAny<RunMechanicCardAutoSuppressionCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MechanicCardAutoSuppressionJobTests"
```
Expected: FAIL — compile error: `MechanicCardAutoSuppressionJob` doesn't exist.

- [ ] **Step 3: Create the job**

Create `MechanicCardAutoSuppressionJob.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Hourly recurring job (#534 ME-M3.2): sends <see cref="RunMechanicCardAutoSuppressionCommand"/> to
/// aggregate mechanic-card feedback and auto-suppress cards breaching admin-tunable thresholds.
/// </summary>
[DisallowConcurrentExecution]
public sealed class MechanicCardAutoSuppressionJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MechanicCardAutoSuppressionJob> _logger;

    public MechanicCardAutoSuppressionJob(
        IServiceProvider serviceProvider,
        ILogger<MechanicCardAutoSuppressionJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new RunMechanicCardAutoSuppressionCommand(), ct).ConfigureAwait(false);
            _logger.LogInformation(
                "MechanicCardAutoSuppressionJob: evaluated={Evaluated}, suppressed={Suppressed}.",
                result.Evaluated, result.Suppressed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MechanicCardAutoSuppressionJob failed.");
        }
    }
}
```

- [ ] **Step 4: Register the job**

In `SharedGameCatalogServiceExtensions.cs`, add a call next to the existing `RegisterWikidataCoverEnrichmentJob(services);` inside `AddSharedGameCatalogContext`:

```csharp
        RegisterMechanicCardAutoSuppressionJob(services);
```

And add the private method next to `RegisterWikidataCoverEnrichmentJob` (~line 486):

```csharp
    private static void RegisterMechanicCardAutoSuppressionJob(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("MechanicCardAutoSuppressionJob", "SharedGameCatalog");

            q.AddJob<Application.Jobs.MechanicCardAutoSuppressionJob>(opts => opts
                .WithIdentity(jobKey)
                .StoreDurably(true));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("MechanicCardAutoSuppressionTrigger", "SharedGameCatalog")
                .WithCronSchedule("0 0 * * * ?") // top of every hour, UTC
                .WithDescription("Hourly: aggregate mechanic-card feedback and auto-suppress cards breaching thresholds (#534)."));
        });
    }
```

> Match the `using Quartz;` / `Application.Jobs` reference style already present in the file for `WikidataCoverEnrichmentJob`. If the file references jobs via a `using` for the Jobs namespace, add `using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;` and use `MechanicCardAutoSuppressionJob` unqualified.

- [ ] **Step 5: Run test to verify it passes**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MechanicCardAutoSuppressionJobTests"
```
Expected: PASS.

- [ ] **Step 6: Full build + focused suite green**

```bash
cd apps/api/src/Api
dotnet build
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MechanicCard&(FullyQualifiedName~AutoSuppression|FullyQualifiedName~ApplyFeedback|FullyQualifiedName~RepositoryFeedback|FullyQualifiedName~ConfigSeed)"
```
Expected: build succeeds; all #534 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Jobs/MechanicCardAutoSuppressionJobTests.cs
git commit -m "feat(mechanic-extractor): #534 Quartz auto-suppression job + registration"
```

---

## Self-Review

**Spec coverage:**
- AC "Background job (Quartz) evalua feedback counts" → Task 5 (job) + Task 4 (handler aggregation). ✓
- AC "Soglia default 5 AND score<0.5 → auto-suppress" → Task 4 breach logic + Task 1 defaults. ✓
- AC "is_suppressed=true, reason auto_feedback" → Task 4 `card.Suppress` + reason prefix. ✓
- AC "Enqueue nuova analisi current+1 / alert admin" → **deferred branch** (spec §Decisioni 1): raise event only; no reprocess (no v2 prompt). Documented cut. ✓ (partial by design)
- AC "Audit log su suppression" → Task 4 `MechanicCardAuditLog` + auto `MechanicCardSuppressedEvent` audit. ✓
- AC "Config soglie via SystemConfiguration (admin tunable)" → Task 1 seeds + Task 4 `IConfigurationService` reads. ✓

**Placeholder scan:** Two intentional namespace-lookups flagged inline (the `ICommand<>`/`ICommandHandler<>` `using` copied from `SuppressMechanicAnalysisCommand*.cs`) — these are concrete "open file X, copy line Y" instructions, not open-ended TODOs. All code blocks are complete.

**Type consistency:** `AutoSuppressionResult(int Evaluated, int Suppressed)`, `MechanicCardFeedbackAggregate(Guid CardId, Guid SharedGameId, int NegativeCount, int PositiveCount)`, `ApplyFeedbackAggregates(int, decimal?, DateTime)`, `GetActiveCardFeedbackAggregatesAsync(CancellationToken)`, `Update(MechanicCard)` — names identical across tasks 2→5.

**Known verification points for the implementer** (call out, don't guess): (a) `MechanicCardOrigin` enum member (`AiReviewed` vs other); (b) `ICommand`/`ICommandHandler` namespace; (c) `MechanicCardAuditLogEntity` DbSet access + `Action` numeric value for `Suppressed` (=1 per the map); (d) whether `ErrorReportsCount`/`FeedbackScore`/`UpdatedAt` need `private set` added.
