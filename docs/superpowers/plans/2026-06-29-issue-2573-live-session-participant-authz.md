# Live-Session Participant Authorization (#2573) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the systemic IDOR on live-session endpoints — any authenticated user can currently mutate or read another user's live session — by enforcing per-session participant authorization on all `{sessionId}`-scoped write and sensitive-read endpoints.

**Architecture:** Single source of truth as a domain method `LiveGameSession.IsAuthorizedParticipant(userId)`; a non-throwing CQRS query `GetLiveSessionParticipantContextQuery` (dedicated file, mirrors the existing stream-context query); a reusable ASP.NET endpoint filter `RequireLiveSessionParticipantFilter` exposed via `.RequireLiveSessionParticipant()`, applied to ~35 endpoints in `LiveSessionEndpoints.cs`. The filter returns 401/404/403 directly (same convention as the existing `RequireAuthenticatedUser`/`RequireAdminSession` filters). No `Handle*` body signatures and no command/query records are modified, keeping the diff isolated from the in-flight epic #2501 work.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs + endpoint filters, MediatR (CQRS), EF Core, xUnit + FluentAssertions + Testcontainers (integration).

## Global Constraints

- **CQRS (CLAUDE.md):** endpoints use `IMediator.Send()` only; no direct service injection. The filter resolves `IMediator` from `HttpContext.RequestServices` — acceptable for a cross-cutting filter, consistent with the stream endpoint's mediator use.
- **Authz rule (canonical, verbatim):** `CreatedByUserId == userId || Players.Any(p => p.IsActive && p.UserId == userId)`.
- **404-before-403 ordering:** a non-existent session yields 404, never 403.
- **Guests never match:** `LiveSessionPlayer.UserId` is `Guid?`; a guest (`null`) must never authorize a caller. Never compare `null == null`.
- **`IsActive` is load-bearing (#2561):** a removed/kicked player (soft-deactivated) loses access. Do not drop the `p.IsActive` clause.
- **ADR-060:** unchanged here — no mutating handler is added; the filter only reads.
- **Exceptions:** `ForbiddenException` / `NotFoundException` live in `Api.Middleware.Exceptions`. The filter, however, returns `Results.*` directly (it is not in a handler), matching existing filters.
- **Scope (locked):** write **and** sensitive reads (35 endpoints). Admin actions use the **same** participant rule (no host-only tier). Exempt: create, join-by-code, `/active`, `/games/{gameId}/dispute-history`, diary POST/GET (already covered), stream (already covered).
- **Branch:** `feature/issue-2573-live-session-authz` (parent `main-dev`).

## File Structure

**Create:**
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetLiveSessionParticipantContextQuery.cs` — query + result records.
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetLiveSessionParticipantContextQueryHandler.cs` — non-throwing handler.
- `apps/api/src/Api/Filters/RequireLiveSessionParticipantFilter.cs` — the endpoint filter.
- `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/LiveGameSessionAuthorizationTests.cs` — unit tests for the domain method.
- `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/GetLiveSessionParticipantContextQueryHandlerTests.cs` — unit tests for the handler.
- `apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionParticipantAuthzEndpointTests.cs` — integration tests for the filter wiring.

**Modify:**
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs` — add `IsAuthorizedParticipant`.
- `apps/api/src/Api/Extensions/EndpointFilterExtensions.cs` — add `RequireLiveSessionParticipant()`.
- `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` — add `.RequireLiveSessionParticipant()` to 35 endpoints (+ `.Produces(403)` where missing).
- (Refactor, Task 6) `GetLiveSessionStreamContextQueryHandler.cs`, `GetLiveSessionDiaryQueryHandler.cs`, `AddDiaryEntryCommandHandler.cs` — reuse the domain method.

---

### Task 1: Domain method `IsAuthorizedParticipant`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/LiveGameSessionAuthorizationTests.cs`

**Interfaces:**
- Produces: `public bool IsAuthorizedParticipant(Guid userId)` on `LiveGameSession`.

- [ ] **Step 1: Write the failing tests**

Create `LiveGameSessionAuthorizationTests.cs`. Build sessions via the existing factory `LiveGameSession.Create(...)` and `AddPlayer(...)` (match the constructor signature already used in `LiveGameSessionTests.cs`; inspect that file for the exact `Create` parameters before writing).

```csharp
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

public class LiveGameSessionAuthorizationTests
{
    private static LiveGameSession NewSession(Guid creatorId) =>
        LiveGameSession.Create(creatorId, "Test Game", gameId: null);

    [Fact]
    public void IsAuthorizedParticipant_Creator_ReturnsTrue()
    {
        var creator = Guid.NewGuid();
        var session = NewSession(creator);
        session.IsAuthorizedParticipant(creator).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorizedParticipant_ActiveLinkedPlayer_ReturnsTrue()
    {
        var creator = Guid.NewGuid();
        var player = Guid.NewGuid();
        var session = NewSession(creator);
        session.AddPlayer(player, "Alice", PlayerColor.Red, role: null, avatarUrl: null);
        session.IsAuthorizedParticipant(player).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorizedParticipant_RemovedPlayer_ReturnsFalse()
    {
        var creator = Guid.NewGuid();
        var player = Guid.NewGuid();
        var session = NewSession(creator);
        var playerId = session.AddPlayer(player, "Alice", PlayerColor.Red, role: null, avatarUrl: null);
        session.RemovePlayer(playerId);
        session.IsAuthorizedParticipant(player).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorizedParticipant_GuestPlayer_NullUser_DoesNotAuthorizeEmptyCaller()
    {
        var creator = Guid.NewGuid();
        var session = NewSession(creator);
        session.AddPlayer(userId: null, "Guest", PlayerColor.Blue, role: null, avatarUrl: null);
        session.IsAuthorizedParticipant(Guid.Empty).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorizedParticipant_Stranger_ReturnsFalse()
    {
        var session = NewSession(Guid.NewGuid());
        session.IsAuthorizedParticipant(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorizedParticipant_EmptyUserId_ReturnsFalse()
    {
        var session = NewSession(Guid.NewGuid());
        session.IsAuthorizedParticipant(Guid.Empty).Should().BeFalse();
    }
}
```

> NOTE before writing: open `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/LiveGameSessionTests.cs` and copy the EXACT `LiveGameSession.Create(...)` and `AddPlayer(...)` signatures (parameter order, optional args, return type of `AddPlayer`). The calls above assume `Create(Guid createdByUserId, string gameName, Guid? gameId)` and `AddPlayer(Guid? userId, string displayName, PlayerColor color, PlayerRole? role, string? avatarUrl) -> Guid playerId`. Adjust to the real signatures if they differ.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~LiveGameSessionAuthorizationTests"`
Expected: FAIL — `LiveGameSession` has no `IsAuthorizedParticipant` (compile error).

- [ ] **Step 3: Add the domain method**

In `LiveGameSession.cs`, add this method next to the computed members (e.g. after the `Host` property / `HasPlayers`). `_players` is the backing `List<LiveSessionPlayer>`; if the private field has a different name, use the public `Players` collection instead.

```csharp
/// <summary>
/// Returns true if <paramref name="userId"/> is authorized to act on this session as a participant:
/// the session creator, OR an active linked player. Guest players (UserId == null) never match,
/// and a removed/deactivated player (IsActive == false) loses access (#2561).
/// Single source of truth for live-session participant authorization. Issue #2573.
/// </summary>
public bool IsAuthorizedParticipant(Guid userId)
{
    if (userId == Guid.Empty)
    {
        return false;
    }

    return CreatedByUserId == userId
        || _players.Any(p => p.IsActive && p.UserId == userId);
}
```

Ensure `using System.Linq;` is present (it usually already is in the aggregate).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~LiveGameSessionAuthorizationTests"`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/LiveGameSessionAuthorizationTests.cs
git commit -m "feat(session-live): add LiveGameSession.IsAuthorizedParticipant domain method (#2573)"
```

---

### Task 2: Participant-context query + handler

**Files:**
- Create: `.../Application/Queries/LiveSessions/GetLiveSessionParticipantContextQuery.cs`
- Create: `.../Application/Queries/LiveSessions/GetLiveSessionParticipantContextQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/GetLiveSessionParticipantContextQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `LiveGameSession.IsAuthorizedParticipant(Guid)` (Task 1); `ILiveSessionRepository.GetByIdAsync(Guid, CancellationToken)`.
- Produces: `internal record GetLiveSessionParticipantContextQuery(Guid SessionId, Guid UserId) : IQuery<LiveSessionParticipantContextResult>` and `internal record LiveSessionParticipantContextResult(bool Found, bool Authorized)`.

- [ ] **Step 1: Write the failing handler test**

Mirror `GetLiveSessionDiaryQueryHandlerTests` (mock `ILiveSessionRepository`). Inspect that file for the mock setup helper before writing.

```csharp
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

public class GetLiveSessionParticipantContextQueryHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repo = new();
    private GetLiveSessionParticipantContextQueryHandler Sut() => new(_repo.Object);

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsNotFoundAndUnauthorized()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
             .ReturnsAsync((LiveGameSession?)null);

        var result = await Sut().Handle(new GetLiveSessionParticipantContextQuery(id, Guid.NewGuid()), default);

        result.Found.Should().BeFalse();
        result.Authorized.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Creator_ReturnsFoundAndAuthorized()
    {
        var creator = Guid.NewGuid();
        var session = LiveGameSession.Create(creator, "Test Game", gameId: null);
        _repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(session);

        var result = await Sut().Handle(new GetLiveSessionParticipantContextQuery(session.Id, creator), default);

        result.Found.Should().BeTrue();
        result.Authorized.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonParticipant_ReturnsFoundButUnauthorized()
    {
        var session = LiveGameSession.Create(Guid.NewGuid(), "Test Game", gameId: null);
        _repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(session);

        var result = await Sut().Handle(new GetLiveSessionParticipantContextQuery(session.Id, Guid.NewGuid()), default);

        result.Found.Should().BeTrue();
        result.Authorized.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~GetLiveSessionParticipantContextQueryHandlerTests"`
Expected: FAIL (types don't exist — compile error).

- [ ] **Step 3: Create the query + result records**

`GetLiveSessionParticipantContextQuery.cs`:

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Result of the live-session participant authorization query.
/// Found = a session with SessionId exists; Authorized = UserId is the creator or an active linked player.
/// Non-throwing so the RequireLiveSessionParticipant endpoint filter maps the flags to 404/403. Issue #2573.
/// </summary>
internal record LiveSessionParticipantContextResult(bool Found, bool Authorized);

/// <summary>
/// Query to resolve per-session participant authorization for live-session write/read endpoints.
/// Mirrors GetLiveSessionStreamContextQuery without companion-presence. Issue #2573.
/// </summary>
internal record GetLiveSessionParticipantContextQuery(Guid SessionId, Guid UserId)
    : IQuery<LiveSessionParticipantContextResult>;
```

- [ ] **Step 4: Create the handler**

`GetLiveSessionParticipantContextQueryHandler.cs`:

```csharp
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Handles <see cref="GetLiveSessionParticipantContextQuery"/>. Resolves session-found + caller-authorized
/// without throwing, so the RequireLiveSessionParticipant endpoint filter controls the HTTP response. Issue #2573.
/// </summary>
internal sealed class GetLiveSessionParticipantContextQueryHandler
    : IQueryHandler<GetLiveSessionParticipantContextQuery, LiveSessionParticipantContextResult>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetLiveSessionParticipantContextQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<LiveSessionParticipantContextResult> Handle(
        GetLiveSessionParticipantContextQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository
            .GetByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return new LiveSessionParticipantContextResult(Found: false, Authorized: false);
        }

        return new LiveSessionParticipantContextResult(
            Found: true,
            Authorized: session.IsAuthorizedParticipant(query.UserId));
    }
}
```

> NOTE: confirm `IQueryHandler<,>` and `IQuery<>` namespaces against `GetLiveSessionStreamContextQueryHandler.cs` (it uses `Api.SharedKernel.Application.Interfaces`). MediatR handler auto-registration: verify these query handlers are discovered by the existing assembly scan (the stream/diary handlers are, so a sibling file in the same namespace will be too).

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~GetLiveSessionParticipantContextQueryHandlerTests"`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetLiveSessionParticipantContextQuery.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetLiveSessionParticipantContextQueryHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/GetLiveSessionParticipantContextQueryHandlerTests.cs
git commit -m "feat(session-live): add GetLiveSessionParticipantContextQuery (#2573)"
```

---

### Task 3: `RequireLiveSessionParticipant` endpoint filter + extension

**Files:**
- Create: `apps/api/src/Api/Filters/RequireLiveSessionParticipantFilter.cs`
- Modify: `apps/api/src/Api/Extensions/EndpointFilterExtensions.cs`

**Interfaces:**
- Consumes: `GetLiveSessionParticipantContextQuery` (Task 2); `ClaimsPrincipal.GetUserId()` (`Api.Extensions`).
- Produces: `RequireLiveSessionParticipantFilter : IEndpointFilter`; `RouteHandlerBuilder.RequireLiveSessionParticipant()`.

- [ ] **Step 1: Create the filter**

`RequireLiveSessionParticipantFilter.cs`:

```csharp
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Filters;

/// <summary>
/// Endpoint filter enforcing per-session participant authorization on live-session endpoints (#2573).
/// The caller must be the session creator or an active linked player.
/// Returns 401 if unauthenticated, 404 if the session does not exist, 403 if not a participant.
/// Reads the {sessionId} route value. Apply AFTER .RequireAuthenticatedUser().
/// Mirrors the response convention of RequireAuthenticatedUser/RequireAdminSession (returns Results.*).
/// </summary>
internal sealed class RequireLiveSessionParticipantFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        var userId = httpContext.User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        if (!httpContext.Request.RouteValues.TryGetValue("sessionId", out var rawSessionId)
            || !Guid.TryParse(rawSessionId?.ToString(), out var sessionId))
        {
            return Results.NotFound(new { error = "Live session not found" });
        }

        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        var ctx = await mediator
            .Send(new GetLiveSessionParticipantContextQuery(sessionId, userId), httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!ctx.Found)
        {
            return Results.NotFound(new { error = "Live session not found" });
        }

        if (!ctx.Authorized)
        {
            return Results.StatusCode(403);
        }

        return await next(context).ConfigureAwait(false);
    }
}
```

- [ ] **Step 2: Add the fluent extension**

In `EndpointFilterExtensions.cs`, add after `RequireAuthenticatedUser`:

```csharp
/// <summary>
/// Requires the caller to be a participant (creator or active linked player) of the live session
/// identified by the {sessionId} route value. Returns 401 / 404 / 403. Apply AFTER .RequireAuthenticatedUser().
/// Issue #2573.
/// </summary>
public static RouteHandlerBuilder RequireLiveSessionParticipant(this RouteHandlerBuilder builder)
{
    return builder.AddEndpointFilter<RequireLiveSessionParticipantFilter>();
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: Build succeeded (filter not yet wired to any endpoint).

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Filters/RequireLiveSessionParticipantFilter.cs \
        apps/api/src/Api/Extensions/EndpointFilterExtensions.cs
git commit -m "feat(session-live): add RequireLiveSessionParticipant endpoint filter (#2573)"
```

---

### Task 4: Wire the filter to the 35 endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs`

**Interfaces:**
- Consumes: `RouteHandlerBuilder.RequireLiveSessionParticipant()` (Task 3).

- [ ] **Step 1: Add `.RequireLiveSessionParticipant()` after `.RequireAuthenticatedUser()` on each of these 35 routes.** Also add `.Produces(403)` to each (and `.Produces(404)` if not already present) for OpenAPI accuracy.

WRITE (28):
`/live-sessions/{sessionId}/start`, `/pause`, `/resume`, `/complete`, `/save`,
`/players` (POST), `/players/{playerId}` (DELETE), `/turn-order` (PUT),
`/teams` (POST), `/teams/{teamId}/players/{playerId}` (PUT),
`/scores` (POST), `/scores` (PUT), `/advance-turn`, `/advance-phase`,
`/phases` (PUT), `/trigger-snapshot`, `/notes` (PUT), `/scores/parse`,
`/scores/confirm`, `/save-complete`, `/setup-checklist` (POST), `/setup-checklist` (PUT),
`/disputes` (POST), `/disputes/{disputeId}/respond` (PUT), `/disputes/{disputeId}/timeout` (POST),
`/disputes/{disputeId}/vote` (POST), `/disputes/{disputeId}/tally` (POST), `/context/refresh` (POST)

SENSITIVE READS (7):
`/live-sessions/{sessionId}` (GET), `/scores` (GET), `/players` (GET), `/phases` (GET),
`/tools` (GET), `/context` (GET), `/resume-context` (GET)

Example transformation (start):

```csharp
group.MapPost("/live-sessions/{sessionId}/start", HandleStartSession)
    .RequireAuthenticatedUser()
    .RequireLiveSessionParticipant()
    .Produces(204)
    .Produces(401)
    .Produces(403)
    .Produces(404)
    .Produces(409)
    .WithTags("LiveSessions")
    .WithSummary("Start a live session")
    .WithDescription("Transitions session from Created/Setup to InProgress. 403 if caller is not a participant.");
```

**DO NOT** add the filter to: `/live-sessions` (POST create), `/live-sessions/code/{code}` (GET), `/live-sessions/active` (GET), `/games/{gameId}/dispute-history` (GET), `/live-sessions/{sessionId}/diary` (POST + GET — already enforced in handler), `/live-sessions/{sessionId:guid}/stream` (GET — already enforced).

> Dispute sub-endpoints (`respond`/`timeout`/`vote`/`tally`) carry `{sessionId}` in the route; the filter gates on that. KNOWN LIMITATION to note in the PR: the filter checks participation in the route's sessionId, not that the dispute actually belongs to that session (cross-consistency `dispute.SessionId == route sessionId` is a separate validation — out of scope for #2573).

- [ ] **Step 2: Build**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Run the full GameManagement live-session unit/integration suite to catch regressions from the new filter**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~LiveSession"`
Expected: PASS (existing tests unaffected; the new filter only blocks non-participants, and existing tests act as the creator/participant). If any existing test now 403s, it was acting as a non-participant against a write endpoint — fix the test setup to add the acting user as a participant (this is the bug-surfacing value of the filter).

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/LiveSessionEndpoints.cs
git commit -m "feat(session-live): enforce participant authz on 35 live-session endpoints (#2573)"
```

---

### Task 5: Integration tests (filter wiring, end-to-end)

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionParticipantAuthzEndpointTests.cs`

**Interfaces:**
- Consumes: `IntegrationWebApplicationFactory`, `TestSessionHelper` (cookie auth), `IMediator` setup commands.

- [ ] **Step 1: Write the integration tests.** Model the class on `LiveSessionStreamEndpointTests` / `LiveSessionDiaryEndpointTests` (same `[Collection]`, fixture, `IAsyncLifetime`). Cover representative verbs end-to-end; rely on Task 1/2 units for breadth.

Tests to write (one method each):
1. `Write_Complete_NonParticipant_Returns403` — user A creates + AddPlayer + Start; user B (valid session, not linked) `POST /complete` → 403.
2. `Write_RecordScore_NonParticipant_Returns403` — user B `POST /scores` → 403.
3. `Read_GetSession_NonParticipant_Returns403` — user B `GET /live-sessions/{id}` → 403.
4. `Read_ResumeContext_NonParticipant_Returns403` — user B `GET /resume-context` → 403 (the photo-leak read).
5. `Write_RecordScore_Creator_Succeeds` — user A (creator) `POST /scores` → 204 (positive path; proves the filter does not block participants).
6. `Write_RecordScore_NonexistentSession_Returns404` — any authed user, random Guid → 404 (404-before-403).
7. `Write_RecordScore_Unauthenticated_Returns401` — no cookie → 401.
8. `Write_AdvanceTurn_RemovedPlayer_Returns403` — user A creates + AddPlayer(B) + RemovePlayer(B); user B → 403 (#2561 IsActive semantics end-to-end).

Use the recipe from the analysis (seed users with `EmailVerified = true`, `Status = "Active"`; create/seed session state via `IMediator`, not HTTP; auth user B via `TestSessionHelper.CreateUserSessionAsync` + `CreateAuthenticatedRequest`). Seed each target into a domain-valid state so the ONLY failure cause is the authz gate.

```csharp
[Fact(DisplayName = "POST /scores returns 403 when caller is authenticated but not a participant")]
public async Task Write_RecordScore_NonParticipant_Returns403()
{
    await using var scope = _factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

    var userAId = Guid.NewGuid();
    db.Users.Add(new UserEntity
    {
        Id = userAId, Email = $"a-{userAId:N}@test.local", DisplayName = "User A",
        PasswordHash = "x", Role = "user", Tier = "free", Status = "Active",
        EmailVerified = true, CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var sessionId = await mediator.Send(new CreateLiveSessionCommand(userAId, "Game", GameId: null));
    var playerId = await mediator.Send(new AddPlayerToLiveSessionCommand(sessionId, "P", PlayerColor.Red, userAId, null, null));
    await mediator.Send(new StartLiveSessionCommand(sessionId));

    var (_, tokenB) = await TestSessionHelper.CreateUserSessionAsync(db);
    var clientB = _factory.CreateClient();

    var body = JsonContent.Create(new { playerId, round = 1, dimension = "points", value = 10 });
    var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Post,
        $"/api/v1/live-sessions/{sessionId}/scores", tokenB);
    request.Content = body;

    var resp = await clientB.SendAsync(request);

    resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

> Adjust the exact request DTO shape (`RecordScoreRequest`) and command signatures to the real ones (verify `CreateLiveSessionCommand`, `AddPlayerToLiveSessionCommand`, `StartLiveSessionCommand` parameter lists). For test #5 the creator's call must satisfy the command's domain preconditions (player exists, session started) so the 204 is genuine.

- [ ] **Step 2: Run the integration tests**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~LiveSessionParticipantAuthzEndpointTests"`
Expected: PASS (8 tests). (Requires Docker for Testcontainers.)

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionParticipantAuthzEndpointTests.cs
git commit -m "test(session-live): integration coverage for participant authz filter (#2573)"
```

---

### Task 6: Refactor existing handlers to the single source of truth (DRY)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetLiveSessionStreamContextQueryHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetLiveSessionDiaryQueryHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/AddDiaryEntryCommandHandler.cs`

**Interfaces:**
- Consumes: `LiveGameSession.IsAuthorizedParticipant(Guid)` (Task 1).

- [ ] **Step 1: Replace the inlined predicate in each handler** with `session.IsAuthorizedParticipant(userId)`.

In `GetLiveSessionStreamContextQueryHandler.cs` (lines 37-38), replace:
```csharp
var isAuthorized = session.CreatedByUserId == query.UserId
    || session.Players.Any(p => p.IsActive && p.UserId == query.UserId);
```
with:
```csharp
var isAuthorized = session.IsAuthorizedParticipant(query.UserId);
```

Apply the equivalent replacement in `GetLiveSessionDiaryQueryHandler.cs` and `AddDiaryEntryCommandHandler.cs` (find the `CreatedByUserId == ... || Players.Any(...)` expression in each and replace with `session.IsAuthorizedParticipant(<userId/authorId var>)`). Keep each handler's surrounding throw/flag logic and action-specific message unchanged.

- [ ] **Step 2: Run the affected suites to verify no behavior change**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~LiveSessionStream|FullyQualifiedName~Diary"`
Expected: PASS (all existing stream + diary tests green — pure refactor).

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetLiveSessionStreamContextQueryHandler.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetLiveSessionDiaryQueryHandler.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/AddDiaryEntryCommandHandler.cs
git commit -m "refactor(session-live): route stream/diary authz through IsAuthorizedParticipant (#2573)"
```

---

### Task 7: Full build + suite + issue DoD

- [ ] **Step 1: Full backend build + unit suite**

Run: `dotnet build apps/api/src/Api/Api.csproj` then `dotnet test apps/api/tests/Api.Tests --filter "Category=Unit|FullyQualifiedName~LiveSession"`
Expected: Build succeeded; all targeted tests PASS; no growth over the known-flaky baseline.

- [ ] **Step 2: Update the issue checklist + note the dispute cross-consistency follow-up** in the PR body (see below). If the read-side decision (`get-by-code`/lobby/spectator) needs a separate ticket, file it.

- [ ] **Step 3: Final commit if any docs changed**, then open PR `feature/issue-2573-live-session-authz` → `main-dev` with `Closes #2573`.

---

## Self-Review

- **Spec coverage:** every `participant-check-needed` write endpoint + the 7 sensitive reads are in Task 4's list (35). Already-covered (stream, diary) refactored in Task 6, not double-gated. Public-by-design + not-session-scoped explicitly excluded. ✔
- **Placeholder scan:** all code blocks are concrete; the only "verify signature" notes point at real existing files to copy from (unavoidable given exact constructor params aren't pre-fetched). ✔
- **Type consistency:** `IsAuthorizedParticipant(Guid)` (Task 1) consumed identically in Tasks 2 & 6; `GetLiveSessionParticipantContextQuery(Guid,Guid)` + `LiveSessionParticipantContextResult(bool,bool)` consistent across Tasks 2 & 3. ✔

## Known limitations / follow-ups (document in PR)
- Dispute sub-endpoints gate on the route `{sessionId}`, not on `dispute.SessionId == route sessionId` cross-consistency — separate validation, out of scope.
- Read-side gating of `GetSessionByCode` and lobby/spectator UX is intentionally **not** changed; if spectator read access is desired, file a follow-up.
- The filter performs one extra `GetByIdAsync` per request (the command/query handler re-loads). Acceptable for live-session throughput; HybridCache mitigates. Revisit only if profiling shows it matters.
