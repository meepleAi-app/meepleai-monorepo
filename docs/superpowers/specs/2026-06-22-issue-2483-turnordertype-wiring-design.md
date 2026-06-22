# Wire real turnOrderType into TurnIndicatorRenderer (design)

**Issue**: [#2483](https://github.com/meepleAi-app/meepleai-monorepo/issues/2483) · **Epic**: #2354 (G5) · **Renderer**: #2378 (G5b, merged) · **Pattern ref**: #2389/#2430 (G5a scoringType wiring)
**Date**: 2026-06-22
**Status**: APPROVED (BE source = "B con fallback ad A")

---

## 1. Context & gap

G5b `TurnIndicatorRenderer` (`apps/web/src/components/features/session-live/turn-indicator-renderer/`, 7 `TurnOrderType` branches) is merged (#2378/#2411) but fed by a **hardcoded placeholder**:

```ts
// apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx:606-615
const turnRendererState = useMemo<TurnState>((): TurnState => ({
  type: 'RoundRobin',                 // ← HARDCODED
  round: activeSession?.currentTurn ?? 0,
  totalRounds: activeSession?.totalTurns ?? 0,
  activePlayerId: activeSession?.activePlayerId ?? '',
  playOrder: activeSession?.players.map(p => p.id) ?? [],
}), [activeSession]);
```

`useLiveSessionStore` has `currentTurn`/`totalTurns`/`activePlayerId`/`players` but **no `turnOrderType`**.

**Important distinction (verified):** `Session.cs` already has `TurnOrderMethod` (string, enum `SessionTracking.Domain.Enums.TurnOrderMethod` — *how* the order was decided: Random/manual) + `TurnOrderJson` (the player order) + `TurnOrderSeed`. These are **NOT** the `TurnOrderType` (the game's turn *mode*: RoundRobin/Sequential/Simultaneous/Realtime/None/Custom/FirstPlayerToken — enum `GameToolkit.Domain.Enums.TurnOrderType`, 7 values, what the renderer switches on). So the renderer's discriminator is genuinely new data to expose.

## 2. Architecture — BE source: "B with fallback to A"

The `turnOrderType` (turn *mode*) is conceptually a property of the **game/toolkit**, not a per-session-instance config (unlike scoring, which can be configured live). So:

- **B (preferred — try first):** expose `turnOrderType` in the session live DTO by **deriving it from the game's toolkit** (chain `Session.GameId → Game → GameToolkit.TurnOrderType`). No new `Session` field, no EF migration, no runtime SignalR event — the value is static for the session and ships in the initial DTO. Simpler.
- **A (fallback):** if the `Session → Game → GameToolkit.TurnOrderType` chain is not cleanly accessible, replicate the G5a pattern: add `Session.TurnOrderType` field (+ EF migration) + `SetTurnOrderType()` + `SessionTurnOrderTypeUpdatedEvent` → SignalR `"TurnOrderTypeUpdated"` + DTO.

**Decision criterion (resolved during impl):** in BE step 1, check whether the session-live query handler can resolve `GameToolkit.TurnOrderType` for the session's game without a new aggregate field. If yes → B. If no (no FK / no toolkit / awkward join) → A. Document which path was taken in the PR.

## 3. G5a pattern to replicate (file-by-file template)

From the scoringType wiring (verified):
- **BE event** (A only): `…/SessionTracking/Domain/Events/SessionScoresUpdatedEvent.cs` → handler `…/Application/EventHandlers/SessionScoresUpdatedSignalRHandler.cs` (sends `"ScoringConfigured"`) → hub `Api/Hubs/GameStateHub.cs:317-325`.
- **BE DTO**: `…/Application/DTOs/SessionDto.cs:21,27` (`ScoringType`/`ScoreData`), built in `GetActiveSessionQueryHandler.cs:62-76`.
- **FE store**: `apps/web/src/lib/stores/live-session-store.ts:59-81` (`scoringType` field + `setScoringConfig` action).
- **FE SignalR** (A only): `apps/web/src/lib/domain-hooks/useSignalrSession.ts:49-143` (`'ScoringConfigured'` handler → `setScoringConfig`).
- **FE adapter**: `apps/web/src/lib/session-live/score-data-to-panel-data.ts:35-108` (`mapScoreDataToPanelData`).
- **FE hook**: `apps/web/src/lib/domain-hooks/useSessionScores.ts:47-63`.
- **FE view**: `SessionLiveView.tsx` consumes store → `ScoringPanelRenderer`.

## 4. Files to touch

**FE (both paths):**
- `apps/web/src/lib/stores/live-session-store.ts` — add `turnOrderType: TurnOrderType | null` + setter (populated from initial DTO; + SignalR setter if A).
- `apps/web/src/lib/session-live/map-turn-data-to-turn-state.ts` (NEW) — `mapTurnDataToTurnState(turnOrderType, sessionData) → TurnState`; maps BE enum (7) → FE union; resolves per-variant `TurnState` fields from `currentTurn`/`totalTurns`/`activePlayerId`/`playOrder`. Handle `FirstPlayerToken` (FE-only, BE enum lacks it → map from a BE signal or default).
- `SessionLiveView.tsx:606-615` — replace hardcoded `turnRendererState` with store-derived via the adapter.
- Tests: store setter, adapter (7 variants + unknown fallback), SessionLiveView renders correct branch.

**BE (B):** expose `turnOrderType` in the session-live DTO derived from the game's toolkit; map enum → string; test the query handler returns the right value.
**BE (A fallback):** `Session.cs` field + `SetTurnOrderType()` + `SessionTurnOrderTypeUpdatedEvent` + SignalR handler + hub method + DTO + EF migration; tests for handler + event.

## 5. Enum mapping (BE → FE)

BE `GameToolkit.TurnOrderType` (7): `RoundRobin=0, Custom=1, Free=2, Sequential=3, Simultaneous=4, Realtime=5, None=6`.
FE union (7): `RoundRobin | Sequential | Simultaneous | Realtime | None | Custom | FirstPlayerToken`.
Mapping: `Free` (BE) → `None` (FE) most likely; `FirstPlayerToken` (FE) has no BE enum value → derive from a BE flag or treat as a Custom/RoundRobin refinement. The adapter resolves this with an explicit table + `UnknownBranch` fallback for unmapped values (the renderer already has `UnknownBranch`).

## 6. Acceptance criteria

- [ ] `turnOrderType` exposed by BE (path B or A — documented).
- [ ] `useLiveSessionStore.turnOrderType` populated from the session DTO (+ SignalR if A).
- [ ] `mapTurnDataToTurnState` adapter maps all 7 variants + unknown fallback.
- [ ] `SessionLiveView` renders the real turn mode (no hardcoded `'RoundRobin'`).
- [ ] BE enum → FE union mapping table explicit + tested.
- [ ] Tests: FE store + adapter + view; BE query handler (B) or handler+event (A).
- [ ] `dotnet build` + BE tests pass; `pnpm typecheck` + `pnpm lint` + FE tests pass.

## 7. Out of scope

- The `TurnIndicatorRenderer` component + branches (#2378, done — no change).
- G5c `ToolkitRenderer` (#2416) and G6 per-game extensions (#2377).
- Changing `TurnOrderMethod`/`TurnOrderJson` semantics (those stay as-is — the player ordering).
- Live mid-session turn-mode changes if path B is taken (static for the session; revisit only if a game needs it).

## 8. Refs

Issue #2483 · epic #2354 · renderer #2378 · pattern #2389/#2430 · BE enum `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Enums/TurnOrderType.cs` · FE types `apps/web/src/lib/session-live/turn-state.ts`.
