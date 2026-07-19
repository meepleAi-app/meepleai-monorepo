# Power Grid L2+L3 flavor — design

**Issue:** #2791 (G6e, epic #3025). **Date:** 2026-07-17. **Status:** approved (recon + owner batch mandate "falli tutti").

## Goal

Add the Power Grid live flavor, reusing the generalized game-agnostic flavor pattern. Power Grid is a **board (market) game**: the 8-slot power-plant market gives it real board identity cheaply, but the mockup's auction overlay and network graph are engine-shaped (not host-entry-shaped) and are cut. Ship a plants + resource-market flavor. It is the first flavor with a **mixed** PUT cadence: discrete plant-slot edits PUT immediately (like Codenames), continuous resource ± PUT debounced (like Puerto Rico).

## Scope decisions (locked — recon + owner batch mandate)

1. **Model the 8-slot plant market + 4 resource-market counters.** `plants: { current: (number|null)[4], future: (number|null)[4] }` (the plant *number* only — the value printed on the card); `resources: { coal, oil, garbage, uranium }` (shared counts). Nothing per-player in gameState.
2. **VP / cities-powered stays in the existing scoring system** (a Count scoring dimension) — `gameState` never carries a score. Same invariant as every prior flavor.
3. **Turn order / phase reuses the existing turn indicator** (`phaseName`, `currentTurnPlayerId`) — NOT modelled in gameState.
4. **Mixed PUT cadence.** Plant-slot set = discrete, must-not-be-lost → **immediate PUT** (optimistic `setGameState` + flush the debounce). Resource ± = continuous → **debounced-500 ms PUT**. A single editor hook exposes both; the immediate path reuses the same debounced primitive and flushes it (so a plant edit never races a pending resource PUT — every PUT sends the full fresh state, last-write-wins converges).
5. **Plant number is a free integer input** (host types the card number, 3..50 in the real game) — a numeric `<input>` per slot, empty = null (slot cleared). Resource counters are `+/-` steppers.

## Cut (engine-shaped or redundant)

The auction overlay (current bidder/bid/pass — transient per-turn, high friction); the network map / city-ownership graph (graph editor too expensive to hand-key, visible on the physical board); the 5-phase / Step 1-2-3 indicator (redundant with the turn indicator); per-city connection costs; per-plant metadata (resource-type/capacity/cities-powered — static rules data); the **resource price bracket** (derived-from-remaining display — needs the game's static price-ladder table; deferred as a follow-up, not MVP).

## Headline: zero backend, no plumbing refactor

L1 provides the opaque `gameState`; the plumbing is already game-agnostic. Power Grid adds one `FLAVOR_MAP` entry + a `flavors/power-grid/` folder + i18n. Nothing else changes.

## Architecture

All under `apps/web/src/components/features/session-live/flavors/power-grid/`.

### L2 — state contract (`power-grid-state.ts`)

```ts
export const POWER_GRID_STATE_VERSION = 1;
export const POWER_GRID_RESOURCES = ['coal', 'oil', 'garbage', 'uranium'] as const; // z.enum
export type PowerGridResource = (typeof POWER_GRID_RESOURCES)[number];
export const POWER_GRID_PLANT_BANKS = ['current', 'future'] as const;
export type PowerGridPlantBank = (typeof POWER_GRID_PLANT_BANKS)[number];

export interface PowerGridResources { coal: number; oil: number; garbage: number; uranium: number }
export interface PowerGridGameState {
  v: 1; game: 'power-grid';
  plants: { current: (number | null)[]; future: (number | null)[] }; // each exactly length 4
  resources: PowerGridResources;                                      // shared market counts
}

export function parsePowerGridGameState(raw: unknown): PowerGridGameState | null; // Zod safeParse → null
export function emptyPowerGridResources(): PowerGridResources;                    // all 0
/** plants: 4 nulls each bank; resources all 0. */
export function initialPowerGridState(): PowerGridGameState;
```

Zod: `resources` is a **fixed** `z.object({coal,oil,garbage,uranium})` (all 4 required, each `int≥0`) — NOT `z.record`. `plants.current` / `plants.future` are each `z.array(z.number().int().min(0).nullable()).length(4)`. Wrong game/version/shape → `null`.

### L2 — palette (`power-grid-palette.ts`)

The 4 resources have canonical-ish colours (coal = dark brown, oil = near-black, garbage = yellow-green, uranium = red). Inline `hsl()` via a palette module (`powerGridResourceColor(resource)`), token-lint safe like the sibling palettes. Add a line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- <reason>` on any hue that trips the rule (the controller verifies with `pnpm exec eslint` per task).

### L2 — host-edit (`use-power-grid-state-editor.ts`)

Mirrors the sibling editors, with the **mixed cadence** twist. `readState()` re-parses `useLiveSessionStore.getState().gameState` fresh; `commit(next)` = optimistic `setGameState(next)` + `debouncedMutate(next)`; the immediate path calls `commit(next)` then `flush()` (the trailing debounce holds only the latest arg, so flush sends the full fresh state — no stale-PUT race).

```ts
interface PowerGridStateEditor {
  state: PowerGridGameState | null;
  initializeState(): void;                                              // seed via initialPowerGridState()
  bumpResource(field: PowerGridResource, delta: 1 | -1): void;          // DEBOUNCED, clamp ≥0
  setPlant(bank: PowerGridPlantBank, index: number, plant: number | null): void; // IMMEDIATE (commit + flush); index 0..3; clamp plant ≥0 when non-null
}
```

Signature: `usePowerGridStateEditor(sessionId: string)` — no `playerIds` (gameState is not per-player). No score mutators — scores flow through the existing score editor. Mutators no-op when parsed state is null (except `initializeState`).

### L3 — components (pure) + container

- **`PowerGridPlantMarketPanel.tsx`** — the 8 plant slots in two labelled banks (Attuali / Future), each slot a plant-number (host: a numeric `<input>` with `aria-label`, empty ⇒ null; read-only: the number or an em-dash). Props `{ plants, editable, onSetPlant?, labels }`.
- **`PowerGridResourceMarketPanel.tsx`** — the 4 resource counters (coal/oil/garbage/uranium), each a coloured chip (via the palette) + count; host: `+/-` steppers. Props `{ resources, editable, onBump?, labels }`.
- **`PowerGridLiveFlavor.tsx`** — container. Self-builds i18n labels (`useIntl` + `useTranslation`). Renders a themed **leaderboard** (from `livePoints`/`session.players`, ungated) + — when `parsePowerGridGameState(store.gameState)` is non-null — the plant-market panel + the resource-market panel; when null, a host `initBoardCta` ("Inizia partita") or a viewer-waiting note. Standard `FlavorProps` (`{ session, viewerRole, sessionId, className?, livePoints?, phaseName? }`).

### Wiring

- `FlavorRenderer.tsx` — add `PowerGridLiveFlavorLazy` (module-scope `dynamic`) + `'power-grid': { live: PowerGridLiveFlavorLazy }` to `FLAVOR_MAP` (note the hyphenated slug — `Slugify("Power Grid") = "power-grid"`).
- i18n: add `pages.sessionLive.flavor.power-grid.*` to `it.json` + `en.json` (identical key set both locales).

## Data flow

- **Scoring** (leaderboard): from `livePoints`/`session.players`, ungated.
- **Plants / resources**: from `useLiveSessionStore(s => s.gameState)` via `parsePowerGridGameState`.
- **Host writes**: plant set → optimistic `setGameState` + **immediate** PUT; resource ± → optimistic + **debounced** PUT; scores → existing score editor.

## Error handling & edge cases

- `gameState` null / wrong game / malformed → host `initBoardCta` (or viewer waiting); leaderboard still renders.
- PUT failure → `sonner` toast; optimistic state retained.
- Resource bumps clamp ≥0; a plant `<input>` that parses to NaN or a negative → treated as `null` (cleared) / clamped ≥0.
- `setPlant` out-of-range index → no-op.
- `initialPowerGridState` seeds 4 nulls per bank + 0 resources (no player dependency).

## Testing

- **Unit:** `parsePowerGridGameState` (valid / wrong game / version / bank length ≠ 4 / malformed); `emptyPowerGridResources`; `initialPowerGridState` (8 null slots, 0 resources); editor mutators (bumpResource clamp + debounced; setPlant sets + clears (null) + clamps + **immediate** PUT via flush; out-of-range no-op; no-op on null except init).
- **Component (RTL + jsdom):** `PowerGridPlantMarketPanel` (8 slots in 2 banks; host input fires `onSetPlant(bank,index,value)`; empty input ⇒ null; read-only no inputs); `PowerGridResourceMarketPanel` (host steppers fire; read-only no buttons; 4 resources render); `PowerGridLiveFlavor` (leaderboard ungated; host CTA when null; populated compose). `jest-axe` AA on the flavor.
- **E2E:** the existing `session-live-catan-flavor.smoke.spec.ts` regression guard covers the gating; no new E2E file.
- **Lint gate:** the controller runs `pnpm exec eslint --max-warnings=0` on the flavor dir per task (the pre-commit hook does NOT run `meepleai/no-inline-hsl-v2` or the `style`-prop case of `local/no-hardcoded-color-utility`).

## File map

Create: `flavors/power-grid/power-grid-state.ts`, `power-grid-palette.ts`, `use-power-grid-state-editor.ts`, `PowerGridPlantMarketPanel.tsx`, `PowerGridResourceMarketPanel.tsx`, `PowerGridLiveFlavor.tsx`, `__tests__/*`.
Modify: `session-live/FlavorRenderer.tsx` (`power-grid` in `FLAVOR_MAP`), `src/locales/it.json` + `en.json` (`flavor.power-grid.*`).

## Out of scope (YAGNI)

The auction overlay; the network map / city-ownership graph; the 5-phase indicator (turn indicator instead); per-city connection costs; per-plant metadata; the resource price-ladder bracket (deferred follow-up); the Power Grid summary flavor; any backend change.
