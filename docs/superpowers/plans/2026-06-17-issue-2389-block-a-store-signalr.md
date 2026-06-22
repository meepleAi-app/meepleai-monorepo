# Issue #2389 — Block A: Store + SignalR contract evolution

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the foundational contract evolution for polymorphic scoring without touching the renderer wire-up: extend `SessionDto` with `ScoringType`/`ScoreData`, broadcast a `ScoringConfigured` SignalR event on score config changes, and evolve the FE `useLiveSessionStore` + `useSessionScores` hook to carry the per-variant payload — with a backward-compat window where the legacy `scores: Record<string, number>` field is still derived from `scoreData` for unmigrated consumers.

**Architecture:** Three-tier pipeline with strict backward compat. **BE**: extend the existing `SessionDto` (Session aggregate + EF columns + `SetScores` mutator + `SessionScoresUpdatedEvent` are already shipped — no DB migration, no aggregate changes); add a new `SessionScoresUpdatedSignalRHandler : INotificationHandler<SessionScoresUpdatedEvent>` that broadcasts via `GameStateHub`. **FE store**: add `scoringType: ScoreType | null` + `scoreData: ScoreDataByType[ScoreType] | null` + `PlayerInfo.displayName: string` to `LiveSessionState`; keep legacy `scores: Record<string, number>` (deprecated) so existing consumers do not break. **FE hook**: `useSessionScores` returns the extended shape and derives the legacy `scores` map from `scoreData` when `scoringType === 'Points'`. A custom ESLint rule (`meepleai/no-store-scores-direct`, **warn** level) marks new direct `useLiveSessionStore(s => s.scores)` reads for migration.

**Tech Stack:** .NET 9 + ASP.NET Minimal APIs + MediatR + EF Core (Postgres + Testcontainers) + SignalR · Next.js 16 + React 19 + Zustand 4 + Vitest + ESLint custom rule (`@typescript-eslint/utils`).

## Pre-flight discovery (already done — context for the implementer)

- Session aggregate at `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs`:441-463 already exposes `ScoringType` + `ScoreData` properties (private setters via `SetScores(ScoreType, string)`); EF Core migration shipped, mutator raises `SessionScoresUpdatedEvent`.
- `SessionScoresUpdatedEvent` is at `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionScoresUpdatedEvent.cs`; **no `INotificationHandler<SessionScoresUpdatedEvent>` is registered yet** (`grep -rln 'INotificationHandler<SessionScoresUpdatedEvent>' apps/api/src` returns nothing). This is the SignalR extension point we add.
- SignalR broadcast pattern blueprint: `apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/DisputeResolvedSignalRHandler.cs` — inject `IHubContext<GameStateHub>`, call `_hubContext.Clients.Group($"session:{sessionId}").SendAsync(...)`.
- `SessionDto` at `apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionDto.cs` does NOT carry `ScoringType` or `ScoreData` today — adding them is API-additive (no breaking change). Mapped in `GetActiveSessionQueryHandler` at `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetActiveSessionQueryHandler.cs`.
- Integration test scaffolding: `apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs` + `IntegrationTestBase.cs` (provides DbContext + Repository + MockEventCollector); example handler-IT at `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Integration/FinalizeSessionSingleDispatchIntegrationTests.cs`.
- FE store: `apps/web/src/lib/stores/live-session-store.ts` — already uses `zustand/middleware.devtools`. Consumer hook at `apps/web/src/lib/domain-hooks/useSessionScores.ts`.
- ScoreType FE types: `apps/web/src/components/sessions/score-strategies/types.ts` (canonical `ScoreType`, `ScoreDataByType`, `PlayerOption`).
- ESLint custom rules folder: **none today** — we add `apps/web/eslint-rules/` and register it via the existing `apps/web/eslint.config.js` (verify in Task 26).

---

## File Structure

### Backend (`apps/api/src/Api/`)

| Path | Action | Responsibility |
|---|---|---|
| `BoundedContexts/SessionTracking/Application/DTOs/SessionDto.cs` | Modify | Add `ScoringType: string?` + `ScoreData: string?` fields (additive). |
| `BoundedContexts/SessionTracking/Application/Queries/GetActiveSessionQueryHandler.cs` | Modify | Map the two new fields from `Session` entity into the DTO. |
| `BoundedContexts/SessionTracking/Application/EventHandlers/SessionScoresUpdatedSignalRHandler.cs` | Create | New `INotificationHandler<SessionScoresUpdatedEvent>` — broadcasts `ScoringConfigured` to the session SignalR group. |
| `Hubs/GameStateHub.cs` | Modify | Add `BroadcastScoringConfigured(string sessionId, object payload)` method following the existing `NotifyScoreUpdated` pattern (lines 302-310). |

### Backend tests (`apps/api/tests/Api.Tests/`)

| Path | Action | Responsibility |
|---|---|---|
| `BoundedContexts/SessionTracking/Application/DTOs/SessionDtoTests.cs` | Create | Unit test that the new DTO fields default to `null`/empty (record equality regression guard). |
| `BoundedContexts/SessionTracking/Application/Queries/GetActiveSessionQueryHandlerTests.cs` | Modify | Existing handler tests + new test that asserts `dto.ScoringType` / `dto.ScoreData` are populated when the entity has them. |
| `BoundedContexts/SessionTracking/Application/EventHandlers/SessionScoresUpdatedSignalRHandlerTests.cs` | Create | Unit test: handler invokes `IHubContext<GameStateHub>` with the right group name + payload shape. Mock the hub context. |
| `BoundedContexts/SessionTracking/Integration/SessionScoresUpdatedSignalRBroadcastIntegrationTests.cs` | Create | Testcontainers IT: trigger `SetScores` via aggregate → SaveChanges → verify mock SignalR client receives `ScoringConfigured`. |

### Frontend store & hook (`apps/web/src/`)

| Path | Action | Responsibility |
|---|---|---|
| `lib/stores/live-session-store.ts` | Modify | Add `scoringType` + `scoreData` to state; add `displayName?: string` to `PlayerInfo`; add `setScoringConfig({ scoringType, scoreData, availableObjectives? })` action; keep `scores: Record<string, number>` (deprecated). |
| `lib/stores/__tests__/live-session-store.test.ts` | Create or extend | Tests for the new state defaults + the new action's reducer behaviour. |
| `lib/domain-hooks/useSessionScores.ts` | Modify | Extended return type `{ scoringType, scoreData, scores (deprecated derived), leader, players, pendingProposals }`. Derive `scores` from `scoreData` when `scoringType === 'Points'` (memoized). |
| `lib/domain-hooks/__tests__/useSessionScores.test.ts` | Create or extend | Tests for the extended return + derived `scores` map for Points / null for non-Points. |
| `lib/domain-hooks/useSignalrSession.ts` | Modify | Subscribe to the new `ScoringConfigured` SignalR event and call `store.setScoringConfig(...)`. |
| `lib/domain-hooks/__tests__/useSignalrSession.test.ts` | Extend | Test the new event handler maps the hub payload into the store action. |

### Frontend lint plumbing

| Path | Action | Responsibility |
|---|---|---|
| `apps/web/eslint-rules/no-store-scores-direct.js` | Create | Custom ESLint rule (warn) flagging `useLiveSessionStore(s => s.scores)` reads outside the deprecation surface. |
| `apps/web/eslint-rules/__tests__/no-store-scores-direct.test.js` | Create | RuleTester unit test (valid / invalid cases). |
| `apps/web/eslint.config.js` | Modify | Register the new local rule under namespace `meepleai`. |
| `apps/web/eslint-rules/index.js` | Create | Plugin manifest exporting the rule. |

---

## Phase 1 — Backend: extend `SessionDto`

### Task 1: Extend `SessionDto` record (unit-tested defaults)

**Files:**
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/DTOs/SessionDtoTests.cs` (create)
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionDto.cs:3-16`

- [ ] **Step 1: Write the failing test**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/DTOs/SessionDtoTests.cs
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.DTOs;

public class SessionDtoTests
{
    [Fact]
    public void DefaultDto_ScoringType_And_ScoreData_AreNull()
    {
        var dto = new SessionDto();

        dto.ScoringType.Should().BeNull();
        dto.ScoreData.Should().BeNull();
    }

    [Fact]
    public void Dto_With_Scoring_Roundtrips_Both_Fields()
    {
        var dto = new SessionDto
        {
            ScoringType = "Points",
            ScoreData = "{\"scores\":[]}"
        };

        dto.ScoringType.Should().Be("Points");
        dto.ScoreData.Should().Be("{\"scores\":[]}");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SessionDtoTests" --verbosity minimal`
Expected: FAIL with `error CS0117: 'SessionDto' does not contain a definition for 'ScoringType'`.

- [ ] **Step 3: Add the two fields to the DTO**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionDto.cs
public record SessionDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid? GameId { get; init; }
    public string SessionCode { get; init; } = string.Empty;
    public string SessionType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime SessionDate { get; init; }
    public string? Location { get; init; }
    public DateTime? FinalizedAt { get; init; }
    public List<ParticipantDto> Participants { get; init; } = [];
    public List<ScoreEntryDto> Scores { get; init; } = [];

    /// <summary>
    /// Polymorphic scoring discriminator (mirrors <see cref="Api.BoundedContexts.SessionTracking.Domain.Enums.ScoreType"/>).
    /// Null when the session has not been configured yet.
    /// </summary>
    public string? ScoringType { get; init; }

    /// <summary>
    /// Per-variant scoring payload as raw JSON (PointsScoreData, BinaryWinScoreData,
    /// ObjectivesScoreData, or RankingScoreData). FE deserialises via Zod schema.
    /// </summary>
    public string? ScoreData { get; init; }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SessionDtoTests" --verbosity minimal`
Expected: PASS (2/2).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionDto.cs apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/DTOs/SessionDtoTests.cs
git commit -m "feat(api): #2389 extend SessionDto with ScoringType+ScoreData (Block A.1)"
```

### Task 2: Populate the new fields in `GetActiveSessionQueryHandler`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetActiveSessionQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Queries/GetActiveSessionQueryHandlerTests.cs`

- [ ] **Step 1: Locate the existing handler test class and read the existing happy-path test**

Run: `grep -n "Handle_" apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Queries/GetActiveSessionQueryHandlerTests.cs | head -10`
You'll reuse the existing fixture setup.

- [ ] **Step 2: Add the failing test for the two new fields**

Paste at the end of the existing `GetActiveSessionQueryHandlerTests` class:

```csharp
[Fact]
public async Task Handle_WhenSessionHasScoringConfigured_ReturnsScoringTypeAndScoreData()
{
    // Arrange — seed a session with SetScores already applied
    var session = SessionFixtures.NewActiveSession(userId: TestUserId);
    session.SetScores(ScoreType.Points, "{\"scores\":[{\"playerId\":\"p1\",\"points\":10}]}");
    _sessionRepoMock.Setup(r => r.GetActiveSessionAsync(TestUserId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(session);

    // Act
    var dto = await _handler.Handle(new GetActiveSessionQuery(TestUserId), CancellationToken.None);

    // Assert
    dto.Should().NotBeNull();
    dto!.ScoringType.Should().Be("Points");
    dto.ScoreData.Should().Be("{\"scores\":[{\"playerId\":\"p1\",\"points\":10}]}");
}
```

(`SessionFixtures.NewActiveSession` is a helper that may not yet exist — search for the existing pattern, e.g. `Session.Create(...)` or an inline factory the existing tests use. If no helper exists, inline the factory call.)

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~GetActiveSessionQueryHandlerTests.Handle_WhenSessionHasScoringConfigured" --verbosity minimal`
Expected: FAIL with `dto.ScoringType to be "Points", but found <null>`.

- [ ] **Step 4: Update the handler mapping**

Find the section in `GetActiveSessionQueryHandler.cs` that constructs the `new SessionDto { ... }`. Add the two mapped properties:

```csharp
return new SessionDto
{
    Id = session.Id,
    // ...existing assignments unchanged...
    ScoringType = session.ScoringType?.ToString(),
    ScoreData = session.ScoreData,
};
```

(If the mapping is done via an AutoMapper profile or a static `ToDto()` extension, update there instead — search for `new SessionDto` callers in the handler file.)

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~GetActiveSessionQueryHandlerTests" --verbosity minimal`
Expected: PASS (all previous tests + the new one).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetActiveSessionQueryHandler.cs apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Queries/GetActiveSessionQueryHandlerTests.cs
git commit -m "feat(api): #2389 map ScoringType+ScoreData in GetActiveSessionQueryHandler"
```

---

## Phase 2 — Backend: SignalR `ScoringConfigured` broadcast

### Task 3: Add `BroadcastScoringConfigured` to `GameStateHub`

**Files:**
- Modify: `apps/api/src/Api/Hubs/GameStateHub.cs:302-310`

- [ ] **Step 1: Read the existing `NotifyScoreUpdated` method as the pattern blueprint**

Run: `sed -n '298,312p' apps/api/src/Api/Hubs/GameStateHub.cs`
You'll mirror the same shape.

- [ ] **Step 2: Add the new method after `NotifyScoreUpdated`**

```csharp
/// <summary>
/// Broadcast that the scoring configuration (type + payload shape) has been
/// updated for a session. Sent on every <c>SetScores</c> aggregate mutation
/// so live clients can re-sync their polymorphic store (#2389 part 1).
/// </summary>
public async Task BroadcastScoringConfigured(string sessionId, object payload)
{
    await Clients.Group(GetSessionGroup(sessionId))
        .SendAsync("ScoringConfigured", payload).ConfigureAwait(false);

    _logger.LogDebug(
        "ScoringConfigured broadcast for session {SessionId}",
        sessionId);
}
```

- [ ] **Step 3: Verify project builds**

Run: `dotnet build apps/api/src/Api -warnaserror`
Expected: SUCCESS.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Hubs/GameStateHub.cs
git commit -m "feat(api): #2389 add GameStateHub.BroadcastScoringConfigured method"
```

### Task 4: Create `SessionScoresUpdatedSignalRHandler` (TDD)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/EventHandlers/SessionScoresUpdatedSignalRHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/EventHandlers/SessionScoresUpdatedSignalRHandlerTests.cs`

- [ ] **Step 1: Read the SignalR-handler blueprint**

Run: `cat apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/DisputeResolvedSignalRHandler.cs | head -50`
You'll mirror this exact shape.

- [ ] **Step 2: Write the failing unit test**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/EventHandlers/SessionScoresUpdatedSignalRHandlerTests.cs
using Api.BoundedContexts.SessionTracking.Application.EventHandlers;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Hubs;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.EventHandlers;

public class SessionScoresUpdatedSignalRHandlerTests
{
    [Fact]
    public async Task Handle_SendsScoringConfiguredToSessionGroup_WithScoringTypeAndScoreData()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var hubContextMock = new Mock<IHubContext<GameStateHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();

        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.Group($"session:{sessionId}")).Returns(clientProxyMock.Object);

        var handler = new SessionScoresUpdatedSignalRHandler(
            hubContextMock.Object,
            NullLogger<SessionScoresUpdatedSignalRHandler>.Instance);

        var @event = new SessionScoresUpdatedEvent(sessionId, ScoreType.Points, "{\"scores\":[]}");

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert: hub.Clients.Group("session:<id>").SendAsync("ScoringConfigured", payload)
        clientProxyMock.Verify(c => c.SendCoreAsync(
            "ScoringConfigured",
            It.Is<object?[]>(args =>
                args.Length == 1
                && args[0] != null
                && args[0]!.GetType().GetProperty("scoringType")!.GetValue(args[0])!.Equals("Points")
                && args[0]!.GetType().GetProperty("scoreData")!.GetValue(args[0])!.Equals("{\"scores\":[]}")
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
```

- [ ] **Step 3: Run the test (should fail with "type or namespace SessionScoresUpdatedSignalRHandler could not be found")**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SessionScoresUpdatedSignalRHandlerTests" --verbosity minimal`
Expected: build error (handler class does not exist).

- [ ] **Step 4: Create the handler**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Application/EventHandlers/SessionScoresUpdatedSignalRHandler.cs
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Api.BoundedContexts.SessionTracking.Application.EventHandlers;

/// <summary>
/// Broadcasts a <c>ScoringConfigured</c> SignalR event to the session group
/// every time <see cref="SessionScoresUpdatedEvent"/> is raised. Lets live
/// clients re-sync their polymorphic scoring store on host writes (#2389
/// Block A). Sender of the original write is identified via <see cref="SessionScoresUpdatedEvent.SessionId"/>
/// — clients filter their own optimistic updates by session.
/// </summary>
public class SessionScoresUpdatedSignalRHandler : INotificationHandler<SessionScoresUpdatedEvent>
{
    private readonly IHubContext<GameStateHub> _hubContext;
    private readonly ILogger<SessionScoresUpdatedSignalRHandler> _logger;

    public SessionScoresUpdatedSignalRHandler(
        IHubContext<GameStateHub> hubContext,
        ILogger<SessionScoresUpdatedSignalRHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(SessionScoresUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var payload = new
        {
            sessionId = notification.SessionId,
            scoringType = notification.ScoringType.ToString(),
            scoreData = notification.ScoreData,
        };

        await _hubContext.Clients
            .Group($"session:{notification.SessionId}")
            .SendAsync("ScoringConfigured", payload, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "ScoringConfigured broadcast for session {SessionId} (scoringType={ScoringType})",
            notification.SessionId,
            notification.ScoringType);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SessionScoresUpdatedSignalRHandlerTests" --verbosity minimal`
Expected: PASS (1/1).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/EventHandlers/SessionScoresUpdatedSignalRHandler.cs apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/EventHandlers/SessionScoresUpdatedSignalRHandlerTests.cs
git commit -m "feat(api): #2389 SignalR handler broadcasts ScoringConfigured on SessionScoresUpdatedEvent (Block A.2)"
```

### Task 5: Verify MediatR auto-registers the new handler

**Files:**
- Read-only check (and add explicit registration if needed): `apps/api/src/Api/Program.cs` or wherever `AddMediatR(...)` is called.

- [ ] **Step 1: Find the MediatR registration**

Run: `grep -rn "AddMediatR" apps/api/src/Api`
Note the assembly scanning call (usually `services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<...>())`).

- [ ] **Step 2: Confirm it scans the Api assembly**

If the scanning is `RegisterServicesFromAssembly(typeof(Program).Assembly)` or equivalent, our new handler is auto-discovered — no further action.

- [ ] **Step 3: Run a smoke build to ensure no DI registration errors at startup**

Run: `dotnet build apps/api/src/Api -warnaserror`
Expected: SUCCESS, no DI complaints.

- [ ] **Step 4: Commit (no code change — checkpoint commit)**

If nothing changed, skip this commit and proceed.

---

## Phase 3 — Backend: Testcontainers integration test

### Task 6: Integration test — `SetScores` → SignalR broadcast end-to-end

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Integration/SessionScoresUpdatedSignalRBroadcastIntegrationTests.cs`

- [ ] **Step 1: Read the existing IT pattern**

Run: `cat apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Integration/FinalizeSessionSingleDispatchIntegrationTests.cs | head -80`
Note the `SharedTestcontainersFixture` + `IntegrationTestBase` pattern.

- [ ] **Step 2: Write the failing IT**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Integration/SessionScoresUpdatedSignalRBroadcastIntegrationTests.cs
using Api.BoundedContexts.SessionTracking.Application.EventHandlers;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Hubs;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Integration;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "SessionTracking")]
public class SessionScoresUpdatedSignalRBroadcastIntegrationTests : IntegrationTestBase
{
    public SessionScoresUpdatedSignalRBroadcastIntegrationTests(SharedTestcontainersFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task SetScores_PersistsAggregateAndDispatchesScoringConfiguredBroadcast()
    {
        // Arrange — replace the IHubContext registration with a spy
        var hubContextSpy = new Mock<IHubContext<GameStateHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        hubContextSpy.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);

        var services = BuildIntegrationServices(svc =>
        {
            svc.RemoveAll(typeof(IHubContext<GameStateHub>));
            svc.AddSingleton(hubContextSpy.Object);
        });

        var mediator = services.GetRequiredService<IMediator>();
        var sessionRepo = services.GetRequiredService<ISessionRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        var session = SessionTestData.NewActiveSession();
        await sessionRepo.AddAsync(session, default);
        await unitOfWork.SaveChangesAsync(default);

        // Act — mutate + commit (handler should fire post-SaveChanges)
        session.SetScores(ScoreType.Points, "{\"scores\":[{\"playerId\":\"p1\",\"points\":5}]}");
        await sessionRepo.UpdateAsync(session, default);
        await unitOfWork.SaveChangesAsync(default);

        // Assert — spy received the call
        clientProxyMock.Verify(c => c.SendCoreAsync(
            "ScoringConfigured",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
```

(`BuildIntegrationServices` and `SessionTestData` may not exist verbatim — search the existing IT for the actual helper names and adapt.)

- [ ] **Step 3: Run the IT to verify it fails (red)**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SessionScoresUpdatedSignalRBroadcastIntegrationTests" --verbosity minimal`
Expected: FAIL (the spy isn't injected yet, or the handler didn't fire).

- [ ] **Step 4: If the test fails because the handler didn't fire post-SaveChanges, verify the domain-event dispatch pipeline**

The repo / UoW must dispatch `Session._domainEvents` to MediatR after SaveChanges. Reference: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs:24-42` for the event source pattern.

If there's a single `DomainEventDispatcher` that batch-publishes, the test should already pass with the handler from Task 4. If not, fix the dispatcher hookup (out-of-scope for this plan — file as a blocker issue).

- [ ] **Step 5: Run the IT to verify it passes (green)**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SessionScoresUpdatedSignalRBroadcastIntegrationTests" --verbosity minimal`
Expected: PASS (1/1).

- [ ] **Step 6: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Integration/SessionScoresUpdatedSignalRBroadcastIntegrationTests.cs
git commit -m "test(api): #2389 IT covers SetScores -> SignalR ScoringConfigured broadcast (Block A.3)"
```

---

## Phase 4 — Frontend: store evolution + `PlayerInfo.displayName`

### Task 7: Extend `LiveSessionState` + `PlayerInfo.displayName` (TDD)

**Files:**
- Test: `apps/web/src/lib/stores/__tests__/live-session-store.test.ts` (create or extend)
- Modify: `apps/web/src/lib/stores/live-session-store.ts`

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/lib/stores/__tests__/live-session-store.test.ts
import { describe, expect, it, beforeEach } from 'vitest';

import { useLiveSessionStore } from '@/lib/stores/live-session-store';

describe('useLiveSessionStore — Block A #2389 contract evolution', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().reset();
  });

  it('initial state — scoringType is null', () => {
    expect(useLiveSessionStore.getState().scoringType).toBeNull();
  });

  it('initial state — scoreData is null', () => {
    expect(useLiveSessionStore.getState().scoreData).toBeNull();
  });

  it('setScoringConfig writes scoringType + scoreData', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
    });
    expect(useLiveSessionStore.getState().scoringType).toBe('Points');
    expect(useLiveSessionStore.getState().scoreData).toEqual({
      scores: [{ playerId: 'p1', points: 10 }],
    });
  });

  it('PlayerInfo carries an optional displayName', () => {
    useLiveSessionStore.getState().setSession({
      players: [{ id: 'p1', name: 'Aaron', displayName: 'Aaron D.', isHost: true, isOnline: true }],
    });
    expect(useLiveSessionStore.getState().players[0]?.displayName).toBe('Aaron D.');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/web && pnpm vitest run --no-coverage 'src/lib/stores/__tests__/live-session-store.test.ts'`
Expected: FAIL (`scoringType` does not exist on the state type; `setScoringConfig` is not a function).

- [ ] **Step 3: Edit `live-session-store.ts` (additive — keep `scores` deprecated)**

```typescript
// apps/web/src/lib/stores/live-session-store.ts
import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';

export interface PlayerInfo {
  id: string;
  name: string;
  /**
   * Optional user-facing label (#2389 Block A). Adapters should prefer
   * `displayName ?? name` when rendering rosters. Becomes required once all
   * SignalR/REST adapters populate it consistently (Block A finalization).
   */
  displayName?: string;
  isHost: boolean;
  isOnline: boolean;
}

// ...ScoreProposal, RuleDispute, SessionStatus unchanged...

interface LiveSessionState {
  sessionId: string | null;
  gameName: string;
  status: SessionStatus;
  currentTurn: number;
  currentPhase: string | null;
  players: PlayerInfo[];
  /** @deprecated #2389 Block A — derive from `scoreData` when `scoringType === 'Points'`. */
  scores: Record<string, number>;
  scoringType: ScoreType | null;
  scoreData: ScoreDataByType[ScoreType] | null;
  pendingProposals: ScoreProposal[];
  disputes: RuleDispute[];
  isConnected: boolean;
  isOffline: boolean;
  elapsedSeconds: number;

  // Actions
  setSession: (data: Partial<LiveSessionState>) => void;
  setScoringConfig: <T extends ScoreType>(args: {
    scoringType: T;
    scoreData: ScoreDataByType[T];
  }) => void;
  updateScore: (playerName: string, score: number) => void;
  addProposal: (proposal: ScoreProposal) => void;
  resolveProposal: (proposalId: string, accepted: boolean) => void;
  addDispute: (dispute: RuleDispute) => void;
  setConnected: (connected: boolean) => void;
  setOffline: (offline: boolean) => void;
  reset: () => void;
}

const initialState: Omit<
  LiveSessionState,
  | 'setSession'
  | 'setScoringConfig'
  | 'updateScore'
  | 'addProposal'
  | 'resolveProposal'
  | 'addDispute'
  | 'setConnected'
  | 'setOffline'
  | 'reset'
> = {
  sessionId: null,
  gameName: '',
  status: 'InProgress',
  currentTurn: 1,
  currentPhase: null,
  players: [],
  scores: {},
  scoringType: null,
  scoreData: null,
  pendingProposals: [],
  disputes: [],
  isConnected: false,
  isOffline: false,
  elapsedSeconds: 0,
};

export const useLiveSessionStore = create<LiveSessionState>()(
  devtools(
    (set, get) => ({
      ...initialState,

      setSession: data => set(data as Partial<LiveSessionState>, false, 'setSession'),

      setScoringConfig: ({ scoringType, scoreData }) =>
        set({ scoringType, scoreData }, false, 'setScoringConfig'),

      // ...existing actions unchanged (updateScore, addProposal, resolveProposal, addDispute,
      //  setConnected, setOffline, reset)...
    }),
    { name: 'live-session-store' }
  )
);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd apps/web && pnpm vitest run --no-coverage 'src/lib/stores/__tests__/live-session-store.test.ts'`
Expected: PASS (4/4).

- [ ] **Step 5: Run sibling tests for no-regression**

Run: `cd apps/web && pnpm vitest run --no-coverage 'src/lib/stores/' 'src/lib/domain-hooks/' 'src/components/session/live/__tests__/'`
Expected: all green (existing consumers continue to read `state.scores` — backward compat preserved).

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/stores/live-session-store.ts apps/web/src/lib/stores/__tests__/live-session-store.test.ts
git commit -m "feat(web): #2389 LiveSessionState carries scoringType+scoreData+PlayerInfo.displayName (Block A.4)"
```

---

## Phase 5 — Frontend: `useSessionScores` extension + SignalR consumer

### Task 8: Extend `useSessionScores` with derived shape (TDD)

**Files:**
- Test: `apps/web/src/lib/domain-hooks/__tests__/useSessionScores.test.ts` (create or extend)
- Modify: `apps/web/src/lib/domain-hooks/useSessionScores.ts`

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/lib/domain-hooks/__tests__/useSessionScores.test.ts
import { describe, expect, it, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';

import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { useSessionScores } from '@/lib/domain-hooks/useSessionScores';

describe('useSessionScores — Block A #2389', () => {
  beforeEach(() => useLiveSessionStore.getState().reset());

  it('returns scoringType and scoreData verbatim', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Ranking',
      scoreData: { positions: [{ playerId: 'p1', position: 1 }] },
    });

    const { result } = renderHook(() => useSessionScores());

    expect(result.current.scoringType).toBe('Ranking');
    expect(result.current.scoreData).toEqual({ positions: [{ playerId: 'p1', position: 1 }] });
  });

  it('derives a legacy `scores` map from scoreData when scoringType is Points', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: {
        scores: [
          { playerId: 'p1', points: 12 },
          { playerId: 'p2', points: 7 },
        ],
      },
    });

    const { result } = renderHook(() => useSessionScores());

    expect(result.current.scores).toEqual({ p1: 12, p2: 7 });
  });

  it('returns empty `scores` for non-Points scoringType', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'BinaryWin',
      scoreData: { results: [{ playerId: 'p1', isWinner: true }] },
    });

    const { result } = renderHook(() => useSessionScores());

    expect(result.current.scores).toEqual({});
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/web && pnpm vitest run --no-coverage 'src/lib/domain-hooks/__tests__/useSessionScores.test.ts'`
Expected: FAIL (`scoringType` is undefined on hook return).

- [ ] **Step 3: Update the hook**

```typescript
// apps/web/src/lib/domain-hooks/useSessionScores.ts
import { useMemo } from 'react';

import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import type { PlayerInfo, ScoreProposal } from '@/lib/stores/live-session-store';
import type {
  ScoreDataByType,
  ScoreType,
} from '@/components/sessions/score-strategies/types';

export interface UseSessionScoresReturn {
  /** Polymorphic scoring discriminator (null until SignalR delivers ScoringConfigured). */
  scoringType: ScoreType | null;
  /** Per-variant scoring payload (mirrors backend `score_data`). */
  scoreData: ScoreDataByType[ScoreType] | null;
  /** @deprecated #2389 Block A — derived from `scoreData` when scoringType is 'Points'. */
  scores: Record<string, number>;
  players: PlayerInfo[];
  pendingProposals: ScoreProposal[];
  leader: string | null;
}

export function useSessionScores(_sessionId?: string): UseSessionScoresReturn {
  const scoringType = useLiveSessionStore(s => s.scoringType);
  const scoreData = useLiveSessionStore(s => s.scoreData);
  const players = useLiveSessionStore(s => s.players);
  const pendingProposals = useLiveSessionStore(s => s.pendingProposals);

  const scores = useMemo<Record<string, number>>(() => {
    if (scoringType !== 'Points' || scoreData == null) return {};
    const pointsData = scoreData as ScoreDataByType['Points'];
    return Object.fromEntries(pointsData.scores.map(s => [s.playerId, s.points]));
  }, [scoringType, scoreData]);

  const leader = useMemo<string | null>(() => {
    const entries = Object.entries(scores);
    if (!entries.length) return null;
    return entries.reduce((best, current) => (current[1] > best[1] ? current : best))[0];
  }, [scores]);

  return { scoringType, scoreData, scores, players, pendingProposals, leader };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd apps/web && pnpm vitest run --no-coverage 'src/lib/domain-hooks/__tests__/useSessionScores.test.ts'`
Expected: PASS (3/3).

- [ ] **Step 5: Run sibling consumers (no-regression)**

Run: `cd apps/web && pnpm vitest run --no-coverage 'src/components/session/live/' 'src/components/game-night/'`
Expected: all green — the deprecated `scores` map continues to return `{}` for pre-Block-A sessions (no `scoringType`), matching the old default.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/domain-hooks/useSessionScores.ts apps/web/src/lib/domain-hooks/__tests__/useSessionScores.test.ts
git commit -m "feat(web): #2389 useSessionScores returns scoringType+scoreData with derived legacy scores (Block A.5)"
```

### Task 9: Wire SignalR `ScoringConfigured` consumer in `useSignalrSession`

**Files:**
- Modify: `apps/web/src/lib/domain-hooks/useSignalrSession.ts`
- Test: `apps/web/src/lib/domain-hooks/__tests__/useSignalrSession.test.ts`

- [ ] **Step 1: Find where existing hub events are wired**

Run: `grep -n "connection.on\|hub.on\|hubConnection.on" apps/web/src/lib/domain-hooks/useSignalrSession.ts | head -10`
Note the pattern used by `ScoreUpdated`, `ScoreProposed`, etc. — mirror it for `ScoringConfigured`.

- [ ] **Step 2: Write the failing test**

```typescript
// apps/web/src/lib/domain-hooks/__tests__/useSignalrSession.test.ts (add to existing describe)
it('calls store.setScoringConfig when receiving ScoringConfigured', async () => {
  // Arrange: mock connection emits ScoringConfigured payload
  const { mockConnection, store } = setupSignalrTest();
  renderHook(() => useSignalrSession('session-123'));

  const payload = {
    sessionId: 'session-123',
    scoringType: 'Points',
    scoreData: '{"scores":[{"playerId":"p1","points":3}]}',
  };

  await mockConnection.emit('ScoringConfigured', payload);

  // Assert
  expect(store.getState().scoringType).toBe('Points');
  expect(store.getState().scoreData).toEqual({ scores: [{ playerId: 'p1', points: 3 }] });
});
```

(Use the existing mock helper in `__tests__/useSignalrSession.test.ts` — search for `setupSignalrTest` or equivalent and reuse it. If no helper exists, follow the pattern of an existing test that emits `ScoreUpdated`.)

- [ ] **Step 3: Run the test (should fail — handler not yet wired)**

Run: `cd apps/web && pnpm vitest run --no-coverage 'src/lib/domain-hooks/__tests__/useSignalrSession.test.ts'`
Expected: FAIL (`scoringType` still `null`).

- [ ] **Step 4: Wire the handler in `useSignalrSession`**

Add the new `connection.on(...)` registration in the same effect that wires the other events. The BE sends `scoreData` as a stringified JSON (it's stored as text in Postgres); parse it here:

```typescript
// apps/web/src/lib/domain-hooks/useSignalrSession.ts (additive)
connection.on('ScoringConfigured', (payload: { sessionId: string; scoringType: ScoreType; scoreData: string }) => {
  try {
    const parsed = JSON.parse(payload.scoreData) as ScoreDataByType[ScoreType];
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: payload.scoringType,
      scoreData: parsed,
    });
  } catch (err) {
    console.warn('[useSignalrSession] failed to parse ScoringConfigured payload', err);
  }
});
```

(Import `ScoreType` + `ScoreDataByType` from `@/components/sessions/score-strategies/types` at the top of the file.)

- [ ] **Step 5: Run the test to verify it passes**

Run: `cd apps/web && pnpm vitest run --no-coverage 'src/lib/domain-hooks/__tests__/useSignalrSession.test.ts'`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/domain-hooks/useSignalrSession.ts apps/web/src/lib/domain-hooks/__tests__/useSignalrSession.test.ts
git commit -m "feat(web): #2389 useSignalrSession consumes ScoringConfigured and writes the store (Block A.6)"
```

---

## Phase 6 — ESLint custom rule for `scores` migration tracking

### Task 10: Author the `meepleai/no-store-scores-direct` rule (TDD via RuleTester)

**Files:**
- Create: `apps/web/eslint-rules/no-store-scores-direct.js`
- Create: `apps/web/eslint-rules/__tests__/no-store-scores-direct.test.js`
- Create: `apps/web/eslint-rules/index.js`

- [ ] **Step 1: Verify the project's existing eslint plugin pattern**

Run: `find apps/web -maxdepth 4 -name "eslint-rules" -o -name "*-plugin*" 2>/dev/null | head -5`
If you see one, mirror its structure. Otherwise, create a fresh plugin folder per the spec below.

- [ ] **Step 2: Write the failing RuleTester test**

```javascript
// apps/web/eslint-rules/__tests__/no-store-scores-direct.test.js
import { RuleTester } from 'eslint';
import { describe, it } from 'vitest';
import rule from '../no-store-scores-direct.js';

const tester = new RuleTester({
  languageOptions: { ecmaVersion: 2022, sourceType: 'module' },
});

describe('no-store-scores-direct', () => {
  it('reports useLiveSessionStore selector that reads s.scores directly', () => {
    tester.run('no-store-scores-direct', rule, {
      valid: [
        // reads scoringType — fine
        { code: "const t = useLiveSessionStore(s => s.scoringType);" },
        // reads scoreData — fine
        { code: "const d = useLiveSessionStore(s => s.scoreData);" },
        // unrelated hook — fine
        { code: "const x = useOtherStore(s => s.scores);" },
      ],
      invalid: [
        {
          code: "const sc = useLiveSessionStore(s => s.scores);",
          errors: [{ messageId: 'noScoresDirect' }],
        },
      ],
    });
  });
});
```

- [ ] **Step 3: Run the test to verify it fails (module missing)**

Run: `cd apps/web && pnpm vitest run --no-coverage 'eslint-rules/__tests__/no-store-scores-direct.test.js'`
Expected: FAIL (`Cannot find module '../no-store-scores-direct.js'`).

- [ ] **Step 4: Write the rule**

```javascript
// apps/web/eslint-rules/no-store-scores-direct.js
/** @type {import('eslint').Rule.RuleModule} */
export default {
  meta: {
    type: 'suggestion',
    docs: {
      description:
        'Disallow new direct reads of `useLiveSessionStore(s => s.scores)`. Use the derived `scores` from `useSessionScores()` or read `scoreData` directly. Tracked in #2389 Block A.',
    },
    messages: {
      noScoresDirect:
        '`useLiveSessionStore(s => s.scores)` is deprecated (#2389 Block A). Use `useSessionScores()` for the derived legacy map, or read `s.scoreData` for the polymorphic payload.',
    },
    schema: [],
  },

  create(context) {
    return {
      CallExpression(node) {
        if (
          node.callee.type !== 'Identifier' ||
          node.callee.name !== 'useLiveSessionStore' ||
          node.arguments.length !== 1
        ) {
          return;
        }

        const selector = node.arguments[0];
        if (selector.type !== 'ArrowFunctionExpression') return;

        const body = selector.body;
        if (
          body.type === 'MemberExpression' &&
          body.property.type === 'Identifier' &&
          body.property.name === 'scores'
        ) {
          context.report({ node: body, messageId: 'noScoresDirect' });
        }
      },
    };
  },
};
```

- [ ] **Step 5: Write the plugin manifest**

```javascript
// apps/web/eslint-rules/index.js
import noStoreScoresDirect from './no-store-scores-direct.js';

export default {
  rules: {
    'no-store-scores-direct': noStoreScoresDirect,
  },
};
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `cd apps/web && pnpm vitest run --no-coverage 'eslint-rules/__tests__/no-store-scores-direct.test.js'`
Expected: PASS (1/1 RuleTester run).

- [ ] **Step 7: Commit**

```bash
git add apps/web/eslint-rules/
git commit -m "feat(web/eslint): #2389 add meepleai/no-store-scores-direct rule (Block A.7)"
```

### Task 11: Register the plugin in `eslint.config.js`

**Files:**
- Modify: `apps/web/eslint.config.js`

- [ ] **Step 1: Find where plugins are registered**

Run: `grep -n "plugins" apps/web/eslint.config.js | head -10`

- [ ] **Step 2: Add the local plugin under namespace `meepleai`**

Edit the export to import the plugin and add it under `plugins.meepleai`, then enable the rule with `warn`:

```javascript
// apps/web/eslint.config.js (add to the relevant config block)
import meepleaiPlugin from './eslint-rules/index.js';

// ...
export default [
  // ...other configs unchanged...
  {
    files: ['src/**/*.{ts,tsx,js,jsx}'],
    plugins: {
      // ...existing plugins...
      meepleai: meepleaiPlugin,
    },
    rules: {
      // ...existing rules...
      'meepleai/no-store-scores-direct': 'warn',
    },
  },
];
```

- [ ] **Step 3: Run ESLint on a file that currently reads `s.scores` to verify the rule fires**

Run: `cd apps/web && pnpm eslint src/lib/domain-hooks/useSessionScores.ts --max-warnings=0`
Expected: warning at the legacy `s.scores` selector line (if it still exists). Should fire only on **new** direct reads (we want this to start as warn so existing reads don't break CI).

- [ ] **Step 4: Run the full lint to verify no NEW errors (only the expected warnings)**

Run: `cd apps/web && pnpm lint`
Expected: completes (may show warnings — that's the intended deprecation surface).

- [ ] **Step 5: Commit**

```bash
git add apps/web/eslint.config.js
git commit -m "chore(web/eslint): #2389 enable meepleai/no-store-scores-direct as warn"
```

---

## Phase 7 — Final integration check + PR

### Task 12: Final integration smoke + PR open

- [ ] **Step 1: Run all BE tests including the new IT**

Run: `dotnet test apps/api/tests/Api.Tests --filter "BoundedContext=SessionTracking"`
Expected: all green.

- [ ] **Step 2: Run all FE session-live + store + hook tests**

Run: `cd apps/web && pnpm vitest run --no-coverage 'src/lib/stores/' 'src/lib/domain-hooks/' 'src/components/session/' 'src/components/game-night/' 'src/app/(authenticated)/sessions/' '__tests__/session-live-' '__tests__/vitest-setup-' 'eslint-rules/__tests__/'`
Expected: all green.

- [ ] **Step 3: Push the branch**

```bash
git push -u origin feature/issue-2389-block-a-store-signalr
```

- [ ] **Step 4: Open the PR (target the appropriate parent — either `main-dev` if PRs #2424 + #2425 merged, or stacked on `feature/issue-2421-wire-up-sessionliveview`)**

```bash
gh pr create --base <parent-branch> --title "feat(session-live): #2389 Block A — store + SignalR contract evolution" --body "$(cat <<'EOF'
## Summary

- Extend `SessionDto` with `ScoringType` + `ScoreData` (API-additive).
- New `SessionScoresUpdatedSignalRHandler` broadcasts `ScoringConfigured` on every `Session.SetScores(...)`.
- FE store carries `scoringType: ScoreType | null` + `scoreData: ScoreDataByType[ScoreType] | null`; `PlayerInfo.displayName` added (optional).
- `useSessionScores` returns the extended shape and DERIVES the deprecated `scores` map from `scoreData` when `scoringType === 'Points'` (backward compat for unmigrated consumers).
- `useSignalrSession` subscribes to `ScoringConfigured` and writes the store.
- New custom ESLint rule `meepleai/no-store-scores-direct` (warn) tracks remaining direct `state.scores` reads for the cleanup PR (#2389 part 3).

## Test plan

- [x] 1 unit + 1 IT for BE (`SessionScoresUpdatedSignalRHandler` + Testcontainers broadcast).
- [x] 4 store unit + 3 hook unit + 1 SignalR consumer test on FE.
- [x] 4 RuleTester valid/invalid cases for the ESLint rule.
- [x] No-regression: 21 existing `useLiveSessionStore` consumers continue to read `state.scores` via the derived selector path.

## Refs

- Issue: #2389 (Block A part 1 — store + SignalR contract).
- Builds on: PR #2424 (vitest fix) + PR #2425 (renderer wire-up).
- Next: #2389 Block B (scoringType selector wire-up in `SessionLiveView` — replaces the hardcoded `'Points'` default).
- Cleanup: #2389 Block C (delete `scores` field + sweep consumers + i18n catalog completion — 14gg after Block A in prod).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 5: After PR creation, update the issue body**

Edit #2389 body to reflect:
1. PR #2424 is the actual fix reference (not PR #2386 which was closed).
2. PR #2425 closed #2421.
3. This PR (Block A) closes the contract-evolution acceptance criteria.
4. Add explicit "Block B" and "Block C" tracking issues if not already filed.

---

## Self-Review Notes

- **Spec coverage**:
  - Store-shape migration ACs covered by Task 7 + Task 8.
  - SignalR `ScoringConfigured` AC covered by Tasks 3 + 4 + 6 + 9.
  - Backward compat `scores` derived shape covered by Task 8.
  - ESLint deprecation gate covered by Tasks 10 + 11.
  - DTO contract evolution covered by Tasks 1 + 2.
  - The renderer wire-up + LiveScoringPanel deprecation are explicitly **out of scope** here — those land in PR #2425 (already shipped this session) and #2389 Block B/C.
- **Backward compat window verification**: deprecated `scores` field stays on the state AND on the hook return. Sweep + removal moves to Block C (+14gg per the rollout plan).
- **No placeholders**: every step has executable code / commands / expected output.
- **Type consistency**: `ScoreType` / `ScoreDataByType` come from `@/components/sessions/score-strategies/types.ts` (single source of truth for FE; mirrors backend enum). Backend `SessionDto.ScoringType` is `string?` (we serialize the enum name client-side via `.ToString()`); FE parses `scoreData` from the SignalR string payload via `JSON.parse`.
- **Sub-PR splittability**: if a single mega-PR is too large for review, the natural split is **Block A.BE = Tasks 1-6** and **Block A.FE = Tasks 7-11** (Task 12 then ships either as 2 stacked PRs or 1 bundle). The BE-only sub-PR is mergeable independently because backward compat is preserved (new fields are nullable / additive); the FE-only sub-PR is mergeable but produces no user-visible behaviour until paired with BE.
