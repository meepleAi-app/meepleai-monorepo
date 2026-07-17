# Zombicide L2+L3 flavor — design

**Issue:** #2793 (G6g, epic #3025). **Date:** 2026-07-17. **Status:** approved (recon + owner batch mandate "falli tutti"). **The last of the 6 G6 games.**

## Goal

Add the Zombicide live flavor, reusing the generalized game-agnostic flavor pattern. Zombicide is **co-op tactical bookkeeping** (wounds / AP / equip / spawn), not a scoring or board-position game — most of the mockup's surface is high-friction manual re-entry with no leaderboard/summary payoff and is cut per the host-entry-friction rule. Keep only the two elements that read as "Zombicide" at a glance and are cheap to key in: a 6-type zombie horde counter grid and a 3-state (0/1/2 wounds) pip per survivor. This is a thin **counters-pools** flavor with a **mixed** PUT cadence — the same shape as Puerto Rico's counters + Power Grid's mixed cadence + Paleo's players-keyed status.

## Scope decisions (locked — recon + owner batch mandate)

1. **Model a 6-type zombie horde counter + a per-player wound level.** `zombies: Record<ZombieType, number>` (6 shared counts); `survivors: Record<playerId, WoundLevel>` where `WoundLevel = 0 | 1 | 2` (per player). Nothing else.
2. **Mission outcome / VP stays in the existing scoring system** (BinaryWin/Custom) — `gameState` never carries a score or a win/loss flag. Same invariant as every prior flavor.
3. **Round phase reuses the existing turn indicator** (`phaseName`) — NOT modelled in gameState.
4. **Mixed PUT cadence.** Zombie ± is continuous → **debounced-500 ms**. Wound-level tap is discrete, must-not-be-lost → **immediate** (`commit` + `flush`, exactly the Power Grid pattern). A single editor hook exposes both.
5. **Wounds are keyed by `session.players`** (one level per player, seeded 0), NOT a free roster — same players-keyed choice as Paleo. A survivor at `wounds === 2` is "down/dead" and is flagged visually in the row.
6. **Wound tap cycles** `0 → 1 → 2 → 0` (a single per-player button; 3 states, ≤2 taps to reach any) — the simplest host-entry for a 3-state value, matching the Paleo status-cycle pattern.

## Cut (high-friction / no payoff)

The skill tree (4 XP levels × per-skill flags — massive keystroke cost, not visible to non-host); the AP counter (base+bonus — resets every turn, zero session-summary value); equip slots per survivor (high-cardinality item picker, no leaderboard/summary payoff); the spawn-deck remaining-card countdown (derivable from Danger Level, another counter to drift); the Danger Level indicator (fully derived from top XP/wounds — compute client-side if ever kept, don't store); the map-tiles grid snapshot (static/cosmetic, near-zero host willingness to key in).

## Headline: zero backend, no plumbing refactor

L1 provides the opaque `gameState`; the plumbing is already game-agnostic. Zombicide adds one `FLAVOR_MAP` entry + a `flavors/zombicide/` folder + i18n. Nothing else changes.

## Architecture

All under `apps/web/src/components/features/session-live/flavors/zombicide/`.

### L2 — state contract (`zombicide-state.ts`)

```ts
export const ZOMBICIDE_STATE_VERSION = 1;
export const ZOMBIE_TYPES = ['walker', 'runner', 'fatty', 'berserker', 'abomination', 'necromancer'] as const; // z.enum
export type ZombieType = (typeof ZOMBIE_TYPES)[number];
export const ZOMBICIDE_WOUND_LEVELS = [0, 1, 2] as const;
export type WoundLevel = (typeof ZOMBICIDE_WOUND_LEVELS)[number]; // 0 | 1 | 2

export type ZombieCounts = Record<ZombieType, number>;
export interface ZombicideGameState {
  v: 1; game: 'zombicide';
  zombies: ZombieCounts;                 // 6 shared counts
  survivors: Record<string, WoundLevel>; // keyed by LiveSessionPlayer.id
}

export function parseZombicideGameState(raw: unknown): ZombicideGameState | null; // Zod safeParse → null
export function emptyZombieCounts(): ZombieCounts;                                // all 0
/** zombies all 0; every player wound level 0. */
export function initialZombicideState(playerIds: readonly string[]): ZombicideGameState;
/** 0 → 1 → 2 → 0. */
export function nextWoundLevel(level: WoundLevel): WoundLevel;
```

Zod: `zombies` is a **fixed** `z.object({walker,runner,fatty,berserker,abomination,necromancer})` (all 6 required, each `int≥0`) — NOT `z.record` (a record would allow a missing type → `undefined` at render). `survivors` is `z.record(z.string(), z.union([z.literal(0),z.literal(1),z.literal(2)]))` (or `z.number().int().min(0).max(2)` cast — use the literal union so a `3` rejects). Wrong game/version/shape → `null`.

### L2 — palette (`zombicide-palette.ts`)

The 3 wound levels have a semantic colour (0 = healthy green, 1 = wounded amber, 2 = down/dead red). Inline `hsl()` via a palette module (`zombicideWoundColor(level)`), token-lint safe like the sibling palettes. Add a line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- <reason>` on any hue that trips the rule (the controller verifies with `pnpm exec eslint` per task). The 6 zombie types have NO per-type colour (plain labelled counters with semantic tokens).

### L2 — host-edit (`use-zombicide-state-editor.ts`)

Mirrors the Power Grid mixed-cadence editor. `readState()` re-parses `useLiveSessionStore.getState().gameState` fresh; `commit(next)` = optimistic `setGameState(next)` + `debouncedMutate(next)`; `commitImmediate(next)` = `commit(next)` then `flush()` (trailing debounce holds only the latest arg → no stale-PUT race).

```ts
interface ZombicideStateEditor {
  state: ZombicideGameState | null;
  initializeState(): void;                          // IMMEDIATE; seed via initialZombicideState(playerIds)
  bumpZombie(type: ZombieType, delta: 1 | -1): void; // DEBOUNCED, clamp ≥0
  cycleWound(playerId: string): void;                // IMMEDIATE; nextWoundLevel, folds a missing player 0→1
}
```

Signature: `useZombicideStateEditor(sessionId: string, playerIds: readonly string[])`. No score mutators — scores flow through the existing score editor. Mutators no-op when parsed state is null (except `initializeState`).

### L3 — components (pure) + container

- **`ZombieHordePanel.tsx`** — the 6 shared zombie-type counters (walker/runner/fatty/berserker/abomination/necromancer), each a label + count; host (`editable`): `+/-` steppers. Props `{ zombies, editable, onBump?, labels }`.
- **`ZombicideSurvivorsPanel.tsx`** — per-player wound tracker: each row = player `displayName` + a wound badge (coloured via the palette; `wounds === 2` shows a "down" marker); host (`editable`): tap-to-cycle button (`0 → 1 → 2 → 0`). Read-only shows the badge without a button. Props `{ players, survivors, editable, onCycle?, labels }`.
- **`ZombicideLiveFlavor.tsx`** — container. Self-builds i18n labels (`useIntl` + `useTranslation`). Renders a themed **leaderboard** (from `livePoints`/`session.players`, ungated) + — when `parseZombicideGameState(store.gameState)` is non-null — the horde panel + the survivors panel; when null, a host `initBoardCta` ("Inizia partita") or a viewer-waiting note. Standard `FlavorProps` (`{ session, viewerRole, sessionId, className?, livePoints?, phaseName? }`).

### Wiring

- `FlavorRenderer.tsx` — add `ZombicideLiveFlavorLazy` (module-scope `dynamic`) + `'zombicide': { live: ZombicideLiveFlavorLazy }` to `FLAVOR_MAP`.
- i18n: add `pages.sessionLive.flavor.zombicide.*` to `it.json` + `en.json` (identical key set both locales).

## Data flow

- **Scoring** (leaderboard): from `livePoints`/`session.players`, ungated.
- **Zombies / wounds**: from `useLiveSessionStore(s => s.gameState)` via `parseZombicideGameState`.
- **Host writes**: zombie ± → optimistic + **debounced** PUT; wound tap → optimistic + **immediate** PUT; scores → existing score editor.

## Error handling & edge cases

- `gameState` null / wrong game / malformed → host `initBoardCta` (or viewer waiting); leaderboard still renders.
- PUT failure → `sonner` toast; optimistic state retained.
- Zombie bumps clamp ≥0; wound cycle wraps 0→1→2→0.
- A player in `session.players` but absent from `gameState.survivors` renders with a `0` (healthy) default badge (no crash); host cycle folds them in (0→1).
- `initialZombicideState` seeds every current player wound 0 + 6 zombie counts 0.

## Testing

- **Unit:** `parseZombicideGameState` (valid / wrong game / version / missing zombie type / wound level 3 rejected / malformed); `emptyZombieCounts`; `initialZombicideState` (zombies 0, every player 0 wounds); `nextWoundLevel` (0→1→2→0); editor mutators (bumpZombie clamp + debounced; cycleWound advances + immediate PUT + folds missing player; no-op on null except init).
- **Component (RTL + jsdom):** `ZombieHordePanel` (host steppers fire; read-only no buttons; 6 types render); `ZombicideSurvivorsPanel` (host tap-to-cycle fires `onCycle`; read-only no buttons; badge per player; wounds=2 down marker); `ZombicideLiveFlavor` (leaderboard ungated; host CTA when null; populated compose). `jest-axe` AA on the flavor.
- **E2E:** the existing `session-live-catan-flavor.smoke.spec.ts` regression guard covers the gating; no new E2E file.
- **Lint gate:** the controller runs `pnpm exec eslint --max-warnings=0` on the flavor dir per task (the pre-commit hook does NOT run `meepleai/no-inline-hsl-v2` or the `style`-prop case of `local/no-hardcoded-color-utility`).

## File map

Create: `flavors/zombicide/zombicide-state.ts`, `zombicide-palette.ts`, `use-zombicide-state-editor.ts`, `ZombieHordePanel.tsx`, `ZombicideSurvivorsPanel.tsx`, `ZombicideLiveFlavor.tsx`, `__tests__/*`.
Modify: `session-live/FlavorRenderer.tsx` (`zombicide` in `FLAVOR_MAP`), `src/locales/it.json` + `en.json` (`flavor.zombicide.*`).

> **Process note (from the Power Grid race):** the container task and the wiring task MUST run sequentially, not in parallel — a shared worktree produces twin commits when both touch files the other type-checks against.

## Out of scope (YAGNI)

The skill tree; the AP counter; equip slots; the spawn-deck countdown; the Danger Level indicator (derived); the map-tiles snapshot; a free-text survivor roster (players-keyed instead); the Zombicide summary flavor; any backend change.
