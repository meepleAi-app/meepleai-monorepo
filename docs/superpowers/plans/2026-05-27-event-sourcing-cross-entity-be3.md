# BE-3 #1590 — Event Sourcing Cross-Entity (Agent / Chat / KB / Session) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `EventTypeRegistry` with 5 new domain events (`agent.created`, `chat.session.created`, `kb.doc.indexed`, `session.created`, `session.finalized`) so that the durable activity log (`domain_event_logs` table) can power a cross-entity activity feed via a new endpoint `GET /api/v1/activity`. Forward-only — no backfill. Unblocks Phase 3b #1593 rail.

**Architecture:** Each new event is a domain event raised via the existing `IDomainEventSource` pipeline (`MeepleAiDbContext.SaveChangesAsync` intercepts events from aggregates, maps them via `DomainEventLogMapper`, inserts log rows atomically with aggregate state). The `Session` aggregate gets a minimal `IDomainEventSource` implementation (not full AggregateRoot conversion) so it can participate. The legacy `/library/activity` endpoint is preserved (mapping layer enriches `gameTitle` via bulk lookup). A new `GET /api/v1/activity` endpoint exposes the generalized cross-entity feed. Two new Prometheus counters expose insert/dispatch-failure rates.

**Tech Stack:** .NET 9 · MediatR · EF Core 9 (Postgres) · FluentValidation · xUnit + FluentAssertions + Moq · Testcontainers Postgres · System.Diagnostics.Metrics + OpenTelemetry · Meziantou.Analyzer

**Spec source:** Issue #1590 body (refined 2026-05-27 via /sc:spec-panel discussion + socratic; 20 locked decisions A1-H2 + C3.1-C3.4).

**⚠️ Branch policy:** parent = `main-dev`. PR target = `main-dev`. Commit messages end with `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>`. Headers ≤80 chars.

---

## File Structure

| File | Responsibility | Status |
|---|---|---|
| `apps/api/src/Api/Infrastructure/Entities/DomainEventLog/DomainEventLog.cs` | Add `PayloadVersion` int property (default 1) | Modify |
| `apps/api/src/Api/Infrastructure/EntityConfigurations/DomainEventLogEntityConfiguration.cs` | Add EF config for `PayloadVersion`, XML doc on `AggregateId`/`AggregateType` declaring logical FKs (AC9) | Modify |
| `apps/api/src/Api/Infrastructure/Migrations/YYYYMMDDHHMMSS_AddPayloadVersionToDomainEventLogs.cs` | EF migration: add `PayloadVersion INT NOT NULL DEFAULT 1` column | Create |
| `apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs` | Register 5 new event types | Modify |
| `apps/api/src/Api/Infrastructure/DomainEventLog/DomainEventLogMapper.cs` | Map `PayloadVersion` from event to entity (default 1 v1) | Modify |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentCreatedEvent.cs` | New domain event for agent.created | Create |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateUserAgentCommandHandler.cs` | Add `AddDomainEvent(AgentCreatedEvent)` after agent saved | Modify |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs` | Verify `AggregateRoot<Guid>` base (already has `AddDomainEvent`); add `RaiseAgentCreatedEvent(userId, gameName?)` method | Modify |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/ChatSessionCreatedEvent.cs` | New domain event for chat.session.created | Create |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatSessionCommandHandler.cs` | Add domain event emission | Modify |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Events/KbDocIndexedEvent.cs` | New domain event for kb.doc.indexed | Create |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs` | `TransitionTo(Ready)` adds `KbDocIndexedEvent` after existing `PdfStateChangedEvent` | Modify |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs` | Light refactor: implement `IDomainEventSource` (private list + `AddDomainEvent`/`PeekEvents`/`ClearEvents`) — NO full AggregateRoot conversion | Modify |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionCreatedEvent.cs` | New domain event for session.created | Create |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionFinalizedEvent.cs` | New domain event for session.finalized | Create |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandHandler.cs` | Add `session.AddDomainEvent(SessionCreatedEvent)` before SaveChangesAsync | Modify |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/FinalizeSessionCommandHandler.cs` | Add `session.AddDomainEvent(SessionFinalizedEvent)` before SaveChangesAsync | Modify |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetActivityFeedQuery.cs` | New cross-entity query (returns generalized `ActivityItemDto`) | Create |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetActivityFeedQueryHandler.cs` | Reads `domain_event_logs` for `userId=caller`, maps to DTO | Create |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetActivityFeedQueryValidator.cs` | Validates limit (1-100) + since (parseable ISO-8601) | Create |
| `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ActivityItemDto.cs` | Generalized response DTO | Create |
| `apps/api/src/Api/Routing/ActivityEndpoints.cs` | `GET /api/v1/activity` endpoint registration | Create |
| `apps/api/src/Api/Routing/RouteRegistry.cs` | Wire `ActivityEndpoints.Map(...)` | Modify |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQueryHandler.cs` | Replace placeholder `"Game"` with `SharedGameRepository.GetNamesByIds` bulk join (D1) + soft-delete handling (D2) | Modify |
| `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.DomainEventLog.cs` | New partial: `DomainEventsInserted` + `DomainEventDispatchFailures` counters (G1, G2) | Create |
| `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs:lines 449-470` | Increment `DomainEventsInserted` after `DomainEventLogs.Add`; increment `DomainEventDispatchFailures` in the catch-around-mediator-publish block | Modify |
| `apps/api/tests/Api.Tests/Infrastructure/DomainEventLog/EventTypeRegistryTests.cs` | Add assertion: all 5 new entries resolve to loaded IDomainEvent types | Modify |
| `apps/api/tests/Api.Tests/Integration/Events/AgentCreatedIntegrationTests.cs` | End-to-end: CreateUserAgent → log row + endpoint surface (AC5) | Create |
| `apps/api/tests/Api.Tests/Integration/Events/ChatSessionCreatedIntegrationTests.cs` | End-to-end (AC5) | Create |
| `apps/api/tests/Api.Tests/Integration/Events/KbDocIndexedIntegrationTests.cs` | End-to-end (AC5) | Create |
| `apps/api/tests/Api.Tests/Integration/Events/SessionLifecycleIntegrationTests.cs` | End-to-end + cross-table assertion (AC5.b — both `session_events` and `domain_event_logs`, timestamps <100ms) | Create |
| `apps/api/tests/Api.Tests/Integration/Events/ActivityFeedEndpointIntegrationTests.cs` | Endpoint integration (AC2, AC3 forward-only/empty-state, AC4 legacy backward-compat) | Create |
| `apps/api/tests/Api.Tests/Observability/Metrics/DomainEventLogMetricsTests.cs` | Asserts counters increment correctly | Create |
| `apps/api/tests/Api.Tests/Architecture/EventLogBoundaryTests.cs` | AC8: assert `GetSessionDiaryQueryHandler` has NO dependency on `IDomainEventLogRepository` and vice versa | Create |

---

## Verified reference facts (from BE exploration — do not re-discover)

- **`DomainEventLogEntity`** (`apps/api/src/Api/Infrastructure/Entities/DomainEventLog/DomainEventLog.cs:15-52`): Fields `Id` (Guid PK), `EventId` (Guid unique), `EventType` (string ≤128), `UserId` (Guid?), `AggregateId` (Guid?), `AggregateType` (string? ≤64), `PayloadJson` (jsonb), `OccurredAt` (DateTime), `LoggedAt` (DateTime). **NO** `PayloadVersion` yet — Task 1 adds it.
- **`EventTypeRegistry`** (`apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs:30-36`): `public static class` with `Dictionary<Type, string> _aliasByType`. Today 2 entries: `[typeof(GameRemovedFromLibraryEvent)] = "library.entry.removed"`, `[typeof(GameSessionRecordedEvent)] = "library.session.recorded"`.
- **`MeepleAiDbContext.SaveChangesAsync`** (`apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs:434-483`): Snapshot via `IDomainEventCollector.PeekEvents()` → `DomainEventLogMapper.Map()` → `DomainEventLogs.Add(...)` → `base.SaveChangesAsync` (atomic) → MediatR `Publish` (handler failures log ERROR but DO NOT rollback).
- **`DomainEventLogMapper`** (verified): translates `IDomainEvent` → `DomainEventLogEntity`. Reads `EventTypeRegistry.AliasByType[event.GetType()]` for `EventType`. Reads `event.EventId`/`OccurredAt`/`UserId` via reflection or direct property access. For new events, all expose `Guid EventId` (random) + `DateTime OccurredAt` (UtcNow) + `Guid UserId` properties.
- **`AggregateRoot<TId>`** base (verified via `UserLibraryEntry` usage `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/UserLibraryEntry.cs:194-196`): exposes `protected void AddDomainEvent(IDomainEvent)`. `IDomainEventCollector.PeekEvents()` collects from all tracked `IDomainEventSource` instances.
- **`Session` aggregate** (`apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs`): currently does NOT inherit `AggregateRoot` and does NOT implement `IDomainEventSource`. Task 7 adds it.
- **`AgentDefinition` aggregate** (`apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs`): DOES inherit `AggregateRoot<Guid>`. `AddDomainEvent` available.
- **`PdfDocument` aggregate** (`apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs:382`): DOES inherit `AggregateRoot`. Already emits `PdfStateChangedEvent` in `TransitionTo`. Task 6 appends `KbDocIndexedEvent` when newState==Ready.
- **`CreateUserAgentCommandHandler`** (verified): saves at line 112 via `_unitOfWork.SaveChangesAsync(ct)`. The `agent` instance is mutable here — call `agent.AddDomainEvent(...)` BEFORE save.
- **`CreateChatSessionCommandHandler`** (verified): saves at line 99. `ChatSession` is NOT an `AggregateRoot` today — for chat, the cleanest fix is the **inline-after-save** pattern: emit via the `IDomainEventCollector` directly post-handler? **No — defer this Task 5 decision: extend `ChatSession` to implement `IDomainEventSource` (same light pattern as Session in Task 7), then `chatSession.AddDomainEvent(...)` before save.**
- **`SharedGameRepository.GetNamesByIds`** (used in BE-1 #1588): `Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)`. Returns Title for non-soft-deleted SharedGames.
- **`IUnitOfWork.SaveChangesAsync`** delegates to `MeepleAiDbContext.SaveChangesAsync` (verified).
- **`MeepleAiMetrics` pattern** (`apps/api/src/Api/Observability/MeepleAiMetrics.cs:33`): `internal static partial class` with shared `Meter Meter = new("MeepleAI.Api", "1.0.0")`. Partial files in `apps/api/src/Api/Observability/Metrics/`. Counter naming: `meepleai.<domain>.<name>.total`. Counter increment with `TagList` for labels.
- **`TestSessionHelper.SeedSharedGameAsync`** (verified BE-2 Task 4) — pass `title` string. Returns `Guid`.
- **`TestSessionHelper.CreateUserSessionAsync`** (verified) — returns `(Guid UserId, string RawToken)`.
- **`TestSessionHelper.CreateAuthenticatedRequest`** (verified) — `(HttpMethod, requestUri, sessionToken)`.
- **`ApiExceptionHandlerMiddleware`** maps FluentValidation `ValidationException` → 422 (verified BE-1 #1588).
- **`SessionEntity` in DbContext** (`session_tracking_sessions` table) is read/written via `_db.Sessions.Add(...)` and saved via `SaveChangesAsync` (verified). The `Session` domain entity maps to `SessionEntity`.

---

## ⚠️ Brownfield risks
- **R1**: `Session` light refactor (Task 7) MUST NOT change any existing behavior — `Session` is not an `AggregateRoot`, just gains `IDomainEventSource` implementation. Existing tests that construct `Session` directly MUST continue to compile (additive interface).
- **R2**: `PayloadVersion` column migration (Task 1) — verify the column default `1` propagates to existing rows (no `NOT NULL DEFAULT 1` constraint = failed migration on filled tables). Use `defaultValue: 1`.
- **R3**: Naming `chat.session.created` — `CreateChatSessionCommandHandler` saves a `ChatSession` (not `ChatThread`). The "thread" terminology in the original issue body was an aliasing error; the FE consumer (#1593) does not require the literal word "thread" — only an event tied to chat creation. The alias `chat.session.created` is semantically and code-name-aligned.
- **R4**: `useActivityFeed` FE collision is **NOT in this plan** — tracked in #1593 (E1). Implementer should not touch the FE.

---

## Task 1: Add `PayloadVersion` column + EF migration

**Goal**: Add the `PayloadVersion` column to `domain_event_logs` (A2, Hohpe non-negotiable).

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/DomainEventLog/DomainEventLog.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/DomainEventLogEntityConfiguration.cs`
- Create: `apps/api/src/Api/Infrastructure/Migrations/YYYYMMDDHHMMSS_AddPayloadVersionToDomainEventLogs.cs` (auto-generated)

- [ ] **Step 1: Add the `PayloadVersion` property to the entity**

In `DomainEventLog.cs`, after the `PayloadJson` property, add:

```csharp
/// <summary>
/// Schema version of the event payload, used for forward-compatible migrations.
/// v1 events: PayloadVersion = 1. New payload schemas (with breaking changes) increment this.
/// </summary>
public int PayloadVersion { get; set; } = 1;
```

- [ ] **Step 2: Update the EF Configuration**

In `DomainEventLogEntityConfiguration.cs`, inside the `Configure` method, after the existing `Property` definitions, add:

```csharp
builder.Property(e => e.PayloadVersion)
    .IsRequired()
    .HasDefaultValue(1);
```

Also add XML doc to the entity class (in the SAME file, for AC9) — modify the existing `AggregateType` and `AggregateId` property XML docs:

In `DomainEventLog.cs`, update the doc comments on `AggregateType` and `AggregateId`:

```csharp
/// <summary>
/// PascalCase aggregate name. Logical FK pointer to a specific aggregate table:
///   - "Session"     → sessions_tracking.id
///   - "Agent"       → agent_definitions.id
///   - "ChatSession" → chat_sessions.id
///   - "PdfDocument" → pdf_documents.id
///   - "UserLibraryEntry" → user_library_entries.id (existing library.* events)
/// Not enforced at DB level — append-only audit log policy (AC9, #1590).
/// </summary>
public string? AggregateType { get; set; }

/// <summary>
/// Logical FK to the aggregate root. See <see cref="AggregateType"/> for the mapping
/// from PascalCase aggregate name to physical table.
/// </summary>
public Guid? AggregateId { get; set; }
```

- [ ] **Step 3: Generate the migration**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api/src/Api
dotnet ef migrations add AddPayloadVersionToDomainEventLogs --no-build
```

Expected: a new file `apps/api/src/Api/Infrastructure/Migrations/YYYYMMDDHHMMSS_AddPayloadVersionToDomainEventLogs.cs` containing an `Up()` that adds the `PayloadVersion` column with `defaultValue: 1` and `nullable: false`, and a `Down()` that drops the column.

If `dotnet ef` blocks (slow EF tooling on Windows), the migration body should be:

```csharp
public partial class AddPayloadVersionToDomainEventLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "PayloadVersion",
            table: "domain_event_logs",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PayloadVersion",
            table: "domain_event_logs");
    }
}
```

- [ ] **Step 4: Update the `DomainEventLogMapper`**

In `apps/api/src/Api/Infrastructure/DomainEventLog/DomainEventLogMapper.cs`, find the line that constructs the `DomainEventLogEntity`. Add `PayloadVersion = 1` to the initializer (all v1 events default to 1; future events with breaking payload changes set it explicitly):

```csharp
return new DomainEventLogEntity
{
    Id = Guid.NewGuid(),
    EventId = domainEvent.EventId,
    EventType = alias,
    UserId = ExtractUserId(domainEvent),
    AggregateId = ExtractAggregateId(domainEvent),
    AggregateType = aggregateType,
    OccurredAt = domainEvent.OccurredAt,
    LoggedAt = DateTime.UtcNow,
    PayloadJson = JsonSerializer.Serialize(domainEvent, SerializerOptions),
    PayloadVersion = 1, // BE-3 #1590 (A2): all v1 events default to 1
};
```

- [ ] **Step 5: Build the project**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet build src/Api/Api.csproj --nologo -v q
```

Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/api/src/Api/Infrastructure/Entities/DomainEventLog/DomainEventLog.cs apps/api/src/Api/Infrastructure/EntityConfigurations/DomainEventLogEntityConfiguration.cs apps/api/src/Api/Infrastructure/DomainEventLog/DomainEventLogMapper.cs apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(events): #1590 add PayloadVersion column to domain_event_logs (BE-3)"
```

---

## Task 2: `AgentCreatedEvent` end-to-end (event + registry + emit + integration test)

**Goal**: Implement `agent.created` event emission from `CreateUserAgentCommandHandler` + integration test (H1, AC5).

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentCreatedEvent.cs`
- Modify: `apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateUserAgentCommandHandler.cs`
- Create: `apps/api/tests/Api.Tests/Integration/Events/AgentCreatedIntegrationTests.cs`

- [ ] **Step 1: Write the failing integration test FIRST**

Create `apps/api/tests/Api.Tests/Integration/Events/AgentCreatedIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventLog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Events;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentCreatedIntegrationTests : IClassFixture<IntegrationWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly IntegrationWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AgentCreatedIntegrationTests(IntegrationWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateUserAgent_LogsAgentCreatedEvent_InDomainEventLogs()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Agent");

        var createRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/agents/quick-create",
            sessionToken,
            new { gameId });

        // Act
        var response = await _client.SendAsync(createRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        // Re-fetch the dbContext from a fresh scope to read post-commit state
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var logRow = await verifyDb.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.EventType == "agent.created" && e.UserId == userId)
            .OrderByDescending(e => e.LoggedAt)
            .FirstOrDefaultAsync();

        logRow.Should().NotBeNull("CreateUserAgentCommand must emit agent.created to domain_event_logs");
        logRow!.EventType.Should().Be("agent.created");
        logRow.UserId.Should().Be(userId);
        logRow.AggregateType.Should().Be("Agent");
        logRow.AggregateId.Should().NotBe(Guid.Empty);
        logRow.PayloadVersion.Should().Be(1);
        logRow.PayloadJson.Should().Contain("\"GameId\"").And.Contain(gameId.ToString());
        logRow.PayloadJson.Should().Contain("\"GameName\"").And.Contain("Catan-BE3-Agent");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AgentCreatedIntegrationTests" --logger "console;verbosity=minimal"
```
Expected: FAIL — no `agent.created` row inserted (event not yet defined or emitted).

- [ ] **Step 3: Create the domain event**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentCreatedEvent.cs`:

```csharp
using Api.SharedKernel.Domain.Events;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Raised by <see cref="AgentDefinition"/> aggregate when a user creates a new agent via
/// <c>CreateUserAgentCommand</c> (user-facing flow). NOT raised for admin/AI-Lab path
/// (<c>CreateAgentDefinitionCommand</c>) — see #1590 decision H1.
/// Registered in <c>EventTypeRegistry</c> with alias <c>"agent.created"</c>.
/// </summary>
internal sealed record AgentCreatedEvent(
    Guid AgentId,
    Guid UserId,
    string AgentType,
    bool IsActive,
    Guid? GameId,
    string? GameName,
    string AgentName,
    DateTime OccurredAt
) : IDomainEvent, INotification
{
    public Guid EventId { get; } = Guid.NewGuid();
}
```

- [ ] **Step 4: Register in `EventTypeRegistry`**

In `apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs`, add the new entry to `_aliasByType`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
// ... existing usings

private static readonly Dictionary<Type, string> _aliasByType = new()
{
    [typeof(GameRemovedFromLibraryEvent)] = "library.entry.removed",
    [typeof(GameSessionRecordedEvent)] = "library.session.recorded",
    [typeof(AgentCreatedEvent)] = "agent.created", // BE-3 #1590
};
```

- [ ] **Step 5: Emit the event from the command handler**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateUserAgentCommandHandler.cs`, find the line where the agent has been saved (around line 112, after `_unitOfWork.SaveChangesAsync(ct)`).

Move the event emission to BEFORE `SaveChangesAsync` (so it's atomic with the save). The handler currently has:

```csharp
// existing code (approximate location):
_agentRepository.Add(agent);
await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

Modify to:

```csharp
// BE-3 #1590: emit agent.created BEFORE save so the log row is atomic with the aggregate
agent.AddDomainEvent(new AgentCreatedEvent(
    AgentId: agent.Id,
    UserId: request.UserId,
    AgentType: agent.Type.Value,
    IsActive: agent.IsActive,
    GameId: agent.GameId,
    GameName: gameName, // already resolved earlier in handler from request.GameId
    AgentName: agent.Name,
    OccurredAt: DateTime.UtcNow
));

_agentRepository.Add(agent);
await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

Notes:
- `agent.AddDomainEvent(...)` is inherited from `AggregateRoot<Guid>` (already the base class of `AgentDefinition`).
- `gameName` should already be available in the handler context (it's used to populate `AgentDto.GameName`). If it isn't, fetch it via `_sharedGameRepository.GetNamesByIdsAsync(new[] { agent.GameId.Value }, ct)` before constructing the event.

- [ ] **Step 6: Run the integration test to verify it passes**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AgentCreatedIntegrationTests" --logger "console;verbosity=minimal"
```
Expected: PASS. Testcontainers spin-up time ~30-60s.

- [ ] **Step 7: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentCreatedEvent.cs apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateUserAgentCommandHandler.cs apps/api/tests/Api.Tests/Integration/Events/AgentCreatedIntegrationTests.cs
git commit -m "feat(events): #1590 emit agent.created event from CreateUserAgent (BE-3)"
```

---

## Task 3: `ChatSessionCreatedEvent` end-to-end

**Goal**: Implement `chat.session.created` event emission (H2, AC5).

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/ChatSessionCreatedEvent.cs`
- Modify: `apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/ChatSession.cs` (add `IDomainEventSource` light implementation)
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatSessionCommandHandler.cs`
- Create: `apps/api/tests/Api.Tests/Integration/Events/ChatSessionCreatedIntegrationTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `apps/api/tests/Api.Tests/Integration/Events/ChatSessionCreatedIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Events;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ChatSessionCreatedIntegrationTests : IClassFixture<IntegrationWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly IntegrationWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ChatSessionCreatedIntegrationTests(IntegrationWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateChatSession_LogsChatSessionCreatedEvent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Chat");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/chat/sessions",
            sessionToken,
            new { gameId });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logRow = await verifyDb.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.EventType == "chat.session.created" && e.UserId == userId)
            .OrderByDescending(e => e.LoggedAt)
            .FirstOrDefaultAsync();

        logRow.Should().NotBeNull();
        logRow!.AggregateType.Should().Be("ChatSession");
        logRow.PayloadVersion.Should().Be(1);
        logRow.PayloadJson.Should().Contain("\"GameId\"").And.Contain(gameId.ToString());
        logRow.PayloadJson.Should().Contain("\"GameName\"").And.Contain("Catan-BE3-Chat");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ChatSessionCreatedIntegrationTests" --logger "console;verbosity=minimal"
```
Expected: FAIL.

- [ ] **Step 3: Create the domain event**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/ChatSessionCreatedEvent.cs`:

```csharp
using Api.SharedKernel.Domain.Events;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Raised when a user creates a new chat session via <c>CreateChatSessionCommand</c>.
/// Registered in <c>EventTypeRegistry</c> with alias <c>"chat.session.created"</c>.
/// Renamed from <c>chat.thread.created</c> (#1590 decision H2 — match the real BE command name
/// <c>CreateChatSessionCommand</c>, not the fictional <c>CreateChatThreadCommand</c>).
/// </summary>
internal sealed record ChatSessionCreatedEvent(
    Guid ChatSessionId,
    Guid UserId,
    Guid? GameId,
    string? GameName,
    Guid? AgentId,
    string? AgentName,
    DateTime OccurredAt
) : IDomainEvent, INotification
{
    public Guid EventId { get; } = Guid.NewGuid();
}
```

- [ ] **Step 4: Add `IDomainEventSource` to `ChatSession`**

`ChatSession` is currently not an `AggregateRoot`. Light refactor (see #1590 decision C1 — same pattern as Session in Task 7).

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/ChatSession.cs`, change the class declaration:

```csharp
// BEFORE: public sealed class ChatSession
// AFTER:
public sealed class ChatSession : IDomainEventSource
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    // ... existing members unchanged ...
}
```

Add `using Api.SharedKernel.Domain.Events;` at the top if missing.

Verify the `IDomainEventSource` interface signature in `apps/api/src/Api/SharedKernel/Domain/Events/IDomainEventSource.cs` and match it exactly (if it has slightly different method names, adapt).

- [ ] **Step 5: Emit the event from the command handler**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatSessionCommandHandler.cs`, locate the line creating the `ChatSession` instance (around line 95 — the line that does `new ChatSession(...)`). Just before `_unitOfWork.SaveChangesAsync(...)` (around line 99), add:

```csharp
// BE-3 #1590: emit chat.session.created BEFORE save (atomic with aggregate persistence)
chatSession.AddDomainEvent(new ChatSessionCreatedEvent(
    ChatSessionId: chatSession.Id,
    UserId: request.UserId,
    GameId: chatSession.GameId,
    GameName: gameName, // resolved earlier in handler if available; else null
    AgentId: chatSession.AgentId,
    AgentName: agentName, // resolved earlier in handler if available; else null
    OccurredAt: DateTime.UtcNow
));

await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

If `gameName` / `agentName` are not already resolved in the handler, fetch them via:
```csharp
var gameName = chatSession.GameId.HasValue
    ? (await _sharedGameRepository.GetNamesByIdsAsync(new[] { chatSession.GameId.Value }, ct))
        .GetValueOrDefault(chatSession.GameId.Value)
    : null;
var agentName = chatSession.AgentId.HasValue
    ? (await _agentRepository.GetByIdAsync(chatSession.AgentId.Value, ct))?.Name
    : null;
```

- [ ] **Step 6: Register in `EventTypeRegistry`**

In `EventTypeRegistry.cs`, add:

```csharp
[typeof(ChatSessionCreatedEvent)] = "chat.session.created", // BE-3 #1590
```

- [ ] **Step 7: Verify `IDomainEventCollector` picks up `ChatSession` events**

The collector iterates EF Change Tracker for entities implementing `IDomainEventSource`. Since `ChatSession` is tracked by EF (`_db.ChatSessions.Add(chatSession)` is called in the handler), its `DomainEvents` will be collected automatically.

If the existing collector uses a different mechanism (e.g. only collects from `AggregateRoot` subclasses), you'll need to verify by reading `apps/api/src/Api/SharedKernel/Domain/Events/IDomainEventCollector.cs` and its implementation. If it filters by `AggregateRoot<>` specifically, change to `IDomainEventSource` (or add `ChatSession` to the collector's tracked set).

- [ ] **Step 8: Run the integration test**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ChatSessionCreatedIntegrationTests" --logger "console;verbosity=minimal"
```
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/ChatSessionCreatedEvent.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/ChatSession.cs apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatSessionCommandHandler.cs apps/api/tests/Api.Tests/Integration/Events/ChatSessionCreatedIntegrationTests.cs
git commit -m "feat(events): #1590 emit chat.session.created from CreateChatSession (BE-3)"
```

---

## Task 4: `KbDocIndexedEvent` end-to-end

**Goal**: Emit `kb.doc.indexed` when `PdfDocument.TransitionTo(Ready)` (B1-B3, AC5).

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Events/KbDocIndexedEvent.cs`
- Modify: `apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs:382` (the `TransitionTo` method)
- Create: `apps/api/tests/Api.Tests/Integration/Events/KbDocIndexedIntegrationTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `apps/api/tests/Api.Tests/Integration/Events/KbDocIndexedIntegrationTests.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Events;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class KbDocIndexedIntegrationTests : IClassFixture<IntegrationWebApplicationFactory<Program>>
{
    private readonly IntegrationWebApplicationFactory<Program> _factory;

    public KbDocIndexedIntegrationTests(IntegrationWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TransitionToReady_LogsKbDocIndexedEvent_NotEveryStateChange()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var (userId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Kb");

        var pdf = PdfDocument.Create(
            sharedGameId: gameId,
            uploadedByUserId: userId,
            fileName: $"test-{Guid.NewGuid():N}.pdf",
            fileSizeBytes: 1024,
            blobStorageUrl: "test://blob"
        );
        dbContext.PdfDocuments.Add(pdf);
        await unitOfWork.SaveChangesAsync();

        // Act: transition through pipeline states up to Ready
        pdf.TransitionTo(PdfProcessingState.Processing);
        pdf.TransitionTo(PdfProcessingState.Indexing);
        pdf.TransitionTo(PdfProcessingState.Ready);
        await unitOfWork.SaveChangesAsync();

        // Assert: only ONE kb.doc.indexed row should exist (NOT 3 — one per transition)
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var rows = await verifyDb.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.EventType == "kb.doc.indexed" && e.AggregateId == pdf.Id)
            .ToListAsync();

        rows.Should().HaveCount(1, "kb.doc.indexed must fire only on Ready transition");
        rows[0].AggregateType.Should().Be("PdfDocument");
        rows[0].UserId.Should().Be(userId);
        rows[0].PayloadJson.Should().Contain("\"FileName\"");
        rows[0].PayloadJson.Should().Contain("\"GameId\"").And.Contain(gameId.ToString());

        // Also verify: PdfStateChangedEvent did NOT create rows (not in registry)
        var stateChangedRows = await verifyDb.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.EventType == "pdf.state.changed")
            .ToListAsync();
        stateChangedRows.Should().BeEmpty("PdfStateChangedEvent is NOT registered (B3)");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~KbDocIndexedIntegrationTests" --logger "console;verbosity=minimal"
```
Expected: FAIL.

- [ ] **Step 3: Create the domain event**

Create `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Events/KbDocIndexedEvent.cs`:

```csharp
using Api.SharedKernel.Domain.Events;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised by <see cref="PdfDocument"/> aggregate when <c>TransitionTo(Ready)</c> succeeds
/// (i.e., the PDF processing pipeline reaches the Ready state — the doc is now searchable).
/// Distinct from <see cref="PdfStateChangedEvent"/> (which fires on EVERY state transition
/// and is NOT registered in <c>EventTypeRegistry</c> — would cause log explosion, see B3 #1590).
/// Registered in <c>EventTypeRegistry</c> with alias <c>"kb.doc.indexed"</c>.
/// </summary>
internal sealed record KbDocIndexedEvent(
    Guid PdfDocumentId,
    Guid UserId,
    string FileName,
    Guid? GameId,
    string? GameName,
    int? PageCount,
    DateTime OccurredAt
) : IDomainEvent, INotification
{
    public Guid EventId { get; } = Guid.NewGuid();
}
```

- [ ] **Step 4: Modify `PdfDocument.TransitionTo` to emit on Ready**

In `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs`, find the `TransitionTo` method (around line 360-390). After the existing `AddDomainEvent(new PdfStateChangedEvent(...))` line (~line 382), add:

```csharp
// BE-3 #1590 (B2): when entering Ready, also raise a dedicated KbDocIndexedEvent
// for the activity log. PdfStateChangedEvent stays for internal handlers (cache, metrics);
// KbDocIndexedEvent is the user-meaningful milestone shown in the activity rail.
if (newState == PdfProcessingState.Ready)
{
    AddDomainEvent(new KbDocIndexedEvent(
        PdfDocumentId: Id,
        UserId: UploadedByUserId,
        FileName: FileName,
        GameId: SharedGameId,
        GameName: null, // payload-time game name resolution happens via consumer; can also be enriched here if SharedGameRepository is injected
        PageCount: PageCount,
        OccurredAt: DateTime.UtcNow
    ));
}
```

Note: `PdfDocument` aggregate doesn't have access to `SharedGameRepository`. Two options:
- (a) Emit `GameName: null` from the aggregate; query-time enrichment (D1 hybrid strategy) supplies the name in the endpoint mapping.
- (b) Resolve `GameName` in a domain event handler post-emit, by enriching the payload before persistence.

**Choose (a)** — keeps aggregate purity. The endpoint mapping (Task 8) resolves `gameName` via `SharedGameRepository.GetNamesByIds` join when the payload's `GameName` is null.

- [ ] **Step 5: Register in `EventTypeRegistry`**

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
// ...
[typeof(KbDocIndexedEvent)] = "kb.doc.indexed", // BE-3 #1590
```

- [ ] **Step 6: Run the integration test**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~KbDocIndexedIntegrationTests" --logger "console;verbosity=minimal"
```
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Events/KbDocIndexedEvent.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs apps/api/tests/Api.Tests/Integration/Events/KbDocIndexedIntegrationTests.cs
git commit -m "feat(events): #1590 emit kb.doc.indexed on PdfDocument Ready (BE-3)"
```

---

## Task 5: `Session` light refactor — implement `IDomainEventSource`

**Goal**: Add `IDomainEventSource` to `Session` (no behavior change, just plumbing for Tasks 6-7).

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/Entities/SessionDomainEventTests.cs`

- [ ] **Step 1: Write the failing unit test**

Create `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/Entities/SessionDomainEventTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.SharedKernel.Domain.Events;
using FluentAssertions;
using MediatR;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionDomainEventTests
{
    private sealed record TestEvent(DateTime OccurredAt) : IDomainEvent, INotification
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    [Fact]
    public void Session_implements_IDomainEventSource()
    {
        var sut = typeof(Session);
        sut.Should().Implement<IDomainEventSource>();
    }

    [Fact]
    public void AddDomainEvent_adds_event_to_DomainEvents()
    {
        var session = CreateValidSession();
        var evt = new TestEvent(DateTime.UtcNow);

        session.AddDomainEvent(evt);

        session.DomainEvents.Should().Contain(evt);
    }

    [Fact]
    public void ClearDomainEvents_empties_list()
    {
        var session = CreateValidSession();
        session.AddDomainEvent(new TestEvent(DateTime.UtcNow));

        session.ClearDomainEvents();

        session.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDomainEvent_null_throws()
    {
        var session = CreateValidSession();
        Action act = () => session.AddDomainEvent(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static Session CreateValidSession()
    {
        // Build a valid Session using its actual factory/constructor.
        // Adapt to match the real Session.Create(...) signature in the codebase.
        return Session.Create(
            gameId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid()
            // ...other required parameters per the actual factory
        );
    }
}
```

Adapt `CreateValidSession()` to match the real `Session` factory/constructor signature (read the actual `Session.cs` to find the entry point — typically `Session.Create(...)` or `new Session(...)`).

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionDomainEventTests" --logger "console;verbosity=minimal"
```
Expected: FAIL — `Session` does not implement `IDomainEventSource`.

- [ ] **Step 3: Add `IDomainEventSource` to `Session`**

In `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs`, modify the class declaration:

```csharp
// BEFORE:
// public sealed class Session
// AFTER:
public sealed class Session : IDomainEventSource
```

Add the implementation (after the existing private fields, before the existing public properties):

```csharp
private readonly List<IDomainEvent> _domainEvents = new();

/// <summary>
/// BE-3 #1590 (C1): light IDomainEventSource implementation so Session can participate
/// in the DomainEventLog pipeline. NOT a full AggregateRoot conversion — Session keeps
/// its existing factory/methods/constructor unchanged.
/// </summary>
public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

public void AddDomainEvent(IDomainEvent domainEvent)
{
    ArgumentNullException.ThrowIfNull(domainEvent);
    _domainEvents.Add(domainEvent);
}

public void ClearDomainEvents() => _domainEvents.Clear();
```

Add the `using Api.SharedKernel.Domain.Events;` import at the top of the file.

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionDomainEventTests" --logger "console;verbosity=minimal"
```
Expected: PASS — 4 tests.

- [ ] **Step 5: Verify `IDomainEventCollector` picks up `Session` events**

Read `apps/api/src/Api/SharedKernel/Domain/Events/IDomainEventCollector.cs` (and its implementation). Confirm it scans EF Change Tracker for entities implementing `IDomainEventSource`. If it only scans `AggregateRoot<>`, modify the scan to use `IDomainEventSource` interface (same fix needed for ChatSession in Task 3).

If you need to fix the collector, do it here (Task 5) — Tasks 6/7 depend on it.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/Entities/SessionDomainEventTests.cs apps/api/src/Api/SharedKernel/Domain/Events/
git commit -m "feat(events): #1590 add IDomainEventSource to Session (light, BE-3)"
```

---

## Task 6: `SessionCreatedEvent` end-to-end + cross-table integration test (AC5.b)

**Goal**: Emit `session.created` from `CreateSessionCommandHandler`. Add the cross-table assertion (C3.3, AC5.b).

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionCreatedEvent.cs`
- Modify: `apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandHandler.cs`
- Create: `apps/api/tests/Api.Tests/Integration/Events/SessionLifecycleIntegrationTests.cs` (includes cross-table assertion)

- [ ] **Step 1: Write the failing integration test with cross-table assertion**

Create `apps/api/tests/Api.Tests/Integration/Events/SessionLifecycleIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Events;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionLifecycleIntegrationTests : IClassFixture<IntegrationWebApplicationFactory<Program>>
{
    private readonly IntegrationWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SessionLifecycleIntegrationTests(IntegrationWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateSession_writes_to_BOTH_session_events_AND_domain_event_logs()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Session");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/sessions",
            sessionToken,
            new { gameId, playerCount = 4 });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: BOTH tables must contain a row
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Find the newly-created session (latest for this user)
        var session = await verifyDb.Sessions
            .AsNoTracking()
            .Where(s => s.CreatedByUserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstAsync();

        // 1. session_events diary row
        var diaryRow = await verifyDb.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == session.Id && e.EventType == "session_created")
            .FirstOrDefaultAsync();
        diaryRow.Should().NotBeNull("session_events must have session_created diary row");

        // 2. domain_event_logs row
        var logRow = await verifyDb.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.EventType == "session.created" && e.AggregateId == session.Id)
            .FirstOrDefaultAsync();
        logRow.Should().NotBeNull("domain_event_logs must have session.created row");
        logRow!.AggregateType.Should().Be("Session");
        logRow.UserId.Should().Be(userId);

        // 3. AC5.b: timestamps differ by < 100ms (same-transaction guarantee)
        var diaryTs = diaryRow!.Timestamp;
        var logTs = logRow.OccurredAt;
        var delta = (diaryTs - logTs).Duration();
        delta.TotalMilliseconds.Should().BeLessThan(100,
            "session_events and domain_event_logs must be written in the same transaction (AC5.b)");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionLifecycleIntegrationTests.CreateSession" --logger "console;verbosity=minimal"
```
Expected: FAIL.

- [ ] **Step 3: Create the domain event**

Create `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionCreatedEvent.cs`:

```csharp
using Api.SharedKernel.Domain.Events;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Raised when <c>CreateSessionCommand</c> succeeds — Session aggregate created and persisted.
/// Registered in <c>EventTypeRegistry</c> with alias <c>"session.created"</c>.
/// Orthogonal to <c>session_events</c> diary table (which has a "session_created" diary row);
/// see #1590 C3 socratic resolution.
/// </summary>
internal sealed record SessionCreatedEvent(
    Guid SessionId,
    Guid UserId,
    Guid GameId,
    string? GameName,
    int PlayerCount,
    DateTime OccurredAt
) : IDomainEvent, INotification
{
    public Guid EventId { get; } = Guid.NewGuid();
}
```

- [ ] **Step 4: Emit from `CreateSessionCommandHandler`**

In `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandHandler.cs`, just before the `await _unitOfWork.SaveChangesAsync(cancellationToken)` at line 239:

```csharp
// BE-3 #1590: emit session.created domain event (orthogonal to session_events diary row)
session.AddDomainEvent(new SessionCreatedEvent(
    SessionId: session.Id,
    UserId: request.UserId,
    GameId: session.GameId,
    GameName: gameName, // resolved earlier in handler if available; else fetch from SharedGameRepository
    PlayerCount: session.PlayerCount, // adapt to actual property name on Session
    OccurredAt: DateTime.UtcNow
));

await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

If `gameName` is not already in scope, resolve it via:
```csharp
var gameNames = await _sharedGameRepository
    .GetNamesByIdsAsync(new[] { session.GameId }, cancellationToken)
    .ConfigureAwait(false);
var gameName = gameNames.GetValueOrDefault(session.GameId);
```

- [ ] **Step 5: Register in `EventTypeRegistry`**

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Events;
// ...
[typeof(SessionCreatedEvent)] = "session.created", // BE-3 #1590
```

- [ ] **Step 6: Run the integration test**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionLifecycleIntegrationTests.CreateSession" --logger "console;verbosity=minimal"
```
Expected: PASS (incl. the <100ms timestamp delta).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionCreatedEvent.cs apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandHandler.cs apps/api/tests/Api.Tests/Integration/Events/SessionLifecycleIntegrationTests.cs
git commit -m "feat(events): #1590 emit session.created + AC5.b cross-table test (BE-3)"
```

---

## Task 7: `SessionFinalizedEvent` end-to-end

**Goal**: Emit `session.finalized` from `FinalizeSessionCommandHandler` (mirror of Task 6).

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionFinalizedEvent.cs`
- Modify: `apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/FinalizeSessionCommandHandler.cs`
- Modify: `apps/api/tests/Api.Tests/Integration/Events/SessionLifecycleIntegrationTests.cs` (add new test)

- [ ] **Step 1: Write the failing test (add to existing class)**

Append this test to `SessionLifecycleIntegrationTests.cs`:

```csharp
[Fact]
public async Task FinalizeSession_writes_session_finalized_to_domain_event_logs()
{
    // Arrange — create a session first
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
    var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Final");

    var createRequest = TestSessionHelper.CreateAuthenticatedRequest(
        HttpMethod.Post,
        "/api/v1/sessions",
        sessionToken,
        new { gameId, playerCount = 3 });
    var createResp = await _client.SendAsync(createRequest);
    createResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

    using var lookupScope = _factory.Services.CreateScope();
    var lookupDb = lookupScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var session = await lookupDb.Sessions.AsNoTracking()
        .Where(s => s.CreatedByUserId == userId)
        .OrderByDescending(s => s.CreatedAt)
        .FirstAsync();

    // Act — finalize
    var finalizeRequest = TestSessionHelper.CreateAuthenticatedRequest(
        HttpMethod.Post,
        $"/api/v1/sessions/{session.Id}/finalize",
        sessionToken,
        new { /* finalize payload matching FinalizeSessionCommand */ });
    var finalizeResp = await _client.SendAsync(finalizeRequest);

    // Assert
    finalizeResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

    using var verifyScope = _factory.Services.CreateScope();
    var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var logRow = await verifyDb.DomainEventLogs
        .AsNoTracking()
        .Where(e => e.EventType == "session.finalized" && e.AggregateId == session.Id)
        .FirstOrDefaultAsync();
    logRow.Should().NotBeNull();
    logRow!.AggregateType.Should().Be("Session");
    logRow.UserId.Should().Be(userId);
    logRow.PayloadJson.Should().Contain("\"PlayerCount\"").And.Contain("\"DurationMinutes\"");
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionLifecycleIntegrationTests.FinalizeSession" --logger "console;verbosity=minimal"
```
Expected: FAIL.

- [ ] **Step 3: Create the domain event**

Create `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionFinalizedEvent.cs`:

```csharp
using Api.SharedKernel.Domain.Events;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Raised when <c>FinalizeSessionCommand</c> succeeds — Session aggregate marked as finalized.
/// Registered in <c>EventTypeRegistry</c> with alias <c>"session.finalized"</c>.
/// </summary>
internal sealed record SessionFinalizedEvent(
    Guid SessionId,
    Guid UserId,
    Guid GameId,
    string? GameName,
    int PlayerCount,
    string? WinnerName,
    int DurationMinutes,
    DateTime OccurredAt
) : IDomainEvent, INotification
{
    public Guid EventId { get; } = Guid.NewGuid();
}
```

- [ ] **Step 4: Emit from `FinalizeSessionCommandHandler`**

In `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/FinalizeSessionCommandHandler.cs`, just before `await _unitOfWork.SaveChangesAsync(cancellationToken)` at line 136:

```csharp
session.AddDomainEvent(new SessionFinalizedEvent(
    SessionId: session.Id,
    UserId: request.UserId,
    GameId: session.GameId,
    GameName: gameName,
    PlayerCount: session.PlayerCount,
    WinnerName: winnerName, // resolve from request.WinnerId if applicable
    DurationMinutes: (int)(session.EndedAt!.Value - session.StartedAt).TotalMinutes,
    OccurredAt: DateTime.UtcNow
));

await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

Adapt property names (`StartedAt`, `EndedAt`, `WinnerId`, `PlayerCount`) to the actual `Session` aggregate's API.

- [ ] **Step 5: Register in `EventTypeRegistry`**

```csharp
[typeof(SessionFinalizedEvent)] = "session.finalized", // BE-3 #1590
```

- [ ] **Step 6: Run the integration test**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionLifecycleIntegrationTests" --logger "console;verbosity=minimal"
```
Expected: PASS (both tests).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionFinalizedEvent.cs apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/FinalizeSessionCommandHandler.cs apps/api/tests/Api.Tests/Integration/Events/SessionLifecycleIntegrationTests.cs
git commit -m "feat(events): #1590 emit session.finalized from Finalize (BE-3)"
```

---

## Task 8: `GET /api/v1/activity` endpoint end-to-end

**Goal**: Expose generalized cross-entity activity feed (AC2, AC3).

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ActivityItemDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetActivityFeedQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetActivityFeedQueryValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetActivityFeedQueryHandler.cs`
- Create: `apps/api/src/Api/Routing/ActivityEndpoints.cs`
- Modify: `apps/api/src/Api/Routing/RouteRegistry.cs` (wire `ActivityEndpoints.Map(...)`)
- Create: `apps/api/tests/Api.Tests/Integration/Events/ActivityFeedEndpointIntegrationTests.cs`

- [ ] **Step 1: Write the failing endpoint integration test**

Create `apps/api/tests/Api.Tests/Integration/Events/ActivityFeedEndpointIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Events;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed class ActivityFeedEndpointIntegrationTests : IClassFixture<IntegrationWebApplicationFactory<Program>>
{
    private readonly IntegrationWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ActivityFeedEndpointIntegrationTests(IntegrationWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetActivity_returns_cross_entity_items_for_user()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Activity");

        // Create an agent → emits agent.created
        var agentRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/agents/quick-create", sessionToken, new { gameId });
        await _client.SendAsync(agentRequest);

        // Act
        var feedRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/activity?limit=20", sessionToken);
        var response = await _client.SendAsync(feedRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ActivityFeedResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().Contain(i => i.EventType == "agent.created" && i.UserId == userId);
        var agentItem = body.Items.First(i => i.EventType == "agent.created");
        agentItem.EntityType.Should().Be("Agent");
        agentItem.EntityId.Should().NotBe(Guid.Empty);
        agentItem.PayloadVersion.Should().Be(1);
        agentItem.Title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetActivity_unauthenticated_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/activity?limit=20");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetActivity_invalid_limit_returns_422()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var req = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/activity?limit=999", sessionToken);
        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetActivity_only_returns_caller_events_not_other_users()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (alice, aliceToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (bob, bobToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Multi");

        // Bob creates an agent
        await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/agents/quick-create", bobToken, new { gameId }));

        // Alice queries her feed
        var req = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/activity?limit=20", aliceToken);
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ActivityFeedResponse>();
        body!.Items.Should().NotContain(i => i.UserId == bob, "Bob's events MUST NOT appear in Alice's feed");
    }
}

internal sealed record ActivityFeedResponse(List<ActivityItem> Items, int Count);

internal sealed record ActivityItem(
    Guid Id,
    Guid EventId,
    string EventType,
    Guid UserId,
    string EntityType,
    Guid EntityId,
    string? Title,
    DateTime Timestamp,
    DateTime LoggedAt,
    int PayloadVersion);
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ActivityFeedEndpointIntegrationTests" --logger "console;verbosity=minimal"
```
Expected: FAIL — endpoint not registered (404).

- [ ] **Step 3: Create the DTO**

Create `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ActivityItemDto.cs`:

```csharp
namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Generalized activity feed item returned by GET /api/v1/activity.
/// BE-3 #1590 — cross-entity DTO mapping AggregateType/Id (DB) to entityType/entityId (FE).
/// </summary>
internal sealed record ActivityItemDto(
    Guid Id,
    Guid EventId,
    string EventType,
    Guid UserId,
    string EntityType,    // = DB AggregateType
    Guid EntityId,        // = DB AggregateId
    string? Title,        // entity-friendly display name, derived from PayloadJson where available
    DateTime Timestamp,   // = DB OccurredAt
    DateTime LoggedAt,    // = DB LoggedAt
    int PayloadVersion
);
```

- [ ] **Step 4: Create the query**

Create `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetActivityFeedQuery.cs`:

```csharp
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal sealed record GetActivityFeedQuery(
    Guid UserId,
    int Limit = 20,
    DateTime? Since = null
) : IQuery<GetActivityFeedResult>;

internal sealed record GetActivityFeedResult(List<ActivityItemDto> Items, int Count);
```

- [ ] **Step 5: Create the validator**

Create `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetActivityFeedQueryValidator.cs`:

```csharp
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal sealed class GetActivityFeedQueryValidator : AbstractValidator<GetActivityFeedQuery>
{
    public GetActivityFeedQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}
```

- [ ] **Step 6: Create the handler**

Create `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetActivityFeedQueryHandler.cs`:

```csharp
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal sealed class GetActivityFeedQueryHandler : IQueryHandler<GetActivityFeedQuery, GetActivityFeedResult>
{
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(90);

    private readonly MeepleAiDbContext _db;

    public GetActivityFeedQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<GetActivityFeedResult> Handle(GetActivityFeedQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var retentionCutoff = DateTime.UtcNow - RetentionWindow;
        var query = _db.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.UserId == request.UserId)
            .Where(e => e.LoggedAt >= retentionCutoff);

        if (request.Since.HasValue)
        {
            query = query.Where(e => e.LoggedAt >= request.Since.Value);
        }

        var rows = await query
            .OrderByDescending(e => e.LoggedAt)
            .Take(request.Limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(r => new ActivityItemDto(
            Id: r.Id,
            EventId: r.EventId,
            EventType: r.EventType,
            UserId: r.UserId ?? Guid.Empty,
            EntityType: r.AggregateType ?? string.Empty,
            EntityId: r.AggregateId ?? Guid.Empty,
            Title: ExtractTitleFromPayload(r.PayloadJson, r.AggregateType),
            Timestamp: r.OccurredAt,
            LoggedAt: r.LoggedAt,
            PayloadVersion: r.PayloadVersion
        )).ToList();

        return new GetActivityFeedResult(items, items.Count);
    }

    private static string? ExtractTitleFromPayload(string payloadJson, string? aggregateType)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            // Title priority by aggregate type:
            //   Agent → AgentName | ChatSession → AgentName/GameName | PdfDocument → FileName | Session → GameName
            //   library.* (legacy via GameRemovedFromLibraryEvent etc.) → null (no name in payload)
            return aggregateType switch
            {
                "Agent" => GetString(doc, "AgentName"),
                "ChatSession" => GetString(doc, "AgentName") ?? GetString(doc, "GameName"),
                "PdfDocument" => GetString(doc, "FileName"),
                "Session" => GetString(doc, "GameName"),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonDocument doc, string propertyName)
    {
        if (!doc.RootElement.TryGetProperty(propertyName, out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.String) return null;
        var value = prop.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
```

- [ ] **Step 7: Create the endpoint**

Create `apps/api/src/Api/Routing/ActivityEndpoints.cs`:

```csharp
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure.Sessions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

internal static class ActivityEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1");
        group.MapGet("/activity", async (
            [FromQuery] int? limit,
            [FromQuery] DateTime? since,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            var userId = session!.Principal!.Subject!.Id;
            var query = new GetActivityFeedQuery(
                UserId: userId,
                Limit: limit ?? 20,
                Since: since
            );
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                success = true,
                items = result.Items,
                count = result.Count
            });
        })
        .RequireAuthenticatedUser()
        .Produces(200)
        .Produces(401)
        .Produces(422)
        .WithTags("Activity")
        .WithSummary("Cross-entity activity feed (BE-3 #1590)")
        .WithDescription("Returns the caller's recent durable domain events from domain_event_logs, " +
                         "ordered by LoggedAt DESC, capped at 100. 90-day retention. " +
                         "Forward-only — events before BE-3 deploy do not appear.")
        .WithOpenApi();
    }
}
```

- [ ] **Step 8: Wire into `RouteRegistry`**

In `apps/api/src/Api/Routing/RouteRegistry.cs`, add (near other endpoint group registrations):

```csharp
ActivityEndpoints.Map(routes); // BE-3 #1590
```

- [ ] **Step 9: Run the integration tests**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ActivityFeedEndpointIntegrationTests" --logger "console;verbosity=minimal"
```
Expected: PASS — all 4 tests (cross-entity, 401, 422, isolation).

- [ ] **Step 10: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/ apps/api/src/Api/Routing/ActivityEndpoints.cs apps/api/src/Api/Routing/RouteRegistry.cs apps/api/tests/Api.Tests/Integration/Events/ActivityFeedEndpointIntegrationTests.cs
git commit -m "feat(activity): #1590 add GET /api/v1/activity cross-entity feed (BE-3)"
```

---

## Task 9: Legacy `/library/activity` — replace placeholder `"Game"` with bulk join

**Goal**: D1 + D2 + AC4 — `GetLibraryActivityQueryHandler` returns real `gameTitle` instead of placeholder; handles soft-deleted SharedGame.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQueryHandler.cs:162-182`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQueryHandlerTests.cs` (update assertions)

- [ ] **Step 1: Update the existing handler test for real gameTitle**

In `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQueryHandlerTests.cs`, find the test that seeds a `library.entry.removed` row and asserts on `GameTitle`. Change the assertion:

```csharp
// BEFORE: result[0].GameTitle.Should().Be("Game");
// AFTER:
result[0].GameTitle.Should().Be("Catan"); // BE-3 #1590: handler now joins SharedGameRepository
```

You'll need to also seed a `SharedGame` with the matching `GameId` and `Title = "Catan"` so the join can resolve it.

If the test class has a shared `IBlobStorageService`/`ISharedGameRepository` mock, add the `GetNamesByIdsAsync` mock setup:

```csharp
_mockSharedGameRepository
    .Setup(r => r.GetNamesByIdsAsync(
        It.IsAny<IReadOnlyCollection<Guid>>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(new Dictionary<Guid, string> { { GameId, "Catan" } });
```

Add a new test for soft-deleted SharedGame (D2):

```csharp
[Fact]
public async Task Handle_softDeletedGame_returnsI18nKey()
{
    // Arrange: seed library.entry.removed row but GameNames dictionary does not contain the id (soft-deleted)
    _mockSharedGameRepository
        .Setup(r => r.GetNamesByIdsAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(GameId)),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Dictionary<Guid, string>()); // empty = soft-deleted

    // ... seed log row as before ...

    var result = await _sut.Handle(new GetLibraryActivityQuery(UserId, 20), CancellationToken.None);

    result[0].GameTitle.Should().Be("library.activity.deletedGame");
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetLibraryActivityQueryHandlerTests" --logger "console;verbosity=minimal"
```
Expected: FAIL — handler still returns `"Game"`.

- [ ] **Step 3: Refactor the handler**

In `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQueryHandler.cs`, locate the section that currently produces `gameTitle = "Game"` placeholder (around lines 162-182). Replace the entire mapping section with:

```csharp
// BE-3 #1590 (D1): bulk-join SharedGameRepository.GetNamesByIds for real gameTitle.
// Replaces the legacy placeholder "Game" at line 180 (// Future enhancement).
// Soft-deleted games (not in the dictionary) → return i18n key "library.activity.deletedGame".
var gameIds = mergedItems
    .Where(i => i.GameId != Guid.Empty)
    .Select(i => i.GameId)
    .Distinct()
    .ToList();

var gameNames = gameIds.Count > 0
    ? await _sharedGameRepository.GetNamesByIdsAsync(gameIds, cancellationToken).ConfigureAwait(false)
    : new Dictionary<Guid, string>();

return mergedItems
    .Select(i => new LibraryActivityItemDto(
        Id: i.Id,
        Type: i.Type,
        Timestamp: i.Timestamp,
        GameId: i.GameId,
        GameTitle: gameNames.TryGetValue(i.GameId, out var title)
            ? title
            : "library.activity.deletedGame", // i18n key — FE renders "Gioco rimosso"
        Message: i.Message
    ))
    .ToList()
    .AsReadOnly();
```

The handler must now have `_sharedGameRepository` injected — add it to the constructor if not present. Mirror the BE-1 #1588 pattern (`ListUserKbDocsQueryHandler` injection).

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetLibraryActivityQueryHandlerTests" --logger "console;verbosity=minimal"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQueryHandler.cs apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Queries/GetLibraryActivityQueryHandlerTests.cs
git commit -m "fix(activity): #1590 resolve real gameTitle via bulk join (BE-3 D1/D2)"
```

---

## Task 10: Observability — counters G1 + G2

**Goal**: AC7 — two Prometheus counters wired into `MeepleAiDbContext.SaveChangesAsync`.

**Files:**
- Create: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.DomainEventLog.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs:449-470`
- Create: `apps/api/tests/Api.Tests/Observability/Metrics/DomainEventLogMetricsTests.cs`

- [ ] **Step 1: Write the failing metrics test**

Create `apps/api/tests/Api.Tests/Observability/Metrics/DomainEventLogMetricsTests.cs`:

```csharp
using System.Diagnostics.Metrics;
using Api.Infrastructure;
using Api.Observability;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Observability.Metrics;

[Trait("Category", TestCategories.Integration)]
public sealed class DomainEventLogMetricsTests : IClassFixture<IntegrationWebApplicationFactory<Program>>
{
    private readonly IntegrationWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DomainEventLogMetricsTests(IntegrationWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DomainEventInserted_counter_increments_on_event_emission()
    {
        // Arrange: subscribe to the Meter via MeterListener
        var measurements = new List<(long Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == "MeepleAI.Api" &&
                    instrument.Name == "meepleai.domain_event_log.inserted.total")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
            measurements.Add((value, tags.ToArray())));
        listener.Start();

        // Act
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Metric");

        var req = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/agents/quick-create", sessionToken, new { gameId });
        await _client.SendAsync(req);

        // Assert: at least one increment for agent.created
        measurements
            .Should().Contain(m =>
                m.Tags.Any(t => t.Key == "event_type" && (string?)t.Value == "agent.created"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DomainEventLogMetricsTests" --logger "console;verbosity=minimal"
```
Expected: FAIL.

- [ ] **Step 3: Create the metrics partial**

Create `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.DomainEventLog.cs`:

```csharp
using System.Diagnostics.Metrics;

namespace Api.Observability;

/// <summary>
/// Domain event log persistence metrics (BE-3 #1590 AC7).
/// G1: inserted counter — every successful log row insert.
/// G2: dispatch failures counter — when an INotificationHandler throws after the log row
///     is durable (the row is NOT rolled back; we only observe the silent handler crash).
/// </summary>
internal static partial class MeepleAiMetrics
{
    public static readonly Counter<long> DomainEventsInserted = Meter.CreateCounter<long>(
        name: "meepleai.domain_event_log.inserted.total",
        unit: "events",
        description: "Total domain events persisted to domain_event_logs by event type (#1590 G1)");

    public static readonly Counter<long> DomainEventDispatchFailures = Meter.CreateCounter<long>(
        name: "meepleai.domain_event_log.dispatch_failures.total",
        unit: "failures",
        description: "Total handler dispatch failures after domain event was persisted (#1590 G2)");
}
```

- [ ] **Step 4: Wire into `MeepleAiDbContext.SaveChangesAsync`**

In `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`, find lines 449-470 (the loop that adds log entities and the try/catch around `_mediator.Publish`).

After `DomainEventLogs.Add(logEntity)` (line ~451), add:

```csharp
// BE-3 #1590 G1: counter increment per inserted row
MeepleAiMetrics.DomainEventsInserted.Add(1,
    new KeyValuePair<string, object?>("event_type", logEntity.EventType));
```

In the catch block around `_mediator.Publish` (line ~468), add:

```csharp
catch (Exception ex)
{
    // existing ERROR log...
    _logger.LogError(ex, ...);

    // BE-3 #1590 G2: counter increment per handler dispatch failure
    MeepleAiMetrics.DomainEventDispatchFailures.Add(1,
        new KeyValuePair<string, object?>("event_type", domainEvent.GetType().Name), // or the alias if available
        new KeyValuePair<string, object?>("handler_name", "unknown")); // refine if handler identity is accessible
}
```

If the dispatch loop iterates per handler (Mediatr publishes to all handlers), and you can capture the handler type name from the exception/context, use that for the `handler_name` tag.

- [ ] **Step 5: Run tests to verify they pass**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DomainEventLogMetricsTests" --logger "console;verbosity=minimal"
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.DomainEventLog.cs apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs apps/api/tests/Api.Tests/Observability/Metrics/DomainEventLogMetricsTests.cs
git commit -m "feat(observability): #1590 add domain event log counters G1+G2 (BE-3)"
```

---

## Task 11: Architectural test (AC8) + final XML doc sweep (AC9 verify)

**Goal**: C3.1 + C3.4 — protect the orthogonal-tables boundary; ensure all XML docs are in place.

**Files:**
- Create: `apps/api/tests/Api.Tests/Architecture/EventLogBoundaryTests.cs`
- Modify (verify only — Task 1 should have done this): `apps/api/src/Api/Infrastructure/Entities/DomainEventLog/DomainEventLog.cs`

- [ ] **Step 1: Write the architectural test**

Create `apps/api/tests/Api.Tests/Architecture/EventLogBoundaryTests.cs`:

```csharp
using System.Reflection;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Infrastructure.Repositories;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

[Trait("Category", TestCategories.Architecture)]
public sealed class EventLogBoundaryTests
{
    /// <summary>
    /// AC8 (#1590 C3.1): the session-diary handler must NOT depend on the durable
    /// domain event log infrastructure. The two tables are orthogonal by design.
    /// </summary>
    [Fact]
    public void GetSessionDiaryQueryHandler_does_NOT_depend_on_IDomainEventLogRepository()
    {
        var ctorParams = GetCtorParameterTypes<GetSessionDiaryQueryHandler>();
        ctorParams.Should().NotContain(t => t.Name == "IDomainEventLogRepository",
            "GetSessionDiaryQueryHandler must not read from domain_event_logs (BE-3 #1590 AC8)");
    }

    /// <summary>
    /// AC8 (#1590 C3.1): the library-activity handler must NOT depend on the
    /// session-diary infrastructure. Drift prevention.
    /// </summary>
    [Fact]
    public void GetLibraryActivityQueryHandler_does_NOT_depend_on_ISessionEventRepository()
    {
        var ctorParams = GetCtorParameterTypes<GetLibraryActivityQueryHandler>();
        ctorParams.Should().NotContain(t => t.Name == "ISessionEventRepository",
            "GetLibraryActivityQueryHandler must not read from session_events (BE-3 #1590 AC8)");
    }

    private static Type[] GetCtorParameterTypes<T>()
    {
        var ctor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance).First();
        return ctor.GetParameters().Select(p => p.ParameterType).ToArray();
    }
}
```

- [ ] **Step 2: Run test to verify it passes (both handlers should already be clean)**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~EventLogBoundaryTests" --logger "console;verbosity=minimal"
```
Expected: PASS — both handlers should already be clean (Task 9 modified `GetLibraryActivityQueryHandler` to inject `_sharedGameRepository`, NOT `ISessionEventRepository`).

If the test FAILS, it means one of the handlers accidentally took a dependency on the other side — fix the handler (the test enforces the architectural rule).

- [ ] **Step 3: Verify XML docs are in place (AC9 sanity check)**

Confirm `DomainEventLog.cs` (modified in Task 1) has the XML docs for `AggregateType` and `AggregateId` that declare the logical-FK mapping. If they're not there, add them now (per Task 1 Step 2).

- [ ] **Step 4: Final full-suite test run**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Events|FullyQualifiedName~EventTypeRegistry|FullyQualifiedName~DomainEventLog|FullyQualifiedName~Activity" --logger "console;verbosity=minimal"
```
Expected: ALL PASS. This is the BE-3 final test gate.

- [ ] **Step 5: Verify `EventTypeRegistryTests` still passes**

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~EventTypeRegistryTests" --logger "console;verbosity=minimal"
```
Expected: PASS — the existing test that asserts all registry entries resolve to real `IDomainEvent` types will validate the 5 new entries automatically.

- [ ] **Step 6: Commit**

```bash
git add apps/api/tests/Api.Tests/Architecture/EventLogBoundaryTests.cs
git commit -m "test(events): #1590 add architectural boundary tests AC8 (BE-3)"
```

---

## Final acceptance check

After all 11 tasks complete, run:

```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "Category=Integration|Category=Architecture|FullyQualifiedName~EventTypeRegistry|FullyQualifiedName~DomainEventLog" --logger "console;verbosity=minimal"
```

Expected: ALL PASS.

| AC | Verified by |
|---|---|
| AC1 (5 events land + idempotence + PayloadVersion) | Tasks 2/3/4/6/7 integration tests |
| AC2 (GET /api/v1/activity generalized) | Task 8 integration tests |
| AC3 (forward-only, no backfill) | No-op in BE-3 (no backfill code written); cold-start UX is FE in #1593 |
| AC4 (GET /library/activity backward-compat + real gameTitle) | Task 9 unit + Task 8 multi-entity feed integration |
| AC5 (≥2 BC integration tests + existing 2 library) | Tasks 2 (Agent) + 3 (Chat) + 4 (KbDoc) + 6 (Session) + EXISTING tests for library.* not removed |
| AC5.b (cross-table SessionTracking <100ms) | Task 6 integration test |
| AC6 (index exists) | Verified in BE exploration (`ix_domain_event_logs_user_loggedat`) — Task 1 confirms by reading EF config |
| AC7 (counters G1 + G2) | Task 10 |
| AC8 (architectural test) | Task 11 |
| AC9 (XML doc on AggregateType/Id) | Task 1 (initial doc) + Task 11 (verification) |

---

## Out of scope (follow-ups / cross-cuts)

- **E1 FE rename `useActivityFeed` → `useDashboardActivityFeed`**: tracked in #1593 pre-req. NOT BE-3.
- **F1 unify `added` double-path**: keep two sources. Cleanup follow-up if ever needed.
- **`PayloadVersion` v2 / breaking payload migrations**: when any event needs a breaking payload change, increment `PayloadVersion` for new emissions; the handler can branch on version (`if payloadVersion >= 2 then DTO_v2 else DTO_v1`). Defer until first real migration is needed.
- **Backfill SessionTracking historical sessions into `domain_event_logs`**: explicitly rejected (AC3 + cold-start UX via FE in #1593).
- **`ActivityTimelineService` runtime fan-out**: explicitly rejected in favor of durable log (issue body §"Endpoint generalization").
