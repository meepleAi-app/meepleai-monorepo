# Paleo L2+L3 flavor — design

**Issue:** #2789 (G6c, epic #3025). **Date:** 2026-07-17. **Status:** approved (recon + owner batch mandate "falli tutti").

## Goal

Add the Paleo live flavor, reusing the generalized game-agnostic flavor pattern (`flavors/<game>/`, `FlavorRenderer` dispatch, self-built i18n labels). Paleo is a **thin, counters-pools** flavor — the same shape as Puerto Rico but smaller: its two headline mechanics (win/loss track, turn phase) are already fully covered by the existing scoring system and the turn indicator, so the only genuinely game-flavored, cheap-to-key content is a small shared resource pool + a per-player tribe status.

## Scope decisions (locked — recon + owner batch mandate)

1. **Model 4 shared resource counters + per-player tribe status.** `resources: {wood, stone, food, knowledge}` (shared, host ± taps); `survivors: Record<playerId, 'alive'|'wounded'|'dead'>` (per-player, tap-to-cycle). Nothing else.
2. **VP / win-loss stays in the existing scoring system** (Paleo's cave-painting vs skull outcome is a BinaryWin/collective scoring concern) — `gameState` never carries a score or a win/loss flag. Same invariant as Catan/Wingspan/Codenames/Puerto Rico.
3. **Turn phase (Mattina/Giorno/Notte) reuses the existing turn indicator** (`phaseName`) — NOT modelled in gameState.
4. **Continuous / discrete edits → DEBOUNCED PUT (500 ms).** Resource ± is continuous; status tap-to-cycle is discrete but rare and non-critical (no fast multi-tap loop) → debounced is fine and keeps the editor uniform (like Puerto Rico).
5. **Tribe status is keyed by `session.players`** (one status per player, seeded `alive`), NOT a free-text roster — a free roster adds typing friction and the player list is the natural, zero-entry seed. (Same players-keyed choice as Zombicide's wound pips.)

## Headline: zero backend, no plumbing refactor

L1 provides the opaque `gameState`; the plumbing is already game-agnostic. Paleo adds one `FLAVOR_MAP` entry + a `flavors/paleo/` folder + i18n. Nothing else changes.

## Architecture

All under `apps/web/src/components/features/session-live/flavors/paleo/`.

### L2 — state contract (`paleo-state.ts`)

```ts
export const PALEO_STATE_VERSION = 1;
export const PALEO_RESOURCES = ['wood', 'stone', 'food', 'knowledge'] as const; // z.enum
export type PaleoResource = (typeof PALEO_RESOURCES)[number];
export const PALEO_STATUSES = ['alive', 'wounded', 'dead'] as const;             // z.enum
export type PaleoStatus = (typeof PALEO_STATUSES)[number];

export interface PaleoResources { wood: number; stone: number; food: number; knowledge: number }
export interface PaleoGameState {
  v: 1; game: 'paleo';
  resources: PaleoResources;                    // shared pool
  survivors: Record<string, PaleoStatus>;       // keyed by LiveSessionPlayer.id
}

export function parsePaleoGameState(raw: unknown): PaleoGameState | null; // Zod safeParse → null
export function emptyPaleoResources(): PaleoResources;                    // all 0
/** Seeds resources 0 + every player 'alive'. */
export function initialPaleoState(playerIds: readonly string[]): PaleoGameState;
/** alive → wounded → dead → alive (wraps). */
export function nextPaleoStatus(status: PaleoStatus): PaleoStatus;
```

Zod: `resources` is a **fixed** `z.object({wood,stone,food,knowledge})` (all 4 required, each `int≥0`) — NOT `z.record` (a record would allow a missing resource → `undefined` at render). `survivors` is `z.record(z.string(), PaleoStatusSchema)`. Wrong game/version/shape → `null`.

### L2 — palette (`paleo-palette.ts`)

The 3 tribe statuses have a semantic colour (alive = green, wounded = amber, dead = muted grey). Inline `hsl()` via a palette module (`paleoStatusColor(status)`), token-lint safe like `catan-palette`/`puerto-rico-palette`. Add a line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- <reason>` on any hue that trips the rule (the controller verifies with `pnpm exec eslint` per task). The 4 resources have NO per-resource colour (plain labelled counters with semantic tokens).

### L2 — host-edit (`use-paleo-state-editor.ts`)

Mirrors the Puerto Rico editor (optimistic `setGameState` + **debounced-500 ms** PUT; flush on unmount; `readState()` fresh reads).

```ts
interface PaleoStateEditor {
  state: PaleoGameState | null;
  initializeState(): void;                                  // seed via initialPaleoState(playerIds)
  bumpResource(field: PaleoResource, delta: 1 | -1): void;  // clamp ≥0
  cycleSurvivorStatus(playerId: string): void;              // nextPaleoStatus; folds in a missing player as 'alive'→next
}
```

No score mutators — scores flow through the existing score editor. Mutators no-op when parsed state is null (except `initializeState`). `readState()` re-parses `useLiveSessionStore.getState().gameState` fresh at call time.

### L3 — components (pure) + container

- **`PaleoResourcePanel.tsx`** — the 4 shared resource counters (wood/stone/food/knowledge), each with a label + count; host (`editable`): `+/-` steppers. Props `{ resources, editable, onBump?, labels }`.
- **`PaleoTribePanel.tsx`** — per-player status roster: each row = player `displayName` + a status badge (coloured via the palette); host (`editable`): tap-to-cycle button (`alive → wounded → dead → alive`). Read-only shows the badge without a button. Props `{ players, survivors, editable, onCycle?, labels }`.
- **`PaleoLiveFlavor.tsx`** — container. Self-builds i18n labels (`useIntl` + `useTranslation`). Renders a themed **leaderboard** (from `livePoints`/`session.players`, ungated) + — when `parsePaleoGameState(store.gameState)` is non-null — the resource panel + the tribe panel; when null, a host `initBoardCta` ("Inizia partita") or a viewer-waiting note. Standard `FlavorProps` (`{ session, viewerRole, sessionId, className?, livePoints?, phaseName? }`).

### Wiring

- `FlavorRenderer.tsx` — add `PaleoLiveFlavorLazy` (module-scope `dynamic`) + `'paleo': { live: PaleoLiveFlavorLazy }` to `FLAVOR_MAP`.
- i18n: add `pages.sessionLive.flavor.paleo.*` to `it.json` + `en.json` (identical key set both locales).

## Data flow

- **Scoring** (leaderboard): from `livePoints`/`session.players`, ungated.
- **Resources / tribe status**: from `useLiveSessionStore(s => s.gameState)` via `parsePaleoGameState`.
- **Host writes**: resource/status edits → optimistic `setGameState` + **debounced** `PUT /game-state`; scores → existing score editor.

## Error handling & edge cases

- `gameState` null / wrong game / malformed → host `initBoardCta` (or viewer waiting); leaderboard still renders.
- PUT failure → `sonner` toast; optimistic state retained.
- Resource bumps clamp ≥0.
- A player in `session.players` but absent from `gameState.survivors` renders with an `alive` default badge (no crash); host cycle folds them in.
- `initialPaleoState` seeds every current player `alive`.

## Testing

- **Unit:** `parsePaleoGameState` (valid / wrong game / version / missing resource / malformed); `emptyPaleoResources`; `initialPaleoState` (resources 0, every player alive); `nextPaleoStatus` (cycle wraps); editor mutators (bumpResource clamp, cycleSurvivorStatus advances + folds missing player, no-op on null except init, debounce + optimistic + flush-on-unmount).
- **Component (RTL + jsdom):** `PaleoResourcePanel` (host steppers fire; read-only no buttons; 4 resources render); `PaleoTribePanel` (host tap-to-cycle fires `onCycle`; read-only no buttons; badge per player); `PaleoLiveFlavor` (leaderboard ungated; host CTA when null; populated compose). `jest-axe` AA on the flavor.
- **E2E:** the existing `session-live-catan-flavor.smoke.spec.ts` regression guard covers the gating; no new E2E file.
- **Lint gate:** the controller runs `pnpm exec eslint --max-warnings=0` on the flavor dir per task (the pre-commit hook does NOT run `meepleai/no-inline-hsl-v2` or the `style`-prop case of `local/no-hardcoded-color-utility`).

## File map

Create: `flavors/paleo/paleo-state.ts`, `paleo-palette.ts`, `use-paleo-state-editor.ts`, `PaleoResourcePanel.tsx`, `PaleoTribePanel.tsx`, `PaleoLiveFlavor.tsx`, `__tests__/*`.
Modify: `session-live/FlavorRenderer.tsx` (`paleo` in `FLAVOR_MAP`), `src/locales/it.json` + `en.json` (`flavor.paleo.*`).

## Out of scope (YAGNI)

Per-player secret hands + chosen action (architecturally incompatible with host-only entry); action/mission/encounter deck counters (decorative, high tap cost); the asymmetric skill tree (heavy one-time setup); the simultaneous-reveal overlay (UI choreography, not persistable state); cave-painting / skull tracks as gameState (redundant — already the scoring outcome); per-member xp / cause-of-death / narrative fields; mission progress trackers (recon-optional, cut for MVP); a free-text tribe roster (players-keyed instead); the Paleo summary flavor; any backend change.
