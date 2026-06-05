# Asse A — Semantic Alignment GameNight/Session Implementation Plan (v2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **🚨 v2 — REWRITE post-discovery 2026-06-04**: il plan v1 assumeva backend scratch ma molte entità esistono già (issue #42 GameNightEvent+GameNightRsvp, #44/#47 GameNightEmailService, #607 token-based GameNightInvitation, Notification aggregate in UserNotifications). Plan v2 è focused **solo sui gap reali**. Effort rebaseline da XL ~15gg a **M+ ~10gg** (-33%). Vedi [Gap Analysis](#gap-analysis-backend-vs-plan-v1) sotto.

**Goal:** Coprire i gap dell'asse A (DEC-1 Polymorphic ScoreType, invariante #10 max 1 live, invariante #11 Session.StartedAt, invariante #15 first-session trigger verify, invariante #13 draft+live warning, /notifications REST endpoint expand, Resend provider swap se necessario) sopra il backend esistente.

**Architecture:** EF Core migrations additive (no rebuild esistente) + DDD domain logic extension su `GameNightEvent`/`Session` aggregate esistenti + Strategy pattern nuovo per polymorphic scoring + extend notification REST/email pipeline.

**Tech Stack:** .NET 9 · ASP.NET Minimal APIs + MediatR · EF Core (pgvector) · FluentValidation · xUnit + Testcontainers · IEmailService (existing wrapper) o Resend (se swap)

**Issue**: [#1896](https://github.com/meepleAi-app/meepleai-monorepo/issues/1896) (parent umbrella [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895))
**Spec consolidato**: [`docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md`](../specs/2026-06-04-claude-design-alignment-spec-panel-review.md) (Sezione 4 — Asse A)
**Domain model**: [`docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`](../../for-developers/specs/2026-06-04-gamenight-session-domain-model.md) (20 invarianti)
**Effort target rebaseline**: M+ ~10 gg dev + 2 gg test/review = **12 gg totali** (vs v1 18 gg)

---

## Gap Analysis: backend vs plan v1

| Spec area | Stato backend reale | Plan v2 azione |
|-----------|-------------------|----------------|
| `GameNightEvent` aggregate (= GameNight nel demo) | ✅ ESISTE — Status enum 5 valori (Draft/Published/Cancelled/Completed/InProgress), CreateAdHoc factory, Complete/CompleteAdHoc/Cancel/AddInvitees/PreInvite methods, domain events wired | **SKIP** — semantic mapping nel doc |
| `GameNightRsvp` per-user | ✅ ESISTE — RsvpStatus enum (Pending/Accepted/Declined/Maybe), Accept/Decline/SetMaybe methods | **SKIP** |
| `GameNightInvitation` token-based public (email guest) | ✅ ESISTE — Issue #607 token-based + #1169 RespondedByName, idempotent Accept/Decline | **SKIP** — è gestito da flow separato |
| `GameNightSession` link entity | ✅ ESISTE — Pending/InProgress/Completed/Skipped, StartedAt/CompletedAt, WinnerId, Start()/Complete()/Skip() | **SKIP** |
| `Session` aggregate (SessionTracking) | ✅ ESISTE — CreatedAt/FinalizedAt/SessionStatus/Participants/IDomainEventSource. **MANCA: StartedAt** | **WP2 T2** add StartedAt |
| `ScoreEntry` polymorphic | ❌ Single-shape (decimal value + round + category). NON polymorphic | **WP3** NEW DEC-1 ScoreType |
| Max 1 live invariant aggregate guard | ❌ NON ESISTE | **WP1 T1** NEW |
| Invariante #15 first-session triggers Planned→InProgress | ⚠️ PARZIALE (CreateAdHoc va direttamente InProgress, ma non c'è trigger automatic da Session creation per GameNight non-AdHoc) | **WP2 T3** wire trigger |
| Invariante #13 draft+live coexistence warning | ❌ NON ESISTE | **WP2 T4** add X-Warning-Code header |
| Tagged vs Invited semantic distinction | ⚠️ MAPPING — PreInvite (Draft, no event) ≈ Tagged + Publish (Draft→Published + events) ≈ Invited | **WP2 T5** documentation + DTO labels |
| `Notification` aggregate in UserNotifications | ✅ ESISTE — entity + repository | **WP4 T11** verify schema + extend |
| `/notifications` REST endpoints (GET inbox + PATCH read) | ⚠️ DA VERIFICARE — potrebbero esistere o no | **WP4 T12** verify + add if missing |
| `GameNightEmailService` invitation email | ✅ ESISTE — IEmailService wrapper (SMTP o altro provider) | **WP4 T13** verify Resend swap necessità |
| `RESEND_API_KEY` secret + ResendEmailSender | ❓ DA VERIFICARE provider attuale | **WP4 T13** if not Resend, swap |

**Plan v1 → v2 simplification**:
- WP1 v1 (5 migration tasks): → **WP1 v2 (1 task)** solo per max 1 live invariant. Altre migration **non necessarie** (entità esistono).
- WP2 v1 (Session invarianti, 4 task): → **WP2 v2 (4 task)** focused su Session.StartedAt + invariante #15 wiring + warning header + tagged/invited mapping.
- WP3 v1 (GameNight state machine, 3 task): **MERGED in WP2 v2** (semantic mapping doc + invariante #15 wire). Niente new entity.
- WP4 v1 (max 1 live): **PROMOSSA WP1 v2** (è il vero gap critical).
- WP5 v1 (ScoreType polimorfico, 5 task): → **WP3 v2 (5 task)** invariata, è il core nuovo lavoro.
- WP6 v1 (Notification, 4 task): → **WP4 v2 (3 task)** focused su /notifications endpoint expand + Resend swap se necessario. Entity ESISTE già.
- WP7 v1 (OpenAPI): → **WP5 v2 (1 task)** standard.

**Total tasks**: v1 25 → **v2 14**. Effort 18gg → 12gg.

---

## Work Packages v2

| WP | Scope | Effort | Critical path | Sub-task |
|----|-------|--------|---------------|----------|
| **WP1** | Max 1 live aggregate guard (invariante #10) | M | YES | T1 |
| **WP2** | Session.StartedAt + invariante #15 wire + warning header + semantic mapping doc | M | YES | T2–T5 |
| **WP3** | Polymorphic ScoreType (DEC-1) — 4 strategies + factory + migration + DTO + integration | L | NO (parallelo WP1+WP2) | T6–T10 |
| **WP4** | Notification system extension — /notifications endpoints + Resend swap verify | M | NO (parallelo WP3) | T11–T13 |
| **WP5** | OpenAPI + acceptance | S | YES (chiude WP) | T14 |

**Mix-model hint (P120)**: 5 haiku (mechanical) + 9 sonnet (judgment / domain logic / strategy design).

**Total**: 14 task TDD bite-sized. ~12 gg effort realistic.

---

## File Structure

### New files
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/ScoreType.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/IScoringStrategy.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/PointsScoringStrategy.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/BinaryWinScoringStrategy.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/ObjectivesScoringStrategy.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/RankingScoringStrategy.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/ScoringStrategyFactory.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/MaxLiveSessionsExceededException.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionStartedDomainEvent.cs`
- `apps/api/src/Api/Infrastructure/Migrations/YYYYMMDD_AddSessionStartedAt.cs`
- `apps/api/src/Api/Infrastructure/Migrations/YYYYMMDD_AddSessionScoringType.cs`
- `apps/api/src/Api/BoundedContexts/UserNotifications/Routing/NotificationEndpoints.cs` (if missing)
- Test mirrors in `apps/api/tests/Api.Tests/`

### Modified files
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs` (add StartedAt + OpenLiveMode factory)
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs` (add OpenLiveSession aggregate guard for invariante #10 + HandleFirstSessionStarted for invariante #15)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/` (new handler: SessionStarted → GameNight.HandleFirstSessionStarted)
- `apps/api/src/Api/BoundedContexts/SessionTracking/Application/SaveSession/` (X-Warning-Code header)
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/ScoreEntry.cs` (or new model — polymorphic via ScoreType)
- `apps/api/src/Api/BoundedContexts/UserNotifications/` (verify + extend if missing)
- `apps/api/src/Api/Infrastructure/Services/` (verify IEmailService concrete = Resend? if not, swap)
- `apps/api/src/Api/openapi.yaml` (new error codes, ScoreType DTO, X-Warning-Code, /notifications expand)
- `docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md` (mapping doc: backend term ↔ demo term)
- `CLAUDE.md` (Domain Model section update)

---

## WP1 — Max 1 live aggregate guard (invariante #10)

### Task 1: MaxLiveSessionsExceededException + aggregate guard

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/MaxLiveSessionsExceededException.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs` (add OpenLiveSession method)
- Test: `apps/api/tests/Api.Tests/Unit/GameManagement/Domain/GameNightEventMaxLiveTests.cs`
- Test: `apps/api/tests/Api.Tests/Integration/GameManagement/OpenLiveSessionEndpointTests.cs`

> Backend reference: `GameNightEvent` aggregate root has `Sessions` accessor (Collection of GameNightSession). Each `GameNightSession.Status` = Pending/InProgress/Completed/Skipped. Live = `Status == InProgress && CompletedAt == null`. **Invariante #10**: a GameNightEvent can have at most 1 GameNightSession in InProgress at a time.

- [ ] **Step 1: Write failing unit test**

```csharp
public class GameNightEventMaxLiveTests
{
    [Fact]
    public void OpenLiveSession_WhenAnotherLiveActive_ThrowsMaxLiveSessionsExceededException()
    {
        var gn = GameNightEvent.CreateAdHoc(organizerId: Guid.NewGuid(), title: "Test", firstGameId: Guid.NewGuid());
        var firstSession = gn.AttachSession(sessionId: Guid.NewGuid(), gameId: Guid.NewGuid(), gameTitle: "Wingspan", playOrder: 1);
        firstSession.Start();
        var secondSession = gn.AttachSession(sessionId: Guid.NewGuid(), gameId: Guid.NewGuid(), gameTitle: "Catan", playOrder: 2);

        var act = () => gn.OpenLiveSession(secondSession.Id);

        act.Should().Throw<MaxLiveSessionsExceededException>()
            .Where(ex => ex.ErrorCode == "MAX_LIVE_SESSIONS_EXCEEDED")
            .Where(ex => ex.GameNightEventId == gn.Id);
    }

    [Fact]
    public void OpenLiveSession_WithoutOtherLive_Succeeds()
    {
        var gn = GameNightEvent.CreateAdHoc(Guid.NewGuid(), "Test", Guid.NewGuid());
        var session = gn.AttachSession(Guid.NewGuid(), Guid.NewGuid(), "Wingspan", 1);

        var act = () => gn.OpenLiveSession(session.Id);

        act.Should().NotThrow();
    }
}
```

- [ ] **Step 2: Run → FAIL** (exception class non esiste, no guard logic)

- [ ] **Step 3: Implement exception**

```csharp
// MaxLiveSessionsExceededException.cs
namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

public class MaxLiveSessionsExceededException : DomainException
{
    public Guid GameNightEventId { get; }
    public override string ErrorCode => "MAX_LIVE_SESSIONS_EXCEEDED";

    public MaxLiveSessionsExceededException(Guid gameNightEventId)
        : base($"GameNightEvent {gameNightEventId} already has an active live session.")
    {
        GameNightEventId = gameNightEventId;
    }
}
```

- [ ] **Step 4: Add GameNightEvent.OpenLiveSession() aggregate method**

```csharp
// GameNightEvent.cs (add method)
public void OpenLiveSession(Guid gameNightSessionId)
{
    ThrowIfCorrupted();

    var alreadyLive = Sessions.Any(s =>
        s.Status == GameNightSessionStatus.InProgress);
    if (alreadyLive)
        throw new MaxLiveSessionsExceededException(Id);

    var session = Sessions.FirstOrDefault(s => s.Id == gameNightSessionId)
        ?? throw new InvalidOperationException($"GameNightSession {gameNightSessionId} not found in GameNightEvent {Id}");
    session.Start();
}
```

> Note: `GameNightSession.Start()` rimane internal. Esposto via `GameNightEvent.OpenLiveSession()` aggregate method che applica guard.

- [ ] **Step 5: Run unit test → PASS**

- [ ] **Step 6: Write failing integration test for HTTP 409**

```csharp
[Fact]
public async Task POST_OpenLiveSession_With_ExistingLive_Returns409_WithErrorCode()
{
    using var app = await TestApp.CreateAsync();
    var gn = await app.SeedGameNightWithSessionAsync();
    await app.OpenLiveAsync(gn.Id, sessionId: gn.Sessions[0].Id);
    var secondSession = await app.AddSecondSessionAsync(gn.Id);

    var response = await app.Client.PostAsync(
        $"/api/v1/game-nights/{gn.Id}/sessions/{secondSession.Id}/live", null);

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    var body = await response.Content.ReadFromJsonAsync<ErrorDto>();
    body!.Code.Should().Be("MAX_LIVE_SESSIONS_EXCEEDED");
}
```

- [ ] **Step 7: Add endpoint + middleware mapping**

```csharp
// GameNightEndpoints.cs (or existing routing file)
app.MapPost("/api/v1/game-nights/{gnId:guid}/sessions/{sId:guid}/live",
    async (Guid gnId, Guid sId, IMediator m) =>
        Results.Ok(await m.Send(new OpenLiveSessionCommand(gnId, sId))));

// DomainExceptionMiddleware.cs (add catch)
catch (MaxLiveSessionsExceededException ex)
{
    context.Response.StatusCode = StatusCodes.Status409Conflict;
    await context.Response.WriteAsJsonAsync(new ErrorDto(ex.ErrorCode, ex.Message));
}
```

- [ ] **Step 8: Run integration test → PASS**

- [ ] **Step 9: Commit**

```bash
git commit -m "feat(game-management): #1896 invariante #10 max 1 live aggregate guard"
```

**Self-review**:
- [ ] Exception in Domain layer (not Application)
- [ ] Aggregate method `OpenLiveSession()` invece di esponendo Start() pubblicamente
- [ ] HTTP 409 mapping via middleware DomainException pattern
- [ ] No migration needed (no new columns, query-time check)

---

## WP2 — Session.StartedAt + invariante #15 + warning header + semantic mapping

### Task 2: Session.StartedAt + OpenLiveMode factory (invariante #11 + #14)

**Mix-model**: sonnet · **Effort**: M (~6h)

> Backend reference: `Session` aggregate in SessionTracking ha `CreatedAt` (DateTime UTC default), `FinalizedAt` (Complete equivalent), `SessionStatus`. **MANCA: StartedAt**. Plan demo dice 3 timestamp distinti.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs` (add StartedAt + OpenLiveMode)
- Create: `apps/api/src/Api/Infrastructure/Migrations/YYYYMMDD_AddSessionStartedAt.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionStartedDomainEvent.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/Domain/SessionStartedAtTests.cs`

- [ ] **Step 1: Write failing unit test**

```csharp
public class SessionStartedAtTests
{
    [Fact]
    public void NewSession_HasNullStartedAt_NotInLiveMode()
    {
        var s = Session.Create(userId: Guid.NewGuid(), gameId: Guid.NewGuid(),
            sessionType: SessionType.Generic);
        s.StartedAt.Should().BeNull();
        s.IsLive.Should().BeFalse();
    }

    [Fact]
    public void OpenLiveMode_SetsStartedAt_RaisesDomainEvent()
    {
        var s = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        var beforeNow = DateTime.UtcNow;

        s.OpenLiveMode();

        s.StartedAt.Should().NotBeNull();
        s.StartedAt!.Value.Should().BeOnOrAfter(beforeNow);
        s.IsLive.Should().BeTrue();
        s.DomainEvents.OfType<SessionStartedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void OpenLiveMode_OnAlreadyLive_Throws()
    {
        var s = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        s.OpenLiveMode();

        var act = () => s.OpenLiveMode();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OpenLiveMode_OnFinalized_Throws()
    {
        var s = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        // assuming Finalize method exists (FinalizedAt)
        s.Finalize();

        var act = () => s.OpenLiveMode();
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Add migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddSessionStartedAt --output-dir Infrastructure/Migrations
```

```csharp
public partial class AddSessionStartedAt : Migration
{
    protected override void Up(MigrationBuilder b) =>
        b.AddColumn<DateTime?>("started_at", "sessions", nullable: true);

    protected override void Down(MigrationBuilder b) =>
        b.DropColumn("started_at", "sessions");
}
```

- [ ] **Step 4: Update Session entity**

```csharp
// Session.cs (add)
public DateTime? StartedAt { get; private set; }
public bool IsLive => StartedAt.HasValue && FinalizedAt is null;

public void OpenLiveMode()
{
    if (IsLive)
        throw new InvalidOperationException(
            $"Session {Id} is already in live mode (StartedAt={StartedAt}).");
    if (FinalizedAt.HasValue)
        throw new InvalidOperationException(
            $"Session {Id} is already finalized. Cannot open live mode.");

    StartedAt = DateTime.UtcNow;
    AddDomainEvent(new SessionStartedDomainEvent(Id, UserId, GameId, StartedAt.Value));
}
```

```csharp
// SessionStartedDomainEvent.cs
public record SessionStartedDomainEvent(
    Guid SessionId,
    Guid UserId,
    Guid GameId,
    DateTime StartedAt) : IDomainEvent;
```

- [ ] **Step 5: Update ApplicationDbContext Session mapping**

```csharp
// ApplicationDbContext.cs OnModelCreating Session config
b.Property(e => e.StartedAt).HasColumnName("started_at");
```

- [ ] **Step 6: Run tests → PASS**

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(session-tracking): #1896 Session.StartedAt + OpenLiveMode (invariante #11 + #14)"
```

**Self-review**:
- [ ] StartedAt nullable, valorizzato SOLO via OpenLiveMode (invariante #14 derived)
- [ ] CreatedAt esistente preserved
- [ ] FinalizedAt esistente = CompletedAt equivalent
- [ ] Domain event raised (consistente con IDomainEventSource pattern già usato)
- [ ] Migration additive (no breaking change)

---

### Task 3: Invariante #15 wire — SessionStarted triggers GameNight Planned→InProgress

**Mix-model**: sonnet · **Effort**: M (~5h)

> Backend reference: `GameNightEvent.CreateAdHoc()` va direttamente a InProgress (skip Draft+Published). Per non-AdHoc flow (Draft → Published), invariante #15 demo dice "first Session creates → Planned should become InProgress". Mapping: "Planned" demo = Published backend (post-RSVP). Quindi quando una Session sotto Published GameNight viene started, dovrebbe transition a InProgress.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/SessionStartedHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs` (add HandleFirstSessionStarted method)
- Test: `apps/api/tests/Api.Tests/Unit/GameManagement/Domain/GameNightEventInvariant15Tests.cs`
- Test: `apps/api/tests/Api.Tests/Integration/GameManagement/Invariant15IntegrationTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
public class GameNightEventInvariant15Tests
{
    [Fact]
    public void HandleFirstSessionStarted_OnPublished_TransitionsToInProgress_Invariant15()
    {
        var gn = GameNightEvent.Create(organizerId: Guid.NewGuid(), title: "Test", scheduledAt: DateTimeOffset.UtcNow.AddDays(1));
        gn.Publish(invitedUserIds: new List<Guid> { Guid.NewGuid() });
        // Status = Published

        gn.HandleFirstSessionStarted(sessionId: Guid.NewGuid());

        gn.Status.Should().Be(GameNightStatus.InProgress);
    }

    [Fact]
    public void HandleFirstSessionStarted_OnDraft_Throws_InvalidOperationException()
    {
        var gn = GameNightEvent.Create(Guid.NewGuid(), "Test", DateTimeOffset.UtcNow.AddDays(1));
        // Status = Draft
        var act = () => gn.HandleFirstSessionStarted(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HandleFirstSessionStarted_OnInProgress_IsIdempotent_NoStateChange()
    {
        var gn = GameNightEvent.CreateAdHoc(Guid.NewGuid(), "Test", Guid.NewGuid());
        // Status = InProgress already (AdHoc)
        gn.HandleFirstSessionStarted(Guid.NewGuid());
        gn.Status.Should().Be(GameNightStatus.InProgress);
    }
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement HandleFirstSessionStarted on GameNightEvent**

```csharp
// GameNightEvent.cs (add)
public void HandleFirstSessionStarted(Guid sessionId)
{
    ThrowIfCorrupted();

    if (Status == GameNightStatus.Published)
    {
        Status = GameNightStatus.InProgress;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    // Idempotent: InProgress → stay InProgress (already AdHoc or already started)
    else if (Status == GameNightStatus.InProgress)
    {
        return;
    }
    else
    {
        throw new InvalidOperationException(
            $"Cannot start session: GameNightEvent {Id} is in {Status} status (invariante #15 requires Published or InProgress).");
    }
}
```

- [ ] **Step 4: Wire MediatR INotificationHandler**

```csharp
// SessionStartedHandler.cs in GameManagement.Application.EventHandlers
public class SessionStartedHandler : INotificationHandler<SessionStartedDomainEvent>
{
    private readonly IGameNightEventRepository _gnRepo;
    private readonly IGameNightSessionRepository _gnsRepo;

    public SessionStartedHandler(IGameNightEventRepository repo, IGameNightSessionRepository gnsRepo)
    {
        _gnRepo = repo;
        _gnsRepo = gnsRepo;
    }

    public async Task Handle(SessionStartedDomainEvent evt, CancellationToken ct)
    {
        // SessionTracking Session is started → find parent GameNightEvent via GameNightSession link
        var link = await _gnsRepo.FindBySessionIdAsync(evt.SessionId, ct);
        if (link is null) return; // standalone session, no GN parent

        var gn = await _gnRepo.GetByIdAsync(link.GameNightEventId, ct)
            ?? throw new NotFoundException($"GameNightEvent {link.GameNightEventId}");
        gn.HandleFirstSessionStarted(evt.SessionId);
        await _gnRepo.SaveAsync(gn, ct);
    }
}
```

- [ ] **Step 5: Run tests + integration test (end-to-end via mediator pipeline) → PASS**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(game-management): #1896 invariante #15 wire — SessionStartedHandler transitions GameNight to InProgress"
```

---

### Task 4: SaveSessionCommand + X-Warning-Code header (invariante #13)

**Mix-model**: sonnet · **Effort**: M (~5h)

> Demo invariante #13: salvare draft con live attiva è permesso ma backend ritorna `X-Warning-Code: SAVED_WHILE_LIVE_ACTIVE` header per frontend toast.

**Files:**
- Modify: existing SaveSessionCommandHandler in SessionTracking/Application/SaveSession
- Modify: endpoint mapper to translate result to HTTP header
- Test: `apps/api/tests/Api.Tests/Integration/SessionTracking/SaveSessionWarningHeaderTests.cs`

- [ ] **Step 1: Write failing integration test**

```csharp
[Fact]
public async Task SaveSession_WithActiveLive_Returns200_WithWarningHeader()
{
    using var app = await TestApp.CreateAsync();
    var gn = await app.SeedGameNightEventAsync();
    var liveSession = await app.OpenLiveSessionAsync(gn.Id);
    var draft = await app.CreateDraftSessionAsync(gn.Id);

    var response = await app.Client.PutAsJsonAsync(
        $"/api/v1/sessions/{draft.Id}/save",
        new SaveSessionRequest { /* polymorphic scoring later in WP3 */ });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    response.Headers.GetValues("X-Warning-Code").Should().ContainSingle("SAVED_WHILE_LIVE_ACTIVE");
}

[Fact]
public async Task SaveSession_WithoutActiveLive_Returns200_NoWarningHeader()
{
    using var app = await TestApp.CreateAsync();
    var gn = await app.SeedGameNightEventAsync();
    var draft = await app.CreateDraftSessionAsync(gn.Id);

    var response = await app.Client.PutAsJsonAsync(
        $"/api/v1/sessions/{draft.Id}/save",
        new SaveSessionRequest { });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    response.Headers.Contains("X-Warning-Code").Should().BeFalse();
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Update handler to return result + flag**

```csharp
public record SaveSessionResult(SessionDto Session, bool LiveActiveWarning);

public async Task<SaveSessionResult> Handle(SaveSessionCommand cmd, CancellationToken ct)
{
    var session = await _db.Sessions.FindAsync(new object?[] { cmd.SessionId }, ct)
        ?? throw new NotFoundException($"Session {cmd.SessionId}");
    session.Finalize(); // existing Complete-equivalent method

    // Check sibling live via GameNightSession link
    bool liveActive = await CheckSiblingLiveAsync(session, ct);

    await _db.SaveChangesAsync(ct);
    return new SaveSessionResult(SessionDto.FromEntity(session), liveActive);
}

private async Task<bool> CheckSiblingLiveAsync(Session session, CancellationToken ct)
{
    var link = await _db.GameNightSessions.FirstOrDefaultAsync(
        l => l.SessionId == session.Id, ct);
    if (link is null) return false;
    return await _db.GameNightSessions.AnyAsync(s =>
        s.GameNightEventId == link.GameNightEventId &&
        s.Status == GameNightSessionStatus.InProgress &&
        s.SessionId != session.Id, ct);
}
```

- [ ] **Step 4: Update endpoint to set X-Warning-Code header**

```csharp
app.MapPut("/api/v1/sessions/{id:guid}/save", async (
    Guid id, SaveSessionRequest req, IMediator m, HttpResponse response) =>
{
    var result = await m.Send(new SaveSessionCommand(id, /* fields */));
    if (result.LiveActiveWarning)
        response.Headers.Append("X-Warning-Code", "SAVED_WHILE_LIVE_ACTIVE");
    return Results.Ok(result.Session);
});
```

- [ ] **Step 5: Run → PASS**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(session-tracking): #1896 SaveSession X-Warning-Code header (invariante #13)"
```

---

### Task 5: Semantic mapping doc — backend term ↔ demo term

**Mix-model**: haiku · **Effort**: S (~3h)

**Files:**
- Modify: `docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md` (add Section "Backend Mapping")
- Modify: `CLAUDE.md` (Domain Model section update)

- [ ] **Step 1**: Add mapping table to domain model spec

| Demo term | Backend term | Note |
|-----------|--------------|------|
| GameNight (Planned) | GameNightEvent (Published) | After Publish() call sends invitations |
| GameNight (Planned, ad-hoc) | GameNightEvent (CreateAdHoc factory) | Skip RSVP, direct InProgress |
| GameNight (InProgress) | GameNightEvent.Status = InProgress | Via HandleFirstSessionStarted or CreateAdHoc |
| GameNight (Completed) | GameNightEvent.Status = Completed | Via Complete() or CompleteAdHoc() |
| Session (created) | Session aggregate (CreatedAt valorized, StartedAt null) | Draft mode equivalent |
| Session (live) | Session.IsLive (StartedAt != null && FinalizedAt == null) | Via OpenLiveMode() |
| Session (completed) | Session (FinalizedAt valorized) | Via Finalize() |
| Player tagged (no notification) | GameNightEvent.PreInvite() | Status=Draft, no events |
| Player invited (notification sent) | GameNightEvent.Publish() | Status=Draft→Published, raises GameNightPublishedEvent → triggers email |
| GameNightPlayer.RsvpStatus.Pending | GameNightRsvp.Status = Pending | Default after Publish |
| GameNightPlayer.RsvpStatus.Confirmed | GameNightRsvp.Accept() → Status = Accepted | |

- [ ] **Step 2**: Update CLAUDE.md Domain Model section linking to mapping

- [ ] **Step 3**: Commit

```bash
git commit -m "docs(domain-model): #1896 backend ↔ demo semantic mapping doc (asse A WP2 T5)"
```

---

## WP3 — Polymorphic ScoreType (DEC-1)

> **Spec reference**: Sezione 4 Asse A — "DEC-1 Polymorphic ScoreType".
> Backend reference: `ScoreEntry` esiste ma è single-shape (decimal value + round + category). Per polymorphic gaming (cooperativo BinaryWin, Objectives, Ranking) serve nuova astrazione.

### Task 6: ScoreType enum + IScoringStrategy interface + factory

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/ScoreType.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/IScoringStrategy.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/ScoringStrategyFactory.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/Scoring/ScoringStrategyFactoryTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Theory]
[InlineData(ScoreType.Points, typeof(PointsScoringStrategy))]
[InlineData(ScoreType.BinaryWin, typeof(BinaryWinScoringStrategy))]
[InlineData(ScoreType.Objectives, typeof(ObjectivesScoringStrategy))]
[InlineData(ScoreType.Ranking, typeof(RankingScoringStrategy))]
public void GetStrategy_ReturnsExpectedStrategyForEachScoreType(
    ScoreType type, Type expectedStrategyType)
{
    var factory = new ScoringStrategyFactory();
    var strategy = factory.GetStrategy(type);
    strategy.Should().BeOfType(expectedStrategyType);
}

[Fact]
public void GetStrategy_OnUnknownType_Throws()
{
    var factory = new ScoringStrategyFactory();
    var act = () => factory.GetStrategy((ScoreType)999);
    act.Should().Throw<ArgumentOutOfRangeException>();
}
```

- [ ] **Step 2: Run → FAIL** (types don't exist)

- [ ] **Step 3: Create types**

```csharp
// ScoreType.cs
public enum ScoreType
{
    [Description("Punti numerici per player")]
    Points = 0,
    [Description("Winner/Loser binario (cooperativo)")]
    BinaryWin = 1,
    [Description("Obiettivi completati per player")]
    Objectives = 2,
    [Description("Posizione 1..N per player")]
    Ranking = 3,
}

// IScoringStrategy.cs
public interface IScoringStrategy
{
    ScoreType Type { get; }
    ValidationResult Validate(string scoreDataJson);
    string Serialize(object scoreData);
    object Deserialize(string scoreDataJson);
    Guid? ComputeWinnerPlayerId(string scoreDataJson);
}

// Stub placeholders for 4 strategies (filled in T7-T8)
public class PointsScoringStrategy : IScoringStrategy
{
    public ScoreType Type => ScoreType.Points;
    public ValidationResult Validate(string s) => throw new NotImplementedException();
    public string Serialize(object d) => throw new NotImplementedException();
    public object Deserialize(string s) => throw new NotImplementedException();
    public Guid? ComputeWinnerPlayerId(string s) => throw new NotImplementedException();
}
// Same stubs for BinaryWin/Objectives/Ranking

// ScoringStrategyFactory.cs
public class ScoringStrategyFactory
{
    public IScoringStrategy GetStrategy(ScoreType type) => type switch
    {
        ScoreType.Points => new PointsScoringStrategy(),
        ScoreType.BinaryWin => new BinaryWinScoringStrategy(),
        ScoreType.Objectives => new ObjectivesScoringStrategy(),
        ScoreType.Ranking => new RankingScoringStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
```

- [ ] **Step 4: Run → PASS** (factory dispatch works, strategies stubbed)

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(session-tracking): #1896 ScoreType enum + IScoringStrategy + factory (DEC-1 skeleton)"
```

---

### Task 7: PointsScoringStrategy + BinaryWinScoringStrategy

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/PointsScoringStrategy.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/BinaryWinScoringStrategy.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/Scoring/PointsScoringStrategyTests.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/Scoring/BinaryWinScoringStrategyTests.cs`

- [ ] **Step 1: Write failing tests for Points (5 cases)**

```csharp
public class PointsScoringStrategyTests
{
    private readonly PointsScoringStrategy _sut = new();

    [Fact]
    public void Validate_ValidJson_ReturnsValid()
    {
        var json = """{"scores":[{"playerId":"a1","points":42},{"playerId":"b2","points":30}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NegativeScore_ReturnsInvalid()
    {
        var json = """{"scores":[{"playerId":"a1","points":-5}]}""";
        var result = _sut.Validate(json);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("non-negative"));
    }

    [Fact]
    public void Validate_DuplicatePlayer_ReturnsInvalid() { /* ... */ }

    [Fact]
    public void ComputeWinnerPlayerId_ReturnsHighestScorePlayer() { /* ... */ }

    [Fact]
    public void Serialize_Deserialize_RoundTrip() { /* ... */ }
}
```

- [ ] **Step 2: Write failing tests for BinaryWin (5 cases similar pattern)**

- [ ] **Step 3: Run → FAIL**

- [ ] **Step 4: Implement Points strategy**

```csharp
public record PointsScoreData(PlayerScore[] Scores);
public record PlayerScore(Guid PlayerId, int Points);

public class PointsScoringStrategy : IScoringStrategy
{
    public ScoreType Type => ScoreType.Points;

    public ValidationResult Validate(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<PointsScoreData>(json);
            if (data?.Scores is null || data.Scores.Length == 0)
                return ValidationResult.Failed("No scores provided");
            var errors = new List<string>();
            if (data.Scores.Any(s => s.Points < 0)) errors.Add("Points must be non-negative");
            var grouped = data.Scores.GroupBy(s => s.PlayerId);
            if (grouped.Any(g => g.Count() > 1)) errors.Add("Duplicate playerId");
            return errors.Any() ? ValidationResult.Failed(errors) : ValidationResult.Valid();
        }
        catch (JsonException ex) { return ValidationResult.Failed($"Invalid JSON: {ex.Message}"); }
    }

    public string Serialize(object data) => JsonSerializer.Serialize((PointsScoreData)data);
    public object Deserialize(string json) => JsonSerializer.Deserialize<PointsScoreData>(json)!;

    public Guid? ComputeWinnerPlayerId(string json)
    {
        var data = (PointsScoreData)Deserialize(json);
        return data.Scores.OrderByDescending(s => s.Points).FirstOrDefault()?.PlayerId;
    }
}
```

- [ ] **Step 5: Implement BinaryWin strategy** (similar pattern, records `BinaryWinScoreData(BinaryPlayerResult[] Results)`, `BinaryPlayerResult(Guid PlayerId, bool IsWinner)`)

- [ ] **Step 6: Run → PASS**

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(session-tracking): #1896 Points + BinaryWin scoring strategies (DEC-1)"
```

---

### Task 8: ObjectivesScoringStrategy + RankingScoringStrategy

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/ObjectivesScoringStrategy.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Scoring/RankingScoringStrategy.cs`
- Test: corresponding test classes

- [ ] **Same pattern as Task 7**: 5 test cases per strategy.

**Objectives logic**: each player has list of completed objectives (string names). Winner = player with most objectives. Ties = null winner.

**Ranking logic**: each player has integer position (1..N). Winner = position 1. Validate distinct positions, sequential 1..N.

- [ ] **Commit**

```bash
git commit -m "feat(session-tracking): #1896 Objectives + Ranking scoring strategies (DEC-1)"
```

---

### Task 9: Session.ScoringType + score_data JSONB migration + SetScores domain method

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/YYYYMMDD_AddSessionScoringType.cs`
- Modify: `Session.cs` (add ScoringType + ScoreData fields + SetScores method)
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Events/SessionScoresUpdatedEvent.cs`
- Test: `apps/api/tests/Api.Tests/Unit/SessionTracking/Domain/SessionSetScoresTests.cs`

- [ ] **Step 1: Add migration**

```csharp
public partial class AddSessionScoringType : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        b.AddColumn<string>("scoring_type", "sessions",
            maxLength: 20, nullable: false, defaultValue: "Points");
        b.AddColumn<string>("score_data", "sessions",
            type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb");
    }

    protected override void Down(MigrationBuilder b)
    {
        b.DropColumn("score_data", "sessions");
        b.DropColumn("scoring_type", "sessions");
    }
}
```

- [ ] **Step 2: Update Session entity**

```csharp
public ScoreType ScoringType { get; private set; } = ScoreType.Points;
public string ScoreData { get; private set; } = "{}";

public void SetScores(ScoreType scoringType, string scoreData)
{
    ScoringType = scoringType;
    ScoreData = scoreData;
    AddDomainEvent(new SessionScoresUpdatedEvent(Id, scoringType, scoreData));
}
```

- [ ] **Step 3: Update DbContext mapping**

```csharp
b.Property(e => e.ScoringType)
    .HasColumnName("scoring_type")
    .HasMaxLength(20)
    .HasConversion<string>();
b.Property(e => e.ScoreData)
    .HasColumnName("score_data")
    .HasColumnType("jsonb");
```

- [ ] **Step 4: Write + run unit tests → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(session-tracking): #1896 Session.ScoringType + score_data JSONB + SetScores (DEC-1)"
```

---

### Task 10: SaveSessionCommand polymorphic + FluentValidation + integration round-trip

**Mix-model**: sonnet · **Effort**: L (~8h)

**Files:**
- Modify: SaveSessionCommand DTO (add ScoringType + ScoreData fields)
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/SaveSession/SaveSessionCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/Integration/SessionTracking/PolymorphicScoringRoundTripTests.cs`

- [ ] **Step 1: Write failing integration test (4 ScoreType round-trip via API)**

```csharp
[Theory]
[InlineData("Points", """{"scores":[{"playerId":"00000000-0000-0000-0000-000000000001","points":50}]}""")]
[InlineData("BinaryWin", """{"results":[{"playerId":"00000000-0000-0000-0000-000000000001","isWinner":true}]}""")]
[InlineData("Objectives", """{"completedByPlayer":[{"playerId":"00000000-0000-0000-0000-000000000001","objectives":["A","B"]}]}""")]
[InlineData("Ranking", """{"positions":[{"playerId":"00000000-0000-0000-0000-000000000001","position":1}]}""")]
public async Task SaveSession_WithPolymorphicScoring_RoundTripViaAPI(string scoringType, string scoreData)
{
    using var app = await TestApp.CreateAsync();
    var draft = await app.CreateDraftSessionAsync();

    var response = await app.Client.PutAsJsonAsync(
        $"/api/v1/sessions/{draft.Id}/save",
        new { scoringType, scoreData });

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var fetched = await app.Client.GetFromJsonAsync<SessionDto>($"/api/v1/sessions/{draft.Id}");
    fetched!.ScoringType.Should().Be(scoringType);
    fetched.ScoreData.Should().Be(scoreData);
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement FluentValidation custom rule**

```csharp
public class SaveSessionCommandValidator : AbstractValidator<SaveSessionCommand>
{
    public SaveSessionCommandValidator(ScoringStrategyFactory factory)
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ScoreData)
            .NotEmpty()
            .Custom((scoreData, context) =>
            {
                var cmd = context.InstanceToValidate;
                var strategy = factory.GetStrategy(cmd.ScoringType);
                var result = strategy.Validate(scoreData);
                if (!result.IsValid)
                    foreach (var err in result.Errors)
                        context.AddFailure("ScoreData", err);
            });
    }
}
```

- [ ] **Step 4: Update handler to call Session.SetScores**

- [ ] **Step 5: Run → PASS**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(session-tracking): #1896 SaveSession polymorphic + 4 ScoreType round-trip (DEC-1)"
```

---

## WP4 — Notification system extension (DEC-5)

> **🚨 AUDIT 2026-06-04**: WP4 (T11+T12+T13) è **GIÀ INTERAMENTE IMPLEMENTATO upstream**. Plan v2 ipotizzava endpoint mancanti — discovery rivela altro.
>
> **Stato reale**:
> - **T11/T12**: Issue #2053 ha shipped `NotificationEndpoints.cs` con TUTTI gli endpoint richiesti (GET /notifications + GET /unread-count + POST /{id}/mark-read + POST /mark-all-read + GET /stream SSE Issue #5005). Notification entity completa (MarkAsRead idempotente + RestoreReadStatus per persistence). INotificationRepository con GetByUserIdAsync (filtering unread+limit) + GetUnreadCountAsync + MarkAllAsReadAsync. CQRS handlers tutti esistenti: GetNotificationsQuery, GetUnreadCountQuery, MarkNotificationReadCommand, MarkAllNotificationsReadCommand. Rate limiting su mark-all-read (Issue #2155). Auth user-scoped (no IDOR). Registrato in Program.cs:887.
> - **T13**: Issue #1629 ha shipped `ResendEmailSender : IEmailSender` in `apps/api/src/Api/Services/Email/`. `EmailService` centralizza la scelta SMTP vs Resend via `IEmailSender` DI dependency. Config supporta `RESEND_FROM_EMAIL` fallback. SMTP path mantenuto per legacy/unit-test fallback.
>
> **Azione plan v2 → v2.1**: WP4 T11+T12+T13 → **NO IMPLEMENTATION needed**. Solo audit commit + skip-to-T14.
>
> **Effort recovered**: 12h (3+5+4) → ~0h. Asse A v2 totale ricalcolato: 12gg → **~10.5gg** (-12.5%).

### Task 11: Verify Notification entity + repository + endpoint audit

**Mix-model**: haiku · **Effort**: S (~3h)

**Files:**
- Read existing: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/Notification.cs`
- Read existing: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Repositories/INotificationRepository.cs`
- Audit: `apps/api/src/Api/BoundedContexts/UserNotifications/` for routing/application layers

- [ ] **Step 1**: Open both Domain files + understand existing schema (fields, constructors, methods)
- [ ] **Step 2**: Grep `/notifications` in `apps/api/src/Api/Routing/` and `apps/api/src/Api/BoundedContexts/UserNotifications/Routing/` to check if endpoint registered
- [ ] **Step 3**: Document audit findings:
  - **Branch A**: Endpoints exist → SKIP T12, jump to T13
  - **Branch B**: Endpoints missing → proceed to T12
- [ ] **Step 4**: Commit audit summary as docs commit

```bash
git commit -m "docs(user-notifications): #1896 audit existing Notification entity + endpoint status (asse A WP4 T11)"
```

---

### Task 12: /notifications endpoints (GET inbox + PATCH read + mark-all-read)

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files** (if needed based on T11 audit):
- Create or extend: `apps/api/src/Api/BoundedContexts/UserNotifications/Routing/NotificationEndpoints.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/GetNotifications/GetNotificationsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/MarkAsRead/MarkAsReadCommand.cs`
- Test: `apps/api/tests/Api.Tests/Integration/UserNotifications/NotificationEndpointsTests.cs`

> Endpoint contract:
> - `GET /api/v1/notifications?page=N&size=M` → `PagedResult<NotificationDto>`
> - `PATCH /api/v1/notifications/{id}/read` → `204 No Content`
> - `POST /api/v1/notifications/mark-all-read` → `{ markedCount }`

- [ ] **Step 1: Write failing integration tests for the 3 endpoints**
- [ ] **Step 2: Run → FAIL** (or skip se already exist per T11)
- [ ] **Step 3: Implement CQRS handlers + endpoints**
  - Standard CQRS pattern via MediatR + EF Core query
  - Auth filter: user can only see/modify own notifications
  - Pagination: `Skip(page * size).Take(size)` con `[Required] [Range(1, 100)] int size`
- [ ] **Step 4: Run → PASS**
- [ ] **Step 5: Commit**

```bash
git commit -m "feat(user-notifications): #1896 /notifications endpoints — GET inbox + PATCH read (DEC-5)"
```

---

### Task 13: Email provider audit — verify Resend swap necessity

**Mix-model**: haiku · **Effort**: S (~4h)

> Backend reference: `GameNightEmailService` chiama `IEmailService.SendRawEmailAsync` (esistente wrapper). Da capire: concrete impl = Resend, SendGrid, SMTP, MailKit, altro? Spec DEC-5 dice Resend.

**Files:**
- Audit: `apps/api/src/Api/Services/` for `IEmailService` concrete implementation
- Audit: `infra/secrets/` for `EMAIL_*`, `RESEND_*`, `SENDGRID_*` env vars
- Audit: `apps/api/src/Api/Program.cs` for `IEmailService` DI registration

- [ ] **Step 1**: `grep -r "class.*: IEmailService"` per concrete implementation
- [ ] **Step 2**: Verify env vars used (check `appsettings.json` + secret examples)
- [ ] **Step 3**: **Decision branches**:
  - **Branch A**: Provider is Resend → SKIP swap. Document in spec.
  - **Branch B**: Provider is SendGrid/SMTP/MailKit/other → implement swap:
    - Create `ResendEmailSender : IEmailService`
    - Add secret `RESEND_API_KEY` to `infra/secrets/email.secret`
    - Update Program.cs DI: `services.AddSingleton<IEmailService, ResendEmailSender>()`
    - Run integration test with mock HTTP for Resend API
- [ ] **Step 4**: Commit (audit summary OR swap implementation)

```bash
git commit -m "feat(infrastructure): #1896 email provider Resend swap (DEC-5)"
# OR (audit-only)
git commit -m "docs(infrastructure): #1896 email provider audit — already correct (DEC-5)"
```

---

## WP5 — OpenAPI + final acceptance

### Task 14: OpenAPI updates + CLAUDE.md update + acceptance verify

**Mix-model**: haiku · **Effort**: S (~4h)

**Files:**
- Modify: OpenAPI YAML/JSON (paths location depends on project setup)
- Modify: `CLAUDE.md` (Domain Model section: add post-asse-A state)
- Modify: spec consolidato changelog inline

- [ ] **Step 1**: Add new error codes (`MAX_LIVE_SESSIONS_EXCEEDED`, `INVALID_SCORE_DATA`)
- [ ] **Step 2**: Add polymorphic `scoreData` schema + `scoringType` enum reference
- [ ] **Step 3**: Add `X-Warning-Code: SAVED_WHILE_LIVE_ACTIVE` response header
- [ ] **Step 4**: Add `/notifications` endpoints + DTOs (paginated inbox) if added in T12
- [ ] **Step 5**: Verify umbrella acceptance criteria:
  - 20 invarianti implementate e testate (via WP1-WP2 + already-existing)
  - DEC-1 ScoreType 4 strategies + 4 unit class + 1 integration test (WP3)
  - DEC-5 Notification + /notifications + email Resend operativo (WP4)
- [ ] **Step 6**: Run full test suite

```bash
cd apps/api/src/Api
dotnet test --filter "BoundedContext=SessionTracking|BoundedContext=GameManagement|BoundedContext=UserNotifications"
```

- [ ] **Step 7**: Update spec consolidato changelog

```markdown
- 2026-06-XX: asse A v2 implementation complete — 14 task TDD shipped via PR #YYYY (effort 12 gg vs v1 estimate 18 gg — 33% reduction post-discovery)
```

- [ ] **Step 8**: Final commit

```bash
git commit -m "docs(api,claude-design-alignment): #1896 asse A complete — OpenAPI + acceptance verify"
```

---

## Self-Review Checklist (post-plan v2)

**Spec coverage post-discovery**:
- [x] Invariante #10 max 1 live → WP1 T1
- [x] Invariante #11 Session.StartedAt → WP2 T2
- [x] Invariante #12 sort createdAt → trivial, included WP2 T2 if needed (verify existing query handler)
- [x] Invariante #13 X-Warning-Code → WP2 T4
- [x] Invariante #14 startedAt derived → WP2 T2 (no user input)
- [x] Invariante #15 first-session triggers InProgress → WP2 T3
- [x] Invariante #16/#17 tagged vs invited → WP2 T5 (semantic mapping doc, no new entity)
- [x] DEC-1 Polymorphic ScoreType → WP3 T6-T10
- [x] DEC-5 Notification + email → WP4 T11-T13 (existing entity, extend endpoints + audit provider)
- [x] OpenAPI updates → WP5 T14

**Placeholder scan**: WP4 T11-T13 have branch decisions (Resend audit). NO TBD.

**Type consistency**:
- `MaxLiveSessionsExceededException.ErrorCode = "MAX_LIVE_SESSIONS_EXCEEDED"` consistent WP1 T1 + WP5 T14
- `SessionStartedDomainEvent` defined T2, used T3
- `IScoringStrategy` consistent across T6/T7/T8
- `ScoreType` enum consistent T6 → T9 → T10

**Effort verification**:
- WP1: 6h ≈ 1gg
- WP2: 6+5+5+3 = 19h ≈ 2.5gg
- WP3: 5+6+6+6+8 = 31h ≈ 4gg
- WP4: 3+5+4 = 12h ≈ 1.5gg
- WP5: 4h ≈ 0.5gg
- **Total**: ~67h ≈ 8.5gg + ~3gg buffer review/CI = **~12gg**, in linea con target 10+2=12 gg ✓
- Effort reduction vs v1: **33% riduzione** (18gg → 12gg)

---

## Execution Handoff

**Plan v2 complete and saved to `docs/superpowers/plans/2026-06-04-asse-a-semantic-alignment.md`.**

**Critical path** (post-discovery):
1. WP1 (max 1 live) — foundation, parallelizable with WP3
2. WP2 (Session.StartedAt + invariante #15 + warning header + mapping doc) — sequential
3. WP3 (polymorphic ScoreType) — parallel WP4
4. WP4 (notification expand) — parallel WP3
5. WP5 (OpenAPI close)

**Recommended sequence (2 dev parallel)**:
- Dev1: WP1 → WP2 → WP5
- Dev2: WP3 → WP4

Both finish ~2 weeks elapsed time vs solo dev ~3 weeks.

**Two execution options**:
1. **Subagent-Driven (recommended)** — Dispatch fresh subagent per task con mix-model (5 haiku + 9 sonnet)
2. **Inline Execution** — Single session troppo grande per WP3 + WP4 combinati

---

## Changelog

- **2026-06-05 v2.1**: WP4 audit-only post-discovery (T11+T12+T13 già shipped upstream issue #2053+#1629+#5005). 12h recovered, effort 12gg → ~10.5gg (-12.5% additional). Plan task count: 14 → 11 effective tasks (3 WP4 collassati ad audit doc).
- **2026-06-04 v2**: rewrite post-discovery. Gap analysis backend reale → effort -33%. 14 task vs 25 task v1.
- **2026-06-04 v1**: initial plan (now archived in git history). Assumed scratch backend, ~50% over-scoped.
