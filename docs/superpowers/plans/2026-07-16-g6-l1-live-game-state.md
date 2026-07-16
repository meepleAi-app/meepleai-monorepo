# G6 Epic · L1 Live Game-State Projection+Streaming — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a generic, game-agnostic vertical slice — write → expose → stream → FE — of the existing free-form `LiveGameSession.GameState`, so L2 (per-game schemas) and L3 (rich flavors) can build on live game-state.

**Architecture:** Reuse `LiveGameSession.GameState` (`JsonDocument?`, already on the live aggregate with `UpdateGameState()`). Add: a `PATCH` write endpoint + CQRS command (host-authz), a `LiveSessionGameStateEvent` domain event raised on update, a `LiveSessionStreamForwarder` handler broadcasting SSE `session:game-state`, `GameState` exposed in `LiveSessionDto`, and FE plumbing (schema + client + store slice + SSE parser/route). State stays opaque JSON (per-game typing = L2).

**Tech Stack:** .NET 9 (ASP.NET Minimal APIs + MediatR + EF Core + xUnit + Moq + Testcontainers), Next.js 16 (Zustand + TanStack Query + Zod + Vitest), SSE.

## Global Constraints

- **CQRS (CLAUDE.md)**: endpoints use ONLY `IMediator.Send()` — no repo injection in endpoints.
- **ADR-060**: every mutating handler calls `_unitOfWork.SaveChangesAsync(ct)` after `UpdateAsync`; domain events dispatch **post-commit** (collector drains after SaveChanges).
- **Authz/IDOR (#2561)**: mutating game-state requires `session.IsAuthorizedParticipant(userId)` (creator or active linked player; guests `UserId==null` fail) → `ForbiddenException` (403).
- **Exceptions (#2568)**: `NotFoundException` (404), `ConflictException` (409, on Completed session — already thrown by `UpdateGameState`), `ForbiddenException` (403). Never `InvalidOperationException` (500).
- **Opaque schema**: `GameState` is free-form `JsonDocument` (C#) / `z.unknown()` (FE) — NO per-game validation at L1 (size cap only).
- **`JsonDocument` disposal**: `UpdateGameState` already disposes the prior doc (`LiveGameSession.cs:836-837`); do not double-dispose.
- **Tests**: xUnit `[Trait("Category", TestCategories.Unit)]` + `[Trait("BoundedContext","GameManagement")]`; integration `[Trait("Category", TestCategories.Integration)]` + `[Trait("Dependency","PostgreSQL")]` + `[Collection("Integration-GroupC")]`. Moq is primary. FE vitest, `data-slot` not `data-testid`.
- **Windows**: kill testhost before BE tests; `rm -rf apps/web/.next/types` if pre-commit typecheck fails on stale types.
- **Branch**: `feature/g6-l1-live-game-state` (parent `main-dev`, created; spec committed `19f784d6c`).

## Key seams (verified)

- `LiveGameSession.cs:63` `public JsonDocument? GameState`; `:824-840` `UpdateGameState(JsonDocument?, TimeProvider?)` (disposes prior); `:477` example `AddDomainEvent(...)` raise site.
- `AggregateRoot.cs:40-43` `AddDomainEvent(IDomainEvent)`.
- Event example: `Domain/Events/LiveSessionStartedEvent.cs`. New events go in `BoundedContexts/GameManagement/Domain/Events/`.
- Command template: `Application/Commands/LiveSessions/AddDiaryEntryCommand.cs` + `AddDiaryEntryCommandHandler.cs` (`ILiveSessionRepository` + `IUnitOfWork`, `GetByIdAsync`→`IsAuthorizedParticipant`→domain method→`UpdateAsync`→`SaveChangesAsync`).
- Validator template: `Application/Validators/LiveSessions/AddDiaryEntryCommandValidator.cs`.
- Forwarder: `Application/EventHandlers/LiveSessionStreamForwarder.cs:11` (`INotificationHandler<T>` per event → `_gateway.BroadcastAsync(sessionId, new LiveSessionStreamEvent("session:X", new {...}), ct)`).
- Gateway: `Application/Services/ILiveSessionStreamGateway.cs` — `LiveSessionStreamEvent(string Type, object Data, string? Id=null)`; `BroadcastAsync(Guid, LiveSessionStreamEvent, CancellationToken)`.
- Mapper registry: `SessionTracking/Domain/Services/SseEventTypeMapper.cs:14-86` `EventTypeMap` dict.
- DTO: `Application/DTOs/LiveSessions/LiveSessionDto.cs:9-33` (`internal record`); mapper `Application/Queries/LiveSessions/QueryHandlers.cs:35-93` `MapToDto` (`new LiveSessionDto(... session.Notes, session.Players.Select(...) ...)`).
- Endpoints: `Routing/LiveSessionEndpoints.cs` — `group.MapPut("/live-sessions/{sessionId}/notes", HandleUpdateNotes)` (L198) template + handler region (457-859) + `#region Request Models`.
- FE: `lib/session-live/use-session-live-stream.ts` (`SESSION_EVENT_TYPES` loop L254), `lib/session-live/parse-sse-event.ts` (switch L403-443, `parseScore` L76-95), `lib/session-live/sse-events.ts` (`SESSION_EVENT_TYPES` L64 + `SessionEvent` union), `lib/stores/live-session-store.ts`, `lib/api/clients/liveSessionsClient.ts`, `lib/api/schemas/live-sessions.schemas.ts`.

---

## Task 1: `LiveSessionGameStateEvent` domain event + raise on update

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/LiveSessionGameStateEvent.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs` (`UpdateGameState`, ~L824-840)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/LiveGameSessionGameStateEventTests.cs`

**Interfaces:**
- Produces: `LiveSessionGameStateEvent(Guid SessionId, JsonDocument? State)` (a domain event). `LiveGameSession.UpdateGameState` raises it.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class LiveGameSessionGameStateEventTests
{
    [Fact]
    public void UpdateGameState_RaisesLiveSessionGameStateEvent_WithSessionIdAndState()
    {
        // Arrange: build an in-progress session via the same factory the other domain tests use.
        var session = LiveGameSessionTestFactory.CreateInProgress(); // see note below
        using var state = JsonDocument.Parse("""{"board":"opaque"}""");

        // Act
        session.UpdateGameState(state);

        // Assert
        var evt = Assert.Single(session.DomainEvents, e => e is LiveSessionGameStateEvent);
        var gs = Assert.IsType<LiveSessionGameStateEvent>(evt);
        Assert.Equal(session.Id, gs.SessionId);
        Assert.NotNull(gs.State);
    }
}
```
> Reuse the existing test factory/helper the current `LiveGameSession` domain tests use to build an InProgress session (grep `LiveGameSession` under `tests/Api.Tests/.../GameManagement/Domain` for the builder; if none, construct via the public factory `LiveGameSession.Create(...)` + `Start(...)` as those tests do). `session.DomainEvents` is the `AggregateRoot` collection.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveGameSessionGameStateEventTests"`
Expected: FAIL — `LiveSessionGameStateEvent` does not exist / no event raised.

- [ ] **Step 3: Create the event + raise it**

`LiveSessionGameStateEvent.cs` (mirror `LiveSessionStartedEvent.cs` — same base type it uses; read that file for the exact base class name, e.g. `DomainEventBase` / `IDomainEvent`):
```csharp
using System.Text.Json;
using Api.SharedKernel.Domain.Events; // adjust to LiveSessionStartedEvent's base namespace

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Raised when a live session's free-form game-state is updated (#3025 L1).
/// Forwarded to the SSE stream as "session:game-state". State is opaque JSON.
/// </summary>
public sealed record LiveSessionGameStateEvent(Guid SessionId, JsonDocument? State)
    : DomainEventBase; // match LiveSessionStartedEvent's base exactly
```

In `LiveGameSession.UpdateGameState` (after the assignment + `UpdatedAt`), add the raise:
```csharp
        GameState?.Dispose();
        GameState = gameState;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAt = now;
        AddDomainEvent(new LiveSessionGameStateEvent(Id, gameState)); // #3025 L1
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveGameSessionGameStateEventTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/LiveSessionGameStateEvent.cs apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/LiveGameSessionGameStateEventTests.cs
git commit -m "feat(session-live): #3025 L1 LiveSessionGameStateEvent raised on UpdateGameState"
```

---

## Task 2: `UpdateLiveGameStateCommand` + validator + handler (host-authz)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/UpdateLiveGameStateCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/UpdateLiveGameStateCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Validators/LiveSessions/UpdateLiveGameStateCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/UpdateLiveGameStateCommandHandlerTests.cs`

**Interfaces:**
- Consumes: Task 1 event (raised by the domain method the handler calls).
- Produces: `UpdateLiveGameStateCommand(Guid SessionId, Guid RequestedByUserId, JsonDocument State) : ICommand<Unit>` (or the codebase's `ICommand`/`IRequest` — mirror `AddDiaryEntryCommand`).

- [ ] **Step 1: Write the failing test** (mirror `AddDiaryEntryCommandHandlerTests` / `LiveSessionCommandHandlerTests`, Moq)

```csharp
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class UpdateLiveGameStateCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private UpdateLiveGameStateCommandHandler CreateSut() => new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_AuthorizedParticipant_UpdatesStateAndSaves()
    {
        var creator = Guid.NewGuid();
        var session = LiveGameSessionTestFactory.CreateInProgress(creator); // creator authorized
        _repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        using var state = JsonDocument.Parse("""{"x":1}""");

        await CreateSut().Handle(new UpdateLiveGameStateCommand(session.Id, creator, state), CancellationToken.None);

        _repo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((LiveGameSession?)null);
        using var state = JsonDocument.Parse("{}");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateSut().Handle(new UpdateLiveGameStateCommand(Guid.NewGuid(), Guid.NewGuid(), state), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NonParticipant_ThrowsForbidden()
    {
        var session = LiveGameSessionTestFactory.CreateInProgress(Guid.NewGuid());
        _repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        using var state = JsonDocument.Parse("{}");
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateSut().Handle(new UpdateLiveGameStateCommand(session.Id, Guid.NewGuid() /* stranger */, state), CancellationToken.None));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UpdateLiveGameStateCommandHandlerTests"`
Expected: FAIL — command/handler don't exist.

- [ ] **Step 3: Write the command + validator + handler**

`UpdateLiveGameStateCommand.cs` (mirror `AddDiaryEntryCommand` — same `ICommand<T>` interface it uses):
```csharp
using System.Text.Json;
using Api.SharedKernel.Application.Interfaces; // ICommand<T> — match AddDiaryEntryCommand

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>Updates the free-form live game-state (#3025 L1). Host/participant only.</summary>
public sealed record UpdateLiveGameStateCommand(
    Guid SessionId,
    Guid RequestedByUserId,
    JsonDocument State) : ICommand<Unit>; // match AddDiaryEntryCommand's return-type convention
```

`UpdateLiveGameStateCommandValidator.cs` (mirror `AddDiaryEntryCommandValidator`):
```csharp
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

public sealed class UpdateLiveGameStateCommandValidator
    : AbstractValidator<Commands.LiveSessions.UpdateLiveGameStateCommand>
{
    // L1 opaque state: only guard non-empty ids + a size cap (reject > 256 KB serialized).
    public UpdateLiveGameStateCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.RequestedByUserId).NotEmpty();
        RuleFor(x => x.State).NotNull()
            .Must(s => s.RootElement.GetRawText().Length <= 256 * 1024)
            .WithMessage("Game state exceeds the 256 KB limit.");
    }
}
```

`UpdateLiveGameStateCommandHandler.cs` (copy `AddDiaryEntryCommandHandler` shape verbatim):
```csharp
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

internal sealed class UpdateLiveGameStateCommandHandler : ICommandHandler<UpdateLiveGameStateCommand, Unit>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateLiveGameStateCommandHandler(ILiveSessionRepository sessionRepository, IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Unit> Handle(UpdateLiveGameStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository
            .GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        if (!session.IsAuthorizedParticipant(command.RequestedByUserId))
            throw new ForbiddenException("Only the session creator or an active participant may update game state.");

        // ConflictException (Completed session) propagates → HTTP 409. Domain raises LiveSessionGameStateEvent.
        session.UpdateGameState(command.State);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
```
> Match `ICommand<Unit>` / `ICommandHandler<TCommand, Unit>` to the codebase convention (grep an existing `Unit`-returning command; if the codebase uses a non-MediatR `Unit`, mirror it). If `ICommand` handlers here return `Task` (void) instead of `Unit`, drop `Unit` and return `Task`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UpdateLiveGameStateCommandHandlerTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/UpdateLiveGameStateCommand.cs apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/UpdateLiveGameStateCommandHandler.cs apps/api/src/Api/BoundedContexts/GameManagement/Application/Validators/LiveSessions/UpdateLiveGameStateCommandValidator.cs apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/UpdateLiveGameStateCommandHandlerTests.cs
git commit -m "feat(session-live): #3025 L1 UpdateLiveGameStateCommand + host-authz handler"
```

---

## Task 3: `PATCH /live-sessions/{id}/game-state` endpoint

**Files:**
- Modify: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` (register route ~L198 area; handler in region L457-859; `UpdateGameStateRequest` in `#region Request Models`)

**Interfaces:**
- Consumes: `UpdateLiveGameStateCommand` (Task 2).

- [ ] **Step 1: Add the request model, route, and handler** (mirror `HandleUpdateNotes` at `LiveSessionEndpoints.cs:198`)

Register (next to the other `MapPut` live-session routes):
```csharp
        group.MapPut("/live-sessions/{sessionId}/game-state", HandleUpdateGameState)
            .WithName("UpdateLiveGameState")
            .WithSummary("Update the live game-state (host/participant only).");
```

Handler (in the Command Handlers region — copy `HandleUpdateNotes`'s signature: how it reads `sessionId`, the authenticated user id, and `IMediator`):
```csharp
    private static async Task<IResult> HandleUpdateGameState(
        Guid sessionId,
        UpdateGameStateRequest request,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        var userId = httpContext.GetAuthenticatedUserId(); // MIRROR HandleUpdateNotes's user-id extraction
        await mediator.Send(new UpdateLiveGameStateCommand(sessionId, userId, request.State), ct);
        return Results.NoContent();
    }
```

Request model (`#region Request Models`):
```csharp
    /// <summary>Body for PATCH/PUT /live-sessions/{id}/game-state. Opaque JSON (#3025 L1).</summary>
    internal sealed record UpdateGameStateRequest(JsonDocument State);
```
> `using System.Text.Json;` at the top if not present. Confirm the exact user-id extraction helper by reading `HandleUpdateNotes` (it already does auth for a mutating live-session route) and copy it verbatim.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: build succeeds (endpoint + command wired). (Endpoint behaviour is covered by the Task 6 integration test — no separate unit test here.)

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/LiveSessionEndpoints.cs
git commit -m "feat(session-live): #3025 L1 PATCH /live-sessions/{id}/game-state endpoint"
```

---

## Task 4: Stream `session:game-state` (forwarder + SseEventTypeMapper)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/LiveSessionStreamForwarder.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/SseEventTypeMapper.cs` (`EventTypeMap`, L14-86)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/EventHandlers/LiveSessionStreamForwarderGameStateTests.cs`

**Interfaces:**
- Consumes: `LiveSessionGameStateEvent` (Task 1), `ILiveSessionStreamGateway`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class LiveSessionStreamForwarderGameStateTests
{
    [Fact]
    public async Task Handle_GameStateEvent_BroadcastsSessionGameState()
    {
        var gateway = new Mock<ILiveSessionStreamGateway>();
        var sut = new LiveSessionStreamForwarder(gateway.Object, NullLogger<LiveSessionStreamForwarder>.Instance);
        var sessionId = Guid.NewGuid();
        using var state = JsonDocument.Parse("""{"k":"v"}""");

        await sut.Handle(new LiveSessionGameStateEvent(sessionId, state), CancellationToken.None);

        gateway.Verify(g => g.BroadcastAsync(
            sessionId,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:game-state"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionStreamForwarderGameStateTests"`
Expected: FAIL — no `INotificationHandler<LiveSessionGameStateEvent>` on the forwarder.

- [ ] **Step 3: Add the handler + mapper entry**

In `LiveSessionStreamForwarder`, add `INotificationHandler<LiveSessionGameStateEvent>` to the class's interface list, and the handler method (mirror the `session:score` handler):
```csharp
    public Task Handle(LiveSessionGameStateEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:game-state for session {SessionId}", notification.SessionId);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:game-state", new
            {
                // Opaque state forwarded as-is (L1). L3 flavors parse it per game.
                state = notification.State,
            }),
            cancellationToken);
    }
```

In `SseEventTypeMapper.EventTypeMap`, add (before the closing brace, next to `session:score`):
```csharp
        [typeof(LiveSessionGameStateEvent)] = "session:game-state",
```
> Add `using Api.BoundedContexts.GameManagement.Domain.Events;` to the mapper if not present.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionStreamForwarderGameStateTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/LiveSessionStreamForwarder.cs apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/SseEventTypeMapper.cs apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/EventHandlers/LiveSessionStreamForwarderGameStateTests.cs
git commit -m "feat(session-live): #3025 L1 stream session:game-state via forwarder + mapper"
```

---

## Task 5: Expose `GameState` in `LiveSessionDto` + mapper

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/LiveSessions/LiveSessionDto.cs:9-33`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/QueryHandlers.cs:35-93` (`MapToDto`)
- Test: extend the existing `GetLiveSessionQuery` handler test (grep `GetLiveSession` tests under GameManagement) to assert `GameState` round-trips.

- [ ] **Step 1: Add the DTO field**

In `LiveSessionDto` record, add after `string? Notes` (L28):
```csharp
    string? Notes,
    System.Text.Json.JsonDocument? GameState,
    IReadOnlyList<LiveSessionPlayerDto> Players,
```

- [ ] **Step 2: Wire the mapper**

In `QueryHandlers.cs` `MapToDto`, add `session.GameState,` in the `new LiveSessionDto(...)` call, positioned after `session.Notes,` and before `session.Players.Select(...)`:
```csharp
            session.Notes,
            session.GameState,
            session.Players.Select(/* …unchanged… */),
```

- [ ] **Step 3: Write/extend the failing test then run**

Add to the existing `GetLiveSessionQueryHandler` test (or create one) a case: seed a session with a non-null `GameState`, run the query, assert `dto.GameState` is not null and round-trips the JSON.
Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetLiveSession"`
Expected: PASS after the mapper change (FAIL before if the assertion is added first).

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/LiveSessions/LiveSessionDto.cs apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/QueryHandlers.cs apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Queries/LiveSessions/
git commit -m "feat(session-live): #3025 L1 expose GameState in LiveSessionDto + mapper"
```

---

## Task 6: BE integration test (PATCH → persist → GET)

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Integration/LiveGameStateEndpointIntegrationTests.cs`

- [ ] **Step 1: Write the integration test** (mirror an existing live-session endpoint integration test — grep `[Collection("Integration-GroupC")]` under GameManagement for the WebApplicationFactory client + auth-seeding helper)

```csharp
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Dependency", "PostgreSQL")]
[Collection("Integration-GroupC")]
public sealed class LiveGameStateEndpointIntegrationTests
{
    // Arrange: create a live session as the host (reuse the fixture's helper),
    // PATCH /api/v1/live-sessions/{id}/game-state with { "state": { "k": 1 } } as the host,
    // then GET /api/v1/sessions/{id}/live (or the live-session GET) and assert the returned
    // gameState round-trips. Assert a non-participant PATCH → 403, and PATCH on a Completed
    // session → 409. Follow the existing integration test's HttpClient + auth cookie pattern.
}
```
> This is the end-to-end proof of the write→persist→expose path. Copy the fixture wiring from a sibling `[Collection("Integration-GroupC")]` live-session test verbatim; only the request/assertions are new.

- [ ] **Step 2: Run (requires Docker)**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveGameStateEndpointIntegrationTests"`
Expected: PASS (Docker up).

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Integration/LiveGameStateEndpointIntegrationTests.cs
git commit -m "test(session-live): #3025 L1 game-state endpoint integration (PATCH→persist→GET+403/409)"
```

---

## Task 7: FE schema — `gameState` on `LiveSessionDtoSchema`

**Files:**
- Modify: `apps/web/src/lib/api/schemas/live-sessions.schemas.ts`
- Test: `apps/web/src/lib/api/schemas/__tests__/live-sessions.schemas.test.ts` (create or extend)

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from 'vitest';
import { LiveSessionDtoSchema } from '../live-sessions.schemas';

describe('LiveSessionDtoSchema — gameState (#3025 L1)', () => {
  it('accepts an opaque gameState object and a null', () => {
    const base = { /* build a minimal valid LiveSessionDto — copy an existing fixture in this test dir */ } as const;
    expect(LiveSessionDtoSchema.parse({ ...base, gameState: { board: 'x' } }).gameState).toEqual({ board: 'x' });
    expect(LiveSessionDtoSchema.parse({ ...base, gameState: null }).gameState).toBeNull();
  });
});
```
> Reuse an existing valid `LiveSessionDto` fixture from the schema tests / `SessionLiveView.test.tsx` `MOCK_SESSION_DTO` shape for `base`.

- [ ] **Step 2: Run → fail**

Run: `pnpm --dir apps/web exec vitest run src/lib/api/schemas/__tests__/live-sessions.schemas.test.ts`
Expected: FAIL — `gameState` stripped/rejected.

- [ ] **Step 3: Add the field**

In `LiveSessionDtoSchema` (add after `scoringConfig` or `notes`):
```ts
  gameState: z.unknown().nullable().optional(),
```

- [ ] **Step 4: Run → pass**; **Step 5: Commit**

```bash
git add apps/web/src/lib/api/schemas/live-sessions.schemas.ts apps/web/src/lib/api/schemas/__tests__/live-sessions.schemas.test.ts
git commit -m "feat(session-live): #3025 L1 gameState on LiveSessionDtoSchema"
```

---

## Task 8: FE client + mutation — `updateGameState`

**Files:**
- Modify: `apps/web/src/lib/api/clients/liveSessionsClient.ts`
- Create: `apps/web/src/hooks/mutations/useUpdateLiveGameState.ts`
- Test: `apps/web/src/hooks/mutations/__tests__/useUpdateLiveGameState.test.tsx`

- [ ] **Step 1: Add the client method** (mirror an existing PATCH/PUT method like `updateNotes`/`configurePhases` in `liveSessionsClient.ts`)

```ts
  /** #3025 L1: update the live game-state (host/participant). */
  async updateGameState(sessionId, state) {
    await httpClient.put(`${BASE}/${encodeURIComponent(sessionId)}/game-state`, { state });
  },
```
Add to the client interface: `updateGameState(sessionId: string, state: unknown): Promise<void>;`.

- [ ] **Step 2: Add the mutation hook + test** (mirror `useUpdateSessionScores` / any live-session mutation hook)

```ts
export function useUpdateLiveGameState(sessionId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (state: unknown) => api.liveSessions.updateGameState(sessionId, state),
    onSuccess: () => qc.invalidateQueries({ queryKey: liveSessionKeys.detail(sessionId) }),
  });
}
```
Test: mock `api.liveSessions.updateGameState`, assert the mutation calls it with the state.

- [ ] **Step 3: Run → pass**; **Step 4: Commit**

```bash
git add apps/web/src/lib/api/clients/liveSessionsClient.ts apps/web/src/hooks/mutations/useUpdateLiveGameState.ts apps/web/src/hooks/mutations/__tests__/useUpdateLiveGameState.test.tsx
git commit -m "feat(session-live): #3025 L1 updateGameState client + mutation hook"
```

---

## Task 9: FE store slice — `gameState` + `setGameState`

**Files:**
- Modify: `apps/web/src/lib/stores/live-session-store.ts`
- Test: `apps/web/src/lib/stores/__tests__/live-session-store.test.ts` (create or extend)

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it, beforeEach } from 'vitest';
import { useLiveSessionStore } from '../live-session-store';

describe('useLiveSessionStore — gameState (#3025 L1)', () => {
  beforeEach(() => useLiveSessionStore.getState().reset());
  it('setGameState sets + reset clears', () => {
    useLiveSessionStore.getState().setGameState({ board: 'x' });
    expect(useLiveSessionStore.getState().gameState).toEqual({ board: 'x' });
    useLiveSessionStore.getState().reset();
    expect(useLiveSessionStore.getState().gameState).toBeNull();
  });
});
```

- [ ] **Step 2: Run → fail**

Run: `pnpm --dir apps/web exec vitest run src/lib/stores/__tests__/live-session-store.test.ts`
Expected: FAIL — no `gameState`/`setGameState`.

- [ ] **Step 3: Add the slice** — 4 edits mirroring `turnOrderType`/`setTurnOrderType`:
  1. `interface LiveSessionState`: `gameState: unknown | null;` and `setGameState: (next: unknown | null) => void;`
  2. `initialState`: `gameState: null,`
  3. Add `'setGameState'` to the `Omit<LiveSessionState, ...>` union in the `initialState` type.
  4. Actions: `setGameState: next => set({ gameState: next }, false, 'setGameState'),`

- [ ] **Step 4: Run → pass**; **Step 5: Commit**

```bash
git add apps/web/src/lib/stores/live-session-store.ts apps/web/src/lib/stores/__tests__/live-session-store.test.ts
git commit -m "feat(session-live): #3025 L1 gameState store slice + setGameState"
```

---

## Task 10: FE SSE — parse + route `session:game-state`

**Files:**
- Modify: `apps/web/src/lib/session-live/sse-events.ts` (`SESSION_EVENT_TYPES` L64 + `SessionEvent` union)
- Modify: `apps/web/src/lib/session-live/parse-sse-event.ts` (switch L403-443 + a `parseGameState`)
- Test: `apps/web/src/lib/session-live/__tests__/parse-sse-event.test.ts` (extend)

**Interfaces:**
- Produces: a `SessionEvent` variant `{ type: 'session:game-state'; sessionId: string; state: unknown }`.

- [ ] **Step 1: Write the failing test** (mirror the existing `parseScore` test)

```ts
it('parses a session:game-state event (#3025 L1)', () => {
  const evt = parseSseEvent('session:game-state', JSON.stringify({ state: { board: 'x' } }), 'sess-1');
  expect(evt).toEqual({ type: 'session:game-state', sessionId: 'sess-1', state: { board: 'x' } });
});
```
> Match the actual `parseSseEvent` entry-point signature used by the sibling tests.

- [ ] **Step 2: Run → fail**; **Step 3: Implement**

In `sse-events.ts`: add `'session:game-state'` to `SESSION_EVENT_TYPES` and a variant to the `SessionEvent` discriminated union:
```ts
  | { type: 'session:game-state'; sessionId: string; state: unknown }
```

In `parse-sse-event.ts`, add the parser (mirror `parseScore` L76-95 — defensive, returns `null` on bad input) + the switch case (L443):
```ts
function parseGameState(data: unknown, sessionId: string): Extract<SessionEvent, { type: 'session:game-state' }> | null {
  if (typeof data !== 'object' || data === null) return null;
  const state = (data as { state?: unknown }).state;
  return { type: 'session:game-state', sessionId, state: state ?? null };
}
// …in the switch:
    case 'session:game-state':
      event = parseGameState(data, resolvedSessionId);
      break;
```

- [ ] **Step 4: Run → pass**; **Step 5: Commit**

```bash
git add apps/web/src/lib/session-live/sse-events.ts apps/web/src/lib/session-live/parse-sse-event.ts apps/web/src/lib/session-live/__tests__/parse-sse-event.test.ts
git commit -m "feat(session-live): #3025 L1 parse + type session:game-state SSE event"
```

---

## Task 11: FE wire — hydrate from DTO + route SSE event → store

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`
- Test: extend `.../__tests__/SessionLiveView.test.tsx`

- [ ] **Step 1: Hydrate + route**

1. **Hydrate** on load: where the shell seeds the store from `sessionQuery.data` (mirror the `setScoringConfig`/`setTurnOrderType` seeding), add `useLiveSessionStore.getState().setGameState(liveSessionDto?.gameState ?? null)` in the same effect.
2. **Route the SSE event**: where `liveStream.events` are consumed (the `composeSessionLiveState` / event effect), add handling for `type === 'session:game-state'` → `setGameState(event.state)`. If there is a central reducer over `liveStream.events`, add the case there; otherwise add an effect that scans new events for `session:game-state` and calls `setGameState`.

- [ ] **Step 2: Write the test**

```tsx
it('#3025 L1 hydrates gameState from the DTO', () => {
  useLiveSessionMock.mockReturnValue({
    data: { ...MOCK_SESSION_DTO, gameState: { board: 'x' } } as unknown as LiveSessionDto,
    isLoading: false, isError: false, isSuccess: true, error: null, refetch: vi.fn(),
  });
  renderWithIntl(<SessionLiveView />);
  expect(useLiveSessionStore.getState().gameState).toEqual({ board: 'x' });
});
```

- [ ] **Step 3: Run → pass**

Run: `pnpm --dir apps/web exec vitest run "src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx"`
Expected: PASS + no regression (all existing tests green).

- [ ] **Step 4: Commit**

```bash
git add "apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx" "apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx"
git commit -m "feat(session-live): #3025 L1 hydrate gameState from DTO + route SSE event to store"
```

---

## Task 12: Full verification + PR

- [ ] **Step 1: BE quality gate**

Run (kill testhost first if needed): `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "Category=Unit&BoundedContext=GameManagement"` then the integration filter with Docker up.
Expected: all green.

- [ ] **Step 2: FE quality gate**

Run: `rm -rf apps/web/.next/types && pnpm --dir apps/web typecheck && pnpm --dir apps/web exec vitest run src/lib/session-live src/lib/stores src/lib/api "src/app/(authenticated)/sessions/[id]/live"`
Expected: all green, 0 regressions.

- [ ] **Step 3: Push + PR to `main-dev`**

```bash
git push -u origin feature/g6-l1-live-game-state
gh pr create --base main-dev --title "feat(session-live): #3025 L1 live game-state projection + streaming" --body "<summary + DoD + 'opaque at L1, per-game typing = L2' + refs #3025>"
```

- [ ] **Step 4: Epic update** — tick L1 in #3025; note L2 (per-game schemas) is unblocked.

## Self-Review

**Spec coverage:** write (T2/T3) ✅ · expose (T5) ✅ · stream (T1 event, T4 forwarder+mapper) ✅ · FE schema (T7)/client (T8)/store (T9)/SSE (T10)/wire (T11) ✅ · opaque schema ✅ · host-entered authz/IDOR (T2) ✅ · 409-on-completed (domain, tested T6) ✅ · tests BE unit+integration + FE ✅ · out-of-scope (per-game schema/engine/undo/delta) untouched ✅.

**Placeholder scan:** the remaining `> …` notes are **adapt-points against real files** (test factory name, `ICommand<Unit>` vs `Task` convention, the endpoint user-id helper, an existing DTO fixture) — each names the exact file to copy from. They are verify-seams, not TODOs. No vague "handle errors"/"add validation" steps.

**Type consistency:** `LiveSessionGameStateEvent(Guid SessionId, JsonDocument? State)` consistent across T1/T2/T4. `UpdateLiveGameStateCommand(SessionId, RequestedByUserId, State)` consistent T2/T3. `gameState`/`setGameState` consistent T9/T10/T11. SSE type `"session:game-state"` consistent T4/T10.

## Risks / adapt-points (confirm during implementation)

1. **`ICommand`/`Unit` convention** — confirm whether GameManagement command handlers return `Unit` (MediatR) or `Task`; mirror `AddDiaryEntryCommand` (returns `Guid`) exactly for the interface, using `Unit`/void as that codebase does for no-return commands.
2. **Domain event base** — read `LiveSessionStartedEvent.cs` for the exact base type/namespace (`DomainEventBase` vs `IDomainEvent`); match it.
3. **Endpoint user-id extraction** — copy verbatim from `HandleUpdateNotes` (a mutating live-session route already doing auth).
4. **Raise-in-domain vs handler** — the event is raised in `UpdateGameState` (so restore-snapshot also streams a state change — acceptable; note it). If undesired, move the raise into the command handler after `UpdateGameState` via a dedicated domain method.
5. **FE SSE routing shape** — confirm whether `liveStream.events` go through a central reducer (add a case) or need an effect (add one) in `SessionLiveView`.
6. **`JsonDocument` lifetime in the event** — the event holds the same `JsonDocument` the aggregate keeps; the forwarder reads it during post-commit dispatch before any next update disposes it. Fine for L1; if flakiness appears, clone the raw text into the event instead.
