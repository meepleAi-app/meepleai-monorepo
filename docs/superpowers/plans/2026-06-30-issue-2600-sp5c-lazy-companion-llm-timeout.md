# SP5-c (#2600) — Lazy companion creation + LLM stream timeout

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Checkbox (`- [ ]`) steps.

**Goal:** (1) Lazily create the SessionTracking companion (decision B) for GameId-backed `LiveGameSession`s that have `TrackingSessionId == null`, triggered on first native-stream subscribe — so legacy sessions get live domain-event broadcast. (2) Add a per-chunk timeout to the LLM stream in the session-agent chat handler so a hung chunk yields an error instead of stalling the live chat.

**Architecture:** BE-only. De-risk: `.superpowers/sdd/sp5c-derisk.md`. Owner decisions: backfill = **B (lazy on-demand)**; scope = **lazy companion + LLM-timeout**.

**Global constraints:**
- **No worktree** — commit directly on `feature/issue-2600-sp5c-lazy-companion-llm-timeout`.
- commitlint header ≤72 chars; commits end with `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **Free-form sessions (GameId == null) NEVER get a companion** — structural (companion factory requires a gameId). Lazy applies ONLY to `TrackingSessionId == null && GameId != null`.
- **Diary is native** (SP3) — it does NOT depend on the companion; do NOT add a companion dependency to diary.
- **Atomicity**: companion creation + `SetTrackingSessionId` must commit in ONE `SaveChangesAsync` (SP0 no-orphan pattern). On `DbUpdateConcurrencyException` (xmin, ADR-060) re-fetch + idempotent no-op if companion now set.
- **Async-iterator safety** (T3): the LLM-timeout must NOT break the chat stream; yield a graceful error (same discipline as SP5-b).

**Reading list:** `CompanionSessionService.cs:24-29`, `ICompanionSessionService.cs`, `CreateLiveSessionCommandHandler.cs:50-78` (companion-create pattern), `LiveGameSession.cs:68-73,99-153` (TrackingSessionId private set + Create), `AddDiaryEntryCommandHandler.cs:32-59` (handler pattern), `LiveSessionEndpoints.cs:900-985` (stream endpoint), `LiveSessionStreamGateway.cs:79-97` (re-reads session in SubscribeAsync), `ChatWithSessionAgentCommandHandler.cs:337-378` (LLM collect-loop + error yields), `OpenRouterLlmClient.cs:45,72`.

---

## Task 1: Domain method + EnsureCompanionCommand (idempotent, race-safe)
**Files:** `LiveGameSession.cs`; new `EnsureCompanionCommand.cs` + `EnsureCompanionCommandHandler.cs` (GameManagement/Application/Commands/LiveSessions); DI registration; Tests: handler unit tests + a `LiveGameSession.SetTrackingSessionId` domain test.

**Interfaces:** `EnsureCompanionCommand(Guid LiveSessionId) : IRequest` (no return, or return the companion Guid?). Uses `ILiveSessionRepository`, `ICompanionSessionService`, `IUnitOfWork`.

- [ ] **Step 1: Failing tests**:
  - Domain: `LiveGameSession.SetTrackingSessionId(companionId)` sets the property when currently null; throws (or no-ops) if already set to a different value; no-op if same value. Pick ONE explicit idempotency semantic and test it.
  - Handler: (a) session with `TrackingSessionId == null && GameId != null` → calls `CreateCompanionAsync(CreatedByUserId, GameId)`, sets TrackingSessionId, saves once; (b) session that already HAS a companion → no-op, does NOT call CreateCompanionAsync, does NOT save; (c) session with `GameId == null` → no-op (free-form, can't create), does NOT call CreateCompanionAsync; (d) concurrency: when SaveChanges throws `DbUpdateConcurrencyException`, the handler re-fetches and if TrackingSessionId is now non-null, completes successfully (idempotent) without creating a second companion.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement**:
  - `LiveGameSession.SetTrackingSessionId(Guid companionId)` domain method (guards per the chosen semantic).
  - `EnsureCompanionCommandHandler`: `GetByIdAsync` → guard `TrackingSessionId == null && GameId != null` (else return) → `CreateCompanionAsync(session.CreatedByUserId, session.GameId.Value, ct)` → `session.SetTrackingSessionId(companionId)` → `UpdateAsync` → `SaveChangesAsync`. Wrap in a try/catch for `DbUpdateConcurrencyException`: re-fetch; if `TrackingSessionId != null` now, return (someone else won the race — do NOT create another companion); else rethrow.
  - Register the handler in DI if needed.
- [ ] **Step 4: Run → PASS** + the GameManagement unit suite (no regression).
- [ ] **Step 5: Commit** — `feat(session-live): #2600 EnsureCompanionCommand + lazy SetTrackingSessionId`

---

## Task 2: Endpoint hook — ensure companion on first stream subscribe
**Files:** `LiveSessionEndpoints.cs` (the `GET /api/v1/live-sessions/{id}/stream` endpoint); Test: an integration test (Testcontainers) under `apps/api/tests/Api.Tests/Integration/GameManagement/`.

- [ ] **Step 1: Failing integration test** — create a GameId-backed `LiveGameSession` with `TrackingSessionId == null` (simulate a legacy row: insert without a companion). Subscribe to `/live-sessions/{id}/stream` as a participant. Assert: after the request starts, the session now HAS a `TrackingSessionId` persisted (companion created), AND a forwarded domain event (e.g. record a score → `session:score`) is delivered on the stream. Also a control: a `GameId == null` free-form session subscribe does NOT create a companion (TrackingSessionId stays null) and still delivers toolkit events.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** — in the stream endpoint, AFTER auth + the `GetLiveSessionStreamContextQuery` resolution + connection-limit check, BEFORE `gateway.SubscribeAsync`: `if (!context.HasCompanion) await mediator.Send(new EnsureCompanionCommand(sessionId), ct);`. The gateway re-reads the session internally so it picks up the now-non-null companion. (The handler no-ops for free-form/already-has-companion, so the guard `!HasCompanion` just avoids the extra dispatch when a companion already exists.) Keep all existing SSE headers/heartbeat/error-handling unchanged.
- [ ] **Step 4: Run → PASS** + the existing stream-endpoint tests (no regression — sessions that already have a companion are unaffected).
- [ ] **Step 5: Commit** — `feat(session-live): #2600 ensure companion on first live-stream subscribe`

---

## Task 3: LLM per-chunk stream timeout
**Files:** `ChatWithSessionAgentCommandHandler.cs` (the LLM collect-loop ~:337-349) + config (a timeout setting, e.g. `appsettings.json` under the RAG/LLM section); Test: the handler's tests.

- [ ] **Step 1: Failing tests** (use a fake `ILlmService` whose stream stalls — a chunk that never arrives / `Task.Delay` beyond the deadline):
  - A chunk that exceeds the per-chunk timeout → the handler yields a timeout error event (a `LLM_TIMEOUT` code or reuse `LLM_ERROR`) and completes the stream gracefully (does NOT hang, does NOT throw out).
  - A valid stream whose chunks each arrive within the timeout is NOT killed (full response delivered) even if the TOTAL time exceeds a single chunk timeout.
  - Client disconnect (original `ct` cancelled) is NOT reported as a timeout error (no spurious timeout event; the stream just ends).
  - The stream still completes (`StreamingComplete`) on the happy path (no regression).
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** — wrap the LLM `await foreach` consumption with a per-chunk deadline:
  - `using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);` → `await using var e = llmStream.GetAsyncEnumerator(streamCts.Token);`
  - Loop: `streamCts.CancelAfter(perChunkTimeout)` (arm), `await e.MoveNextAsync()`, on success `streamCts.CancelAfter(Timeout.Infinite)` (disarm during processing), process/buffer the chunk, repeat.
  - On `OperationCanceledException` where `streamCts.IsCancellationRequested && !ct.IsCancellationRequested` → it's a TIMEOUT → yield the timeout error event, stop. Where `ct.IsCancellationRequested` → client disconnect → just stop (no error event).
  - `perChunkTimeout` from config (provisional, e.g. 30s; mark "needs tuning"). Keep the existing buffering/citation/broadcast logic after the collect loop unchanged.
  - Respect async-iterator safety: the timeout path yields the error (outside any try that contains a yield, per C# rules) and does not abort the surrounding stream machinery.
- [ ] **Step 4: Run → PASS** + the KnowledgeBase chat suite (no regression).
- [ ] **Step 5: Commit** — `feat(session-live): #2600 per-chunk LLM stream timeout in session chat`

---

## Task 4: Integration verify + docs
**Files:** docs; issue acceptance.

- [ ] **Step 1: Build + suite** — `dotnet build` the Api project + run the GameManagement + KnowledgeBase unit suites; confirm green.
- [ ] **Step 2: Docs** — update `.superpowers/sdd/sp5c-derisk.md` with an "## Outcome" section; note in ADR-083 (or the SP0 OQ#5 reference) that lazy companion creation is shipped (on-subscribe, GameId-backed only; free-form stays toolkit-only). Add the LLM per-chunk timeout config + its provisional value to the observability/ops note.
- [ ] **Step 3: Commit** — `docs(session-live): #2600 SP5-c lazy companion + LLM timeout outcome`

---

## Self-Review
- **Lazy companion**: GameId-backed null-companion session → companion created on first subscribe, atomic, idempotent, race-safe (T1/T2). Free-form stays toolkit-only. Diary untouched (native).
- **LLM timeout**: per-chunk deadline, timeout→error event, valid stream preserved, client-disconnect distinguished, async-iterator-safe (T3).
- **No regression**: existing companion-backed sessions + chat happy-path unchanged.

## Out of scope
- Eager backfill of historical rows (decision B = lazy).
- Resilience beyond the LLM-timeout gap (already mature: Polly/circuit-breaker/graceful-degradation).
- A companion for free-form (no-GameId) sessions (structurally impossible; toolkit-only by design).
