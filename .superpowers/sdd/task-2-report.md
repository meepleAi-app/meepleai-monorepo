# Task 2 Report — SP5-c (#2600) Endpoint hook: ensure companion on first subscribe

## Fix: stale stream-not-linked header (final-review Important)

**Option chosen:** Option A — changed `EnsureCompanionCommand` from `ICommand` (void) to `ICommand<Guid?>`, returning the post-ensure `TrackingSessionId`. The endpoint uses this return value instead of the stale `ctx.HasCompanion` to decide whether to emit `X-Warning-Code: stream-not-linked`.

### Files changed

| File | Change |
|------|--------|
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/EnsureCompanionCommand.cs` | `ICommand` → `ICommand<Guid?>`, updated XML doc |
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/EnsureCompanionCommandHandler.cs` | `ICommandHandler<EnsureCompanionCommand>` → `ICommandHandler<EnsureCompanionCommand, Guid?>`, `Task Handle(…)` → `Task<Guid?> Handle(…)`. All return paths: already-has-companion → returns existing id; free-form → returns null; created → returns new id; concurrency-race-winner → returns winner's id |
| `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` | Dispatch branch replaced with `isLinkedAfterEnsure` logic: if `ctx.HasCompanion`, skip dispatch (already linked); else dispatch and check `ensuredTrackingId.HasValue`. Warning header uses `!isLinkedAfterEnsure` instead of `!ctx.HasCompanion` |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/EnsureCompanionCommandHandlerTests.cs` | +4 new return-value tests: `Handle_NullCompanionAndGameIdPresent_ReturnsNewCompanionId`, `Handle_AlreadyHasCompanion_ReturnsExistingTrackingSessionId`, `Handle_FreeFormSession_ReturnsNull`, `Handle_ConcurrencyConflict_WhenRefetchShowsCompanionSet_ReturnsWinnersTrackingId`. Added `CreateSessionAlreadyHasCompanionWithKnownId()` helper + `ExistingCompanionId` constant. Total: 16 unit tests (12 original + 4 new) |
| `apps/api/tests/Api.Tests/Integration/GameManagement/LazyCompanionOnSubscribeTests.cs` | Test 1 (`Subscribe_GameIdBacked_NullCompanion_CreatesCompanion`) extended: added assertion `resp.Headers.Should().NotContainKey("X-Warning-Code")` — the subscribe that just linked the session must NOT receive the warning |

### Tests run locally (no Docker)

16 unit tests in `EnsureCompanionCommandHandlerTests` — all passed (0 failed).

```
Superato Handle_NullCompanionAndGameIdPresent_SetsTrackingSessionId
Superato Handle_ConcurrencyConflict_WhenRefetchShowsCompanionSet_ReturnsWinnersTrackingId
Superato Handle_NullCompanionAndGameIdPresent_CreatesCompanionOnce
Superato Handle_AlreadyHasCompanion_ReturnsExistingTrackingSessionId
Superato Handle_ConcurrencyConflict_WhenRefetchStillNoCompanion_Rethrows
Superato Handle_ConcurrencyConflict_WhenRefetchShowsCompanionSet_CompletesSuccessfully
Superato Handle_ConcurrencyConflict_WhenRefetchShowsCompanionSet_DoesNotCreateSecondCompanion
Superato Handle_NullCompanionAndGameIdPresent_SavesOnce
Superato Handle_FreeFormSession_DoesNotSave
Superato Handle_SessionNotFound_DoesNotCallCreateCompanion
Superato Handle_FreeFormSession_ReturnsNull
Superato Handle_AlreadyHasCompanion_DoesNotSave
Superato Handle_AlreadyHasCompanion_DoesNotCallCreateCompanion
Superato Handle_SessionNotFound_ThrowsNotFoundException
Superato Handle_FreeFormSession_DoesNotCallCreateCompanion
Superato Handle_NullCompanionAndGameIdPresent_ReturnsNewCompanionId
```

### Tests needing Docker (CI only)

Integration tests in `LazyCompanionOnSubscribeTests` (Testcontainers / `Integration-GroupC`). Test 1 has been updated with the new header assertion (`NotContainKey("X-Warning-Code")`). Test 2 (free-form → `stream-not-linked` present) and Tests 3–4 unchanged. All 4 compile cleanly.

### Build output

```
Api         → 0 errors, 0 warnings  (Release)
Api.Tests   → 0 errors (build)
Unit tests  → 16/16 passed
```

### Blocking concerns

None.

## Endpoint change (`LiveSessionEndpoints.cs`)

Inserted after the 403 auth guard (line 983) and **before** SSE header writes (line 993):

```csharp
// SP5-c (#2600 Task 2): lazily provision a companion for legacy GameId-backed sessions
// that pre-date the SP0 Saga (TrackingSessionId == null && GameId != null).
if (!ctx.HasCompanion)
    await mediator.Send(new EnsureCompanionCommand(sessionId), ct).ConfigureAwait(false);
```

- `IMediator` was already injected into the endpoint delegate — no signature change needed.
- Context variable is named `ctx` (of type `LiveSessionStreamContextResult`) with property `HasCompanion`.
- `EnsureCompanionCommand` import was already present via `using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;`.
- The `X-Warning-Code: stream-not-linked` header is preserved as-is (based on the stale `ctx.HasCompanion`). For a GameId-backed legacy session on its first subscribe, the warning fires once; on subsequent subscribes `HasCompanion == true` so no dispatch and no warning. For free-form (GameId==null) sessions the warning is always correct. Cosmetic tradeoff accepted per brief scope.
- Auth/Found/Authorized short-circuits still fire before the dispatch — unauthorized callers never reach `EnsureCompanionCommand`.
- The gateway re-reads the session inside `SubscribeAsync` and picks up the freshly-persisted `TrackingSessionId`.

## Integration tests written

File: `apps/api/tests/Api.Tests/Integration/GameManagement/LazyCompanionOnSubscribeTests.cs`

| # | Test | Assertion |
|---|------|-----------|
| 1 | `Subscribe_GameIdBacked_NullCompanion_CreatesCompanion` | After subscribe, `TrackingSessionId` non-null AND companion `SessionTracking.Session` row exists |
| 2 | `Subscribe_FreeForm_NoGameId_DoesNotCreateCompanion` | `TrackingSessionId` stays null, `X-Warning-Code` still set, 0 companion rows |
| 3 | `Subscribe_AlreadyHasCompanion_CompanionIsUnchanged` | Same `TrackingSessionId`, still exactly 1 companion row, no `X-Warning-Code` |
| 4 | `Subscribe_Concurrent_CreatesExactlyOneCompanion` | Both subscribes return 200, exactly 1 companion row, 0 orphans |

All 4 tests use `HttpCompletionOption.ResponseHeadersRead` to avoid blocking on the SSE body and `Integration-GroupC` Testcontainers isolation. Legacy row seeding uses direct `LiveGameSessionEntity` insert (bypassing `CreateLiveSessionCommand` which would auto-create via SP0 Saga). Test 3 seeds a `SessionTracking.SessionEntity` with string-enum values (`SessionType="GameSpecific"`, `Status="Active"`) matching `SessionMapper.ToEntity`.

## Tests executed vs. need Docker

- **Ran and passed (no Docker)**: `Category=Unit` filter — 20,162 unit tests, 0 failures. Includes Task 1's `EnsureCompanionCommandHandlerTests`.
- **Need Docker (Testcontainers)**: All 4 new integration tests in `LazyCompanionOnSubscribeTests`. Not executed locally — Testcontainers requires Docker which is unavailable in this environment. Tests compile cleanly (`dotnet build` 0 errors).

## Build output

```
Api         → 0 warnings, 0 errors  (54s)
Api.Tests   → 0 errors (202 pre-existing xUnit/Sonar warnings, unchanged)  (23s)
Unit tests  → 20162 passed, 0 failed
```

## Concurrency test (Test 4) feasibility

The in-process `TestHost` dispatches concurrent HTTP requests in separate Task contexts with independent DI scopes, so both `EnsureCompanionCommand` handlers execute concurrently against the same DB row. The xmin optimistic-concurrency token (ADR-060) ensures only one `SaveChanges` wins; the loser catches `DbUpdateConcurrencyException`, re-fetches, sees `TrackingSessionId != null`, and returns idempotently. Test 4 is valid in the Testcontainers harness.

## Blocking concerns

None. Change is minimal (1 guard + 1 await), handler is idempotent + race-safe (Task 1), existing `LiveSessionStreamEndpointTests` (AC-1 to AC-4) are unaffected.
