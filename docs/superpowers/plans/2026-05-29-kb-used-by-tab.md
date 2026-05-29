# KB Used-by Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Used by" tab inside `KbDocDetailPanel` that lists, given a PDF document, the AI agents that explicitly consume it via `AgentDefinition.KbCardIds`.

**Architecture:** New `GET /api/v1/admin/kb/docs/{docId}/agents` endpoint backed by a JSONB containment query (`kb_card_ids @> '["docId"]'::jsonb`) + GIN index. Read-only FE tab mirroring the F3-FU-1 (#1650) pattern: URL-driven `?tab=used-by`, dedicated `used-by/` folder under the KB explorer, dedicated Zod schemas + API client + React Query hook (no polling — the association is static).

**Tech Stack:** .NET 9 + EF Core + Npgsql (jsonb `@>`) + MediatR (IQuery/IQueryHandler) + xUnit + Testcontainers (Postgres) · Next.js 16 + React 19 + TanStack Query + Zod + Vitest

**Design doc:** `docs/superpowers/specs/2026-05-29-kb-used-by-tab-design.md`
**Issue:** [#1651](https://github.com/meepleAi-app/meepleai-monorepo/issues/1651) (P3) — sibling of [#1650](https://github.com/meepleAi-app/meepleai-monorepo/issues/1650) (PR #1668)
**Parent branch:** `main-dev`

---

## File Structure

### Backend — new files

| Path | Responsibility |
|---|---|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbDocConsumingAgentDto.cs` | Read DTO returned by the endpoint (Id, Name, Type, IsActive, Status, IsSystemDefined, TypologySlug, GameId, GameName, InvocationCount, LastInvokedAt). |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQuery.cs` | Query record `(Guid DocumentId) : IQuery<IReadOnlyList<KbDocConsumingAgentDto>>`. |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQueryHandler.cs` | Handler — calls repo + bulk-resolves GameName + maps to DTO. |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQueryHandlerTests.cs` | Unit tests for mapping logic (repo mocked). |
| `apps/api/tests/Api.Tests/Integration/KnowledgeBase/ConsumingAgentsEndpointTests.cs` | Testcontainers integration tests covering AC1–AC8. |
| (auto-generated) `apps/api/src/Api/Migrations/<timestamp>_AddKbCardIdsGinIndex.cs` + Designer | EF migration creating the GIN index on `kb_card_ids`. |

### Backend — modified files

| Path | Change |
|---|---|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IAgentDefinitionRepository.cs` | Add `GetByConsumedDocumentAsync(Guid, CancellationToken)`. |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/AgentDefinitionRepository.cs` | Implement `GetByConsumedDocumentAsync` via `FromSqlInterpolated` + `kb_card_ids @>`. |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/AgentDefinitionConfiguration.cs` | Add `HasIndex("_kbCardIdsJson").HasDatabaseName("ix_agent_definitions_kb_card_ids").HasMethod("gin")`. |
| `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs` | Map `GET /docs/{docId:guid}/agents`. |

### Frontend — new files

| Path | Responsibility |
|---|---|
| `apps/web/src/lib/api/schemas/kb-consuming-agents.schemas.ts` | Zod schemas — single source for the JSON contract. |
| `apps/web/src/lib/api/admin-kb-used-by.ts` | API client — `fetchKbDocConsumingAgents(docId)`. |
| `apps/web/src/hooks/queries/useKbDocConsumingAgents.ts` | React Query hook — no polling, enabled when tab active. |
| `apps/web/src/hooks/queries/__tests__/useKbDocConsumingAgents.test.ts` | Hook unit tests. |
| `apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByEmptyState.tsx` | Empty state ("Nessun agent consuma questo documento"). |
| `apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByAgentRow.tsx` | Single agent row — name, type, system badge, status, game, usage, link. |
| `apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByPanel.tsx` | Container — orchestrates query + loading/error/empty/ready. |
| `apps/web/src/components/admin/knowledge-base/explorer/used-by/__tests__/UsedByAgentRow.test.tsx` | Row tests (badge, link, game-null fallback, status chips). |
| `apps/web/src/components/admin/knowledge-base/explorer/used-by/__tests__/UsedByPanel.test.tsx` | Panel tests (4 states). |
| `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx` | Tabs nav tests (3 tabs present, link preserves docId). |

### Frontend — modified files

| Path | Change |
|---|---|
| `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx` | Add `'used-by'` to `KbDocTabKey` + `TABS` array. |
| `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx` | Read `?tab=used-by` and branch render to `<UsedByPanel docId={...}/>`. |
| `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx` | Add mock for `admin-kb-used-by` + test for `?tab=used-by` branch. |

---

## Task 0: Pre-flight & branch creation

**Files:** (none — git only)

- [ ] **Step 0.1: Verify clean working tree on parent branch**

Run:
```bash
git branch --show-current
git status
```
Expected: `main-dev`, clean tree. **If HEAD is on another `feature/*` branch, STOP and run `git checkout main-dev`.** (CLAUDE.md branch hygiene rule, issue #806.)

- [ ] **Step 0.2: Fast-forward main-dev**

Run:
```bash
git pull --ff-only
```
Expected: `Already up to date.` or successful FF.

- [ ] **Step 0.3: Create feature branch**

Run:
```bash
git checkout -b feature/issue-1651-used-by-tab
git config branch.feature/issue-1651-used-by-tab.parent main-dev
```
Expected: switched to new branch; parent config recorded for PR target detection.

---

## Task 1: Backend — DTO `KbDocConsumingAgentDto`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbDocConsumingAgentDto.cs`

- [ ] **Step 1.1: Write the DTO**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbDocConsumingAgentDto.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Read DTO for the "Used by" tab in KbDocDetailPanel.
/// Identifies an agent that explicitly consumes a given PDF document via AgentDefinition.KbCardIds.
/// Issue #1651: F3-FU-2 — Used-by tab.
/// </summary>
internal sealed record KbDocConsumingAgentDto(
    Guid Id,
    string Name,
    string Type,
    bool IsActive,
    string Status,
    bool IsSystemDefined,
    string? TypologySlug,
    Guid? GameId,
    string? GameName,
    int InvocationCount,
    DateTime? LastInvokedAt
);
```

- [ ] **Step 1.2: Build to verify compilation**

Run from `apps/api/src/Api/`:
```bash
dotnet build --nologo -clp:NoSummary
```
Expected: build succeeds with 0 errors.

- [ ] **Step 1.3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbDocConsumingAgentDto.cs
git commit -m "feat(admin-kb): #1651 add KbDocConsumingAgentDto"
```

---

## Task 2: Backend — Query record

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQuery.cs`

- [ ] **Step 2.1: Write the query record**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQuery.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Lists the agent definitions that explicitly consume a given PDF document
/// (i.e. AgentDefinition.KbCardIds contains DocumentId).
/// Returns an empty list when no agent consumes the document.
/// Issue #1651: F3-FU-2 — Used-by tab.
/// </summary>
internal sealed record GetConsumingAgentsByDocumentIdQuery(Guid DocumentId)
    : IQuery<IReadOnlyList<KbDocConsumingAgentDto>>;
```

- [ ] **Step 2.2: Build**

Run from `apps/api/src/Api/`:
```bash
dotnet build --nologo -clp:NoSummary
```
Expected: build succeeds.

- [ ] **Step 2.3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQuery.cs
git commit -m "feat(admin-kb): #1651 add GetConsumingAgentsByDocumentIdQuery record"
```

---

## Task 3: Backend — Repository method + GIN index + migration

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IAgentDefinitionRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/AgentDefinitionRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/AgentDefinitionConfiguration.cs`
- Create (auto): `apps/api/src/Api/Migrations/<timestamp>_AddKbCardIdsGinIndex.cs`

- [ ] **Step 3.1: Add method to repository interface**

Edit `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IAgentDefinitionRepository.cs`. After the existing `CountActiveByGameIdsAsync` declaration (last method), add:

```csharp
    /// <summary>
    /// Returns the agent definitions whose KbCardIds explicitly contains the given document id.
    /// Uses Postgres JSONB containment (kb_card_ids @>); soft-deleted agents are excluded.
    /// Returns an empty list when documentId is Guid.Empty.
    /// Issue #1651: F3-FU-2 — Used-by tab.
    /// </summary>
    Task<IReadOnlyList<AgentDefinition>> GetByConsumedDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 3.2: Implement the method**

Edit `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/AgentDefinitionRepository.cs`. After the existing `CountActiveByGameIdsAsync` implementation (last method), add:

```csharp
    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgentDefinition>> GetByConsumedDocumentAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty)
            return Array.Empty<AgentDefinition>();

        // JSONB containment: agents whose kb_card_ids array contains documentId.
        // is_deleted = false is asserted explicitly in SQL — the global query filter
        // is not guaranteed to compose over FromSqlInterpolated.
        var docIdJsonLiteral = $"[\"{documentId}\"]";

        return await DbContext.Set<AgentDefinition>()
            .FromSqlInterpolated(
                $@"SELECT * FROM knowledge_base.agent_definitions
                   WHERE kb_card_ids @> {docIdJsonLiteral}::jsonb
                   AND is_deleted = false")
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
```

- [ ] **Step 3.3: Add GIN index to entity configuration**

Edit `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/AgentDefinitionConfiguration.cs`. Find the block (around line 76):

```csharp
        // KbCardIds (Issue #4932) - stored as JSONB array of Guid
        builder.Ignore(a => a.KbCardIds);
        builder.Property<string>("_kbCardIdsJson")
            .HasColumnName("kb_card_ids")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();
```

Append, immediately after the `.IsRequired();` line of that property:

```csharp
        // Issue #1651: GIN index on kb_card_ids to support `@>` containment query
        // for the Used-by tab (agents that consume a given PDF document).
        builder.HasIndex("_kbCardIdsJson")
            .HasDatabaseName("ix_agent_definitions_kb_card_ids")
            .HasMethod("gin");
```

- [ ] **Step 3.4: Generate the migration**

Run from `apps/api/src/Api/`:
```bash
dotnet ef migrations add AddKbCardIdsGinIndex --output-dir Migrations --no-build
```
Wait — the project does have a build context; if `--no-build` fails, drop it.

Run:
```bash
dotnet ef migrations add AddKbCardIdsGinIndex --output-dir Migrations
```
Expected: two new files appear under `apps/api/src/Api/Migrations/` — `<timestamp>_AddKbCardIdsGinIndex.cs` and `.Designer.cs`, plus an update to `MeepleAiDbContextModelSnapshot.cs`.

- [ ] **Step 3.5: Verify migration content**

Open the generated `apps/api/src/Api/Migrations/<timestamp>_AddKbCardIdsGinIndex.cs`. It MUST contain a `CreateIndex` call equivalent to:

```csharp
migrationBuilder.CreateIndex(
    name: "ix_agent_definitions_kb_card_ids",
    schema: "knowledge_base",
    table: "agent_definitions",
    column: "kb_card_ids")
    .Annotation("Npgsql:IndexMethod", "gin");
```

If the generated migration contains anything **else** (e.g., re-creating unrelated indexes, dropping tables), STOP — the snapshot may be stale. Investigate before continuing.

- [ ] **Step 3.6: Apply migration to dev DB**

From `apps/api/src/Api/`:
```bash
dotnet ef database update
```
Expected: `Done.` and the index appears in Postgres. If it fails because the dev DB is unreachable, ensure `make dev-core` is running (`cd infra && make dev-core`).

- [ ] **Step 3.7: Build**

```bash
dotnet build --nologo -clp:NoSummary
```
Expected: 0 errors.

- [ ] **Step 3.8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IAgentDefinitionRepository.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/AgentDefinitionRepository.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/AgentDefinitionConfiguration.cs \
        apps/api/src/Api/Migrations/*AddKbCardIdsGinIndex* \
        apps/api/src/Api/Migrations/MeepleAiDbContextModelSnapshot.cs
git commit -m "feat(admin-kb): #1651 repo GetByConsumedDocumentAsync + GIN index on kb_card_ids"
```

> **Staging note**: per project memory, staging migrations are not auto-applied. The deploy will need a manual `dotnet ef database update` on staging. Flag this in the PR description.

---

## Task 4: Backend — Handler + unit tests (TDD)

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQueryHandlerTests.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQueryHandler.cs`

- [ ] **Step 4.1: Write the failing unit tests first**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQueryHandlerTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1651")]
public sealed class GetConsumingAgentsByDocumentIdQueryHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _agentRepo = new(MockBehavior.Strict);
    private readonly Mock<ISharedGameRepository> _gameRepo = new(MockBehavior.Strict);

    private GetConsumingAgentsByDocumentIdQueryHandler CreateHandler() =>
        new(_agentRepo.Object, _gameRepo.Object);

    private static AgentDefinition MakeCustomAgent(string name, Guid? gameId = null)
    {
        var agent = AgentDefinition.Create(
            name: name,
            description: "test",
            type: AgentType.Custom("HybridSearch", "Hybrid search"),
            config: new AgentDefinitionConfig("gpt-4o-mini", 1024, 0.5));
        if (gameId.HasValue) agent.SetGameId(gameId);
        return agent;
    }

    private static AgentDefinition MakeSystemAgent(string name, string typologySlug)
    {
        return AgentDefinition.CreateSystem(
            name: name,
            description: "system",
            type: AgentType.Custom("HybridSearch", "Hybrid search"),
            config: new AgentDefinitionConfig("gpt-4o-mini", 1024, 0.5),
            typologySlug: typologySlug);
    }

    [Fact]
    public async Task Handle_NoConsumingAgents_ReturnsEmptyList()
    {
        // Arrange
        var docId = Guid.NewGuid();
        _agentRepo.Setup(r => r.GetByConsumedDocumentAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgentDefinition>());

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetConsumingAgentsByDocumentIdQuery(docId), default);

        // Assert
        result.Should().BeEmpty();
        _gameRepo.Verify(g => g.GetNamesByIdsAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never, "no agents → no game-name resolution needed");
    }

    [Fact]
    public async Task Handle_MapsCustomAgent_WithGameNameResolved()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agent = MakeCustomAgent("Alpha", gameId);

        _agentRepo.Setup(r => r.GetByConsumedDocumentAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { agent });
        _gameRepo.Setup(g => g.GetNamesByIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(gameId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [gameId] = "Wingspan" });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetConsumingAgentsByDocumentIdQuery(docId), default);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Id.Should().Be(agent.Id);
        dto.Name.Should().Be("Alpha");
        dto.IsSystemDefined.Should().BeFalse();
        dto.TypologySlug.Should().BeNull();
        dto.GameId.Should().Be(gameId);
        dto.GameName.Should().Be("Wingspan");
        dto.Status.Should().Be(AgentDefinitionStatus.Draft.ToString());
    }

    [Fact]
    public async Task Handle_MapsSystemAgent_WithFlagAndTypologySlug()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var agent = MakeSystemAgent("Arbitro", typologySlug: "arbitro");

        _agentRepo.Setup(r => r.GetByConsumedDocumentAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { agent });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetConsumingAgentsByDocumentIdQuery(docId), default);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.IsSystemDefined.Should().BeTrue();
        dto.TypologySlug.Should().Be("arbitro");
        dto.GameId.Should().BeNull();
        dto.GameName.Should().BeNull();
        _gameRepo.Verify(g => g.GetNamesByIdsAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never, "no agent has a GameId → no resolution call");
    }

    [Fact]
    public async Task Handle_AgentWithMissingGame_LeavesGameNameNull()
    {
        // Arrange — the agent has a GameId, but the game has been removed from the catalog.
        var docId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agent = MakeCustomAgent("Beta", gameId);

        _agentRepo.Setup(r => r.GetByConsumedDocumentAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { agent });
        _gameRepo.Setup(g => g.GetNamesByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>()); // empty — game not found

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetConsumingAgentsByDocumentIdQuery(docId), default);

        // Assert
        result.Should().HaveCount(1);
        result[0].GameId.Should().Be(gameId);
        result[0].GameName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_BulkResolvesGameNames_OneCallForMultipleAgents()
    {
        // Arrange — two agents, each with a distinct game. Must call GetNamesByIdsAsync ONCE.
        var docId = Guid.NewGuid();
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var a1 = MakeCustomAgent("A1", g1);
        var a2 = MakeCustomAgent("A2", g2);

        _agentRepo.Setup(r => r.GetByConsumedDocumentAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { a1, a2 });
        _gameRepo.Setup(g => g.GetNamesByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [g1] = "Game1", [g2] = "Game2" });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetConsumingAgentsByDocumentIdQuery(docId), default);

        // Assert
        result.Should().HaveCount(2);
        _gameRepo.Verify(g => g.GetNamesByIdsAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once, "bulk lookup, no N+1");
    }
}
```

- [ ] **Step 4.2: Run tests — verify they fail (handler doesn't exist yet)**

Run from `apps/api/tests/Api.Tests/`:
```bash
dotnet test --filter "FullyQualifiedName~GetConsumingAgentsByDocumentIdQueryHandlerTests" --nologo
```
Expected: compilation **fails** with "GetConsumingAgentsByDocumentIdQueryHandler does not exist in the current context" (handler not implemented yet). This is the failing-test signal.

- [ ] **Step 4.3: Implement the handler**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQueryHandler.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetConsumingAgentsByDocumentIdQuery — Issue #1651, F3-FU-2 Used-by tab.
/// Lists agents that explicitly consume the document (KbCardIds containment), resolves
/// GameName via bulk lookup, and maps to KbDocConsumingAgentDto. Soft-deleted agents are
/// excluded by the repository implementation; the global query filter is not relied upon.
/// </summary>
internal sealed class GetConsumingAgentsByDocumentIdQueryHandler
    : IQueryHandler<GetConsumingAgentsByDocumentIdQuery, IReadOnlyList<KbDocConsumingAgentDto>>
{
    private readonly IAgentDefinitionRepository _agentRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public GetConsumingAgentsByDocumentIdQueryHandler(
        IAgentDefinitionRepository agentRepository,
        ISharedGameRepository sharedGameRepository)
    {
        _agentRepository = agentRepository
            ?? throw new ArgumentNullException(nameof(agentRepository));
        _sharedGameRepository = sharedGameRepository
            ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    public async Task<IReadOnlyList<KbDocConsumingAgentDto>> Handle(
        GetConsumingAgentsByDocumentIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var agents = await _agentRepository
            .GetByConsumedDocumentAsync(query.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (agents.Count == 0)
            return Array.Empty<KbDocConsumingAgentDto>();

        var gameIds = agents
            .Where(a => a.GameId.HasValue)
            .Select(a => a.GameId!.Value)
            .Distinct()
            .ToList();

        var gameNames = gameIds.Count > 0
            ? await _sharedGameRepository
                .GetNamesByIdsAsync(gameIds, cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<Guid, string>();

        return agents.Select(a => MapToDto(a, gameNames)).ToList();
    }

    private static KbDocConsumingAgentDto MapToDto(
        AgentDefinition agent,
        IReadOnlyDictionary<Guid, string> gameNames)
    {
        var gameName = agent.GameId.HasValue
            && gameNames.TryGetValue(agent.GameId.Value, out var name)
                ? name
                : null;

        return new KbDocConsumingAgentDto(
            Id: agent.Id,
            Name: agent.Name,
            Type: agent.Type.Value,
            IsActive: agent.IsActive,
            Status: agent.Status.ToString(),
            IsSystemDefined: agent.IsSystemDefined,
            TypologySlug: agent.TypologySlug,
            GameId: agent.GameId,
            GameName: gameName,
            InvocationCount: agent.InvocationCount,
            LastInvokedAt: agent.LastInvokedAt);
    }
}
```

- [ ] **Step 4.4: Run tests — verify they pass**

```bash
dotnet test --filter "FullyQualifiedName~GetConsumingAgentsByDocumentIdQueryHandlerTests" --nologo
```
Expected: 5 passed, 0 failed.

- [ ] **Step 4.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQueryHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetConsumingAgentsByDocumentIdQueryHandlerTests.cs
git commit -m "feat(admin-kb): #1651 GetConsumingAgentsByDocumentIdQueryHandler + unit tests"
```

---

## Task 5: Backend — Endpoint registration

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`

- [ ] **Step 5.1: Register the endpoint**

Edit `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`. Find the block:

```csharp
        // GET /api/v1/admin/kb/docs/{docId}/ingestion-log — Issue #1650
        kbGroup.MapGet("/docs/{docId:guid}/ingestion-log", async (
            Guid docId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new GetLatestIngestionLogByDocumentIdQuery(docId);
            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetKbDocIngestionLog")
        .WithSummary("Get the latest ProcessingJob (with Steps and LogEntries) for a PdfDocumentId.");
```

Immediately AFTER it, add:

```csharp
        // GET /api/v1/admin/kb/docs/{docId}/agents — Issue #1651 F3-FU-2
        kbGroup.MapGet("/docs/{docId:guid}/agents", async (
            Guid docId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new GetConsumingAgentsByDocumentIdQuery(docId);
            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetKbDocConsumingAgents")
        .WithSummary("List agent definitions that consume a given PDF document (KbCardIds containment).");
```

- [ ] **Step 5.2: Build**

```bash
dotnet build --nologo -clp:NoSummary
```
Expected: 0 errors. If the compiler complains about `GetConsumingAgentsByDocumentIdQuery` not found, double-check the `using` directive at top of `AdminKnowledgeBaseEndpoints.cs` — `using Api.BoundedContexts.KnowledgeBase.Application.Queries;` should already cover it (it's used for other KB queries).

- [ ] **Step 5.3: Commit**

```bash
git add apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs
git commit -m "feat(admin-kb): #1651 map GET /admin/kb/docs/{docId}/agents endpoint"
```

---

## Task 6: Backend — Integration tests (Testcontainers, AC1–AC8)

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/ConsumingAgentsEndpointTests.cs`

- [ ] **Step 6.1: Write the integration test file**

Create `apps/api/tests/Api.Tests/Integration/KnowledgeBase/ConsumingAgentsEndpointTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for GET /api/v1/admin/kb/docs/{docId}/agents.
/// Issue #1651: F3-FU-2 Used-by tab — verifies JSONB containment, soft-delete exclusion,
/// system-agent visibility, GameName bulk resolution, and admin authentication.
/// Uses Testcontainers Postgres because the @> containment is not translatable on EF InMemory.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1651")]
public sealed class ConsumingAgentsEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public ConsumingAgentsEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"used_by_{Guid.NewGuid():N}";
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
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ========================================
    // AC8 — Authentication
    // ========================================

    [Fact]
    public async Task GET_NoSession_Returns401()
    {
        var docId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/v1/admin/kb/docs/{docId}/agents");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // AC5 — Zero consumers
    // ========================================

    [Fact]
    public async Task GET_NoConsumingAgents_Returns200WithEmptyArray()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var request = AuthRequest(HttpMethod.Get, $"/api/v1/admin/kb/docs/{docId}/agents", sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("[]");
    }

    // ========================================
    // AC7 — Guid.Empty defensive
    // ========================================

    [Fact]
    public async Task GET_EmptyGuid_Returns200WithEmptyArray()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var request = AuthRequest(
            HttpMethod.Get,
            $"/api/v1/admin/kb/docs/{Guid.Empty}/agents",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    // ========================================
    // AC1 + AC2 — Includes consumers, excludes non-consumers
    // ========================================

    [Fact]
    public async Task GET_OneConsumingAgent_ReturnsThatAgentOnly()
    {
        // Arrange — Agent A consumes docId; Agent B does not.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var otherDocId = Guid.NewGuid();

        var agentA = MakeAgent("Agent A");
        agentA.UpdateKbCardIds(new[] { docId });
        var agentB = MakeAgent("Agent B");
        agentB.UpdateKbCardIds(new[] { otherDocId });

        dbContext.Set<AgentDefinition>().AddRange(agentA, agentB);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.SendAsync(
            AuthRequest(HttpMethod.Get, $"/api/v1/admin/kb/docs/{docId}/agents", sessionToken));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadArray(response);
        items.Should().HaveCount(1);
        items[0].GetProperty("name").GetString().Should().Be("Agent A");
    }

    // ========================================
    // AC3 — Soft-deleted excluded
    // ========================================

    [Fact]
    public async Task GET_SoftDeletedAgent_IsExcluded()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var agent = MakeAgent("Soft Deleted");
        agent.UpdateKbCardIds(new[] { docId });
        agent.SoftDelete();

        dbContext.Set<AgentDefinition>().Add(agent);
        await dbContext.SaveChangesAsync();

        var response = await _client.SendAsync(
            AuthRequest(HttpMethod.Get, $"/api/v1/admin/kb/docs/{docId}/agents", sessionToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadArray(response)).Should().BeEmpty();
    }

    // ========================================
    // AC4 — System agent included with flag
    // ========================================

    [Fact]
    public async Task GET_SystemAgent_IsIncludedWithIsSystemDefinedTrue()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var sysAgent = AgentDefinition.CreateSystem(
            name: "Arbitro",
            description: "system",
            type: AgentType.Custom("HybridSearch", "Hybrid search"),
            config: new AgentDefinitionConfig("gpt-4o-mini", 1024, 0.5),
            typologySlug: "arbitro");
        sysAgent.UpdateKbCardIds(new[] { docId });

        dbContext.Set<AgentDefinition>().Add(sysAgent);
        await dbContext.SaveChangesAsync();

        var response = await _client.SendAsync(
            AuthRequest(HttpMethod.Get, $"/api/v1/admin/kb/docs/{docId}/agents", sessionToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadArray(response);
        items.Should().HaveCount(1);
        items[0].GetProperty("isSystemDefined").GetBoolean().Should().BeTrue();
        items[0].GetProperty("typologySlug").GetString().Should().Be("arbitro");
    }

    // ========================================
    // AC6 — GameName null when game is missing
    // ========================================

    [Fact]
    public async Task GET_AgentWithMissingGame_HasGameNameNull()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var phantomGameId = Guid.NewGuid(); // not in shared_games table

        var agent = MakeAgent("Beta");
        agent.SetGameId(phantomGameId);
        agent.UpdateKbCardIds(new[] { docId });

        dbContext.Set<AgentDefinition>().Add(agent);
        await dbContext.SaveChangesAsync();

        var response = await _client.SendAsync(
            AuthRequest(HttpMethod.Get, $"/api/v1/admin/kb/docs/{docId}/agents", sessionToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadArray(response);
        items.Should().HaveCount(1);
        items[0].GetProperty("gameId").GetGuid().Should().Be(phantomGameId);
        items[0].TryGetProperty("gameName", out var gameNameProp).Should().BeTrue();
        (gameNameProp.ValueKind == JsonValueKind.Null).Should().BeTrue();
    }

    // ========================================
    // Helpers
    // ========================================

    private static AgentDefinition MakeAgent(string name) =>
        AgentDefinition.Create(
            name: name,
            description: "test",
            type: AgentType.Custom("HybridSearch", "Hybrid search"),
            config: new AgentDefinitionConfig("gpt-4o-mini", 1024, 0.5));

    private static HttpRequestMessage AuthRequest(HttpMethod method, string uri, string sessionToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={sessionToken}");
        return request;
    }

    private static async Task<List<JsonElement>> ReadArray(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
```

- [ ] **Step 6.2: Run the integration tests**

From `apps/api/tests/Api.Tests/`:
```bash
dotnet test --filter "FullyQualifiedName~ConsumingAgentsEndpointTests" --nologo
```
Expected: 7 passed (AC1, AC2 covered by `GET_OneConsumingAgent_ReturnsThatAgentOnly`; AC3, AC4, AC5, AC6, AC7, AC8 each one test).

If any test fails: investigate. Common likely failures:
- "kb_card_ids @> ... operator does not exist" → schema name wrong in repo SQL; confirm it's `knowledge_base.agent_definitions`.
- 500 instead of 200 → `JsonContains` typo or AgentDefinition.Create signature mismatch — check error stack trace.

- [ ] **Step 6.3: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/KnowledgeBase/ConsumingAgentsEndpointTests.cs
git commit -m "test(admin-kb): #1651 integration tests for /docs/{id}/agents (AC1-AC8)"
```

---

## Task 7: Frontend — Zod schemas + API client + hook + tests

**Files:**
- Create: `apps/web/src/lib/api/schemas/kb-consuming-agents.schemas.ts`
- Create: `apps/web/src/lib/api/admin-kb-used-by.ts`
- Create: `apps/web/src/hooks/queries/useKbDocConsumingAgents.ts`
- Create: `apps/web/src/hooks/queries/__tests__/useKbDocConsumingAgents.test.ts`

- [ ] **Step 7.1: Write Zod schemas**

Create `apps/web/src/lib/api/schemas/kb-consuming-agents.schemas.ts`:

```ts
import { z } from 'zod';

/**
 * Mirrors backend `KbDocConsumingAgentDto`
 * (BoundedContexts/KnowledgeBase/Application/DTOs/KbDocConsumingAgentDto.cs).
 * Issue #1651: F3-FU-2 — Used-by tab.
 */
export const KbDocConsumingAgentSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  type: z.string(),
  isActive: z.boolean(),
  status: z.enum(['Draft', 'Testing', 'Published']),
  isSystemDefined: z.boolean(),
  typologySlug: z.string().nullable(),
  gameId: z.string().uuid().nullable(),
  gameName: z.string().nullable(),
  invocationCount: z.number().int().nonnegative(),
  lastInvokedAt: z.string().datetime({ offset: true }).nullable(),
});
export type KbDocConsumingAgent = z.infer<typeof KbDocConsumingAgentSchema>;

export const KbDocConsumingAgentsResponseSchema = z.array(KbDocConsumingAgentSchema);
```

- [ ] **Step 7.2: Write API client**

Create `apps/web/src/lib/api/admin-kb-used-by.ts`:

```ts
/**
 * Admin KB Used-by API client.
 * Issue #1651: F3-FU-2 — agents that consume a PDF document.
 */

import { apiClient } from '@/lib/api/client';
import {
  KbDocConsumingAgentsResponseSchema,
  type KbDocConsumingAgent,
} from '@/lib/api/schemas/kb-consuming-agents.schemas';

/**
 * GET /api/v1/admin/kb/docs/{docId}/agents
 * Returns the agents that explicitly consume the document via KbCardIds.
 * Empty array when no agent consumes it.
 */
export async function fetchKbDocConsumingAgents(
  docId: string,
): Promise<KbDocConsumingAgent[]> {
  return apiClient.get<KbDocConsumingAgent[]>(
    `/api/v1/admin/kb/docs/${docId}/agents`,
    KbDocConsumingAgentsResponseSchema,
  );
}
```

- [ ] **Step 7.3: Write the React Query hook**

Create `apps/web/src/hooks/queries/useKbDocConsumingAgents.ts`:

```ts
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { fetchKbDocConsumingAgents } from '@/lib/api/admin-kb-used-by';
import type { KbDocConsumingAgent } from '@/lib/api/schemas/kb-consuming-agents.schemas';

export const kbDocConsumingAgentsKeys = {
  all: ['kb', 'doc', 'consuming-agents'] as const,
  byId: (docId: string) => [...kbDocConsumingAgentsKeys.all, docId] as const,
};

export interface UseKbDocConsumingAgentsOptions {
  readonly docId: string | null | undefined;
  readonly enabled?: boolean;
}

/**
 * TanStack Query hook listing agents that consume a given document.
 * No polling — the association is static (changes only on agent edit).
 * Backend contract: GET /api/v1/admin/kb/docs/{docId}/agents.
 * Issue #1651.
 */
export function useKbDocConsumingAgents(
  options: UseKbDocConsumingAgentsOptions,
): UseQueryResult<KbDocConsumingAgent[], Error> {
  const { docId, enabled = true } = options;
  const isValid = typeof docId === 'string' && docId.length > 0;

  return useQuery<KbDocConsumingAgent[], Error>({
    queryKey: isValid ? kbDocConsumingAgentsKeys.byId(docId) : kbDocConsumingAgentsKeys.all,
    queryFn: async () => (isValid ? fetchKbDocConsumingAgents(docId) : []),
    enabled: enabled && isValid,
    staleTime: 30_000,
  });
}
```

- [ ] **Step 7.4: Write hook tests**

Create `apps/web/src/hooks/queries/__tests__/useKbDocConsumingAgents.test.ts`:

```ts
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

vi.mock('@/lib/api/admin-kb-used-by', () => ({
  fetchKbDocConsumingAgents: vi.fn(),
}));

import { fetchKbDocConsumingAgents } from '@/lib/api/admin-kb-used-by';
import { useKbDocConsumingAgents } from '../useKbDocConsumingAgents';

function wrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

describe('useKbDocConsumingAgents', () => {
  beforeEach(() => {
    vi.mocked(fetchKbDocConsumingAgents).mockReset();
  });

  it('does not fetch when docId is null', () => {
    const { result } = renderHook(() => useKbDocConsumingAgents({ docId: null }), {
      wrapper: wrapper(),
    });
    expect(result.current.fetchStatus).toBe('idle');
    expect(fetchKbDocConsumingAgents).not.toHaveBeenCalled();
  });

  it('does not fetch when docId is empty string', () => {
    renderHook(() => useKbDocConsumingAgents({ docId: '' }), { wrapper: wrapper() });
    expect(fetchKbDocConsumingAgents).not.toHaveBeenCalled();
  });

  it('does not fetch when enabled=false', () => {
    renderHook(
      () => useKbDocConsumingAgents({ docId: 'doc-1', enabled: false }),
      { wrapper: wrapper() },
    );
    expect(fetchKbDocConsumingAgents).not.toHaveBeenCalled();
  });

  it('fetches and returns the agents when docId is valid and enabled', async () => {
    vi.mocked(fetchKbDocConsumingAgents).mockResolvedValue([
      {
        id: '11111111-1111-1111-1111-111111111111',
        name: 'Alpha',
        type: 'HybridSearch',
        isActive: true,
        status: 'Published',
        isSystemDefined: false,
        typologySlug: null,
        gameId: null,
        gameName: null,
        invocationCount: 0,
        lastInvokedAt: null,
      },
    ]);

    const { result } = renderHook(
      () => useKbDocConsumingAgents({ docId: 'doc-1' }),
      { wrapper: wrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data?.[0].name).toBe('Alpha');
    expect(fetchKbDocConsumingAgents).toHaveBeenCalledWith('doc-1');
  });
});
```

- [ ] **Step 7.5: Run hook tests**

From `apps/web/`:
```bash
pnpm test --run hooks/queries/__tests__/useKbDocConsumingAgents
```
Expected: 4 passed.

- [ ] **Step 7.6: Typecheck**

```bash
pnpm typecheck
```
Expected: 0 errors.

- [ ] **Step 7.7: Commit**

```bash
git add apps/web/src/lib/api/schemas/kb-consuming-agents.schemas.ts \
        apps/web/src/lib/api/admin-kb-used-by.ts \
        apps/web/src/hooks/queries/useKbDocConsumingAgents.ts \
        apps/web/src/hooks/queries/__tests__/useKbDocConsumingAgents.test.ts
git commit -m "feat(admin-kb): #1651 FE schemas + API client + useKbDocConsumingAgents hook"
```

---

## Task 8: Frontend — UsedByEmptyState + UsedByAgentRow + tests

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByEmptyState.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByAgentRow.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/used-by/__tests__/UsedByAgentRow.test.tsx`

- [ ] **Step 8.1: Write UsedByEmptyState**

Create `apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByEmptyState.tsx`:

```tsx
'use client';

export function UsedByEmptyState() {
  return (
    <div
      data-testid="used-by-empty"
      className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 p-8 text-center text-sm text-muted-foreground min-h-[200px] flex flex-col items-center justify-center gap-2"
    >
      <span aria-hidden="true" className="text-3xl">
        🧑‍🤝‍🧑
      </span>
      <p>Nessun agent consuma questo documento.</p>
      <p className="text-xs">
        Aggiungi il documento alla KB di un agent per vederlo apparire qui.
      </p>
    </div>
  );
}
```

- [ ] **Step 8.2: Write UsedByAgentRow**

Create `apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByAgentRow.tsx`:

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber/emerald/rose chip palette (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import Link from 'next/link';

import type { KbDocConsumingAgent } from '@/lib/api/schemas/kb-consuming-agents.schemas';

interface UsedByAgentRowProps {
  readonly agent: KbDocConsumingAgent;
}

function statusChipClass(status: KbDocConsumingAgent['status']): string {
  switch (status) {
    case 'Published':
      return 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30';
    case 'Testing':
      return 'bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30';
    default: // Draft
      return 'bg-muted text-muted-foreground border-border';
  }
}

function formatRelative(iso: string | null): string {
  if (iso === null) return 'mai';
  const date = new Date(iso);
  return date.toLocaleDateString('it-IT', { year: 'numeric', month: '2-digit', day: '2-digit' });
}

/**
 * Single row in the "Used by" tab. Read-only — links out to the agent detail
 * in the AI Lab. Issue #1651.
 */
export function UsedByAgentRow({ agent }: UsedByAgentRowProps) {
  const gameLabel = agent.gameName ?? 'KB globale';

  return (
    <li className="py-3" data-testid="used-by-agent-row">
      <Link
        href={`/admin/agents/definitions/${agent.id}`}
        className="block hover:bg-muted/40 rounded-md px-3 py-2 -mx-3 transition-colors"
      >
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-quicksand font-bold text-sm truncate">{agent.name}</span>
          {agent.isSystemDefined && (
            <span
              data-testid="used-by-system-badge"
              className="inline-flex items-center px-1.5 py-0.5 text-[9.5px] font-semibold rounded-full border bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30 uppercase tracking-wider"
              title={agent.typologySlug ?? undefined}
            >
              sistema
            </span>
          )}
          <span
            className={`inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border ${statusChipClass(agent.status)}`}
          >
            {agent.status}
          </span>
          <span className="ml-auto text-[10.5px] font-mono text-muted-foreground">
            {agent.type}
          </span>
        </div>
        <div className="mt-1 flex items-center gap-3 text-[11.5px] text-muted-foreground font-mono">
          <span>{gameLabel}</span>
          <span aria-hidden="true">·</span>
          <span>
            {agent.invocationCount.toLocaleString('it-IT')} invocaz · ultimo {formatRelative(agent.lastInvokedAt)}
          </span>
        </div>
      </Link>
    </li>
  );
}
```

- [ ] **Step 8.3: Write the row tests**

Create `apps/web/src/components/admin/knowledge-base/explorer/used-by/__tests__/UsedByAgentRow.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import type { ReactNode } from 'react';

import { UsedByAgentRow } from '../UsedByAgentRow';
import type { KbDocConsumingAgent } from '@/lib/api/schemas/kb-consuming-agents.schemas';

vi.mock('next/link', () => ({
  default: ({ href, children, ...rest }: { href: string; children: ReactNode; [k: string]: unknown }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

const baseAgent: KbDocConsumingAgent = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Alpha',
  type: 'HybridSearch',
  isActive: true,
  status: 'Published',
  isSystemDefined: false,
  typologySlug: null,
  gameId: '22222222-2222-2222-2222-222222222222',
  gameName: 'Wingspan',
  invocationCount: 42,
  lastInvokedAt: '2026-05-20T10:30:00Z',
};

function renderRow(overrides: Partial<KbDocConsumingAgent> = {}) {
  return render(<ul><UsedByAgentRow agent={{ ...baseAgent, ...overrides }} /></ul>);
}

describe('UsedByAgentRow', () => {
  it('renders the agent name and game name', () => {
    renderRow();
    expect(screen.getByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('links to the agent detail in the AI Lab', () => {
    renderRow();
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute(
      'href',
      '/admin/agents/definitions/11111111-1111-1111-1111-111111111111',
    );
  });

  it('shows "KB globale" label when gameName is null', () => {
    renderRow({ gameName: null, gameId: null });
    expect(screen.getByText('KB globale')).toBeInTheDocument();
  });

  it('shows the system badge for system agents', () => {
    renderRow({ isSystemDefined: true, typologySlug: 'arbitro' });
    expect(screen.getByTestId('used-by-system-badge')).toBeInTheDocument();
  });

  it('hides the system badge for custom agents', () => {
    renderRow({ isSystemDefined: false });
    expect(screen.queryByTestId('used-by-system-badge')).not.toBeInTheDocument();
  });

  it('renders the status chip with the correct status label', () => {
    renderRow({ status: 'Draft' });
    expect(screen.getByText('Draft')).toBeInTheDocument();
  });

  it('renders "mai" when lastInvokedAt is null', () => {
    renderRow({ lastInvokedAt: null });
    expect(screen.getByText(/ultimo mai/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 8.4: Run row tests**

From `apps/web/`:
```bash
pnpm test --run components/admin/knowledge-base/explorer/used-by/__tests__/UsedByAgentRow
```
Expected: 7 passed.

- [ ] **Step 8.5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByEmptyState.tsx \
        apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByAgentRow.tsx \
        apps/web/src/components/admin/knowledge-base/explorer/used-by/__tests__/UsedByAgentRow.test.tsx
git commit -m "feat(admin-kb): #1651 UsedByEmptyState + UsedByAgentRow + tests"
```

---

## Task 9: Frontend — UsedByPanel (container) + tests

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByPanel.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/used-by/__tests__/UsedByPanel.test.tsx`

- [ ] **Step 9.1: Write UsedByPanel**

Create `apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByPanel.tsx`:

```tsx
'use client';

import { useKbDocConsumingAgents } from '@/hooks/queries/useKbDocConsumingAgents';

import { UsedByAgentRow } from './UsedByAgentRow';
import { UsedByEmptyState } from './UsedByEmptyState';

interface UsedByPanelProps {
  readonly docId: string;
}

/**
 * Container of the "Used by" tab inside KbDocDetailPanel. Wires the React Query
 * hook to UsedByAgentRow + UsedByEmptyState. Read-only. Issue #1651.
 */
export function UsedByPanel({ docId }: UsedByPanelProps) {
  const query = useKbDocConsumingAgents({ docId });

  if (query.isLoading) {
    return (
      <div
        data-testid="used-by-panel-loading"
        className="border border-border/60 rounded-lg bg-card/80 p-6 animate-pulse min-h-[200px]"
      >
        <div className="h-4 w-1/2 bg-muted rounded mb-3" />
        <div className="h-4 w-1/3 bg-muted rounded mb-3" />
        <div className="h-4 w-2/3 bg-muted rounded" />
      </div>
    );
  }

  if (query.isError) {
    return (
      <div
        data-testid="used-by-panel-error"
        className="border border-rose-500/30 rounded-lg bg-rose-500/5 p-6 text-sm text-rose-700 dark:text-rose-300"
      >
        Errore caricamento agent consumatori: {query.error.message}
      </div>
    );
  }

  const agents = query.data ?? [];
  if (agents.length === 0) {
    return <UsedByEmptyState />;
  }

  return (
    <section
      data-testid="used-by-panel"
      className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 overflow-hidden"
    >
      <header className="p-4 border-b border-border/60 dark:border-zinc-700/60">
        <h3 className="font-quicksand font-bold text-sm">
          Agent che consumano questo documento ({agents.length})
        </h3>
      </header>
      <ul className="divide-y divide-border/60 dark:divide-zinc-700/60 px-4">
        {agents.map(a => (
          <UsedByAgentRow key={a.id} agent={a} />
        ))}
      </ul>
    </section>
  );
}
```

- [ ] **Step 9.2: Write UsedByPanel tests**

Create `apps/web/src/components/admin/knowledge-base/explorer/used-by/__tests__/UsedByPanel.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { UsedByPanel } from '../UsedByPanel';
import type { KbDocConsumingAgent } from '@/lib/api/schemas/kb-consuming-agents.schemas';

vi.mock('next/link', () => ({
  default: ({ href, children, ...rest }: { href: string; children: ReactNode; [k: string]: unknown }) => (
    <a href={href} {...rest}>{children}</a>
  ),
}));

const mockUseKbDocConsumingAgents = vi.fn();
vi.mock('@/hooks/queries/useKbDocConsumingAgents', () => ({
  useKbDocConsumingAgents: (opts: unknown) => mockUseKbDocConsumingAgents(opts),
}));

const agent: KbDocConsumingAgent = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Alpha',
  type: 'HybridSearch',
  isActive: true,
  status: 'Published',
  isSystemDefined: false,
  typologySlug: null,
  gameId: null,
  gameName: null,
  invocationCount: 0,
  lastInvokedAt: null,
};

describe('UsedByPanel', () => {
  beforeEach(() => {
    mockUseKbDocConsumingAgents.mockReset();
  });

  it('renders the loading skeleton', () => {
    mockUseKbDocConsumingAgents.mockReturnValue({ isLoading: true, isError: false, data: undefined });
    render(<UsedByPanel docId="doc-1" />);
    expect(screen.getByTestId('used-by-panel-loading')).toBeInTheDocument();
  });

  it('renders the error state', () => {
    mockUseKbDocConsumingAgents.mockReturnValue({
      isLoading: false,
      isError: true,
      error: new Error('boom'),
      data: undefined,
    });
    render(<UsedByPanel docId="doc-1" />);
    expect(screen.getByTestId('used-by-panel-error')).toBeInTheDocument();
    expect(screen.getByText(/boom/)).toBeInTheDocument();
  });

  it('renders the empty state when no consumers', () => {
    mockUseKbDocConsumingAgents.mockReturnValue({ isLoading: false, isError: false, data: [] });
    render(<UsedByPanel docId="doc-1" />);
    expect(screen.getByTestId('used-by-empty')).toBeInTheDocument();
  });

  it('renders the list when consumers are present', () => {
    mockUseKbDocConsumingAgents.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [agent, { ...agent, id: '22222222-2222-2222-2222-222222222222', name: 'Beta' }],
    });
    render(<UsedByPanel docId="doc-1" />);
    expect(screen.getByTestId('used-by-panel')).toBeInTheDocument();
    expect(screen.getByText(/Agent che consumano questo documento \(2\)/)).toBeInTheDocument();
    expect(screen.getByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
  });
});
```

- [ ] **Step 9.3: Run panel tests**

```bash
pnpm test --run components/admin/knowledge-base/explorer/used-by/__tests__/UsedByPanel
```
Expected: 4 passed.

- [ ] **Step 9.4: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/used-by/UsedByPanel.tsx \
        apps/web/src/components/admin/knowledge-base/explorer/used-by/__tests__/UsedByPanel.test.tsx
git commit -m "feat(admin-kb): #1651 UsedByPanel container + 4-state tests"
```

---

## Task 10: Frontend — Extend KbDocDetailTabs with "Used by" + test

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx`

- [ ] **Step 10.1: Modify KbDocDetailTabs**

Edit `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx`. Replace ONLY the `KbDocTabKey` type declaration (line 5) and the `TABS` array (lines 12-15). Do NOT touch the `KbDocDetailTabsProps` interface (lines 7-10) — it stays as-is.

New `KbDocTabKey` line:

```tsx
export type KbDocTabKey = 'overview' | 'ingestion' | 'used-by';
```

New `TABS` array:

```tsx
const TABS: ReadonlyArray<{ readonly key: KbDocTabKey; readonly label: string }> = [
  { key: 'overview', label: 'Overview' },
  { key: 'ingestion', label: 'Ingestion log' },
  { key: 'used-by', label: 'Used by' },
];
```

Update the doc comment (line 17-21) to reflect that this issue lands the third tab:

```tsx
/**
 * Inner tab nav for `KbDocDetailPanel`. URL-driven via `?tab=overview` (default)
 * | `?tab=ingestion` | `?tab=used-by`. Preserves `docId` in each link.
 * Issues #1650 (Ingestion log) + #1651 (Used by). Future follow-ups #1653/#1654.
 */
```

- [ ] **Step 10.2: Write tabs test**

Create `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import type { ReactNode } from 'react';

import { KbDocDetailTabs } from '../KbDocDetailTabs';

vi.mock('next/link', () => ({
  default: ({ href, children, ...rest }: { href: string; children: ReactNode; [k: string]: unknown }) => (
    <a href={href} {...rest}>{children}</a>
  ),
}));

describe('KbDocDetailTabs', () => {
  it('renders three tabs: Overview, Ingestion log, Used by', () => {
    render(<KbDocDetailTabs docId="doc-1" activeTab="overview" />);
    expect(screen.getByRole('link', { name: 'Overview' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ingestion log' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Used by' })).toBeInTheDocument();
  });

  it('Overview link omits the tab param (default)', () => {
    render(<KbDocDetailTabs docId="doc-1" activeTab="overview" />);
    const link = screen.getByRole('link', { name: 'Overview' });
    expect(link).toHaveAttribute('href', '/admin/knowledge-base?docId=doc-1');
  });

  it('Used by link sets ?tab=used-by and preserves docId', () => {
    render(<KbDocDetailTabs docId="doc-1" activeTab="overview" />);
    const link = screen.getByRole('link', { name: 'Used by' });
    expect(link).toHaveAttribute('href', '/admin/knowledge-base?docId=doc-1&tab=used-by');
  });

  it('marks the active tab with aria-current="page"', () => {
    render(<KbDocDetailTabs docId="doc-1" activeTab="used-by" />);
    const link = screen.getByRole('link', { name: 'Used by' });
    expect(link).toHaveAttribute('aria-current', 'page');
  });
});
```

- [ ] **Step 10.3: Run tabs tests**

```bash
pnpm test --run components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs
```
Expected: 4 passed.

- [ ] **Step 10.4: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx \
        apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx
git commit -m "feat(admin-kb): #1651 add Used-by tab to KbDocDetailTabs"
```

---

## Task 11: Frontend — Wire KbDocDetailPanel branch + extend existing test

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx`

- [ ] **Step 11.1: Modify KbDocDetailPanel**

Edit `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`. Add the new import after the existing `IngestionPanel` import (line 9):

```tsx
import { IngestionPanel } from './ingestion/IngestionPanel';
import { KbDocDetailTabs, type KbDocTabKey } from './KbDocDetailTabs';
import { UsedByPanel } from './used-by/UsedByPanel';
```

Then replace the `activeTab` parsing line (line 48-49):

```tsx
  const activeTab: KbDocTabKey =
    searchParams?.get('tab') === 'ingestion' ? 'ingestion' : 'overview';
```

with:

```tsx
  const activeTab: KbDocTabKey = (() => {
    const tab = searchParams?.get('tab');
    if (tab === 'ingestion') return 'ingestion';
    if (tab === 'used-by') return 'used-by';
    return 'overview';
  })();
```

Then replace the ready-branch render block (lines 109-184) — specifically the ternary `{activeTab === 'ingestion' ? <IngestionPanel ... /> : <>... overview ...</>}` — with a 3-branch render. Replace the block:

```tsx
      {activeTab === 'ingestion' ? (
        <IngestionPanel docId={doc.id} chunkCount={doc.chunkCount} pageCount={doc.pageCount ?? 0} />
      ) : (
        <>
          {/* Hero */}
          ...
        </>
      )}
```

with:

```tsx
      {activeTab === 'ingestion' && (
        <IngestionPanel docId={doc.id} chunkCount={doc.chunkCount} pageCount={doc.pageCount ?? 0} />
      )}

      {activeTab === 'used-by' && <UsedByPanel docId={doc.id} />}

      {activeTab === 'overview' && (
        <>
          {/* Hero */}
          <header className="p-5 border-b border-border/60 dark:border-zinc-700/60 bg-gradient-to-b from-amber-500/5 to-transparent">
            <div className="flex items-start gap-4">
              <span aria-hidden="true" className="text-3xl">
                📄
              </span>
              <div className="flex-1 min-w-0">
                <h2 className="font-quicksand font-bold text-lg truncate">{doc.title}</h2>
                <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted-foreground font-mono">
                  <span>{doc.gameName ?? 'KB globale'}</span>
                  <span aria-hidden="true">·</span>
                  <span>{doc.docType}</span>
                  <span aria-hidden="true">·</span>
                  <span>uploaded {formatDate(doc.uploadedAt)}</span>
                  <span
                    className={`ml-auto inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border ${processingChipClass(doc.processingStatus)}`}
                  >
                    {doc.processingStatus}
                  </span>
                </div>
              </div>
            </div>

            <dl className="mt-4 grid grid-cols-3 gap-2">
              <Stat label="Chunks" value={doc.chunkCount.toLocaleString('it-IT')} />
              <Stat label="Pagine" value={doc.pageCount?.toLocaleString('it-IT') ?? '—'} />
              <Stat label="Lingua" value={doc.language} />
            </dl>
          </header>

          {/* Chunks */}
          <section className="p-4">
            <h3 className="font-quicksand font-semibold text-sm mb-2">Chunks</h3>
            <ul className="divide-y divide-border/60 dark:divide-zinc-700/60">
              {chunks.map(c => (
                <li key={c.id} className="py-2.5">
                  <div className="flex items-center gap-2 text-[10.5px] font-mono text-muted-foreground mb-0.5">
                    <code>c-{c.position.toString().padStart(4, '0')}</code>
                    {c.pageNumber !== null && <span>· p. {c.pageNumber}</span>}
                    {c.headingPath.length > 0 && (
                      <>
                        <span aria-hidden="true">·</span>
                        <span className="truncate" data-testid="kb-chunk-heading">
                          {c.headingPath.join(' › ')}
                        </span>
                      </>
                    )}
                  </div>
                  <p className="text-[12.5px] text-foreground leading-snug line-clamp-2">
                    {c.snippet}
                  </p>
                </li>
              ))}
            </ul>
            {chunksQuery.hasNextPage && (
              <button
                type="button"
                onClick={() => chunksQuery.fetchNextPage()}
                disabled={chunksQuery.isFetchingNextPage}
                className="mt-3 w-full text-center text-xs font-medium text-amber-700 dark:text-amber-300 border border-border/60 dark:border-zinc-700/60 rounded-md py-2 hover:bg-muted/70 disabled:opacity-60"
              >
                {chunksQuery.isFetchingNextPage ? 'Caricamento…' : 'Carica altri'}
              </button>
            )}
          </section>
        </>
      )}
```

- [ ] **Step 11.2: Add mock + test for used-by branch in existing panel test**

Edit `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx`. Find the existing `vi.mock('@/lib/api/admin-kb-ingestion', ...)` block (around line 40) and add immediately AFTER it:

```tsx
// ── admin-kb-used-by mock (UsedByPanel uses fetchKbDocConsumingAgents) ─────────
vi.mock('@/lib/api/admin-kb-used-by', () => ({
  fetchKbDocConsumingAgents: vi.fn().mockResolvedValue([]),
}));
```

Then at the end of the `describe('KbDocDetailPanel', ...)` block, immediately BEFORE the closing `});`, add the new test:

```tsx
  it('renders the Used-by tab when ?tab=used-by is present', async () => {
    mockSearchParams = new URLSearchParams('tab=used-by');
    mockUseKbDocDetail.mockReturnValue({ data: readyEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({ data: undefined, hasNextPage: false });

    render(<KbDocDetailPanel docId="doc-1" />, { wrapper: makeWrapper() });

    await waitFor(() => {
      // Empty list (mock returns []) → empty state renders.
      expect(screen.getByTestId('used-by-empty')).toBeInTheDocument();
    });

    // Overview content must NOT be present when used-by is active.
    expect(screen.queryByText('Chunks')).not.toBeInTheDocument();
  });
```

- [ ] **Step 11.3: Run panel tests**

```bash
pnpm test --run components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel
```
Expected: all previous tests still pass + 1 new test passes. If a pre-existing test fails because the `used-by-empty` testid pollutes another test's render, check the `beforeEach` resets `mockSearchParams` to `''`.

- [ ] **Step 11.4: Typecheck**

```bash
pnpm typecheck
```
Expected: 0 errors.

- [ ] **Step 11.5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx \
        apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx
git commit -m "feat(admin-kb): #1651 wire UsedByPanel into KbDocDetailPanel"
```

---

## Task 12: Final verification + PR

**Files:** (none — verification only)

- [ ] **Step 12.1: Full backend test sweep**

From `apps/api/tests/Api.Tests/`:
```bash
dotnet test --filter "Trait[Issue]=1651" --nologo
```
Expected: all #1651 tests pass (5 unit + 7 integration = 12 tests minimum).

- [ ] **Step 12.2: Full frontend test + lint + typecheck**

From `apps/web/`:
```bash
pnpm lint && pnpm typecheck && pnpm test --run
```
Expected: 0 lint errors, 0 type errors, no new test failures vs. baseline.

- [ ] **Step 12.3: Push branch**

```bash
git push -u origin feature/issue-1651-used-by-tab
```

- [ ] **Step 12.4: Open PR to main-dev**

```bash
gh pr create --base main-dev --title "feat(admin-kb): #1651 F3-FU-2 — Used-by tab in KbDocDetailPanel" --body "$(cat <<'EOF'
## Summary

Implements F3-FU-2 (#1651): a new **Used by** tab inside `KbDocDetailPanel` showing the AI agents that explicitly consume the selected PDF document via `AgentDefinition.KbCardIds`.

- **Backend**: `GET /api/v1/admin/kb/docs/{docId}/agents` backed by Postgres JSONB containment `kb_card_ids @> '["docId"]'::jsonb` + GIN index; new dedicated DTO `KbDocConsumingAgentDto` (system agents included with `isSystemDefined` flag + typology slug).
- **Frontend**: URL-driven tab `?tab=used-by` mirroring #1650 pattern. New `used-by/` folder: `UsedByPanel` + `UsedByAgentRow` + `UsedByEmptyState`. Hook `useKbDocConsumingAgents` (no polling — association is static). Read-only with link to `/admin/agents/definitions/{agentId}`.

**Design doc**: `docs/superpowers/specs/2026-05-29-kb-used-by-tab-design.md`
**Plan**: `docs/superpowers/plans/2026-05-29-kb-used-by-tab.md`

## Decisions (product)
- Consumo = solo `KbCardIds` esplicito (no game-scoping → zero falsi positivi) · read-only + link (no detach) · mostra anche system agent con badge "sistema".

## Decisions (technical, post spec-panel review)
- JSONB `@>` containment + GIN index (NOT in-memory scan — auto-agent private-game rendono il totale non limitato).
- DTO dedicato (NOT `AgentDto` riusato — no shared models, regola di progetto + system flag mancante).

## Test plan
- [x] Backend: 5 unit tests (handler mapping) + 7 integration tests Testcontainers (AC1–AC8) — JSONB `@>` non è mockabile su EF InMemory.
- [x] Frontend: hook (4) + row (7) + panel (4) + tabs (4) + existing panel test extension (1) = 20+ tests.
- [x] Lint + typecheck verdi (FE), `dotnet build` + test #1651 verdi (BE).
- [ ] CI: full BE + FE suite — confirm no regression vs baseline.
- [ ] Manual: `/admin/knowledge-base?docId=<existing>&tab=used-by` → lista o empty state; click riga → naviga al detail agent.

## Deployment note
Staging migrations are not auto-applied. After merge, run `dotnet ef database update` on staging (per project memory).

## Out of scope (follow-ups)
- Detach (rimuovi doc da agent.KbCardIds) — deferito.
- Paginazione / filtri / polling — non necessari a questa scala.
- Badge count nel sub-nav — coperto da #1655.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 12.5: Verify PR URL + base branch**

Run:
```bash
gh pr view --json baseRefName,url
```
Expected: `baseRefName: "main-dev"` (CRITICAL — NOT `main`); URL printed.

---

## Self-Review Checklist (run after writing plan; this section informational)

1. **Spec coverage**: all 6 spec-panel findings → addressed by tasks (JSONB+GIN T3, dedicated DTO T1, AC1-AC8 T6, integration tests T6, edge cases T6, link target T8). ✅
2. **Placeholder scan**: every step has complete code or exact commands. ✅
3. **Type consistency**: `KbDocConsumingAgentDto` field names (camelCase in JSON, PascalCase in C#) consistent across DTO, schema, hook, row, test. ✅
