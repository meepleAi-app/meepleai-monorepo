# Puerto Rico L2+L3 flavor — design

**Issue:** #2792 (G6f, epic #3025). **Date:** 2026-07-17. **Status:** approved (recon + owner scope pick).

## Goal

Add the Puerto Rico live flavor, reusing the generalized game-agnostic flavor pattern. Puerto Rico's live mockup is the richest of the G6 games (5 tabs, a per-player mat, an 8-role board), so the crux is an aggressive host-entry-friction cut: model coarse per-player counters + the 3 shared board pools, cut the role board and the tile/building tableau layout.

## Scope decisions (locked — recon + owner)

1. **Model coarse per-player counters + 3 shared pools.** Per player: `doubloons`, `colonists`, a 5-good `storehouse` (corn/indigo/sugar/tobacco/coffee), and `plantations`/`quarries`/`buildings` **counts** (not tile layout). Shared: `galleons` (3 cargo ships), `tradingHouse` (4 slots), `colonistShip`.
2. **CUT (high-friction / redundant):** the plantation/building **tableau layout** (positions/adjacency — model counts only); the **8-role board as live state** (chosen-by, doubloons-per-role) — redundant with the existing `TurnIndicatorRenderer` phase stepper (`phaseName` already tells "whose turn / what role"). We do NOT model `currentRole` (derivable from turn state; YAGNI).
3. **VP stays in the existing scoring system** (Points; buildings-vp / shipped-goods-vp / large-building-bonus map onto scoring dimensions) — `gameState` never carries scores. Same invariant as Catan/Wingspan/Codenames.
4. **Continuous ± adjustments → DEBOUNCED PUT (500 ms)** (like Catan/Wingspan, unlike Codenames' discrete immediate-PUT). Every Puerto Rico edit is a counter tweak (bump doubloons, load a galleon), not a discrete must-not-be-lost event.
5. **Goods keep the game's identity** — the 5-good storehouse is what makes it feel like Puerto Rico (a goods-economy game); worth the medium re-entry friction (accepted, like Catan's).

## Headline: zero backend, no plumbing refactor

L1 provides the opaque `gameState`; the plumbing is already game-agnostic (`FlavorRenderer` dispatches on `FLAVOR_MAP` with `FlavorProps`; each flavor self-builds i18n labels). Puerto Rico adds one `FLAVOR_MAP` entry + a `flavors/puerto-rico/` folder + i18n. Nothing else changes.

## Architecture

All under `apps/web/src/components/features/session-live/flavors/puerto-rico/`.

### L2 — state contract (`puerto-rico-state.ts`)

```ts
export const PUERTO_RICO_STATE_VERSION = 1;
export const PUERTO_RICO_GOODS = ['corn', 'indigo', 'sugar', 'tobacco', 'coffee'] as const; // z.enum
export type PuertoRicoGood = (typeof PUERTO_RICO_GOODS)[number];

export interface PuertoRicoPlayerState {
  doubloons: number; colonists: number;
  storehouse: Record<PuertoRicoGood, number>;
  plantations: number; quarries: number; buildings: number;
}
export interface PuertoRicoGalleon { good: PuertoRicoGood | null; loaded: number; cap: number }
export interface PuertoRicoGameState {
  v: 1; game: 'puerto-rico';
  players: Record<string, PuertoRicoPlayerState>;   // keyed by LiveSessionPlayer.id
  galleons: PuertoRicoGalleon[];                    // 3 cargo ships
  tradingHouse: { slots: (PuertoRicoGood | null)[] }; // exactly 4
  colonistShip: { onShip: number; supply: number };
}

export function parsePuertoRicoGameState(raw: unknown): PuertoRicoGameState | null; // Zod safeParse → null
export function emptyPuertoRicoPlayerState(): PuertoRicoPlayerState;                 // all 0
/** Cargo-ship caps by player count: [n+1, n+2, n+3] (standard PR). */
export function initialPuertoRicoState(playerIds: readonly string[]): PuertoRicoGameState;
```

Zod: numeric fields `z.number().int().min(0)`; `storehouse` is a **fixed** `z.object({ corn, indigo, sugar, tobacco, coffee })` (all 5 required, each `int≥0`) — NOT a `z.record` (which would allow missing keys → `undefined` when a component reads `storehouse[good]`); `galleons` `z.array(...)` (3 in a standard game, but the schema tolerates any length ≥0 for resilience); `tradingHouse.slots` `z.array((GoodSchema|null)).length(4)`. Wrong game/version/shape → `null`.

`initialPuertoRicoState`: seeds a zeroed player state per id, 3 galleons `{good:null, loaded:0, cap: n+1|n+2|n+3}` (n = playerIds.length), 4 null trading slots, `colonistShip {onShip:0, supply:0}`.

### L2 — palette (`puerto-rico-palette.ts`)

The 5 goods have canonical colours (corn=yellow, indigo=blue, sugar=white/pale, tobacco=brown, coffee=dark). Inline `hsl()` via a palette module (token-lint safe, like `catan-palette`/`codenames-palette`). Add line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- <reason>` on any hue that trips the rule (mirroring the sibling palettes — the controller verifies with `pnpm exec eslint` per task).

### L2 — host-edit (`use-puerto-rico-state-editor.ts`)

Mirrors the Catan/Wingspan editor (optimistic `setGameState` + **debounced-500 ms** PUT; flush on unmount; `readState()` fresh reads). Parameterized mutators keep it compact:

```ts
type PlayerCounter = 'doubloons' | 'colonists' | 'plantations' | 'quarries' | 'buildings';
interface PuertoRicoStateEditor {
  state: PuertoRicoGameState | null;
  initializeState(): void;
  bumpPlayerCounter(playerId: string, field: PlayerCounter, delta: 1 | -1): void; // clamp ≥0
  bumpPlayerGood(playerId: string, good: PuertoRicoGood, delta: 1 | -1): void;     // clamp ≥0
  setGalleonGood(index: number, good: PuertoRicoGood | null): void; // also resets loaded to 0 on good change
  bumpGalleonLoaded(index: number, delta: 1 | -1): void;            // clamp [0, cap]
  setTradingSlot(index: number, good: PuertoRicoGood | null): void; // index 0..3
  bumpColonistShip(field: 'onShip' | 'supply', delta: 1 | -1): void; // clamp ≥0
}
```

No score mutators — scores flow through the existing score editor. Mutators no-op when parsed state is null (except `initializeState`).

### L3 — components (pure) + container

- **`PuertoRicoPlayerMatSummary.tsx`** — per-player compact card: doubloons, colonists, the 5-good storehouse (coloured chips + counts), plantation/quarry/building counts. Host (`editable`): `+/-` steppers on each. Props `{ player, state, editable, on* , labels }`.
- **`PuertoRicoGalleonsPanel.tsx`** — the 3 cargo ships: each shows `good` (coloured) + `loaded/cap`. Host: pick a good per ship + bump loaded. Props `{ galleons, editable, onSetGood?, onBumpLoaded?, labels }`.
- **`PuertoRicoTradingHousePanel.tsx`** — 4 slots, each a good or empty. Host: cycle/set the good per slot. Props `{ slots, editable, onSetSlot?, labels }`.
- **`PuertoRicoColonistShipPanel.tsx`** — `onShip` + `supply` counters. Host: steppers. Props `{ colonistShip, editable, onBump?, labels }`.
- **`PuertoRicoLiveFlavor.tsx`** — container. Self-builds i18n labels. Renders a themed **leaderboard** (from `livePoints`/`session.players`, ungated) + — when `parsePuertoRicoGameState(store.gameState)` is non-null — the galleons/trading/colonist-ship shared panels + a per-player `PuertoRicoPlayerMatSummary` for each `session.players`; when null, a host `initBoardCta` ("Inizia partita") or a viewer-waiting note. Standard `FlavorProps`.

### Wiring

- `FlavorRenderer.tsx` — add `PuertoRicoLiveFlavorLazy` (module-scope `dynamic`) + `'puerto-rico': { live: PuertoRicoLiveFlavorLazy }` to `FLAVOR_MAP`. (Plumbing already game-agnostic — no other change.)
- i18n: add `pages.sessionLive.flavor.puerto-rico.*` (note the hyphen — use the exact `gameSlug`) to `it.json` + `en.json`.

## Data flow

- **Scoring** (leaderboard): from `livePoints`/`session.players`, ungated.
- **Counters/pools**: from `useLiveSessionStore(s => s.gameState)` via `parsePuertoRicoGameState`.
- **Host writes**: counter/pool edits → optimistic `setGameState` + **debounced** `PUT /game-state`; scores → existing score editor.

## Error handling & edge cases

- `gameState` null / wrong game / malformed → host `initBoardCta` (or viewer waiting); leaderboard still renders. `console.warn` once on malformed (family-consistent).
- PUT failure → `sonner` toast; optimistic state retained.
- All counter bumps clamp ≥0; galleon loaded clamps `[0, cap]`; `setGalleonGood` resets that ship's `loaded` to 0 (a new good empties the hold).
- A player present in `session.players` but absent from `gameState.players` renders with a zeroed default (no crash); host edits fold them in (fold via `emptyPuertoRicoPlayerState()`).
- `initialPuertoRicoState` handles 3/4/5 players via the `[n+1, n+2, n+3]` cap formula.

## Testing

- **Unit:** `parsePuertoRicoGameState` (valid / wrong game / version / bad trading-slot length / malformed); `emptyPuertoRicoPlayerState`; `initialPuertoRicoState` (per-player zeroed states, galleon caps for 3/4/5 players, 4 null trading slots); editor mutators (each clamps; `setGalleonGood` resets loaded; `bumpGalleonLoaded` caps at `cap`; no-op on null except init; debounce + optimistic + flush-on-unmount).
- **Component (RTL + jsdom):** `PuertoRicoPlayerMatSummary` (host steppers fire callbacks; read-only no buttons; storehouse renders 5 goods); `PuertoRicoGalleonsPanel` / `PuertoRicoTradingHousePanel` / `PuertoRicoColonistShipPanel` (host edit vs read-only); `PuertoRicoLiveFlavor` (leaderboard ungated; host CTA when null; populated compose). `jest-axe` AA on the flavor.
- **E2E:** the existing `session-live-catan-flavor.smoke.spec.ts` regression guard covers the gating; no new E2E file.
- **Lint gate:** the controller runs `pnpm exec eslint --max-warnings=0` on the flavor dir per task (the pre-commit hook does NOT run `meepleai/no-inline-hsl-v2` or the `style`-prop case of `local/no-hardcoded-color-utility`).

## File map

Create: `flavors/puerto-rico/puerto-rico-state.ts`, `puerto-rico-palette.ts`, `use-puerto-rico-state-editor.ts`, `PuertoRicoPlayerMatSummary.tsx`, `PuertoRicoGalleonsPanel.tsx`, `PuertoRicoTradingHousePanel.tsx`, `PuertoRicoColonistShipPanel.tsx`, `PuertoRicoLiveFlavor.tsx`, `__tests__/*`.
Modify: `session-live/FlavorRenderer.tsx` (`puerto-rico` in `FLAVOR_MAP`), `src/locales/it.json` + `en.json` (`flavor.puerto-rico.*`).

## Out of scope (YAGNI)

The 8-role selection board as live state (redundant with the turn indicator); the plantation/building tile tableau layout (counts only); `currentRole` (derivable from turn state); per-good galleon capacity rules beyond the `[n+1,n+2,n+3]` init; the mockup's 5-tab right column (the app shell owns the right column; the flavor lives inside the single Flavor tab); Puerto Rico summary flavor; any backend change.
