# Catan L2+L3 rich flavor — design

**Issue:** #3033 (pilot of epic #3025). **Date:** 2026-07-16. **Status:** approved (brainstorm with owner).

## Goal

Replace the Catan MVP flavor (#2787, a themed leaderboard) with a **rich, mockup-faithful live flavor** driven by a **per-game state schema** (L2) that the host edits inline, streamed to all viewers via the generic L1 game-state layer (PR #3031). Catan is the **pilot** that validates the full L2+L3 stack for the remaining G6 games.

## Scope decisions (locked)

Three product decisions taken with the owner during brainstorming:

1. **State fidelity — "board preset + live-state" (Tier A).** The 19-hex board comes from a one-click **preset generator** (no manual hex data-entry). Live dynamic state the host updates per turn: dice roll, robber position, and per-player pieces / dev-count / badges / hand-size. **No piece-coordinate tracking** (settlement/city/road placement on the board) — impractical to maintain by hand without a game engine.
2. **Host-edit — inline on the flavor.** When `viewerRole === 'Host'`, flavor elements become editable inline (quick-tap dice 2-12, tap hex → robber, steppers, badge toggles). Autosave **debounced 500 ms** → `PUT /game-state` (L1). Non-host viewers see read-only.
3. **Resources — hand size only.** Per player we track a single public number (total cards in hand). **No per-resource hand composition** (secret info + high friction). Shared resource bank **omitted** in MVP.

**Cross-cutting invariant:** Victory Points stay in the **existing scoring system** (`scoreData` / the polymorphic score editor). The Catan `gameState` carries **only** Catan-specific extras not already modeled. This avoids two sources of truth for points. The flavor reads live VP from the store/scoring exactly as the MVP already does (`livePoints` overlay).

## Headline: zero backend

L1 already ships the full opaque **write → persist → stream → expose** path for `LiveGameSession.GameState`:
- `PUT /api/v1/live-sessions/{id}/game-state` (participant-guarded, 256 KiB cap) → persists.
- `LiveSessionDto.gameState` (`JsonElement?`) on `GET /{id}`.
- SSE `session:game-state` broadcast → `useLiveSessionStore.gameState` mirror (hydrated by `SessionLiveView`).
- FE mutation `useUpdateLiveGameState(sessionId)`.

L2 is therefore a **FE JSON convention + parser**; L3 is the **rich renderer**. **No backend changes.** The BE treats the state as opaque (no per-game shape validation) — the FE owns typing.

## Architecture

All work under `apps/web/src/components/features/session-live/flavors/catan/`.

### L2 — data contract (`catan-state.ts`)

Versioned TypeScript type + Zod schema, with a defensive parser over the opaque `gameState`:

```ts
export const CATAN_STATE_VERSION = 1;

export type CatanTerrain = 'wood' | 'brick' | 'sheep' | 'wheat' | 'ore' | 'desert';

export interface CatanHex {
  id: string;          // 'h0'..'h18'
  col: number;         // 0..4
  row: number;         // 0..(colHeight-1)
  terrain: CatanTerrain;
  number: number | null; // 2..12 (no 7); null for desert
}

export interface CatanPort {
  hexId: string;
  edge: number;                    // 0..5
  type: 'generic' | CatanTerrain;  // 'generic' = 3:1
  ratio: '3:1' | '2:1';
}

export interface CatanPlayerState {
  handSize: number;                                  // >= 0, public
  built: { settlements: number; cities: number; roads: number }; // built counts
  devCount: number;                                  // >= 0 dev cards held
  badges: { longestRoad: boolean; largestArmy: boolean };
}

export interface CatanGameState {
  v: 1;
  game: 'catan';
  board: {
    hexes: CatanHex[];        // exactly 19
    robberHexId: string;      // id of a hex in `hexes`
    ports?: CatanPort[];      // optional (preset); rendering is polish
  };
  dice: { last: number | null; history: number[] }; // sum 2..12; history newest-first
  players: Record<string, CatanPlayerState>;         // keyed by LiveSessionPlayer.id
}

/** Safe-parse the opaque gameState. Returns null on wrong game/version/shape (never throws). */
export function parseCatanGameState(raw: unknown): CatanGameState | null;

/** Piece totals per player (base game) — remaining = TOTALS - built. */
export const CATAN_PIECE_TOTALS = { settlements: 5, cities: 4, roads: 15 } as const;
```

`parseCatanGameState` uses `CatanGameStateSchema.safeParse`; a `game !== 'catan'` or `v !== 1` value returns `null` (so another game's state, or a future version, degrades to the empty state rather than crashing). `players` is a permissive record (unknown player ids tolerated; the renderer only reads ids present in `session.players`).

### L2 — board preset (`catan-board-preset.ts`)

```ts
export function generateStandardBoard(): {
  hexes: CatanHex[];
  robberHexId: string;
  ports: CatanPort[];
};
```

Produces a valid base-game layout:
- **Terrain multiset:** exactly 4 wood · 4 sheep · 4 wheat · 3 brick · 3 ore · 1 desert (19), shuffled (Fisher–Yates over `Math.random`).
- **Number tokens:** the standard 18-token set `2,3,3,4,4,5,5,6,6,8,8,9,9,10,10,11,11,12` assigned to the 18 non-desert hexes (shuffled); desert gets `number: null`.
- **Positions:** fixed column layout `[3,4,5,4,3]` → ids `h0..h18`.
- **Robber:** starts on the desert hex.
- **Ports:** a fixed standard 9-port coastline set (static positions; type mix 4×generic + 5×resource). Included in the schema; **rendering is polish** (MVP may render hexes+numbers+robber and skip port glyphs).

The board is **persisted into gameState** (so every viewer sees the same random board via SSE/DTO); it is generated **once** by the host, not re-derived on load.

### L2 — host-edit (`use-catan-state-editor.ts`)

A hook wrapping `useUpdateLiveGameState(sessionId)` plus the current parsed state. Returns typed mutators:

```ts
interface CatanStateEditor {
  initializeState(): void;                       // host CTA when gameState is null → preset board + zeroed players
  regenerateBoard(): void;                        // new preset board, keep dice/players
  setDiceRoll(sum: number): void;                 // set dice.last + prepend history
  moveRobber(hexId: string): void;
  bumpBuilt(playerId, piece: 'settlements'|'cities'|'roads', delta: 1|-1): void; // clamped [0, TOTALS[piece]]
  setDevCount(playerId, delta: 1|-1): void;       // clamped >= 0
  setHandSize(playerId, delta: 1|-1): void;       // clamped >= 0
  toggleBadge(playerId, badge: 'longestRoad'|'largestArmy'): void; // exclusive across players
}
```

Each mutator computes the next `CatanGameState` from the current one, then:
1. **Optimistic:** `useLiveSessionStore.getState().setGameState(next)` immediately (instant host feedback).
2. **Debounced 500 ms:** `useUpdateLiveGameState.mutate(next)` (trailing; flush on unmount / player-change).

Badges are **exclusive**: toggling `longestRoad` on player A clears it on all others (same for `largestArmy`) — matches Catan rules and the mockup's single-holder badges.

`initializeState` seeds `players` for every `session.players[].id` with zeroed state (`handSize: 0`, `built: {0,0,0}`, `devCount: 0`, no badges). Players added after init are lazily defaulted by the renderer (missing id → zeroed view) and folded into the state on the next host edit.

On PUT failure: a `sonner` toast; the optimistic state remains and the next successful edit or the SSE echo reconciles.

### L3 — components

Pure leaf components (props + callbacks; testable in isolation):

- **`CatanHexBoard.tsx`** — SVG flat-top 19-hex board from `board.hexes` (column heights `[3,4,5,4,3]`), terrain fill (palette), number token (6/8 rendered "hot"), robber marker on `robberHexId`. Props: `{ board, editable, onMoveRobber? }`. Host (`editable`): hex tap → `onMoveRobber(hexId)` (keyboard-accessible buttons). Ports rendered if present (polish).
- **`CatanDiceControl.tsx`** — last roll (big) + mini history strip. Props: `{ dice, editable, onRoll? }`. Host: a 2–12 quick-tap row → `onRoll(sum)`.
- **`CatanPlayerCard.tsx`** — one card per player: color swatch + name + VP (from scoring/`livePoints`) + hand size + built/remaining pieces + dev count + badges. Props: `{ player: LiveSessionPlayerDto, state: CatanPlayerState, vp: number, editable, on* }`. Host: steppers (hand/pieces/dev) + badge toggles; non-host: read-only display.
- **`CatanLiveFlavor.tsx`** (**rewrite**, container) — reads `useLiveSessionStore(s => s.gameState)` → `parseCatanGameState`; uses `use-catan-state-editor`. Composes the reused round/turn/phase header + `CatanHexBoard` + `CatanDiceControl` + player cards. **Empty state** (parsed state `null`): host sees a "Genera board Catan" CTA (`initializeState`); non-host sees "In attesa dell'host". Props: `{ session, labels, livePoints?, phaseName?, viewerRole, sessionId }`.

`catan-palette.ts` is extended with terrain fill colors (verbatim from the mockup's `RES` set — the brief's only non-token colors) alongside the existing piece palette.

### Wiring

`FlavorRenderer.tsx` threads two extra props to the flavor: `viewerRole` and `sessionId` (both already available in `SessionLiveView` / the flavor tab host). No other dispatcher change. i18n: extend `flavor.catan.*` (board, dice, pieces, dev, badges, edit affordances, empty-state) in `it.json` + `en.json`; templates read via `intl.messages` (react-intl does not ICU-interpolate the `{n}`/`{name}` placeholders used here).

## Data flow

- **Read:** `SessionLiveView` (L1) hydrates `store.gameState` from the DTO + `session:game-state` SSE. `CatanLiveFlavor` reads it → `parseCatanGameState` → typed | null.
- **Write (host):** leaf callback → editor mutator → optimistic `setGameState` + debounced `PUT /game-state` → BE persists + broadcasts → every viewer's store updates.
- **VP:** from `scoreData` / `livePoints` (existing overlay), **not** from `gameState`.

## Error handling & edge cases

- `gameState === null` → empty state (host CTA / viewer waiting).
- `gameState` present but not Catan / wrong version / malformed → `parseCatanGameState` returns `null` → same empty state; host can (re)generate. `console.warn` once.
- PUT failure → `sonner` toast; optimistic state retained.
- Board preset is always valid (unit-tested distribution).
- `prefers-reduced-motion` disables the 6/8 "hot" pulse.
- A player present in `session.players` but absent from `gameState.players` renders with a zeroed default (no crash); host edits fold them in.

## Testing

- **Unit:** `parseCatanGameState` (valid · wrong game · wrong version · malformed · extra player ids tolerated); `generateStandardBoard` (exact terrain multiset · 18 number tokens · desert numberless · robber on desert · 19 ids); editor mutators (each produces the correct next JSON · clamps · badge exclusivity · optimistic `setGameState` · debounce trailing + flush).
- **Component (RTL + jsdom):** `CatanHexBoard` (19 hexes · robber marker · host tap → `onMoveRobber` · read-only no buttons); `CatanDiceControl` (host 2–12 tap → `onRoll` · read-only history); `CatanPlayerCard` (host steppers/toggles → callbacks · read-only display · clamps at 0); `CatanLiveFlavor` (empty-state host CTA vs viewer waiting · populated compose · viewerRole gating). `jest-axe` AA on the flavor.
- **E2E:** regression-guard (non-catan fixture → no flavor tab) + `test.fixme()` positive path (flavor gated on `liveSessionDto`, null in `?fixture=host` — the known SSE-smoke limitation).

## File map

Create:
- `flavors/catan/catan-state.ts`
- `flavors/catan/catan-board-preset.ts`
- `flavors/catan/use-catan-state-editor.ts`
- `flavors/catan/CatanHexBoard.tsx`
- `flavors/catan/CatanDiceControl.tsx`
- `flavors/catan/CatanPlayerCard.tsx`
- `__tests__/` for each above

Modify:
- `flavors/catan/CatanLiveFlavor.tsx` (rewrite as container)
- `flavors/catan/catan-palette.ts` (terrain colors)
- `session-live/FlavorRenderer.tsx` (thread `viewerRole` + `sessionId`)
- the flavor-tab host in `SessionLiveView` / `RightColumnTabs` (pass the two props)
- `apps/web/messages/it.json` + `en.json` (`flavor.catan.*`)

## Out of scope (YAGNI)

Piece-coordinate placement on the board; per-resource hand composition; shared resource bank; trade offers; dev-deck breakdown; the mockup's 5-tab right column (the app shell owns the right column; the flavor lives inside the single Flavor tab); any backend change. These can become follow-ups if a real game session proves them worth the host-entry cost.
