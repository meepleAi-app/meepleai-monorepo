# WS1 — interactive start-next-game + max-1-live blocked modal + LIVE badge + #2647 fix

**Issues**: closes [#2633](https://github.com/meepleAi-app/meepleai-monorepo/issues/2633) (SI-2 acceptance) + [#2647](https://github.com/meepleAi-app/meepleai-monorepo/issues/2647) (OpenLiveMode gap) · epic [#2619](https://github.com/meepleAi-app/meepleai-monorepo/issues/2619)
**Date**: 2026-07-04
**Method**: `/sc:spec-panel` (Fowler · Newman · Nygard · Adzic · Cockburn) → synthesis
**Status**: DESIGNED — 11 locked decisions; 3 product calls open (§4).

---

## 1. What the panel uncovered (real bugs, not just #2647)

The naive WS1 ("re-enable start + catch 409") sits on top of **serious pre-existing domain defects** in the start flow:

1. **Phantom double-night ownership** — `StartGameNightSessionCommandHandler` dispatches `CreateSessionCommand` with `GameNightEventId=null` → `CreateSessionCommandHandler.ResolveGameNightAsync` mints a **new ad-hoc InProgress night** + link, while the outer handler ALSO links the same session via `AddSession` → the tracking Session is owned by **two** `GameNightEvent`s → `FindByLinkedSessionIdAsync` is nondeterministic. **This breaks #2633** (the live session must belong to the night you opened).
2. **Orphan session on blocked 2nd-open** — `CreateSessionCommand` commits a durable Session *before* `StartCurrentSession`'s max-1-live 409 fires → every blocked open leaks an orphan.
3. **Concurrency 500** — two concurrent starts both pass the in-memory guard; the xmin loser throws an uncaught `DbUpdateConcurrencyException` (the `catch` only handles `InvalidOperationException`) → HTTP 500 + orphan.
4. **Error code dropped** — `ConflictException(string)` hardcodes `errorCode='conflict'`, so `MaxLiveSessionsExceededException.Code` never reaches the HTTP body → FE can't discriminate the max-live 409.
5. **#2647 mis-fix trap** — putting `OpenLiveMode` inside `CreateSessionCommandHandler` dispatches `SessionStartedDomainEvent` *before* the aggregate link is committed → `FindByLinkedSessionIdAsync` resolves null/phantom → promotion silently skipped. The fix must be a dedicated command dispatched **last**.

## 2. Locked decisions

| ID | Decision |
|---|---|
| **DEC-1** | New SessionTracking `OpenSessionLiveModeCommand(SessionId, CallerUserId)` dispatched from the start handler (+ gamebook twin) as the **LAST** step, AFTER `AddSession`+`StartCurrentSession`+`SaveChanges`. NOT inside `CreateSessionCommandHandler`. |
| **DEC-2** | Keep the Published→InProgress promotion **event-driven** via the existing `SessionStartedHandler` → `GameNightEvent.HandleFirstSessionStarted`. No direct `Status` mutation. |
| **DEC-3** | Add `bool SkipGameNightEnvelope=false` to `CreateSessionCommand`. Both orchestrators pass `true` → `CreateSessionCommandHandler` skips `ResolveGameNightAsync` + the link + game-night diary rows; `GameNightEvent.AddSession` is the **sole** linker against `command.GameNightId`. Kills the phantom + the Published-precondition collision. |
| **DEC-4** | Guard-before-create: `GameNightEvent.EnsureCanStartSession()` (throws `MaxLiveSessionsExceededException` if any child InProgress) called at the TOP of `Handle`, before `CreateSessionCommand`. Blocked 2nd-open persists **zero rows**. |
| **DEC-5** | Wrap the aggregate `SaveChangesAsync`, catch `DbUpdateConcurrencyException` (xmin) → rethrow `MaxLiveSessionsExceededException` → 409 (not 500). |
| **DEC-6** | `OpenSessionLiveModeCommandHandler` idempotent: `if session.IsLive return Unit` (no 409); else `OpenLiveMode()`; save. |
| **DEC-7** | Add `ConflictException(string errorCode, string message)` ctor; `MaxLiveSessionsExceededException` passes `Code='MAX_LIVE_SESSIONS_EXCEEDED'` so the FE can discriminate. |
| **DEC-8** | Apply DEC-1/3/4/5 identically to `AttachGamebookCampaignToGameNightCommandHandler` via a shared private helper. |
| **DEC-9** | FE read-only by default; organizer-only "Avvia prossimo gioco" CTA. Add `bool IsViewerOrganizer` to `GameNightLiveDto` (from `gameNight.OrganizerId==query.CallerUserId`, already computed) → threaded through `mapNightLive` into the VM. |
| **DEC-10** | FE race avoidance = optimistic disable + server-409 authority. CTA disabled while pending AND when `vm.status==='live'`. On success invalidate `gameNightLiveKeys.detail(id)` and re-derive; do NOT wire `useGameNightMultiSession` (0 consumers, split-brain). On 409 `MAX_LIVE_SESSIONS_EXCEEDED` → blocked modal; other 409 → generic toast. |
| **DEC-11** | LIVE badge derives PURELY from the read-model InProgress session; **#2647 is NOT a prerequisite for the badge** (status/currentGame already derive from `GameNightSession.Status==InProgress`, set synchronously by `StartCurrentSession`). #2647's payoff = `Session.StartedAt` for the `/sessions/[id]` timer + the night promotion that gates gamebook-attach/completion. |

## 3. Backend flow (resolved) + file-by-file plan

**Flow** (`StartGameNightSessionCommandHandler`, mirrored in the gamebook-attach twin):
1. Load `GameNightEvent` (Published), authorize organizer.
2. `gameNight.EnsureCanStartSession()` — 409 if any child InProgress (zero rows on block).
3. `CreateSessionCommand` with `SkipGameNightEnvelope=true` — tracking Session + participants only (StartedAt null), no phantom night/link/diary.
4. `AddSession` + `StartCurrentSession` → `SaveChanges`, wrapped to map `DbUpdateConcurrencyException`→409. Single durable link; **badge already correct**.
5. `OpenSessionLiveModeCommand(sessionId, userId)` — idempotent, `OpenLiveMode()` sets StartedAt + raises event → existing handler promotes Published→InProgress. **This is where #2647 is fixed.**

**Transaction/event boundary** (locked): two aggregates, three SaveChanges, eventually consistent within one request (ADR-060, not a distributed tx). Ordering is load-bearing: OpenLiveMode strictly after the link commit; `SkipGameNightEnvelope` guarantees exactly one link → deterministic resolution.

**Files** — BE: (1) `CreateSessionCommand.cs` +flag · (2) `CreateSessionCommandHandler.cs` skip-branch · (3) `GameNightEvent.cs` `EnsureCanStartSession()` · (4) NEW `OpenSessionLiveModeCommand.cs`+handler · (5) `ConflictException.cs` ctor · (6) `MaxLiveSessionsExceededException.cs` code · (7) `StartGameNightSessionCommandHandler.cs` reorder+dispatch · (8) `AttachGamebookCampaignToGameNightCommandHandler.cs` parity · (9) `GameNightLiveDto.cs` `IsViewerOrganizer` · (10) `GetGameNightLiveQueryHandler.cs` populate. FE: (11) `game-nights.schemas.ts` · (12) `mapNightLive.ts` thread flag + expose next-Pending game · (13) `gameNightSessionClient.ts` surface `errorCode` · (14) NEW start-next RQ mutation · (15) `NightLiveClientView.tsx`/`NightLiveHub.tsx` CTA + modal · (16) NEW `BlockedLiveSessionModal`.

## 4. Product calls — RESOLVED 2026-07-04

1. **CTA "next game" source = next Pending from the lineup.** The read model exposes the night's **un-started lineup** (GameIds in `GameIdsJson` that have no `GameNightSession` yet) as planned/upcoming slots; the CTA starts the next un-started GameId (`AddSession`+`StartCurrentSession` create+start it). No lineup → CTA hidden. This extends `mapNightLive` + the live DTO to surface un-started games (Slice B's `plannedGames` today only reflects *started* sessions).
2. **Blocked-modal copy = honest.** "C'è già una partita live. Aprila per continuare." + a jump-to-session action. WS1 cannot complete a game, so no "resolve it" promise.
3. **Direct POST /sessions one-click flow IS reconciled in WS1.** Its `Session.StartedAt` is also set (total consistency — every started session goes live). Mechanism: the final `OpenSessionLiveMode` step runs for the direct/ad-hoc path too (its ad-hoc night is already InProgress → `HandleFirstSessionStarted` is an idempotent no-op, only `StartedAt` is set). DEC-1's "not inside CreateSessionCommandHandler" still holds for the game-night orchestrators; the exact wiring for the direct path (endpoint-level dispatch vs handler) is settled during implementation, keeping OpenLiveMode a single dedicated command.

## 5. Acceptance (Given/When/Then) — see synthesis; BE via real pipeline (not fixtures)

Happy path (StartedAt set, 1 event, 1 night, 1 link, Published→InProgress once) · blocked no-race (409 `MAX_LIVE_SESSIONS_EXCEEDED`, zero rows) · concurrency (loser 409 not 500) · idempotent re-start · direct-flow regression · gamebook parity · FE badge from read model · organizer gate · optimistic disable · 409 discrimination · post-success derivation · scope boundary (start only, no complete).
