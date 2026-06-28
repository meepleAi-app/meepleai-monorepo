# Epic #2501 SP0 — TrackingSessionId + Session Companion (Saga at-creation) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Garantire che ogni nuova `LiveGameSession` con un gioco catalogato possieda, at-creation e in modo atomico, una `SessionTracking.Session` companion, esponendo il suo id come `TrackingSessionId` — il ponte di correlazione reale per chat/diary/media delle fasi successive (SP2/SP3/SP4).

**Architecture:** `CreateLiveSessionCommandHandler` (GameManagement) chiama un'interfaccia anti-corruption `ICompanionSessionService` (ACL, rispetta l'invariante ADR-083: GameManagement→SessionTracking unidirezionale via interfaccia dedicata) che crea una `Session` companion via `ISessionRepository.AddAsync` **senza** SaveChanges proprio. L'handler crea poi la `LiveGameSession` con `trackingSessionId = companion.Id` e committa **entrambi gli aggregati in un solo `IUnitOfWork.SaveChangesAsync`** → atomicità transazionale EF (nessuna `LiveGameSession` orfana). La colonna DB è nullable (legacy + sessioni improvvisate senza GameId restano null; backfill = follow-up OQ#5).

**Tech Stack:** .NET 9, EF Core (PostgreSQL), MediatR (CQRS), xUnit + Testcontainers.

## Global Constraints

- **CQRS**: endpoint usano solo `IMediator.Send()`; handler non vengono iniettati negli endpoint.
- **DDD**: entity con setter privati + factory; repository interface nel Domain, impl in Infrastructure.
- **Invariante ADR-083**: `GameManagement` NON importa mai `KnowledgeBase`. `GameManagement → SessionTracking` è ammesso ma SOLO via interfaccia ACL dedicata (no riferimenti diretti ai tipi SessionTracking nell'Application layer di GameManagement; il coupling vive nell'Infrastructure impl dell'ACL).
- **Atomicità (ADR-060)**: ogni handler che chiama `AddAsync` deve poi chiamare `await _unitOfWork.SaveChangesAsync(ct)`; un solo SaveChanges per Handle.
- **Concorrenza**: `xmin` Postgres, server-owned, nessuna assegnazione nel mapper.
- **Eccezioni (#2568)**: `ConflictException`(409)/`NotFoundException`(404), mai `InvalidOperationException`(500) per condizioni note.
- **Nullable garantito at-creation per nuove sessioni CON GameId**: `TrackingSessionId` è `Guid?` nel dominio/DB (null = legacy/improvvisata); la factory `Create` lo popola sempre quando il chiamante passa un valore; l'handler lo passa sempre quando `command.GameId.HasValue`.
- **Branch**: `feature/issue-2501-sp0-saga-tracking-session` (parent `main-dev`).

---

### Task 1: Dominio — `TrackingSessionId` su `LiveGameSession`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs`
- Test: collocare accanto ai test dominio esistenti di `LiveGameSession` (cercare con `Glob "**/*LiveGameSession*Test*.cs"` sotto `tests/`; se assenti per la factory, creare `tests/Api.Tests/Unit/GameManagement/LiveGameSessionCreateTests.cs` seguendo il pattern xUnit del progetto).

**Interfaces:**
- Produces: `LiveGameSession.TrackingSessionId` (`Guid?`, getter pubblico, setter privato); `LiveGameSession.Create(..., Guid? trackingSessionId = null)` nuovo parametro opzionale in coda; `LiveGameSession.Reconstitute(..., Guid? trackingSessionId)` nuovo parametro.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Create_WithTrackingSessionId_SetsProperty()
{
    var trackingId = Guid.NewGuid();
    var session = LiveGameSession.Create(
        id: Guid.NewGuid(),
        createdByUserId: Guid.NewGuid(),
        gameName: "Mage Knight",
        timeProvider: TimeProvider.System,
        gameId: Guid.NewGuid(),
        trackingSessionId: trackingId);

    Assert.Equal(trackingId, session.TrackingSessionId);
}

[Fact]
public void Create_WithoutTrackingSessionId_LeavesNull()
{
    var session = LiveGameSession.Create(
        id: Guid.NewGuid(),
        createdByUserId: Guid.NewGuid(),
        gameName: "Free session",
        timeProvider: TimeProvider.System);

    Assert.Null(session.TrackingSessionId);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet build` then `dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~LiveGameSessionCreateTests"`
Expected: FAIL — compile error (`trackingSessionId` parameter / `TrackingSessionId` property not defined).

- [ ] **Step 3: Write minimal implementation**

In `LiveGameSession.cs`, add the property next to the other scalar properties (after `ChatSessionId` at line ~65):

```csharp
    /// <summary>
    /// Id of the SessionTracking.Session companion created at-creation (Saga, ADR-083 SP0).
    /// Non-null for new sessions created with a catalog GameId; null for legacy rows and
    /// free-form sessions without a GameId (backfill tracked as OQ#5 follow-up).
    /// This is the real cross-BC correlation bridge (replaces the dead-code ChatSessionId).
    /// </summary>
    public Guid? TrackingSessionId { get; private set; }
```

In the `Create` factory signature, add the parameter at the end of the list (after `turnAdvancePolicy`):

```csharp
        TurnAdvancePolicy turnAdvancePolicy = TurnAdvancePolicy.Manual,
        Guid? trackingSessionId = null)
```

In the object initializer inside `Create` (the `new LiveGameSession { ... }` block at line ~122), add:

```csharp
            TurnAdvancePolicy = turnAdvancePolicy,
            TrackingSessionId = trackingSessionId
```

In `Reconstitute`, add the parameter (after `xmin` or near the scalar params) and set it in the reconstituted instance object initializer (the `new LiveGameSession { ... }` block at line ~200 that ends near line 216):

```csharp
            // signature: add after the existing scalar params, before the collection params
            Guid? trackingSessionId,
```
```csharp
            // object initializer: alongside ChatSessionId/TurnAdvancePolicy/Xmin
            TrackingSessionId = trackingSessionId,
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~LiveGameSessionCreateTests"`
Expected: PASS (2 tests). NOTE: this will break `LiveGameSessionMapper.ToDomain` callers of `Reconstitute` — fixed in Task 2; build of the Api project may fail until Task 2. If so, proceed to Task 2 and re-run.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs tests/Api.Tests
git commit -m "feat(session-live): #2501 SP0 add TrackingSessionId to LiveGameSession domain"
```

---

### Task 2: Persistenza — entity, EF config, mapper

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/Mappers/LiveGameSessionMapper.cs`
- Test: `tests/Api.Tests` mapper round-trip (cercare un test esistente `LiveGameSessionMapper*Test*.cs` con `Glob`; se esiste, estenderlo; altrimenti aggiungere un unit test round-trip).

**Interfaces:**
- Consumes: `LiveGameSession.TrackingSessionId` (Task 1).
- Produces: `LiveGameSessionEntity.TrackingSessionId` (`Guid?`); colonna `tracking_session_id`; mapper round-trip del campo.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Mapper_RoundTrips_TrackingSessionId()
{
    var trackingId = Guid.NewGuid();
    var domain = LiveGameSession.Create(
        id: Guid.NewGuid(),
        createdByUserId: Guid.NewGuid(),
        gameName: "Mage Knight",
        timeProvider: TimeProvider.System,
        gameId: Guid.NewGuid(),
        trackingSessionId: trackingId);

    var entity = LiveGameSessionMapper.ToEntity(domain);
    Assert.Equal(trackingId, entity.TrackingSessionId);

    var back = LiveGameSessionMapper.ToDomain(entity);
    Assert.Equal(trackingId, back.TrackingSessionId);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~LiveGameSessionMapper"`
Expected: FAIL — `LiveGameSessionEntity.TrackingSessionId` not defined.

- [ ] **Step 3: Write minimal implementation**

In `LiveGameSessionEntity.cs`, after `ChatSessionId` (line ~65):

```csharp
    // ADR-083 SP0: id of the SessionTracking.Session companion (cross-BC correlation bridge).
    public Guid? TrackingSessionId { get; set; }
```

In `LiveGameSessionEntityConfiguration.cs`, after the `ChatSessionId` property mapping (line ~111):

```csharp
        builder.Property(e => e.TrackingSessionId)
            .HasColumnName("tracking_session_id");
```

In `LiveGameSessionMapper.ToEntity`, in the `new LiveGameSessionEntity { ... }` initializer (after `ChatSessionId = domain.ChatSessionId,` at line ~68):

```csharp
            TrackingSessionId = domain.TrackingSessionId,
```

In `LiveGameSessionMapper.ToDomain`, in the `Reconstitute(...)` call (after `chatSessionId: entity.ChatSessionId,` at line ~264):

```csharp
            trackingSessionId: entity.TrackingSessionId,
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/api/src/Api && dotnet build && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~LiveGameSessionMapper"`
Expected: PASS. The whole Api project now builds (Reconstitute signature satisfied).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure tests/Api.Tests apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/Mappers/LiveGameSessionMapper.cs
git commit -m "feat(session-live): #2501 SP0 persist tracking_session_id (entity+config+mapper)"
```

---

### Task 3: Migration EF — colonna `tracking_session_id`

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddTrackingSessionIdToLiveGameSession.cs` (auto-generata)

**Interfaces:**
- Consumes: entity/config di Task 2.
- Produces: colonna nullable `tracking_session_id uuid NULL` su `live_game_sessions`.

- [ ] **Step 1: Generate the migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddTrackingSessionIdToLiveGameSession`
Expected: nuovo file migration generato.

- [ ] **Step 2: Review the generated SQL**

Apri il file migration generato. VERIFICA che `Up()` contenga SOLO:
```csharp
migrationBuilder.AddColumn<Guid>(
    name: "tracking_session_id",
    table: "live_game_sessions",
    type: "uuid",
    nullable: true);
```
e `Down()` il `DropColumn` corrispondente. Se la migration include altre modifiche di schema impreviste (drift), FERMARSI e investigare prima di procedere.

- [ ] **Step 3: Apply + verify it builds**

Run: `dotnet build`
Expected: PASS. (Non serve `database update` qui: i test integration usano Testcontainers che applica le migration automaticamente.)

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations
git commit -m "feat(session-live): #2501 SP0 migration add tracking_session_id (nullable)"
```

---

### Task 4: ACL — `ICompanionSessionService` + impl + DI

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Abstractions/ICompanionSessionService.cs` (allineare alla cartella di astrazioni già usata in GameManagement Application; se non esiste, `Application/Services/`)
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Services/CompanionSessionService.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs` (registra accanto a `ILiveSessionRepository` alla riga ~38)
- Test: `tests/Api.Tests/Unit/GameManagement/CompanionSessionServiceTests.cs`

**Interfaces:**
- Consumes: `Api.BoundedContexts.SessionTracking.Domain.Repositories.ISessionRepository` (ha `Task AddAsync(Session, CancellationToken)`); `Api.BoundedContexts.SessionTracking.Domain.Entities.Session.Create(Guid userId, Guid gameId, SessionType sessionType, string? location = null, DateTime? sessionDate = null)`; `SessionType.GameSpecific`.
- Produces: `ICompanionSessionService.CreateCompanionAsync(Guid userId, Guid gameId, CancellationToken ct) : Task<Guid>` — crea (Add, NO SaveChanges) la companion e ritorna il suo `Id`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task CreateCompanionAsync_AddsSession_AndReturnsItsId()
{
    var repo = new Mock<Api.BoundedContexts.SessionTracking.Domain.Repositories.ISessionRepository>();
    Session? captured = null;
    repo.Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
        .Callback<Session, CancellationToken>((s, _) => captured = s)
        .Returns(Task.CompletedTask);

    var sut = new CompanionSessionService(repo.Object);
    var userId = Guid.NewGuid();
    var gameId = Guid.NewGuid();

    var trackingId = await sut.CreateCompanionAsync(userId, gameId, CancellationToken.None);

    Assert.NotNull(captured);
    Assert.Equal(captured!.Id, trackingId);
    Assert.Equal(userId, captured.UserId);
    Assert.Equal(gameId, captured.GameId);
    repo.Verify(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~CompanionSessionServiceTests"`
Expected: FAIL — `CompanionSessionService` / `ICompanionSessionService` not defined.

- [ ] **Step 3: Write minimal implementation**

`ICompanionSessionService.cs`:
```csharp
namespace Api.BoundedContexts.GameManagement.Application.Abstractions;

/// <summary>
/// Anti-corruption boundary (ADR-083 SP0): GameManagement creates a SessionTracking.Session
/// companion at-creation without referencing SessionTracking types in its Application layer.
/// The companion's id becomes LiveGameSession.TrackingSessionId.
/// </summary>
public interface ICompanionSessionService
{
    /// <summary>Adds (no SaveChanges) a companion Session and returns its id.
    /// Caller commits it atomically together with the LiveGameSession via IUnitOfWork.</summary>
    Task<Guid> CreateCompanionAsync(Guid userId, Guid gameId, CancellationToken ct);
}
```

`CompanionSessionService.cs`:
```csharp
using Api.BoundedContexts.GameManagement.Application.Abstractions;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

internal sealed class CompanionSessionService : ICompanionSessionService
{
    private readonly ISessionRepository _sessionRepository;

    public CompanionSessionService(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<Guid> CreateCompanionAsync(Guid userId, Guid gameId, CancellationToken ct)
    {
        var companion = Session.Create(userId, gameId, SessionType.GameSpecific);
        await _sessionRepository.AddAsync(companion, ct).ConfigureAwait(false);
        return companion.Id;
    }
}
```

In `GameManagementServiceExtensions.cs` (after line ~38):
```csharp
        services.AddScoped<Api.BoundedContexts.GameManagement.Application.Abstractions.ICompanionSessionService,
            Api.BoundedContexts.GameManagement.Infrastructure.Services.CompanionSessionService>(); // #2501 SP0 ACL companion
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet build && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~CompanionSessionServiceTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement tests/Api.Tests
git commit -m "feat(session-live): #2501 SP0 add ICompanionSessionService ACL + impl + DI"
```

---

### Task 5: Wire `CreateLiveSessionCommandHandler` — companion atomica

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/CreateLiveSessionCommandHandler.cs`
- Test (unit, atomicità): `tests/Api.Tests/Unit/GameManagement/CreateLiveSessionCommandHandlerTests.cs`
- Test (integration, happy path): collocare accanto ai test integration GameManagement esistenti (cercare con `Glob "tests/**/Integration/**/*.cs"`; usare il pattern Testcontainers + `MeepleAiDbContext` reale già in uso nel progetto). Se non esiste una base integration per LiveSession, creare `tests/Api.Tests/Integration/GameManagement/CreateLiveSessionCompanionIntegrationTests.cs` seguendo un test integration esistente come modello.

**Interfaces:**
- Consumes: `ICompanionSessionService.CreateCompanionAsync` (Task 4); `LiveGameSession.Create(..., trackingSessionId:)` (Task 1).
- Produces: handler che, quando `command.GameId.HasValue`, popola `TrackingSessionId` con la companion id e committa entrambi in un solo SaveChanges.

- [ ] **Step 1: Write the failing tests**

Unit (atomicità — companion fallisce → niente commit, niente LiveGameSession aggiunta):
```csharp
[Fact]
public async Task Handle_WhenCompanionFails_DoesNotPersistLiveSession()
{
    var sessionRepo = new Mock<ILiveSessionRepository>();
    var uow = new Mock<IUnitOfWork>();
    var companion = new Mock<ICompanionSessionService>();
    companion.Setup(c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("companion insert failed"));

    var sut = new CreateLiveSessionCommandHandler(sessionRepo.Object, TimeProvider.System, uow.Object, companion.Object);
    var cmd = new CreateLiveSessionCommand(UserId: Guid.NewGuid(), GameName: "Mage Knight", GameId: Guid.NewGuid());

    await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(cmd, CancellationToken.None));

    sessionRepo.Verify(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()), Times.Never);
    uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task Handle_WithGameId_SetsTrackingSessionIdToCompanionId()
{
    var trackingId = Guid.NewGuid();
    LiveGameSession? added = null;
    var sessionRepo = new Mock<ILiveSessionRepository>();
    sessionRepo.Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
        .Callback<LiveGameSession, CancellationToken>((s, _) => added = s)
        .Returns(Task.CompletedTask);
    var uow = new Mock<IUnitOfWork>();
    var companion = new Mock<ICompanionSessionService>();
    companion.Setup(c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(trackingId);

    var sut = new CreateLiveSessionCommandHandler(sessionRepo.Object, TimeProvider.System, uow.Object, companion.Object);
    var cmd = new CreateLiveSessionCommand(UserId: Guid.NewGuid(), GameName: "Mage Knight", GameId: Guid.NewGuid());

    await sut.Handle(cmd, CancellationToken.None);

    Assert.NotNull(added);
    Assert.Equal(trackingId, added!.TrackingSessionId);
    uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task Handle_WithoutGameId_DoesNotCreateCompanion()
{
    LiveGameSession? added = null;
    var sessionRepo = new Mock<ILiveSessionRepository>();
    sessionRepo.Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
        .Callback<LiveGameSession, CancellationToken>((s, _) => added = s)
        .Returns(Task.CompletedTask);
    var uow = new Mock<IUnitOfWork>();
    var companion = new Mock<ICompanionSessionService>();

    var sut = new CreateLiveSessionCommandHandler(sessionRepo.Object, TimeProvider.System, uow.Object, companion.Object);
    var cmd = new CreateLiveSessionCommand(UserId: Guid.NewGuid(), GameName: "Free session", GameId: null);

    await sut.Handle(cmd, CancellationToken.None);

    Assert.NotNull(added);
    Assert.Null(added!.TrackingSessionId);
    companion.Verify(c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

Integration (happy path — entrambi persistiti atomicamente, ids correlati): seed un user + uno shared game validi (seguendo le fixture esistenti), invia `CreateLiveSessionCommand` con quel `GameId`, poi:
```csharp
// dopo Handle:
var liveId = result; // Guid ritornato
var live = await db.LiveGameSessions.AsNoTracking().SingleAsync(s => s.Id == liveId);
Assert.NotNull(live.TrackingSessionId);
var companionExists = await db.SessionTrackingSessions.AsNoTracking()
    .AnyAsync(s => s.Id == live.TrackingSessionId);
Assert.True(companionExists);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~CreateLiveSessionCommandHandlerTests|FullyQualifiedName~CreateLiveSessionCompanionIntegrationTests"`
Expected: FAIL — il ctor dell'handler non accetta `ICompanionSessionService`.

- [ ] **Step 3: Write minimal implementation**

In `CreateLiveSessionCommandHandler.cs`, inietta `ICompanionSessionService` e usalo prima di creare la live session:

```csharp
using Api.BoundedContexts.GameManagement.Application.Abstractions;
// ...
    private readonly ICompanionSessionService _companionSessionService;

    public CreateLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork,
        ICompanionSessionService companionSessionService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _companionSessionService = companionSessionService ?? throw new ArgumentNullException(nameof(companionSessionService));
    }
```

In `Handle`, after building `scoringConfig` and BEFORE `LiveGameSession.Create`:

```csharp
        // ADR-083 SP0: create the SessionTracking.Session companion at-creation (Saga).
        // Add-only (no SaveChanges here): the single SaveChangesAsync below commits the
        // companion and the LiveGameSession atomically in one EF transaction — a companion
        // failure rolls back the LiveGameSession, so no orphan is ever persisted.
        Guid? trackingSessionId = null;
        if (command.GameId.HasValue)
        {
            trackingSessionId = await _companionSessionService
                .CreateCompanionAsync(command.UserId, command.GameId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        var session = LiveGameSession.Create(
            Guid.NewGuid(),
            command.UserId,
            command.GameName,
            _timeProvider,
            command.GameId,
            command.Visibility,
            command.GroupId,
            scoringConfig,
            command.AgentMode,
            trackingSessionId: trackingSessionId);

        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return session.Id;
```

(Note: `LiveGameSession.Create` ha `turnAdvancePolicy` come penultimo param con default; passare `trackingSessionId:` come argomento nominato evita ambiguità posizionali.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet build && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~CreateLiveSessionCommandHandlerTests|FullyQualifiedName~CreateLiveSessionCompanionIntegrationTests"`
Expected: PASS (3 unit + 1 integration).

- [ ] **Step 5: Run the full GameManagement + SessionTracking suite (no regressions)**

Run: `dotnet test ../../../../tests/Api.Tests --filter "BoundedContext=GameManagement|BoundedContext=SessionTracking"`
Expected: PASS, no new failures vs baseline.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/CreateLiveSessionCommandHandler.cs tests/Api.Tests
git commit -m "feat(session-live): #2501 SP0 wire companion saga into CreateLiveSessionCommandHandler (atomic)"
```

---

## Out of scope (tracciato per le sub-fasi successive)
- Rimozione `ChatSessionId`/`SetAgentMode` dead-code (SP5.1) — NON in questo plan.
- SSE nativo `/live-sessions/{id}/stream` (SP2), diary (SP3), media/foto (SP4).
- Backfill companion per righe legacy + sessioni improvvisate senza GameId (OQ#5) — colonna nullable lo consente; follow-up.
- Esposizione `TrackingSessionId` nel `LiveSessionDto` FE — non necessaria finché SP2/SP3/SP4 non la consumano.

## Self-Review
- **Spec coverage**: SP0 = «Session-companion canonica (Saga + TrackingSessionId)» → Task 1-5 coprono campo dominio, persistenza, migration, ACL, wiring atomico. La garanzia non-nullable «per le nuove con GameId» è implementata; il caso GameId-null e legacy è esplicitamente nullable+follow-up (delta documentato per review owner). ✓
- **Placeholder scan**: ogni step di codice mostra il codice reale; i path test non-confermati hanno istruzione esplicita di Glob + pattern da seguire. ✓
- **Type consistency**: `CreateCompanionAsync(Guid,Guid,CancellationToken):Task<Guid>` e `Create(...,trackingSessionId:)` usati identici in Task 1/4/5; `SessionType.GameSpecific` verificato (Session.cs:652). ✓
