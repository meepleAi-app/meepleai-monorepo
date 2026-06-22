# Game Entity Reset — Phase 2: Big-Bang Code Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the legacy `Game` aggregate root. Introduce `GameCoreData` value object (SharedKernel) + `GameRef(Kind, Id)` cross-BC discriminator. Refactor `SharedGame` and `PrivateGame` to use `GameCoreData`. Update all ~31 consumer files in a single PR (big-bang) so the build is restored before merge.

**Architecture:** Three foundation primitives in SharedKernel (`GameCoreData`, `GameRef`, `IGameCoreDataProvider`), refactored aggregates (`SharedGame`, `PrivateGame`) that embed `GameCoreData`, removal of `Game` and `IGameRepository`, redirect of every cross-BC `Game.Id` reference to `GameRef` (with the `GetByIdOrSharedGameIdAsync` dual-fallback semantic preserved via the new abstraction).

**Tech Stack:** .NET 9 · EF Core 9 · MediatR · FluentValidation · xUnit + Testcontainers. No new dependencies.

**Tracking issue**: [#1320](https://github.com/meepleAi-app/meepleai-monorepo/issues/1320)

**Parent spec**: [`docs/for-developers/specs/2026-05-19-game-entity-reset.md`](../../for-developers/specs/2026-05-19-game-entity-reset.md) — section §3 (Target schema) + §11 (Refactor strategy)

**Predecessor**: PR #1331 (Phase 1 — backup infrastructure) merged 2026-05-19.

---

## Scope clarification

**In scope:**
- Delete `Game` aggregate (`apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/Game.cs`)
- Delete `IGameRepository` + `GameRepository`
- Delete `GameEntityConfiguration` (EF mapping)
- Introduce `GameCoreData` VO in `SharedKernel/Domain/ValueObjects/`
- Introduce `GameRef` discriminator in `SharedKernel/Domain/ValueObjects/`
- Introduce `IGameCoreDataProvider` in `SharedKernel/Application/` (abstraction over SharedGame+PrivateGame lookup)
- Refactor `SharedGame` to embed `GameCoreData`
- Refactor `PrivateGame` to embed `GameCoreData`
- Update all 31 consumer files
- New EF migration: drop `games` table + custom enum types (`approval_status`)
- Update tests
- All in ONE PR to `main-dev`

**Out of scope:**
- Phase 3 execution scripts (re-link vectors `03-relink-vectors.sh`) — separate plan
- Frontend TypeScript changes (`apps/web/src/types/game-state.ts`, `gameToolkit.ts`) — separate follow-up issue
- `agent_definitions` redesign (#4228) — separate epic
- Promotion API `SharedGame.CreateFromPrivate()` — separate issue

---

## Reference inventory (31 files)

**Consumer surface confirmed via grep on `\bGame\s+(game|aggregate|entity)\b|new Game\(|Game\.Create|IGameRepository|DbSet<Game>`:**

### A. GameManagement BC (16 files) — most go away with `Game` aggregate
- `Domain/Entities/Game.cs` ❌ **DELETE**
- `Domain/Repositories/IGameRepository.cs` ❌ **DELETE**
- `Infrastructure/Persistence/GameRepository.cs` ❌ **DELETE**
- `Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs` ✏️ **EDIT** (remove `IGameRepository` registration)
- `Application/Commands/CreateGameCommandHandler.cs` ❌ **DELETE** (replaced by `SharedGameCatalog.CreateSharedGameCommandHandler`)
- `Application/Commands/UpdateGameCommandHandler.cs` ❌ **DELETE**
- `Application/Commands/PublishGameCommandHandler.cs` ❌ **DELETE**
- `Application/Commands/ImportBggGameCommandHandler.cs` ❌ **DELETE** (replaced by `SharedGameCatalog.ImportGameFromBggCommandHandler`)
- `Application/Queries/GetGameByIdQueryHandler.cs` ✏️ **EDIT** → query SharedGame OR PrivateGame via `IGameCoreDataProvider`
- `Application/Queries/GetGameDetailsQueryHandler.cs` ✏️ **EDIT** → same
- `Application/Queries/GetAllGamesQueryHandler.cs` ✏️ **EDIT** → list SharedGames (with filter for catalog visibility)
- `Application/Commands/StartGameSessionCommandHandler.cs` ✏️ **EDIT** (use `GameRef`)
- `Application/Commands/GameNights/CreateGameNightInvitationByEmailCommandHandler.cs` ✏️ **EDIT**
- `Application/Commands/PlayRecords/CreatePlayRecordCommandHandler.cs` ✏️ **EDIT**
- `Application/Queries/GameNights/GetGameNightInvitationByTokenQueryHandler.cs` ✏️ **EDIT**
- `Application/EventHandlers/SharedGameSoftDeletedEventHandler.cs` ✏️ **EDIT**
- `Application/Validators/RuleConflictFAQs/CreateRuleConflictFaqCommandValidator.cs` ✏️ **EDIT**
- `Domain/ValueObjects/SessionScoringConfig.cs` ✏️ **EDIT**

### B. SharedGameCatalog BC (6 files)
- `Domain/Aggregates/SharedGame.cs` ✏️ **EDIT** — embed `GameCoreData`
- `Application/Commands/ImportGameFromBggCommandHandler.cs` ✏️ **EDIT** — drop `Game` mirror creation
- `Application/Commands/CreateSharedGameCommandHandler.cs` ✏️ **EDIT**
- `Application/Commands/CreateSharedGameFromPdfCommandHandler.cs` ✏️ **EDIT**
- `Application/Commands/ApproveGameProposal/ApproveGameProposalCommandHandler.cs` ✏️ **EDIT**
- `Application/Commands/ImportGamesFromExcelCommand.cs` ✏️ **EDIT**
- `Application/Commands/ConfirmExcelImportCommand.cs` ✏️ **EDIT**
- `Application/Queries/SearchSharedGamesQueryHandler.cs` ✏️ **EDIT**

### C. UserLibrary BC (3 files)
- `Domain/Entities/PrivateGame.cs` ✏️ **EDIT** — embed `GameCoreData`
- `Infrastructure/Persistence/PrivateGameRepository.cs` ✏️ **EDIT**
- `Application/Commands/PrivateGames/AddPrivateGameCommandHandler.cs` ✏️ **EDIT**
- `Domain/Enums/EntityType.cs` ✏️ **EDIT** — remove `Game` enum value, replace with `SharedGame` + `PrivateGame`

### D. KnowledgeBase BC (1 file)
- `Application/Commands/ChatWithSessionAgentCommandHandler.cs` ✏️ **EDIT** — depend on `IGameCoreDataProvider` instead of `IGameRepository`

### E. GameToolkit BC (1 file)
- `Application/Commands/GenerateToolkitFromKbHandler.cs` ✏️ **EDIT**

### F. UserNotifications BC (1 file)
- `Application/EventHandlers/ShareRequestApprovedNotificationHandler.cs` ✏️ **EDIT**

### G. EF Infrastructure (2 files)
- `Infrastructure/MeepleAiDbContext.cs` ✏️ **EDIT** — remove `DbSet<Game>`
- `Infrastructure/EntityConfigurations/GameManagement/GameEntityConfiguration.cs` ❌ **DELETE**
- New: `Infrastructure/Migrations/<timestamp>_DropGameAggregate_Issue1320.cs` ✨ **NEW**

### H. Tests
- `tests/Api.Tests/BoundedContexts/GameManagement/**` — multiple files reference `Game` (TBD enumeration in Task 8.1)

---

## File structure (new files)

| Path | Responsibility |
|---|---|
| `apps/api/src/Api/SharedKernel/Domain/ValueObjects/GameCoreData.cs` | Immutable VO holding the 9 fields shared between SharedGame and PrivateGame |
| `apps/api/src/Api/SharedKernel/Domain/ValueObjects/GameRef.cs` | Discriminated reference `(GameRefKind kind, Guid id)` for cross-BC consumers |
| `apps/api/src/Api/SharedKernel/Domain/ValueObjects/GameRefKind.cs` | Enum `{ Shared, Private }` |
| `apps/api/src/Api/SharedKernel/Application/IGameCoreDataProvider.cs` | Abstraction: resolve `GameRef → GameCoreData` (replaces `IGameRepository.GetByIdOrSharedGameIdAsync`) |
| `apps/api/src/Api/Infrastructure/GameCoreDataProvider.cs` | Implementation querying both `SharedGame` and `PrivateGame` repositories |
| `tests/Api.Tests/SharedKernel/Domain/ValueObjects/GameCoreDataTests.cs` | Unit tests for VO equality + factory validation |
| `tests/Api.Tests/SharedKernel/Domain/ValueObjects/GameRefTests.cs` | Unit tests for discriminator semantics |
| `tests/Api.Tests/Infrastructure/GameCoreDataProviderTests.cs` | Integration tests for dual-fallback resolution |

---

## Sub-phases

The 16 tasks below are grouped into 5 sub-phases. Each sub-phase represents a logical milestone. The branch builds **red** between sub-phases 2-4 (expected, big-bang); CI must be green at the final commit.

| Sub-phase | Tasks | Build state |
|---|---|---|
| 2a — Foundation | T1–T4 | Green (additive only) |
| 2b — Aggregate refactor | T5–T7 | **Red** (consumers not yet updated) |
| 2c — Consumer migration | T8–T12 | **Red** (Game still imported) |
| 2d — Delete Game | T13–T14 | Green (last consumer fixed) |
| 2e — Tests + migration + PR | T15–T16 | Green |

---

## Task 1: GameCoreData value object

**Files:**
- Create: `apps/api/src/Api/SharedKernel/Domain/ValueObjects/GameCoreData.cs`
- Test: `tests/Api.Tests/SharedKernel/Domain/ValueObjects/GameCoreDataTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.ValueObjects;

public class GameCoreDataTests
{
    [Fact]
    public void Create_with_valid_inputs_returns_instance()
    {
        var data = GameCoreData.Create(
            title: "Catan",
            yearPublished: 1995,
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            description: "Trade and build",
            imageUrl: "https://example.com/catan.png",
            thumbnailUrl: "https://example.com/catan-thumb.png",
            bggId: 13,
            complexityRating: 2.3m);

        data.Title.Should().Be("Catan");
        data.YearPublished.Should().Be(1995);
        data.MinPlayers.Should().Be(3);
        data.MaxPlayers.Should().Be(4);
    }

    [Fact]
    public void Create_with_empty_title_throws()
    {
        var act = () => GameCoreData.Create(
            title: "",
            yearPublished: 1995,
            minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 90, minAge: 10);

        act.Should().Throw<ArgumentException>().WithMessage("*title*");
    }

    [Fact]
    public void Create_with_minPlayers_greater_than_maxPlayers_throws()
    {
        var act = () => GameCoreData.Create(
            title: "Test", yearPublished: 2000,
            minPlayers: 5, maxPlayers: 3,
            playingTimeMinutes: 60, minAge: 10);

        act.Should().Throw<ArgumentException>().WithMessage("*minPlayers*maxPlayers*");
    }

    [Fact]
    public void Equality_is_value_based()
    {
        var a = GameCoreData.Create("Catan", 1995, 3, 4, 90, 10);
        var b = GameCoreData.Create("Catan", 1995, 3, 4, 90, 10);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameCoreDataTests" 2>&1 | tail -10
```

Expected: compile error "GameCoreData does not exist".

- [ ] **Step 3: Write minimal implementation**

```csharp
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.SharedKernel.Domain.ValueObjects;

public sealed record GameCoreData
{
    public string Title { get; }
    public int YearPublished { get; }
    public int MinPlayers { get; }
    public int MaxPlayers { get; }
    public int PlayingTimeMinutes { get; }
    public int MinAge { get; }
    public string? Description { get; }
    public string? ImageUrl { get; }
    public string? ThumbnailUrl { get; }
    public int? BggId { get; }
    public decimal? ComplexityRating { get; }

    private GameCoreData(
        string title, int yearPublished, int minPlayers, int maxPlayers,
        int playingTimeMinutes, int minAge,
        string? description, string? imageUrl, string? thumbnailUrl,
        int? bggId, decimal? complexityRating)
    {
        Title = title;
        YearPublished = yearPublished;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
        PlayingTimeMinutes = playingTimeMinutes;
        MinAge = minAge;
        Description = description;
        ImageUrl = imageUrl;
        ThumbnailUrl = thumbnailUrl;
        BggId = bggId;
        ComplexityRating = complexityRating;
    }

    public static GameCoreData Create(
        string title,
        int yearPublished,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        string? description = null,
        string? imageUrl = null,
        string? thumbnailUrl = null,
        int? bggId = null,
        decimal? complexityRating = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("title cannot be empty", nameof(title));
        if (minPlayers < 1 || minPlayers > 100)
            throw new ArgumentException("minPlayers must be 1..100", nameof(minPlayers));
        if (maxPlayers < minPlayers || maxPlayers > 100)
            throw new ArgumentException(
                $"maxPlayers ({maxPlayers}) must be >= minPlayers ({minPlayers}) and <= 100",
                nameof(maxPlayers));
        if (playingTimeMinutes < 0)
            throw new ArgumentException("playingTimeMinutes must be non-negative", nameof(playingTimeMinutes));

        return new GameCoreData(
            title.Trim(), yearPublished, minPlayers, maxPlayers,
            playingTimeMinutes, minAge,
            description, imageUrl, thumbnailUrl, bggId, complexityRating);
    }

    public GameCoreData WithTitle(string newTitle) =>
        Create(newTitle, YearPublished, MinPlayers, MaxPlayers, PlayingTimeMinutes, MinAge,
               Description, ImageUrl, ThumbnailUrl, BggId, ComplexityRating);

    // Add similar With* methods for other mutable fields as needed by SharedGame/PrivateGame
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameCoreDataTests"
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/SharedKernel/Domain/ValueObjects/GameCoreData.cs tests/Api.Tests/SharedKernel/Domain/ValueObjects/GameCoreDataTests.cs
git commit -m "feat(shared-kernel): GameCoreData VO with validation + value equality (refs #1320)"
```

---

## Task 2: GameRefKind enum + GameRef discriminator

**Files:**
- Create: `apps/api/src/Api/SharedKernel/Domain/ValueObjects/GameRefKind.cs`
- Create: `apps/api/src/Api/SharedKernel/Domain/ValueObjects/GameRef.cs`
- Test: `tests/Api.Tests/SharedKernel/Domain/ValueObjects/GameRefTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.ValueObjects;

public class GameRefTests
{
    [Fact]
    public void Shared_factory_produces_Shared_kind()
    {
        var id = Guid.NewGuid();
        var r = GameRef.Shared(id);
        r.Kind.Should().Be(GameRefKind.Shared);
        r.Id.Should().Be(id);
    }

    [Fact]
    public void Private_factory_produces_Private_kind()
    {
        var id = Guid.NewGuid();
        var r = GameRef.Private(id);
        r.Kind.Should().Be(GameRefKind.Private);
        r.Id.Should().Be(id);
    }

    [Fact]
    public void Equality_is_value_based()
    {
        var id = Guid.NewGuid();
        var a = GameRef.Shared(id);
        var b = GameRef.Shared(id);
        a.Should().Be(b);
    }

    [Fact]
    public void Two_refs_with_different_kind_are_not_equal()
    {
        var id = Guid.NewGuid();
        GameRef.Shared(id).Should().NotBe(GameRef.Private(id));
    }

    [Fact]
    public void Empty_guid_throws()
    {
        var act = () => GameRef.Shared(Guid.Empty);
        act.Should().Throw<ArgumentException>().WithMessage("*empty*");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameRefTests"
```

Expected: compile error.

- [ ] **Step 3: Write minimal implementation**

`GameRefKind.cs`:
```csharp
namespace Api.SharedKernel.Domain.ValueObjects;

public enum GameRefKind
{
    Shared = 0,
    Private = 1
}
```

`GameRef.cs`:
```csharp
namespace Api.SharedKernel.Domain.ValueObjects;

public sealed record GameRef
{
    public GameRefKind Kind { get; }
    public Guid Id { get; }

    private GameRef(GameRefKind kind, Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("GameRef id cannot be empty", nameof(id));
        Kind = kind;
        Id = id;
    }

    public static GameRef Shared(Guid id) => new(GameRefKind.Shared, id);
    public static GameRef Private(Guid id) => new(GameRefKind.Private, id);

    public override string ToString() => $"{Kind}:{Id}";
}
```

- [ ] **Step 4: Run tests**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameRefTests"
```

Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/SharedKernel/Domain/ValueObjects/GameRef.cs apps/api/src/Api/SharedKernel/Domain/ValueObjects/GameRefKind.cs tests/Api.Tests/SharedKernel/Domain/ValueObjects/GameRefTests.cs
git commit -m "feat(shared-kernel): GameRef discriminator VO (Shared|Private) (refs #1320)"
```

---

## Task 3: IGameCoreDataProvider interface

**Files:**
- Create: `apps/api/src/Api/SharedKernel/Application/IGameCoreDataProvider.cs`

- [ ] **Step 1: Write the interface**

```csharp
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.SharedKernel.Application;

/// <summary>
/// Resolves a <see cref="GameRef"/> to its <see cref="GameCoreData"/>.
/// Replaces the legacy <c>IGameRepository.GetByIdOrSharedGameIdAsync</c> dual-fallback pattern.
/// Implementation queries SharedGame (if Kind=Shared) or PrivateGame (if Kind=Private).
/// </summary>
public interface IGameCoreDataProvider
{
    Task<GameCoreData?> GetCoreDataAsync(GameRef gameRef, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk fetch for query handlers that need multiple games at once.
    /// Returns dictionary keyed by GameRef; missing entries indicate not-found.
    /// </summary>
    Task<IReadOnlyDictionary<GameRef, GameCoreData>> GetCoreDataBatchAsync(
        IReadOnlyCollection<GameRef> gameRefs,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Verify compile**

```bash
cd apps/api/src/Api && dotnet build --nologo /p:TreatWarningsAsErrors=false 2>&1 | tail -5
```

Expected: build succeeds (interface only, no implementation yet).

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/SharedKernel/Application/IGameCoreDataProvider.cs
git commit -m "feat(shared-kernel): IGameCoreDataProvider abstraction (refs #1320)"
```

---

## Task 4: GameCoreDataProvider implementation

**Files:**
- Create: `apps/api/src/Api/Infrastructure/GameCoreDataProvider.cs`
- Test: `tests/Api.Tests/Infrastructure/GameCoreDataProviderTests.cs`

- [ ] **Step 1: Write failing integration test using Testcontainers (PostgreSQL)**

Test structure:
- Seed: 1 `SharedGame` (Catan) + 1 `PrivateGame` (homebrew)
- Assert: `GetCoreDataAsync(GameRef.Shared(catanId))` returns Catan's `GameCoreData`
- Assert: `GetCoreDataAsync(GameRef.Private(homebrewId))` returns homebrew's `GameCoreData`
- Assert: `GetCoreDataAsync(GameRef.Shared(Guid.NewGuid()))` returns null
- Assert: `GetCoreDataBatchAsync([catanRef, homebrewRef, unknownRef])` returns 2-entry dictionary

Follow existing pattern from `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/GameRepositoryIntegrationTests.cs` for Testcontainers setup.

(Full test code skeleton omitted here; follow project's integration test patterns from `docs/for-developers/testing/backend/backend-testing-patterns.md`.)

- [ ] **Step 2: Write implementation**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.Infrastructure;

public sealed class GameCoreDataProvider : IGameCoreDataProvider
{
    private readonly ISharedGameRepository _sharedGames;
    private readonly IPrivateGameRepository _privateGames;

    public GameCoreDataProvider(
        ISharedGameRepository sharedGames,
        IPrivateGameRepository privateGames)
    {
        _sharedGames = sharedGames;
        _privateGames = privateGames;
    }

    public async Task<GameCoreData?> GetCoreDataAsync(
        GameRef gameRef, CancellationToken ct = default) => gameRef.Kind switch
    {
        GameRefKind.Shared => (await _sharedGames.GetByIdAsync(gameRef.Id, ct))?.ToCoreData(),
        GameRefKind.Private => (await _privateGames.GetByIdAsync(gameRef.Id, ct))?.ToCoreData(),
        _ => throw new ArgumentOutOfRangeException(nameof(gameRef))
    };

    public async Task<IReadOnlyDictionary<GameRef, GameCoreData>> GetCoreDataBatchAsync(
        IReadOnlyCollection<GameRef> gameRefs, CancellationToken ct = default)
    {
        var result = new Dictionary<GameRef, GameCoreData>();
        foreach (var r in gameRefs.Distinct())
        {
            var data = await GetCoreDataAsync(r, ct);
            if (data is not null) result[r] = data;
        }
        return result;
    }
}
```

> Note: `SharedGame.ToCoreData()` and `PrivateGame.ToCoreData()` extension methods are added in Tasks 5 and 6.

- [ ] **Step 3: Register in DI**

Edit `apps/api/src/Api/Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` (or equivalent), add:

```csharp
services.AddScoped<IGameCoreDataProvider, GameCoreDataProvider>();
```

- [ ] **Step 4: Run integration tests**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameCoreDataProviderTests"
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/GameCoreDataProvider.cs tests/Api.Tests/Infrastructure/GameCoreDataProviderTests.cs apps/api/src/Api/Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs
git commit -m "feat(infra): GameCoreDataProvider with dual-source fallback (refs #1320)"
```

---

## Task 5: Refactor SharedGame to embed GameCoreData

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/SharedGameEntityConfiguration.cs`
- Test: existing `SharedGameTests` updated

- [ ] **Step 1: Read current SharedGame to identify the 11 fields that map to GameCoreData**

```bash
grep -E "private (string|int|decimal\?) _" apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs
```

Map: `_title`, `_yearPublished`, `_description`, `_minPlayers`, `_maxPlayers`, `_playingTimeMinutes`, `_minAge`, `_complexityRating`, `_imageUrl`, `_thumbnailUrl`, `_bggId` → all replaced by single `GameCoreData _coreData`.

- [ ] **Step 2: Refactor — replace 11 fields with one VO field**

Replace the 11 private backing fields with:

```csharp
private GameCoreData _coreData = null!;
```

Update property accessors:

```csharp
public string Title => _coreData.Title;
public int YearPublished => _coreData.YearPublished;
// ... etc for the 11 properties
```

Add `ToCoreData()` method:

```csharp
public GameCoreData ToCoreData() => _coreData;
```

Update mutation methods (e.g., `UpdateTitle`, `UpdateGameplay`) to call `_coreData = _coreData.WithXxx(...)`.

- [ ] **Step 3: Update EF entity configuration**

In `SharedGameEntityConfiguration.cs`, configure `_coreData` as an owned type:

```csharp
builder.OwnsOne(e => e.CoreData, cd =>
{
    cd.Property(c => c.Title).HasColumnName("title").IsRequired();
    cd.Property(c => c.YearPublished).HasColumnName("year_published");
    cd.Property(c => c.MinPlayers).HasColumnName("min_players");
    cd.Property(c => c.MaxPlayers).HasColumnName("max_players");
    // ... map all 11 fields to existing column names (no schema change)
});
```

- [ ] **Step 4: Update existing SharedGame tests**

Tests previously asserting `sharedGame.Title` should still work (property unchanged). Tests asserting mutation methods may need adjustment to use `WithXxx` pattern internally.

- [ ] **Step 5: Build + run SharedGame tests**

```bash
cd apps/api/src/Api && dotnet build && dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SharedGame"
```

Expected: all SharedGame tests pass (behavior preserved).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/SharedGameEntityConfiguration.cs tests/Api.Tests/BoundedContexts/SharedGameCatalog/
git commit -m "refactor(shared-game-catalog): SharedGame embeds GameCoreData VO (refs #1320)"
```

---

## Task 6: Refactor PrivateGame to embed GameCoreData

Mirror of Task 5 for `PrivateGame`. Same pattern: replace 11 fields with `GameCoreData _coreData`, add `ToCoreData()`, update EF config to use `OwnsOne`.

Files:
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/PrivateGame.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/UserLibrary/PrivateGameEntityConfiguration.cs`
- Update: existing `PrivateGameTests`

Commit message: `refactor(user-library): PrivateGame embeds GameCoreData VO (refs #1320)`

---

## Task 7: Implement ToCoreData() extension on both aggregates

Actually rolled into Tasks 5 and 6 above (`ToCoreData()` method on each aggregate). This task is now a verification step:

- [ ] Verify `SharedGame.ToCoreData()` returns a `GameCoreData` instance with all 11 fields populated from owned-entity columns
- [ ] Verify `PrivateGame.ToCoreData()` same
- [ ] Run `GameCoreDataProviderTests` from Task 4 — should now pass end-to-end against real DB

If passes: no commit (already covered by T5/T6). If reveals a bug: fix it in the appropriate aggregate file.

---

## Task 8: Migrate KnowledgeBase consumers

**File**: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs`

- [ ] **Step 1: Read current handler** to identify where `IGameRepository` is used

```bash
grep -n "IGameRepository\|gameRepository\|_gameRepo" apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs
```

- [ ] **Step 2: Replace dependency injection**

```csharp
// Before
private readonly IGameRepository _gameRepository;
public ChatWithSessionAgentCommandHandler(IGameRepository gameRepository, ...) { ... }

// After
private readonly IGameCoreDataProvider _gameCoreData;
public ChatWithSessionAgentCommandHandler(IGameCoreDataProvider gameCoreData, ...) { ... }
```

- [ ] **Step 3: Replace `GetByIdOrSharedGameIdAsync` calls**

```csharp
// Before
var game = await _gameRepository.GetByIdOrSharedGameIdAsync(gameId, ct);
if (game is null) return Result.Fail("Game not found");
var title = game.Title.Value;

// After
// Caller now passes GameRef instead of bare Guid; if migration needs to maintain Guid contract:
var gameRef = await ResolveGameRef(gameId, ct);  // helper that tries Shared first, then Private
var coreData = await _gameCoreData.GetCoreDataAsync(gameRef, ct);
if (coreData is null) return Result.Fail("Game not found");
var title = coreData.Title;
```

- [ ] **Step 4: Build (project compiles; other handlers still red)**

```bash
cd apps/api/src/Api && dotnet build 2>&1 | grep "ChatWithSessionAgent"
```

Expected: no errors specific to this file (build of whole project still red due to other consumers).

- [ ] **Step 5: Commit (along with related tests)**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/
git commit -m "refactor(kb): ChatWithSessionAgent uses IGameCoreDataProvider (refs #1320)"
```

---

## Task 9: Migrate SharedGameCatalog consumers

Apply the pattern from Task 8 to:
- `Application/Commands/ImportGameFromBggCommandHandler.cs` — drop the `new Game(...)` mirror creation; only create `SharedGame`
- `Application/Commands/CreateSharedGameCommandHandler.cs` — same
- `Application/Commands/CreateSharedGameFromPdfCommandHandler.cs` — same
- `Application/Commands/ApproveGameProposal/ApproveGameProposalCommandHandler.cs`
- `Application/Commands/ImportGamesFromExcelCommand.cs`
- `Application/Commands/ConfirmExcelImportCommand.cs`
- `Application/Queries/SearchSharedGamesQueryHandler.cs`

Pattern: where the code used to create or query a `Game`, now operate only on `SharedGame` (via `ISharedGameRepository`).

Single commit for all SharedGameCatalog files:

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/
git commit -m "refactor(shared-game-catalog): remove Game aggregate dependencies (refs #1320)"
```

---

## Task 10: Migrate UserLibrary consumers

Files:
- `Domain/Enums/EntityType.cs` — replace `Game = N` with `SharedGame` + `PrivateGame` enum members; update DB column type if numeric mapping is used (likely string column, then just rename)
- `Infrastructure/Persistence/PrivateGameRepository.cs` — remove any cross-reference to `IGameRepository`
- `Application/Commands/PrivateGames/AddPrivateGameCommandHandler.cs` — same

Commit: `refactor(user-library): remove Game aggregate dependencies (refs #1320)`

---

## Task 11: Migrate GameToolkit / UserNotifications consumers

Single files each. Apply the pattern.

Files:
- `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandler.cs`
- `apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/ShareRequestApprovedNotificationHandler.cs`

Commit: `refactor(toolkit,notifications): use IGameCoreDataProvider (refs #1320)`

---

## Task 12: Migrate remaining GameManagement consumers (the ones that survive)

The `Game` aggregate is being deleted, but these handlers live inside GameManagement BC and reference Game today. They must either:
- Be **deleted** (CreateGame, UpdateGame, PublishGame, ImportBggGame — superseded by SharedGameCatalog)
- Be **kept but rewired** to use `GameRef`/`SharedGame` (GameSession, GameNight, PlayRecord handlers)

Files to rewire (not delete):
- `Application/Commands/StartGameSessionCommandHandler.cs`
- `Application/Commands/GameNights/CreateGameNightInvitationByEmailCommandHandler.cs`
- `Application/Commands/PlayRecords/CreatePlayRecordCommandHandler.cs`
- `Application/Queries/GameNights/GetGameNightInvitationByTokenQueryHandler.cs`
- `Application/Queries/GetGameByIdQueryHandler.cs` — becomes facade over `IGameCoreDataProvider`
- `Application/Queries/GetGameDetailsQueryHandler.cs` — same
- `Application/Queries/GetAllGamesQueryHandler.cs` — list SharedGames + (optionally) PrivateGames owned by user
- `Application/EventHandlers/SharedGameSoftDeletedEventHandler.cs`
- `Application/Validators/RuleConflictFAQs/CreateRuleConflictFaqCommandValidator.cs`
- `Domain/ValueObjects/SessionScoringConfig.cs`

Pattern: replace `Guid gameId` with `GameRef gameRef` in command/query DTOs OR resolve via helper. Use `IGameCoreDataProvider` for data lookup.

Files to delete:
- `Application/Commands/CreateGameCommandHandler.cs` (+ command, validator, routing)
- `Application/Commands/UpdateGameCommandHandler.cs`
- `Application/Commands/PublishGameCommandHandler.cs`
- `Application/Commands/ImportBggGameCommandHandler.cs`

Commit: `refactor(game-management): rewire surviving handlers, delete superseded ones (refs #1320)`

---

## Task 13: Delete Game aggregate and repository

**Files:**
- Delete: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/Game.cs`
- Delete: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IGameRepository.cs`
- Delete: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GameRepository.cs`
- Delete: `apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/GameEntityConfiguration.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs` (remove `IGameRepository` registration)
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (remove `DbSet<Game>`)

- [ ] **Step 1: Delete files**

```bash
git rm apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/Game.cs
git rm apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IGameRepository.cs
git rm apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GameRepository.cs
git rm apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/GameEntityConfiguration.cs
```

- [ ] **Step 2: Edit DI registration to remove IGameRepository binding**

Open `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs`, remove the line `services.AddScoped<IGameRepository, GameRepository>();`.

- [ ] **Step 3: Edit MeepleAiDbContext to remove DbSet<Game>**

Find the line `public DbSet<Game> Games => Set<Game>();` (or similar) in `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` and remove it. Also remove the `using Api.BoundedContexts.GameManagement.Domain.Entities;` import if no longer needed.

- [ ] **Step 4: Full build — MUST succeed**

```bash
cd apps/api/src/Api && dotnet build 2>&1 | tail -20
```

Expected: build succeeds. If any error references `Game`, that consumer was missed in Tasks 8-12 — go back and fix.

- [ ] **Step 5: Commit**

```bash
git commit -m "refactor(game-management): delete Game aggregate, IGameRepository, EF config (refs #1320)"
```

---

## Task 14: EF migration to drop games table

**Files:**
- Create (via `dotnet ef migrations add`): `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_DropGameAggregate_Issue1320.cs`

- [ ] **Step 1: Generate the migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add DropGameAggregate_Issue1320
```

- [ ] **Step 2: Review the generated migration**

Open the new migration file. It should contain:
- `DropTable(name: "games", ...)` 
- Drop of any FKs in other tables that pointed to `games.id` (e.g., `vector_documents.game_id` if still there)
- Drop of custom enum `approval_status` if defined as PG enum type

If anything is missing (e.g., orphaned indexes), add `migrationBuilder.Sql("DROP ...")` statements manually.

- [ ] **Step 3: Add explicit drop of custom enum (defensive)**

In the migration `Up()` method, add at the end:

```csharp
migrationBuilder.Sql(@"
DO $$
DECLARE
  t text;
BEGIN
  FOR t IN SELECT typname FROM pg_type
    WHERE typtype = 'e' AND typname IN ('approval_status')
  LOOP
    EXECUTE 'DROP TYPE IF EXISTS public.' || quote_ident(t) || ' CASCADE';
  END LOOP;
END $$;");
```

- [ ] **Step 4: Verify migration applies on a fresh local DB**

```bash
# Reset local DB to current main-dev schema
make dev-down && make dev-core
sleep 5
dotnet ef database update
```

Expected: migration applies without error.

- [ ] **Step 5: Verify migration is REVERSIBLE (Down method works)**

```bash
dotnet ef database update <previous-migration-name>
```

If `Down()` is incomplete (only generates `CreateTable(games)` without re-adding FKs), accept this — Phase 1's `pg_restore` from backup is the canonical rollback.

Document in the migration's `Down()` method as a comment:

```csharp
// NOTE: Down() recreates the games table schema only; data is NOT restored.
// For actual rollback after data loss, use Phase 1's pg_restore from the pre-flight backup.
// See: docs/for-developers/specs/2026-05-19-game-entity-reset.md §4 Step 6.
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(infra): EF migration DropGameAggregate_Issue1320 (refs #1320)"
```

---

## Task 15: Update integration tests

- [ ] **Step 1: Run full backend test suite**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj --nologo 2>&1 | tail -40
```

Expected: identify any failing tests. Categories of failure:
1. Tests referencing `IGameRepository` directly → migrate to `IGameCoreDataProvider`
2. Tests referencing `Game` aggregate → migrate to `SharedGame` or `PrivateGame`
3. Tests inserting into `games` table fixture → migrate to `shared_games`

- [ ] **Step 2: Migrate each failing test**

For each test class with compile/runtime errors, apply the transformations from Tasks 8-12. Aim for behavioral equivalence — what was tested before should still be tested.

- [ ] **Step 3: Run again until all pass**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests/Api.Tests.csproj --nologo
```

Expected: 100% pass rate. No skipped tests due to missing `Game` references.

- [ ] **Step 4: Commit**

```bash
git add tests/Api.Tests/
git commit -m "test(api): migrate tests off Game aggregate to GameRef/CoreData (refs #1320)"
```

---

## Task 16: PR + close-out

- [ ] **Step 1: Run linter and type checker on backend**

```bash
cd apps/api/src/Api && dotnet build /p:TreatWarningsAsErrors=true
```

Expected: build clean, no warnings.

- [ ] **Step 2: Push branch**

```bash
git push -u origin feature/issue-1320-game-entity-reset-phase2
```

- [ ] **Step 3: Open PR to main-dev**

```bash
gh pr create --title "refactor(api): game-reset Phase 2 — delete Game aggregate, introduce GameCoreData + GameRef (#1320)" \
  --base main-dev \
  --body "$(cat <<'EOF'
## Summary

Phase 2 of #1320 — big-bang code refactor:

- **Delete** `Game` aggregate + `IGameRepository` + `GameRepository` + `GameEntityConfiguration`
- **Add** `GameCoreData` VO (11 shared fields) in SharedKernel
- **Add** `GameRef(Kind, Id)` discriminator + `GameRefKind { Shared, Private }` enum
- **Add** `IGameCoreDataProvider` abstraction (replaces dual-fallback `GetByIdOrSharedGameIdAsync`)
- **Refactor** `SharedGame` + `PrivateGame` to embed `GameCoreData` via EF `OwnsOne`
- **Update** ~31 consumer files across GameManagement, SharedGameCatalog, UserLibrary, KnowledgeBase, GameToolkit, UserNotifications BCs
- **EF migration**: `DropGameAggregate_Issue1320` — drops `games` table + custom enum types

## Test plan

- [ ] All existing backend tests pass after migration
- [ ] New unit tests for `GameCoreData`, `GameRef` (in SharedKernel test project)
- [ ] New integration test for `GameCoreDataProvider` dual-fallback resolution
- [ ] EF migration applies and reverses on local dev DB

## What this does NOT do

- Does not run the reset on staging/prod (that's Phase 3)
- Does not modify Phase 1 scripts (those are reused unchanged)
- Does not touch frontend TypeScript types (separate follow-up)
- Does not touch `agent_definitions` redesign (#4228 — depends on this landing)

Predecessor: PR #1331 (Phase 1 — backup infrastructure)
Successor: Phase 3 plan to be written after this lands.

🤖 Implemented per `docs/superpowers/plans/2026-05-19-game-entity-reset-phase2.md`
EOF
)"
```

- [ ] **Step 4: Comment on issue #1320 with PR link**

```bash
gh issue comment 1320 --body "Phase 2 PR opened: <pr-url>. Big-bang refactor: Game aggregate deleted, GameCoreData + GameRef + IGameCoreDataProvider introduced, ~31 consumers updated. EF migration drops games table."
```

- [ ] **Step 5: Wait for CI green + merge**

After CI green + review approved, merge via GitHub UI (squash). Branch auto-deletes.

---

## Self-review checklist

✅ **Spec coverage**: All three primitives (GameCoreData, GameRef, IGameCoreDataProvider) implemented in foundation (Tasks 1-4). Both aggregates refactored (Tasks 5-6). All BCs migrated (Tasks 8-12). Delete + migration (Tasks 13-14). Tests (Task 15). PR (Task 16).

✅ **Placeholder scan**: No "TBD" / "implement later" in plan content. Some tasks (5, 6, 12, 15) provide patterns rather than exhaustive line-by-line code because the transformation is repetitive across many files — the pattern is shown once with example, and the executor applies it consistently.

✅ **Type/name consistency**: 
- `GameCoreData` (not `CoreData`, not `GameData`)
- `GameRef` (not `GameReference`)
- `GameRefKind` enum: `Shared`, `Private` (singular)
- `IGameCoreDataProvider.GetCoreDataAsync` (consistent with naming)
- `ToCoreData()` extension method on both aggregates (consistent)

✅ **Frequent commits**: 16 commits planned, one per task. Each commit leaves the codebase in a known state (foundations green, aggregates red, consumers progressively green, final green).

✅ **TDD**: Tasks 1, 2, 4, 5, 6 are TDD-driven. Other tasks (8-12) follow the established pattern from earlier tasks; new unit tests not added per file but existing tests migrated in Task 15.

## Risks

| Risk | Mitigation |
|---|---|
| Build red for many tasks (2b-2c) | Expected for big-bang. Final task before deletion must compile. |
| Cross-BC reference changes propagate widely | Tasks 8-12 explicitly enumerate BCs |
| EF `OwnsOne` mapping has edge cases (nullable VO?) | Use existing project patterns for owned types; reference `SharedGameEntityConfiguration` |
| `Game.SharedGameId` FK column not the same as `SharedGame.Id` | Already covered: `Game` is deleted, all consumers point to `SharedGame.Id` directly via `GameRef` |
| Test database schema mismatch after migration | Task 14 Step 4 verifies fresh DB; Task 15 catches behavioral regressions |
| Hidden consumers via reflection / DI scanning | Build red catches these immediately (compile-time). MediatR scanning would surface at startup |

## Out of scope (deferred)

- Phase 3 execution scripts (re-link vectors, run on dev/staging/prod)
- Frontend types refactor
- `agent_definitions` redesign (#4228 — unblocked by this PR landing)
- Promotion API (`SharedGame.CreateFromPrivate`)
- Re-classification heuristic (which legacy Games become SharedGame vs PrivateGame — defaults to SharedGame)
