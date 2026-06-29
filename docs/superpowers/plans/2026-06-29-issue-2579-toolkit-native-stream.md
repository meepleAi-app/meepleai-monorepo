# #2579 — Toolkit/whiteboard/timer/widget events → native stream + repoint hooks (Option B Full)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Checkbox (`- [ ]`) steps.

**Goal:** Make the native `/api/v1/live-sessions/{id}/stream` carry ALL toolkit live events (whiteboard/turn/timer on `SessionBroadcastService` + widget on `SessionSyncService`), then repoint the 3 toolkit hooks off the legacy `/game-sessions/{id}/stream/v2`. Fully unblocks the `/stream/v2` sunset (Fase 4 / #2588 Slice B). Fixes 2 latent bugs (whiteboard event-name, widget wrong-bus).

**Architecture:** Subscribe-side fan-in merge in `LiveSessionStreamGateway.SubscribeAsync` (ADR-083 SP2 ACL). De-risk: `.superpowers/sdd/2579-toolkit-repoint-derisk.md` (decision = Option B). BE + FE. No producer changes; legacy `/stream/v2` untouched (still works until Fase 4 deletion).

**Global constraints:**
- **No worktree** — commit directly on `feature/issue-2579-toolkit-native-stream`.
- Toolkit events are **live-only** (hooks `loadX()` REST on mount) → yield with `Id = null` (no `id:` line; do NOT pollute the companion Last-Event-ID/sequence resume).
- **No dedup needed**: domain events flow ONLY via companion (forwarder); toolkit ONLY via the `liveSessionId` channels. Keep it that way.
- Companion subscription (replay + visibility + sequence) stays **byte-for-byte unchanged**.
- commitlint: header ≤72 chars. Commits end with the Co-Authored-By trailer.

**Reading list:** `LiveSessionStreamGateway.cs` (the merge target), `ILiveSessionStreamGateway.cs` (`LiveSessionStreamEvent(Type,Data,Id=null)`), `SessionBroadcastService.cs` (`SubscribeAsync(key,userId,lastEventId,ct)`), `SessionSyncService.cs:30,76` (`SubscribeToSessionEvents(sessionId,ct)` yields `INotification`), `SseEventTypeMapper.cs` (`GetEventType` static), `LiveSessionEndpoints.cs:968-984` (write loop, `id:` only when `evt.Id != null`, `SseJsonOptions`), `useTurnOrder.ts:198-242`, `useWhiteboardTool.ts:227-259`, `useWidgetSync.ts:98-146`.

---

## Task 1: BE — gateway 3-way fan-in merge
**Files:** `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Services/LiveSessionStreamGateway.cs`; DI registration (find where `ILiveSessionStreamGateway` is registered); Test: the gateway's unit test (find `LiveSessionStreamGateway*Tests` or add one).

**Interfaces:** `SubscribeAsync` keeps its signature. Gateway gains an `ISessionSyncService` dependency (ctor). `SseEventTypeMapper.GetEventType` is static (no injection).

- [ ] **Step 1: Failing tests** (use test fakes/mocks for `ISessionBroadcastService` + `ISessionSyncService` + `ILiveSessionRepository`):
  - (a) An event published on the `SessionBroadcastService[liveSessionId]` channel (whiteboard/turn/timer) reaches a native subscriber with `Id == null`.
  - (b) An `INotification` (e.g. `WidgetStateUpdatedEvent`) published on `SessionSyncService[liveSessionId]` reaches the subscriber as `Type == SseEventTypeMapper.GetEventType(evt)` (`session:toolkit`), `Data == evt`, `Id == null`.
  - (c) A companion-channel envelope still reaches the subscriber with its **original `Id` preserved** (sequence intact).
  - (d) When `TrackingSessionId == null`, sources (a)+(b) STILL deliver (no `yield break`); companion source is simply skipped.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** the merge:
  - Build an unbounded `Channel<LiveSessionStreamEvent>`.
  - Pump 1 (companion, only if `companionId != null`): `await foreach` `_broadcast.SubscribeAsync(companionId.Value, userId, lastEventId, ct)` → `new LiveSessionStreamEvent(env.EventType, env.Data, env.Id)` (preserve Id).
  - Pump 2 (SBS toolkit): `await foreach` `_broadcast.SubscribeAsync(liveSessionId, userId, null, ct)` → `new LiveSessionStreamEvent(env.EventType, env.Data, null)` (drop Id).
  - Pump 3 (SSS widget): `await foreach` `_syncService.SubscribeToSessionEvents(liveSessionId, ct)` → `new LiveSessionStreamEvent(SseEventTypeMapper.GetEventType(evt), evt, null)`.
  - Each pump writes to `channel.Writer`; complete the writer when ALL pumps finish (or `ct` cancels). Iterator reads `channel.Reader.ReadAllAsync(ct)` and yields.
  - Wrap each pump body so one source faulting/completing does not kill the others (a single source ending is normal).
- [ ] **Step 4: Run → PASS** + the existing gateway suite (companion path unchanged).
- [ ] **Step 5: Commit** — `feat(session-live): #2579 gateway fan-in toolkit + widget onto native stream`

---

## Task 2: BE — concurrency, cleanup, no-double-delivery regression
**Files:** Test only (extend Task 1's test class).

- [ ] **Step 1: Tests**:
  - (a) **Cancellation**: cancel the subscription `ct` → the returned enumerable completes, and all 3 underlying subscriptions are torn down (assert the `SessionSyncService` subscriber bag empties / the broadcast unsubscribes — assert via the fakes' disposal/cancellation).
  - (b) **No double-delivery**: a domain event published via the forwarder path (companion) appears exactly ONCE (it is NOT also on the `liveSessionId` SBS channel).
  - (c) **Replay still works**: a `lastEventId` is forwarded ONLY to the companion subscription (Pump 1), NOT to Pump 2/3 (assert the fake records `lastEventId == null` for the `liveSessionId` SBS call).
- [ ] **Step 2: Run** — green if Task 1 wired correctly; else fix the merge.
- [ ] **Step 3: Verify** the cancellation test genuinely exercises teardown (not a no-op).
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** — `test(session-live): #2579 gateway merge cancellation + no-double-delivery`

---

## Task 3: FE — repoint useTurnOrder + useWidgetSync (mechanical)
**Files:** `apps/web/src/lib/domain-hooks/useTurnOrder.ts`, `apps/web/src/lib/domain-hooks/useWidgetSync.ts`; Tests: their `__tests__`.

- [ ] **Step 1: Update tests** — assert each hook opens an `EventSource` to `/api/v1/live-sessions/{sessionId}/stream` (NOT `/game-sessions/{sessionId}/stream/v2`); listener stays `session:toolkit`; payload discrimination unchanged (turn fields / `widgetType` filter).
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** — change ONLY the EventSource URL in both hooks to the native endpoint. Remove the obsolete "SP2: stays on legacy … see #2561 / SP5" comment block. Keep `session:toolkit` + the existing payload handling.
- [ ] **Step 4: Run → PASS** + the toolkit-component tests that mount these hooks (no regression).
- [ ] **Step 5: Commit** — `fix(session-live): #2579 repoint useTurnOrder + useWidgetSync to native stream`

---

## Task 4: FE — repoint useWhiteboardTool + fix event-name bug
**Files:** `apps/web/src/lib/domain-hooks/useWhiteboardTool.ts`; Test: its `__tests__`.

- [ ] **Step 1: Update/extend tests** — assert the hook opens an `EventSource` to `/api/v1/live-sessions/{sessionId}/stream` AND registers its listener for **`session:whiteboard`** (the corrected event name), applying stroke-added/structured-updated/whiteboard-cleared sub-types by the `type` payload field.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** — change the EventSource URL to native AND `addEventListener('session:toolkit', …)` → `addEventListener('session:whiteboard', …)`. Remove the obsolete SP2 legacy comment.
- [ ] **Step 4: Run → PASS** + WhiteboardTool component test (no regression).
- [ ] **Step 5: Commit** — `fix(session-live): #2579 repoint whiteboard hook + correct session:whiteboard event`

---

## Task 5: Integration verify + sunset-unblock note
**Files:** grep audit; docs note; (no code beyond comment cleanup if any remains).

- [ ] **Step 1: Grep** `stream/v2` across `apps/web/src` → assert ZERO remaining consumers (all 3 hooks repointed). If any other consumer exists, report it (do not silently leave it).
- [ ] **Step 2: Verify** the legacy `/stream/v2` endpoint + the v1 `/stream` endpoint are now consumer-free from the FE (note: BE endpoints stay until Fase 4 deletion ≥2026-09-29 per the Sunset header — do NOT delete them here).
- [ ] **Step 3: Update** `.superpowers/sdd/2579-toolkit-repoint-derisk.md` outcome + the gating note (Fase 4 / #2588 Slice B `/stream/v2` sunset now FE-unblocked). Update issue #2579 acceptance in the PR body.
- [ ] **Step 4: Commit** — `docs(session-live): #2579 confirm /stream/v2 FE-unblocked for Fase 4 sunset`

---

## Self-Review
- **AC**: native stream carries whiteboard (`session:whiteboard`) + turn/widget (`session:toolkit`) + timer (`session:timer`); 3 hooks consume the native stream; whiteboard event-name fixed; widget no longer on the wrong bus.
- **Live-only**: toolkit events `Id = null` → companion resume unaffected (T1/T2).
- **No regression**: companion domain-event path + replay + visibility unchanged (T1c, T2c).
- **Scope honesty**: BE legacy endpoints NOT deleted (Fase 4 owns that); only the FE consumers move + the gate is unblocked.

## Out of scope
- Deleting `/stream/v2` + v1 `/stream` endpoints (Fase 4 / #2588 Slice B, ≥2026-09-29).
- Sharing one native EventSource across the 3 hooks (each keeps its own connection — matches current behavior).
- Adding toolkit types to the shared `parse-sse-event.ts` (hooks self-parse; not needed).
