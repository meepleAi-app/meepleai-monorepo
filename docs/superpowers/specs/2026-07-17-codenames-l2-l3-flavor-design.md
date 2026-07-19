# Codenames L2+L3 flavor — design

**Issue:** #2790 (G6d, epic #3025). **Date:** 2026-07-17. **Status:** approved (parallel recon + owner pick).

## Goal

Add the Codenames live flavor, reusing the generalized per-game flavor pattern (`flavors/<game>/`, game-agnostic `FlavorRenderer`, self-built i18n labels). Codenames is the 3rd flavor and the first with a genuinely rich, low-friction game-specific surface: the 5×5 word grid with two viewing perspectives.

## Why Codenames (recon finding)

Its live mockup is the universal session skeleton plus three game-specific pieces: the 25-cell word grid (two-perspective render + spymaster key), the two team trackers, and the single active clue. Host-entry friction is **LOW** — the 25-word/key layout is generated once by a preset (like Catan's `generateStandardBoard`), and play is a single tap per reveal (the action every physical game already tracks). VP maps onto the existing scoring system (Ranking scoreType). This makes Codenames the cheapest-to-model rich flavor of the remaining games.

## Scope decisions (locked)

1. **Model the 25-cell board + the active clue + the current team in `gameState`.** Everything else is derived or cut.
2. **VP stays in the existing scoring system** (Ranking; team agents-found/remaining is scored in the Score tab as today) — `gameState` never carries scores. Same invariant as Catan/Wingspan.
3. **`currentTeam` is STORED in `gameState`, not derived from the turn/phase system.** The recon preferred deriving it from `phaseName`, but that assumes the session is configured with team-named phases — the same fragile cross-system dependency that produced the Wingspan dimension-name seam. Storing a single `'red' | 'blue'` scalar is robust, self-contained, and the `WordGrid`/`ClueStrip` are the only subscribers. (Open for the owner to override at spec review.)
4. **`assassinRevealed` and team found/remaining are DERIVED from `board`, not stored** (single source of truth = the cells). Game-over is derived: an assassin cell revealed → the other team wins; all of a team's cells revealed → that team wins.
5. **Discrete edits → IMMEDIATE PUT, no debounce.** Unlike Catan/Wingspan (continuous `+/-` adjustments debounced 500 ms), every Codenames edit (reveal a cell, set a clue, switch team, regenerate) is a discrete must-not-be-lost event. The editor commits optimistically + PUTs immediately (no trailing debounce) so a fast multi-guess turn never drops a tap.
6. **Perspective (operative ↔ spymaster) is LOCAL component state, not `gameState`** — a per-viewer display/privacy preference (the spymaster key is not shared truth). Cut from persistence, like the mockup's screen-share hide toggle.
7. **Cut (render from the existing action-log / derive, never new `gameState`):** clue-history persistence (the universal ActionLog already narrates clue/reveal events), guess-pip/timer countdown (client-computed from `clue.number` + reveal count), rich game-over copy (derived).

## Headline: zero backend, no new plumbing

L1 provides the opaque `gameState`. The plumbing is already game-agnostic (Wingspan #2788): `FlavorRenderer` dispatches on `FLAVOR_MAP` with game-agnostic `FlavorProps`, and each flavor self-builds its i18n labels. Codenames adds one `FLAVOR_MAP` entry + a `flavors/codenames/` folder + i18n keys. Nothing else changes.

## Architecture

All under `apps/web/src/components/features/session-live/flavors/codenames/`.

### L2 — state contract (`codenames-state.ts`)

```ts
export const CODENAMES_STATE_VERSION = 1;
export const CODENAMES_BOARD_SIZE = 25;
/** Standard key distribution for a 25-card board: startingTeam gets 9, other 8, 7 neutral, 1 assassin. */
export const CODENAMES_KEY_COUNTS = { starting: 9, other: 8, neutral: 7, assassin: 1 } as const;

export type CodenamesTeam = 'red' | 'blue';          // z.enum
export type CodenamesKey = 'red' | 'blue' | 'neutral' | 'assassin'; // z.enum

export interface CodenamesCell { word: string; key: CodenamesKey; revealed: boolean }
export interface CodenamesClue { word: string; number: number }      // number >= 0

export interface CodenamesGameState {
  v: 1;
  game: 'codenames';
  board: CodenamesCell[];        // exactly 25
  currentTeam: CodenamesTeam;
  clue: CodenamesClue | null;
}

export function parseCodenamesGameState(raw: unknown): CodenamesGameState | null; // Zod safeParse → null

// Derivations (pure helpers, no stored duplication):
export function isAssassinRevealed(s: CodenamesGameState): boolean;               // board.some(assassin && revealed)
// total = count of cells whose key === team (the 9/8 split is encoded in the board);
// found = those also revealed.
export function teamCounts(s: CodenamesGameState, team: CodenamesTeam): { total: number; found: number };
// assassin revealed → the on-turn team (currentTeam) loses, so the OTHER team wins;
// else a team with all its cells revealed wins; else null. No startingTeam needed.
export function codenamesWinner(s: CodenamesGameState): CodenamesTeam | null;
```

`board` Zod: `z.array(CodenamesCellSchema).length(25)`; `clue` nullable; `currentTeam` enum. Wrong game/version/shape → `null`.

### L2 — board preset (`codenames-board-preset.ts`)

```ts
export function generateCodenamesBoard(startingTeam?: CodenamesTeam): { board: CodenamesCell[]; startingTeam: CodenamesTeam };
```

- Pick 25 **distinct** words from a bundled `CODENAMES_WORD_BANK` (a static list ≥ 60 words; language-neutral-ish or IT/EN mix — a fixed const, not i18n).
- Assign keys: 9 starting-team, 8 other, 7 neutral, 1 assassin (25 total); Fisher–Yates shuffle both word selection and key assignment.
- `startingTeam` defaults to a shuffled choice; returned so the flavor knows the 9/8 split.
- All `revealed: false`. Unit-tested: exactly 25 distinct words, key multiset {9,8,7,1}, one assassin.

### L2 — host-edit (`use-codenames-state-editor.ts`)

Mirrors the Catan/Wingspan editor but **without debounce** (immediate PUT). Reads `useLiveSessionStore(s => s.gameState)` → `parseCodenamesGameState`; wraps `useUpdateLiveGameState(sessionId)`; each mutator = optimistic `setGameState(next)` + **immediate** `mutate(next)`.

```ts
interface CodenamesStateEditor {
  state: CodenamesGameState | null;
  initializeState(): void;                 // host CTA → generateCodenamesBoard(), currentTeam = startingTeam, clue = null
  regenerateBoard(): void;                 // fresh preset board, reset clue
  revealCell(index: number): void;         // 0..24; toggles that cell's `revealed` true (idempotent)
  setClue(word: string, number: number): void;   // number clamped >= 0
  clearClue(): void;
  switchTeam(): void;                      // red <-> blue, clears clue
}
```

No score mutators — scores flow through the existing score editor.

### L3 — components (pure) + container

- **`CodenamesWordGrid.tsx`** — SVG/CSS 5×5 grid of 25 word cards. Props `{ board, editable, perspective: 'operative' | 'spymaster', onRevealCell?, labels }`. Covered cells: operative view shows a neutral card; spymaster view tints each covered card by its `key` colour. Revealed cells show their `key` colour + a "covered" mark. Host (`editable`): tap an unrevealed cell → `onRevealCell(index)` (keyboard-accessible button, Enter/Space). The perspective toggle is owned by the container (local state), passed down.
- **`CodenamesTeamTracker.tsx`** — two team cards; `found`/`total` derived via `teamCounts`; current-team highlight from `currentTeam`. Read-only. Props `{ board, currentTeam, labels }`.
- **`CodenamesCurrentClueStrip.tsx`** — active clue as `WORD : NUMBER` + guess pips (derived from `clue.number`). Host on-turn: editable word input + number stepper + "give clue"/"clear" + "end turn (switch team)". Props `{ clue, currentTeam, editable, onSetClue?, onClearClue?, onSwitchTeam?, labels }`.
- **`CodenamesLiveFlavor.tsx`** — container. Self-builds i18n labels (`useIntl` + `useTranslation`). Renders: a themed **leaderboard** (from `livePoints`/`session.players`, ungated), then — when `parseCodenamesGameState(store.gameState)` is non-null — a perspective toggle (local state, host-only or all) + `CodenamesWordGrid` + `CodenamesTeamTracker` + `CodenamesCurrentClueStrip` + a derived game-over banner (`codenamesWinner`); when null, a host `initBoardCta` ("Genera griglia") or a viewer-waiting note. Props: the standard `FlavorProps` (`{ session, viewerRole, sessionId, className?, livePoints?, phaseName? }`).

### Wiring

- `FlavorRenderer.tsx` — add `CodenamesLiveFlavorLazy` (module-scope `dynamic`) + `codenames: { live: CodenamesLiveFlavorLazy }` to `FLAVOR_MAP`. (Props/plumbing already game-agnostic from #2788 — no other change.)
- i18n: add `pages.sessionLive.flavor.codenames.*` to `it.json` + `en.json`.

## Data flow

- **Scoring** (leaderboard): from `livePoints`/`session.players` (existing), ungated.
- **Board/clue/team**: from `useLiveSessionStore(s => s.gameState)` via `parseCodenamesGameState`.
- **Host writes**: reveal/clue/team/regenerate → optimistic `setGameState` + **immediate** `PUT /game-state`; scores → existing score editor.

## Error handling & edge cases

- `gameState` null / wrong game / malformed → host `initBoardCta` (or viewer waiting); leaderboard still renders. `console.warn` once on malformed (consistent with the family).
- PUT failure → `sonner` toast; optimistic state retained.
- `revealCell` on an already-revealed or out-of-range index → no-op.
- `setClue` clamps `number >= 0`.
- Preset always yields a valid 25/distinct/{9,8,7,1} board (unit-tested).
- Non-host viewers: read-only grid, no reveal, spymaster perspective available only if the product wants it — MVP: perspective toggle host-only (avoids leaking the key to operatives on a shared screen); default operative view for everyone.

## Testing

- **Unit:** `parseCodenamesGameState` (valid / wrong game / version / non-25 board / malformed); `generateCodenamesBoard` (25 distinct words, key multiset {9,8,7,1}, one assassin, startingTeam returned); derivations (`isAssassinRevealed`, `teamCounts`, `codenamesWinner`: assassin→other, all-found→team, else null); editor mutators (revealCell idempotent + immediate PUT, setClue clamp, switchTeam clears clue, initialize/regenerate, no-op on null except init).
- **Component (RTL + jsdom):** `CodenamesWordGrid` (25 cells; host tap→onRevealCell; read-only no buttons; spymaster vs operative tint differs); `CodenamesTeamTracker` (found/remaining derived; current-team highlight); `CodenamesCurrentClueStrip` (host edit callbacks vs read-only chip); `CodenamesLiveFlavor` (leaderboard ungated; host CTA when null; populated compose; game-over banner on assassin). `jest-axe` AA on the flavor.
- **E2E:** the existing `session-live-catan-flavor.smoke.spec.ts` regression guard already covers the gating (a non-flavor fixture shows no game tab); no new E2E file (positive path is the known `?fixture=host` limitation).

## File map

Create:
- `flavors/codenames/codenames-state.ts`
- `flavors/codenames/codenames-board-preset.ts`
- `flavors/codenames/use-codenames-state-editor.ts`
- `flavors/codenames/CodenamesWordGrid.tsx`
- `flavors/codenames/CodenamesTeamTracker.tsx`
- `flavors/codenames/CodenamesCurrentClueStrip.tsx`
- `flavors/codenames/CodenamesLiveFlavor.tsx`
- `flavors/codenames/__tests__/*`

Modify:
- `session-live/FlavorRenderer.tsx` — `codenames` in `FLAVOR_MAP`.
- `src/locales/it.json` + `en.json` — `flavor.codenames.*`.

## Out of scope (YAGNI)

Clue-history persistence; guess timer; screen-share key-hide persistence; rich game-over copy; free-text word-list importer (preset only for MVP); Codenames summary flavor; any backend change; deriving `currentTeam` from phases (stored instead, for robustness).
