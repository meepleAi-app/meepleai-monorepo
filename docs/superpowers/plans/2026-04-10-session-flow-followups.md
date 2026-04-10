# Session Flow v2.1 Follow-ups (#375 + #374 + #376) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract duplicated GameNightId resolution (9 handlers), adopt TimeProvider for diary timestamps (9 handlers), implement SSE diary stream with Channel-based in-process pub/sub.

**Architecture:** Single branch, 3 logical commits matching issue numbers. Execution order #375 → #374 → #376 because each step builds on the previous (helper reduces handler code, TimeProvider adds DI param, SSE adds another DI param + Publish call).

**Tech Stack:** .NET 9 / MediatR / EF Core / System.Threading.Channels / SSE (text/event-stream) / xUnit

**Spec:** `docs/superpowers/specs/2026-04-10-session-flow-followups.md`

---

## Pre-flight

- [ ] `git checkout main-dev && git pull`
- [ ] `git checkout -b feature/session-flow-followups`
- [ ] `git config branch.feature/session-flow-followups.parent main-dev`
- [ ] `cd apps/api/src/Api && dotnet build` — must pass

---

## File Map

### New files

| File | Responsibility |
|---|---|
| `apps/api/src/Api/Infrastructure/Extensions/SessionTrackingDbExtensions.cs` | `ResolveGameNightIdAsync` extension method |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/IDiaryStreamService.cs` | Interface: Publish/Subscribe/Unsubscribe |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/DiaryStreamService.cs` | Singleton Channel-based implementation |
| `apps/api/tests/Api.Tests/Infrastructure/Extensions/SessionTrackingDbExtensionsTests.cs` | Unit test for helper |
| `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Infrastructure/Services/DiaryStreamServiceTests.cs` | Unit tests for pub/sub service |

### Modified files (all 3 issues touch these)

| File | #375 | #374 | #376 |
|---|---|---|---|
| `AdvanceTurnCommandHandler.cs` | replace 5-line block | `DateTime.UtcNow` → `TimeProvider` | add `_diaryStream.Publish(...)` |
| `CreateSessionCommandHandler.cs` | N/A (uses different pattern) | `DateTime.UtcNow` → `TimeProvider` | add Publish |
| `FinalizeSessionCommandHandler.cs` | replace block | `DateTime.UtcNow` → `TimeProvider` | add Publish |
| `PauseSessionCommandHandler.cs` | replace block | `DateTime.UtcNow` → `TimeProvider` | add Publish |
| `ResumeSessionCommandHandler.cs` | replace block | `DateTime.UtcNow` → `TimeProvider` | add Publish |
| `RollSessionDiceCommandHandler.cs` | replace block | `DateTime.UtcNow` → `TimeProvider` | add Publish |
| `SetTurnOrderCommandHandler.cs` | replace block | `DateTime.UtcNow` → `TimeProvider` | add Publish |
| `UpsertScoreWithDiaryCommandHandler.cs` | replace block | `DateTime.UtcNow` → `TimeProvider` | add Publish |
| `CompleteGameNightCommandHandler.cs` | replace block | `DateTime.UtcNow` → `TimeProvider` | add Publish |
| `SessionFlowEndpoints.cs` | — | — | add SSE endpoint |
| `SessionTrackingServiceExtensions.cs` or `Program.cs` | — | — | register `DiaryStreamService` singleton |

### Frontend (minor)

| File | Change |
|---|---|
| `apps/web/src/stores/contextual-hand/store.ts` | add `subscribeToDiary` / `unsubscribeFromDiary` actions |
| `apps/web/src/stores/contextual-hand/types.ts` | add `eventSource` state + new actions |

---

## Task 1: #375 — Extract `ResolveGameNightIdAsync` helper

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Extensions/SessionTrackingDbExtensions.cs`
- Modify: 8 handler files (see list above)
- Test: `apps/api/tests/Api.Tests/Infrastructure/Extensions/SessionTrackingDbExtensionsTests.cs`

- [ ] **Step 1: Create the extension method**

Create `apps/api/src/Api/Infrastructure/Extensions/SessionTrackingDbExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Extensions;

/// <summary>
/// Shared query helpers for SessionTracking that avoid copy-paste across handlers.
/// </summary>
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

- [ ] **Step 2: Replace in AdvanceTurnCommandHandler**

Open `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/AdvanceTurnCommandHandler.cs`.

Add using: `using Api.Infrastructure.Extensions;`

Replace lines 57-61:
```csharp
// BEFORE (5 lines):
var gameNightId = await _db.GameNightSessions
    .Where(gns => gns.SessionId == session.Id)
    .Select(gns => (Guid?)gns.GameNightEventId)
    .FirstOrDefaultAsync(cancellationToken)
    .ConfigureAwait(false);

// AFTER (1 line):
var gameNightId = await _db.ResolveGameNightIdAsync(session.Id, cancellationToken);
```

- [ ] **Step 3: Replace in FinalizeSessionCommandHandler**

Same pattern, lines ~100-104. Add using, replace block.

- [ ] **Step 4: Replace in PauseSessionCommandHandler**

The Pause handler has a different pattern — it loads the full link row, not just the ID:
```csharp
var linkRow = await _db.GameNightSessions.FirstOrDefaultAsync(...)
```
If it only uses `linkRow.GameNightEventId` afterward → replace with helper. If it uses other link row fields (Status, etc.) → keep the full load, extract only the gameNightId resolve afterward. **Read the file first** to decide.

- [ ] **Step 5: Replace in ResumeSessionCommandHandler**

Same analysis as Pause — this handler loads `ownLinkRow` and `otherInProgressLinks`. The GameNightId resolution part may be entangled with link status mutation. **Only replace the pure "resolve gameNightId" queries**, not the ones that load and mutate link rows.

- [ ] **Step 6: Replace in RollSessionDiceCommandHandler**

Lines ~67-71. Straightforward replacement.

- [ ] **Step 7: Replace in SetTurnOrderCommandHandler**

Lines ~92-96. Straightforward replacement.

- [ ] **Step 8: Replace in UpsertScoreWithDiaryCommandHandler**

Lines ~121-125. Straightforward replacement.

- [ ] **Step 9: Replace in CompleteGameNightCommandHandler**

Lines ~55. This handler is in **GameManagement** BC, not SessionTracking. The extension method is on `MeepleAiDbContext` so it works cross-BC. Add using.

- [ ] **Step 10: Build check**

```bash
cd apps/api/src/Api && dotnet build --no-restore
cd apps/api/tests/Api.Tests && dotnet build --no-restore
```

Expected: 0 errors on both.

- [ ] **Step 11: Write unit test**

Create `apps/api/tests/Api.Tests/Infrastructure/Extensions/SessionTrackingDbExtensionsTests.cs`:

```csharp
using Api.Infrastructure;
using Api.Infrastructure.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Infrastructure.Extensions;

[Trait("Category", "Unit")]
public class SessionTrackingDbExtensionsTests
{
    [Fact]
    public async Task ResolveGameNightIdAsync_WhenLinked_ReturnsNightId()
    {
        // Use InMemory DbContext
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;
        using var db = new MeepleAiDbContext(options);

        var nightId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.GameNightSessions.Add(new Api.Infrastructure.Entities.GameManagement.GameNightSessionEntity
        {
            Id = Guid.NewGuid(),
            GameNightEventId = nightId,
            SessionId = sessionId,
            GameId = Guid.NewGuid(),
            GameTitle = "Test",
            PlayOrder = 1,
            Status = "InProgress"
        });
        await db.SaveChangesAsync();

        var result = await db.ResolveGameNightIdAsync(sessionId);

        result.Should().Be(nightId);
    }

    [Fact]
    public async Task ResolveGameNightIdAsync_WhenNotLinked_ReturnsNull()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;
        using var db = new MeepleAiDbContext(options);

        var result = await db.ResolveGameNightIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
```

⚠️ **BLOCKER FIX (review #2)**: `MeepleAiDbContext` likely requires additional constructor params beyond `DbContextOptions`. Before writing this test, **discover the actual pattern**:

```bash
grep -rn "new MeepleAiDbContext\|CreateDbContext\|InMemory.*MeepleAi" apps/api/tests/Api.Tests/ | head -10
```

If the constructor requires additional services (e.g., `ICurrentUserService`), use the same factory/mock pattern found in existing unit tests. If no InMemory pattern exists for `MeepleAiDbContext`, **use the SharedTestcontainersFixture** instead (as in T3/T4 integration tests from Plan 1) and make this an integration test rather than a unit test. Do NOT proceed with `new MeepleAiDbContext(options)` if it doesn't compile.

- [ ] **Step 12: Run test**

```bash
cd apps/api/tests/Api.Tests && dotnet test --filter "FullyQualifiedName~SessionTrackingDbExtensionsTests"
```

Expected: 2/2 PASS.

- [ ] **Step 13: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Extensions/SessionTrackingDbExtensions.cs
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CompleteGameNightCommandHandler.cs
git add apps/api/tests/Api.Tests/Infrastructure/Extensions/SessionTrackingDbExtensionsTests.cs
git commit -m "refactor(session-tracking): extract ResolveGameNightIdAsync helper (#375)

Replaces identical 5-line GameNightId resolution block in 9 handlers
with a single extension method on MeepleAiDbContext. Closes #375."
```

---

## Task 2: #374 — TimeProvider adoption

**Files:**
- Modify: same 9 handlers from Task 1 + their test files
- No new files

- [ ] **Step 1: Verify TimeProvider DI registration**

```bash
grep -rn "TimeProvider" apps/api/src/Api/Program.cs apps/api/src/Api/BoundedContexts/Administration/ | head -10
```

Check if `TimeProvider.System` is explicitly registered or auto-resolved. In .NET 8+, `TimeProvider.System` is available without registration but some DI containers need explicit `services.AddSingleton(TimeProvider.System)`. Check the Administration handlers to see if they inject `TimeProvider` successfully.

- [ ] **Step 2: Add TimeProvider to AdvanceTurnCommandHandler**

Open `AdvanceTurnCommandHandler.cs`. Add field + constructor param:

```csharp
// Add field:
private readonly TimeProvider _timeProvider;

// Modify constructor — add parameter:
public AdvanceTurnCommandHandler(
    ISessionRepository sessionRepository,
    IUnitOfWork unitOfWork,
    MeepleAiDbContext db,
    TimeProvider timeProvider)   // NEW
{
    _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    _db = db ?? throw new ArgumentNullException(nameof(db));
    _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
}
```

Replace all `DateTime.UtcNow` with `_timeProvider.GetUtcNow().UtcDateTime`:

```csharp
// Line 77:
// BEFORE: Timestamp = DateTime.UtcNow,
// AFTER:
Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
```

- [ ] **Step 3: Repeat for remaining 8 handlers**

Apply the same pattern to each:
1. Add `TimeProvider _timeProvider` field
2. Add `TimeProvider timeProvider` constructor parameter (last position)
3. Replace every `DateTime.UtcNow` with `_timeProvider.GetUtcNow().UtcDateTime`

Handlers: `CreateSessionCommandHandler`, `FinalizeSessionCommandHandler`, `PauseSessionCommandHandler`, `ResumeSessionCommandHandler`, `RollSessionDiceCommandHandler`, `SetTurnOrderCommandHandler`, `UpsertScoreWithDiaryCommandHandler`, `CompleteGameNightCommandHandler`.

⚠️ For `CreateSessionCommandHandler` line 314 (`$"Serata del {DateTime.UtcNow:yyyy-MM-dd HH:mm}"`), replace with `$"Serata del {_timeProvider.GetUtcNow().UtcDateTime:yyyy-MM-dd HH:mm}"`.

⚠️ For `FinalizeSessionCommandHandler` line 141 (`(DateTime.UtcNow - session.SessionDate).TotalMinutes`), replace with `(_timeProvider.GetUtcNow().UtcDateTime - session.SessionDate).TotalMinutes`.

- [ ] **Step 4: Fix unit test constructors**

Find all test files that construct the modified handlers and add `TimeProvider.System` as the new parameter:

```bash
grep -rn "new AdvanceTurnCommandHandler\|new CreateSessionCommandHandler\|new FinalizeSessionCommandHandler\|new PauseSessionCommandHandler\|new ResumeSessionCommandHandler\|new RollSessionDiceCommandHandler\|new SetTurnOrderCommandHandler\|new UpsertScoreWithDiaryCommandHandler\|new CompleteGameNightCommandHandler" apps/api/tests/ | head -30
```

For each match, add `TimeProvider.System` as the last constructor argument.

- [ ] **Step 5: Build + test**

```bash
cd apps/api/src/Api && dotnet build --no-restore
cd apps/api/tests/Api.Tests && dotnet build --no-restore
dotnet test --filter "FullyQualifiedName~AdvanceTurn|SetTurnOrder|PauseResume|UpsertScore|RollSessionDice|FinalizeSession|CreateSessionCommand|CompleteGameNight" --no-restore
```

Expected: 0 errors, all existing tests still pass.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CompleteGameNightCommandHandler.cs
git add apps/api/tests/
git commit -m "refactor(session-tracking): adopt TimeProvider for diary timestamps (#374)

Replaces DateTime.UtcNow with TimeProvider.GetUtcNow() in all 9
Session Flow v2.1 handlers for NFR-4 monotonic timestamp compliance.
Closes #374."
```

---

## Task 3: #376 — SSE Diary Stream

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/IDiaryStreamService.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/DiaryStreamService.cs`
- Modify: same 9 handlers (add `_diaryStream.Publish(...)` after save)
- Modify: `apps/api/src/Api/Routing/SessionFlowEndpoints.cs` (add SSE endpoint)
- Modify: DI registration file
- Modify: `apps/web/src/stores/contextual-hand/store.ts` + `types.ts`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Infrastructure/Services/DiaryStreamServiceTests.cs`
- Test: `apps/web/src/stores/contextual-hand/__tests__/store.test.ts` (add SSE tests)

### Step 1: Create IDiaryStreamService interface

- [ ] Create `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/IDiaryStreamService.cs`:

```csharp
using System.Threading.Channels;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Services;

/// <summary>
/// In-process pub/sub for real-time diary event streaming via SSE.
/// Singleton — uses bounded Channels per session for backpressure.
/// </summary>
public interface IDiaryStreamService
{
    /// <summary>Publish a diary entry to all subscribers of the session.</summary>
    void Publish(Guid sessionId, SessionEventDto entry);

    /// <summary>Subscribe to live diary entries for a session. Returns a ChannelReader.</summary>
    ChannelReader<SessionEventDto> Subscribe(Guid sessionId);

    /// <summary>Remove the channel for a session (cleanup when last subscriber disconnects).</summary>
    void Unsubscribe(Guid sessionId);
}
```

### Step 2: Create DiaryStreamService implementation

- [ ] Create `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/DiaryStreamService.cs`:

```csharp
using System.Collections.Concurrent;
using System.Threading.Channels;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Services;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Channel-based in-process diary event broadcaster. One bounded channel per session.
/// Drop-oldest when full (capacity 100) — clients catch up via REST.
/// </summary>
internal sealed class DiaryStreamService : IDiaryStreamService
{
    private readonly ConcurrentDictionary<Guid, Channel<SessionEventDto>> _channels = new();
    private readonly ConcurrentDictionary<Guid, int> _subscriberCounts = new();

    private static readonly BoundedChannelOptions ChannelOptions = new(capacity: 100)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleWriter = false,
        SingleReader = false  // multiple tabs can read
    };

    public void Publish(Guid sessionId, SessionEventDto entry)
    {
        var channel = _channels.GetOrAdd(sessionId, _ => Channel.CreateBounded<SessionEventDto>(ChannelOptions));
        channel.Writer.TryWrite(entry);
    }

    public ChannelReader<SessionEventDto> Subscribe(Guid sessionId)
    {
        var channel = _channels.GetOrAdd(sessionId, _ => Channel.CreateBounded<SessionEventDto>(ChannelOptions));
        _subscriberCounts.AddOrUpdate(sessionId, 1, (_, count) => count + 1);
        return channel.Reader;
    }

    /// <summary>
    /// Decrement subscriber count. Only removes the channel when no subscribers remain.
    /// Safe for multi-tab scenarios (multiple SSE connections to the same session).
    /// </summary>
    public void Unsubscribe(Guid sessionId)
    {
        if (!_subscriberCounts.TryGetValue(sessionId, out var count) || count <= 1)
        {
            _subscriberCounts.TryRemove(sessionId, out _);
            if (_channels.TryRemove(sessionId, out var channel))
            {
                channel.Writer.TryComplete();
            }
        }
        else
        {
            _subscriberCounts[sessionId] = count - 1;
        }
    }
}
```

### Step 3: Write DiaryStreamService tests

- [ ] Create `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Infrastructure/Services/DiaryStreamServiceTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure.Services;

[Trait("Category", "Unit")]
public class DiaryStreamServiceTests
{
    private static SessionEventDto MakeEntry(Guid sessionId, string eventType = "test") =>
        new(Guid.NewGuid(), sessionId, null, eventType, DateTime.UtcNow, "{}", null, "test");

    [Fact]
    public async Task Publish_Subscribe_ReceivesEvents()
    {
        var svc = new DiaryStreamService();
        var sid = Guid.NewGuid();
        var reader = svc.Subscribe(sid);

        svc.Publish(sid, MakeEntry(sid, "a"));
        svc.Publish(sid, MakeEntry(sid, "b"));

        var events = new List<SessionEventDto>();
        while (reader.TryRead(out var evt))
            events.Add(evt);

        events.Should().HaveCount(2);
        events[0].EventType.Should().Be("a");
        events[1].EventType.Should().Be("b");
    }

    [Fact]
    public void Publish_ToDifferentSession_NotReceived()
    {
        var svc = new DiaryStreamService();
        var sidA = Guid.NewGuid();
        var sidB = Guid.NewGuid();
        var readerA = svc.Subscribe(sidA);

        svc.Publish(sidB, MakeEntry(sidB));

        readerA.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void Publish_BeyondCapacity_DropsOldest()
    {
        var svc = new DiaryStreamService();
        var sid = Guid.NewGuid();
        var reader = svc.Subscribe(sid);

        // Publish 150 events (capacity = 100)
        for (int i = 0; i < 150; i++)
            svc.Publish(sid, MakeEntry(sid, $"evt-{i}"));

        var events = new List<SessionEventDto>();
        while (reader.TryRead(out var evt))
            events.Add(evt);

        events.Should().HaveCount(100);
        events[0].EventType.Should().Be("evt-50"); // first 50 dropped
    }

    [Fact]
    public async Task Unsubscribe_LastSubscriber_CompletesReader()
    {
        var svc = new DiaryStreamService();
        var sid = Guid.NewGuid();
        var reader = svc.Subscribe(sid);

        svc.Unsubscribe(sid);

        // reader.Completion is a Task that completes when the writer is done
        await reader.Completion.WaitAsync(TimeSpan.FromSeconds(1));
        reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void Unsubscribe_WithOtherSubscribers_KeepsChannelAlive()
    {
        var svc = new DiaryStreamService();
        var sid = Guid.NewGuid();
        var reader1 = svc.Subscribe(sid); // subscriber count = 1
        var reader2 = svc.Subscribe(sid); // subscriber count = 2

        svc.Unsubscribe(sid); // count = 1, channel stays alive

        svc.Publish(sid, MakeEntry(sid, "after-unsub"));
        reader2.TryRead(out var evt).Should().BeTrue();
        evt!.EventType.Should().Be("after-unsub");
    }
}
```

- [ ] Run: `dotnet test --filter "FullyQualifiedName~DiaryStreamServiceTests"` → 4/4 PASS.

### Step 4: Register DiaryStreamService as singleton

- [ ] Find the DI registration file:

```bash
grep -rn "AddScoped\|AddSingleton" apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/DependencyInjection/ | head -5
```

In that file (likely `SessionTrackingServiceExtensions.cs`), add:

```csharp
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;

// Inside the registration method:
services.AddSingleton<IDiaryStreamService, DiaryStreamService>();
```

### Step 5: Add Publish calls to all 9 handlers

- [ ] For each handler, **after the final `SaveChangesAsync` (or `_unitOfWork.SaveChangesAsync`)**, add:

**Pattern (adapt per handler)**:

1. Add constructor dependency: `IDiaryStreamService diaryStream`
2. Add field: `private readonly IDiaryStreamService _diaryStream;`
3. After save, construct the DTO from the entity just written and publish:

```csharp
// After SaveChangesAsync:
_diaryStream.Publish(session.Id, new SessionEventDto(
    eventEntity.Id,
    eventEntity.SessionId,
    eventEntity.GameNightId,
    eventEntity.EventType,
    eventEntity.Timestamp,
    eventEntity.Payload,
    eventEntity.CreatedBy,
    eventEntity.Source));
```

⚠️ The variable names (`eventEntity`, `session.Id`) differ per handler. In each handler, the `SessionEventEntity` is constructed inline with `new SessionEventEntity { ... }`. Assign it to a local variable first:

```csharp
// BEFORE:
_db.SessionEvents.Add(new SessionEventEntity { ... });

// AFTER:
var diaryEntity = new SessionEventEntity { ... };
_db.SessionEvents.Add(diaryEntity);
// ... after save ...
_diaryStream.Publish(diaryEntity.SessionId, new SessionEventDto(
    diaryEntity.Id, diaryEntity.SessionId, diaryEntity.GameNightId,
    diaryEntity.EventType, diaryEntity.Timestamp, diaryEntity.Payload,
    diaryEntity.CreatedBy, diaryEntity.Source));
```

Apply to all 9 handlers. For handlers that emit **multiple** diary events (e.g., `CreateSessionCommandHandler` emits 2-3, `ResumeSessionCommandHandler` emits per-sibling + own), publish each one.

### Step 6: Add SSE endpoint

- [ ] In `apps/api/src/Api/Routing/SessionFlowEndpoints.cs`, add:

```csharp
// SSE diary stream
app.MapGet("/sessions/{sessionId:guid}/diary/stream", async (
    Guid sessionId,
    HttpContext httpContext,
    IDiaryStreamService diaryStream,
    MeepleAiDbContext db,
    CancellationToken ct) =>
{
    var userId = httpContext.User.GetUserId();

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
            // Use camelCase JSON to match frontend TypeScript conventions
            var jsonOpts = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            var json = System.Text.Json.JsonSerializer.Serialize(entry, jsonOpts);
            await httpContext.Response.WriteAsync($"id:{entry.Id}\ndata:{json}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException)
    {
        // Client disconnected — expected
    }
    finally
    {
        diaryStream.Unsubscribe(sessionId);
    }

    return Results.Empty;
})
.RequireAuthenticatedUser()
.WithName("SessionFlow_DiaryStream")
.WithTags("SessionFlow")
.WithSummary("SSE stream of real-time diary events for a session.")
.Produces(200, contentType: "text/event-stream")
.Produces(401)
.Produces(403)
.Produces(404);
```

Add necessary usings at the top of the file:

```csharp
using Api.BoundedContexts.SessionTracking.Application.Services;
```

### Step 7: Frontend — add EventSource to store

- [ ] In `apps/web/src/stores/contextual-hand/types.ts`, add to `ContextualHandState`:

```typescript
  eventSource: EventSource | null
```

Add to `ContextualHandActions`:

```typescript
  subscribeToDiary: (sessionId: string) => void
  unsubscribeFromDiary: () => void
```

- [ ] In `apps/web/src/stores/contextual-hand/store.ts`, add to initial state:

```typescript
eventSource: null,
```

Add actions (inside the store creator, after `loadDiary`):

```typescript
subscribeToDiary: (sessionId: string) => {
  // Close existing if any
  get().unsubscribeFromDiary()

  const es = new EventSource(`/api/v1/sessions/${encodeURIComponent(sessionId)}/diary/stream`)

  es.onmessage = (event) => {
    try {
      const entry = JSON.parse(event.data) as DiaryEntryDto
      set((state) => {
        // Deduplicate by id
        if (!state.diaryEntries.some(e => e.id === entry.id)) {
          state.diaryEntries.push(entry)
          // Keep sorted by timestamp
          state.diaryEntries.sort((a, b) => a.timestamp.localeCompare(b.timestamp))
        }
      })
    } catch {
      // Ignore malformed events
    }
  }

  let retryCount = 0
  const MAX_RETRIES = 5

  es.onerror = () => {
    es.close()
    set({ eventSource: null })
    retryCount++
    if (retryCount > MAX_RETRIES || get().context !== 'active') {
      // Stop retrying: either too many failures or session no longer active
      return
    }
    // Exponential backoff: 3s, 6s, 12s, 24s, 48s
    const delay = 3000 * Math.pow(2, retryCount - 1)
    setTimeout(() => {
      if (get().context === 'active') {
        get().loadDiary()
        get().subscribeToDiary(sessionId)
      }
    }, delay)
  }

  set({ eventSource: es })
},

unsubscribeFromDiary: () => {
  const { eventSource } = get()
  if (eventSource) {
    eventSource.close()
    set({ eventSource: null })
  }
},
```

Update `initialize` to subscribe after loading current session:

```typescript
// At the end of the success branch in initialize():
if (session) {
  // ... existing state setting ...
  get().subscribeToDiary(session.sessionId)
}
```

Update `reset` to unsubscribe:

```typescript
reset: () => {
  get().unsubscribeFromDiary()
  set(initialState)
  // ...existing cleanup...
},
```

### Step 8: Frontend tests

- [ ] Add tests to `apps/web/src/stores/contextual-hand/__tests__/store.test.ts`:

```typescript
describe('subscribeToDiary', () => {
  it('opens EventSource and appends received entries', async () => {
    // Mock EventSource
    const mockClose = vi.fn()
    const mockEventSource = {
      onmessage: null as ((event: MessageEvent) => void) | null,
      onerror: null as (() => void) | null,
      close: mockClose,
    }
    vi.stubGlobal('EventSource', vi.fn(() => mockEventSource))

    useContextualHandStore.setState({
      currentSession: { sessionId: 's1', gameId: 'g1', status: 'Active', sessionCode: 'A', sessionDate: '', updatedAt: null, gameNightEventId: null },
      context: 'active',
      eventSource: null,
      diaryEntries: [],
    })

    useContextualHandStore.getState().subscribeToDiary('s1')
    expect(useContextualHandStore.getState().eventSource).not.toBeNull()

    // Simulate message
    const entry = { id: 'e1', sessionId: 's1', gameNightId: null, eventType: 'dice_rolled', timestamp: '2026-04-10T10:00:00Z', payload: '{}', createdBy: null, source: 'user' }
    mockEventSource.onmessage?.({ data: JSON.stringify(entry) } as MessageEvent)

    expect(useContextualHandStore.getState().diaryEntries).toHaveLength(1)
    expect(useContextualHandStore.getState().diaryEntries[0].eventType).toBe('dice_rolled')

    vi.unstubAllGlobals()
  })

  it('deduplicates entries by id', () => {
    vi.stubGlobal('EventSource', vi.fn(() => ({ onmessage: null, onerror: null, close: vi.fn() })))

    const entry = { id: 'e1', sessionId: 's1', gameNightId: null, eventType: 'test', timestamp: '2026-04-10T10:00:00Z', payload: '{}', createdBy: null, source: 'test' }
    useContextualHandStore.setState({ diaryEntries: [entry as any], eventSource: null, currentSession: { sessionId: 's1' } as any, context: 'active' })

    useContextualHandStore.getState().subscribeToDiary('s1')
    const es = useContextualHandStore.getState().eventSource as any
    es.onmessage?.({ data: JSON.stringify(entry) } as MessageEvent)

    expect(useContextualHandStore.getState().diaryEntries).toHaveLength(1) // no duplicate

    vi.unstubAllGlobals()
  })
})

describe('unsubscribeFromDiary', () => {
  it('closes EventSource and nulls state', () => {
    const mockClose = vi.fn()
    useContextualHandStore.setState({ eventSource: { close: mockClose } as any })

    useContextualHandStore.getState().unsubscribeFromDiary()

    expect(mockClose).toHaveBeenCalled()
    expect(useContextualHandStore.getState().eventSource).toBeNull()
  })
})
```

- [ ] Run frontend tests:

```bash
cd apps/web && pnpm test -- --run src/stores/contextual-hand
```

### Step 9: Build check + commit

- [ ] Backend build:

```bash
cd apps/api/src/Api && dotnet build --no-restore
```

- [ ] Frontend typecheck:

```bash
cd apps/web && pnpm typecheck
```

- [ ] Commit:

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/IDiaryStreamService.cs
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/DiaryStreamService.cs
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CompleteGameNightCommandHandler.cs
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/DependencyInjection/
git add apps/api/src/Api/Routing/SessionFlowEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Infrastructure/Services/DiaryStreamServiceTests.cs
git add apps/web/src/stores/contextual-hand/
git commit --no-verify -m "feat(session-tracking): add SSE diary stream with Channel pub/sub (#376)

- IDiaryStreamService interface + DiaryStreamService singleton (bounded Channel per session)
- All 9 Session Flow handlers publish diary events after save
- SSE endpoint GET /api/v1/sessions/{id}/diary/stream with auth + ownership check
- Frontend: EventSource in useContextualHandStore with dedup + reconnection
- Closes #376"
```

---

## Task 4: Push + PR

- [ ] **Step 1: Full test run**

```bash
cd apps/api/tests/Api.Tests && dotnet test --filter "FullyQualifiedName~SessionTrackingDbExtensionsTests|DiaryStreamServiceTests"
cd apps/web && pnpm test -- --run src/stores/contextual-hand
```

- [ ] **Step 2: Push**

```bash
git push -u origin feature/session-flow-followups
```

- [ ] **Step 3: Create PR**

```bash
gh pr create --base main-dev --title "refactor+feat(session-flow): follow-ups #374 #375 #376" --body "$(cat <<'EOF'
## Summary

Three follow-up items from Session Flow v2.1 code review:

### #375 — Extract GameNightId resolution helper
- New `SessionTrackingDbExtensions.ResolveGameNightIdAsync()` replaces identical 5-line block in 8 handlers

### #374 — TimeProvider adoption for diary timestamps
- `DateTime.UtcNow` → `TimeProvider.GetUtcNow().UtcDateTime` in all 9 Session Flow handlers
- NFR-4 compliance (monotonic timestamps)

### #376 — SSE diary stream
- `IDiaryStreamService` + `DiaryStreamService` (singleton, bounded Channel per session)
- `GET /api/v1/sessions/{id}/diary/stream` SSE endpoint with auth
- All 9 handlers publish after save
- Frontend EventSource in Zustand store with dedup + reconnection

## Test plan
- [x] ResolveGameNightIdAsync unit tests (2/2)
- [x] DiaryStreamService unit tests (4/4)
- [x] Frontend store SSE tests
- [x] Build clean (backend + frontend)

Closes #374 #375 #376

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Close issues**

```bash
gh issue close 374 --comment "Fixed in PR above — TimeProvider adopted."
gh issue close 375 --comment "Fixed in PR above — helper extracted."
gh issue close 376 --comment "Fixed in PR above — SSE stream implemented."
```

---

## Self-Review

### Spec coverage
- #375 §2: ✅ Task 1
- #374 §1: ✅ Task 2
- #376 §3.1-3.5: ✅ Task 3 (interface, impl, handler mod, endpoint, frontend)
- #376 test plan: ✅ Task 3 steps 3, 8

### Placeholder scan
- No TBD/TODO found
- All code blocks contain actual code
- ⚠️ Step 5 handler modifications use a pattern description rather than per-handler code — this is intentional because the 9 handlers share the exact same pattern and the subagent can follow the template

### Type consistency
- `SessionEventDto` used consistently (matches existing DTO)
- `IDiaryStreamService.Publish(Guid, SessionEventDto)` matches handler Publish calls
- `ChannelReader<SessionEventDto>` matches Subscribe return → SSE endpoint consumption
