# SP2 — SSE nativo `/live-sessions/{id}/stream` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Esporre uno stream SSE nativo keyed su `LiveGameSession.Id` (`GET /api/v1/live-sessions/{id}/stream`) che aggrega in un Canonical Event Model i domain-event di LiveGameSession + chat-RAG (con citazioni) + diary/media del companion SessionTracking, riusando l'infra `SessionBroadcastService` via ACL e deprecando `/game-sessions/{id}/stream/v2`.

**Architecture:** Endpoint GameManagement → ACL `ILiveSessionStreamGateway` (Application) → impl Infrastructure che risolve `TrackingSessionId` (companion SP0) e delega a `ISessionBroadcastService` keyed sul companion. Un forwarder `INotificationHandler<>` traduce i domain-event di LiveGameSession in eventi canonici neutri e li broadcasta via gateway. `SessionBroadcastService` è migliorato con sequence monotono Redis INCR (fallback in-process) + replay durevole backed-Redis, in modo backward-compatible per lo stream legacy.

**Tech Stack:** .NET 9 ASP.NET Minimal APIs + MediatR + EF Core, StackExchange.Redis (`IConnectionMultiplexer` già in DI), xUnit + Testcontainers; FE Next.js + TypeScript + Vitest + Playwright.

## Global Constraints

- **Decisione owner (Opzione A)**: enhancement sequence/replay sull'infra condivisa `SessionBroadcastService`; ogni modifica **backward-compatible** per lo stream legacy `/game-sessions/{id}/stream/v2` (i test legacy sono la guardia).
- **Invariante ADR-083**: `GameManagement.Application` dipende SOLO da `ILiveSessionStreamGateway` (no import di tipi `SessionTracking`); l'impl in `GameManagement.Infrastructure` può riferire l'infra SessionTracking — pattern identico a `CompanionSessionService` (SP0). `GameManagement` **non importa MAI** `KnowledgeBase`.
- **CQRS**: endpoint usano solo `IMediator.Send()` / handler; nessuna service injection diretta negli endpoint.
- **Redis opzionale**: ogni feature Redis (INCR seq, replay durevole) ha fallback in-process; no regressione quando Redis assente (dev/test single-instance).
- **Eccezioni**: `ConflictException`→409, `NotFoundException`→404; mai `InvalidOperationException` (500).
- **Companion nullable**: `LiveGameSession.TrackingSessionId` è `Guid?` (non-null solo con GameId). Stream su sessione senza companion → `200` vuoto-ma-valido con `X-Warning-Code: stream-not-linked`, MAI 404/500.
- **Naming**: tipi-evento canonici `session:<kind>` (kebab) allineati alla tassonomia FE `sse-events.ts`.

---

## File Structure

**Backend (nuovi):**
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/ILiveSessionStreamGateway.cs` — ACL interface + `LiveSessionStreamEvent` record.
- `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Services/LiveSessionStreamGateway.cs` — impl (resolve companion + delega a `ISessionBroadcastService`).
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/LiveSessionStreamForwarder.cs` — `INotificationHandler<>` per i domain-event.
- `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/ISessionSequenceProvider.cs` (+ impl `RedisSessionSequenceProvider`) — sequence monotono.

**Backend (modificati):**
- `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` — nuova route `/{sessionId}/stream` (dopo `:295`, prima di `:302`).
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/ISessionBroadcastService.cs` — aggiungi `PublishEnvelopeAsync`.
- `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/SessionBroadcastService.cs` — seq monotono + replay durevole; refactor `PublishAsync`→`PublishEnvelopeAsync`.
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs` — broadcast `session:chat` con `citations[]` sul canale companion.
- `apps/api/src/Api/Routing/SessionTracking/SessionQueryEndpoints.cs` — header `Deprecation`/`Sunset` su `/stream/v2`.
- `apps/api/src/Api/Extensions/*ServiceExtensions.cs` — DI dei nuovi servizi.

**Frontend (modificati):**
- `apps/web/src/lib/session-live/use-session-live-stream.ts:214` · `lib/domain-hooks/useWidgetSync.ts:103` · `useTurnOrder.ts:200` · `useWhiteboardTool.ts:230` — URL → `/live-sessions/{id}/stream`.
- `apps/web/src/lib/session-live/sse-events.ts` — tipi canonici (riconciliazione).
- Test: `use-session-live-stream.test.ts:149` · `useTurnOrder.test.ts:345` · `useWidgetSync.test.ts:98` · E2E `session-live.smoke.spec.ts:128`.

> **Reading list per gli implementer** (REUSE — leggi prima di codificare): `ISessionBroadcastService.cs` (firme), `SessionBroadcastService.cs:63-200` (Subscribe/Publish + buffer), `SessionQueryEndpoints.cs:320-418` (loop SSE da replicare), `LiveSessionEndpoints.cs:288-310` (pattern authz GET LiveSession), `CreateLiveSessionCommandHandler.cs:50-60` + `ICompanionSessionService.cs` (pattern ACL), `ChatWithSessionAgentCommandHandler.cs:546-564` (CitationDto).

---

## Task 1: ACL `ILiveSessionStreamGateway` + canonical event type

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/ILiveSessionStreamGateway.cs`
- Test: `apps/api/tests/Api.Tests/GameManagement/LiveSessionStreamGatewayContractTests.cs` (verifica via mock che l'interfaccia esista con le firme attese)

**Interfaces:**
- Produces:
  - `record LiveSessionStreamEvent(string Type, object Data)` — evento canonico neutro (no tipi SessionTracking/KB).
  - `interface ILiveSessionStreamGateway`:
    - `IAsyncEnumerable<LiveSessionStreamEvent> SubscribeAsync(Guid liveSessionId, Guid userId, string? lastEventId, CancellationToken ct)`
    - `Task BroadcastAsync(Guid liveSessionId, LiveSessionStreamEvent evt, CancellationToken ct = default)`

- [ ] **Step 1: Write the failing test** — assert l'interfaccia è implementabile con quelle firme (compile-time contract via fake impl in test).

```csharp
public class LiveSessionStreamGatewayContractTests
{
    private sealed class FakeGateway : ILiveSessionStreamGateway
    {
        public IAsyncEnumerable<LiveSessionStreamEvent> SubscribeAsync(Guid id, Guid u, string? l, CancellationToken ct)
            => AsyncEnumerable.Empty<LiveSessionStreamEvent>();
        public Task BroadcastAsync(Guid id, LiveSessionStreamEvent e, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public void CanonicalEvent_carries_type_and_data()
    {
        var e = new LiveSessionStreamEvent("session:score", new { playerId = Guid.NewGuid(), value = 3 });
        Assert.Equal("session:score", e.Type);
        Assert.NotNull(e.Data);
    }
}
```

- [ ] **Step 2: Run test, verify it fails** — `dotnet test --filter LiveSessionStreamGatewayContractTests` → FAIL (type not defined).
- [ ] **Step 3: Create the interface + record** in the Application/Services file (namespace `Api.BoundedContexts.GameManagement.Application.Services`).
- [ ] **Step 4: Run test, verify PASS.**
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T1 ILiveSessionStreamGateway ACL contract"`

---

## Task 2: `SessionBroadcastService.PublishEnvelopeAsync` (additive, backward-compat)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/ISessionBroadcastService.cs` (add method)
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/SessionBroadcastService.cs` (refactor `PublishAsync` to delegate)
- Test: `apps/api/tests/Api.Tests/SessionTracking/SessionBroadcastServiceTests.cs` (estendi l'esistente, se assente crealo)

**Interfaces:**
- Consumes: `SseEventEnvelope`, `EventVisibility` (esistenti).
- Produces: `Task PublishEnvelopeAsync(Guid sessionId, SseEventEnvelope envelope, EventVisibility visibility = default, CancellationToken ct = default)` su `ISessionBroadcastService`. L'`Id` dell'envelope passato è ignorato/sovrascritto dal servizio (centralizza l'assegnazione id — preparatorio a T4).

- [ ] **Step 1: Write failing test** — pubblicare un envelope esplicito raggiunge un subscriber con lo `EventType` fornito (non derivato dal mapper).

```csharp
[Fact]
public async Task PublishEnvelopeAsync_delivers_explicit_event_type()
{
    var svc = new SessionBroadcastService(NullLogger<SessionBroadcastService>.Instance);
    var sid = Guid.NewGuid(); var uid = Guid.NewGuid();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    var received = new List<SseEventEnvelope>();
    var sub = Task.Run(async () => {
        await foreach (var e in svc.SubscribeAsync(sid, uid, null, cts.Token)) { received.Add(e); break; }
    });
    await Task.Delay(100);
    await svc.PublishEnvelopeAsync(sid,
        new SseEventEnvelope { Id = "ignored", EventType = "session:score", Data = new { value = 7 } });
    await sub;
    Assert.Single(received);
    Assert.Equal("session:score", received[0].EventType);
}
```

- [ ] **Step 2: Run, verify FAIL** (method missing).
- [ ] **Step 3: Implement** — add `PublishEnvelopeAsync` to interface; in impl, factor the body of `PublishAsync` (`:138-155`) so that `PublishAsync` builds the envelope (via `SseEventTypeMapper`) then calls `PublishEnvelopeAsync`. `PublishEnvelopeAsync` re-assigns `Id` (keep `{sessionId:N}-{Ticks:x}` for now — T4 replaces it), then `PublishToRedisAsync` (if subscriber) + `PublishLocally`.
- [ ] **Step 4: Run new test + existing SessionBroadcastService tests → PASS** (backward-compat guard).
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T2 PublishEnvelopeAsync (explicit event type, backward-compat)"`

---

## Task 3: `LiveSessionStreamGateway` impl (resolve companion + delega)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Services/LiveSessionStreamGateway.cs`
- Modify: DI registration (`apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs` o l'extension GameManagement) — `services.AddScoped<ILiveSessionStreamGateway, LiveSessionStreamGateway>()`.
- Test: `apps/api/tests/Api.Tests/GameManagement/LiveSessionStreamGatewayTests.cs`

**Interfaces:**
- Consumes: `ILiveSessionRepository` (per risolvere `LiveGameSession.TrackingSessionId`), `ISessionBroadcastService` (`PublishEnvelopeAsync`/`SubscribeAsync`).
- Produces: impl di `ILiveSessionStreamGateway`. `BroadcastAsync`: risolve companion id; se null → no-op (log debug); altrimenti `PublishEnvelopeAsync(companionId, new SseEventEnvelope{ EventType=evt.Type, Data=evt.Data })`. `SubscribeAsync`: risolve companion id; se null → `AsyncEnumerable.Empty`; altrimenti delega a `ISessionBroadcastService.SubscribeAsync(companionId, …)` mappando `SseEventEnvelope`→`LiveSessionStreamEvent`.

- [ ] **Step 1: Write failing test** — `BroadcastAsync` su sessione con companion chiama `PublishEnvelopeAsync(companionId, envelope con EventType=evt.Type)`; su sessione senza companion → nessuna pubblicazione.

```csharp
[Fact]
public async Task BroadcastAsync_resolves_companion_and_publishes()
{
    var liveId = Guid.NewGuid(); var companionId = Guid.NewGuid();
    var repo = new Mock<ILiveSessionRepository>();
    repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: companionId));
    var broadcast = new Mock<ISessionBroadcastService>();
    var gw = new LiveSessionStreamGateway(repo.Object, broadcast.Object, NullLogger<LiveSessionStreamGateway>.Instance);

    await gw.BroadcastAsync(liveId, new LiveSessionStreamEvent("session:score", new { value = 3 }));

    broadcast.Verify(b => b.PublishEnvelopeAsync(companionId,
        It.Is<SseEventEnvelope>(e => e.EventType == "session:score"),
        It.IsAny<EventVisibility>(), It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task BroadcastAsync_noop_when_no_companion()
{
    var liveId = Guid.NewGuid();
    var repo = new Mock<ILiveSessionRepository>();
    repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: null));
    var broadcast = new Mock<ISessionBroadcastService>();
    var gw = new LiveSessionStreamGateway(repo.Object, broadcast.Object, NullLogger<LiveSessionStreamGateway>.Instance);

    await gw.BroadcastAsync(liveId, new LiveSessionStreamEvent("session:score", new { value = 3 }));

    broadcast.Verify(b => b.PublishEnvelopeAsync(It.IsAny<Guid>(), It.IsAny<SseEventEnvelope>(),
        It.IsAny<EventVisibility>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

> `FakeLiveSessionWith` usa il factory/`Reconstitute` reale di `LiveGameSession` — leggi `LiveGameSession.cs:160-245` per la firma. Se la costruzione in test è ostica, esponi un helper di test esistente o usa `Reconstitute`.

- [ ] **Step 2: Run, verify FAIL.**
- [ ] **Step 3: Implement** la classe (namespace `…GameManagement.Infrastructure.Services`). Resolve via `ILiveSessionRepository.GetByIdAsync`. Registra in DI.
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T3 LiveSessionStreamGateway resolves companion + delegates"`

---

## Task 4: Endpoint `GET /api/v1/live-sessions/{sessionId}/stream`

**Files:**
- Modify: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` (route dopo `:295`, handler statico privato nello stile esistente)
- Test: `apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionStreamEndpointTests.cs` (Testcontainers — segui il pattern degli integration test esistenti; per lo streaming verifica header + primo evento/heartbeat)

**Interfaces:**
- Consumes: `ILiveSessionStreamGateway.SubscribeAsync`, authz su `LiveGameSession.Id` (pattern `LiveSessionEndpoints` GET `:302`).
- Produces: endpoint SSE che scrive `text/event-stream`, heartbeat 30s, `Last-Event-ID` via query `lastEventId`, header `X-Warning-Code: stream-not-linked` quando `TrackingSessionId` è null.

- [ ] **Step 1: Write failing integration test** — (a) utente non autorizzato → 403/404; (b) sessione con companion → risposta `200` `Content-Type: text/event-stream`; (c) sessione senza companion (no GameId) → `200` + header `X-Warning-Code: stream-not-linked`, nessun evento di dominio.

```csharp
[Fact]
public async Task Stream_returns_event_stream_for_authorized_user()
{
    var (client, sessionId) = await CreateLiveSessionWithCompanionAsync();
    using var resp = await client.GetAsync(
        $"/api/v1/live-sessions/{sessionId}/stream",
        HttpCompletionOption.ResponseHeadersRead);
    Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    Assert.Equal("text/event-stream", resp.Content.Headers.ContentType?.MediaType);
}

[Fact]
public async Task Stream_warns_when_session_has_no_companion()
{
    var (client, sessionId) = await CreateLiveSessionWithoutGameIdAsync();
    using var resp = await client.GetAsync(
        $"/api/v1/live-sessions/{sessionId}/stream",
        HttpCompletionOption.ResponseHeadersRead);
    Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    Assert.Contains("stream-not-linked", resp.Headers.GetValues("X-Warning-Code"));
}
```

- [ ] **Step 2: Run, verify FAIL** (404 route missing).
- [ ] **Step 3: Implement** — registra `liveSessions.MapGet("/{sessionId:guid}/stream", HandleStreamAsync).RequireAuthenticatedUser()`. Nell'handler: authz (carica LiveGameSession, verifica partecipazione come negli altri handler); set header SSE (`Content-Type`, `Cache-Control: no-cache`, `X-Accel-Buffering: no`); se `TrackingSessionId == null` → set `X-Warning-Code: stream-not-linked` e tieni la connessione con soli heartbeat; altrimenti `await foreach` su `gateway.SubscribeAsync(...)` scrivendo `id:`/`event:`/`data:` + heartbeat 30s. **Replica il loop di** `SessionQueryEndpoints.cs:320-418`.
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T4 native /live-sessions/{id}/stream endpoint"`

---

## Task 5: Forwarder `INotificationHandler<>` per i domain-event LiveGameSession

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/LiveSessionStreamForwarder.cs`
- Test: `apps/api/tests/Api.Tests/GameManagement/LiveSessionStreamForwarderTests.cs`

**Interfaces:**
- Consumes: `ILiveSessionStreamGateway.BroadcastAsync`, i domain-event: `LiveSessionScoreRecordedEvent` (`SessionId,PlayerId,Round,Dimension,Value`), `LiveSessionTurnAdvancedEvent` (`SessionId,NewTurnIndex,CurrentPlayerId`), `LiveSessionPhaseAdvancedEvent` (`SessionId,TurnIndex,NewPhaseIndex,PhaseName?,TotalPhases`), `LiveSessionPlayerAddedEvent`, `LiveSessionPlayerRemovedEvent`, `LiveSessionPausedEvent`, `LiveSessionResumedEvent`, `LiveSessionCompletedEvent`.
- Produces: per ogni evento, `BroadcastAsync(evt.SessionId, new LiveSessionStreamEvent("session:<kind>", dto))`. Mapping tipo: score→`session:score`, turn→`session:turn`, phase→`session:phase`, playerAdded→`session:player-join`, playerRemoved→`session:player-leave`, paused→`session:pause`, resumed→`session:resume`, completed→`session:endgame`.

- [ ] **Step 1: Write failing test** — `Handle(LiveSessionScoreRecordedEvent)` chiama `BroadcastAsync(sessionId, evt con Type "session:score" e Data contenente playerId/value)`.

```csharp
[Fact]
public async Task Forwards_score_event_as_session_score()
{
    var gw = new Mock<ILiveSessionStreamGateway>();
    var handler = new LiveSessionStreamForwarder(gw.Object, NullLogger<LiveSessionStreamForwarder>.Instance);
    var sid = Guid.NewGuid(); var pid = Guid.NewGuid();
    await handler.Handle(new LiveSessionScoreRecordedEvent(sid, pid, round: 1, dimension: "vp", value: 5), default);
    gw.Verify(g => g.BroadcastAsync(sid,
        It.Is<LiveSessionStreamEvent>(e => e.Type == "session:score"),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

> Implementa come classe unica con più `INotificationHandler<T>` (o handler separati per evento). Verifica le firme reali degli event record in `…GameManagement/Domain/Events/*.cs`.

- [ ] **Step 2: Run, verify FAIL.**
- [ ] **Step 3: Implement** tutti i mapping. DTO `Data` anonimo o record dedicato con i campi che il FE consuma (cfr. `sse-events.ts` payload shapes — vedi Task 9).
- [ ] **Step 4: Run → PASS** (un test per ciascun tipo, almeno score/turn/phase/player/endgame).
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T5 domain-event forwarder → canonical stream events"`

---

## Task 6: Sequence monotono persistente (Redis INCR + fallback)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/ISessionSequenceProvider.cs` + `RedisSessionSequenceProvider.cs`
- Modify: `SessionBroadcastService.cs` (id generation in `PublishEnvelopeAsync`)
- Modify: DI registration
- Test: `apps/api/tests/Api.Tests/SessionTracking/SessionSequenceProviderTests.cs`

**Interfaces:**
- Produces: `interface ISessionSequenceProvider { Task<long> NextAsync(Guid sessionId, CancellationToken ct); }`. Impl: `IConnectionMultiplexer?` → Redis `INCR meepleai:session:seq:{id}` (con TTL); fallback `ConcurrentDictionary<Guid,long>` + `Interlocked.Increment` quando Redis assente.
- Envelope `Id` diventa `$"{sessionId:N}-{seq:D20}"` (monotono, ordinabile, parsabile per replay `id>N`).

- [ ] **Step 1: Write failing test** — `NextAsync` ritorna valori strettamente crescenti per la stessa sessione (fallback in-process, no Redis); sessioni diverse hanno contatori indipendenti.

```csharp
[Fact]
public async Task NextAsync_is_strictly_monotonic_per_session()
{
    var p = new RedisSessionSequenceProvider(redis: null, NullLogger<RedisSessionSequenceProvider>.Instance);
    var sid = Guid.NewGuid();
    var a = await p.NextAsync(sid, default);
    var b = await p.NextAsync(sid, default);
    Assert.True(b > a);
}
```

- [ ] **Step 2: Run, verify FAIL.**
- [ ] **Step 3: Implement** provider; inietta in `SessionBroadcastService`; in `PublishEnvelopeAsync` sostituisci `Id = $"{sessionId:N}-{Ticks:x}"` con `Id = $"{sessionId:N}-{seq:D20}"` via provider. **Backward-compat**: il replay `GetEventsSince(lastEventId,…)` deve confrontare per ordine di sequenza — aggiorna il parsing in `SessionSubscriptionPool` se confronta stringhe (leggi `:439-497`).
- [ ] **Step 4: Run nuovo test + TUTTI i test legacy SessionBroadcastService → PASS** (guardia backward-compat dello stream `/stream/v2`).
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T6 monotonic Redis INCR sequence (fallback in-process)"`

---

## Task 7: Replay durevole backed-Redis (cross-instance)

**Files:**
- Modify: `SessionBroadcastService.cs` (+ eventuale `SessionSubscriptionPool`) — replay buffer su Redis list capped quando Redis presente; fallback `CircularEventBuffer` in-process.
- Test: `apps/api/tests/Api.Tests/SessionTracking/SessionBroadcastReplayTests.cs`

**Interfaces:**
- Consumes: `IConnectionMultiplexer?`, `ISessionSequenceProvider`.
- Produces: replay `id>lastEventId` servito dal buffer durevole (Redis) così che una connessione su istanza diversa recuperi gli eventi mancati. Cap = `EventBufferSize` (100) per sessione.

- [ ] **Step 1: Write failing test** — pubblica 3 eventi, riconnetti con `lastEventId` = id del 1° → ricevi solo il 2° e 3° (no dup, no gap), in ordine di sequenza. (Con Redis assente, il test verifica il fallback in-process; con Testcontainers Redis, il path durevole.)

```csharp
[Fact]
public async Task Reconnect_replays_only_events_after_lastEventId()
{
    var svc = NewBroadcastService(); var sid = Guid.NewGuid(); var uid = Guid.NewGuid();
    var ids = new List<string>();
    await PublishCapturingIds(svc, sid, count: 3, ids);
    var replayed = await CollectReplayAsync(svc, sid, uid, lastEventId: ids[0]);
    Assert.Equal(new[] { ids[1], ids[2] }, replayed.Select(e => e.Id).ToArray());
}
```

- [ ] **Step 2: Run, verify FAIL** (cross-instance replay non durevole oggi).
- [ ] **Step 3: Implement** replay durevole; mantieni in-process come fallback. Idempotenza: dedup-by-id sul client già garantita; lato server assicura ordine per sequenza.
- [ ] **Step 4: Run → PASS** + test legacy → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T7 durable Redis-backed replay buffer"`

---

## Task 8: Citazioni nell'evento `session:chat`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs` — al completamento, broadcast `session:chat` con `citations[]` sul canale del companion della LiveGameSession (via `ILiveSessionStreamGateway.BroadcastAsync`).
- Test: `apps/api/tests/Api.Tests/KnowledgeBase/ChatSessionAgentBroadcastTests.cs`

**Interfaces:**
- Consumes: `ILiveSessionStreamGateway.BroadcastAsync` (KnowledgeBase → GameManagement è già una dipendenza stabilita e aciclica — il chat handler dipende già da `ILiveSessionRepository`). `CitationDto` (esistente, `:546-553`).
- Produces: evento canonico `session:chat` `{ messageId, senderId, content, visibility, citations[] }` dove `citations` mappa `CitationDto` tier-aware (`SnippetPreview` solo se `CopyrightTier==Full`, altrimenti `ParaphrasedSnippet`).

- [ ] **Step 1: Write failing test** — al completamento di una chat groundabile, viene broadcastato `session:chat` con `citations` non vuoto; tier≠Full → niente snippet verbatim; non-grounded → `citations: []`.

```csharp
[Fact]
public async Task Chat_broadcasts_session_chat_with_citations()
{
    var gw = new Mock<ILiveSessionStreamGateway>();
    // ... arrange handler con gw + retrieval che ritorna 1 citation Full ...
    // act: esegui lo stream completo
    gw.Verify(g => g.BroadcastAsync(It.IsAny<Guid>(),
        It.Is<LiveSessionStreamEvent>(e => e.Type == "session:chat"),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

- [ ] **Step 2: Run, verify FAIL.**
- [ ] **Step 3: Implement** — dopo aver prodotto `StreamingComplete.Citations`, costruisci il DTO chat + `citations[]` e `BroadcastAsync(liveSessionId, …)`. `liveSessionId` = `agentSession.GameSessionId` (già risolto dal handler). Non introdurre import GameManagement→KnowledgeBase (il gateway è iniettato in KnowledgeBase, non il contrario).
- [ ] **Step 4: Run → PASS** (3 scenari: Full, tier-protetto, non-grounded).
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T8 chat-RAG citations on session:chat stream event"`

---

## Task 9: Riconciliazione tassonomia FE ↔ Canonical Event Model

**Files:**
- Modify: `apps/web/src/lib/session-live/sse-events.ts` — assicura che i tipi canonici BE (`session:score|turn|phase|player-join|player-leave|pause|resume|endgame|chat|diary|media|heartbeat`) siano tutti nel union + `SESSION_EVENT_TYPES`.
- Modify: `apps/web/src/lib/session-live/parse-sse-event.ts` — parser per `session:phase` e `session:media` se mancanti.
- Test: `apps/web/src/lib/session-live/__tests__/parse-sse-event.test.ts` (estendi)

**Interfaces:**
- Consumes: payload canonici prodotti da Task 5/8.
- Produces: FE parser che mappa ogni tipo canonico a `SessionEvent`.

- [ ] **Step 1: Write failing test** — `parseSseEvent('session:phase', json, sid)` ritorna un evento tipizzato; `'session:media'` idem.

```typescript
it('parses session:phase', () => {
  const e = parseSseEvent('session:phase', JSON.stringify({ turnIndex: 2, newPhaseIndex: 1, phaseName: 'Combat' }), 'sid');
  expect(e).not.toBeNull();
  expect(e?.type).toBe('session:phase');
});
```

- [ ] **Step 2: Run, verify FAIL.**
- [ ] **Step 3: Implement** i tipi/parser mancanti, riusando la normalizzazione campi esistente (`participantId|playerId`, ecc.).
- [ ] **Step 4: Run `pnpm test parse-sse-event` → PASS.**
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T9 reconcile FE event taxonomy with canonical model"`

---

## Task 10: Repunta i 4 consumer FE all'endpoint nativo (test-first)

**Files:**
- Modify: `apps/web/src/lib/session-live/use-session-live-stream.ts:214`, `lib/domain-hooks/useWidgetSync.ts:103`, `useTurnOrder.ts:200`, `useWhiteboardTool.ts:230` — URL `/api/v1/game-sessions/{id}/stream/v2` → `/api/v1/live-sessions/{id}/stream`.
- Modify (test-first): `use-session-live-stream.test.ts:149`, `useTurnOrder.test.ts:345`, `useWidgetSync.test.ts:98`.

**Interfaces:**
- Consumes: endpoint Task 4. Nessuna modifica di firma; solo URL.

- [ ] **Step 1: Update the failing tests first** — cambia le asserzioni di URL alla nuova rotta e aggiungi, in `use-session-live-stream.test.ts`, un test che asserisce che **nessun** EventSource viene creato verso `/game-sessions/**/stream/v2`.

```typescript
it('connects to the native live-sessions stream, never the legacy game-sessions route', () => {
  renderHook(() => useSessionLiveStream({ sessionId: 'session-1', enabled: true }));
  const url = MockEventSource.lastInstance()!.url;
  expect(url).toContain('/api/v1/live-sessions/session-1/stream');
  expect(url).not.toContain('/game-sessions/');
});
```

- [ ] **Step 2: Run, verify FAIL** (URL ancora legacy).
- [ ] **Step 3: Update the 4 hook URLs.**
- [ ] **Step 4: Run `pnpm test session-live useTurnOrder useWidgetSync` → PASS.**
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T10 repoint FE consumers to native stream (test-first)"`

---

## Task 11: E2E smoke + deprecazione `/stream/v2`

**Files:**
- Modify: `apps/web/e2e/session-live.smoke.spec.ts:114-163` (`:128`) — punta alla nuova rotta con mock esplicito (no abort cieco `/game-sessions/**`).
- Modify: `apps/api/src/Api/Routing/SessionTracking/SessionQueryEndpoints.cs` — header `Deprecation: true` + `Sunset: <data>` + `Link` rel="successor-version" su `/stream/v2`.
- Test: l'E2E aggiornato + un unit BE che asserisce gli header di deprecazione.

**Interfaces:**
- Consumes: endpoint Task 4 (FE), endpoint legacy (header).

- [ ] **Step 1: Update E2E (test-first)** — il test verifica raggiungibilità di `/api/v1/live-sessions/{id}/stream` e che la superficie live non chiami `/game-sessions/**/stream/v2`.
- [ ] **Step 2: Run E2E, verify FAIL** (rotta nuova non ancora mock/raggiunta).
- [ ] **Step 3: Add deprecation headers** all'endpoint legacy; allinea il mock E2E.
- [ ] **Step 4: Run E2E + unit header → PASS.**
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T11 E2E native stream + deprecate /stream/v2 (expand-and-contract)"`

---

## Task 12: Metriche/osservabilità (AC-OBS-1 minimo)

**Files:**
- Modify: `SessionBroadcastService.cs` o l'endpoint — Prometheus active-connection gauge + reconnect counter (riusa l'infra metriche esistente; cerca `meepleai_*` counters per il pattern).
- Test: `apps/api/tests/Api.Tests/.../SseMetricsTests.cs`

**Interfaces:**
- Produces: metrica `meepleai_live_sse_active_connections` (gauge per-sessione/totale) + `meepleai_live_sse_reconnect_total` (counter).

- [ ] **Step 1: Write failing test** — connessione → gauge +1; disconnessione → -1; reconnect con lastEventId → counter +1.
- [ ] **Step 2: Run, verify FAIL.**
- [ ] **Step 3: Implement** usando l'infra metriche del repo.
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** — `git commit -m "feat(session-live): #2561 SP2 T12 SSE observability (connection gauge + reconnect counter)"`

---

## Self-Review (eseguita)

- **Copertura AC**: SSE-1→T6/T7; SSE-2→T4 (heartbeat/429 riusati); SSE-3→T10; SSE-4→T5/T8/T9; SSE-5→T10/T11; SSE-6→T6/T7; OBS-1→T12. ✅
- **Invarianti ADR**: T1/T3 (ACL no-import), T8 (gateway iniettato in KB, non viceversa). ✅
- **Backward-compat legacy**: T2/T6/T7 girano i test legacy come gate. ✅
- **Type consistency**: `LiveSessionStreamEvent(Type,Data)`, `ILiveSessionStreamGateway.{SubscribeAsync,BroadcastAsync}`, `ISessionBroadcastService.PublishEnvelopeAsync`, `ISessionSequenceProvider.NextAsync` — coerenti tra task.
- **Note di rischio**: T6 modifica il parsing del replay del legacy (id format) — è il punto più delicato; i test legacy sono la guardia. Se il legacy parse-id assume `Ticks`, il cambio formato va fatto retro-compatibile (accetta sia `{Ticks:x}` storico che `{seq:D20}` nuovo) oppure si versiona il buffer.

## Out of scope (→ SP5 / Fase 4)
- Consolidamento dei 4 EventSource FE in 1 connessione (SP5).
- Rimozione fisica `/stream/v2` (Fase 4).
- Backfill companion sessioni in-flight pre-SP0 (SP5, OQ#5).
