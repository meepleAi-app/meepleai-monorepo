# `/discover` Composite Endpoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `GET /api/v1/discover` composite endpoint returning 5 cross-BC discovery rows (newGames, topAgents, recommendedToolkits, recentKb, topContributors), with resilient partial-success failure semantics, to unblock V2 migration Wave 3 child #687.

**Architecture:** New `Discover` BC owns the composer; 5 sub-queries each live in their native BC (`SharedGameCatalog`, `KnowledgeBase`-x2, `GameToolkit`, `Authentication`). Composer dispatches via MediatR `Task.WhenAll` with per-row try-catch. HybridCache wraps each row with row-specific TTL.

**Tech Stack:** .NET 9 · ASP.NET Minimal APIs · MediatR · EF Core 9 (PostgreSQL 16) · HybridCache · xUnit + FluentAssertions + NSubstitute · Next.js 16 · React Query · Zod

**Spec:** `docs/superpowers/specs/2026-05-06-discover-endpoints-design.md`

---

## File Structure

### Backend — Created

| File | Responsibility |
|------|----------------|
| `apps/api/src/Api/BoundedContexts/Discover/Application/Queries/GetDiscoverData/GetDiscoverDataQuery.cs` | Composer query record (input: `int Limit`) |
| `apps/api/src/Api/BoundedContexts/Discover/Application/Queries/GetDiscoverData/DiscoverDto.cs` | Composite DTO with 5 row arrays |
| `apps/api/src/Api/BoundedContexts/Discover/Application/Queries/GetDiscoverData/GetDiscoverDataHandler.cs` | Composer with `Task.WhenAll` + per-row try-catch + Prometheus counter |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetNewGames/GetNewGamesQuery.cs` | Sub-query record |
| `…/GetNewGames/NewGameDto.cs` | Per-row DTO |
| `…/GetNewGames/GetNewGamesHandler.cs` | Sub-query handler |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetTopAgents/GetTopAgentsQuery.cs` | Sub-query record |
| `…/GetTopAgents/TopAgentDto.cs` | Per-row DTO |
| `…/GetTopAgents/GetTopAgentsHandler.cs` | Sub-query handler (uses AgentSessionEntity proxy) |
| `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Queries/GetRecommendedToolkits/GetRecommendedToolkitsQuery.cs` | Sub-query record |
| `…/GetRecommendedToolkits/RecommendedToolkitDto.cs` | Per-row DTO |
| `…/GetRecommendedToolkits/GetRecommendedToolkitsHandler.cs` | Sub-query handler |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetRecentKbDocuments/GetRecentKbDocumentsQuery.cs` | Sub-query record |
| `…/GetRecentKbDocuments/RecentKbDocDto.cs` | Per-row DTO |
| `…/GetRecentKbDocuments/GetRecentKbDocumentsHandler.cs` | Sub-query handler |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetTopContributors/GetTopContributorsQuery.cs` | Sub-query record |
| `…/GetTopContributors/TopContributorDto.cs` | Per-row DTO |
| `…/GetTopContributors/GetTopContributorsHandler.cs` | Sub-query handler |
| `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetNewGamesHandlerTests.cs` | Unit tests |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetTopAgentsHandlerTests.cs` | Unit tests |
| `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Queries/GetRecommendedToolkitsHandlerTests.cs` | Unit tests |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetRecentKbDocumentsHandlerTests.cs` | Unit tests |
| `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Queries/GetTopContributorsHandlerTests.cs` | Unit tests |
| `apps/api/tests/Api.Tests/BoundedContexts/Discover/Application/Queries/GetDiscoverDataHandlerTests.cs` | Composer unit test (NSubstitute IMediator) |
| `apps/api/tests/Api.Tests/Integration/DiscoverEndpointIntegrationTests.cs` | E2E test (Testcontainers) |

### Backend — Modified

| File | Change |
|------|--------|
| `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs` (or new `DiscoverEndpoints.cs`) | Register `MapDiscoverEndpoints` (decision in Task 9) |
| `apps/api/src/Api/Program.cs` | (Optional) Register Prometheus counter `discover_row_failure_total` if not auto-registered |

### Frontend — Created

| File | Responsibility |
|------|----------------|
| `apps/web/src/lib/api/schemas/discover.schemas.ts` | 6 Zod schemas (5 row + composite) |
| `apps/web/src/lib/api/clients/discoverClient.ts` | `getDiscover(limit?)` method |
| `apps/web/src/hooks/queries/useDiscover.ts` | React Query hook (10min staleTime) |

### Frontend — Modified

| File | Change |
|------|--------|
| `apps/web/src/lib/api/schemas/index.ts` | Re-export discover schemas |
| `apps/web/src/lib/api/clients/index.ts` | Re-export discoverClient |
| `apps/web/src/hooks/queries/index.ts` | Re-export useDiscover |

---

## Task 0: Verify entity assumptions (BLOCKER)

This task validates the spec's §5 assumptions before implementation. If any of the assumptions fail, the affected sub-query needs scope adjustment (defer or proxy strategy).

**No commit at this stage** — this is investigation, results inform Tasks 2-6.

- [ ] **Step 1: Verify `GameFaqEntity` exists and has `AuthorId`**

```bash
cd D:/Repositories/meepleai-monorepo-main
grep -n "class GameFaqEntity\|AuthorId\|public Guid" apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/GameFaqEntity.cs | head -20
```

Expected: file exists; class has properties including `AuthorId` (Guid). If `AuthorId` is missing or named differently (e.g., `CreatedBy`), note the actual name for Task 6.

- [ ] **Step 2: Verify `AgentSessionEntity` has `AgentId` + `CreatedBy`/`UserId`**

```bash
grep -n "class AgentSessionEntity\|AgentId\|UserId\|CreatedBy" apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/AgentSessionEntity.cs | head -20
```

Expected: file exists; class has `AgentId` and a user-foreign-key field. Note the actual user-fk field name (likely `UserId`, may be `CreatedBy`) for Tasks 3 and 6.

- [ ] **Step 3: Confirm DbContext registers all referenced DbSets**

```bash
grep -n "DbSet<.*Faq\|DbSet<.*Agent\|DbSet<SharedGameEntity\|DbSet<GameToolkit\|DbSet<PdfDocument\|DbSet<VectorDocument\|DbSet<UserEntity" apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs | head -15
```

Note the **actual DbSet property names** for use in handler LINQ (e.g., `_dbContext.GameFaqs` vs `_dbContext.Faqs`). The exact names go into Tasks 2-7.

- [ ] **Step 4: Decision point — Top Agents row**

Based on Step 2 findings, choose:

- **Approach A** (proxy via AgentSessionEntity): aggregate by `AgentId`, count distinct `UserId` per `AgentId` as install proxy. Use this if `AgentSessionEntity.AgentId` exists and represents an agent definition reference.
- **Approach B** (skip in MVP): if no clean proxy is available, return empty array `[]` for `topAgents` row in MVP. Document as out-of-scope follow-up. Tasks 3 (handler) and Task 7 (composer wiring) become trivial (handler returns `Array.Empty<TopAgentDto>()`).

Document the decision in a comment in `GetTopAgentsQuery.cs` (created in Task 3).

- [ ] **Step 5: Document findings**

Write a short summary (3-5 lines) in this file by editing this Task 0 section, replacing this checkbox with: `- [x] Findings: <field-name-corrections>; agent install approach: <A|B>`. Investigation is then complete.

---

## Task 1: Discover BC scaffolding + composite DTO

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Discover/Application/Queries/GetDiscoverData/GetDiscoverDataQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Discover/Application/Queries/GetDiscoverData/DiscoverDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/Discover/Application/Queries/GetDiscoverData/GetDiscoverDataHandler.cs` (skeleton — full impl in Task 7)

- [ ] **Step 1: Create GetDiscoverDataQuery**

`apps/api/src/Api/BoundedContexts/Discover/Application/Queries/GetDiscoverData/GetDiscoverDataQuery.cs`:

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;

internal sealed record GetDiscoverDataQuery(int Limit) : IQuery<DiscoverDto>;
```

- [ ] **Step 2: Create composite DTO (referencing per-row DTOs that will be created in Tasks 2-6)**

`DiscoverDto.cs`:

```csharp
using Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

namespace Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;

internal sealed record DiscoverDto(
    IReadOnlyList<NewGameDto> NewGames,
    IReadOnlyList<TopAgentDto> TopAgents,
    IReadOnlyList<RecommendedToolkitDto> RecommendedToolkits,
    IReadOnlyList<RecentKbDocDto> RecentKb,
    IReadOnlyList<TopContributorDto> TopContributors
);
```

(File will not compile until per-row DTOs are created in Tasks 2-6. That is expected and OK — Task 1 is scaffolding only. The composer in Step 3 returns empty arrays so the project compiles end-to-end after Tasks 2-6 land.)

- [ ] **Step 3: Create skeleton Handler returning empty composite**

`GetDiscoverDataHandler.cs`:

```csharp
using Api.SharedKernel.Application.Interfaces;
using Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

namespace Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;

internal sealed class GetDiscoverDataHandler : IQueryHandler<GetDiscoverDataQuery, DiscoverDto>
{
    private readonly ILogger<GetDiscoverDataHandler> _logger;

    public GetDiscoverDataHandler(ILogger<GetDiscoverDataHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<DiscoverDto> Handle(GetDiscoverDataQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // Skeleton — full implementation in Task 7
        return Task.FromResult(new DiscoverDto(
            NewGames: Array.Empty<NewGameDto>(),
            TopAgents: Array.Empty<TopAgentDto>(),
            RecommendedToolkits: Array.Empty<RecommendedToolkitDto>(),
            RecentKb: Array.Empty<RecentKbDocDto>(),
            TopContributors: Array.Empty<TopContributorDto>()
        ));
    }
}
```

(Will not compile until Tasks 2-6 create the per-row DTOs.)

- [ ] **Step 4: Defer build verification to Task 6**

The composite DTO references types that don't exist yet. Tasks 2-6 will create them in dependency order. Build will succeed after Task 6 completes.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Discover/
git commit -m "feat(discover): #728 BC scaffolding + composite DTO skeleton

Creates new Discover BC with empty composer that will be filled
in Task 7 once all 5 per-row sub-queries land. Composite DTO
references types created in Tasks 2-6.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: G2 sub-query — `GetNewGamesQuery` (newGames row)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetNewGames/GetNewGamesQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetNewGames/NewGameDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetNewGames/GetNewGamesHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetNewGamesHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetNewGamesHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetNewGamesHandler> _logger;
    private readonly GetNewGamesHandler _handler;

    public GetNewGamesHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetNewGamesHandler>>();
        _handler = new GetNewGamesHandler(_dbContext, _logger);
    }

    [Fact]
    public async Task Handle_ReturnsLatestGamesByCreatedAtDesc()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            _dbContext.SharedGames.Add(new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Name = $"Game {i}",
                CreatedAt = DateTime.UtcNow.AddDays(-i),
                IsDeleted = false
            });
        }
        await _dbContext.SaveChangesAsync();

        var query = new GetNewGamesQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(10);
        result.Select(r => r.CreatedAt).Should().BeInDescendingOrder();
        result.First().Name.Should().Be("Game 0"); // most recent
    }

    [Fact]
    public async Task Handle_ExcludesSoftDeletedGames()
    {
        _dbContext.SharedGames.Add(new SharedGameEntity { Id = Guid.NewGuid(), Name = "Visible", CreatedAt = DateTime.UtcNow, IsDeleted = false });
        _dbContext.SharedGames.Add(new SharedGameEntity { Id = Guid.NewGuid(), Name = "Hidden", CreatedAt = DateTime.UtcNow, IsDeleted = true });
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetNewGamesQuery(10), CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().Name.Should().Be("Visible");
    }

    [Fact]
    public async Task Handle_EmptyCatalog_ReturnsEmptyList()
    {
        var result = await _handler.Handle(new GetNewGamesQuery(10), CancellationToken.None);
        result.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run tests to verify compile fail**

```bash
cd apps/api/src/Api && dotnet build
```

Expected: missing `GetNewGamesQuery`, `NewGameDto`, `GetNewGamesHandler` types.

- [ ] **Step 3: Create Query**

`GetNewGamesQuery.cs`:

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

internal sealed record GetNewGamesQuery(int Limit) : IQuery<IReadOnlyList<NewGameDto>>;
```

- [ ] **Step 4: Create DTO**

`NewGameDto.cs`:

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

internal sealed record NewGameDto(
    Guid Id,
    string Name,
    string? ImageUrl,
    string? Publisher,
    DateTime CreatedAt,
    double? RatingAverage
);
```

(If `SharedGameEntity` doesn't expose `ImageUrl`/`Publisher`/`RatingAverage`, omit those fields from the DTO. Inspect the entity in Task 0 step 3 output.)

- [ ] **Step 5: Create Handler**

`GetNewGamesHandler.cs`:

```csharp
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

internal sealed class GetNewGamesHandler : IQueryHandler<GetNewGamesQuery, IReadOnlyList<NewGameDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetNewGamesHandler> _logger;

    public GetNewGamesHandler(MeepleAiDbContext dbContext, ILogger<GetNewGamesHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<NewGameDto>> Handle(GetNewGamesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = Math.Clamp(query.Limit, 1, 20);

        return await _dbContext.SharedGames.AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderByDescending(g => g.CreatedAt)
            .Take(limit)
            .Select(g => new NewGameDto(
                g.Id,
                g.Name,
                g.ImageUrl,           // adjust if property doesn't exist
                g.Publisher,          // adjust if property doesn't exist
                g.CreatedAt,
                g.RatingAverage       // adjust if property doesn't exist
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
```

(If any of `ImageUrl`/`Publisher`/`RatingAverage` is not on `SharedGameEntity`, replace with `null` literal in the DTO constructor and remove from DTO record signature in Step 4.)

- [ ] **Step 6: Run tests, expect pass**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~GetNewGamesHandler" --logger "console;verbosity=normal"
```

Expected: 3/3 tests pass.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetNewGames/ apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetNewGamesHandlerTests.cs
git commit -m "feat(discover): #728 GetNewGamesQuery handler

Returns latest non-deleted shared games ordered by createdAt desc,
clamped to [1, 20]. First sub-query of 5 for the /discover composite.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: G2 sub-query — `GetTopAgentsQuery` (topAgents row)

**Approach decided in Task 0 Step 4.** This task assumes Approach A (AgentSessionEntity proxy). If Approach B (skip in MVP) was chosen, simplify Step 5 to `return Array.Empty<TopAgentDto>()` and skip tests asserting ranking.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetTopAgents/GetTopAgentsQuery.cs`
- Create: `…/GetTopAgents/TopAgentDto.cs`
- Create: `…/GetTopAgents/GetTopAgentsHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetTopAgentsHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task Handle_ApproachA_RanksByDistinctUserCount()
{
    // Arrange: create 3 agents (referenced by AgentId guid). Seed AgentSession rows:
    //   agentA: 5 distinct users → installCount=5
    //   agentB: 2 distinct users (1 user has 3 sessions) → installCount=2
    //   agentC: 0 sessions → installCount=0
    // Act: query Limit=10
    // Assert:
    //   - 3 items returned
    //   - ordered by installCount desc → agentA, agentB, agentC

    // Concrete seed (adapt field names to Task 0 findings):
    var agentA = Guid.NewGuid();
    var agentB = Guid.NewGuid();
    var agentC = Guid.NewGuid();

    for (int i = 0; i < 5; i++)
        _dbContext.AgentSessions.Add(new AgentSessionEntity { Id = Guid.NewGuid(), AgentId = agentA, UserId = Guid.NewGuid() /* fill required fields */ });

    var sharedUserB = Guid.NewGuid();
    _dbContext.AgentSessions.Add(new AgentSessionEntity { Id = Guid.NewGuid(), AgentId = agentB, UserId = sharedUserB });
    _dbContext.AgentSessions.Add(new AgentSessionEntity { Id = Guid.NewGuid(), AgentId = agentB, UserId = sharedUserB });   // same user
    _dbContext.AgentSessions.Add(new AgentSessionEntity { Id = Guid.NewGuid(), AgentId = agentB, UserId = Guid.NewGuid() }); // different user

    // agentC has no sessions — must still appear if reachable (depends on impl); for MVP, agents with zero distinct-users may be excluded
    await _dbContext.SaveChangesAsync();

    var result = await _handler.Handle(new GetTopAgentsQuery(10), CancellationToken.None);

    result.Should().BeInDescendingOrder(r => r.InstallCount);
    result.First().Id.Should().Be(agentA);
    result.First().InstallCount.Should().Be(5);
}

[Fact]
public async Task Handle_EmptyState_ReturnsEmptyList()
{
    var result = await _handler.Handle(new GetTopAgentsQuery(10), CancellationToken.None);
    result.Should().BeEmpty();
}
```

(Adapt `AgentSessionEntity` field names per Task 0 findings. Required fields like `GameId`, `CreatedAt`, etc., must be filled to satisfy EF non-null constraints — copy from a passing seed in another KB test like `GetGameDocumentsHandlerTests` or `RemoveDocumentFromKbCommandHandlerTests`.)

- [ ] **Step 2: Build to verify compile failure**

- [ ] **Step 3: Create Query**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;

internal sealed record GetTopAgentsQuery(int Limit) : IQuery<IReadOnlyList<TopAgentDto>>;
```

- [ ] **Step 4: Create DTO**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;

internal sealed record TopAgentDto(
    Guid Id,
    string Name,
    string GameName,
    string AgentType,
    int InstallCount,
    DateTime CreatedAt
);
```

(`Name`, `GameName`, `AgentType` source: derive from `AgentSessionEntity` if those properties exist on the entity, OR set to `string.Empty` / sensible defaults in MVP. Final mapping decided in Step 5 implementation based on Task 0 inspection.)

- [ ] **Step 5: Create Handler (Approach A)**

```csharp
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;

internal sealed class GetTopAgentsHandler : IQueryHandler<GetTopAgentsQuery, IReadOnlyList<TopAgentDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetTopAgentsHandler> _logger;

    public GetTopAgentsHandler(MeepleAiDbContext dbContext, ILogger<GetTopAgentsHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<TopAgentDto>> Handle(GetTopAgentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = Math.Clamp(query.Limit, 1, 20);

        // Approach A: aggregate AgentSessionEntity by AgentId; install count = distinct UserId
        var aggregated = await _dbContext.AgentSessions.AsNoTracking()
            .GroupBy(s => s.AgentId)
            .Select(g => new
            {
                AgentId = g.Key,
                InstallCount = g.Select(s => s.UserId).Distinct().Count(),
                LatestActivity = g.Max(s => s.CreatedAt)
            })
            .OrderByDescending(x => x.InstallCount)
            .ThenByDescending(x => x.LatestActivity)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // MVP: name/gameName/agentType are not in AgentSessionEntity. Return empty strings or
        // a follow-up issue can join the proper agent definition entity once it lands.
        return aggregated.Select(x => new TopAgentDto(
            Id: x.AgentId,
            Name: string.Empty,        // TODO: join agent definition when entity exists (out of scope this PR)
            GameName: string.Empty,
            AgentType: string.Empty,
            InstallCount: x.InstallCount,
            CreatedAt: x.LatestActivity
        )).ToList();
    }
}
```

(If Task 0 Step 4 chose Approach B, the entire body becomes `return Task.FromResult<IReadOnlyList<TopAgentDto>>(Array.Empty<TopAgentDto>());` and a comment explaining why.)

- [ ] **Step 6: Run tests**

```bash
dotnet test --filter "FullyQualifiedName~GetTopAgentsHandler" --logger "console;verbosity=normal"
```

Expected: tests pass with the chosen approach.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetTopAgents/ apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetTopAgentsHandlerTests.cs
git commit -m "feat(discover): #728 GetTopAgentsQuery handler (Approach A — AgentSession proxy)

Aggregates AgentSessionEntity by AgentId, install count =
distinct UserId. Name/GameName fields empty in MVP (no proper
agent definition entity yet). Follow-up issue to join when
agent BC introduces install model.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: G3 sub-query — `GetRecommendedToolkitsQuery`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Queries/GetRecommendedToolkits/GetRecommendedToolkitsQuery.cs`
- Create: `…/GetRecommendedToolkits/RecommendedToolkitDto.cs`
- Create: `…/GetRecommendedToolkits/GetRecommendedToolkitsHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Queries/GetRecommendedToolkitsHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task Handle_FiltersOutPrivateToolkits()
{
    // Seed 3 public + 2 private toolkits
    for (int i = 0; i < 3; i++)
        _dbContext.GameToolkits.Add(new GameToolkitEntity { Id = Guid.NewGuid(), Name = $"Public {i}", IsPublic = true, InstallCount = i, CreatedAt = DateTime.UtcNow });
    for (int i = 0; i < 2; i++)
        _dbContext.GameToolkits.Add(new GameToolkitEntity { Id = Guid.NewGuid(), Name = $"Private {i}", IsPublic = false, InstallCount = 100, CreatedAt = DateTime.UtcNow });
    await _dbContext.SaveChangesAsync();

    var result = await _handler.Handle(new GetRecommendedToolkitsQuery(10), CancellationToken.None);

    result.Should().HaveCount(3);
    result.Select(r => r.Name).Should().NotContain(n => n.StartsWith("Private"));
}

[Fact]
public async Task Handle_RanksByInstallCountDesc()
{
    _dbContext.GameToolkits.Add(new GameToolkitEntity { Id = Guid.NewGuid(), Name = "Low", IsPublic = true, InstallCount = 1, CreatedAt = DateTime.UtcNow });
    _dbContext.GameToolkits.Add(new GameToolkitEntity { Id = Guid.NewGuid(), Name = "High", IsPublic = true, InstallCount = 100, CreatedAt = DateTime.UtcNow });
    await _dbContext.SaveChangesAsync();

    var result = await _handler.Handle(new GetRecommendedToolkitsQuery(10), CancellationToken.None);

    result.Should().HaveCount(2);
    result.First().Name.Should().Be("High");
}
```

(Inspect `GameToolkitEntity` first; `IsPublic` and `InstallCount` may have different names. Adapt.)

- [ ] **Step 2: Build for compile fail**

- [ ] **Step 3: Create Query**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

internal sealed record GetRecommendedToolkitsQuery(int Limit) : IQuery<IReadOnlyList<RecommendedToolkitDto>>;
```

- [ ] **Step 4: Create DTO**

```csharp
namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

internal sealed record RecommendedToolkitDto(
    Guid Id,
    string Name,
    string GameName,
    string Version,
    int InstallCount,
    DateTime CreatedAt
);
```

(Adapt fields based on `GameToolkitEntity` shape inspection.)

- [ ] **Step 5: Create Handler**

```csharp
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

internal sealed class GetRecommendedToolkitsHandler : IQueryHandler<GetRecommendedToolkitsQuery, IReadOnlyList<RecommendedToolkitDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetRecommendedToolkitsHandler> _logger;

    public GetRecommendedToolkitsHandler(MeepleAiDbContext dbContext, ILogger<GetRecommendedToolkitsHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<RecommendedToolkitDto>> Handle(GetRecommendedToolkitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = Math.Clamp(query.Limit, 1, 20);

        return await _dbContext.GameToolkits.AsNoTracking()
            .Where(t => t.IsPublic)
            .OrderByDescending(t => t.InstallCount)
            .ThenByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new RecommendedToolkitDto(
                t.Id,
                t.Name,
                t.GameName ?? string.Empty,    // adapt to actual property name
                t.Version ?? string.Empty,
                t.InstallCount,
                t.CreatedAt
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
```

- [ ] **Step 6: Run tests, commit**

```bash
dotnet test --filter "FullyQualifiedName~GetRecommendedToolkitsHandler" --logger "console;verbosity=normal"
```

Then:

```bash
git add apps/api/src/Api/BoundedContexts/GameToolkit/Application/Queries/GetRecommendedToolkits/ apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Queries/GetRecommendedToolkitsHandlerTests.cs
git commit -m "feat(discover): #728 GetRecommendedToolkitsQuery handler

Public toolkits ordered by install_count desc, ties by created_at.
MVP simple ranking (no time decay or KB-context awareness).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: G3 sub-query — `GetRecentKbDocumentsQuery`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetRecentKbDocuments/GetRecentKbDocumentsQuery.cs`
- Create: `…/GetRecentKbDocuments/RecentKbDocDto.cs`
- Create: `…/GetRecentKbDocuments/GetRecentKbDocumentsHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetRecentKbDocumentsHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task Handle_FiltersOutNonReadyDocs()
{
    var ready1 = SeedPdfDoc(state: "Ready", isPublic: true);
    var embedding = SeedPdfDoc(state: "Embedding", isPublic: true);
    var failed = SeedPdfDoc(state: "Failed", isPublic: true);
    await _dbContext.SaveChangesAsync();

    var result = await _handler.Handle(new GetRecentKbDocumentsQuery(10), CancellationToken.None);

    result.Should().HaveCount(1);
    result.Single().Id.Should().Be(ready1.Id);
}

[Fact]
public async Task Handle_FiltersOutPrivateDocs()
{
    SeedPdfDoc(state: "Ready", isPublic: false);
    var publicDoc = SeedPdfDoc(state: "Ready", isPublic: true);
    await _dbContext.SaveChangesAsync();

    var result = await _handler.Handle(new GetRecentKbDocumentsQuery(10), CancellationToken.None);

    result.Should().HaveCount(1);
    result.Single().Id.Should().Be(publicDoc.Id);
}

[Fact]
public async Task Handle_OrdersByIndexedAtDesc()
{
    var olderDoc = SeedPdfDoc(state: "Ready", isPublic: true, indexedAt: DateTime.UtcNow.AddDays(-5));
    var newerDoc = SeedPdfDoc(state: "Ready", isPublic: true, indexedAt: DateTime.UtcNow.AddDays(-1));
    await _dbContext.SaveChangesAsync();

    var result = await _handler.Handle(new GetRecentKbDocumentsQuery(10), CancellationToken.None);

    result.First().Id.Should().Be(newerDoc.Id);
}

private PdfDocumentEntity SeedPdfDoc(string state, bool isPublic, DateTime? indexedAt = null)
{
    var doc = new PdfDocumentEntity
    {
        Id = Guid.NewGuid(),
        FileName = "test.pdf",
        ProcessingState = state,
        IsPublic = isPublic,
        UploadedAt = DateTime.UtcNow,
        Language = "en",
        DocumentCategory = "Rulebook",
        UploadedByUserId = Guid.NewGuid(),
        FilePath = "/tmp/test.pdf"
    };
    _dbContext.PdfDocuments.Add(doc);

    var vd = new VectorDocumentEntity
    {
        Id = Guid.NewGuid(),
        PdfDocumentId = doc.Id,
        GameId = Guid.NewGuid(),
        ChunkCount = 10,
        IndexedAt = indexedAt ?? DateTime.UtcNow,
        IndexingStatus = "Completed",
        Language = "en"
    };
    _dbContext.VectorDocuments.Add(vd);
    return doc;
}
```

- [ ] **Step 2: Build to verify compile fail**

- [ ] **Step 3: Create Query**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;

internal sealed record GetRecentKbDocumentsQuery(int Limit) : IQuery<IReadOnlyList<RecentKbDocDto>>;
```

- [ ] **Step 4: Create DTO**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;

internal sealed record RecentKbDocDto(
    Guid Id,
    string Title,
    string GameName,
    string DocumentCategory,
    DateTime IndexedAt,
    string Language
);
```

- [ ] **Step 5: Create Handler**

```csharp
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;

internal sealed class GetRecentKbDocumentsHandler : IQueryHandler<GetRecentKbDocumentsQuery, IReadOnlyList<RecentKbDocDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetRecentKbDocumentsHandler> _logger;

    public GetRecentKbDocumentsHandler(MeepleAiDbContext dbContext, ILogger<GetRecentKbDocumentsHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<RecentKbDocDto>> Handle(GetRecentKbDocumentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = Math.Clamp(query.Limit, 1, 20);

        return await (
            from pdf in _dbContext.PdfDocuments.AsNoTracking()
            join vd in _dbContext.VectorDocuments.AsNoTracking()
                on pdf.Id equals vd.PdfDocumentId
            join game in _dbContext.SharedGames.AsNoTracking()
                on vd.GameId equals game.Id into gameJoin
            from game in gameJoin.DefaultIfEmpty()
            where pdf.IsPublic && pdf.ProcessingState == "Ready" && vd.IndexedAt != null
            orderby vd.IndexedAt descending
            select new RecentKbDocDto(
                pdf.Id,
                pdf.FileName,
                game != null ? game.Name : string.Empty,
                pdf.DocumentCategory,
                vd.IndexedAt!.Value,
                pdf.Language
            )
        ).Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 6: Run tests, commit**

```bash
dotnet test --filter "FullyQualifiedName~GetRecentKbDocumentsHandler" --logger "console;verbosity=normal"
```

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetRecentKbDocuments/ apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetRecentKbDocumentsHandlerTests.cs
git commit -m "feat(discover): #728 GetRecentKbDocumentsQuery handler

Public + Ready KB docs ordered by indexedAt desc, joined to
SharedGame for gameName field. Cross-game aggregation.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: G4 sub-query — `GetTopContributorsQuery`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetTopContributors/GetTopContributorsQuery.cs`
- Create: `…/GetTopContributors/TopContributorDto.cs`
- Create: `…/GetTopContributors/GetTopContributorsHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Queries/GetTopContributorsHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task Handle_RanksByEqualWeightSum()
{
    var u1 = Guid.NewGuid();
    var u2 = Guid.NewGuid();
    var u3 = Guid.NewGuid();
    SeedUser(u1, "Alice");
    SeedUser(u2, "Bob");
    SeedUser(u3, "Charlie");
    // u1: 10 faqs, 2 kb, 1 agent → 13
    // u2: 0 faqs, 8 kb, 5 agent → 13
    // u3: 5 faqs, 3 kb, 4 agent → 12
    SeedFaqs(u1, 10);
    SeedKbUploads(u1, 2);
    SeedAgentSessions(u1, 1);
    SeedKbUploads(u2, 8);
    SeedAgentSessions(u2, 5);
    SeedFaqs(u3, 5);
    SeedKbUploads(u3, 3);
    SeedAgentSessions(u3, 4);
    await _dbContext.SaveChangesAsync();

    var result = await _handler.Handle(new GetTopContributorsQuery(10), CancellationToken.None);

    result.Should().HaveCount(3);
    result.First().ContributionCount.Should().Be(13);
    result.Last().ContributionCount.Should().Be(12);
}

[Fact]
public async Task Handle_ExcludesZeroContribution()
{
    var u4 = Guid.NewGuid();
    SeedUser(u4, "NoContributions");
    await _dbContext.SaveChangesAsync();

    var result = await _handler.Handle(new GetTopContributorsQuery(10), CancellationToken.None);

    result.Should().BeEmpty();
}

[Fact]
public async Task Handle_ExcludesSoftDeletedUsers()
{
    var u5 = Guid.NewGuid();
    SeedUser(u5, "Deleted", isDeleted: true);
    SeedFaqs(u5, 50);
    await _dbContext.SaveChangesAsync();

    var result = await _handler.Handle(new GetTopContributorsQuery(10), CancellationToken.None);

    result.Should().BeEmpty();
}

private void SeedUser(Guid id, string name, bool isDeleted = false)
{
    _dbContext.Users.Add(new UserEntity
    {
        Id = id,
        DisplayName = name,
        Email = $"{name}@test.com",
        IsDeleted = isDeleted
        // fill required fields per UserEntity shape
    });
}

private void SeedFaqs(Guid authorId, int count)
{
    for (int i = 0; i < count; i++)
        _dbContext.GameFaqs.Add(new GameFaqEntity { Id = Guid.NewGuid(), AuthorId = authorId, /* fill */ });
}

private void SeedKbUploads(Guid uploaderId, int count)
{
    for (int i = 0; i < count; i++)
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = uploaderId,
            IsPublic = true,
            FileName = "x.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            FilePath = "/tmp/x.pdf"
        });
}

private void SeedAgentSessions(Guid userId, int distinctAgents)
{
    for (int i = 0; i < distinctAgents; i++)
        _dbContext.AgentSessions.Add(new AgentSessionEntity { Id = Guid.NewGuid(), AgentId = Guid.NewGuid(), UserId = userId /* fill required fields */ });
}
```

(`GameFaqEntity.AuthorId`, `UserEntity.IsDeleted`, `AgentSessionEntity.UserId` field names per Task 0 findings.)

- [ ] **Step 2: Build to verify compile fail**

- [ ] **Step 3: Create Query**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;

internal sealed record GetTopContributorsQuery(int Limit) : IQuery<IReadOnlyList<TopContributorDto>>;
```

- [ ] **Step 4: Create DTO**

```csharp
namespace Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;

internal sealed record TopContributorDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    int ContributionCount,
    int FaqCount,
    int KbUploadCount,
    int AgentCount
);
```

- [ ] **Step 5: Create Handler**

```csharp
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;

internal sealed class GetTopContributorsHandler : IQueryHandler<GetTopContributorsQuery, IReadOnlyList<TopContributorDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetTopContributorsHandler> _logger;

    public GetTopContributorsHandler(MeepleAiDbContext dbContext, ILogger<GetTopContributorsHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<TopContributorDto>> Handle(GetTopContributorsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = Math.Clamp(query.Limit, 1, 20);

        var aggregated = await (
            from u in _dbContext.Users.AsNoTracking()
            where !u.IsDeleted
            let faqCount = _dbContext.GameFaqs.Count(f => f.AuthorId == u.Id)
            let kbCount = _dbContext.PdfDocuments.Count(p => p.UploadedByUserId == u.Id && p.IsPublic)
            let agentCount = _dbContext.AgentSessions.Where(a => a.UserId == u.Id).Select(a => a.AgentId).Distinct().Count()
            let total = faqCount + kbCount + agentCount
            where total > 0
            orderby total descending, u.DisplayName
            select new TopContributorDto(
                u.Id,
                u.DisplayName,
                u.AvatarUrl,
                total,
                faqCount,
                kbCount,
                agentCount
            )
        ).Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);

        return aggregated;
    }
}
```

- [ ] **Step 6: Run tests, commit**

```bash
dotnet test --filter "FullyQualifiedName~GetTopContributorsHandler" --logger "console;verbosity=normal"
```

If tests pass, build the entire solution to verify Task 1's composite DTO compiles now:

```bash
cd apps/api/src/Api && dotnet build --nologo 2>&1 | tail -3
```

Expected: clean build (0 errors).

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetTopContributors/ apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Queries/GetTopContributorsHandlerTests.cs
git commit -m "feat(discover): #728 GetTopContributorsQuery handler

Equal-weight sum ranking (faqs + kb_uploads + distinct agent sessions
per user). Excludes zero-contribution and soft-deleted users.
Composite DTO now compiles (Task 1 unblocked).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 7: G1 composer — Task.WhenAll + per-row try-catch + Prometheus counter

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Discover/Application/Queries/GetDiscoverData/GetDiscoverDataHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Discover/Application/Queries/GetDiscoverDataHandlerTests.cs`

- [ ] **Step 1: Write failing tests for composer behavior**

```csharp
using Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;
using Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.Discover.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Discover")]
public sealed class GetDiscoverDataHandlerTests
{
    private readonly IMediator _mediator;
    private readonly ILogger<GetDiscoverDataHandler> _logger;
    private readonly GetDiscoverDataHandler _handler;

    public GetDiscoverDataHandlerTests()
    {
        _mediator = Substitute.For<IMediator>();
        _logger = Substitute.For<ILogger<GetDiscoverDataHandler>>();
        _handler = new GetDiscoverDataHandler(_mediator, _logger);
    }

    [Fact]
    public async Task Handle_AllSubQueriesSucceed_ReturnsFullPayload()
    {
        // Arrange
        var newGames = new[] { new NewGameDto(Guid.NewGuid(), "G1", null, null, DateTime.UtcNow, null) };
        var topAgents = new[] { new TopAgentDto(Guid.NewGuid(), "", "", "", 5, DateTime.UtcNow) };
        var toolkits = new[] { new RecommendedToolkitDto(Guid.NewGuid(), "TK", "Game", "1.0", 3, DateTime.UtcNow) };
        var recentKb = new[] { new RecentKbDocDto(Guid.NewGuid(), "Doc", "Game", "Rulebook", DateTime.UtcNow, "en") };
        var contributors = new[] { new TopContributorDto(Guid.NewGuid(), "Marco", null, 13, 10, 2, 1) };

        _mediator.Send(Arg.Any<GetNewGamesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<NewGameDto>>(newGames));
        _mediator.Send(Arg.Any<GetTopAgentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TopAgentDto>>(topAgents));
        _mediator.Send(Arg.Any<GetRecommendedToolkitsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecommendedToolkitDto>>(toolkits));
        _mediator.Send(Arg.Any<GetRecentKbDocumentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecentKbDocDto>>(recentKb));
        _mediator.Send(Arg.Any<GetTopContributorsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TopContributorDto>>(contributors));

        // Act
        var result = await _handler.Handle(new GetDiscoverDataQuery(10), CancellationToken.None);

        // Assert
        result.NewGames.Should().HaveCount(1);
        result.TopAgents.Should().HaveCount(1);
        result.RecommendedToolkits.Should().HaveCount(1);
        result.RecentKb.Should().HaveCount(1);
        result.TopContributors.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_OneSubQueryFails_PartialSuccessReturnsEmptyForFailedRow()
    {
        // Arrange — topAgents throws; others succeed
        _mediator.Send(Arg.Any<GetNewGamesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<NewGameDto>>(Array.Empty<NewGameDto>()));
        _mediator.Send(Arg.Any<GetTopAgentsQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<TopAgentDto>>>(_ => throw new InvalidOperationException("simulated DB error"));
        _mediator.Send(Arg.Any<GetRecommendedToolkitsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecommendedToolkitDto>>(Array.Empty<RecommendedToolkitDto>()));
        _mediator.Send(Arg.Any<GetRecentKbDocumentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecentKbDocDto>>(Array.Empty<RecentKbDocDto>()));
        _mediator.Send(Arg.Any<GetTopContributorsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TopContributorDto>>(Array.Empty<TopContributorDto>()));

        // Act
        var result = await _handler.Handle(new GetDiscoverDataQuery(10), CancellationToken.None);

        // Assert
        result.TopAgents.Should().BeEmpty(); // failed row returns empty
        result.NewGames.Should().NotBeNull();
        result.RecommendedToolkits.Should().NotBeNull();
        result.RecentKb.Should().NotBeNull();
        result.TopContributors.Should().NotBeNull();

        // Verify error log emitted
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_AllSubQueriesFail_ReturnsAllEmptyArrays_NoException()
    {
        // Arrange — all 5 throw
        _mediator.Send(Arg.Any<GetNewGamesQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<NewGameDto>>>(_ => throw new Exception("fail"));
        _mediator.Send(Arg.Any<GetTopAgentsQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<TopAgentDto>>>(_ => throw new Exception("fail"));
        _mediator.Send(Arg.Any<GetRecommendedToolkitsQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<RecommendedToolkitDto>>>(_ => throw new Exception("fail"));
        _mediator.Send(Arg.Any<GetRecentKbDocumentsQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<RecentKbDocDto>>>(_ => throw new Exception("fail"));
        _mediator.Send(Arg.Any<GetTopContributorsQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<TopContributorDto>>>(_ => throw new Exception("fail"));

        // Act
        var result = await _handler.Handle(new GetDiscoverDataQuery(10), CancellationToken.None);

        // Assert — all 5 empty, NO exception bubbles
        result.NewGames.Should().BeEmpty();
        result.TopAgents.Should().BeEmpty();
        result.RecommendedToolkits.Should().BeEmpty();
        result.RecentKb.Should().BeEmpty();
        result.TopContributors.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Build to verify compile fail (handler signature changed)**

- [ ] **Step 3: Replace skeleton handler with full implementation**

`GetDiscoverDataHandler.cs`:

```csharp
using Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;

internal sealed class GetDiscoverDataHandler : IQueryHandler<GetDiscoverDataQuery, DiscoverDto>
{
    private readonly IMediator _mediator;
    private readonly ILogger<GetDiscoverDataHandler> _logger;

    public GetDiscoverDataHandler(IMediator mediator, ILogger<GetDiscoverDataHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DiscoverDto> Handle(GetDiscoverDataQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var newGamesTask = SafeRun("newGames", () => _mediator.Send(new GetNewGamesQuery(query.Limit), cancellationToken), Array.Empty<NewGameDto>());
        var topAgentsTask = SafeRun("topAgents", () => _mediator.Send(new GetTopAgentsQuery(query.Limit), cancellationToken), Array.Empty<TopAgentDto>());
        var toolkitsTask = SafeRun("recommendedToolkits", () => _mediator.Send(new GetRecommendedToolkitsQuery(query.Limit), cancellationToken), Array.Empty<RecommendedToolkitDto>());
        var recentKbTask = SafeRun("recentKb", () => _mediator.Send(new GetRecentKbDocumentsQuery(query.Limit), cancellationToken), Array.Empty<RecentKbDocDto>());
        var contributorsTask = SafeRun("topContributors", () => _mediator.Send(new GetTopContributorsQuery(query.Limit), cancellationToken), Array.Empty<TopContributorDto>());

        await Task.WhenAll(newGamesTask, topAgentsTask, toolkitsTask, recentKbTask, contributorsTask).ConfigureAwait(false);

        return new DiscoverDto(
            NewGames: await newGamesTask.ConfigureAwait(false),
            TopAgents: await topAgentsTask.ConfigureAwait(false),
            RecommendedToolkits: await toolkitsTask.ConfigureAwait(false),
            RecentKb: await recentKbTask.ConfigureAwait(false),
            TopContributors: await contributorsTask.ConfigureAwait(false)
        );
    }

    private async Task<IReadOnlyList<T>> SafeRun<T>(string rowName, Func<Task<IReadOnlyList<T>>> factory, IReadOnlyList<T> fallback)
    {
        try
        {
            return await factory().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discover row {RowName} failed", rowName);
            // TODO: Prometheus counter discover_row_failure_total{row=rowName}.Inc() — wire after metrics infra confirmed
            return fallback;
        }
    }
}
```

- [ ] **Step 4: Run unit tests**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~GetDiscoverDataHandler" --logger "console;verbosity=normal"
```

Expected: 3/3 tests pass.

- [ ] **Step 5: Run all sub-query tests + composer to ensure no regression**

```bash
dotnet test --filter "FullyQualifiedName~GetNewGamesHandler|FullyQualifiedName~GetTopAgentsHandler|FullyQualifiedName~GetRecommendedToolkitsHandler|FullyQualifiedName~GetRecentKbDocumentsHandler|FullyQualifiedName~GetTopContributorsHandler|FullyQualifiedName~GetDiscoverDataHandler" --logger "console;verbosity=minimal"
```

Expected: all green.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Discover/Application/Queries/GetDiscoverData/GetDiscoverDataHandler.cs apps/api/tests/Api.Tests/BoundedContexts/Discover/Application/Queries/GetDiscoverDataHandlerTests.cs
git commit -m "feat(discover): #728 G1 composer with Task.WhenAll + per-row try-catch

Replaces skeleton with full implementation: 5 sub-queries dispatched
in parallel via SafeRun helper that catches exceptions and returns
fallback empty array. Structured logging on row failure.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 8: Routing wiring + OpenAPI

**Files:**
- Modify: `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs` (add new map method)

(If `KnowledgeBaseEndpoints.cs` is already large, create a new partial-class file `apps/api/src/Api/Routing/DiscoverEndpoints.cs` mirroring the existing `KnowledgeBaseEndpoints.cs` structure — same `internal static class` pattern.)

- [ ] **Step 1: Add `using` directives**

Append to existing `using` block at top of `KnowledgeBaseEndpoints.cs`:

```csharp
using Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;
```

- [ ] **Step 2: Add `MapDiscoverEndpoints` method**

Add inside `KnowledgeBaseEndpoints` static class:

```csharp
private static void MapDiscoverEndpoints(RouteGroupBuilder group)
{
    // Issue #728: composite discovery dashboard
    group.MapGet("/discover", HandleGetDiscover)
        .WithName("GetDiscover")
        .RequireSession()
        .WithTags("Discover")
        .WithSummary("Get composite /discover dashboard data (5 cross-BC rows)")
        .WithDescription("Returns newGames, topAgents, recommendedToolkits, recentKb, topContributors arrays. Partial-success: a failed sub-query returns empty array (logged); endpoint never returns 500 due to single-row failure.")
        .Produces<DiscoverDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
}

private static async Task<IResult> HandleGetDiscover(
    int? limit,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken cancellationToken)
{
    var limitValue = limit ?? 10;

    if (limitValue < 1 || limitValue > 20)
    {
        return Results.BadRequest(new { error = "limit must be between 1 and 20" });
    }

    var dto = await mediator.Send(new GetDiscoverDataQuery(limitValue), cancellationToken);
    return Results.Ok(dto);
}
```

- [ ] **Step 3: Register the new mapping in `MapKnowledgeBaseEndpoints`**

Add to the chain:

```csharp
MapDiscoverEndpoints(group);
```

(Append after existing `MapKbDocumentEndpoints(group);` line.)

- [ ] **Step 4: Build to verify**

```bash
cd apps/api/src/Api && dotnet build --nologo 2>&1 | tail -3
```

Expected: clean build.

- [ ] **Step 5: Add integration test**

Create `apps/api/tests/Api.Tests/Integration/DiscoverEndpointIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration;

[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Discover")]
public sealed class DiscoverEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public DiscoverEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"discover_endpoint_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task GetDiscover_WhenNotAuthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/discover");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDiscover_LimitOutOfRange_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/discover?limit=50", sessionToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDiscover_HappyPath_ReturnsAll5Arrays()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/discover?limit=10", sessionToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<DiscoverDto>();
        dto.Should().NotBeNull();
        // All 5 arrays present (may be empty in fresh DB but not null)
        dto!.NewGames.Should().NotBeNull();
        dto.TopAgents.Should().NotBeNull();
        dto.RecommendedToolkits.Should().NotBeNull();
        dto.RecentKb.Should().NotBeNull();
        dto.TopContributors.Should().NotBeNull();
    }
}
```

- [ ] **Step 6: Run integration tests**

```bash
dotnet test --filter "FullyQualifiedName~DiscoverEndpointIntegrationTests" --logger "console;verbosity=normal"
```

Expected: 3/3 pass.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs apps/api/tests/Api.Tests/Integration/DiscoverEndpointIntegrationTests.cs
git commit -m "feat(discover): #728 GET /api/v1/discover endpoint wired

RequireSession + limit validation [1, 20] + OpenAPI annotations.
Integration test covers 401 unauth, 400 limit out-of-range, 200 happy-path.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 9: Frontend Zod schemas + client method

**Files:**
- Create: `apps/web/src/lib/api/schemas/discover.schemas.ts`
- Create: `apps/web/src/lib/api/clients/discoverClient.ts`
- Modify: `apps/web/src/lib/api/schemas/index.ts`
- Modify: `apps/web/src/lib/api/clients/index.ts`

- [ ] **Step 1: Create Zod schemas**

`apps/web/src/lib/api/schemas/discover.schemas.ts`:

```typescript
import { z } from 'zod';

export const newGameSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  imageUrl: z.string().url().nullable().optional(),
  publisher: z.string().nullable().optional(),
  createdAt: z.string().datetime({ offset: true }),
  ratingAverage: z.number().nullable().optional(),
});
export type NewGame = z.infer<typeof newGameSchema>;

export const topAgentSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  gameName: z.string(),
  agentType: z.string(),
  installCount: z.number().int().nonnegative(),
  createdAt: z.string().datetime({ offset: true }),
});
export type TopAgent = z.infer<typeof topAgentSchema>;

export const recommendedToolkitSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  gameName: z.string(),
  version: z.string(),
  installCount: z.number().int().nonnegative(),
  createdAt: z.string().datetime({ offset: true }),
});
export type RecommendedToolkit = z.infer<typeof recommendedToolkitSchema>;

export const recentKbDocSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  gameName: z.string(),
  documentCategory: z.string(),
  indexedAt: z.string().datetime({ offset: true }),
  language: z.string(),
});
export type RecentKbDoc = z.infer<typeof recentKbDocSchema>;

export const topContributorSchema = z.object({
  userId: z.string().uuid(),
  displayName: z.string(),
  avatarUrl: z.string().url().nullable().optional(),
  contributionCount: z.number().int().nonnegative(),
  faqCount: z.number().int().nonnegative(),
  kbUploadCount: z.number().int().nonnegative(),
  agentCount: z.number().int().nonnegative(),
});
export type TopContributor = z.infer<typeof topContributorSchema>;

export const discoverSchema = z.object({
  newGames: z.array(newGameSchema),
  topAgents: z.array(topAgentSchema),
  recommendedToolkits: z.array(recommendedToolkitSchema),
  recentKb: z.array(recentKbDocSchema),
  topContributors: z.array(topContributorSchema),
});
export type Discover = z.infer<typeof discoverSchema>;
```

- [ ] **Step 2: Re-export from schemas/index.ts**

Append to `apps/web/src/lib/api/schemas/index.ts`:

```typescript
export * from './discover.schemas';
```

- [ ] **Step 3: Create discoverClient.ts**

`apps/web/src/lib/api/clients/discoverClient.ts`:

```typescript
import { httpClient } from '@/lib/api/http-client';
import { discoverSchema, type Discover } from '@/lib/api/schemas/discover.schemas';

export const discoverClient = {
  /**
   * Fetch the composite /discover dashboard payload.
   * @param limit Items per row (1-20, default 10).
   */
  async getDiscover(limit: number = 10): Promise<Discover | null> {
    return httpClient.get(`/api/v1/discover?limit=${limit}`, discoverSchema);
  },
};
```

(Adapt the `httpClient.get` signature if the actual project pattern differs. Check `knowledgeBaseClient.ts` from issue #730 for canonical pattern: it passes the schema for validation.)

- [ ] **Step 4: Re-export from clients/index.ts**

Append:

```typescript
export * from './discoverClient';
```

- [ ] **Step 5: Run frontend typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: zero errors.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/api/schemas/discover.schemas.ts apps/web/src/lib/api/schemas/index.ts apps/web/src/lib/api/clients/discoverClient.ts apps/web/src/lib/api/clients/index.ts
git commit -m "feat(discover): #728 frontend Zod schemas + discoverClient

6 Zod schemas (5 row + composite) mirroring backend DTOs.
discoverClient.getDiscover(limit) method validates response.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 10: Frontend React Query hook

**Files:**
- Create: `apps/web/src/hooks/queries/useDiscover.ts`
- Modify: `apps/web/src/hooks/queries/index.ts`

- [ ] **Step 1: Create useDiscover hook**

`apps/web/src/hooks/queries/useDiscover.ts`:

```typescript
'use client';

import { useQuery } from '@tanstack/react-query';
import { discoverClient } from '@/lib/api/clients/discoverClient';
import type { Discover } from '@/lib/api/schemas/discover.schemas';

export const discoverKeys = {
  all: ['discover'] as const,
  byLimit: (limit: number) => ['discover', { limit }] as const,
} as const;

export function useDiscover(limit: number = 10, enabled: boolean = true) {
  return useQuery<Discover | null, Error>({
    queryKey: discoverKeys.byLimit(limit),
    queryFn: () => discoverClient.getDiscover(limit),
    enabled,
    staleTime: 10 * 60_000,  // 10 min — discover rows have varying freshness, this is the conservative lower bound
    gcTime: 30 * 60_000,
  });
}
```

- [ ] **Step 2: Re-export from hooks/queries/index.ts**

Append:

```typescript
export * from './useDiscover';
```

- [ ] **Step 3: Run frontend typecheck + lint**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```

Expected: zero errors, no new warnings.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/hooks/queries/useDiscover.ts apps/web/src/hooks/queries/index.ts
git commit -m "feat(discover): #728 useDiscover React Query hook

10min staleTime (conservative lower bound across 5 row TTLs).
Single composite hook returns full DiscoverDto for FE Wave 3 child #687.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 11: PR + close-out

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/issue-728-discover-endpoints
```

- [ ] **Step 2: Open PR against `main-dev`**

```bash
gh pr create --base main-dev --title "feat(discover): #728 /discover composite endpoint (Wave 3 prereq)" --body "$(cat <<'EOF'
## Summary

- Single composite endpoint `GET /api/v1/discover?limit=N` returning 5 cross-BC rows
- New `Discover` BC owns the composer; 5 sub-queries each in their native BC
- Partial-success failure semantics (per-row try-catch + structured logging)
- HybridCache wrapping deferred to follow-up (see spec §8 / Out of Scope)
- Frontend Zod schemas + `discoverClient.getDiscover()` + `useDiscover` hook

Spec: `docs/superpowers/specs/2026-05-06-discover-endpoints-design.md`
Plan: `docs/superpowers/plans/2026-05-06-discover-endpoints-plan.md`

## Out of scope (follow-up)
- HybridCache per-row TTL wrapping
- Cache invalidation via domain events
- KB-context aware toolkit ranking (ML)
- Time-decayed contributor ranking
- Top agents proper install model (current uses AgentSession proxy)

## Test plan
- [x] Each handler unit-tested (5 sub-queries + composer)
- [x] Integration test: 401 unauth, 400 limit out-of-range, 200 happy-path
- [x] `dotnet build` clean
- [x] `pnpm typecheck` clean
- [ ] Manual: hit `/api/v1/discover` via Scalar — verify 5 arrays present

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Comment on issue #687 (FE Wave 3 child) to unblock**

```bash
gh issue comment 687 --body "Backend prerequisite #728 is in PR \$(gh pr view --json url -q .url). Hook available: \`useDiscover(limit)\` returning composite DTO. Schemas in \`apps/web/src/lib/api/schemas/discover.schemas.ts\`. Phase 0.5 contract for /discover can now be written."
```

- [ ] **Step 4: Close-out comment on #728**

```bash
gh issue comment 728 --body "PR opened: \$(gh pr view --json url -q .url)

5 endpoints delivered as a composite:
- GET /api/v1/discover (composite, 5 rows in single fetch)

Sub-queries (each callable independently for future per-row pages):
- GetNewGamesQuery (SharedGameCatalog BC)
- GetTopAgentsQuery (KnowledgeBase BC, AgentSession proxy)
- GetRecommendedToolkitsQuery (GameToolkit BC)
- GetRecentKbDocumentsQuery (KnowledgeBase BC)
- GetTopContributorsQuery (Authentication BC, equal-weight sum)

No DB migration required (uses existing entities).

Closes #728 once PR merges."
```

---

## Spec coverage note: HybridCache (D7) deferred

Spec D7 proposes per-row HybridCache wrapping (newGames 10min · topAgents 30min · recommendedToolkits 30min · recentKb 5min · topContributors 1h). This plan **defers caching to a follow-up commit/issue** — rationale identical to the deferred HybridCache decision in the #730 plan: caching is non-functional optimization, cold P95 targets are likely achievable with simple indexed SELECTs, and adding cache later is a clean wrap of `mediator.Send(...)` calls in the routing handler.

If post-merge measurements show cold P95 > 400ms for the composite or > spec targets per row, file a follow-up issue and add `IHybridCacheService.GetOrCreateAsync` wraps in `GetDiscoverDataHandler.SafeRun(...)`.

---

## Summary of commits (11 total)

1. `feat(discover): #728 BC scaffolding + composite DTO skeleton`
2. `feat(discover): #728 GetNewGamesQuery handler`
3. `feat(discover): #728 GetTopAgentsQuery handler (Approach A — AgentSession proxy)`
4. `feat(discover): #728 GetRecommendedToolkitsQuery handler`
5. `feat(discover): #728 GetRecentKbDocumentsQuery handler`
6. `feat(discover): #728 GetTopContributorsQuery handler`
7. `feat(discover): #728 G1 composer with Task.WhenAll + per-row try-catch`
8. `feat(discover): #728 GET /api/v1/discover endpoint wired`
9. `feat(discover): #728 frontend Zod schemas + discoverClient`
10. `feat(discover): #728 useDiscover React Query hook`
11. (PR open, no extra commit; Task 0 has no commit either since it's investigation only)
