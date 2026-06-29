# Public Session Lobby by Code (#2590) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans / subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Fix the guest QR-join data-disclosure + broken-feature contradiction (#2590) by adding a dedicated **public, rate-limited, narrow** lobby endpoint, leaving the existing authenticated full-DTO route untouched.

**Architecture:** Two-contract split (expand-and-contract, additive, non-breaking), mirroring the GameNight RSVP public pattern (#1169). New `GET /api/v1/live-sessions/code/{code}/public` → `.AllowAnonymous()` + `.RequireRateLimiting("LiveSessionCodeReadPublic")` → new `GetPublicLiveSessionByCodeQuery` → server-side projection into a new narrow `PublicLiveSessionDto`. The existing `GET /code/{code}` (`.RequireAuthenticatedUser()`, full `LiveSessionDto`) is unchanged so the registered-user join flow keeps working.

**Tech Stack:** .NET 9 Minimal APIs + endpoint rate limiting, MediatR (CQRS), EF Core; Next.js 16 + Zod; xUnit + Testcontainers + Vitest.

## Global Constraints (decisions locked with product)
- **Code-as-capability**: NO visibility check. Any valid code resolves the narrow lobby (the code is already a voluntarily-shared secret). `PlayRecordVisibility` has only `{Private, Group}` — do not add a `Public` value here.
- **Scope = new public endpoint only**: the existing authenticated `GET /code/{code}` + full `LiveSessionDto` is UNTOUCHED. Narrowing the authenticated tier is a separate follow-up.
- **Narrow whitelist (exact)**: response = `{ Id, SessionCode, GameName, GameSlug, Status, Players[] }`, each player = `{ Id, DisplayName, Color, TotalScore, CurrentRank, IsActive }`. **Excluded**: `CreatedByUserId`, player `UserId`, `Notes`, `RoundScores`, `Teams`, `GroupId`, `GameId`, `Visibility`, `AgentMode`, `AvatarUrl`, all timestamps, `CurrentTurnPlayerId`.
- **Defaults**: `displayName` shown as today; Completed session → 200 (FE renders "Completata"); unknown code → 404 (no enumeration oracle beyond existence); rate-limit 60/min per IP (mirror `GameNightTokenRead`), `GetClientIpAddress` (RemoteIp only).
- **Backward-compat**: do NOT change `LiveSessionDtoSchema` / `getByCode`. Add a NEW FE schema + client method.
- **Branch**: `feature/issue-2590-public-session-lobby` (parent `main-dev`).

## File Structure

**Create (BE):**
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/LiveSessions/PublicLiveSessionDto.cs` — narrow records.
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetPublicLiveSessionByCodeQuery.cs` — query.
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetPublicLiveSessionByCodeQueryHandler.cs` — projection handler.
- `apps/api/tests/Api.Tests/.../Handlers/LiveSessions/GetPublicLiveSessionByCodeQueryHandlerTests.cs` — unit.
- `apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionByCodePublicEndpointTests.cs` — integration.

**Modify (BE):**
- `apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs` — add `LiveSessionCodeReadPublic` policy (both blocks).
- `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` — new public endpoint + handler.

**Create/Modify (FE):**
- `apps/web/src/lib/api/schemas/live-sessions.schemas.ts` — `PublicLiveSessionDtoSchema`.
- `apps/web/src/lib/api/clients/liveSessionsClient.ts` — `getPublicByCode(code)`.
- `apps/web/src/app/(public)/join/session/[code]/guest-session-view.tsx` — repoint fetch to the typed public client.
- `apps/web/src/app/(public)/join/session/[code]/__tests__/...` — FE test (schema + repoint).

---

### Task 1: Narrow `PublicLiveSessionDto`

**Files:**
- Create: `.../DTOs/LiveSessions/PublicLiveSessionDto.cs`

**Interfaces:**
- Produces: `public sealed record PublicLiveSessionDto(Guid Id, string SessionCode, string GameName, string GameSlug, LiveSessionStatus Status, IReadOnlyList<PublicLiveSessionPlayerDto> Players)` and `public sealed record PublicLiveSessionPlayerDto(Guid Id, string DisplayName, PlayerColor Color, int TotalScore, int CurrentRank, bool IsActive)`.

- [ ] **Step 1: Create the records** (mirror the `public sealed record` style of `PublicGameNightInvitationDto`).

```csharp
using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;

/// <summary>
/// Public, code-addressable read-only lobby/scoreboard projection of a live session (#2590).
/// Deliberately narrow: NO CreatedByUserId, no player UserId, no Notes/RoundScores/Teams/GroupId/Visibility.
/// Served by GET /api/v1/live-sessions/code/{code}/public (AllowAnonymous + rate-limited).
/// </summary>
public sealed record PublicLiveSessionDto(
    Guid Id,
    string SessionCode,
    string GameName,
    string GameSlug,
    LiveSessionStatus Status,
    IReadOnlyList<PublicLiveSessionPlayerDto> Players);

/// <summary>Public scoreboard player projection — session-scoped player Id only, never the linked UserId.</summary>
public sealed record PublicLiveSessionPlayerDto(
    Guid Id,
    string DisplayName,
    PlayerColor Color,
    int TotalScore,
    int CurrentRank,
    bool IsActive);
```

- [ ] **Step 2: Build** `dotnet build apps/api/src/Api/Api.csproj` → succeeds.
- [ ] **Step 3: Commit** `feat(session-live): add narrow PublicLiveSessionDto (#2590)`

---

### Task 2: `GetPublicLiveSessionByCodeQuery` + projection handler

**Files:**
- Create: `.../Queries/LiveSessions/GetPublicLiveSessionByCodeQuery.cs`
- Create: `.../Queries/LiveSessions/GetPublicLiveSessionByCodeQueryHandler.cs`
- Test: `.../Handlers/LiveSessions/GetPublicLiveSessionByCodeQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `ILiveSessionRepository.GetByCodeAsync(string, CancellationToken)`; `PublicLiveSessionDto` (Task 1).
- Produces: `internal record GetPublicLiveSessionByCodeQuery(string SessionCode) : IQuery<PublicLiveSessionDto?>`.

- [ ] **Step 1: Write the failing unit tests** (mock `ILiveSessionRepository`; build a session via `LiveGameSession.Create(...)` + `AddPlayer(...)` with Notes/scores set, assert the projection omits sensitive members and that an unknown code returns null).

```csharp
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetPublicLiveSessionByCodeQueryHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repo = new();
    private GetPublicLiveSessionByCodeQueryHandler Sut() => new(_repo.Object);

    [Fact]
    public async Task Handle_UnknownCode_ReturnsNull()
    {
        _repo.Setup(r => r.GetByCodeAsync("NOPE12", It.IsAny<CancellationToken>()))
             .ReturnsAsync((LiveGameSession?)null);

        var result = await Sut().Handle(new GetPublicLiveSessionByCodeQuery("NOPE12"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_KnownCode_ProjectsNarrowDto_WithoutSensitiveFields()
    {
        var session = LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Catan", TimeProvider.System, gameId: Guid.NewGuid());
        var player = session.AddPlayer(Guid.NewGuid(), "Alice", PlayerColor.Red); // linked user
        _repo.Setup(r => r.GetByCodeAsync(session.SessionCode, It.IsAny<CancellationToken>()))
             .ReturnsAsync(session);

        var result = await Sut().Handle(new GetPublicLiveSessionByCodeQuery(session.SessionCode), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(session.Id);
        result.GameName.Should().Be("Catan");
        result.Players.Should().ContainSingle();
        result.Players[0].DisplayName.Should().Be("Alice");
        // Compile-time guarantee: PublicLiveSessionPlayerDto has NO UserId member, PublicLiveSessionDto has NO Notes/CreatedByUserId.
        typeof(PublicLiveSessionPlayerDto).GetProperty("UserId").Should().BeNull();
        typeof(PublicLiveSessionDto).GetProperty("CreatedByUserId").Should().BeNull();
        typeof(PublicLiveSessionDto).GetProperty("Notes").Should().BeNull();
    }
}
```

- [ ] **Step 2: Run → FAIL** (`dotnet test ... --filter "FullyQualifiedName~GetPublicLiveSessionByCodeQueryHandlerTests"`) — types missing.

- [ ] **Step 3: Create the query**

```csharp
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Public lookup of a live session by join code, returning a narrow read-only lobby projection.
/// Code-as-capability: any valid code resolves; null result → 404 at the endpoint. Issue #2590.
/// </summary>
internal record GetPublicLiveSessionByCodeQuery(string SessionCode) : IQuery<PublicLiveSessionDto?>;
```

- [ ] **Step 4: Create the handler** (server-side projection — do NOT call `GetLiveSessionQueryHandler.MapToDto`).

```csharp
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

internal sealed class GetPublicLiveSessionByCodeQueryHandler
    : IQueryHandler<GetPublicLiveSessionByCodeQuery, PublicLiveSessionDto?>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetPublicLiveSessionByCodeQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<PublicLiveSessionDto?> Handle(
        GetPublicLiveSessionByCodeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository
            .GetByCodeAsync(query.SessionCode, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return null;
        }

        var players = session.Players
            .Select(p => new PublicLiveSessionPlayerDto(
                p.Id, p.DisplayName, p.Color, p.TotalScore, p.CurrentRank, p.IsActive))
            .ToList();

        return new PublicLiveSessionDto(
            session.Id, session.SessionCode, session.GameName, session.GameSlug, session.Status, players);
    }
}
```

> NOTE: confirm `LiveGameSession.GameSlug` and `LiveSessionPlayer.{TotalScore,CurrentRank,Color,DisplayName,IsActive}` accessors exist (they back the existing `LiveSessionDto`/`LiveSessionPlayerDto` mapping). If `GameSlug` is computed in `MapToDto` rather than on the aggregate, replicate that derivation here.

- [ ] **Step 5: Run → PASS.**
- [ ] **Step 6: Commit** `feat(session-live): add GetPublicLiveSessionByCodeQuery projection (#2590)`

---

### Task 3: Rate-limit policy `LiveSessionCodeReadPublic`

**Files:**
- Modify: `apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs`

- [ ] **Step 1: Add the policy in BOTH blocks.** First read the file: the disabled/test block (~line 138) registers each policy as `GetNoLimiter`; the real block (`services.AddRateLimiter` ~line 152) registers `GameNightTokenRead` as a 60/min per-IP sliding window via `GetClientIpAddress`. Add a sibling `LiveSessionCodeReadPublic` next to each `GameNightTokenRead` registration, copying the exact 60/min sliding-window shape from the real `GameNightTokenRead` definition (locate it in the real block; the grep at plan time only surfaced the disabled-block copy at line 138).

Disabled/test block (next to line ~139):
```csharp
options.AddPolicy("LiveSessionCodeReadPublic", _ =>
    RateLimitPartition.GetNoLimiter<string>("unlimited"));
```

Real block: replicate the `GameNightTokenRead` SlidingWindow policy verbatim with `partitionKey: $"live-session-code-read-{GetClientIpAddress(httpContext)}"`, `Window = 1min`, `PermitLimit = 60`, `QueueLimit = 0` (match the real `GameNightTokenRead` values exactly).

- [ ] **Step 2: Build** → succeeds.
- [ ] **Step 3: Commit** `feat(session-live): add LiveSessionCodeReadPublic rate-limit policy (#2590)`

---

### Task 4: Public endpoint wiring + integration tests

**Files:**
- Modify: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionByCodePublicEndpointTests.cs`

**Interfaces:**
- Consumes: `GetPublicLiveSessionByCodeQuery` (Task 2), `LiveSessionCodeReadPublic` policy (Task 3).

- [ ] **Step 1: Write the failing integration tests** (mirror `LiveSessionParticipantAuthzEndpointTests` fixture + `GameNight` public RSVP tests). Cover:
  1. `Anonymous_ValidCode_Returns200_NarrowBody` — no cookie → 200; body has gameName/status/players.
  2. `Anonymous_ValidCode_BodyOmitsSensitiveFields` (load-bearing) — raw JSON `Should().NotContain("createdByUserId" / "userId" / "notes" / "roundScores" / "teams" / "visibility" / "groupId")`.
  3. `UnknownCode_Returns404`.
  4. `AuthenticatedNonParticipant_SameNarrowBody` — optional-auth never broadens disclosure.
  5. `OldRoute_StillAuthenticated` — `GET /code/{code}` (no `/public`) anonymous still 401 (no-regression).

```csharp
[Fact(DisplayName = "GET /code/{code}/public returns 200 to an anonymous caller with the narrow body")]
public async Task Anonymous_ValidCode_Returns200_NarrowBody()
{
    var (code, _) = await CreateSessionWithCodeAsync(); // seeds session via IMediator, returns SessionCode
    var anon = _factory.CreateClient();

    var resp = await anon.GetAsync($"/api/v1/live-sessions/code/{code}/public");

    resp.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await resp.Content.ReadAsStringAsync();
    body.Should().NotContain("createdByUserId").And.NotContain("notes").And.NotContain("roundScores");
}
```

> Seed helper: create a session via `CreateLiveSessionCommand`, `AddPlayerToLiveSessionCommand` (linked user), set Notes/score via the relevant commands so the projection has something to (not) leak; read `SessionCode` back via `GetLiveSessionQuery`/repo. Reuse the `SeedUserAsync` + Testcontainers fixture pattern from `LiveSessionParticipantAuthzEndpointTests`.

- [ ] **Step 2: Run → FAIL** (route 404 — not wired).

- [ ] **Step 3: Wire the endpoint** in `MapLiveSessionEndpoints`, beside the existing `/code/{code}` (line ~349):

```csharp
group.MapGet("/live-sessions/code/{code}/public", HandleGetPublicSessionByCode)
    .AllowAnonymous()
    .RequireRateLimiting("LiveSessionCodeReadPublic")
    .Produces<PublicLiveSessionDto>(200)
    .Produces(404)
    .Produces(429)
    .WithTags("LiveSessions")
    .WithSummary("Get a live session lobby by join code (public, read-only)")
    .WithDescription("Public QR-code endpoint. Returns a narrow read-only lobby/scoreboard projection. Optional auth. Rate-limited 60/min per IP. Issue #2590.");
```

Handler (in the Query Handlers region):
```csharp
private static async Task<IResult> HandleGetPublicSessionByCode(
    string code,
    [FromServices] IMediator mediator,
    CancellationToken cancellationToken)
{
    var result = await mediator.Send(new GetPublicLiveSessionByCodeQuery(code), cancellationToken).ConfigureAwait(false);
    return result is null ? Results.NotFound() : Results.Ok(result);
}
```

> Route ordering: literal-segment routes are registered before `{sessionId}` routes (see the file's NOTE at ~line 312). `/code/{code}/public` is unambiguous (literal `code` + literal `public`), but register it near the other `/code/...` routes to keep the convention.

- [ ] **Step 4: Run integration → PASS.** Then run `--filter "FullyQualifiedName~LiveSession&Category=Unit"` to confirm no unit regressions.
- [ ] **Step 5: Commit** `feat(session-live): public lobby-by-code endpoint (#2590)`

---

### Task 5: Frontend — narrow schema, client method, repoint guest view

**Files:**
- Modify: `apps/web/src/lib/api/schemas/live-sessions.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/liveSessionsClient.ts`
- Modify: `apps/web/src/app/(public)/join/session/[code]/guest-session-view.tsx`
- Test: FE unit/schema test for the public client + repoint.

- [ ] **Step 1: Add the Zod schema** `PublicLiveSessionDtoSchema` (exact whitelist: id, sessionCode, gameName, gameSlug, status, players[{id, displayName, color, totalScore, currentRank, isActive}]) and a `PublicLiveSessionDto` TS type. Do NOT touch `LiveSessionDtoSchema`.

- [ ] **Step 2: Add the client method** `getPublicByCode(code: string): Promise<PublicLiveSessionDto>` calling `GET ${BASE}/code/${encodeURIComponent(code)}/public` and parsing with `PublicLiveSessionDtoSchema`. Mark with a JSDoc `// PUBLIC — anonymous-safe; narrow DTO`.

- [ ] **Step 3: Repoint `guest-session-view.tsx`** from the raw `fetch(.../code/${code})` to `api.liveSessions.getPublicByCode(code)` (typed). Adjust the local type from `LiveSessionDto` to `PublicLiveSessionDto`. The component already only reads gameName/sessionCode/status + player {id, displayName, color, totalScore, isActive} — confirm no removed field is referenced (it is not, per the spec-panel audit).

- [ ] **Step 4: FE test** — assert `getPublicByCode` hits `/code/{code}/public` and validates against the narrow schema; assert the guest view renders gameName + scoreboard from the narrow shape (mock the client). Run `pnpm test` + `pnpm typecheck` + `pnpm lint`.
- [ ] **Step 5: Commit** `feat(web): repoint guest join view to public lobby endpoint (#2590)`

---

### Task 6: Build + suite + DoD/PR

- [ ] **Step 1:** `dotnet build apps/api/src/Api/Api.csproj` (0 errors) + `dotnet test --filter "FullyQualifiedName~LiveSession&Category=Unit"` + the two new integration/FE suites.
- [ ] **Step 2:** code review (security subagent) → address findings.
- [ ] **Step 3:** PR `feature/issue-2590-public-session-lobby` → `main-dev`, `Closes #2590`. Document the deferred follow-ups (authenticated-tier narrowing; code entropy/TTL).

## Self-Review
- **Spec coverage**: AllowAnonymous (T4) + narrow DTO (T1/T2) + rate-limit (T3) + FE repoint (T5) + field-exclusion test (T4 step1 #2). Decisions honored: no visibility gate (code-as-capability), authenticated route untouched (T4 #5 regression test). ✔
- **Type consistency**: `PublicLiveSessionDto`/`PublicLiveSessionPlayerDto` (T1) consumed identically in T2/T4; `GetPublicLiveSessionByCodeQuery(string)→PublicLiveSessionDto?` consistent T2/T4. ✔
- **Placeholder scan**: rate-limit real-block values flagged "match GameNightTokenRead exactly" (must be read at impl time — the disabled-block copy is not the real shape). ✔

## Known follow-ups (document in PR)
- Authenticated `/code/{code}` still returns the full DTO to any logged-in non-participant (deferred per product decision — separate issue if desired).
- Code entropy (6 chars / 1.07e9) + no TTL: rate-limit bounds online enumeration; widening to 8 chars / adding code TTL is a separate hardening ticket.
- Optional ops follow-up (Nygard): `meepleai_live_session_code_lookup_total{result}` counter + miss-rate alert for enumeration detection.
