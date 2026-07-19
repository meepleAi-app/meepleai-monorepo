# Wingspan L2+L3 flavor — design

**Issue:** #2788 (G6b, part of epic #3025). **Date:** 2026-07-16. **Status:** approved (brainstorm with owner).

## Goal

Add a rich, mockup-faithful Wingspan live flavor on the L1 game-state layer, reusing the proven Catan L2/L3 pattern (`flavors/<game>/`: state + editor + pure components + container). Wingspan is the 2nd game and the first to be primarily **scoring-centric** rather than board-centric — its richness lives in the 6 Victory-Point categories + the 4-round structure, not a visual board.

## Key finding that shaped the scope

The `sp4-session-wingspan-live` mockup is the **universal session skeleton** (7 right-column tabs, roster, `LiveScoringPanel`, `ActionLog`) with Wingspan theming. The Wingspan-specific live content is **category scoring**: the 6 canonical VP categories (`M.CATS`) with a per-player breakdown (`M.BREAKDOWN[player][cat]`) and a category-tab + `+/-` stepper score-entry panel. There is **no visual board, no bird placement, no habitat tableau** in the live view (that richness is in the post-game summary). So most of Wingspan's live "richness" maps onto the app's **existing scoring system** (`scoringConfig.enabledDimensions` + `roundScores` + the polymorphic score editor), which the Catan MVP flavor already rendered as a per-dimension breakdown.

## Scope decisions (locked)

1. **Themed scoring + round context** (chosen). The 6 Wingspan VP categories are **scoring dimensions** (entered via the existing score editor, read by the flavor from `roundScores`/`livePoints`). The Wingspan-specific `gameState` is small: **round context only** (`round` 1–4 + 4 round-goal tiles).
2. **VP stays in the existing scoring system** — the same cross-cutting invariant as Catan. `gameState` never carries scores.
3. **Round-goal tiles = free-text labels** (the canonical goal-tile set varies by edition; free text = zero host-entry friction). The per-round turn budget (8/7/6/5) is a derived constant, not stored.
4. **Scoring is NOT gated on `gameState`** (divergence from Catan): the leaderboard + category breakdown always render (they come from `roundScores`), independent of whether the round-context `gameState` exists. Only the round tracker shows a host CTA when `gameState` is null — no "generate board" wall.
5. **Label-provisioning generalization** (motivated by this 2nd game): `FlavorRenderer` currently passes a Catan-specific `labels` prop built in `SessionLiveView`. That does not scale to N games. Each flavor will **self-build its i18n labels via `useIntl`**; `FlavorRenderer` becomes game-agnostic (`session` + `viewerRole` + `sessionId` + `livePoints` + `phaseName`). `CatanLiveFlavor` is refactored the same way for consistency (the `catanFlavorLabels` memo moves into the component).

## Headline: zero backend

L1 (PR #3031) provides the opaque write→persist→stream→expose path. L2 is a FE JSON convention + parser; L3 is the renderer. No backend changes. Scores use the existing scoring endpoints/editor.

## Architecture

All under `apps/web/src/components/features/session-live/flavors/wingspan/`.

### L2 — state contract (`wingspan-state.ts`)

```ts
export const WINGSPAN_STATE_VERSION = 1;
export const WINGSPAN_ROUND_TURN_BUDGET = [8, 7, 6, 5] as const; // turns per round 1..4

export interface WingspanRoundGoal { label: string } // free-text; scoredBy resolved via scoring, not here

export interface WingspanGameState {
  v: 1;
  game: 'wingspan';
  round: number;              // 1..4
  roundGoals: WingspanRoundGoal[]; // up to 4 entries (one per round)
}

export function parseWingspanGameState(raw: unknown): WingspanGameState | null; // Zod safeParse; null on wrong game/version/shape

export function initialWingspanState(): WingspanGameState; // { v:1, game:'wingspan', round:1, roundGoals:[] }

// The 6 canonical Wingspan VP categories. `id` is the scoring dimension name the flavor
// sums over `roundScores`; label/emoji are for display. The six ids (fixed):
//   birds · bonusCards · endOfRoundGoals · eggs · cachedFood · tuckedCards
export const WINGSPAN_CATEGORIES: ReadonlyArray<{ id: string; label: string; emoji: string }>;
// KNOWN SEAM: the breakdown sums `roundScores` by these dimension ids. If a Wingspan session
// was created with differently-named dimensions, the breakdown shows 0 per category (graceful,
// no crash) while the leaderboard (livePoints/totalScore) is unaffected. Aligning session
// creation to these dimension names is a follow-up, not part of this FE flavor.
```

Zod schema: `round` is `z.number().int().min(1).max(4)`; `roundGoals` is `z.array(z.object({ label: z.string() })).max(4)`. `parseWingspanGameState` returns `null` on `game !== 'wingspan'` / `v !== 1` / malformed (degrades to the round-tracker empty state without crashing).

### L2 — host-edit (`use-wingspan-state-editor.ts`)

Mirrors `use-catan-state-editor`: reads `useLiveSessionStore(s => s.gameState)` → `parseWingspanGameState`; wraps `useUpdateLiveGameState(sessionId)` + `useDebouncedCallback(fn, 500)`; each mutator = optimistic `setGameState(next)` + debounced PUT; flush on unmount; `readState()` re-parses fresh at call time.

```ts
interface WingspanStateEditor {
  state: WingspanGameState | null;
  initializeState(): void;                 // host CTA when null → round 1, empty goals
  setRound(round: number): void;           // clamp 1..4
  advanceRound(): void;                    // round = min(round+1, 4)
  setRoundGoal(index: number, label: string): void; // index 0..3
}
```

No score mutators — scores flow through the existing score editor.

### L3 — components (pure) + container

- **`WingspanRoundTracker.tsx`** — displays round `n/4` + the per-round turn budget (`WINGSPAN_ROUND_TURN_BUDGET[round-1]`) + the 4 round-goal tiles. Props `{ state, editable, onSetRound?, onAdvanceRound?, onSetRoundGoal?, labels }`. Host (`editable`): advance-round button + inline-editable goal labels (text inputs). Read-only: static.
- **`WingspanCategoryBreakdown.tsx`** — per-player themed breakdown across `WINGSPAN_CATEGORIES`, summing `roundScores` per category (reuse a `sumDimension(roundScores, playerId, categoryId)` helper mirroring the Catan MVP). Read-only display (scores edited via the score editor). Props `{ players, roundScores, labels }`.
- **`WingspanLiveFlavor.tsx`** — container. Renders: a themed **leaderboard** (players sorted by `livePoints ?? totalScore`), the **category breakdown**, and the **round tracker**. Scoring sections render unconditionally; the round tracker renders its host CTA (`initializeState`) when `parseWingspanGameState(store.gameState)` is null and the viewer is Host, else a "waiting" note. Self-builds labels via `useIntl`. Props `{ session, viewerRole, sessionId, className?, livePoints?, phaseName? }`.

### Generalization (FlavorRenderer + Catan refactor)

- `FlavorRenderer` drops the `labels` prop; forwards only game-agnostic props (`session`, `viewerRole`, `sessionId`, `className`, `livePoints`, `phaseName`). Its `FLAVOR_MAP` gains `wingspan: { live: WingspanLiveFlavorLazy }`.
- `CatanLiveFlavor` is refactored to self-build its labels internally via `useIntl` (the `catanFlavorLabels` `useMemo` is removed from `SessionLiveView`, and `CatanLiveFlavorLabels` becomes an internal concern). `SessionLiveView`'s two `FlavorRenderer` sites drop the `labels={catanFlavorLabels}` prop.
- i18n: add `flavor.wingspan.*`; `flavor.catan.*` values are unchanged (only the build site moves).

## Data flow

- **Scoring** (leaderboard + breakdown): from `roundScores` / `livePoints` (existing), independent of `gameState`.
- **Round context**: from `useLiveSessionStore(s => s.gameState)` via `parseWingspanGameState`.
- **Host writes**: round/goal edits → optimistic `setGameState` + debounced `PUT /game-state`; scores → existing score editor.

## Error handling & edge cases

- `gameState` null / wrong game / malformed → round tracker shows host CTA (or viewer waiting); the scoring sections still render. `console.warn` once on malformed.
- PUT failure → `sonner` toast; optimistic state retained.
- `setRound` clamps 1..4; `advanceRound` caps at 4; `setRoundGoal` ignores out-of-range indices.
- A session with no Wingspan scoring dimensions → the breakdown shows 0 per category (no crash).

## Testing

- **Unit:** `parseWingspanGameState` (valid / wrong game / wrong version / malformed / round out-of-range rejected); `initialWingspanState`; editor mutators (setRound clamp, advanceRound cap at 4, setRoundGoal index guard, optimistic `setGameState`, debounce).
- **Component (RTL + jsdom):** `WingspanRoundTracker` (host advance/goal-edit callbacks vs read-only static); `WingspanCategoryBreakdown` (per-category sums from `roundScores`); `WingspanLiveFlavor` (scoring renders with null gameState; round-tracker host CTA vs viewer waiting; populated compose). `jest-axe` AA on the flavor.
- **E2E:** the existing `session-live-catan-flavor.smoke.spec.ts` regression guard (a non-Wingspan/Catan fixture shows no game flavor tab) already covers the gating; the fixture Wingspan session already exercises "no Catan tab". A Wingspan positive path is `test.fixme()` (same `?fixture=host` limitation). No new E2E file.

## File map

Create:
- `flavors/wingspan/wingspan-state.ts`
- `flavors/wingspan/use-wingspan-state-editor.ts`
- `flavors/wingspan/WingspanRoundTracker.tsx`
- `flavors/wingspan/WingspanCategoryBreakdown.tsx`
- `flavors/wingspan/WingspanLiveFlavor.tsx`
- `flavors/wingspan/__tests__/*`

Modify:
- `session-live/FlavorRenderer.tsx` — game-agnostic props + `wingspan` in `FLAVOR_MAP`.
- `flavors/catan/CatanLiveFlavor.tsx` — self-build labels via `useIntl` (remove the labels prop dependency).
- `sessions/[id]/live/_components/SessionLiveView.tsx` — remove `catanFlavorLabels` memo + `labels=` prop at both `FlavorRenderer` sites.
- `src/locales/it.json` + `en.json` — add `flavor.wingspan.*`.

## Out of scope (YAGNI)

Per-player habitat/bird tableau (a summary-view concern, not in the live mockup); individual bird-card placement; food/egg supply tracking; the canonical round-goal-tile picker (free text instead); Wingspan summary flavor (separate follow-up like Catan's #3022); any backend change.
