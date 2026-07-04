# #2587-A Slice 1 — correlate-at-start + quota + lifecycle-sync

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Checkbox (`- [ ]`) steps.

**Goal:** Direction A core: when a `LiveGameSession` STARTS, atomically create a correlated GameManagement `GameSession` (lifecycle/quota aggregate), enforce the per-user session quota, and store `CorrelatedGameSessionId`. Sync the correlated GameSession's completion to the LiveGameSession's. Fixes the quota-bypass + history-invisibility for started GameId-backed sessions.

**Architecture:** BE-only (GameManagement). De-risk: `.superpowers/sdd/2587-slice1-derisk.md`. Design: `docs/superpowers/specs/2026-06-30-issue-2587-funnel-convergence-design.md` (Direzione A ratified). Correlation happens at **START** (players exist), NOT at create.

**Global constraints:**
- **No worktree** — commit directly on `feature/issue-2587-slice1-correlate-quota`.
- commitlint ≤72; commits end with `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **Free-form (GameId == null)** sessions get NO correlated GameSession (GameSession requires a gameId) — structural, like the SP0 companion. No quota for them (acceptable).
- **Atomicity**: GameSession add + LiveGameSession update (SetCorrelatedGameSessionId) commit in ONE SaveChanges. xmin race-safe (ADR-060) — on `DbUpdateConcurrencyException` re-fetch + idempotent (SP5-c pattern).
- **Idempotency**: a re-start of an already-correlated session must NOT create a second GameSession.
- **Quota counts GameSessions** (`ISessionQuotaService` → `IGameSessionRepository.CountActiveByUserIdAsync`) → effective once correlation creates them. Check quota BEFORE creating the new GameSession (so it doesn't count itself).

**Reading list:** `LifecycleCommandHandlers.cs:13-41` (start), `:116-144` (complete); `LiveSessionEndpoints.cs:478-485` (HandleStartSession); `GameEndpoints.cs:448-456` (tier/role resolution pattern); `StartGameSessionCommandHandler.cs` (quota + GameSession.Create + SessionPlayer mapping); `LiveGameSession.cs:78` (Players, LiveSessionPlayer shape); `EnsureCompanionCommandHandler.cs` (SP5-c xmin idempotent pattern).

---

## Task 1: Domain + persistence — CorrelatedGameSessionId
**Files:** `LiveGameSession.cs`; EF config (LiveGameSession entity config); a new EF migration; Tests: domain test.

- [ ] **Step 1: Failing test** — `LiveGameSession.SetCorrelatedGameSessionId(Guid)`: null→set; same→no-op; different→throw `InvalidOperationException`; empty→`ArgumentException`. (Mirror `SetTrackingSessionId` from SP5-c.)
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** — add `CorrelatedGameSessionId` (`Guid?`, private set) + the domain method (XML doc); add the EF mapping (nullable column) in the LiveGameSession entity config (mirror how `TrackingSessionId` is mapped); generate a migration `dotnet ef migrations add AddCorrelatedGameSessionId` (review the SQL: one nullable `correlated_game_session_id` column; no data change). Map in `LiveGameSessionMapper` ToEntity/ToDomain (mirror `TrackingSessionId`).
- [ ] **Step 4: Run → PASS** + the GameManagement domain/mapper suite + `dotnet build`.
- [ ] **Step 5: Commit** — `feat(session-live): #2587 LiveGameSession.CorrelatedGameSessionId + migration`

---

## Task 2: Correlate-at-start + quota
**Files:** `LiveSessionEndpoints.cs` (HandleStartSession), `StartLiveSessionCommand.cs` (enrich), `LifecycleCommandHandlers.cs` (StartLiveSessionCommandHandler); a GameSession-creation seam (inject `IGameSessionRepository` + `ISessionQuotaService` into the start handler, OR a small `ICorrelatedGameSessionService` ACL — your choice, keep it in GameManagement); Tests: handler unit tests.

- [ ] **Step 1: Failing tests** (mock repos + quota service):
  - (a) Start of a GameId-backed session with players + `CorrelatedGameSessionId == null` → quota checked, GameSession created (players mapped: LiveSessionPlayer.displayName→PlayerName, index→PlayerOrder, default Color), `SetCorrelatedGameSessionId` applied, `session.Start` called, ONE SaveChanges.
  - (b) Quota exceeded → `QuotaExceededException`, NO GameSession created, NO start.
  - (c) Already-correlated session (re-start) → NO second GameSession, idempotent.
  - (d) Free-form `GameId == null` → NO GameSession, NO quota check, `session.Start` still happens (toolkit-only session can still start).
  - (e) Concurrency: `DbUpdateConcurrencyException` on save → re-fetch; if now correlated → idempotent success.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement**:
  - Endpoint `HandleStartSession`: add `HttpContext`/principal; resolve `userId` + `UserTier.Parse(principal.Tier)` + `UserRole` (mirror `GameEndpoints.cs:448`); pass into an enriched `StartLiveSessionCommand(sessionId, userId, userTier, userRole)`.
  - Handler: GetById → if `GameId.HasValue && CorrelatedGameSessionId == null`: `CheckQuotaAsync` (throw `QuotaExceededException` if denied) → create `GameSession` (map players) → `session.SetCorrelatedGameSessionId(gameSession.Id)` → `_gameSessionRepository.AddAsync(gameSession)`. Then `session.Start(_timeProvider)` → `UpdateAsync(session)` → ONE `SaveChangesAsync`. Wrap save in try/catch `DbUpdateConcurrencyException` → re-fetch + idempotent (SP5-c pattern). DI: register any new deps.
  - Player mapping: confirm `SessionPlayer(PlayerName, PlayerOrder, Color)` — if `Color` is required and LiveSessionPlayer has none, use a deterministic default palette by index.
- [ ] **Step 4: Run → PASS** + the GameManagement unit suite + build.
- [ ] **Step 5: Commit** — `feat(session-live): #2587 correlate GameSession + quota on live-session start`

---

## Task 3: Lifecycle-sync — complete the correlated GameSession
**Files:** `LifecycleCommandHandlers.cs` (CompleteLiveSessionCommandHandler + Abandon handler if one exists); Tests: handler unit tests.

- [ ] **Step 1: Failing tests**:
  - (a) Complete a LiveGameSession with `CorrelatedGameSessionId != null` → the correlated GameSession is loaded + Completed (so it stops counting active for quota); ONE SaveChanges.
  - (b) Complete a session with `CorrelatedGameSessionId == null` (free-form) → no-op on GameSession, LiveGameSession still completes.
  - (c) If an Abandon handler exists, mirror: abandon LiveGameSession → abandon/terminate the correlated GameSession.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** — in CompleteLiveSessionCommandHandler: after `session.Complete(...)`, if `CorrelatedGameSessionId != null` → `gameSession = _gameSessionRepository.GetByIdAsync(CorrelatedGameSessionId)` → `gameSession.Complete(...)` (use the GameSession lifecycle method; check the exact name — Complete/End) → `_gameSessionRepository.UpdateAsync(gameSession)`. ONE SaveChanges for both. (Inject `IGameSessionRepository`.) Apply the same to Abandon if present.
- [ ] **Step 4: Run → PASS** + suite + build.
- [ ] **Step 5: Commit** — `feat(session-live): #2587 complete correlated GameSession on live-session complete`

---

## Task 4: Integration (Testcontainers)
**Files:** integration test under `apps/api/tests/Api.Tests/Integration/GameManagement/`.

- [ ] **Step 1: Tests**:
  - Start a GameId-backed LiveGameSession with players → a GameSession row is created + `CorrelatedGameSessionId` persisted + the GameSession appears in the user's active-sessions query (`IGameSessionRepository` active list / `api.sessions.getActive` equivalent).
  - Quota: with a tier whose limit is N, start N sessions → the (N+1)th start returns quota-exceeded (409/the mapped status).
  - Complete the LiveGameSession → the correlated GameSession is Completed → it no longer counts active (a subsequent start is allowed again).
  - Free-form (GameId == null) start → no GameSession created, start succeeds.
- [ ] **Step 2: Run** (needs Docker; if unavailable locally, write + compile, note they run in CI — do NOT mark passing if not run).
- [ ] **Step 3: Commit** — `test(session-live): #2587 correlate+quota+lifecycle integration`

---

## Self-Review
- Quota-bypass fixed: started GameId-backed sessions now create a counted GameSession; quota enforced at start (T2); freed at complete (T3).
- History-visibility fixed: started sessions now have a GameSession → appear in `api.sessions.getActive` (T4).
- Atomic + idempotent + race-safe (T2). Free-form structurally excluded (no GameId).
- Honors Opzione-B: GameSession is now populated for real sessions (its quota/history role becomes real).

## Out of scope (future slices)
- Slice 2: backfill lazy for legacy `CorrelatedGameSessionId == null` sessions.
- Slice 3: FE e2e (create→start→visible-in-history) + scoring source-of-truth doc.
- Pause/Resume → GameSession sync (only if quota counts pause as active differently — verify, else skip).
