# #2438 PR-B — Player Statistics Redis Cache + Invalidation (BE) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cache `GetPlayerStatistics` server-side (HybridCache L1+L2 Redis, 5-min TTL) with per-user tag invalidation on the events that change a user's stats.

**Architecture:** Wrap `GetPlayerStatisticsQueryHandler.Handle` with `IHybridCacheService.GetOrCreateAsync` (the exact pattern used by `GetGameLeaderboardQueryHandler`), keyed per user + date range, tagged `player-stats:{userId}`. A new MediatR `INotificationHandler` evicts that tag on `PlayRecordCompletedEvent` and `PlayRecordDeletedEvent` (the events that change which Completed records exist for a user). `PlayRecordCreatedEvent` is intentionally NOT a trigger — a freshly created record is `Planned`, and stats only count `Completed` records (documented in the ADR).

**Tech Stack:** .NET 9 · MediatR · IHybridCacheService (HybridCache L1+L2 Redis) · EF Core · xUnit + Moq + Testcontainers

**Reference patterns (read before implementing):**
- cache wrapping: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/Leaderboard/GetGameLeaderboardQueryHandler.cs:19,47-55` (TTL const, key string, `GetOrCreateAsync(key, factory, tags, expiration, ct)`)
- cache service contract: `apps/api/src/Api/Services/IHybridCacheService.cs` (`GetOrCreateAsync<T> where T:class`; `RemoveByTagAsync(tag)`)
- invalidation handler pattern: `apps/api/src/Api/BoundedContexts/Administration/Application/EventHandlers/UserActivityCacheInvalidationEventHandler.cs`
- the handler to wrap: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayerStatisticsQueryHandler.cs`
- events: `PlayRecordCompletedEvent(RecordId, Duration)` (no userId), `PlayRecordDeletedEvent(RecordId, DeletedByUserId)`, both `: DomainEventBase`

**Notes:** `PlayerStatisticsDto` is a `record` (reference type) → satisfies `GetOrCreateAsync<T> where T : class`. `IHybridCacheService` is registered Singleton (`InfrastructureServiceExtensions.cs:351`). MediatR auto-discovers `INotificationHandler` — no manual DI registration needed (confirm against the reference handler).

**Test command:** `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PlayerStatistics|FullyQualifiedName~PlayRecordStatsCache"`

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `docs/for-claude/architecture/adr/adr-NNN-player-stats-cache.md` | ADR: key schema, tag, triggers, TTL | Create |
| `.../Queries/PlayRecords/GetPlayerStatisticsQueryHandler.cs` | inject cache, wrap Handle, extract `ComputeAsync` | Modify |
| `.../Application/EventHandlers/PlayRecordStatsCacheInvalidationHandler.cs` | evict `player-stats:{userId}` on Completed/Deleted | Create |
| `tests/.../PlayRecords/GetPlayerStatisticsQueryHandlerCacheTests.cs` | cache hit/miss unit | Create |
| `tests/.../EventHandlers/PlayRecordStatsCacheInvalidationHandlerTests.cs` | invalidation unit | Create |
| `tests/.../Integration/GameManagement/PlayerStatisticsCacheTests.cs` | end-to-end cache+invalidation (Testcontainers) | Create |

---

### Task 1: ADR

**Files:**
- Create: `docs/for-claude/architecture/adr/adr-NNN-player-stats-cache.md` (run `ls docs/for-claude/architecture/adr/ | tail -3` to pick the next ADR number)

- [ ] **Step 1: Write the ADR** (short — context, decision, consequences):

Key points to capture:
- **Key**: `player-stats:{userId}:{startDate.Ticks ?? 0}:{endDate.Ticks ?? 0}` — per-user, per-range (mirrors `GetGameLeaderboard`'s Ticks-based key for timezone-stable, serialization-safe keys).
- **Tag**: `player-stats:{userId}` — one tag per user, so a single `RemoveByTagAsync` evicts ALL the user's ranged entries at once (avoids enumerating date combinations).
- **TTL**: 5 min (matches the FE React Query `staleTime` and `GetGameLeaderboard`).
- **Triggers**: `PlayRecordCompletedEvent` + `PlayRecordDeletedEvent`. NOT `PlayRecordCreatedEvent` — a new record is `Planned`, and `GetPlayerStatistics` filters `Status == Completed`, so a Created event cannot change any cached stat. Documenting this avoids a useless eviction + a `Completed`-event userId lookup being mistaken for incompleteness.
- **Completed userId lookup**: `PlayRecordCompletedEvent` carries no userId, so the handler resolves it via `_context.PlayRecords.IgnoreQueryFilters().Where(r => r.Id == recordId).Select(r => r.CreatedByUserId).FirstOrDefaultAsync()`. `PlayRecordDeletedEvent` carries `DeletedByUserId` directly (no lookup).
- **Failure mode**: HybridCache L2 (Redis) failures fall back to L1 (per the service contract) — stats stay available.

- [ ] **Step 2: Commit**

```bash
git add docs/for-claude/architecture/adr/
git commit -m "docs(adr): #2438 player-stats cache key + invalidation strategy"
```

---

### Task 2: Wrap the query handler with HybridCache

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayerStatisticsQueryHandler.cs`

- [ ] **Step 1: Write the failing cache test**

Create `tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/GetPlayerStatisticsQueryHandlerCacheTests.cs`. Mock `IHybridCacheService`; assert the handler delegates to `GetOrCreateAsync` with the expected key + tag. Mirror the mocking flavor of `DeletePlayRecordCommandHandlerTests.cs` (Moq). Because the current handler takes `MeepleAiDbContext` (not easily mockable), the cache test asserts on the cache interaction: set up the mock `GetOrCreateAsync` to invoke its factory and return its value, construct the handler with an in-memory `MeepleAiDbContext` (via `TestDbContextFactory.CreateInMemoryDbContext()`, see `GetPlayRecordQueryHandlerTests.cs`), and `Verify` the key/tag.

```csharp
[Fact]
[Trait("Category", "Unit")]
public async Task Handle_UsesCacheKeyAndTagPerUserAndRange()
{
    var userId = Guid.NewGuid();
    var ctx = TestDbContextFactory.CreateInMemoryDbContext();
    var cache = new Mock<IHybridCacheService>();
    cache.Setup(c => c.GetOrCreateAsync(
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task<PlayerStatisticsDto>>>(),
            It.IsAny<string[]>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
        .Returns<string, Func<CancellationToken, Task<PlayerStatisticsDto>>, string[], TimeSpan?, CancellationToken>(
            (key, factory, tags, exp, ct) => factory(ct));

    var handler = new GetPlayerStatisticsQueryHandler(ctx, cache.Object);
    await handler.Handle(new GetPlayerStatisticsQuery(userId, null, null), CancellationToken.None);

    cache.Verify(c => c.GetOrCreateAsync(
        It.Is<string>(k => k == $"player-stats:{userId}:0:0"),
        It.IsAny<Func<CancellationToken, Task<PlayerStatisticsDto>>>(),
        It.Is<string[]>(t => t.Contains($"player-stats:{userId}")),
        It.Is<TimeSpan?>(e => e == TimeSpan.FromMinutes(5)),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

- [ ] **Step 2: Run it — FAIL** (ctor has no cache param yet)

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetPlayerStatisticsQueryHandlerCacheTests"`
Expected: FAIL (compile — `GetPlayerStatisticsQueryHandler` has no 2-arg ctor).

- [ ] **Step 3: Refactor the handler — inject cache, wrap, extract compute**

- Add `using Api.Services;`.
- Add `private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);` and an `IHybridCacheService _cache` field + ctor param (mirror `GetGameLeaderboardQueryHandler`).
- Rename the current `Handle` body to `private async Task<PlayerStatisticsDto> ComputeAsync(GetPlayerStatisticsQuery query, CancellationToken cancellationToken)` (keep its logic verbatim).
- New `Handle`:

```csharp
public async Task<PlayerStatisticsDto> Handle(GetPlayerStatisticsQuery query, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(query);

    var key = $"player-stats:{query.UserId}:{query.StartDate?.Ticks ?? 0}:{query.EndDate?.Ticks ?? 0}";

    return await _cache.GetOrCreateAsync(
        key,
        ct => ComputeAsync(query, ct),
        tags: [$"player-stats:{query.UserId}"],
        expiration: CacheTtl,
        ct: cancellationToken).ConfigureAwait(false);
}
```

> Move the `ArgumentNullException.ThrowIfNull(query)` out of `ComputeAsync` into `Handle` (above) to avoid double-null-check; keep the rest of `ComputeAsync` exactly as the current body.

- [ ] **Step 4: Run it — PASS**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetPlayerStatisticsQueryHandlerCacheTests"`
Expected: PASS.

- [ ] **Step 5: Run the existing statistics tests — no regression**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetPlayerStatistics"`
Expected: PASS. If any existing test constructs `GetPlayerStatisticsQueryHandler(ctx)` with one arg, update it to pass a cache mock (or a pass-through `IHybridCacheService` test double). Search: `grep -rn "new GetPlayerStatisticsQueryHandler" tests/`.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayerStatisticsQueryHandler.cs tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/GetPlayerStatisticsQueryHandlerCacheTests.cs
git commit -m "feat(play-records): #2438 cache player statistics (HybridCache 5min)"
```

---

### Task 3: Invalidation handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/PlayRecordStatsCacheInvalidationHandler.cs`
- Test: `tests/Api.Tests/BoundedContexts/GameManagement/Application/EventHandlers/PlayRecordStatsCacheInvalidationHandlerTests.cs`

Read `UserActivityCacheInvalidationEventHandler.cs` first for the exact `INotificationHandler` + `IHybridCacheService` injection shape and namespaces.

- [ ] **Step 1: Write the failing tests**

```csharp
// Deleted event → evicts player-stats:{DeletedByUserId} (no lookup)
[Fact]
[Trait("Category", "Unit")]
public async Task Handle_Deleted_EvictsUserStatsTag()
{
    var userId = Guid.NewGuid();
    var ctx = TestDbContextFactory.CreateInMemoryDbContext();
    var cache = new Mock<IHybridCacheService>();
    var handler = new PlayRecordStatsCacheInvalidationHandler(ctx, cache.Object);

    await handler.Handle(new PlayRecordDeletedEvent(Guid.NewGuid(), userId), CancellationToken.None);

    cache.Verify(c => c.RemoveByTagAsync($"player-stats:{userId}", It.IsAny<CancellationToken>()), Times.Once);
}
```

(Completed-event test requires seeding a record in the in-memory ctx so the userId lookup resolves; add it mirroring the integration seed pattern.)

- [ ] **Step 2: Run — FAIL** (handler missing). Run the filter `~PlayRecordStatsCacheInvalidationHandlerTests`. Expected: FAIL (compile).

- [ ] **Step 3: Implement the handler**

```csharp
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Evicts the per-user player-statistics cache tag when a record's Completed/Deleted
/// state changes which Completed records exist for the user (#2438). Created is not a
/// trigger — a Planned record is not counted by GetPlayerStatistics (see ADR).
/// </summary>
internal sealed class PlayRecordStatsCacheInvalidationHandler
    : INotificationHandler<PlayRecordCompletedEvent>,
      INotificationHandler<PlayRecordDeletedEvent>
{
    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;

    public PlayRecordStatsCacheInvalidationHandler(MeepleAiDbContext context, IHybridCacheService cache)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task Handle(PlayRecordCompletedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        // Completed event carries no userId — resolve it (record is not deleted here).
        var userId = await _context.PlayRecords
            .IgnoreQueryFilters()
            .Where(r => r.Id == notification.RecordId)
            .Select(r => r.CreatedByUserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (userId != Guid.Empty)
        {
            await _cache.RemoveByTagAsync($"player-stats:{userId}", cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task Handle(PlayRecordDeletedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await _cache.RemoveByTagAsync($"player-stats:{notification.DeletedByUserId}", cancellationToken).ConfigureAwait(false);
    }
}
```

> Verify the event property names against the actual files (`PlayRecordCompletedEvent.RecordId`, `PlayRecordDeletedEvent.RecordId`/`DeletedByUserId`). Verify MediatR auto-discovery covers this handler (the reference `UserActivityCacheInvalidationEventHandler` has no manual registration → none needed here either; if the reference IS registered manually, mirror that).

- [ ] **Step 4: Run — PASS.** Then `grep`-verify no manual registration gap.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/PlayRecordStatsCacheInvalidationHandler.cs tests/Api.Tests/BoundedContexts/GameManagement/Application/EventHandlers/PlayRecordStatsCacheInvalidationHandlerTests.cs
git commit -m "feat(play-records): #2438 evict stats cache on Completed/Deleted"
```

---

### Task 4: Integration test (cache + invalidation end-to-end)

**Files:**
- Create: `tests/Api.Tests/Integration/GameManagement/PlayerStatisticsCacheTests.cs`

Mirror `PlayRecordCommandTests.cs` for the Testcontainers fixture + `SendInScopeAsync` + seed helpers. Register a REAL `IHybridCacheService` (or the test container's) in the integration service collection if not already present.

- [ ] **Step 1: Write the test**

Scenario: seed a completed record for a user → query stats (miss, computes) → complete a second record (raises `PlayRecordCompletedEvent` → invalidation) → query stats again → the new record is reflected (cache was evicted, not stale).

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task Stats_AreInvalidated_WhenANewRecordCompletes()
{
    var userId = await SeedTestUserAsync();
    var r1 = await CreateCompletedRecordAsync(userId);     // helper: create+start+complete
    var first = await SendInScopeAsync(new GetPlayerStatisticsQuery(userId, null, null));
    first.TotalSessions.Should().Be(1);

    await CreateCompletedRecordAsync(userId);              // raises Completed → invalidation
    var second = await SendInScopeAsync(new GetPlayerStatisticsQuery(userId, null, null));
    second.TotalSessions.Should().Be(2);                  // NOT stale → cache was evicted
}
```

> Build `CreateCompletedRecordAsync` from the existing `CreateTestRecordAsync` + `StartPlayRecordCommand` + `CompletePlayRecordCommand` helpers in `PlayRecordCommandTests.cs`. If the integration DI lacks `IHybridCacheService`, add it from `InfrastructureServiceExtensions` or register `HybridCacheService` in the test service collection.

- [ ] **Step 2: Run — PASS** (Docker required)

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PlayerStatisticsCacheTests"`
Expected: PASS.

- [ ] **Step 3: Run the full PlayRecord + statistics suite — no regression**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PlayRecord|FullyQualifiedName~PlayerStatistics"`
Expected: ALL PASS.

- [ ] **Step 4: Commit**

```bash
git add tests/Api.Tests/Integration/GameManagement/PlayerStatisticsCacheTests.cs
git commit -m "test(play-records): #2438 stats cache invalidation integration"
```

---

## Self-Review

**1. Spec coverage (PR-B = Redis cache + invalidation + ADR):**
- HybridCache 5-min on GetPlayerStatistics → Task 2. ✅
- Per-user tag invalidation on the stats-changing events → Task 3 (Completed + Deleted). ✅
- ADR (key schema + triggers) → Task 1. ✅
- Created-event correctly excluded (Planned ≠ counted) — documented, not a gap. ✅

**2. Placeholder scan:** Event property names + MediatR auto-discovery + integration DI presence are explicit read-then-verify steps against named reference files, with fallbacks — not vague TODOs. The Completed-event unit test seed is flagged as "mirror the integration seed".

**3. Type consistency:** `IHybridCacheService.GetOrCreateAsync<PlayerStatisticsDto>` (record = class ✅). Key `player-stats:{userId}:{ticks}:{ticks}` and tag `player-stats:{userId}` consistent across Tasks 1, 2, 3. `ComputeAsync` signature matches the wrapped `Handle`. `RemoveByTagAsync(string, ct)` matches the interface.

**Risk:** the Completed-event userId lookup adds one indexed `SELECT` per completion — negligible. If `GetPlayerStatisticsQuery` constructor arity differs from `(userId, startDate, endDate)`, align the test calls to the real record (read `GetPlayerStatisticsQuery.cs`).
