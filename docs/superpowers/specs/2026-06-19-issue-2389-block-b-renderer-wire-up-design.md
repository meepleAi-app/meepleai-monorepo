# Issue #2389 Block B — Renderer wire-up design

**Date**: 2026-06-19
**Branch**: `feature/issue-2389-block-b-renderer-wire-up`
**Parent**: `main-dev`
**Effort**: ~6-8h focused (1 day)
**Author**: Aaron Degrassi
**Status**: Design approved — proceeding to implementation plan

---

## Context

Issue [#2389](https://github.com/meepleAi-app/meepleai-monorepo/issues/2389) is a 3-block migration of the live-session store toward polymorphic scoring (`ScoreType` enum: `Points | BinaryWin | Ranking | Objectives`).

- **Block A** (PR [#2428](https://github.com/meepleAi-app/meepleai-monorepo/pull/2428), merged 2026-06-17): store contract evolution + SignalR `ScoringConfigured` event + `useSessionScores` hook extension + `setScoringConfig` action + ESLint rule `local/no-store-scores-direct` (warn). No consumer wire-up.
- **Block B** (this design): wire the polymorphic store selector into `SessionLiveView`'s read-only `ScoringPanelRenderer`, replacing the hardcoded `kind: 'Points'` adapter shipped by PR #2423 (G5a closure).
- **Block C** (+14gg later): delete deprecated `scores: Record<string, number>` field + sweep all direct `state.scores` reads + i18n catalog completion.

This document defines the Block B scope, design decisions, and implementation contract. The implementation plan is published separately at `docs/superpowers/plans/2026-06-19-issue-2389-block-b-renderer-wire-up.md` (per skill `superpowers:writing-plans`).

## Scope

**In-scope**:
1. Wire polymorphic `scoringType` + `scoreData` from the live-session store into `SessionLiveView`'s `scoringPanelData` memo.
2. Replace the hardcoded `kind: 'Points'` adapter (lines 947-959 of `SessionLiveView.tsx`) with a pure-function adapter handling all 4 `ScoreType` variants.
3. Add a REST hydration `useEffect` that pre-populates `scoringType`/`scoreData` from `sessionQuery.data` on initial mount, closing the ~1-2s SignalR handshake gap.
4. Gate the renderer mount on `scoringPanelData != null` (strict null gate per AC #2 of #2389).
5. Hoist `MVP_OBJECTIVES_CATALOGUE` from `scores/page.tsx` to a shared lib module for editor + adapter co-consumption.
6. Add unit tests for the adapter (~14 cases) and integration tests for `SessionLiveView` (+8 cases).

**Out-of-scope (documented gaps)**:
- `useUpdateSessionScores` mutation wire — `ScoringPanelRenderer` is read-only by design. Editor mutation belongs to `PolymorphicScoreEditor` swap, deferred to Block B+ follow-up.
- Toast 403/429 — n/a without mutation wire.
- Legacy `PUT /api/v1/game-sessions/{id}/participants/{playerId}/score` endpoint carve-out — `_handleScoreUpdate` callback in `SessionLiveView` (lines 507-580) is currently dead code; left untouched.
- `EndgameDialog.finalScores` polymorphic adapter — currently hardcoded `{ playerName, score, isWinner: false }` at lines 1364-1368; deferred to separate sub-issue.
- Backend changes — Block A already evolved `SessionDto.ScoringType` + `SessionDto.ScoreData` + `SessionScoresUpdatedSignalRHandler`. No BE work in Block B.
- Multi-pod SignalR fan-out backplane — not relevant; Block B is read-side only.

## Design Decisions

### DEC-1 — Scope: Renderer-only

Block B wires the polymorphic store selector into the **read-only** `ScoringPanelRenderer`. Editor mutation, toast feedback, and EndgameDialog adapter are explicitly deferred. Rationale: the `ScoringPanelRenderer` exposes no `onScoreChange` prop; adding one would expand renderer scope. The `PolymorphicScoreEditor` (shipped in Asse D follow-up P1 #1899) is the mutable counterpart and lives in a separate component tree (`components/sessions/PolymorphicScoreEditor.tsx`). Swapping the renderer for the editor in host mode requires layout work, debounce wiring, and 403/429 toast plumbing — multi-day scope, separate ticket.

**Consequence**: acceptance criteria #4 (mutation wire), #5 (toast), and #6 (legacy carve-out) from the kickoff prompt are explicitly **not satisfied** by Block B. They are tracked as follow-up items (see "Follow-up" section).

### DEC-2 — Adapter: pure function module

The adapter `(scoringType, scoreData, players) → ScoringPanelData | null` lives in a new pure-function module at `apps/web/src/lib/session-live/score-data-to-panel-data.ts`. SessionLiveView wraps it in a `useMemo` callsite.

Rationale:
- Testable in isolation via Vitest with no React/hook ceremony.
- Decoupled from `useLiveSessionStore` — adapter accepts plain data, returns plain data.
- Reusable from both desktop right column (`tab === 'score'`) and mobile drawer (`mobileTab === 'score'`).
- Single source of truth for the editor-shape → renderer-shape mapping.

Alternatives rejected:
- Hoisting into `useSessionScores` hook would couple the hook to a renderer-specific shape.
- Inline `useMemo` in `SessionLiveView` would inflate the orchestrator (already 1384 lines) with 4 switch cases and make testing harder.

### DEC-3 — Null gate: REST hydration + strict null

The strict null gate per AC #2 ("renderer non renderizza quando scoringType null") is correct, but only acceptable if the null window is short. Today the store's `scoringType` is populated only by `useSignalrSession` (SignalR `ScoringConfigured` event), which fires after the WebSocket handshake completes (~1-2s post-mount). Without mitigation, every session-live open shows an empty score tab for 1-2s.

**Mitigation**: a `useEffect` in `SessionLiveView` reads `sessionQuery.data?.scoringType` and `sessionQuery.data?.scoreData` (already exposed by Block A's BE evolution) and calls `setScoringConfig` to pre-populate the store. This closes the gap from ~1-2s to ~200-500ms (REST request latency).

Subsequent SignalR `ScoringConfigured` pushes overwrite the REST-hydrated state — SignalR remains the authoritative live-update channel.

JSON.parse failure (malformed `scoreData` string) is silently swallowed; SignalR will deliver canonical state. No user-visible error.

### DEC-4 — Objectives catalogue: hoist MVP_OBJECTIVES_CATALOGUE

The `Objectives` renderer variant requires a full catalogue (`objectives: ObjectiveScoringItem[]`) that is not part of `ScoreDataByType['Objectives']` (which only has `completedByPlayer`). Today a hardcoded `MVP_OBJECTIVES_CATALOGUE: readonly string[]` lives in `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx:73-80`.

**Decision**: hoist the constant to `apps/web/src/lib/session-live/mvp-objectives-catalogue.ts`. Both the existing editor (`scores/page.tsx`) and the new adapter import it from the shared location. The adapter accepts it as an optional `availableObjectives` parameter (preserving purity — no implicit imports).

Real game-level catalogue wiring is deferred to a follow-up issue; the MVP constant is a placeholder shared by both editor and renderer until then.

## Architecture

### File changes summary

| Action | Path | LOC est. |
|--------|------|----------|
| NEW | `apps/web/src/lib/session-live/mvp-objectives-catalogue.ts` | ~10 |
| NEW | `apps/web/src/lib/session-live/score-data-to-panel-data.ts` | ~90 |
| NEW | `apps/web/src/lib/session-live/score-data-to-panel-data.test.ts` | ~180 (14 test) |
| MOD | `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` | ~30 lines (replace memo + add useEffect + selectors) |
| MOD | `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.test.tsx` | ~140 (+8 test) |
| MOD | `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx` | ~3 lines (import path + remove inline constant) |

Total: ~3 new files, 3 modified files, ~450 lines net (mostly test code).

### Adapter contract

```typescript
// apps/web/src/lib/session-live/score-data-to-panel-data.ts

export function mapScoreDataToPanelData(
  scoringType: ScoreType | null,
  scoreData: ScoreDataByType[ScoreType] | null,
  players: ReadonlyArray<{
    readonly id: string;
    readonly name: string;
    readonly displayName?: string;
  }>,
  options?: {
    readonly availableObjectives?: ReadonlyArray<string>;
  }
): ScoringPanelData | null;
```

**Behavior contract**:
- Returns `null` iff `scoringType === null` OR `scoreData === null`.
- `players[]` is the master list: all players appear in the output. Missing `scoreData` entries are padded with type-specific defaults (`Points: 0`, `Ranking: players.length`, `BinaryWin: false`, `Objectives: []`).
- `displayName` falls back to `name` per-player when undefined.
- `Objectives` variant: catalogue from `options.availableObjectives ?? []`. Each catalogue entry becomes `{ id: label, label, done: anyPlayerCompleted }`.
- Pure function: no side effects, no implicit imports, deterministic.

### SessionLiveView wire

1. New imports:
   ```typescript
   import { mapScoreDataToPanelData } from '@/lib/session-live/score-data-to-panel-data';
   import { MVP_OBJECTIVES_CATALOGUE } from '@/lib/session-live/mvp-objectives-catalogue';
   import { useLiveSessionStore } from '@/lib/stores/live-session-store';
   import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
   ```

2. Store selectors:
   ```typescript
   const scoringType = useLiveSessionStore(s => s.scoringType);
   const scoreData = useLiveSessionStore(s => s.scoreData);
   const setScoringConfig = useLiveSessionStore(s => s.setScoringConfig);
   ```

3. REST hydration effect (placed after `liveStream` declaration):
   ```typescript
   useEffect(() => {
     const dto = sessionQuery.data;
     if (dto?.scoringType == null || dto.scoreData == null) return;
     try {
       const parsed = JSON.parse(dto.scoreData) as ScoreDataByType[ScoreType];
       setScoringConfig({
         scoringType: dto.scoringType as ScoreType,
         scoreData: parsed,
       });
     } catch {
       // Malformed JSON: skip. SignalR will deliver canonical state.
     }
   }, [sessionQuery.data, setScoringConfig]);
   ```

4. Replace `scoringPanelData` memo (lines 947-959):
   ```typescript
   const scoringPanelData = useMemo<ScoringPanelData | null>(
     () =>
       mapScoreDataToPanelData(
         scoringType,
         scoreData,
         activeSession?.players ?? [],
         { availableObjectives: MVP_OBJECTIVES_CATALOGUE }
       ),
     [scoringType, scoreData, activeSession?.players]
   );
   ```

5. Gate renderer mount in desktop right column (line 1242-1244):
   ```typescript
   {tab === 'score' && scoringPanelData != null && (
     <ScoringPanelRenderer data={scoringPanelData} labels={scoringPanelLabels} className="p-3" />
   )}
   ```

6. Gate renderer mount in mobile drawer (line 1103-1112):
   ```typescript
   case 'score':
   default:
     return scoringPanelData != null ? (
       <ScoringPanelRenderer data={scoringPanelData} labels={scoringPanelLabels} className="p-2" />
     ) : null;
   ```

### Backward compatibility

- `activeSession.players` (composed from REST + SSE via `composeSessionLiveState`) remains the source of truth for turn indicator, roster, action log, and adapter player master list.
- Store `players` (parallel state from SignalR `setSession`) is not consumed by Block B — `activeSession.players` is the master list to maintain visual consistency with turn indicator, roster, and action log (all REST+SSE composed). Any temporary divergence is intentional and reverted once `useLiveSessionStore` becomes the single source of truth (Block C+).
- Legacy `useLiveSessionStore.scores` (playerName-keyed, `@deprecated` in Block A) is not read or written by Block B. The ESLint rule `local/no-store-scores-direct` remains at `warn` level until Block C.

## Testing

### Unit tests: `score-data-to-panel-data.test.ts`

14 cases organized in 4 groups:

| Group | Cases | Notes |
|-------|-------|-------|
| Null gates | 3 | scoringType null, scoreData null, both null |
| Happy path | 4 | One per variant: Points, BinaryWin, Ranking, Objectives |
| displayName fallback | 1 | undefined displayName → name |
| Missing player padding | 4 | One per variant: defaults applied |
| Objectives catalogue edge | 2 | Empty catalogue, done=true when any completed |

### Integration tests: `SessionLiveView.test.tsx` extension

8 new cases, additive to existing 67+:

| Group | Cases | Notes |
|-------|-------|-------|
| REST hydration | 3 | DTO with config / malformed JSON / legacy session no config |
| Null gate | 1 | scoringType null → empty score tab |
| Variant mount | 4 | Per-ScoreType: assert `data-slot="scoring-panel-{kind}"` mounted |

Mock pattern for variant tests:
```typescript
useLiveSessionStore.setState({
  scoringType: 'Points',
  scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
});
render(<SessionLiveView />);
expect(
  screen.getByTestId('scoring-panel-renderer').querySelector('[data-slot="scoring-panel-points"]')
).toBeInTheDocument();
```

### Regression

- All 67+ existing `SessionLiveView.test.tsx` cases must pass without modification beyond adding a `beforeEach` reset of `useLiveSessionStore` to default null state.
- `scores/page.tsx` test file (if exists) must pass after the `MVP_OBJECTIVES_CATALOGUE` import path change.

## Task breakdown

The implementation plan defines 8 TDD task commits (one task = one commit). See `docs/superpowers/plans/2026-06-19-issue-2389-block-b-renderer-wire-up.md` for the executable plan with per-task acceptance criteria.

High-level sequence:

| Task | Type | Description |
|------|------|-------------|
| T1 | PREP | Hoist `MVP_OBJECTIVES_CATALOGUE` to lib module |
| T2 | RED | Adapter test scaffold (14 RED tests) |
| T3 | GREEN | Adapter pure function implementation |
| T4 | RED | SessionLiveView wire test scaffold (8 RED) |
| T5 | GREEN | Wire REST hydration + adapter |
| T6 | QA | typecheck + lint sweep |
| T7 | DOC | CLAUDE.md + tracking issues filed |
| T8 | PR | Push + open PR to main-dev |

## Follow-up

Tracked as separate issues to be filed after Block B merge:

1. **Block B+ editor swap**: host-role `PolymorphicScoreEditor` mount + `useUpdateSessionScores` mutation wire + 403/429 toast feedback + inline debounce 500ms. Effort ~2-3 days.
2. **EndgameDialog polymorphic finalScores adapter**: replace hardcoded `{ score, isWinner: false }` at lines 1364-1368 with a winner-computation adapter (Points → sorted/leader, Ranking → position=1, BinaryWin → isWinner=true, Objectives → max completedObjectives.length). Effort ~0.5 day.
3. **Real Objectives catalogue wiring**: replace `MVP_OBJECTIVES_CATALOGUE` placeholder with game-level catalogue lookup (likely via game catalogue query or BE field on `SessionDto`). Effort ~1 day pending BE work.
4. **Legacy participant score endpoint deprecation**: remove `_handleScoreUpdate` callback and its dead-code legacy `PUT /api/v1/game-sessions/{id}/participants/{playerId}/score` reference. Depends on Block B+ editor swap shipping first. Effort ~0.25 day.

## Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| REST hydration race with SignalR `ScoringConfigured` | Low | SignalR overwrites unconditionally; semantically convergent. |
| `JSON.parse(scoreData)` malformed | Low | try/catch swallows; SignalR delivers canonical state. |
| ESLint rule `local/no-store-scores-direct` flags new direct selectors | None | Rule only targets `s.scores` reads, not `s.scoringType` / `s.scoreData`. |
| `activeSession.players` vs store `players` divergence affects adapter output | Low | Block A explicitly accepts this divergence during deprecation window. |
| Existing 67+ tests regress on `useLiveSessionStore` state pollution | Medium | Add `beforeEach` reset to default null state in `SessionLiveView.test.tsx`. |

## Acceptance criteria (Block B specific)

- [ ] `score-data-to-panel-data.ts` adapter shipped with 14 passing unit tests.
- [ ] `SessionLiveView.tsx` consumes adapter via `useMemo`; hardcoded `kind: 'Points'` removed.
- [ ] REST hydration `useEffect` pre-populates store from `sessionQuery.data` when `scoringType`+`scoreData` present.
- [ ] Both desktop right column and mobile drawer gate renderer on `scoringPanelData != null`.
- [ ] `MVP_OBJECTIVES_CATALOGUE` hoisted to `lib/session-live/`; editor (`scores/page.tsx`) import updated.
- [ ] All 67+ existing `SessionLiveView.test.tsx` cases pass.
- [ ] 8 new `SessionLiveView.test.tsx` cases pass (3 hydration + 1 null gate + 4 variant mount).
- [ ] `pnpm typecheck` and `pnpm lint` clean (no new errors or warnings).
- [ ] PR opened to `main-dev` with title `feat(session-live): #2389 Block B — scoringType selector wire-up`.
- [ ] Follow-up issues filed for Block B+ editor swap, EndgameDialog adapter, real Objectives catalogue.
