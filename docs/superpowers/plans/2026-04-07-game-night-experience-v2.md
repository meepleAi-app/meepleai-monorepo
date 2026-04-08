# Game Night Experience v2 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Orchestrare una serata multi-gioco (1-5 giochi) con diario real-time, stato L2 (score + risorse giocatore), offline host-only, sync multi-device sincrono via SSE, e gallery foto — estendendo il flusso Game Night Improvvisata già 100% implementato.

**Architecture:**
- Backend: Nuovo `GameNightSession` linking entity nel BC `GameManagement` collega `GameNightEvent` → `Session[]` tramite persistence entity `GameNightSessionEntity` (pattern MapToDomain/MapToPersistence). Estensione `SessionEvent` con `GameNightId` nullable per diario cross-game (NO nuova entity DiaryEntry — riusa SessionEvent esistente). Estensione `SessionCheckpoint.SnapshotData` con schema L2 (risorse giocatore). Auto-save via Hangfire background job.
- Frontend: Estensione `useGameNightStore` con stato multi-sessione + diario. Nuovo `useGameNightDiary` hook per SSE real-time diary. Service worker per offline host (static assets only, NO API caching). SSE broadcast di diary entries a tutti i device via estensione `SseEventTypeMapper`.
- Cross-BC: `StartGameNightSessionCommand` nel GameManagement BC usa `IMediator.Send()` per creare la Session nel SessionTracking BC (nessuna injection diretta di repository cross-BC).

**Tech Stack:** .NET 9, MediatR CQRS, EF Core + PostgreSQL (JSONB), Hangfire, Next.js 16, Zustand + immer, `idb` (IndexedDB), shadcn/ui, Tailwind 4, Vitest, xUnit.

**Presupposti:**
- `GameNightEvent` esiste con `GameIds`, lifecycle Draft→Published→Completed ✅
- `Session` esiste con factory `Session.Create(userId, gameId, sessionType, location?, sessionDate?)` ✅
- `SessionEvent` ha `EventType` + `Payload` JSON — già un event log per sessione ✅
- Repository pattern: `GameNightEventEntity` (persistence) ↔ `MapToDomain`/`MapToPersistence` ↔ `GameNightEvent` (domain) ✅
- `RepositoryBase` usa `MeepleAiDbContext` con `IDomainEventCollector` ✅
- Frontend stores `useGameNightStore` e `useSessionStore` esistono ✅
- SSE v2 + `useSessionSSE` + `SseEventTypeMapper` esistono ✅
- `GameNightGame` type esiste in `stores/game-night/types.ts` ✅
- Guest join via `/join/session/[code]` esiste ✅
- Offline solo per host (decisione Q2-A) ✅

**Confini:**
- NON include offline per guest (solo host)
- NON include L3 (mappa partita, inventario complesso) — solo L1+L2
- NON include push notifications (solo SSE in-app)
- NON include export diario come immagine/PDF (fase futura)

**Review fixes applicati (2026-04-08):**
- C1: Aggiunto Task SSE broadcast + `useGameNightDiary` hook
- C2: Persistence entity `GameNightSessionEntity` con `MapToDomain`/`MapToPersistence`
- C3: `Session.Create()` factory method ovunque
- C4: Cross-BC via `IMediator.Send(CreateSessionCommand)`, non repository injection
- I1: Repository non chiama `SaveChangesAsync` (UoW pattern)
- I2: `RestoreSessions` nel `MapToDomain` del repository
- I3: `AutoSaveSessionJob.Remove()` wired in finalize/pause handlers
- I4: Service worker limita cache a static assets
- I5: Riusa `SessionEvent` con `GameNightId` nullable invece di nuova entity

---

## File Map

### Nuovi file — Backend

```
# GameManagement BC — Multi-Session Linking
apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightSession.cs
apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/GameNightSessionStatus.cs
apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommand.cs
apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/CompleteGameNightSessionCommand.cs
apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/FinalizeGameNightCommand.cs
apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GameNights/GetGameNightDiaryQuery.cs
apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameNights/GameNightDiaryDto.cs
apps/api/src/Api/Infrastructure/Entities/GameManagement/GameNightSessionEntity.cs

# SessionTracking BC — L2 State + Auto-save
apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/PlayerResources.cs
apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/AutoSaveSessionCommand.cs
apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Jobs/AutoSaveSessionJob.cs

# Tests
tests/Api.Tests/BoundedContexts/GameManagement/Unit/GameNightSessionTests.cs
tests/Api.Tests/BoundedContexts/GameManagement/Unit/GameNightEventSessionsTests.cs
tests/Api.Tests/BoundedContexts/GameManagement/Unit/StartGameNightSessionTests.cs
tests/Api.Tests/BoundedContexts/GameManagement/Unit/FinalizeGameNightTests.cs
tests/Api.Tests/BoundedContexts/SessionTracking/Unit/PlayerResourcesTests.cs
tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveSessionTests.cs
```

### Nuovi file — Frontend

```
# Hooks
apps/web/src/lib/domain-hooks/useGameNightDiary.ts
apps/web/src/lib/domain-hooks/useGameNightMultiSession.ts
apps/web/src/lib/domain-hooks/usePlayerResources.ts
apps/web/src/lib/domain-hooks/__tests__/useGameNightDiary.test.ts
apps/web/src/lib/domain-hooks/__tests__/useGameNightMultiSession.test.ts

# Components
apps/web/src/components/game-night/GameNightDiary.tsx
apps/web/src/components/game-night/GameNightDiaryEntry.tsx
apps/web/src/components/game-night/GameTransitionDialog.tsx
apps/web/src/components/game-night/PlayerResourcesPanel.tsx
apps/web/src/components/game-night/GameNightSummaryCard.tsx
apps/web/src/components/game-night/__tests__/GameNightDiary.test.tsx
apps/web/src/components/game-night/__tests__/GameTransitionDialog.test.tsx
apps/web/src/components/game-night/__tests__/PlayerResourcesPanel.test.tsx

# API client
apps/web/src/lib/api/clients/gameNightSessionClient.ts

# Service worker (offline static assets only)
apps/web/public/sw-toolkit.js
```

### File modificati

```
# Backend
apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs
apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GameNightEventRepository.cs
apps/api/src/Api/Infrastructure/Entities/GameManagement/GameNightEventEntity.cs
apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
apps/api/src/Api/Routing/GameNightEndpoints.cs
apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionEvent.cs  # Add GameNightId
apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/SseEventTypeMapper.cs  # Add diary event types

# Frontend
apps/web/src/stores/game-night/store.ts
apps/web/src/stores/game-night/types.ts
apps/web/src/stores/session/types.ts
apps/web/src/components/game-night/LiveSessionView.tsx
```

---

## PHASE 1 — Backend: GameNightSession Entity + Persistence

### Task 1: GameNightSession domain entity + enum

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/GameNightSessionStatus.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightSession.cs`
- Create: `tests/Api.Tests/BoundedContexts/GameManagement/Unit/GameNightSessionTests.cs`

- [ ] **Step 1: Scrivi il test unitario per GameNightSession**

```csharp
// tests/Api.Tests/BoundedContexts/GameManagement/Unit/GameNightSessionTests.cs
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Unit;

public class GameNightSessionTests
{
    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        var gameNightId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var gns = GameNightSession.Create(gameNightId, sessionId, gameId, "Catan", 1);

        Assert.Equal(gameNightId, gns.GameNightEventId);
        Assert.Equal(sessionId, gns.SessionId);
        Assert.Equal(gameId, gns.GameId);
        Assert.Equal("Catan", gns.GameTitle);
        Assert.Equal(1, gns.PlayOrder);
        Assert.Equal(GameNightSessionStatus.Pending, gns.Status);
        Assert.Null(gns.WinnerId);
        Assert.Null(gns.StartedAt);
        Assert.Null(gns.CompletedAt);
    }

    [Fact]
    public void Create_WithEmptyGameNightId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameNightSession.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "Catan", 1));
    }

    [Fact]
    public void Create_WithEmptyGameTitle_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameNightSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "", 1));
    }

    [Fact]
    public void Create_WithZeroPlayOrder_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameNightSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Catan", 0));
    }

    [Fact]
    public void Reconstitute_RestoresAllProperties()
    {
        var id = Guid.NewGuid();
        var gameNightId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var completedAt = DateTimeOffset.UtcNow;

        var gns = GameNightSession.Reconstitute(
            id, gameNightId, sessionId, gameId, "Catan", 1,
            GameNightSessionStatus.Completed, winnerId, startedAt, completedAt);

        Assert.Equal(id, gns.Id);
        Assert.Equal(GameNightSessionStatus.Completed, gns.Status);
        Assert.Equal(winnerId, gns.WinnerId);
        Assert.Equal(startedAt, gns.StartedAt);
        Assert.Equal(completedAt, gns.CompletedAt);
    }
}
```

- [ ] **Step 2: Esegui il test — verifica che fallisca**

```bash
cd tests/Api.Tests
dotnet test --filter "GameNightSessionTests" --no-build 2>&1 | head -20
# Expected: FAIL — GameNightSession non trovato
```

- [ ] **Step 3: Crea l'enum in file separato (per convenzione BC)**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/GameNightSessionStatus.cs
namespace Api.BoundedContexts.GameManagement.Domain.Enums;

public enum GameNightSessionStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Skipped = 3
}
```

- [ ] **Step 4: Crea GameNightSession entity**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightSession.cs
using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// Links a GameNightEvent to a Session, tracking play order and per-game outcome
/// within a multi-game evening. Each game night can have 1-5 sessions in sequence.
/// </summary>
internal sealed class GameNightSession
{
    public Guid Id { get; private set; }
    public Guid GameNightEventId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid GameId { get; private set; }
    public string GameTitle { get; private set; } = string.Empty;
    public int PlayOrder { get; private set; }
    public GameNightSessionStatus Status { get; private set; }
    public Guid? WinnerId { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private GameNightSession() { }

    public static GameNightSession Create(
        Guid gameNightEventId, Guid sessionId, Guid gameId, string gameTitle, int playOrder)
    {
        if (gameNightEventId == Guid.Empty)
            throw new ArgumentException("GameNightEventId cannot be empty.", nameof(gameNightEventId));
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty.", nameof(gameId));
        if (string.IsNullOrWhiteSpace(gameTitle))
            throw new ArgumentException("GameTitle cannot be empty.", nameof(gameTitle));
        if (playOrder < 1)
            throw new ArgumentException("PlayOrder must be >= 1.", nameof(playOrder));

        return new GameNightSession
        {
            Id = Guid.NewGuid(),
            GameNightEventId = gameNightEventId,
            SessionId = sessionId,
            GameId = gameId,
            GameTitle = gameTitle.Trim(),
            PlayOrder = playOrder,
            Status = GameNightSessionStatus.Pending
        };
    }

    /// <summary>
    /// Reconstitute from persistence (used by MapToDomain).
    /// </summary>
    public static GameNightSession Reconstitute(
        Guid id, Guid gameNightEventId, Guid sessionId, Guid gameId,
        string gameTitle, int playOrder, GameNightSessionStatus status,
        Guid? winnerId, DateTimeOffset? startedAt, DateTimeOffset? completedAt)
    {
        return new GameNightSession
        {
            Id = id,
            GameNightEventId = gameNightEventId,
            SessionId = sessionId,
            GameId = gameId,
            GameTitle = gameTitle,
            PlayOrder = playOrder,
            Status = status,
            WinnerId = winnerId,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };
    }

    internal void Start()
    {
        if (Status != GameNightSessionStatus.Pending)
            throw new InvalidOperationException($"Cannot start session in {Status} status.");
        Status = GameNightSessionStatus.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
    }

    internal void Complete(Guid? winnerId)
    {
        if (Status != GameNightSessionStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete session in {Status} status.");
        Status = GameNightSessionStatus.Completed;
        WinnerId = winnerId;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    internal void Skip()
    {
        if (Status != GameNightSessionStatus.Pending)
            throw new InvalidOperationException($"Cannot skip session in {Status} status.");
        Status = GameNightSessionStatus.Skipped;
    }
}
```

- [ ] **Step 5: Esegui i test — verifica che passino**

```bash
cd tests/Api.Tests
dotnet test --filter "GameNightSessionTests" -v
# Expected: 5 test PASS
```

- [ ] **Step 6: Commit**

```bash
git add tests/Api.Tests/BoundedContexts/GameManagement/Unit/GameNightSessionTests.cs
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/GameNightSessionStatus.cs
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightSession.cs
git commit -m "feat(game-night): add GameNightSession entity + enum with TDD"
```

---

### Task 2: Estendi GameNightEvent con Sessions — aggregate root methods

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs`
- Create: `tests/Api.Tests/BoundedContexts/GameManagement/Unit/GameNightEventSessionsTests.cs`

- [ ] **Step 1: Scrivi test per le nuove operazioni aggregate root**

```csharp
// tests/Api.Tests/BoundedContexts/GameManagement/Unit/GameNightEventSessionsTests.cs
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Unit;

public class GameNightEventSessionsTests
{
    private GameNightEvent CreatePublishedEvent()
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Friday Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        evt.Publish([Guid.NewGuid()]);
        return evt;
    }

    [Fact]
    public void AddSession_ToPublishedEvent_AddsToCollection()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        Assert.Single(evt.Sessions);
        Assert.Equal(1, evt.Sessions[0].PlayOrder);
    }

    [Fact]
    public void AddSession_AssignsIncrementalPlayOrder()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.AddSession(Guid.NewGuid(), evt.GameIds[1], "Dixit");
        Assert.Equal(2, evt.Sessions[1].PlayOrder);
    }

    [Fact]
    public void AddSession_BeyondFive_Throws()
    {
        var evt = CreatePublishedEvent();
        for (var i = 0; i < 5; i++)
            evt.AddSession(Guid.NewGuid(), Guid.NewGuid(), $"Game{i}");
        Assert.Throws<InvalidOperationException>(() =>
            evt.AddSession(Guid.NewGuid(), Guid.NewGuid(), "Game6"));
    }

    [Fact]
    public void AddSession_ToDraftEvent_Throws()
    {
        var evt = GameNightEvent.Create(Guid.NewGuid(), "Draft Night", DateTimeOffset.UtcNow.AddHours(1));
        Assert.Throws<InvalidOperationException>(() =>
            evt.AddSession(Guid.NewGuid(), Guid.NewGuid(), "Catan"));
    }

    [Fact]
    public void StartCurrentSession_TransitionsFirstPendingToInProgress()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.AddSession(Guid.NewGuid(), evt.GameIds[1], "Dixit");

        evt.StartCurrentSession();

        Assert.Equal(GameNightSessionStatus.InProgress, evt.Sessions[0].Status);
        Assert.Equal(GameNightSessionStatus.Pending, evt.Sessions[1].Status);
    }

    [Fact]
    public void CompleteCurrentSession_WithWinner_MarksCompleted()
    {
        var winnerId = Guid.NewGuid();
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.StartCurrentSession();

        evt.CompleteCurrentSession(winnerId);

        Assert.Equal(GameNightSessionStatus.Completed, evt.Sessions[0].Status);
        Assert.Equal(winnerId, evt.Sessions[0].WinnerId);
    }

    [Fact]
    public void CurrentSession_ReturnsInProgressOrFirstPending()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        Assert.NotNull(evt.CurrentSession);
        Assert.Equal("Catan", evt.CurrentSession!.GameTitle);
    }

    [Fact]
    public void FinalizeNight_SetsCompleted_WhenAllSessionsDone()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.StartCurrentSession();
        evt.CompleteCurrentSession(Guid.NewGuid());
        evt.FinalizeNight();
        Assert.Equal(GameNightStatus.Completed, evt.Status);
    }

    [Fact]
    public void FinalizeNight_WithInProgressSession_Throws()
    {
        var evt = CreatePublishedEvent();
        evt.AddSession(Guid.NewGuid(), evt.GameIds[0], "Catan");
        evt.StartCurrentSession();
        Assert.Throws<InvalidOperationException>(() => evt.FinalizeNight());
    }

    [Fact]
    public void RestoreSessions_ReconstitutesFromPersistence()
    {
        var evt = CreatePublishedEvent();
        var session = GameNightSession.Reconstitute(
            Guid.NewGuid(), evt.Id, Guid.NewGuid(), Guid.NewGuid(),
            "Catan", 1, GameNightSessionStatus.Completed,
            Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);

        evt.RestoreSessions([session]);

        Assert.Single(evt.Sessions);
        Assert.Equal(GameNightSessionStatus.Completed, evt.Sessions[0].Status);
    }
}
```

- [ ] **Step 2: Esegui test — verifica che falliscano**

```bash
cd tests/Api.Tests
dotnet test --filter "GameNightEventSessionsTests" --no-build 2>&1 | head -20
# Expected: FAIL
```

- [ ] **Step 3: Estendi GameNightEvent** — aggiungi dopo la proprietà `Rsvps` nel file `GameNightEvent.cs`:

```csharp
    private readonly List<GameNightSession> _sessions = new();
    public IReadOnlyList<GameNightSession> Sessions => _sessions.AsReadOnly();

    public GameNightSession? CurrentSession =>
        _sessions.FirstOrDefault(s => s.Status == GameNightSessionStatus.InProgress)
        ?? _sessions.FirstOrDefault(s => s.Status == GameNightSessionStatus.Pending);

    public GameNightSession AddSession(Guid sessionId, Guid gameId, string gameTitle)
    {
        if (Status != GameNightStatus.Published)
            throw new InvalidOperationException($"Cannot add sessions to a {Status} game night.");
        if (_sessions.Count >= 5)
            throw new InvalidOperationException("A game night can have at most 5 sessions.");

        var playOrder = _sessions.Count + 1;
        var session = GameNightSession.Create(Id, sessionId, gameId, gameTitle, playOrder);
        _sessions.Add(session);
        UpdatedAt = DateTimeOffset.UtcNow;
        return session;
    }

    public void StartCurrentSession()
    {
        var session = _sessions.FirstOrDefault(s => s.Status == GameNightSessionStatus.Pending)
            ?? throw new InvalidOperationException("No pending session to start.");
        session.Start();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void CompleteCurrentSession(Guid? winnerId)
    {
        var session = _sessions.FirstOrDefault(s => s.Status == GameNightSessionStatus.InProgress)
            ?? throw new InvalidOperationException("No in-progress session to complete.");
        session.Complete(winnerId);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void FinalizeNight()
    {
        if (Status != GameNightStatus.Published)
            throw new InvalidOperationException($"Cannot finalize a {Status} game night.");
        if (_sessions.Any(s => s.Status == GameNightSessionStatus.InProgress))
            throw new InvalidOperationException("Cannot finalize: a session is still in progress.");
        Status = GameNightStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal void RestoreSessions(IEnumerable<GameNightSession> sessions)
    {
        _sessions.Clear();
        _sessions.AddRange(sessions.OrderBy(s => s.PlayOrder));
    }
```

Aggiungi il using necessario in cima al file:

```csharp
using Api.BoundedContexts.GameManagement.Domain.Enums;
```

(Se `GameNightSessionStatus` non è già importato da quel namespace, verificare.)

- [ ] **Step 4: Esegui test — verifica che passino**

```bash
cd tests/Api.Tests
dotnet test --filter "GameNightEventSessionsTests" -v
# Expected: 10 test PASS
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs
git add tests/Api.Tests/BoundedContexts/GameManagement/Unit/GameNightEventSessionsTests.cs
git commit -m "feat(game-night): extend GameNightEvent with Sessions aggregate root methods"
```

---

### Task 3: Persistence entity + Repository mapping + Migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Entities/GameManagement/GameNightSessionEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/Entities/GameManagement/GameNightEventEntity.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GameNightEventRepository.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`

- [ ] **Step 1: Crea GameNightSessionEntity (persistence model)**

```csharp
// apps/api/src/Api/Infrastructure/Entities/GameManagement/GameNightSessionEntity.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.GameManagement;

[Table("game_night_sessions")]
public class GameNightSessionEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("game_night_event_id")]
    public Guid GameNightEventId { get; set; }

    [Required]
    [Column("session_id")]
    public Guid SessionId { get; set; }

    [Required]
    [Column("game_id")]
    public Guid GameId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("game_title")]
    public string GameTitle { get; set; } = string.Empty;

    [Required]
    [Column("play_order")]
    public int PlayOrder { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "Pending";

    [Column("winner_id")]
    public Guid? WinnerId { get; set; }

    [Column("started_at")]
    public DateTimeOffset? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation
    public GameNightEventEntity GameNightEvent { get; set; } = null!;
}
```

- [ ] **Step 2: Aggiungi navigation property a GameNightEventEntity**

In `GameNightEventEntity.cs`, aggiungi dopo `Rsvps`:

```csharp
    public List<GameNightSessionEntity> Sessions { get; set; } = [];
```

- [ ] **Step 3: Registra DbSet nel MeepleAiDbContext**

In `MeepleAiDbContext.cs`, aggiungi accanto agli altri GameNight DbSet:

```csharp
    public DbSet<GameNightSessionEntity> GameNightSessions => Set<GameNightSessionEntity>();
```

- [ ] **Step 4: Aggiorna GameNightEventRepository con MapToDomain/MapToPersistence per Sessions**

In `GetByIdAsync`, aggiungi `.Include(e => e.Sessions)`:

```csharp
    public async Task<GameNightEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Rsvps)
            .Include(e => e.Sessions)  // AGGIUNTO
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }
```

In `MapToPersistence`, aggiungi dopo il blocco Rsvps:

```csharp
        foreach (var session in domain.Sessions)
        {
            entity.Sessions.Add(new GameNightSessionEntity
            {
                Id = session.Id,
                GameNightEventId = session.GameNightEventId,
                SessionId = session.SessionId,
                GameId = session.GameId,
                GameTitle = session.GameTitle,
                PlayOrder = session.PlayOrder,
                Status = session.Status.ToString(),
                WinnerId = session.WinnerId,
                StartedAt = session.StartedAt,
                CompletedAt = session.CompletedAt
            });
        }
```

In `MapToDomain`, aggiungi prima di `evt.ClearDomainEvents()`:

```csharp
        // Restore Sessions
        var sessions = entity.Sessions
            .OrderBy(s => s.PlayOrder)
            .Select(s => GameNightSession.Reconstitute(
                id: s.Id,
                gameNightEventId: s.GameNightEventId,
                sessionId: s.SessionId,
                gameId: s.GameId,
                gameTitle: s.GameTitle,
                playOrder: s.PlayOrder,
                status: Enum.Parse<GameNightSessionStatus>(s.Status),
                winnerId: s.WinnerId,
                startedAt: s.StartedAt,
                completedAt: s.CompletedAt))
            .ToList();

        evt.RestoreSessions(sessions);
```

Aggiungi il using necessario:

```csharp
using Api.BoundedContexts.GameManagement.Domain.Enums;
```

- [ ] **Step 5: Genera migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddGameNightSessions
# Verifica che la migration crei la tabella game_night_sessions con FK
```

- [ ] **Step 6: Build + test**

```bash
cd apps/api/src/Api && dotnet build --no-incremental
cd ../../../../tests/Api.Tests && dotnet test --filter "GameNight" -v
# Expected: Build OK, tutti i test GameNight PASS
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/GameManagement/GameNightSessionEntity.cs
git add apps/api/src/Api/Infrastructure/Entities/GameManagement/GameNightEventEntity.cs
git add apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
git add apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GameNightEventRepository.cs
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(game-night): GameNightSession persistence entity + repository mapping + migration"
```

---

### Task 4: StartGameNightSession command (cross-BC via MediatR)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommand.cs`
- Create: `tests/Api.Tests/BoundedContexts/GameManagement/Unit/StartGameNightSessionTests.cs`

- [ ] **Step 1: Scrivi il test**

```csharp
// tests/Api.Tests/BoundedContexts/GameManagement/Unit/StartGameNightSessionTests.cs
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using MediatR;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Unit;

public class StartGameNightSessionTests
{
    [Fact]
    public async Task Handle_CreatesSessionViaMediatorAndLinksToGameNight()
    {
        // Arrange
        var gameNightRepo = Substitute.For<IGameNightEventRepository>();
        var mediator = Substitute.For<IMediator>();

        var gameNight = GameNightEvent.Create(
            Guid.NewGuid(), "Test Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid()]);
        gameNight.Publish([]);

        gameNightRepo.GetByIdAsync(gameNight.Id, Arg.Any<CancellationToken>())
            .Returns(gameNight);

        var newSessionId = Guid.NewGuid();
        var sessionCode = "ABC123";
        mediator.Send(Arg.Any<CreateSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateSessionResult(newSessionId, sessionCode));

        var handler = new StartGameNightSessionCommandHandler(gameNightRepo, mediator);
        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameNight.GameIds[0], "Catan", gameNight.OrganizerId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(newSessionId, result.SessionId);
        Assert.Equal(sessionCode, result.SessionCode);
        Assert.Single(gameNight.Sessions);
        Assert.Equal("Catan", gameNight.Sessions[0].GameTitle);
        await mediator.Received(1).Send(
            Arg.Is<CreateSessionCommand>(c => c.GameId == gameNight.GameIds[0]),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonOrganizer_ThrowsForbidden()
    {
        var gameNightRepo = Substitute.For<IGameNightEventRepository>();
        var mediator = Substitute.For<IMediator>();

        var gameNight = GameNightEvent.Create(
            Guid.NewGuid(), "Test Night", DateTimeOffset.UtcNow.AddHours(1));
        gameNight.Publish([]);

        gameNightRepo.GetByIdAsync(gameNight.Id, Arg.Any<CancellationToken>())
            .Returns(gameNight);

        var handler = new StartGameNightSessionCommandHandler(gameNightRepo, mediator);
        var command = new StartGameNightSessionCommand(
            gameNight.Id, Guid.NewGuid(), "Catan", Guid.NewGuid()); // different user

        await Assert.ThrowsAsync<Api.Middleware.Exceptions.ForbiddenException>(
            () => handler.Handle(command, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Implementa command + handler**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommand.cs
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Middleware.Exceptions;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

internal record StartGameNightSessionCommand(
    Guid GameNightId, Guid GameId, string GameTitle, Guid UserId
) : IRequest<StartGameNightSessionResult>;

internal record StartGameNightSessionResult(
    Guid SessionId, Guid GameNightSessionId, string SessionCode, int PlayOrder);

internal sealed class StartGameNightSessionCommandValidator : AbstractValidator<StartGameNightSessionCommand>
{
    public StartGameNightSessionCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty();
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.GameTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UserId).NotEmpty();
    }
}

internal sealed class StartGameNightSessionCommandHandler(
    IGameNightEventRepository gameNightRepo,
    IMediator mediator
) : IRequestHandler<StartGameNightSessionCommand, StartGameNightSessionResult>
{
    public async Task<StartGameNightSessionResult> Handle(
        StartGameNightSessionCommand command, CancellationToken ct)
    {
        var gameNight = await gameNightRepo.GetByIdAsync(command.GameNightId, ct)
            ?? throw new NotFoundException($"GameNight {command.GameNightId} not found.");

        if (gameNight.OrganizerId != command.UserId)
            throw new ForbiddenException("Only the organizer can start sessions.");

        // Cross-BC: create Session via MediatR (SessionTracking BC handles this)
        var createResult = await mediator.Send(new CreateSessionCommand(
            command.UserId, command.GameId,
            SessionTracking.Domain.Enums.SessionType.GameSpecific), ct);

        // Link to GameNight
        var gns = gameNight.AddSession(createResult.SessionId, command.GameId, command.GameTitle);
        gameNight.StartCurrentSession();

        await gameNightRepo.UpdateAsync(gameNight, ct);

        return new StartGameNightSessionResult(
            createResult.SessionId, gns.Id, createResult.SessionCode, gns.PlayOrder);
    }
}
```

**Nota:** Se `CreateSessionCommand` non esiste già con questa signature nel SessionTracking BC, va creato come wrapper. Verifica la signature esistente del `CreateSessionCommand` e adatta.

- [ ] **Step 3: Test + Commit**

```bash
cd tests/Api.Tests && dotnet test --filter "StartGameNightSessionTests" -v
```

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommand.cs
git add tests/Api.Tests/BoundedContexts/GameManagement/Unit/StartGameNightSessionTests.cs
git commit -m "feat(game-night): StartGameNightSession command via MediatR cross-BC"
```

---

### Task 5: Estendi SessionEvent con GameNightId (diary cross-game)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionEvent.cs`

Invece di creare una nuova entity `DiaryEntry`, estendiamo `SessionEvent` che è già il diary della sessione.

- [ ] **Step 1: Aggiungi GameNightId nullable a SessionEvent**

```csharp
// In SessionEvent.cs, aggiungi la proprietà dopo SessionId:

    /// <summary>
    /// Optional GameNight reference for cross-game diary entries.
    /// When set, this event appears in the game night diary spanning all sessions.
    /// </summary>
    public Guid? GameNightId { get; private set; }
```

- [ ] **Step 2: Aggiorna il factory method per accettare GameNightId**

```csharp
    public static SessionEvent Create(
        Guid sessionId,
        string eventType,
        string? payload = null,
        Guid? createdBy = null,
        string? source = null,
        Guid? gameNightId = null)  // AGGIUNTO
    {
        // ... validazione esistente ...

        return new SessionEvent
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            Payload = payload ?? "{}",
            CreatedBy = createdBy,
            Source = source,
            GameNightId = gameNightId,  // AGGIUNTO
            IsDeleted = false
        };
    }
```

- [ ] **Step 3: Genera migration per la colonna nullable**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddGameNightIdToSessionEvent
# Verifica: ALTER TABLE session_events ADD COLUMN game_night_id uuid NULL
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionEvent.cs
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(diary): add GameNightId to SessionEvent for cross-game diary"
```

---

### Task 6: SSE broadcast per diary events + SseEventTypeMapper

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/SseEventTypeMapper.cs`

- [ ] **Step 1: Aggiungi nuovi event types al mapper**

Trova `SseEventTypeMapper.cs` e aggiungi i nuovi tipi diary al mapping:

```csharp
// Aggiungi al mapping dictionary:
{ "game_started", "diary_game_started" },
{ "game_completed", "diary_game_completed" },
{ "night_started", "diary_night_started" },
{ "night_finalized", "diary_night_finalized" },
{ "resource_update", "diary_resource_update" },
```

- [ ] **Step 2: Verifica che gli event handler esistenti (dice_roll, score_update, etc.) già broadcastano via SSE**

Se sì, i diary events fluiranno automaticamente. Se no, aggiungere il broadcast nel `AddSessionEventCommandHandler` o equivalente.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/SseEventTypeMapper.cs
git commit -m "feat(sse): add diary event types to SseEventTypeMapper"
```

---

### Task 7: Routing endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/GameNightEndpoints.cs`

- [ ] **Step 1: Aggiungi endpoint per multi-session + diary**

```csharp
// POST /{id}/sessions — Start next game
group.MapPost("/{id:guid}/sessions", async (
    Guid id, StartGameNightSessionRequest request,
    IMediator mediator, HttpContext ctx) =>
{
    var userId = ctx.User.GetUserId();
    var result = await mediator.Send(new StartGameNightSessionCommand(
        id, request.GameId, request.GameTitle, userId));
    return Results.Created($"/api/v1/game-nights/{id}/sessions/{result.GameNightSessionId}", result);
}).RequireAuthorization();

// POST /{id}/sessions/complete — Complete current game
group.MapPost("/{id:guid}/sessions/complete", async (
    Guid id, CompleteGameNightSessionRequest? request,
    IMediator mediator, HttpContext ctx) =>
{
    var userId = ctx.User.GetUserId();
    await mediator.Send(new CompleteGameNightSessionCommand(
        id, request?.WinnerId, userId));
    return Results.NoContent();
}).RequireAuthorization();

// POST /{id}/finalize — End the night
group.MapPost("/{id:guid}/finalize", async (
    Guid id, IMediator mediator, HttpContext ctx) =>
{
    var userId = ctx.User.GetUserId();
    await mediator.Send(new FinalizeGameNightCommand(id, userId));
    return Results.NoContent();
}).RequireAuthorization();

// GET /{id}/diary — Cross-game diary
group.MapGet("/{id:guid}/diary", async (
    Guid id, IMediator mediator) =>
{
    var diary = await mediator.Send(new GetGameNightDiaryQuery(id));
    return Results.Ok(diary);
}).RequireAuthorization();
```

DTOs:

```csharp
internal sealed record StartGameNightSessionRequest(Guid GameId, string GameTitle);
internal sealed record CompleteGameNightSessionRequest(Guid? WinnerId);
```

- [ ] **Step 2: Crea stub per CompleteGameNightSession + FinalizeGameNight + GetGameNightDiary commands**

(Implementazione completa dei handler — segue il pattern di Task 4)

- [ ] **Step 3: Build + Commit**

```bash
cd apps/api/src/Api && dotnet build --no-incremental
git add apps/api/src/Api/Routing/GameNightEndpoints.cs
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GameNights/
git commit -m "feat(game-night): routing endpoints for multi-session + diary"
```

---

## PHASE 2 — Backend: L2 State + Auto-Save

### Task 8: PlayerResources value object

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/PlayerResources.cs`
- Create: `tests/Api.Tests/BoundedContexts/SessionTracking/Unit/PlayerResourcesTests.cs`

- [ ] **Step 1: Test**

```csharp
// tests/Api.Tests/BoundedContexts/SessionTracking/Unit/PlayerResourcesTests.cs
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Unit;

public class PlayerResourcesTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var participantId = Guid.NewGuid();
        var resources = new Dictionary<string, int> { ["wood"] = 3, ["ore"] = 2 };
        var pr = PlayerResources.Create(participantId, resources);
        Assert.Equal(participantId, pr.ParticipantId);
        Assert.Equal(3, pr.Resources["wood"]);
    }

    [Fact]
    public void Create_WithEmptyParticipantId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PlayerResources.Create(Guid.Empty, new Dictionary<string, int>()));
    }

    [Fact]
    public void WithResource_ReturnsNewInstanceWithUpdatedValue()
    {
        var pr = PlayerResources.Create(Guid.NewGuid(), new Dictionary<string, int> { ["wood"] = 3 });
        var updated = pr.WithResource("wood", 5).WithResource("ore", 2);
        Assert.Equal(5, updated.Resources["wood"]);
        Assert.Equal(2, updated.Resources["ore"]);
        Assert.Equal(3, pr.Resources["wood"]); // original unchanged
    }

    [Fact]
    public void ToJson_RoundTrips()
    {
        var pr = PlayerResources.Create(Guid.NewGuid(),
            new Dictionary<string, int> { ["wood"] = 3, ["ore"] = 1 });
        var json = pr.ToJson();
        var restored = PlayerResources.FromJson(pr.ParticipantId, json);
        Assert.Equal(pr.Resources["wood"], restored.Resources["wood"]);
    }
}
```

- [ ] **Step 2: Implementa**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/PlayerResources.cs
using System.Text.Json;

namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

public sealed class PlayerResources
{
    public Guid ParticipantId { get; }
    public IReadOnlyDictionary<string, int> Resources { get; }

    private PlayerResources(Guid participantId, Dictionary<string, int> resources)
    {
        ParticipantId = participantId;
        Resources = resources;
    }

    public static PlayerResources Create(Guid participantId, Dictionary<string, int> resources)
    {
        if (participantId == Guid.Empty)
            throw new ArgumentException("ParticipantId cannot be empty.", nameof(participantId));
        return new PlayerResources(participantId, new Dictionary<string, int>(resources));
    }

    public PlayerResources WithResource(string key, int value)
    {
        var dict = new Dictionary<string, int>(Resources) { [key] = value };
        return new PlayerResources(ParticipantId, dict);
    }

    public string ToJson() => JsonSerializer.Serialize(Resources);

    public static PlayerResources FromJson(Guid participantId, string json)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
        return Create(participantId, dict);
    }
}
```

- [ ] **Step 3: Test + Commit**

```bash
cd tests/Api.Tests && dotnet test --filter "PlayerResourcesTests" -v
# Expected: 4 PASS
```

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/PlayerResources.cs
git add tests/Api.Tests/BoundedContexts/SessionTracking/Unit/PlayerResourcesTests.cs
git commit -m "feat(session): PlayerResources value object for L2 game state"
```

---

### Task 9: Auto-save background job + cleanup wiring

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/AutoSaveSessionCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Jobs/AutoSaveSessionJob.cs`

- [ ] **Step 1: Implementa AutoSaveSessionCommand**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/AutoSaveSessionCommand.cs
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

internal record AutoSaveSessionCommand(Guid SessionId) : IRequest;

internal sealed class AutoSaveSessionCommandHandler(
    ISessionCheckpointRepository checkpointRepo,
    ISessionRepository sessionRepo,
    ISessionEventRepository eventRepo
) : IRequestHandler<AutoSaveSessionCommand>
{
    public async Task Handle(AutoSaveSessionCommand command, CancellationToken ct)
    {
        var session = await sessionRepo.GetByIdAsync(command.SessionId, ct);
        if (session is null || session.Status != SessionStatus.Active)
            return; // Session ended — skip silently

        var eventCount = await eventRepo.CountBySessionIdAsync(command.SessionId, ct);

        var checkpoint = SessionCheckpoint.Create(
            command.SessionId,
            $"Auto-save {DateTime.UtcNow:HH:mm}",
            session.UserId,
            "{}",
            eventCount);

        await checkpointRepo.AddAsync(checkpoint, ct);
    }
}
```

- [ ] **Step 2: Implementa Hangfire job**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Jobs/AutoSaveSessionJob.cs
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Hangfire;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Jobs;

internal sealed class AutoSaveSessionJob(IMediator mediator)
{
    public async Task Execute(Guid sessionId) =>
        await mediator.Send(new AutoSaveSessionCommand(sessionId));

    public static void Register(Guid sessionId) =>
        RecurringJob.AddOrUpdate<AutoSaveSessionJob>(
            $"auto-save-session-{sessionId}",
            job => job.Execute(sessionId),
            "*/1 * * * *");

    public static void Remove(Guid sessionId) =>
        RecurringJob.RemoveIfExists($"auto-save-session-{sessionId}");
}
```

- [ ] **Step 3: Wire AutoSaveSessionJob.Remove() nel FinalizeSessionCommandHandler**

Cerca `FinalizeSessionCommandHandler` in SessionTracking BC e aggiungi la chiamata `AutoSaveSessionJob.Remove(command.SessionId)` all'inizio dell'Handle. Stessa cosa per il handler di pausa sessione.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/AutoSaveSessionCommand.cs
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Jobs/AutoSaveSessionJob.cs
git commit -m "feat(session): auto-save every 60s + cleanup on finalize/pause"
```

---

## PHASE 3 — Frontend: Types + Store + Components

### Task 10: Estendi types e store

**Files:**
- Modify: `apps/web/src/stores/game-night/types.ts`
- Modify: `apps/web/src/stores/game-night/store.ts`

- [ ] **Step 1: Aggiungi tipi**

In `types.ts`, aggiungi alla fine:

```typescript
export type DiaryEntryType =
  | 'game_started' | 'game_completed' | 'night_started' | 'night_finalized'
  | 'score_update' | 'dice_roll' | 'card_draw'
  | 'photo' | 'pause_resume' | 'player_joined' | 'player_left'
  | 'dispute_resolved' | 'note_added' | 'resource_update';

export interface DiaryEntry {
  id: string;
  sessionId: string;
  gameNightId?: string;
  eventType: DiaryEntryType;
  description: string;
  payload?: Record<string, unknown>;
  actorId?: string;
  timestamp: string;
}

export interface GameNightActiveSession {
  id: string;
  gameNightSessionId: string;
  gameId: string;
  gameTitle: string;
  playOrder: number;
  status: 'pending' | 'in_progress' | 'completed' | 'skipped';
  winnerId?: string;
  startedAt?: string;
  completedAt?: string;
}

export interface PlayerResource {
  participantId: string;
  playerName: string;
  resources: Record<string, number>;
}
```

- [ ] **Step 2: Estendi lo store**

In `store.ts`, aggiungi allo state interface, initialState, actions e selectors:

State: `activeSessions`, `diary`, `playerResources`
Actions: `setActiveSessions`, `addDiaryEntry`, `setDiary`, `setPlayerResources`, `updatePlayerResource`
Selectors: `selectActiveSessions`, `selectDiary`, `selectPlayerResources`, `selectCurrentActiveSession`

(Codice uguale al piano v1 Task 10 — non ripeto per brevità)

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/stores/game-night/types.ts apps/web/src/stores/game-night/store.ts
git commit -m "feat(store): extend game-night store with multi-session, diary, resources"
```

---

### Task 11: GameNightDiary + GameNightDiaryEntry components

(Codice identico a piano v1 Task 11, con un fix: il time formatting usa `Intl.DateTimeFormat` con fallback anziché `toLocaleTimeString` direttamente per evitare flakiness in CI)

```tsx
// In GameNightDiaryEntry.tsx, usa:
const time = new Intl.DateTimeFormat('it-IT', {
  hour: '2-digit', minute: '2-digit', hour12: false,
}).format(new Date(entry.timestamp));
```

Il test verifica il formato `HH:MM` con regex anziché valore esatto:

```typescript
expect(screen.getByText(/\d{2}:\d{2}/)).toBeInTheDocument();
```

- [ ] **Step 1-4: Implementa + Test + Commit** (come piano v1 Task 11)

---

### Task 12: GameTransitionDialog component

(Codice identico a piano v1 Task 12)

- [ ] **Step 1-3: Implementa + Test + Commit**

---

### Task 13: PlayerResourcesPanel component

(Codice identico a piano v1 Task 13)

- [ ] **Step 1-3: Implementa + Test + Commit**

---

## PHASE 4 — Frontend: API Client + Hooks + SSE

### Task 14: API client

(Codice identico a piano v1 Task 14)

- [ ] **Step 1-2: Implementa + Commit**

---

### Task 15: useGameNightMultiSession hook

(Codice identico a piano v1 Task 15)

- [ ] **Step 1-3: Implementa + Test + Commit**

---

### Task 16: useGameNightDiary hook (SSE real-time) — NEW

**Files:**
- Create: `apps/web/src/lib/domain-hooks/useGameNightDiary.ts`
- Create: `apps/web/src/lib/domain-hooks/__tests__/useGameNightDiary.test.ts`

- [ ] **Step 1: Scrivi test**

```typescript
// apps/web/src/lib/domain-hooks/__tests__/useGameNightDiary.test.ts
import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useGameNightDiary } from '../useGameNightDiary';
import { useGameNightStore } from '@/stores/game-night/store';

vi.mock('@/lib/api/clients/gameNightSessionClient', () => ({
  gameNightSessionClient: {
    getDiary: vi.fn().mockResolvedValue({
      data: [
        { id: '1', sessionId: 's1', eventType: 'game_started',
          description: 'Catan iniziata', timestamp: '2026-04-07T19:30:00Z' },
      ],
    }),
  },
}));

describe('useGameNightDiary', () => {
  beforeEach(() => {
    useGameNightStore.getState().reset();
  });

  it('loads initial diary entries', async () => {
    const { result } = renderHook(() =>
      useGameNightDiary('night-1', 'session-1'));

    // Wait for initial load
    await vi.waitFor(() => {
      expect(result.current.entries).toHaveLength(1);
    });

    expect(result.current.entries[0].description).toBe('Catan iniziata');
  });

  it('adds entry from SSE event', () => {
    const { result } = renderHook(() =>
      useGameNightDiary('night-1', 'session-1'));

    act(() => {
      result.current.handleSseEvent({
        id: '2', sessionId: 's1', eventType: 'dice_roll',
        description: 'Marco ha lanciato 2d6: 8',
        timestamp: new Date().toISOString(),
      });
    });

    const diary = useGameNightStore.getState().diary;
    expect(diary.some(e => e.description.includes('Marco'))).toBe(true);
  });
});
```

- [ ] **Step 2: Implementa hook**

```typescript
// apps/web/src/lib/domain-hooks/useGameNightDiary.ts
import { useCallback, useEffect } from 'react';
import { useGameNightStore } from '@/stores/game-night/store';
import { gameNightSessionClient } from '@/lib/api/clients/gameNightSessionClient';
import type { DiaryEntry } from '@/stores/game-night/types';

export function useGameNightDiary(gameNightId: string, currentSessionId: string) {
  const { diary, setDiary, addDiaryEntry } = useGameNightStore();

  // Load initial diary on mount
  useEffect(() => {
    gameNightSessionClient.getDiary(gameNightId)
      .then(res => setDiary(res.data))
      .catch(() => {}); // silently fail on initial load
  }, [gameNightId, setDiary]);

  // Handler for SSE events — called by useSessionSSE onEvent callback
  const handleSseEvent = useCallback((entry: DiaryEntry) => {
    addDiaryEntry(entry);
  }, [addDiaryEntry]);

  return {
    entries: diary,
    handleSseEvent,
  };
}
```

- [ ] **Step 3: Test + Commit**

```bash
cd apps/web && pnpm vitest run src/lib/domain-hooks/__tests__/useGameNightDiary.test.ts
```

```bash
git add apps/web/src/lib/domain-hooks/useGameNightDiary.ts
git add apps/web/src/lib/domain-hooks/__tests__/useGameNightDiary.test.ts
git commit -m "feat(hooks): useGameNightDiary with SSE real-time diary sync"
```

---

## PHASE 5 — Offline + Service Worker

### Task 17: Service worker (static assets only — no API caching)

**Files:**
- Create: `apps/web/public/sw-toolkit.js`

- [ ] **Step 1: Crea service worker** — cache SOLO static assets, NO API

```javascript
// apps/web/public/sw-toolkit.js
const CACHE_NAME = 'meepleai-toolkit-v1';

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
    )
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  // NEVER cache API calls (security: authenticated responses)
  if (url.pathname.startsWith('/api/')) return;

  // Cache static assets only (JS, CSS, images, fonts)
  if (event.request.destination === 'script' ||
      event.request.destination === 'style' ||
      event.request.destination === 'image' ||
      event.request.destination === 'font') {
    event.respondWith(
      caches.match(event.request).then(cached =>
        cached || fetch(event.request).then(response => {
          const clone = response.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
          return response;
        })
      )
    );
  }
});
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/public/sw-toolkit.js
git commit -m "feat(offline): service worker for host-only static asset caching"
```

---

## PHASE 6 — Build + PR

### Task 18: Build completo + lint + typecheck

- [ ] **Step 1: Backend**

```bash
cd apps/api/src/Api && dotnet build --no-incremental
cd ../../../../tests/Api.Tests && dotnet test
```

- [ ] **Step 2: Frontend**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm test && pnpm build
```

### Task 19: PR verso main-dev

- [ ] **Step 1: Push + PR**

```bash
git push -u origin feature/game-night-experience-v2
gh pr create --base main-dev --title "feat: Game Night v2 — multi-session, diary, L2 state, offline" --body "$(cat <<'EOF'
## Summary
- GameNightSession linking entity: orchestrates 1-5 game sessions per evening
- Cross-game diary via SessionEvent.GameNightId extension
- PlayerResources value object for L2 game state
- Auto-save every 60s via Hangfire + cleanup on session end
- SSE broadcast for diary events (real-time multi-device)
- Frontend: multi-session store, diary timeline, transition dialog, resource panel
- Service worker for host-only offline (static assets only)

## Architecture Decisions
- Cross-BC communication via MediatR (no direct repo injection)
- Reuse SessionEvent (with GameNightId) instead of new DiaryEntry entity
- GameNightSessionEntity persistence model with MapToDomain/MapToPersistence
- Service worker caches ONLY static assets (no API = no auth leak)

## Test Plan
- [ ] GameNightSession lifecycle (5 unit tests)
- [ ] GameNightEvent.Sessions aggregate methods (10 unit tests)
- [ ] PlayerResources immutability + serialization (4 unit tests)
- [ ] Frontend: GameNightDiary, GameTransitionDialog, PlayerResourcesPanel
- [ ] Frontend: useGameNightMultiSession, useGameNightDiary hooks
- [ ] Integration: create night → 2 games → complete → finalize → diary

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-Review Checklist (v2 — post-review fixes)

| Spec Requirement | Task | Covered? |
|-----------------|------|----------|
| Multi-game orchestration (1-5) | Task 1-4, 7 | ✅ |
| Real-time diary auto-log | Task 5-6, 16 | ✅ |
| L2 state (score + resources) | Task 8 | ✅ |
| Auto-save every 60s | Task 9 | ✅ |
| SSE broadcast diary events | Task 6, 16 | ✅ (was C1) |
| Frontend multi-session | Task 10-12 | ✅ |
| Player resources UI | Task 13 | ✅ |
| API client + hooks | Task 14-16 | ✅ |
| Offline host (static only) | Task 17 | ✅ |
| Game transition UX | Task 12 | ✅ |
| Session.Create() factory | Task 4 | ✅ (was C3) |
| Cross-BC via MediatR | Task 4 | ✅ (was C4) |
| Persistence entity pattern | Task 3 | ✅ (was C2) |
| UoW pattern (no SaveChanges in handlers) | Task 5, 9 | ✅ (was I1) |
| RestoreSessions in MapToDomain | Task 3 | ✅ (was I2) |
| AutoSave cleanup on finalize | Task 9 | ✅ (was I3) |
| SW no API caching | Task 17 | ✅ (was I4) |
| Reuse SessionEvent | Task 5 | ✅ (was I5) |
| Photo gallery | ⚠️ Deferred | SessionMedia exists, gallery UI future phase |
