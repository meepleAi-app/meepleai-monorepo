# Task 2 Report — #2587 Slice 1: Correlate GameSession + Quota on Live-Session Start

## Status: COMPLETE ✅

## Changes Made

### Production Code
- **`StartLiveSessionCommand.cs`** — enriched record: added `UserId`, `UserTier`, `UserRole` fields
- **`LifecycleCommandHandlers.cs`** — `StartLiveSessionCommandHandler` rewritten with 2 new dependencies (`IGameSessionRepository`, `ISessionQuotaService`):
  - Guard `GameId.HasValue && CorrelatedGameSessionId == null`: check quota, create `GameSession`, call `SetCorrelatedGameSessionId`
  - Single `SaveChangesAsync` commits both atomically
  - `DbUpdateConcurrencyException` catch → re-fetch → idempotent return if already correlated
  - Free-form guard (`GameId == null`): skips quota + GameSession creation
- **`LiveSessionEndpoints.cs`** — `HandleStartSession`: resolves `UserId`/`UserTier`/`UserRole` from `TryGetActiveSession()`, passes to command

### Test Code
- **`LiveSessionCommandHandlerTests.cs`** — updated 4 existing call sites (constructor + command arity); added 5 new TDD tests (a–e); added usings for `QuotaExceededException` and `DbUpdateConcurrencyException`
- **`LiveSessionValidatorTests.cs`** — updated 2 command instantiations to 4-arg form
- **`LiveSessionRepositoryIntegrationTests.cs`** — added using; fixed 2 call sites
- **`LiveSessionDiaryChainIntegrationTests.cs`** — added using; fixed 1 call site
- **`LiveSessionDiaryEndpointTests.cs`** — added using; fixed 1 call site

## Test Summary

**GameManagement Unit Suite**: 1498 passed, 0 failed (+5 new vs 1493 baseline)

### New Tests (a–e)
- **(a)** `StartLiveSession_GameIdBacked_CreatesCorrelatedGameSessionAndChecksQuota` — quota checked, GameSession added, correlation set, single SaveChanges
- **(b)** `StartLiveSession_QuotaDenied_ThrowsQuotaExceededAndDoesNotCreateGameSession` — QuotaExceededException thrown, no AddAsync, no Start
- **(c)** `StartLiveSession_AlreadyCorrelated_SkipsGameSessionCreation` — no quota check, no AddAsync, correlation unchanged
- **(d)** `StartLiveSession_FreeForm_SkipsQuotaAndGameSessionCreation` — no quota, no AddAsync, starts, CorrelatedGameSessionId null
- **(e)** `StartLiveSession_ConcurrencyException_RefetchShowsCorrelated_ReturnsIdempotently` — DbUpdateConcurrencyException → re-fetch → correlated → returns; SaveChanges once

## Blocking Concerns
None.
