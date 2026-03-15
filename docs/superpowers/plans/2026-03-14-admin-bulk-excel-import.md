# Admin Bulk Excel Import & BGG Enrichment — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable admin to import games from Excel, enrich via BGG queue, auto-download PDFs, and export enriched catalog.

**Architecture:** Extend existing SharedGameCatalog domain (new factory+status), extend BggImportQueueEntity with JobType discriminator, add Excel parsing via ClosedXML, decouple PDF creation via domain events.

**Tech Stack:** .NET 9, EF Core, ClosedXML, MediatR, PostgreSQL (jsonb), Polly, xUnit+Testcontainers, Next.js 16 (React 19, shadcn/ui, TanStack Table)

**Spec:** `docs/superpowers/specs/2026-03-14-admin-bulk-excel-import-design.md`

---

## File Map

### New Files (Backend)

| File | Responsibility |
|------|---------------|
| `Api/BoundedContexts/SharedGameCatalog/Domain/Enums/GameDataStatus.cs` | New enum |
| `Api/BoundedContexts/SharedGameCatalog/Domain/Enums/BggQueueJobType.cs` | New enum (Import/Enrichment) |
| `Api/BoundedContexts/SharedGameCatalog/Domain/Events/PdfReadyForProcessingEvent.cs` | Domain event |
| `Api/BoundedContexts/SharedGameCatalog/Domain/Events/SharedGamePdfUploadedEvent.cs` | Domain event |
| `Api/BoundedContexts/SharedGameCatalog/Application/Commands/ImportGamesFromExcelCommand.cs` | Command + Handler |
| `Api/BoundedContexts/SharedGameCatalog/Application/Commands/ExportGamesToExcelCommand.cs` | Command + Handler (query-side) |
| `Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnqueueEnrichmentCommand.cs` | Command + Handler |
| `Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnqueueAllSkeletonsCommand.cs` | Command + Handler |
| `Api/BoundedContexts/SharedGameCatalog/Application/Commands/MarkGamesCompleteCommand.cs` | Command + Handler |
| `Api/BoundedContexts/SharedGameCatalog/Application/Commands/AutoDownloadPdfCommand.cs` | Command + Handler |
| `Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/PdfReadyForProcessingEventHandler.cs` | Dispatches to DocumentProcessing |
| `Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/SharedGamePdfUploadedEventHandler.cs` | Sets HasUploadedPdf flag |
| `Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/SsrfSafeHttpClient.cs` | SSRF-validated download |
| `Api/Routing/AdminCatalogIngestionEndpoints.cs` | New endpoints |
| `Api/Infrastructure/Migrations/XXXXXX_AlterBggImportQueueForEnrichment.cs` | Migration 1 (auto-generated) |
| `Api/Infrastructure/Migrations/XXXXXX_AddGameDataStatusAndSkeletonSupport.cs` | Migration 2 (auto-generated) |

### Modified Files (Backend)

| File | Changes |
|------|---------|
| `SharedGame.cs` | Add `CreateSkeleton()`, `EnrichFromBgg()`, `MarkComplete()`, `GameDataStatus` property |
| `GameRules.cs` | Add `ExternalUrl`, `CreateFromUrl()` factory |
| `BggImportQueueEntity.cs` | Add `JobType`, `SharedGameId`, `BatchId`; make `BggId` nullable |
| `IBggImportQueueService.cs` | Rename `EnqueueAsync` → `EnqueueImportAsync`, add `EnqueueEnrichmentAsync/BatchAsync` |
| `BggImportQueueService.cs` | Implement new methods, atomic claiming SQL |
| `BggImportQueueBackgroundService.cs` | Add enrichment branch, stale recovery, circuit breaker |
| `BggImportQueueEndpoints.cs` | Add new enrichment routes |
| `ISharedGameRepository.cs` | Add `GetByTitleAsync()`, `ExistsByTitleAsync()`, `GetSkeletonGamesAsync()` |
| `SharedGameRepository.cs` | Implement new methods |
| `MeepleAiDbContext.cs` | Configure new columns, GameDataStatus, JobType |
| `RateLimitingServiceExtensions.cs` | Add `ExcelImportAdmin` policy |
| `InfrastructureServiceExtensions.cs` | Register new HttpClient ("PdfDownloader"), ClosedXML |
| `SharedGameCatalogHealthCheck.cs` | Add enrichment queue depth check |

### New Files (Frontend)

| File | Responsibility |
|------|---------------|
| `apps/web/src/app/admin/(dashboard)/catalog-ingestion/page.tsx` | Main page with 3 tabs |
| `apps/web/src/app/admin/(dashboard)/catalog-ingestion/lib/catalog-ingestion-api.ts` | API client + React Query hooks |
| `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/ExcelImportTab.tsx` | Tab 1: Upload + preview |
| `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/EnrichmentQueueTab.tsx` | Tab 2: Queue management |
| `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/ExportTab.tsx` | Tab 3: Export |

### Test Files

| File | Coverage |
|------|----------|
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameSkeletonTests.cs` | CreateSkeleton, EnrichFromBgg, state machine |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/GameRulesExternalUrlTests.cs` | GameRules.CreateFromUrl, Create with ExternalUrl |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/ImportGamesFromExcelCommandTests.cs` | Excel parsing, dedup, validation |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/EnqueueEnrichmentCommandTests.cs` | Enqueue idempotency, validation |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/AutoDownloadPdfCommandTests.cs` | SSRF, download, failure handling |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/MarkGamesCompleteCommandTests.cs` | State transition validation |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/BggEnrichmentProcessingTests.cs` | Background service enrichment branch |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/SsrfSafeHttpClientTests.cs` | IP validation, redirect handling |
| `tests/Api.Tests/Integration/ExcelImportEndpointTests.cs` | Full endpoint integration |
| `tests/Api.Tests/Integration/EnrichmentQueueIntegrationTests.cs` | Queue processing, atomic claim, stale recovery |
| `apps/web/__tests__/admin/catalog-ingestion/` | Frontend component tests |

---

## Chunk 1: Domain Model Changes

### Task 1.1: `GameDataStatus` Enum

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/GameDataStatus.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameSkeletonTests.cs`

- [ ] **Step 1: Create the enum file**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

public enum GameDataStatus
{
    Skeleton = 0,
    EnrichmentQueued = 1,
    Enriching = 2,
    Enriched = 3,
    PdfDownloading = 4,
    Complete = 5,
    Failed = 6
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/GameDataStatus.cs
git commit -m "feat(shared-game-catalog): add GameDataStatus enum for data completeness lifecycle"
```

---

### Task 1.2: `BggQueueJobType` Enum

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/BggQueueJobType.cs`

- [ ] **Step 1: Create the enum file**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

public enum BggQueueJobType
{
    Import = 0,
    Enrichment = 1
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/BggQueueJobType.cs
git commit -m "feat(shared-game-catalog): add BggQueueJobType enum for queue discriminator"
```

---

### Task 1.3: `GameRules.ExternalUrl` + `CreateFromUrl()`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/GameRules.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/GameRulesExternalUrlTests.cs`

- [ ] **Step 1: Write tests for GameRules changes**

```csharp
public class GameRulesExternalUrlTests
{
    [Fact]
    public void CreateFromUrl_WithValidUrl_ShouldCreateWithOnlyExternalUrl()
    {
        var rules = GameRules.CreateFromUrl("https://example.com/rules.pdf");
        Assert.Equal("https://example.com/rules.pdf", rules.ExternalUrl);
        Assert.Empty(rules.Content);
        Assert.Empty(rules.Language);
    }

    [Fact]
    public void CreateFromUrl_WithEmptyUrl_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => GameRules.CreateFromUrl(""));
    }

    [Fact]
    public void CreateFromUrl_WithHttpUrl_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => GameRules.CreateFromUrl("http://insecure.com/rules.pdf"));
    }

    [Fact]
    public void Create_WithExternalUrl_ShouldSetAllFields()
    {
        var rules = GameRules.Create("content", "en", "https://example.com/rules.pdf");
        Assert.Equal("content", rules.Content);
        Assert.Equal("en", rules.Language);
        Assert.Equal("https://example.com/rules.pdf", rules.ExternalUrl);
    }

    [Fact]
    public void Create_WithoutExternalUrl_ShouldWorkAsBeforeBackwardCompat()
    {
        var rules = GameRules.Create("content", "en");
        Assert.Null(rules.ExternalUrl);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail** (GameRules has no ExternalUrl yet)

```bash
cd apps/api/src/Api && dotnet test --filter "GameRulesExternalUrlTests" --no-build 2>&1 || true
```

- [ ] **Step 3: Implement GameRules changes**

Add to `GameRules.cs`:
- New property: `public string? ExternalUrl { get; private set; }`
- Private constructor: add `string? externalUrl = null` param
- Update `Create()`: add optional `string? externalUrl = null` param
- New factory: `CreateFromUrl(string externalUrl)` — validates HTTPS, non-empty; sets Content="", Language=""
- Update `GetEqualityComponents()` to include ExternalUrl

- [ ] **Step 4: Run tests — verify they pass**

```bash
cd apps/api/src/Api && dotnet test --filter "GameRulesExternalUrlTests"
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/GameRules.cs
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/GameRulesExternalUrlTests.cs
git commit -m "feat(shared-game-catalog): add ExternalUrl to GameRules value object"
```

---

### Task 1.4: `SharedGame.CreateSkeleton()` + `GameDataStatus` Property

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameSkeletonTests.cs`

- [ ] **Step 1: Write tests for CreateSkeleton**

```csharp
public class SharedGameSkeletonTests
{
    private static readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void CreateSkeleton_WithValidTitle_ShouldCreateWithDefaults()
    {
        var game = SharedGame.CreateSkeleton("Catan", Guid.NewGuid(), _timeProvider);
        Assert.Equal("Catan", game.Title);
        Assert.Equal(GameDataStatus.Skeleton, game.GameDataStatus);
        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Equal(0, game.YearPublished);
        Assert.Equal(0, game.MinPlayers);
        Assert.Equal(0, game.MaxPlayers);
    }

    [Fact]
    public void CreateSkeleton_WithBggId_ShouldSetBggId()
    {
        var game = SharedGame.CreateSkeleton("Catan", Guid.NewGuid(), _timeProvider, bggId: 13);
        Assert.Equal(13, game.BggId);
    }

    [Fact]
    public void CreateSkeleton_WithEmptyTitle_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            SharedGame.CreateSkeleton("", Guid.NewGuid(), _timeProvider));
    }

    [Fact]
    public void CreateSkeleton_WithTitleOver500Chars_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            SharedGame.CreateSkeleton(new string('x', 501), Guid.NewGuid(), _timeProvider));
    }

    [Fact]
    public void CreateSkeleton_CannotSubmitForApproval()
    {
        var game = SharedGame.CreateSkeleton("Catan", Guid.NewGuid(), _timeProvider);
        Assert.Throws<InvalidOperationException>(() =>
            game.SubmitForApproval(Guid.NewGuid()));
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

- [ ] **Step 3: Implement CreateSkeleton on SharedGame**

Add to `SharedGame.cs`:
- New property: `public GameDataStatus GameDataStatus { get; private set; } = GameDataStatus.Complete;`
- New property: `public string? BggRawData { get; private set; }` — NO, this is infra-only. Do NOT add to domain.
- New factory method using private constructor directly:

```csharp
public static SharedGame CreateSkeleton(string title, Guid createdBy, TimeProvider timeProvider, int? bggId = null)
{
    if (string.IsNullOrWhiteSpace(title))
        throw new ArgumentException("Title cannot be empty.", nameof(title));
    if (title.Length > 500)
        throw new ArgumentException("Title cannot exceed 500 characters.", nameof(title));

    var now = timeProvider.GetUtcNow().UtcDateTime;
    var game = new SharedGame
    {
        Id = Guid.NewGuid(),
        Title = title.Trim(),
        YearPublished = 0,
        Description = string.Empty,
        MinPlayers = 0,
        MaxPlayers = 0,
        PlayingTimeMinutes = 0,
        MinAge = 0,
        GameDataStatus = GameDataStatus.Skeleton,
        Status = GameStatus.Draft,
        BggId = bggId,
        CreatedBy = createdBy,
        CreatedAt = now,
        ModifiedAt = now
    };
    return game;
}
```

- Modify `SubmitForApproval()` to guard: `if (GameDataStatus < GameDataStatus.Enriched) throw new InvalidOperationException("Game must be enriched before approval.");`

- [ ] **Step 4: Run tests — verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameSkeletonTests.cs
git commit -m "feat(shared-game-catalog): add CreateSkeleton factory and GameDataStatus property"
```

---

### Task 1.5: `SharedGame.EnrichFromBgg()` + State Machine

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameSkeletonTests.cs` (append)

- [ ] **Step 1: Write state machine + enrichment tests**

```csharp
// Append to SharedGameSkeletonTests.cs

[Fact]
public void EnrichFromBgg_OnSkeleton_AfterEnrichingState_ShouldEnrich()
{
    var game = SharedGame.CreateSkeleton("Catan", Guid.NewGuid(), TimeProvider.System);
    game.TransitionTo(GameDataStatus.EnrichmentQueued);
    game.TransitionTo(GameDataStatus.Enriching);
    game.EnrichFromBgg(
        description: "Trade, build, settle",
        yearPublished: 1995,
        minPlayers: 3, maxPlayers: 4,
        playingTimeMinutes: 90, minAge: 10,
        complexityRating: 2.3m, averageRating: 7.2m,
        imageUrl: "https://cf.geekdo-images.com/catan.jpg",
        thumbnailUrl: "https://cf.geekdo-images.com/catan_t.jpg",
        rulebookUrl: "https://example.com/catan-rules.pdf");

    Assert.Equal(GameDataStatus.Enriched, game.GameDataStatus);
    Assert.Equal(1995, game.YearPublished);
    Assert.Equal("Trade, build, settle", game.Description);
}

[Fact]
public void TransitionTo_InvalidTransition_ShouldThrow()
{
    var game = SharedGame.CreateSkeleton("Catan", Guid.NewGuid(), TimeProvider.System);
    // Skeleton → Complete is invalid
    Assert.Throws<InvalidOperationException>(() =>
        game.TransitionTo(GameDataStatus.Complete));
}

[Fact]
public void MarkComplete_FromEnriched_ShouldTransition()
{
    var game = SharedGame.CreateSkeleton("Catan", Guid.NewGuid(), TimeProvider.System);
    game.TransitionTo(GameDataStatus.EnrichmentQueued);
    game.TransitionTo(GameDataStatus.Enriching);
    game.EnrichFromBgg(/* valid params */);
    game.MarkComplete();
    Assert.Equal(GameDataStatus.Complete, game.GameDataStatus);
}

[Fact]
public void Failed_CanBeReenqueued()
{
    var game = SharedGame.CreateSkeleton("Catan", Guid.NewGuid(), TimeProvider.System);
    game.TransitionTo(GameDataStatus.EnrichmentQueued);
    game.TransitionTo(GameDataStatus.Enriching);
    game.TransitionTo(GameDataStatus.Failed);
    game.TransitionTo(GameDataStatus.EnrichmentQueued); // Re-enqueue
    Assert.Equal(GameDataStatus.EnrichmentQueued, game.GameDataStatus);
}
```

- [ ] **Step 2: Run tests — verify they fail**

- [ ] **Step 3: Implement state machine and EnrichFromBgg**

Add to `SharedGame.cs`:

```csharp
private static readonly Dictionary<GameDataStatus, HashSet<GameDataStatus>> _validTransitions = new()
{
    [GameDataStatus.Skeleton] = new() { GameDataStatus.EnrichmentQueued },
    [GameDataStatus.EnrichmentQueued] = new() { GameDataStatus.Enriching },
    [GameDataStatus.Enriching] = new() { GameDataStatus.Enriched, GameDataStatus.Failed },
    [GameDataStatus.Enriched] = new() { GameDataStatus.PdfDownloading, GameDataStatus.Complete },
    [GameDataStatus.PdfDownloading] = new() { GameDataStatus.Complete, GameDataStatus.Enriched },
    [GameDataStatus.Failed] = new() { GameDataStatus.EnrichmentQueued },
    [GameDataStatus.Complete] = new() { }
};

public void TransitionTo(GameDataStatus newStatus)
{
    if (!_validTransitions.TryGetValue(GameDataStatus, out var valid) || !valid.Contains(newStatus))
        throw new InvalidOperationException(
            $"Cannot transition from {GameDataStatus} to {newStatus}.");
    GameDataStatus = newStatus;
}

public void EnrichFromBgg(string description, int yearPublished, int minPlayers,
    int maxPlayers, int playingTimeMinutes, int minAge,
    decimal? complexityRating, decimal? averageRating,
    string imageUrl, string thumbnailUrl, string? rulebookUrl)
{
    if (GameDataStatus != GameDataStatus.Enriching)
        throw new InvalidOperationException($"Cannot enrich from state {GameDataStatus}.");

    // Run full domain validation
    ValidateDescription(description);
    ValidateYear(yearPublished);
    ValidatePlayers(minPlayers, maxPlayers);
    ValidatePlayingTime(playingTimeMinutes);

    Title = Title; // unchanged
    Description = description;
    YearPublished = yearPublished;
    MinPlayers = minPlayers;
    MaxPlayers = maxPlayers;
    PlayingTimeMinutes = playingTimeMinutes;
    MinAge = minAge;
    ComplexityRating = complexityRating;
    AverageRating = averageRating;
    ImageUrl = imageUrl;
    ThumbnailUrl = thumbnailUrl;

    if (!string.IsNullOrEmpty(rulebookUrl))
        Rules = GameRules.CreateFromUrl(rulebookUrl);

    GameDataStatus = GameDataStatus.Enriched;
}

public void MarkComplete()
{
    TransitionTo(GameDataStatus.Complete);
}
```

- [ ] **Step 4: Run tests — verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameSkeletonTests.cs
git commit -m "feat(shared-game-catalog): add EnrichFromBgg, state machine, MarkComplete on SharedGame"
```

---

### Task 1.6: Domain Events

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/PdfReadyForProcessingEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/SharedGamePdfUploadedEvent.cs`

- [ ] **Step 1: Create both event files**

```csharp
// PdfReadyForProcessingEvent.cs
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;
public sealed record PdfReadyForProcessingEvent(
    Guid PdfDocumentId, Guid SharedGameId, Guid UserId) : INotification;

// SharedGamePdfUploadedEvent.cs
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;
public sealed record SharedGamePdfUploadedEvent(Guid SharedGameId) : INotification;
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/
git commit -m "feat(shared-game-catalog): add PdfReadyForProcessing and SharedGamePdfUploaded domain events"
```

---

## Chunk 2: Infrastructure — Queue Extension + Migrations

### Task 2.1: Extend `BggImportQueueEntity`

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/BggImportQueueEntity.cs`

- [ ] **Step 1: Add new columns to entity**

```csharp
// Add these properties:
public BggQueueJobType JobType { get; set; } = BggQueueJobType.Import;
public Guid? SharedGameId { get; set; }
public Guid? BatchId { get; set; }

// Change BggId from:
public required int BggId { get; set; }
// To:
public int? BggId { get; set; }
```

- [ ] **Step 2: Fix all compilation errors** — search all usages of `BggId` that assume non-null. Key files:
  - `BggImportQueueService.cs`: Add null guards on `BggId.Value` in import path
  - `BggImportQueueBackgroundService.cs`: Add null check before `new ImportGameFromBggCommand(queueItem.BggId.Value, ...)`
  - `BggImportQueueEndpoints.cs`: existing routes still pass `int bggId` — validated at endpoint level

- [ ] **Step 3: Verify build succeeds**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/BggImportQueueEntity.cs
git add apps/api/src/Api/Infrastructure/Services/BggImportQueueService.cs
git add apps/api/src/Api/Infrastructure/BackgroundServices/BggImportQueueBackgroundService.cs
git commit -m "feat(infrastructure): extend BggImportQueueEntity with JobType, SharedGameId, BatchId"
```

---

### Task 2.2: Extend `IBggImportQueueService` Interface

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Services/IBggImportQueueService.cs`
- Modify: `apps/api/src/Api/Infrastructure/Services/BggImportQueueService.cs`

- [ ] **Step 1: Rename `EnqueueAsync` → `EnqueueImportAsync` in interface and implementation**

- [ ] **Step 2: Add new methods to interface**

```csharp
Task<BggImportQueueEntity> EnqueueEnrichmentAsync(
    Guid sharedGameId, int? bggId, string gameName,
    Guid requestedByUserId, Guid batchId, CancellationToken ct = default);

Task<List<BggImportQueueEntity>> EnqueueEnrichmentBatchAsync(
    IEnumerable<(Guid SharedGameId, int? BggId, string GameName)> items,
    Guid requestedByUserId, CancellationToken ct = default);

Task<bool> ClaimNextQueuedItemAsync(Guid itemId, CancellationToken ct = default);
```

- [ ] **Step 3: Implement in `BggImportQueueService.cs`**

`EnqueueEnrichmentAsync`: Creates entity with `JobType = Enrichment`, sets SharedGameId, BatchId.

`ClaimNextQueuedItemAsync`: Raw SQL atomic claim:
```csharp
var affected = await _dbContext.Database.ExecuteSqlRawAsync(
    "UPDATE bgg_import_queue SET status = 1, updated_at = @p0 WHERE id = @p1 AND status = 0",
    timeProvider.GetUtcNow().UtcDateTime, itemId);
return affected > 0;
```

- [ ] **Step 4: Update all callers of old `EnqueueAsync`** to use `EnqueueImportAsync`

Search: `grep -r "EnqueueAsync" --include="*.cs"` in api/src — update each callsite.

- [ ] **Step 5: Build and verify**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Services/
git commit -m "feat(infrastructure): extend IBggImportQueueService with enrichment methods and atomic claim"
```

---

### Task 2.3: EF Configuration + Migration 1

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`

- [ ] **Step 1: Add EF configuration for new columns**

In `OnModelCreating`, find `BggImportQueueEntity` config and add:
```csharp
entity.Property(e => e.JobType).HasDefaultValue(BggQueueJobType.Import);
entity.Property(e => e.SharedGameId).IsRequired(false);
entity.Property(e => e.BatchId).IsRequired(false);
entity.Property(e => e.BggId).IsRequired(false); // Was required
entity.HasOne<SharedGameEntity>().WithMany().HasForeignKey(e => e.SharedGameId).IsRequired(false);
```

- [ ] **Step 2: Generate Migration 1**

```bash
cd apps/api/src/Api && dotnet ef migrations add AlterBggImportQueueForEnrichment
```

- [ ] **Step 3: Review generated migration** — verify it adds JobType, SharedGameId, BatchId, and alters BggId nullability. Check that existing data defaults are correct.

- [ ] **Step 4: Apply migration to dev DB**

```bash
cd apps/api/src/Api && dotnet ef database update
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(infrastructure): migration AlterBggImportQueueForEnrichment"
```

---

### Task 2.4: SharedGame EF Config + Migration 2

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`

- [ ] **Step 1: Add EF configuration for SharedGame new columns**

```csharp
// On SharedGameEntity:
entity.Property(e => e.GameDataStatus).HasDefaultValue(GameDataStatus.Complete);
entity.Property(e => e.BggRawData).HasColumnType("jsonb").IsRequired(false);
entity.Property(e => e.HasUploadedPdf).HasDefaultValue(false);

// On GameRules owned type:
entity.OwnsOne(e => e.Rules, rules => {
    // existing config...
    rules.Property(r => r.ExternalUrl).IsRequired(false);
});
```

Note: `BggRawData` is on the **infrastructure entity** (`SharedGameEntity`), NOT on the domain aggregate. Add property to `SharedGameEntity` if it uses a separate entity class, or handle via shadow property.

- [ ] **Step 2: Add `GameDataStatus`, `HasUploadedPdf` to SharedGameEntity/mapping**

Find the SharedGame entity configuration pattern (Domain ↔ Infrastructure mapping in `SharedGameRepository.cs`). Add the new properties to both the infra entity and the mapping methods.

- [ ] **Step 3: Generate Migration 2**

```bash
cd apps/api/src/Api && dotnet ef migrations add AddGameDataStatusAndSkeletonSupport
```

- [ ] **Step 4: Review migration** — verify all columns added, defaults correct, backfill for existing rows.

- [ ] **Step 5: Apply migration**

```bash
cd apps/api/src/Api && dotnet ef database update
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/
git commit -m "feat(infrastructure): migration AddGameDataStatusAndSkeletonSupport"
```

---

### Task 2.5: Repository Extensions

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/ISharedGameRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/SharedGameRepository.cs`

- [ ] **Step 1: Add new methods to interface**

```csharp
Task<bool> ExistsByTitleAsync(string title, CancellationToken ct = default);
Task<List<SharedGame>> GetByGameDataStatusAsync(GameDataStatus status, CancellationToken ct = default);
```

- [ ] **Step 2: Implement in repository**

`ExistsByTitleAsync`: case-insensitive `EF.Functions.ILike(e.Title, title)`
`GetByGameDataStatusAsync`: filter by status, return list

- [ ] **Step 3: Build and verify**

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/
git commit -m "feat(shared-game-catalog): add repository methods for title check and status filter"
```

---

## Chunk 3: Excel Import Command

### Task 3.1: Add ClosedXML Dependency

- [ ] **Step 1: Add NuGet package**

```bash
cd apps/api/src/Api && dotnet add package ClosedXML --version 0.104.1
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/Api.csproj
git commit -m "chore(deps): add ClosedXML 0.104.1 for Excel parsing"
```

---

### Task 3.2: `ImportGamesFromExcelCommand` + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ImportGamesFromExcelCommand.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/ImportGamesFromExcelCommandTests.cs`

- [ ] **Step 1: Write unit tests for the handler** (mock ISharedGameRepository)

Key test cases:
- Valid 2-row Excel → 2 games created
- Row with existing BggId → skip as duplicate
- Row with existing title (case-insensitive) → skip
- Row with empty name → error
- Row with whitespace name → error
- Row with BggId ≤ 0 → error
- Intra-file duplicate (same BggId in rows 2 and 5) → second skipped
- Row with name > 500 chars → error

- [ ] **Step 2: Run tests — verify they fail**

- [ ] **Step 3: Implement command + handler**

```csharp
public sealed record ImportGamesFromExcelCommand(
    IFormFile File, Guid UserId) : ICommand<ExcelImportResult>;

public sealed record ExcelImportResult(
    int Total, int Created, int Duplicates, int Errors,
    IReadOnlyList<ExcelRowError> RowErrors);

public sealed record ExcelRowError(int RowNumber, string? ColumnName, string ErrorMessage);
```

Handler:
1. Open workbook via `new XLWorkbook(command.File.OpenReadStream())`
2. Get first worksheet
3. Find header row, locate "Name" and "BggId" columns
4. Iterate data rows (skip header)
5. Per row: trim, validate, check intra-file set, check DB, create skeleton
6. Each row in its own `SaveChangesAsync` call (partial success)

- [ ] **Step 4: Add FluentValidation validator**

```csharp
public class ImportGamesFromExcelCommandValidator : AbstractValidator<ImportGamesFromExcelCommand>
{
    public ImportGamesFromExcelCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File.Length).LessThanOrEqualTo(5 * 1024 * 1024).WithMessage("Max 5MB");
        RuleFor(x => x.File.FileName).Must(f => f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 5: Run tests — verify they pass**

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ImportGamesFromExcelCommand.cs
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/ImportGamesFromExcelCommandTests.cs
git commit -m "feat(shared-game-catalog): add ImportGamesFromExcelCommand with handler and validation"
```

---

### Task 3.3: Rate Limit Policy + Endpoint

**Files:**
- Modify: `apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs`
- Create: `apps/api/src/Api/Routing/AdminCatalogIngestionEndpoints.cs`

- [ ] **Step 1: Add `ExcelImportAdmin` rate limit policy**

```csharp
options.AddPolicy("ExcelImportAdmin", _ =>
    RateLimitPartition.GetFixedWindowLimiter<string>(
        "ExcelImportAdmin",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1,
            Window = TimeSpan.FromMinutes(5)
        }));
```

- [ ] **Step 2: Create endpoint file with import route**

```csharp
public static class AdminCatalogIngestionEndpoints
{
    public static void MapAdminCatalogIngestionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin/catalog-ingestion")
            .WithTags("Admin Catalog Ingestion")
            .RequireAuthorization("AdminOrEditorPolicy");

        group.MapPost("/excel-import", async (IFormFile file, HttpContext ctx, IMediator mediator) =>
        {
            var (authorized, session, error) = ctx.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new ImportGamesFromExcelCommand(file, session!.User!.Id));
            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .RequireRateLimiting("ExcelImportAdmin");
    }
}
```

- [ ] **Step 3: Register endpoint in Program.cs / endpoint registration**

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs
git add apps/api/src/Api/Routing/AdminCatalogIngestionEndpoints.cs
git commit -m "feat(routing): add admin catalog ingestion endpoint for Excel import"
```

---

## Chunk 4: BGG Enrichment Processing

### Task 4.1: Enqueue Enrichment Commands

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnqueueEnrichmentCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnqueueAllSkeletonsCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MarkGamesCompleteCommand.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/EnqueueEnrichmentCommandTests.cs`

- [ ] **Step 1: Write tests for idempotency**

```csharp
[Fact]
public async Task EnqueueEnrichment_GameAlreadyQueued_ShouldSkip()
{
    // Game with GameDataStatus.EnrichmentQueued should be skipped
}

[Fact]
public async Task EnqueueEnrichment_SkeletonGame_ShouldEnqueue()
{
    // Game with GameDataStatus.Skeleton should be enqueued
}

[Fact]
public async Task EnqueueEnrichment_FailedGame_ShouldReenqueue()
{
    // Game with GameDataStatus.Failed should be re-enqueued
}

[Fact]
public async Task MarkGamesComplete_EnrichedGame_ShouldTransition()
{
    // Game with GameDataStatus.Enriched should become Complete
}

[Fact]
public async Task MarkGamesComplete_SkeletonGame_ShouldSkip()
{
    // Game with GameDataStatus.Skeleton should be skipped (invalid transition)
}
```

- [ ] **Step 2: Run tests — verify they fail**

- [ ] **Step 3: Implement commands**

`EnqueueEnrichmentCommand(IReadOnlyList<Guid> SharedGameIds, Guid UserId)`:
- Load games, filter by `GameDataStatus` in (Skeleton, Failed)
- Transition each to `EnrichmentQueued`
- Call `IBggImportQueueService.EnqueueEnrichmentBatchAsync`
- Return `EnqueueResult(int Enqueued, int Skipped)`

`EnqueueAllSkeletonsCommand(Guid UserId)`:
- Query all games with `GameDataStatus.Skeleton` or `Failed`
- Same logic as above

`MarkGamesCompleteCommand(IReadOnlyList<Guid> SharedGameIds)`:
- Load games, filter by `GameDataStatus == Enriched`
- Call `game.MarkComplete()` on each
- Return count

- [ ] **Step 4: Run tests — verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/EnqueueEnrichmentCommandTests.cs
git commit -m "feat(shared-game-catalog): add enqueue enrichment and mark-complete commands"
```

---

### Task 4.2: Extend Background Service for Enrichment

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/BackgroundServices/BggImportQueueBackgroundService.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/BggEnrichmentProcessingTests.cs`

- [ ] **Step 1: Write tests for enrichment branch**

Key tests:
- Item with `JobType = Enrichment` + BggId → fetches details, updates SharedGame
- Item with `JobType = Enrichment` + no BggId → auto-match search, then fetch
- Auto-match zero results → mark Failed
- Auto-match ambiguous → mark Failed
- Atomic claim: two calls for same item → only one succeeds
- Stale recovery: item stuck > 5min → reset to Queued

- [ ] **Step 2: Run tests — verify they fail**

- [ ] **Step 3: Implement enrichment branch in `ProcessNextQueueItemAsync`**

After claiming item:
```csharp
if (queueItem.JobType == BggQueueJobType.Enrichment)
{
    await ProcessEnrichmentItemAsync(queueItem, ct);
    return;
}
// ... existing import logic
```

`ProcessEnrichmentItemAsync`:
1. If `BggId` is null → auto-match via `SearchGamesAsync`
2. Fetch details via `GetGameDetailsAsync`
3. Load SharedGame, call `EnrichFromBgg(...)`
4. Store `BggRawData` on infra entity (not domain)
5. Check if rules URL exists → dispatch `AutoDownloadPdfCommand`
6. Save changes

- [ ] **Step 4: Add stale recovery method**

```csharp
private async Task RecoverStaleItemsAsync(IBggImportQueueService queueService, CancellationToken ct)
{
    // Raw SQL: reset Processing items older than 5 min
    // Log each recovered item at Warning
}
```

Call in `ExecuteAsync` on startup and every 5 minutes.

- [ ] **Step 5: Add Polly circuit breaker on BGG HttpClient**

In `InfrastructureServiceExtensions.cs` → `AddHttpClients()`:
```csharp
services.AddHttpClient("BggApi")
    .AddPolicyHandler(Policy.Handle<HttpRequestException>()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(60)));
```

- [ ] **Step 6: Run tests — verify they pass**

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/BackgroundServices/BggImportQueueBackgroundService.cs
git add apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/BggEnrichmentProcessingTests.cs
git commit -m "feat(infrastructure): extend BggImportQueueBackgroundService with enrichment processing"
```

---

### Task 4.3: Enrichment Endpoints + Batch Notification

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminCatalogIngestionEndpoints.cs` (or `BggImportQueueEndpoints.cs`)

- [ ] **Step 1: Add enrichment endpoints**

```csharp
group.MapPost("/enqueue-enrichment", async (EnqueueEnrichmentRequest req, HttpContext ctx, IMediator m) =>
{
    var (auth, session, err) = ctx.RequireAdminSession();
    if (!auth) return err!;
    var result = await m.Send(new EnqueueEnrichmentCommand(req.SharedGameIds, session!.User!.Id));
    return Results.Ok(result);
});

group.MapPost("/enqueue-all-skeletons", async (HttpContext ctx, IMediator m) =>
{
    var (auth, session, err) = ctx.RequireAdminSession();
    if (!auth) return err!;
    var result = await m.Send(new EnqueueAllSkeletonsCommand(session!.User!.Id));
    return Results.Ok(result);
});

group.MapPost("/mark-complete", async (MarkCompleteRequest req, HttpContext ctx, IMediator m) =>
{
    var (auth, session, err) = ctx.RequireAdminSession();
    if (!auth) return err!;
    var result = await m.Send(new MarkGamesCompleteCommand(req.SharedGameIds));
    return Results.Ok(result);
});
```

- [ ] **Step 2: Add batch notification event handler**

Create handler that checks when last item in a BatchId completes → sends notification via `UserNotifications`.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/AdminCatalogIngestionEndpoints.cs
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/
git commit -m "feat(routing): add enrichment queue and mark-complete admin endpoints"
```

---

## Chunk 5: PDF Auto-Download

### Task 5.1: SSRF-Safe HTTP Client

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/SsrfSafeHttpClient.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/SsrfSafeHttpClientTests.cs`

- [ ] **Step 1: Write SSRF validation tests**

```csharp
[Theory]
[InlineData("http://example.com/file.pdf", false)]    // HTTP rejected
[InlineData("https://10.0.0.1/file.pdf", false)]      // Private IP
[InlineData("https://172.16.0.1/file.pdf", false)]     // Private IP
[InlineData("https://192.168.1.1/file.pdf", false)]    // Private IP
[InlineData("https://127.0.0.1/file.pdf", false)]      // Loopback
[InlineData("https://169.254.169.254/file.pdf", false)] // Link-local/AWS metadata
[InlineData("https://[::1]/file.pdf", false)]           // IPv6 loopback
[InlineData("https://example.com/rules.pdf", true)]     // Valid
public async Task ValidateUrl_ShouldRejectUnsafeUrls(string url, bool expected) { }

[Fact]
public async Task Download_NonPdfContentType_ShouldThrow() { }

[Fact]
public async Task Download_InvalidMagicBytes_ShouldThrow() { }

[Fact]
public async Task Download_ExceedsMaxSize_ShouldThrow() { }
```

- [ ] **Step 2: Run tests — verify they fail**

- [ ] **Step 3: Implement SsrfSafeHttpClient**

```csharp
public sealed class SsrfSafeHttpClient(HttpClient httpClient)
{
    public async Task<Stream> DownloadPdfAsync(string url, CancellationToken ct)
    {
        ValidateUrlScheme(url);
        var resolvedIp = await ResolveAndValidateIpAsync(url, ct);
        // Configure HttpClient to use resolved IP, disable redirects
        // Download, validate Content-Type, magic bytes, size
        return stream;
    }

    private static void ValidateUrlScheme(string url) { /* HTTPS only */ }
    private static async Task<IPAddress> ResolveAndValidateIpAsync(string url, CancellationToken ct) { /* DNS resolve + IP range check */ }
    private static bool IsPrivateOrReserved(IPAddress ip) { /* All blocked ranges */ }
}
```

- [ ] **Step 4: Run tests — verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/SsrfSafeHttpClient.cs
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/SsrfSafeHttpClientTests.cs
git commit -m "feat(shared-game-catalog): add SsrfSafeHttpClient for PDF auto-download"
```

---

### Task 5.2: AutoDownloadPdfCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AutoDownloadPdfCommand.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/AutoDownloadPdfCommandTests.cs`

- [ ] **Step 1: Write tests**

Key cases:
- Valid URL → download, store blob, create PdfDocument, publish events, set Complete
- SSRF blocked → set Enriched, no blob stored
- Download timeout → set Enriched
- Blob stored but PdfDocument fails → delete orphan blob, set Enriched

- [ ] **Step 2: Run tests — verify they fail**

- [ ] **Step 3: Implement command + handler**

```csharp
public sealed record AutoDownloadPdfCommand(
    Guid SharedGameId, string PdfUrl, Guid RequestedByUserId) : ICommand;
```

Handler:
1. Download via `SsrfSafeHttpClient.DownloadPdfAsync`
2. `IBlobStorageService.StoreAsync(stream)` → blobId
3. Create PdfDocument entity (follow `UploadPdfCommandHandler` pattern)
4. Publish `PdfReadyForProcessingEvent`
5. Publish `SharedGamePdfUploadedEvent`
6. Update game: `TransitionTo(GameDataStatus.Complete)`
7. Wrap in try/catch — on failure, cleanup blob, set `Enriched`

- [ ] **Step 4: Run tests — verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AutoDownloadPdfCommand.cs
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/AutoDownloadPdfCommandTests.cs
git commit -m "feat(shared-game-catalog): add AutoDownloadPdfCommand with SSRF protection"
```

---

### Task 5.3: Event Handlers

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/PdfReadyForProcessingEventHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/SharedGamePdfUploadedEventHandler.cs`

- [ ] **Step 1: Implement PdfReadyForProcessingEventHandler**

Dispatches `EnqueuePdfCommand` from DocumentProcessing context.

- [ ] **Step 2: Implement SharedGamePdfUploadedEventHandler**

Loads SharedGame, sets `HasUploadedPdf = true`, saves.

- [ ] **Step 3: Write unit tests for both handlers**

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/
git commit -m "feat(shared-game-catalog): add event handlers for PDF processing and upload flag"
```

---

## Chunk 6: Excel Export + BGG Access Restriction

### Task 6.1: Excel Export Command

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ExportGamesToExcelCommand.cs`

- [ ] **Step 1: Write tests** — verify column output, filters, max 10K limit, empty result

- [ ] **Step 2: Implement command**

```csharp
public sealed record ExportGamesToExcelCommand(
    IReadOnlyList<GameDataStatus>? StatusFilter,
    bool? HasPdfFilter) : ICommand<byte[]>;
```

Handler uses ClosedXML to generate `.xlsx` in memory. Query SharedGames with filters, map to columns per spec.

- [ ] **Step 3: Add export endpoint**

```csharp
group.MapGet("/excel-export", async ([AsParameters] ExportFilters filters, HttpContext ctx, IMediator m) =>
{
    var (auth, _, err) = ctx.RequireAdminSession();
    if (!auth) return err!;
    var bytes = await m.Send(new ExportGamesToExcelCommand(filters.Status, filters.HasPdf));
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "catalog-export.xlsx");
});
```

- [ ] **Step 4: Run tests — verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ExportGamesToExcelCommand.cs
git add apps/api/src/Api/Routing/AdminCatalogIngestionEndpoints.cs
git commit -m "feat(shared-game-catalog): add Excel export with filters for admin catalog"
```

---

### Task 6.2: BGG Access Restriction Audit

- [ ] **Step 1: Search for any public BGG search endpoints**

```bash
grep -r "SearchGamesAsync\|BggSearch\|bgg.*search" apps/api/src/Api/Routing/ --include="*.cs"
```

- [ ] **Step 2: If found, protect with AdminOrEditorPolicy or remove**

- [ ] **Step 3: Verify UserLibrary endpoints do NOT call IBggApiService**

```bash
grep -r "IBggApiService\|BggApi" apps/api/src/Api/BoundedContexts/UserLibrary/ --include="*.cs"
```

- [ ] **Step 4: Commit any changes**

```bash
git commit -m "fix(security): restrict BGG search to admin-only endpoints"
```

---

## Chunk 7: Observability

### Task 7.1: Structured Logging + Health Check

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/HealthChecks/SharedGameCatalogHealthCheck.cs`

- [ ] **Step 1: Add EventId constants**

Create static class in SharedGameCatalog with all EventIds from spec (5001-5030).

- [ ] **Step 2: Add logging calls to all handlers** (Excel import, enrichment, PDF download)

- [ ] **Step 3: Extend health check**

```csharp
// Check enrichment queue depth
var queueDepth = await dbContext.BggImportQueue
    .CountAsync(q => q.Status == BggImportStatus.Queued && q.JobType == BggQueueJobType.Enrichment);
if (queueDepth > 500)
    return HealthCheckResult.Degraded($"Enrichment queue depth: {queueDepth}");
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/HealthChecks/
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/
git commit -m "feat(observability): add structured logging and health check for enrichment pipeline"
```

---

## Chunk 8: Frontend

### Task 8.1: API Client + Types

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/lib/catalog-ingestion-api.ts`

- [ ] **Step 1: Define TypeScript types**

```typescript
export interface ExcelImportResult {
  total: number; created: number; duplicates: number; errors: number;
  rowErrors: { rowNumber: number; columnName?: string; errorMessage: string }[];
}

export interface EnqueueResult { enqueued: number; skipped: number; }

export interface CatalogGame {
  sharedGameId: string; name: string; bggId?: number;
  dataStatus: string; gameStatus: string; hasUploadedPdf: boolean;
  // ... enriched fields
}
```

- [ ] **Step 2: Implement API functions + React Query hooks**

```typescript
export function useExcelImport() { return useMutation(...) }
export function useEnrichmentQueue(filters) { return useQuery(...) }
export function useEnqueueEnrichment() { return useMutation(...) }
export function useMarkComplete() { return useMutation(...) }
export function useExcelExport() { /* download blob */ }
```

- [ ] **Step 3: Commit**

---

### Task 8.2: Main Page + Tabs

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/page.tsx`

- [ ] **Step 1: Create page with 3 tabs using shadcn Tabs component**

```tsx
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

export default function CatalogIngestionPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Catalog Ingestion</h1>
      <Tabs defaultValue="import">
        <TabsList>
          <TabsTrigger value="import">Import</TabsTrigger>
          <TabsTrigger value="enrichment">Enrichment Queue</TabsTrigger>
          <TabsTrigger value="export">Export</TabsTrigger>
        </TabsList>
        <TabsContent value="import"><ExcelImportTab /></TabsContent>
        <TabsContent value="enrichment"><EnrichmentQueueTab /></TabsContent>
        <TabsContent value="export"><ExportTab /></TabsContent>
      </Tabs>
    </div>
  );
}
```

- [ ] **Step 2: Commit**

---

### Task 8.3: Excel Import Tab

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/ExcelImportTab.tsx`

- [ ] **Step 1: Implement drag-and-drop upload** (follow `PdfUploadStep.tsx` pattern)
- [ ] **Step 2: Add preview table** (DataTable with Name, BggId, Status columns)
- [ ] **Step 3: Add "Confirm Import" button** → calls `useExcelImport` mutation
- [ ] **Step 4: Show results** (green/yellow/red per row)
- [ ] **Step 5: Commit**

---

### Task 8.4: Enrichment Queue Tab

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/EnrichmentQueueTab.tsx`

- [ ] **Step 1: Implement DataTable with status filter** (dropdown for GameDataStatus)
- [ ] **Step 2: Add multi-select checkbox column**
- [ ] **Step 3: Add action buttons** ("Enrich Selected", "Enqueue All Skeletons", "Mark Complete")
- [ ] **Step 4: Add queue summary bar** (queued/processing/completed/failed counts)
- [ ] **Step 5: Commit**

---

### Task 8.5: Export Tab

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog-ingestion/components/ExportTab.tsx`

- [ ] **Step 1: Implement filter controls** (status multi-select, hasPdf checkbox)
- [ ] **Step 2: Add preview** (first 20 rows in DataTable)
- [ ] **Step 3: Add "Download .xlsx" button** → fetches blob, triggers download
- [ ] **Step 4: Add 10K row warning**
- [ ] **Step 5: Commit**

---

## Chunk 9: Integration Tests + Final Validation

### Task 9.1: Integration Tests

**Files:**
- Create: `tests/Api.Tests/Integration/ExcelImportEndpointTests.cs`
- Create: `tests/Api.Tests/Integration/EnrichmentQueueIntegrationTests.cs`

- [ ] **Step 1: Write Excel import integration test** (real DB via Testcontainers)
- [ ] **Step 2: Write enrichment queue integration test** (atomic claim, stale recovery)
- [ ] **Step 3: Write batch notification integration test**
- [ ] **Step 4: Run all tests**

```bash
cd apps/api/src/Api && dotnet test --filter "Category=Integration"
```

- [ ] **Step 5: Commit**

---

### Task 9.2: Final Validation

- [ ] **Step 1: Run full test suite**

```bash
cd apps/api/src/Api && dotnet test
```

- [ ] **Step 2: Run frontend tests**

```bash
cd apps/web && pnpm test
```

- [ ] **Step 3: Manual smoke test** — upload a small Excel, enqueue 1-2 games, verify enrichment

- [ ] **Step 4: Final commit + PR**

```bash
git push -u origin feature/admin-bulk-excel-import
# Create PR to parent branch (per CLAUDE.md rules)
```

---

## Task Summary

| Chunk | Tasks | Estimated Steps |
|-------|-------|----------------|
| 1. Domain Model | 6 tasks | ~30 steps |
| 2. Infrastructure/Queue | 5 tasks | ~25 steps |
| 3. Excel Import | 3 tasks | ~18 steps |
| 4. BGG Enrichment | 3 tasks | ~21 steps |
| 5. PDF Auto-Download | 3 tasks | ~15 steps |
| 6. Export + BGG Restriction | 2 tasks | ~10 steps |
| 7. Observability | 1 task | ~4 steps |
| 8. Frontend | 5 tasks | ~20 steps |
| 9. Integration + Validation | 2 tasks | ~10 steps |
| **Total** | **30 tasks** | **~153 steps** |

## Dependency Order

```
Chunk 1 (Domain) → Chunk 2 (Infra/Migrations) → Chunk 3 (Excel Import)
                                                → Chunk 4 (BGG Enrichment) → Chunk 5 (PDF Download)
                                                → Chunk 6 (Export)
                                                → Chunk 7 (Observability)
Chunks 1-7 complete → Chunk 8 (Frontend) → Chunk 9 (Integration + Final)
```

Chunks 3, 4, 5, 6, 7 can be parallelized after Chunk 2 completes.
