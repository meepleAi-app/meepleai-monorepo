# Session Flow v2.1 Follow-ups: #374 + #375 + #376

**Data**: 2026-04-10
**Status**: Approved
**Parent**: `docs/superpowers/specs/2026-04-09-session-flow-v2.1.md`
**Issues**: [#374](https://github.com/meepleAi-app/meepleai-monorepo/issues/374), [#375](https://github.com/meepleAi-app/meepleai-monorepo/issues/375), [#376](https://github.com/meepleAi-app/meepleai-monorepo/issues/376)

---

## 1. #374 — TimeProvider Adoption for Diary Timestamps

### Problem

9 Session Flow v2.1 handlers use `DateTime.UtcNow` for diary event timestamps. Spec NFR-4 requires `TimeProvider` for monotonic timestamps. The `Administration` BC already uses `TimeProvider` injection — SessionTracking should follow.

### Scope

**In scope** (Session Flow v2.1 handlers only):
- `AdvanceTurnCommandHandler`
- `CreateSessionCommandHandler`
- `FinalizeSessionCommandHandler`
- `PauseSessionCommandHandler`
- `ResumeSessionCommandHandler`
- `RollSessionDiceCommandHandler`
- `SetTurnOrderCommandHandler`
- `UpsertScoreWithDiaryCommandHandler`
- `CompleteGameNightCommandHandler` (BC GameManagement)

**Not in scope**: 20+ pre-existing handlers, domain entity factories (`SessionEvent.Create`, `ScoreEntry.Create`, `DiceRoll.Create`). Entity factory refactoring is a separate tech-debt item.

### Change

Each handler gains `TimeProvider` as constructor dependency:

```csharp
// Before
var now = DateTime.UtcNow;

// After  
private readonly TimeProvider _timeProvider;
// constructor: _timeProvider = timeProvider ?? throw ...
var now = _timeProvider.GetUtcNow().UtcDateTime;
```

For `SessionEventEntity` construction (object initializer pattern), replace `Timestamp = DateTime.UtcNow` with `Timestamp = _timeProvider.GetUtcNow().UtcDateTime`.

DI registration: `TimeProvider.System` is the default .NET 8+ singleton. No registration needed — already resolved by the framework.

### Test impact

Unit tests that mock handler constructors need an additional `TimeProvider` parameter. Use `TimeProvider.System` in tests, or `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing` if precise assertions are needed. Check if the package is already referenced:

```bash
grep -rn "TimeProvider.Testing" apps/api/src/Api/Api.csproj apps/api/tests/Api.Tests/Api.Tests.csproj
```

If not, `TimeProvider.System` is sufficient for these tests.

---

## 2. #375 — Extract GameNightId Resolution Helper

### Problem

9 handlers contain this identical 5-line block:

```csharp
var gameNightId = await _db.GameNightSessions
    .Where(gns => gns.SessionId == sessionId)
    .Select(gns => (Guid?)gns.GameNightEventId)
    .FirstOrDefaultAsync(ct)
    .ConfigureAwait(false);
```

### Change

Create extension method on `MeepleAiDbContext`:

```csharp
// File: apps/api/src/Api/Infrastructure/Extensions/SessionTrackingDbExtensions.cs
namespace Api.Infrastructure.Extensions;

public static class SessionTrackingDbExtensions
{
    /// <summary>
    /// Resolves the GameNightEvent ID for a given session, if any.
    /// Returns null if the session is not linked to a GameNight.
    /// </summary>
    public static async Task<Guid?> ResolveGameNightIdAsync(
        this MeepleAiDbContext db,
        Guid sessionId,
        CancellationToken ct = default)
    {
        return await db.GameNightSessions
            .Where(gns => gns.SessionId == sessionId)
            .Select(gns => (Guid?)gns.GameNightEventId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
```

Replace in all 9 handlers:

```csharp
// Before (5 lines)
var gameNightId = await _db.GameNightSessions
    .Where(gns => gns.SessionId == session.Id)
    .Select(gns => (Guid?)gns.GameNightEventId)
    .FirstOrDefaultAsync(ct)
    .ConfigureAwait(false);

// After (1 line)
var gameNightId = await _db.ResolveGameNightIdAsync(session.Id, ct);
```

### Handlers to update

1. `AdvanceTurnCommandHandler`
2. `CreateSessionCommandHandler` (2 occurrences)
3. `FinalizeSessionCommandHandler`
4. `PauseSessionCommandHandler`
5. `ResumeSessionCommandHandler` (2 occurrences)
6. `RollSessionDiceCommandHandler`
7. `SetTurnOrderCommandHandler`
8. `UpsertScoreWithDiaryCommandHandler`

### Test

One unit test for the extension method itself (with InMemory DbContext). No handler test changes needed — the method signature is the same.

---

## 3. #376 — SSE Diary Stream

### Problem

Spec v2.1 deferred SSE diary streaming. Multi-device use (phone + tablet at game table) requires real-time event push. Current solution: REST polling or manual reload.

### Decisions

| Decision | Choice |
|---|---|
| Stream scope | **Session-scoped**: `GET /api/v1/sessions/{id}/diary/stream` |
| Trigger mechanism | **Channel in-process**: singleton `IDiaryStreamService` with `Channel<DiaryEntryDto>` per sessionId |
| Reconnection | **No server replay**: client calls `GET /diary` REST for catch-up, then reopens SSE |
| Multi-instance | **Not in scope**: single API instance deployment. Redis pub/sub as future drop-in |

### Architecture

```
Handler (e.g., RollDice)
  ├── _db.SessionEvents.Add(entity)
  ├── _unitOfWork.SaveChangesAsync()
  └── _diaryStream.Publish(sessionId, dto)   ← NEW

DiaryStreamService (singleton)
  ConcurrentDictionary<Guid, Channel<DiaryEntryDto>>
  ├── Publish(sessionId, entry): TryWrite to bounded channel
  └── Subscribe(sessionId): returns ChannelReader

SSE Endpoint: GET /sessions/{id}/diary/stream
  ├── Auth + ownership check
  ├── Set headers: text/event-stream, no-cache, keep-alive
  └── await foreach (entry in reader.ReadAllAsync(ct))
        → write "id:{entry.Id}\ndata:{json}\n\n" + flush
```

### Components

#### 3.1 IDiaryStreamService (interface)

```csharp
// File: apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/IDiaryStreamService.cs
namespace Api.BoundedContexts.SessionTracking.Application.Services;

public interface IDiaryStreamService
{
    /// <summary>Publish a diary entry to all subscribers of the session.</summary>
    void Publish(Guid sessionId, DiaryEntryDto entry);

    /// <summary>Subscribe to live diary entries for a session.</summary>
    ChannelReader<DiaryEntryDto> Subscribe(Guid sessionId);

    /// <summary>Remove the channel for a session (cleanup when last subscriber disconnects).</summary>
    void RemoveChannel(Guid sessionId);
}
```

Note: `DiaryEntryDto` here refers to the existing `SessionEventDto` (aliased as `DiaryEventDto` in query files). Use the DTO from `Api.BoundedContexts.SessionTracking.Application.DTOs.SessionEventDto`.

#### 3.2 DiaryStreamService (implementation)

```csharp
// File: apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/DiaryStreamService.cs
```

- Singleton registered in DI
- `ConcurrentDictionary<Guid, Channel<SessionEventDto>>` backing store
- `Publish`: get-or-add channel, `TryWrite` (bounded, drop oldest)
- `Subscribe`: get-or-add channel, return `channel.Reader`
- `RemoveChannel`: remove from dictionary, complete the channel writer
- Channel config: `BoundedChannelOptions { Capacity = 100, FullMode = BoundedChannelFullMode.DropOldest, SingleWriter = false, SingleReader = false }`

#### 3.3 Handler modification

Every Session Flow v2.1 handler that emits `SessionEventEntity` gets `IDiaryStreamService` injected. After `SaveChangesAsync`, call:

```csharp
_diaryStream.Publish(sessionId, new SessionEventDto(
    entity.Id, entity.SessionId, entity.GameNightId,
    entity.EventType, entity.Timestamp, entity.Payload,
    entity.CreatedBy, entity.Source));
```

Handlers to modify (same 9 as #374):
- AdvanceTurnCommandHandler
- CreateSessionCommandHandler
- FinalizeSessionCommandHandler
- PauseSessionCommandHandler
- ResumeSessionCommandHandler
- RollSessionDiceCommandHandler
- SetTurnOrderCommandHandler
- UpsertScoreWithDiaryCommandHandler
- CompleteGameNightCommandHandler

#### 3.4 SSE Endpoint

In `SessionFlowEndpoints.cs`:

```csharp
app.MapGet("/sessions/{sessionId:guid}/diary/stream", async (
    Guid sessionId,
    HttpContext httpContext,
    IDiaryStreamService diaryStream,
    MeepleAiDbContext db,
    CancellationToken ct) =>
{
    var userId = httpContext.User.GetUserId();

    // Ownership check (reuse extension from #375)
    var sessionOwnerId = await db.SessionTrackingSessions
        .AsNoTracking()
        .Where(s => s.Id == sessionId)
        .Select(s => (Guid?)s.UserId)
        .FirstOrDefaultAsync(ct);

    if (sessionOwnerId is null) return Results.NotFound();
    if (sessionOwnerId.Value != userId) return Results.Forbid();

    httpContext.Response.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";

    var reader = diaryStream.Subscribe(sessionId);

    try
    {
        await foreach (var entry in reader.ReadAllAsync(ct))
        {
            var json = JsonSerializer.Serialize(entry, SseJsonOptions.Default);
            await httpContext.Response.WriteAsync($"id:{entry.Id}\ndata:{json}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);
        }
    }
    finally
    {
        // Cleanup hint (service decides whether to remove based on subscriber count)
        diaryStream.RemoveChannel(sessionId);
    }

    return Results.Empty;
})
.RequireAuthenticatedUser()
.WithName("SessionFlow_DiaryStream")
.WithTags("SessionFlow")
.Produces(200, contentType: "text/event-stream")
.Produces(401)
.Produces(403)
.Produces(404);
```

#### 3.5 Frontend integration

In `useContextualHandStore`, add:

```typescript
// New state
eventSource: EventSource | null

// New actions
subscribeToDiary: (sessionId: string) => void
unsubscribeFromDiary: () => void
```

`subscribeToDiary` opens `EventSource('/api/v1/sessions/{id}/diary/stream')`. On `message` event, parse JSON and append to `diaryEntries` array (deduplicate by id). On `error`, wait 3s then call `loadDiary()` for catch-up and reopen.

`unsubscribeFromDiary` calls `eventSource.close()` and sets to null.

`initialize()` should call `subscribeToDiary` after loading current session. `reset()` should call `unsubscribeFromDiary`.

### DI Registration

```csharp
// In SessionTrackingServiceExtensions.cs (or Program.cs)
services.AddSingleton<IDiaryStreamService, DiaryStreamService>();
```

### Test Plan

**Unit**:
- `DiaryStreamService`: publish 5 entries, subscribe, verify all received in order
- `DiaryStreamService`: publish to session A, subscriber to session B receives nothing
- `DiaryStreamService`: bounded channel drops oldest when full (publish 150, subscriber reads last 100)
- `DiaryStreamService`: RemoveChannel completes the reader

**Integration** (Docker required):
- Handler emits event → subscriber receives it via service
- SSE endpoint returns `text/event-stream` content type + correct headers

**Frontend** (Vitest):
- `subscribeToDiary` opens EventSource, receives mock events, appends to store
- `unsubscribeFromDiary` closes EventSource
- Reconnection: on error, `loadDiary()` called for catch-up

### Not in scope

- Multi-instance support (Redis pub/sub)
- GameNight-scoped stream
- `Last-Event-ID` server-side replay
- Backpressure/throttling beyond bounded channel

---

## 4. Execution Order

All 3 issues modify the same 9 handler files. Execute in this order to minimize merge conflicts:

1. **#375 first** (extract helper) — pure refactor, shrinks handler code
2. **#374 second** (TimeProvider) — adds constructor param, uses smaller handler code from #375
3. **#376 last** (SSE stream) — adds `IDiaryStreamService` constructor param + `Publish` call after save

Single branch, single PR, 3 logical commits matching issue numbers.
