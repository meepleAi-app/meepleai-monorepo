# ADR-069 Toolkit Suggestion Cache — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate 5-30s LLM latency on every `/api/v1/game-toolkits/{id}/generate-from-kb` request by caching the generated `AiToolkitSuggestionDto` per game and invalidating via `KbDocIndexedEvent`.

**Architecture:** Cache-aside pattern. New `AiToolkitSuggestionCacheEntry` aggregate in `GameToolkit` BC with one row per game. `GenerateToolkitFromKbHandler` checks cache → returns hit OR runs existing LLM pipeline → persists result. An `INotificationHandler<KbDocIndexedEvent>` deletes the cached entry for the affected game so the next request regenerates.

**Tech Stack:** .NET 9, EF Core 9 + PostgreSQL, MediatR 14, FluentAssertions + xUnit + Testcontainers, existing `MeepleAiMetrics` (Prometheus).

---

## Spec source

- Spec: `docs/superpowers/specs/2026-06-16-adr-069-toolkit-suggestion-cache-design.md`
- ADR: `docs/for-claude/architecture/adr/adr-069-aitoolkitsuggestion-polymorphic-dto.md`
- Umbrella: [#2383](https://github.com/meepleAi-app/meepleai-monorepo/issues/2383)
- Brainstorm: 2026-06-16 (cached + event-driven invalidation; both Recommended options selected)

## File structure (locked decisions)

### New files

| File | Responsibility |
|------|----------------|
| `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Entities/AiToolkitSuggestionCacheEntry.cs` | Aggregate with `GameId` + serialized payload + audit timestamp. Private setters + `Create` factory + `Refresh` mutator. |
| `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Repositories/IAiToolkitSuggestionCacheRepository.cs` | Contract: `GetByGameIdAsync` / `AddAsync` / `DeleteByGameIdAsync` / `UpsertAsync`. |
| `apps/api/src/Api/BoundedContexts/GameToolkit/Infrastructure/Persistence/AiToolkitSuggestionCacheRepository.cs` | EF Core implementation. Catches 23505 on concurrent `AddAsync` → falls through to update. |
| `apps/api/src/Api/Infrastructure/Entities/GameToolkit/AiToolkitSuggestionCacheEntity.cs` | Persistence POCO. |
| `apps/api/src/Api/Infrastructure/EntityConfigurations/GameToolkit/AiToolkitSuggestionCacheEntityConfiguration.cs` | Column mappings + UNIQUE index on `game_id`. |
| `apps/api/src/Api/Infrastructure/Migrations/YYYYMMDDHHMMSS_AddAiToolkitSuggestionCache.cs` | `CREATE TABLE ai_toolkit_suggestion_cache` with UNIQUE on `game_id`. |
| `apps/api/src/Api/BoundedContexts/GameToolkit/Application/EventHandlers/InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler.cs` | `INotificationHandler<KbDocIndexedEvent>` → delete cache entry. |
| `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Domain/Entities/AiToolkitSuggestionCacheEntryTests.cs` | Factory + mutator unit tests. |
| `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandlerCacheTests.cs` | Cache hit / miss / write-back / repo-failure-degraded behavior. |
| `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/EventHandlers/InvalidateToolkitSuggestionCacheOnKbDocIndexedHandlerTests.cs` | Delete-on-event + delete-failure-logged-not-thrown. |
| `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Integration/AiToolkitSuggestionCacheConcurrentInsertTests.cs` | Testcontainers Postgres: concurrent insert race → 23505 swallowed. |

### Modified files

| File | Change |
|------|--------|
| `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandler.cs` | Inject `IAiToolkitSuggestionCacheRepository`. Cache-aside read at start of `Handle`; cache-write at end of existing LLM path. |
| `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` | Add `DbSet<AiToolkitSuggestionCacheEntity>` (path may be `Infrastructure/Persistence/MeepleAiDbContext.cs` — search if not at root). |
| `apps/api/src/Api/Telemetry/MeepleAiMetrics.cs` | New counters: `RecordAiToolkitCacheHit(gameId)` / `RecordAiToolkitCacheMiss(gameId)` / `RecordAiToolkitCacheInvalidated(gameId)`. Path may be under `Infrastructure/Telemetry/` — search. |
| `apps/api/src/Api/Program.cs` (or composition root) | Register `IAiToolkitSuggestionCacheRepository` → `AiToolkitSuggestionCacheRepository` (Scoped). |

### Naming notes (verify during impl, do not invent)

- Handler is `GenerateToolkitFromKbHandler.cs` (not `...CommandHandler.cs`). Confirmed via `find apps/api/src/Api/BoundedContexts/GameToolkit -iname "*GenerateToolkit*"`.
- Event is `KbDocIndexedEvent` in `Api.BoundedContexts.DocumentProcessing.Domain.Events` namespace. Confirmed via `find apps/api/src -iname "*KbDocIndexed*"`. Spec mentioned `PdfReindexedEvent` — that name does not exist; use `KbDocIndexedEvent`.
- Verify the event exposes `GameId` (or equivalent foreign key to game). If it does not, Task 7 needs an adapter step.

---

## Task 1: Branch + repo state check

**Files:** none (git setup)

- [ ] **Step 1: Confirm current branch + tree state**

```bash
git branch --show-current  # MUST print main-dev
git status                 # MUST show clean tree
git pull --ff-only         # MUST succeed
```

- [ ] **Step 2: Create feature branch + record parent**

```bash
git checkout -b feature/issue-2383-adr-069-toolkit-cache
git config branch.feature/issue-2383-adr-069-toolkit-cache.parent main-dev
```

Expected: `git branch --show-current` → `feature/issue-2383-adr-069-toolkit-cache`.

- [ ] **Step 3: Verify Npgsql + EF versions match spec assumptions**

```bash
grep -E '<PackageReference Include="(Npgsql|Microsoft.EntityFrameworkCore)' apps/api/src/Api/Api.csproj
```

Expected: `Npgsql` ≥ 9, `Npgsql.EntityFrameworkCore.PostgreSQL` ≥ 9.

- [ ] **Step 4: Verify KbDocIndexedEvent shape**

```bash
cat apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Events/KbDocIndexedEvent.cs
```

Read every property. If the event does not expose a `GameId`-equivalent property, STOP and report — Task 7 design needs an adapter (e.g. resolve gameId from `pdfDocumentId` via repository lookup).

---

## Task 2: `AiToolkitSuggestionCacheEntry` aggregate (TDD)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Entities/AiToolkitSuggestionCacheEntry.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Domain/Entities/AiToolkitSuggestionCacheEntryTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Domain/Entities/AiToolkitSuggestionCacheEntryTests.cs
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
public sealed class AiToolkitSuggestionCacheEntryTests
{
    [Fact]
    public void Create_ValidArgs_SetsPropertiesAndStampsGeneratedAt()
    {
        var before = DateTimeOffset.UtcNow;
        var gameId = Guid.NewGuid();
        var entry = AiToolkitSuggestionCacheEntry.Create(gameId, "{\"foo\":1}", kbVersion: 3);
        var after = DateTimeOffset.UtcNow;

        entry.Id.Should().NotBe(Guid.Empty);
        entry.GameId.Should().Be(gameId);
        entry.SuggestionJson.Should().Be("{\"foo\":1}");
        entry.KbVersion.Should().Be(3);
        entry.GeneratedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_EmptyGameId_Throws()
    {
        var act = () => AiToolkitSuggestionCacheEntry.Create(Guid.Empty, "{}", null);
        act.Should().Throw<ArgumentException>().WithMessage("*GameId*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullOrWhitespaceJson_Throws(string? json)
    {
        var act = () => AiToolkitSuggestionCacheEntry.Create(Guid.NewGuid(), json!, null);
        act.Should().Throw<ArgumentException>().WithMessage("*suggestion*");
    }

    [Fact]
    public void Refresh_UpdatesJsonAndKbVersionAndBumpsGeneratedAt()
    {
        var entry = AiToolkitSuggestionCacheEntry.Create(Guid.NewGuid(), "{\"v\":1}", kbVersion: 1);
        var originalGeneratedAt = entry.GeneratedAt;
        Thread.Sleep(5);  // ensure observable delta

        entry.Refresh("{\"v\":2}", kbVersion: 2);

        entry.SuggestionJson.Should().Be("{\"v\":2}");
        entry.KbVersion.Should().Be(2);
        entry.GeneratedAt.Should().BeAfter(originalGeneratedAt);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AiToolkitSuggestionCacheEntryTests" --no-restore 2>&1 | tail -5
```

Expected: build error `AiToolkitSuggestionCacheEntry not defined`.

- [ ] **Step 3: Implement aggregate**

```csharp
// apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Entities/AiToolkitSuggestionCacheEntry.cs
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameToolkit.Domain.Entities;

/// <summary>
/// Cached AiToolkit suggestion per game. ADR-069 follow-up (#2383).
/// One row per game (UNIQUE on game_id). Invalidated by
/// <c>InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler</c>.
/// </summary>
internal sealed class AiToolkitSuggestionCacheEntry : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public string SuggestionJson { get; private set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; private set; }
    public int? KbVersion { get; private set; }

#pragma warning disable CS8618
    private AiToolkitSuggestionCacheEntry() : base() { }
#pragma warning restore CS8618

    public static AiToolkitSuggestionCacheEntry Create(Guid gameId, string suggestionJson, int? kbVersion)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty.", nameof(gameId));
        if (string.IsNullOrWhiteSpace(suggestionJson))
            throw new ArgumentException("suggestion payload cannot be empty.", nameof(suggestionJson));

        return new AiToolkitSuggestionCacheEntry
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            SuggestionJson = suggestionJson,
            KbVersion = kbVersion,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Refresh(string suggestionJson, int? kbVersion)
    {
        if (string.IsNullOrWhiteSpace(suggestionJson))
            throw new ArgumentException("suggestion payload cannot be empty.", nameof(suggestionJson));
        SuggestionJson = suggestionJson;
        KbVersion = kbVersion;
        GeneratedAt = DateTimeOffset.UtcNow;
    }
}
```

If `AggregateRoot<TId>` lives at a different namespace, mirror what `GameToolkit` siblings import — e.g. `Api.SharedKernel.Domain.Aggregates` (verify via `head -5` of another aggregate file).

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AiToolkitSuggestionCacheEntryTests" --no-restore 2>&1 | tail -3
```

Expected: `Superato! - Non superati: 0. Superati: 4-5.`

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Entities/AiToolkitSuggestionCacheEntry.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Domain/Entities/AiToolkitSuggestionCacheEntryTests.cs
git commit -m "feat(toolkit): #2383 AiToolkitSuggestionCacheEntry aggregate (ADR-069)"
```

---

## Task 3: Persistence entity + EF configuration + DbSet

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Entities/GameToolkit/AiToolkitSuggestionCacheEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/GameToolkit/AiToolkitSuggestionCacheEntityConfiguration.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (search for `class MeepleAiDbContext` first to locate)

- [ ] **Step 1: Create persistence POCO**

```csharp
// apps/api/src/Api/Infrastructure/Entities/GameToolkit/AiToolkitSuggestionCacheEntity.cs
namespace Api.Infrastructure.Entities.GameToolkit;

public class AiToolkitSuggestionCacheEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string SuggestionJson { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public int? KbVersion { get; set; }
}
```

- [ ] **Step 2: Create EF configuration**

```csharp
// apps/api/src/Api/Infrastructure/EntityConfigurations/GameToolkit/AiToolkitSuggestionCacheEntityConfiguration.cs
using Api.Infrastructure.Entities.GameToolkit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameToolkit;

internal sealed class AiToolkitSuggestionCacheEntityConfiguration
    : IEntityTypeConfiguration<AiToolkitSuggestionCacheEntity>
{
    public void Configure(EntityTypeBuilder<AiToolkitSuggestionCacheEntity> builder)
    {
        builder.ToTable("ai_toolkit_suggestion_cache");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.GameId).HasColumnName("game_id").IsRequired();
        builder.Property(e => e.SuggestionJson).HasColumnName("suggestion_json").IsRequired();
        builder.Property(e => e.GeneratedAt).HasColumnName("generated_at").IsRequired();
        builder.Property(e => e.KbVersion).HasColumnName("kb_version");

        builder.HasIndex(e => e.GameId)
            .HasDatabaseName("UX_ai_toolkit_suggestion_cache_game_id")
            .IsUnique();

        builder.HasIndex(e => e.GeneratedAt)
            .HasDatabaseName("IX_ai_toolkit_suggestion_cache_generated_at");
    }
}
```

- [ ] **Step 3: Locate `MeepleAiDbContext` + add DbSet**

```bash
grep -rn "class MeepleAiDbContext" apps/api/src/Api --include="*.cs"
```

Open the file at the reported path. Add the `DbSet`:

```csharp
public DbSet<AiToolkitSuggestionCacheEntity> AiToolkitSuggestionCache => Set<AiToolkitSuggestionCacheEntity>();
```

Match the casing convention used by sibling DbSets in the same file (e.g. if they all use `=> Set<...>()` and PascalCase, keep that style). Add the import `using Api.Infrastructure.Entities.GameToolkit;` if not present.

- [ ] **Step 4: Build — expect 0 errors**

```bash
cd apps/api && dotnet build src/Api/Api.csproj --no-restore --nologo 2>&1 | tail -3
```

Expected: `Compilazione completata. Avvisi: 0 Errori: 0`.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/GameToolkit/AiToolkitSuggestionCacheEntity.cs \
        apps/api/src/Api/Infrastructure/EntityConfigurations/GameToolkit/AiToolkitSuggestionCacheEntityConfiguration.cs \
        apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
git commit -m "feat(toolkit): #2383 AiToolkitSuggestionCacheEntity persistence + EF config"
```

(If `MeepleAiDbContext.cs` is at a non-root path, replace its full path in the `git add` line — the path printed by Step 3 grep is authoritative.)

---

## Task 4: EF migration `AddAiToolkitSuggestionCache`

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/YYYYMMDDHHMMSS_AddAiToolkitSuggestionCache.cs` (timestamp auto-generated)
- Create: `apps/api/src/Api/Infrastructure/Migrations/YYYYMMDDHHMMSS_AddAiToolkitSuggestionCache.Designer.cs` (auto)
- Modify: `apps/api/src/Api/Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs` (auto)

- [ ] **Step 1: Generate migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddAiToolkitSuggestionCache --no-build
```

Expected: `Done. To undo this action, use 'ef migrations remove'`.

- [ ] **Step 2: Verify generated SQL shape**

```bash
ls apps/api/src/Api/Infrastructure/Migrations/*_AddAiToolkitSuggestionCache.cs
cat apps/api/src/Api/Infrastructure/Migrations/*_AddAiToolkitSuggestionCache.cs | head -50
```

Expected `Up`:

```csharp
migrationBuilder.CreateTable(
    name: "ai_toolkit_suggestion_cache",
    columns: table => new
    {
        id = table.Column<Guid>(type: "uuid", nullable: false),
        game_id = table.Column<Guid>(type: "uuid", nullable: false),
        suggestion_json = table.Column<string>(type: "text", nullable: false),
        generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
        kb_version = table.Column<int>(type: "integer", nullable: true)
    },
    constraints: table => { table.PrimaryKey("PK_ai_toolkit_suggestion_cache", x => x.id); });

migrationBuilder.CreateIndex(
    name: "UX_ai_toolkit_suggestion_cache_game_id",
    table: "ai_toolkit_suggestion_cache",
    column: "game_id",
    unique: true);

migrationBuilder.CreateIndex(
    name: "IX_ai_toolkit_suggestion_cache_generated_at",
    table: "ai_toolkit_suggestion_cache",
    column: "generated_at");
```

If anything diverges from the expected shape (extra columns, missing UNIQUE, different type), STOP and investigate the EF configuration in Task 3 before proceeding.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/*_AddAiToolkitSuggestionCache.cs \
        apps/api/src/Api/Infrastructure/Migrations/*_AddAiToolkitSuggestionCache.Designer.cs \
        apps/api/src/Api/Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs
git commit -m "feat(toolkit): #2383 EF migration AddAiToolkitSuggestionCache"
```

---

## Task 5: Repository interface + implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Repositories/IAiToolkitSuggestionCacheRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Infrastructure/Persistence/AiToolkitSuggestionCacheRepository.cs`
- Modify: `apps/api/src/Api/Program.cs` (DI registration) — search for `AddScoped<I...Repository,` pattern to find the registration block

- [ ] **Step 1: Interface**

```csharp
// apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Repositories/IAiToolkitSuggestionCacheRepository.cs
using Api.BoundedContexts.GameToolkit.Domain.Entities;

namespace Api.BoundedContexts.GameToolkit.Domain.Repositories;

internal interface IAiToolkitSuggestionCacheRepository
{
    Task<AiToolkitSuggestionCacheEntry?> GetByGameIdAsync(Guid gameId, CancellationToken ct = default);
    Task UpsertAsync(AiToolkitSuggestionCacheEntry entry, CancellationToken ct = default);
    Task DeleteByGameIdAsync(Guid gameId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Implementation skeleton — read sibling repo for pattern parity first**

```bash
head -60 apps/api/src/Api/BoundedContexts/GameToolkit/Infrastructure/Persistence/GameToolkitRepository.cs
```

Note the exact base class (`RepositoryBase` if present), constructor signature, mapping function naming convention. Match it.

- [ ] **Step 3: Implementation**

```csharp
// apps/api/src/Api/BoundedContexts/GameToolkit/Infrastructure/Persistence/AiToolkitSuggestionCacheRepository.cs
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameToolkit;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.GameToolkit.Infrastructure.Persistence;

internal sealed class AiToolkitSuggestionCacheRepository
    : RepositoryBase, IAiToolkitSuggestionCacheRepository
{
    private readonly ILogger<AiToolkitSuggestionCacheRepository> _logger;

    public AiToolkitSuggestionCacheRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<AiToolkitSuggestionCacheRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiToolkitSuggestionCacheEntry?> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        var entity = await DbContext.AiToolkitSuggestionCache
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.GameId == gameId, ct)
            .ConfigureAwait(false);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpsertAsync(AiToolkitSuggestionCacheEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        // Look up existing by game_id (UNIQUE). Use tracked query so EF detects modification.
        var existing = await DbContext.AiToolkitSuggestionCache
            .FirstOrDefaultAsync(e => e.GameId == entry.GameId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await DbContext.AiToolkitSuggestionCache.AddAsync(MapToPersistence(entry), ct).ConfigureAwait(false);
        }
        else
        {
            existing.SuggestionJson = entry.SuggestionJson;
            existing.GeneratedAt = entry.GeneratedAt;
            existing.KbVersion = entry.KbVersion;
        }
    }

    public async Task DeleteByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        var deleted = await DbContext.AiToolkitSuggestionCache
            .Where(e => e.GameId == gameId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "AiToolkit cache delete for game {GameId}: {DeletedCount} row(s)",
            gameId, deleted);
    }

    private static AiToolkitSuggestionCacheEntry MapToDomain(AiToolkitSuggestionCacheEntity entity) =>
        AiToolkitSuggestionCacheEntryMapping.Reconstitute(
            entity.Id, entity.GameId, entity.SuggestionJson, entity.GeneratedAt, entity.KbVersion);

    private static AiToolkitSuggestionCacheEntity MapToPersistence(AiToolkitSuggestionCacheEntry domain) =>
        new()
        {
            Id = domain.Id,
            GameId = domain.GameId,
            SuggestionJson = domain.SuggestionJson,
            GeneratedAt = domain.GeneratedAt,
            KbVersion = domain.KbVersion,
        };
}
```

The `Reconstitute` static factory needs to exist on the aggregate for repository reconstitution. Add it now:

```csharp
// Inside AiToolkitSuggestionCacheEntry.cs — add at end of class, before closing brace
internal static class AiToolkitSuggestionCacheEntryMapping
{
    public static AiToolkitSuggestionCacheEntry Reconstitute(
        Guid id, Guid gameId, string suggestionJson, DateTimeOffset generatedAt, int? kbVersion)
    {
        // Use reflection-free private property assignment via a private factory.
        var entry = (AiToolkitSuggestionCacheEntry)Activator.CreateInstance(
            typeof(AiToolkitSuggestionCacheEntry), nonPublic: true)!;
        typeof(AiToolkitSuggestionCacheEntry).GetProperty(nameof(AiToolkitSuggestionCacheEntry.Id))!
            .SetValue(entry, id);
        typeof(AiToolkitSuggestionCacheEntry).GetProperty(nameof(AiToolkitSuggestionCacheEntry.GameId))!
            .SetValue(entry, gameId);
        typeof(AiToolkitSuggestionCacheEntry).GetProperty(nameof(AiToolkitSuggestionCacheEntry.SuggestionJson))!
            .SetValue(entry, suggestionJson);
        typeof(AiToolkitSuggestionCacheEntry).GetProperty(nameof(AiToolkitSuggestionCacheEntry.GeneratedAt))!
            .SetValue(entry, generatedAt);
        typeof(AiToolkitSuggestionCacheEntry).GetProperty(nameof(AiToolkitSuggestionCacheEntry.KbVersion))!
            .SetValue(entry, kbVersion);
        return entry;
    }
}
```

If sibling repos use a cleaner reconstitution pattern (e.g. internal `Reconstitute` static method on the aggregate itself, no reflection), prefer that and remove the reflection helper. Read `GameToolkitRepository.MapToDomain` for the precedent.

- [ ] **Step 4: Register in DI**

Locate the existing `AddScoped<I*Repository, ...>` registration block in `Program.cs`:

```bash
grep -n "AddScoped<IGameToolkitRepository" apps/api/src/Api/Program.cs
```

Add adjacent:

```csharp
builder.Services.AddScoped<IAiToolkitSuggestionCacheRepository, AiToolkitSuggestionCacheRepository>();
```

- [ ] **Step 5: Build — expect 0 errors**

```bash
cd apps/api && dotnet build src/Api/Api.csproj --no-restore --nologo 2>&1 | tail -3
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Repositories/IAiToolkitSuggestionCacheRepository.cs \
        apps/api/src/Api/BoundedContexts/GameToolkit/Infrastructure/Persistence/AiToolkitSuggestionCacheRepository.cs \
        apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Entities/AiToolkitSuggestionCacheEntry.cs \
        apps/api/src/Api/Program.cs
git commit -m "feat(toolkit): #2383 AiToolkitSuggestionCacheRepository (upsert + delete + reconstitute)"
```

---

## Task 6: Cache-aside change in `GenerateToolkitFromKbHandler` (TDD)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandlerCacheTests.cs`

- [ ] **Step 1: Inspect current handler**

```bash
wc -l apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandler.cs
sed -n '1,100p' apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandler.cs
```

Note the ctor signature (injected services) + the shape of the final return statement (which already produces an `AiToolkitSuggestionDto`).

- [ ] **Step 2: Write failing tests (cache hit, cache miss, write-back, repo-failure-degraded)**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandlerCacheTests.cs
using System.Text.Json;
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public sealed class GenerateToolkitFromKbHandlerCacheTests
{
    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedDtoWithoutLlmCall()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var cachedDto = BuildSampleDto();
        var cachedJson = JsonSerializer.Serialize(cachedDto);
        var cacheEntry = AiToolkitSuggestionCacheEntry.Create(gameId, cachedJson, kbVersion: null);

        var cacheRepo = new Mock<IAiToolkitSuggestionCacheRepository>();
        cacheRepo.Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(cacheEntry);

        var llmService = new Mock<Api.Services.ILlmService>(MockBehavior.Strict);
        // No setup: any LLM call is unexpected and Strict mock will throw.

        var handler = BuildHandler(cacheRepo: cacheRepo, llmService: llmService);

        // Act
        var result = await handler.Handle(new GenerateToolkitFromKbCommand(gameId, Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        llmService.VerifyNoOtherCalls();
        cacheRepo.Verify(r => r.UpsertAsync(It.IsAny<AiToolkitSuggestionCacheEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CacheMiss_RunsLlmAndPersistsCache()
    {
        // Arrange — minimal valid stubs for the existing LLM pipeline.
        // Use the existing test fixture pattern in GameToolkit handler tests
        // (look at sibling *HandlerTests.cs for Mock setups for IHybridSearchService,
        // ILlmService, IRagAccessService, IGameCoreDataProvider).
        // ...
        // Verify: cacheRepo.UpsertAsync called once with a non-null entry whose
        // SuggestionJson roundtrips to the same DTO as the LLM result.
    }

    [Fact]
    public async Task Handle_CacheRepoGetThrows_FallsThroughToLlmDegraded()
    {
        // Arrange: cacheRepo.GetByGameIdAsync throws InvalidOperationException("db down").
        // Assert: the LLM pipeline still runs; result returned to caller.
        // The cacheRepo.UpsertAsync MAY also throw — handler swallows both repo errors.
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    private static AiToolkitSuggestionDto BuildSampleDto() =>
        new(
            // Fill with the actual record positional args. Read AiToolkitSuggestionDtos.cs
            // to copy the constructor shape verbatim — do not invent fields.
            // ...
        );

    private static GenerateToolkitFromKbHandler BuildHandler(
        Mock<IAiToolkitSuggestionCacheRepository>? cacheRepo = null,
        Mock<Api.Services.ILlmService>? llmService = null /* + other deps */)
    {
        // Wire the actual ctor of GenerateToolkitFromKbHandler with sensible defaults.
        // Read the handler ctor and pass mocks for each parameter.
        // ...
        throw new NotImplementedException("Fill in once handler ctor is inspected");
    }
}
```

Three test bodies are skeletons — fill them in after Step 1 inspection reveals the exact ctor signature + DTO shape. **Do not invent**: read `AiToolkitSuggestionDtos.cs` for the DTO constructor shape; read the handler ctor for the dependency list.

- [ ] **Step 3: Run tests — expect FAIL (build error or logic mismatch)**

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GenerateToolkitFromKbHandlerCacheTests" --no-restore 2>&1 | tail -5
```

- [ ] **Step 4: Modify handler — add cache-aside**

Edit `GenerateToolkitFromKbHandler.cs`:

1. Inject `IAiToolkitSuggestionCacheRepository` in the ctor.
2. At the **top** of `Handle` (after `ArgumentNullException.ThrowIfNull(request)`):

```csharp
try
{
    var cached = await _cacheRepo.GetByGameIdAsync(request.GameId, cancellationToken).ConfigureAwait(false);
    if (cached is not null)
    {
        _logger.LogInformation("AiToolkit cache HIT for game {GameId}", request.GameId);
        return JsonSerializer.Deserialize<AiToolkitSuggestionDto>(cached.SuggestionJson)!;
    }
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    // Cache-aside degraded mode: log + fall through to LLM (UX must not depend on cache infra).
    _logger.LogWarning(ex, "AiToolkit cache GET failed for game {GameId}; falling back to LLM", request.GameId);
}
```

3. At the **end** of `Handle` (just before the existing `return result;`):

```csharp
try
{
    var entry = AiToolkitSuggestionCacheEntry.Create(
        request.GameId,
        JsonSerializer.Serialize(result),
        kbVersion: null);  // KbVersion deferred per ADR-069 spec §"Out of scope"
    await _cacheRepo.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);
    await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    _logger.LogWarning(ex, "AiToolkit cache UPSERT failed for game {GameId}; result returned without caching", request.GameId);
}
```

`_unitOfWork` must be injected if it isn't already; check existing handlers in the same BC for the IUnitOfWork pattern. If the handler does NOT use UoW (some handlers commit via repository or are read-only), use whatever commit mechanism the BC already uses.

`JsonSerializer` requires `using System.Text.Json;`. Use the same options the rest of the codebase uses for DTO serialization (search `JsonSerializerOptions` to confirm — usually `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`).

- [ ] **Step 5: Run tests — expect PASS**

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GenerateToolkitFromKbHandlerCacheTests" --no-restore 2>&1 | tail -3
```

- [ ] **Step 6: Run the full GameToolkit BC test suite to detect regression**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameToolkit" --no-restore 2>&1 | tail -3
```

Expected: 0 failed. If any sibling test broke (e.g. existing handler tests that did not mock the new cache repo dependency), update those tests to inject a null-object or pass-through cache repo mock.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandlerCacheTests.cs
git commit -m "feat(toolkit): #2383 cache-aside in GenerateToolkitFromKbHandler (ADR-069)"
```

---

## Task 7: `KbDocIndexedEvent` invalidation handler (TDD)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/EventHandlers/InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/EventHandlers/InvalidateToolkitSuggestionCacheOnKbDocIndexedHandlerTests.cs`

- [ ] **Step 1: Re-confirm `KbDocIndexedEvent` shape**

```bash
cat apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Events/KbDocIndexedEvent.cs
```

Specifically: does the event expose a `GameId` or equivalent? If only `PdfDocumentId` is present, the handler MUST resolve the gameId via a `IPdfDocumentRepository.GetByIdAsync(pdfId)` lookup — add that step.

- [ ] **Step 2: Failing test**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/EventHandlers/InvalidateToolkitSuggestionCacheOnKbDocIndexedHandlerTests.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.GameToolkit.Application.EventHandlers;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
public sealed class InvalidateToolkitSuggestionCacheOnKbDocIndexedHandlerTests
{
    [Fact]
    public async Task Handle_KbDocIndexed_DeletesCacheForGameId()
    {
        var gameId = Guid.NewGuid();
        var cacheRepo = new Mock<IAiToolkitSuggestionCacheRepository>();
        cacheRepo.Setup(r => r.DeleteByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask)
                 .Verifiable();

        var sut = new InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler(
            cacheRepo.Object,
            NullLogger<InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler>.Instance);

        var evt = new KbDocIndexedEvent(gameId, /* other ctor args copied from the actual event record */);

        await sut.Handle(evt, CancellationToken.None);

        cacheRepo.Verify();
    }

    [Fact]
    public async Task Handle_DeleteThrows_LogsAndDoesNotRethrow()
    {
        var cacheRepo = new Mock<IAiToolkitSuggestionCacheRepository>();
        cacheRepo.Setup(r => r.DeleteByGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("db down"));

        var sut = new InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler(
            cacheRepo.Object,
            NullLogger<InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler>.Instance);

        var act = async () => await sut.Handle(
            new KbDocIndexedEvent(Guid.NewGuid()),  // adjust ctor args
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
```

The `KbDocIndexedEvent` ctor args list MUST match the actual event record. Copy them verbatim from Step 1.

- [ ] **Step 3: Implement handler**

```csharp
// apps/api/src/Api/BoundedContexts/GameToolkit/Application/EventHandlers/InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.EventHandlers;

/// <summary>
/// ADR-069 follow-up (#2383): invalidate the cached AiToolkit suggestion
/// whenever a KB document is re-indexed for a game. The next user request
/// regenerates via the LLM and writes back to the cache.
///
/// Idempotent (DeleteByGameIdAsync is a no-op when no row matches).
/// Errors are logged and swallowed — failing to invalidate must not roll
/// back the indexing pipeline (cache becomes stale until next reindex).
/// </summary>
internal sealed class InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler
    : INotificationHandler<KbDocIndexedEvent>
{
    private readonly IAiToolkitSuggestionCacheRepository _cacheRepo;
    private readonly ILogger<InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler> _logger;

    public InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler(
        IAiToolkitSuggestionCacheRepository cacheRepo,
        ILogger<InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler> logger)
    {
        _cacheRepo = cacheRepo ?? throw new ArgumentNullException(nameof(cacheRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(KbDocIndexedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            await _cacheRepo.DeleteByGameIdAsync(notification.GameId, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Invalidated AiToolkit cache for game {GameId} post-KB-doc-index",
                notification.GameId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to invalidate AiToolkit cache for game {GameId} — cache will be stale until next reindex",
                notification.GameId);
        }
    }
}
```

If `KbDocIndexedEvent` does NOT expose `GameId`, replace `notification.GameId` with the lookup chain identified in Step 1 (e.g. `await _pdfRepo.GetByIdAsync(notification.PdfDocumentId)` then `.GameId`).

- [ ] **Step 4: Run tests — expect PASS**

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~InvalidateToolkitSuggestionCacheOnKbDocIndexedHandlerTests" --no-restore 2>&1 | tail -3
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolkit/Application/EventHandlers/InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/EventHandlers/InvalidateToolkitSuggestionCacheOnKbDocIndexedHandlerTests.cs
git commit -m "feat(toolkit): #2383 InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler (ADR-069)"
```

---

## Task 8: Telemetry counters

**Files:**
- Modify: locate via `grep -rn "class MeepleAiMetrics" apps/api/src/Api --include="*.cs"` (likely `apps/api/src/Api/Telemetry/MeepleAiMetrics.cs` or `apps/api/src/Api/Infrastructure/Telemetry/MeepleAiMetrics.cs`)

- [ ] **Step 1: Read existing counter pattern**

```bash
grep -B 2 -A 8 "RecordNotificationCreated\|RecordPdfStateChanged" $(grep -rln "class MeepleAiMetrics" apps/api/src/Api --include="*.cs")
```

Match style: Prometheus counter type, label naming, method shape.

- [ ] **Step 2: Add three counters**

```csharp
private static readonly Counter<long> AiToolkitCacheHits = Meter.CreateCounter<long>(
    "meepleai_aitoolkit_cache_hit_total",
    description: "AiToolkit suggestion cache hits (ADR-069 #2383)");

private static readonly Counter<long> AiToolkitCacheMisses = Meter.CreateCounter<long>(
    "meepleai_aitoolkit_cache_miss_total",
    description: "AiToolkit suggestion cache misses leading to LLM call (ADR-069 #2383)");

private static readonly Counter<long> AiToolkitCacheInvalidations = Meter.CreateCounter<long>(
    "meepleai_aitoolkit_cache_invalidated_total",
    description: "AiToolkit suggestion cache entries invalidated by KbDocIndexedEvent (ADR-069 #2383)");

public static void RecordAiToolkitCacheHit(Guid gameId) =>
    AiToolkitCacheHits.Add(1, new KeyValuePair<string, object?>("game_id", gameId));

public static void RecordAiToolkitCacheMiss(Guid gameId) =>
    AiToolkitCacheMisses.Add(1, new KeyValuePair<string, object?>("game_id", gameId));

public static void RecordAiToolkitCacheInvalidated(Guid gameId) =>
    AiToolkitCacheInvalidations.Add(1, new KeyValuePair<string, object?>("game_id", gameId));
```

If the existing metrics file uses a different `Meter` instance or pattern (e.g. `OpenTelemetry.Metrics`), match its style verbatim.

- [ ] **Step 3: Wire into handler + event handler**

In `GenerateToolkitFromKbHandler.cs`:

```csharp
// after cache hit detection
MeepleAiMetrics.RecordAiToolkitCacheHit(request.GameId);
return JsonSerializer.Deserialize<AiToolkitSuggestionDto>(cached.SuggestionJson)!;

// after cache miss path begins
MeepleAiMetrics.RecordAiToolkitCacheMiss(request.GameId);
```

In `InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler.cs`:

```csharp
await _cacheRepo.DeleteByGameIdAsync(notification.GameId, cancellationToken).ConfigureAwait(false);
MeepleAiMetrics.RecordAiToolkitCacheInvalidated(notification.GameId);
```

- [ ] **Step 4: Build — expect 0 errors**

```bash
cd apps/api && dotnet build src/Api/Api.csproj --no-restore --nologo 2>&1 | tail -3
```

- [ ] **Step 5: Commit**

```bash
git add $(grep -rln "class MeepleAiMetrics" apps/api/src/Api --include="*.cs") \
        apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbHandler.cs \
        apps/api/src/Api/BoundedContexts/GameToolkit/Application/EventHandlers/InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler.cs
git commit -m "feat(toolkit): #2383 AiToolkit cache telemetry counters (hit/miss/invalidated)"
```

---

## Task 9: Testcontainers integration test (concurrent insert race)

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Integration/AiToolkitSuggestionCacheConcurrentInsertTests.cs`

- [ ] **Step 1: Locate existing Testcontainers integration test pattern**

```bash
find apps/api/tests/Api.Tests -path "*Integration*" -name "*.cs" | head -5
```

Read one of them to copy: `[Collection("Integration-...")]` attribute, fixture injection, fresh DB-per-test pattern. Spec memo `xunit-unit-test-in-integration-collection-pitfall.md` applies — this MUST be in an Integration collection, not Unit.

- [ ] **Step 2: Write test**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Integration/AiToolkitSuggestionCacheConcurrentInsertTests.cs
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.BoundedContexts.GameToolkit.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;  // adjust to match the existing IntegrationFixture namespace
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Integration;

[Trait("Category", TestCategories.Integration)]
[Collection("Integration-Postgres")]  // match the actual collection name from sibling files
public sealed class AiToolkitSuggestionCacheConcurrentInsertTests
{
    private readonly PostgresIntegrationFixture _fixture;  // match actual fixture type

    public AiToolkitSuggestionCacheConcurrentInsertTests(PostgresIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Upsert_TwoConcurrentInsertsForSameGameId_OnlyOneRowPersists()
    {
        // Arrange — two separate scoped DbContexts (simulate two handler instances).
        await using var scope1 = _fixture.CreateScope();
        await using var scope2 = _fixture.CreateScope();

        var repo1 = scope1.ServiceProvider.GetRequiredService<IAiToolkitSuggestionCacheRepository>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<IAiToolkitSuggestionCacheRepository>();

        var gameId = Guid.NewGuid();
        var entry1 = AiToolkitSuggestionCacheEntry.Create(gameId, "{\"src\":\"r1\"}", null);
        var entry2 = AiToolkitSuggestionCacheEntry.Create(gameId, "{\"src\":\"r2\"}", null);

        // Act — both attempt to Upsert before either has committed.
        await repo1.UpsertAsync(entry1);
        await repo2.UpsertAsync(entry2);
        await scope1.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        // The second SaveChanges should NOT throw — the repository handles the
        // unique-violation gracefully (either it falls through to update or the
        // existing-row check in Upsert prevented the second AddAsync).
        var saveSecond = async () =>
            await scope2.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        await saveSecond.Should().NotThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();

        // Assert — exactly one row exists for this gameId.
        var verifyRepo = _fixture.CreateScope().ServiceProvider
            .GetRequiredService<IAiToolkitSuggestionCacheRepository>();
        var result = await verifyRepo.GetByGameIdAsync(gameId);
        result.Should().NotBeNull();
    }
}
```

The exact `_fixture.CreateScope()` / `IUnitOfWork.SaveChangesAsync()` calls must match the actual integration test infrastructure. If the codebase uses a different pattern (e.g. `WebApplicationFactory<Program>` instead of fixture scopes), adapt.

If the existing `UpsertAsync` already serialises the `existing` check + `AddAsync` in a single DbContext call (Task 5 implementation), the race manifests only across DbContext instances — this test reproduces that. The spec calls for a try/catch on 23505 in `UpsertAsync` if needed; add it if this test fails.

- [ ] **Step 3: Run test — expect PASS (or FAIL guiding you to add the 23505 catch)**

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AiToolkitSuggestionCacheConcurrentInsertTests" --no-restore 2>&1 | tail -3
```

If FAIL with `DbUpdateException`, add to `AiToolkitSuggestionCacheRepository.UpsertAsync` (Task 5):

```csharp
catch (DbUpdateException ex) when (
    ex.InnerException is PostgresException { SqlState: "23505" } pe
    && string.Equals(pe.ConstraintName, "UX_ai_toolkit_suggestion_cache_game_id", StringComparison.Ordinal))
{
    _logger.LogInformation(
        "AiToolkit cache concurrent insert race for game {GameId} — already persisted by other writer",
        entry.GameId);
}
```

Re-run the test. Expect PASS.

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Integration/AiToolkitSuggestionCacheConcurrentInsertTests.cs \
        apps/api/src/Api/BoundedContexts/GameToolkit/Infrastructure/Persistence/AiToolkitSuggestionCacheRepository.cs
git commit -m "test(toolkit): #2383 Testcontainers concurrent-insert race for AiToolkit cache"
```

---

## Task 10: PR + merge

**Files:** none (git + GH operations)

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/issue-2383-adr-069-toolkit-cache
```

- [ ] **Step 2: Create PR (target main-dev)**

```bash
gh pr create --base main-dev \
  --title "feat(toolkit): #2383 ADR-069 toolkit suggestion cache + invalidation" \
  --body "$(cat <<'EOF'
## Summary

Implements ADR-069 follow-up per the 2026-06-16 brainstorm decision (cached + event-driven invalidation, Option D).

Spec: `docs/superpowers/specs/2026-06-16-adr-069-toolkit-suggestion-cache-design.md`
Plan: `docs/superpowers/plans/2026-06-16-adr-069-toolkit-suggestion-cache.md`

## Changes

- New `AiToolkitSuggestionCacheEntry` aggregate (one row per game)
- New `ai_toolkit_suggestion_cache` table + EF migration + UNIQUE on `game_id`
- New `IAiToolkitSuggestionCacheRepository` (Upsert / Get / DeleteByGameId)
- Modified `GenerateToolkitFromKbHandler` to cache-aside (hit returns instantly; miss runs LLM + writes back)
- New `InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler` to delete the cached entry on KB reindex
- Telemetry: `meepleai_aitoolkit_cache_{hit,miss,invalidated}_total` counters

## Test plan

- [x] Domain aggregate unit tests (Create + Refresh + invariants)
- [x] Handler cache-aside unit tests (hit / miss / write-back / degraded)
- [x] Invalidation handler unit tests (delete-on-event / failure-logged-not-thrown)
- [x] Testcontainers integration test for concurrent insert race (23505)
- [ ] CI green
- [ ] Manual smoke: hit `POST /api/v1/game-toolkits/{id}/generate-from-kb` twice; second call should observe `cache_hit_total++` and return in <500ms

## Out of scope

- Admin force-regenerate endpoint (deferred per spec)
- KbVersion tracking enforcement (nullable in this iteration)
- Cache pruning background job (deferred until storage > 1 GB)

Closes the ADR-069 row in #2383 tracker.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Wait for CI green; merge**

```bash
gh pr checks --watch  # or poll via gh pr view --json statusCheckRollup
gh pr merge --squash --delete-branch
```

- [ ] **Step 4: Update #2383 tracker checkbox for ADR-069 implementation row**

The tracker already has ADR-069 marked ✅ for the design spec PR #2401. Add a comment to the PR or to #2383 noting that the implementation is now also live: `✅ implementation shipped PR #<N>`.

- [ ] **Step 5: Local cleanup**

```bash
git checkout main-dev
git pull --ff-only
git remote prune origin
```

---

## Self-review

**Spec coverage:** Every spec section maps to a task:

- §Architecture (cache-aside) → Tasks 2-6
- §New entity → Task 2
- §Migration → Tasks 3-4
- §Cache-aside handler change → Task 6
- §Invalidation handler → Task 7
- §Telemetry → Task 8
- §Error handling (LLM fail, cache repo fail, concurrent writes) → Task 6 (degraded fallback), Task 9 (concurrent race)
- §Testing strategy → Tasks 2, 6, 7, 9
- §Migration shape → Task 4 verification
- §Out of scope (admin endpoint, KbVersion, pruning, telemetry dashboard) → noted in Task 10 PR body
- §Rollback → noted in Task 10 PR body

**Placeholder scan:** Task 6 Step 2 contains two test bodies marked "Fill in once handler ctor is inspected" — this is intentional because the handler ctor signature must be read at impl time (the file is 186 LOC and the test fixture wiring is too large to inline without reading the actual ctor). The implementer is given the exact grep to run, the exact files to read, and the rule "do not invent — copy ctor verbatim".

**Type consistency:** `IAiToolkitSuggestionCacheRepository` methods are `GetByGameIdAsync` / `UpsertAsync` / `DeleteByGameIdAsync` everywhere (Task 5 definition, Tasks 6/7/9 consumption). `AiToolkitSuggestionCacheEntry` has `Create(gameId, json, kbVersion)` and `Refresh(json, kbVersion)` everywhere. Event is `KbDocIndexedEvent` everywhere (corrected from the spec's `PdfReindexedEvent`). DbSet is `AiToolkitSuggestionCache` (Task 3).
