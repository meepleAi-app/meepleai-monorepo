# Catan L2+L3 Rich Flavor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Catan MVP leaderboard flavor with a rich, mockup-faithful live flavor driven by a per-game state schema (L2) that the host edits inline, streamed to all viewers via the L1 generic game-state layer.

**Architecture:** FE-only. L1 (PR #3031) already provides write/persist/stream/expose of the opaque `LiveGameSession.GameState`. L2 = a versioned FE JSON schema + defensive parser + a client-side board preset + a debounced host-edit hook that PUTs via L1. L3 = an SVG hex board + dice control + player cards, composed in a rewritten `CatanLiveFlavor` container. Victory Points stay in the existing scoring system; `gameState` carries only Catan-specific extras.

**Tech Stack:** Next.js 16 · React 19 · TypeScript · Zod · Zustand (`useLiveSessionStore`) · TanStack Query (`useUpdateLiveGameState`) · Vitest + Testing Library + jest-axe · Tailwind (semantic tokens; inline `hsl()` only for terrain/piece colors).

## Global Constraints

- **Issue:** #3033 (pilot of epic #3025). Spec: `docs/superpowers/specs/2026-07-16-catan-l2-l3-rich-flavor-design.md`.
- **Zero backend changes.** The BE treats `gameState` as opaque; the FE owns typing.
- **VP stays in scoring** (`scoreData` / `livePoints`). `gameState` never carries VP.
- **State schema version:** `v: 1`, discriminator `game: 'catan'`. `parseCatanGameState` returns `null` (never throws) on wrong game/version/shape.
- **Piece totals (base game):** `{ settlements: 5, cities: 4, roads: 15 }`. Stored `built` counts; `remaining = total - built`.
- **Resources:** hand SIZE only (public integer). No per-resource composition. No shared bank.
- **Host-edit only** when `viewerRole === 'Host'`; autosave debounced **500 ms** → `useUpdateLiveGameState`; optimistic `setGameState` first; flush on unmount.
- **Badges are exclusive** across players (`longestRoad`, `largestArmy` each held by ≤1 player).
- **Colors:** use semantic Tailwind tokens everywhere EXCEPT Catan terrain/piece colors, which are inline `hsl()`/hex via the palette module (mockup brief's only non-token colors; token-lint safe).
- **Tests:** Vitest. Query DOM via `data-slot` / roles, NOT `getByTestId`. Component a11y via `jest-axe` (`toHaveNoViolations`). Placeholders forbidden.
- **Files live under** `apps/web/src/components/features/session-live/flavors/catan/` unless stated. Run commands from `apps/web/`.
- **Windows:** if a run hangs, the pre-commit hook runs `tsc --noEmit` (~2 min) — allow ≥5 min for commits. If typecheck errors on stale `.next/types`, run `rm -rf .next/types` first (never `--no-verify`).

## File Structure

Create:
- `flavors/catan/catan-state.ts` — Zod schema, types, `parseCatanGameState`, `CATAN_PIECE_TOTALS`, `emptyCatanPlayerState`.
- `flavors/catan/catan-board-preset.ts` — `generateStandardBoard()`.
- `flavors/catan/use-catan-state-editor.ts` — `useCatanStateEditor()` host mutators + optimistic + debounced PUT.
- `flavors/catan/CatanHexBoard.tsx` — SVG 19-hex board (pure).
- `flavors/catan/CatanDiceControl.tsx` — dice display + host quick-tap (pure).
- `flavors/catan/CatanPlayerCard.tsx` — per-player card (pure).
- `flavors/catan/__tests__/*` — one test file per unit above.

Modify:
- `flavors/catan/CatanLiveFlavor.tsx` — rewrite as container.
- `flavors/catan/catan-palette.ts` — add terrain fill colors.
- `session-live/FlavorRenderer.tsx` — thread `viewerRole` + `sessionId`.
- `sessions/[id]/live/_components/SessionLiveView.tsx` — pass `viewerRole` + `sessionId` to both `FlavorRenderer` render sites (~L1389 mobile, ~L1613 desktop); extend `catanFlavorLabels` memo.
- `src/locales/it.json` + `src/locales/en.json` — `pages.sessionLive.flavor.catan.*` keys.
- `flavors/catan/__tests__/CatanLiveFlavor.test.tsx` — rewrite for the container.

---

## Task 1: L2 state schema + parser

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/catan-state.ts`
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/catan-state.test.ts`

**Interfaces:**
- Produces: `CatanGameState`, `CatanHex`, `CatanPort`, `CatanPlayerState`, `CatanTerrain` types; `parseCatanGameState(raw: unknown): CatanGameState | null`; `CATAN_STATE_VERSION = 1`; `CATAN_PIECE_TOTALS = { settlements: 5, cities: 4, roads: 15 }`; `emptyCatanPlayerState(): CatanPlayerState`.

- [ ] **Step 1: Write the failing test**

```ts
// catan-state.test.ts
import { describe, expect, it } from 'vitest';

import {
  CATAN_PIECE_TOTALS,
  emptyCatanPlayerState,
  parseCatanGameState,
} from '../catan-state';

const VALID = {
  v: 1,
  game: 'catan',
  board: {
    hexes: [{ id: 'h0', col: 0, row: 0, terrain: 'desert', number: null }],
    robberHexId: 'h0',
    ports: [{ hexId: 'h0', edge: 4, type: 'generic', ratio: '3:1' }],
  },
  dice: { last: 8, history: [8, 6] },
  players: {
    p1: { handSize: 3, built: { settlements: 2, cities: 1, roads: 4 }, devCount: 2, badges: { longestRoad: true, largestArmy: false } },
  },
};

describe('parseCatanGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parseCatanGameState(VALID);
    expect(parsed).not.toBeNull();
    expect(parsed?.dice.last).toBe(8);
    expect(parsed?.players.p1?.badges.longestRoad).toBe(true);
  });

  it('returns null for a different game', () => {
    expect(parseCatanGameState({ ...VALID, game: 'wingspan' })).toBeNull();
  });

  it('returns null for a future version', () => {
    expect(parseCatanGameState({ ...VALID, v: 2 })).toBeNull();
  });

  it('returns null for malformed / non-object input', () => {
    expect(parseCatanGameState(null)).toBeNull();
    expect(parseCatanGameState('nope')).toBeNull();
    expect(parseCatanGameState({ v: 1, game: 'catan' })).toBeNull();
  });

  it('accepts optional ports absent', () => {
    const { ports: _drop, ...board } = VALID.board;
    expect(parseCatanGameState({ ...VALID, board })).not.toBeNull();
  });

  it('exposes base-game piece totals', () => {
    expect(CATAN_PIECE_TOTALS).toEqual({ settlements: 5, cities: 4, roads: 15 });
  });

  it('emptyCatanPlayerState is fully zeroed', () => {
    expect(emptyCatanPlayerState()).toEqual({
      handSize: 0,
      built: { settlements: 0, cities: 0, roads: 0 },
      devCount: 0,
      badges: { longestRoad: false, largestArmy: false },
    });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/catan-state.test.ts`
Expected: FAIL — `Cannot find module '../catan-state'`.

- [ ] **Step 3: Write the implementation**

```ts
// catan-state.ts
import { z } from 'zod';

export const CATAN_STATE_VERSION = 1;

export const CATAN_PIECE_TOTALS = { settlements: 5, cities: 4, roads: 15 } as const;
export type CatanPiece = keyof typeof CATAN_PIECE_TOTALS;

export const CatanTerrainSchema = z.enum(['wood', 'brick', 'sheep', 'wheat', 'ore', 'desert']);
export type CatanTerrain = z.infer<typeof CatanTerrainSchema>;

export const CatanHexSchema = z.object({
  id: z.string(),
  col: z.number().int(),
  row: z.number().int(),
  terrain: CatanTerrainSchema,
  number: z.number().int().nullable(),
});
export type CatanHex = z.infer<typeof CatanHexSchema>;

export const CatanPortSchema = z.object({
  hexId: z.string(),
  edge: z.number().int(),
  type: z.union([z.literal('generic'), CatanTerrainSchema]),
  ratio: z.enum(['3:1', '2:1']),
});
export type CatanPort = z.infer<typeof CatanPortSchema>;

export const CatanPlayerStateSchema = z.object({
  handSize: z.number().int(),
  built: z.object({
    settlements: z.number().int(),
    cities: z.number().int(),
    roads: z.number().int(),
  }),
  devCount: z.number().int(),
  badges: z.object({ longestRoad: z.boolean(), largestArmy: z.boolean() }),
});
export type CatanPlayerState = z.infer<typeof CatanPlayerStateSchema>;

export const CatanGameStateSchema = z.object({
  v: z.literal(CATAN_STATE_VERSION),
  game: z.literal('catan'),
  board: z.object({
    hexes: z.array(CatanHexSchema),
    robberHexId: z.string(),
    ports: z.array(CatanPortSchema).optional(),
  }),
  dice: z.object({ last: z.number().int().nullable(), history: z.array(z.number().int()) }),
  players: z.record(z.string(), CatanPlayerStateSchema),
});
export type CatanGameState = z.infer<typeof CatanGameStateSchema>;

/** Safe-parse the opaque L1 gameState. Returns null (never throws) on wrong game/version/shape. */
export function parseCatanGameState(raw: unknown): CatanGameState | null {
  const result = CatanGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function emptyCatanPlayerState(): CatanPlayerState {
  return {
    handSize: 0,
    built: { settlements: 0, cities: 0, roads: 0 },
    devCount: 0,
    badges: { longestRoad: false, largestArmy: false },
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/catan-state.test.ts`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/catan/catan-state.ts" "apps/web/src/components/features/session-live/flavors/catan/__tests__/catan-state.test.ts"
git commit -m "feat(session-live): #3033 Catan L2 state schema + parser"
```

---

## Task 2: Board preset generator

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/catan-board-preset.ts`
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/catan-board-preset.test.ts`

**Interfaces:**
- Consumes: `CatanHex`, `CatanPort`, `CatanTerrain` from `./catan-state`.
- Produces: `generateStandardBoard(): { hexes: CatanHex[]; robberHexId: string; ports: CatanPort[] }`.

- [ ] **Step 1: Write the failing test**

```ts
// catan-board-preset.test.ts
import { describe, expect, it } from 'vitest';

import { generateStandardBoard } from '../catan-board-preset';
import type { CatanTerrain } from '../catan-state';

function terrainCounts(hexes: { terrain: CatanTerrain }[]): Record<string, number> {
  return hexes.reduce<Record<string, number>>((acc, h) => {
    acc[h.terrain] = (acc[h.terrain] ?? 0) + 1;
    return acc;
  }, {});
}

describe('generateStandardBoard', () => {
  it('produces exactly 19 hexes with ids h0..h18', () => {
    const { hexes } = generateStandardBoard();
    expect(hexes).toHaveLength(19);
    expect(new Set(hexes.map(h => h.id)).size).toBe(19);
    expect(hexes.every(h => /^h\d+$/.test(h.id))).toBe(true);
  });

  it('uses the standard base-game terrain multiset', () => {
    expect(terrainCounts(generateStandardBoard().hexes)).toEqual({
      wood: 4, sheep: 4, wheat: 4, brick: 3, ore: 3, desert: 1,
    });
  });

  it('assigns the standard 18 number tokens to non-desert hexes; desert is numberless', () => {
    const { hexes } = generateStandardBoard();
    const desert = hexes.filter(h => h.terrain === 'desert');
    expect(desert).toHaveLength(1);
    expect(desert[0]?.number).toBeNull();
    const numbers = hexes.filter(h => h.terrain !== 'desert').map(h => h.number).sort((a, b) => (a ?? 0) - (b ?? 0));
    expect(numbers).toEqual([2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12]);
  });

  it('starts the robber on the desert hex', () => {
    const { hexes, robberHexId } = generateStandardBoard();
    const robberHex = hexes.find(h => h.id === robberHexId);
    expect(robberHex?.terrain).toBe('desert');
  });

  it('lays hexes out in columns of heights 3,4,5,4,3', () => {
    const { hexes } = generateStandardBoard();
    const perCol = [0, 1, 2, 3, 4].map(c => hexes.filter(h => h.col === c).length);
    expect(perCol).toEqual([3, 4, 5, 4, 3]);
  });

  it('emits ports anchored to existing hex ids', () => {
    const { hexes, ports } = generateStandardBoard();
    const ids = new Set(hexes.map(h => h.id));
    expect(ports.length).toBeGreaterThan(0);
    expect(ports.every(p => ids.has(p.hexId))).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/catan-board-preset.test.ts`
Expected: FAIL — `Cannot find module '../catan-board-preset'`.

- [ ] **Step 3: Write the implementation**

```ts
// catan-board-preset.ts
import type { CatanHex, CatanPort, CatanTerrain } from './catan-state';

const COL_HEIGHTS = [3, 4, 5, 4, 3] as const;

// Standard base-game terrain multiset (19 tiles).
const TERRAINS: CatanTerrain[] = [
  'wood', 'wood', 'wood', 'wood',
  'sheep', 'sheep', 'sheep', 'sheep',
  'wheat', 'wheat', 'wheat', 'wheat',
  'brick', 'brick', 'brick',
  'ore', 'ore', 'ore',
  'desert',
];

// Standard number-token set (18 tokens; no 7). One per non-desert tile.
const NUMBER_TOKENS = [2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12];

// Fixed coastal port layout (9 ports). hexId anchors are POSITIONAL (perimeter
// tiles are the same regardless of the shuffled terrain), so they always exist.
const PORTS: CatanPort[] = [
  { hexId: 'h0', edge: 4, type: 'generic', ratio: '3:1' },
  { hexId: 'h1', edge: 3, type: 'sheep', ratio: '2:1' },
  { hexId: 'h3', edge: 5, type: 'wheat', ratio: '2:1' },
  { hexId: 'h7', edge: 0, type: 'generic', ratio: '3:1' },
  { hexId: 'h12', edge: 0, type: 'ore', ratio: '2:1' },
  { hexId: 'h16', edge: 0, type: 'wood', ratio: '2:1' },
  { hexId: 'h18', edge: 1, type: 'generic', ratio: '3:1' },
  { hexId: 'h11', edge: 2, type: 'brick', ratio: '2:1' },
  { hexId: 'h15', edge: 1, type: 'generic', ratio: '3:1' },
];

function shuffle<T>(input: readonly T[]): T[] {
  const a = [...input];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    const tmp = a[i];
    a[i] = a[j];
    a[j] = tmp;
  }
  return a;
}

export function generateStandardBoard(): {
  hexes: CatanHex[];
  robberHexId: string;
  ports: CatanPort[];
} {
  const terrains = shuffle(TERRAINS);
  const numbers = shuffle(NUMBER_TOKENS);
  const hexes: CatanHex[] = [];
  let idx = 0;
  let numIdx = 0;
  let robberHexId = 'h0';

  for (let col = 0; col < COL_HEIGHTS.length; col++) {
    for (let row = 0; row < COL_HEIGHTS[col]; row++) {
      const terrain = terrains[idx];
      const id = `h${idx}`;
      const number = terrain === 'desert' ? null : numbers[numIdx++];
      if (terrain === 'desert') robberHexId = id;
      hexes.push({ id, col, row, terrain, number });
      idx++;
    }
  }

  return { hexes, robberHexId, ports: PORTS };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/catan-board-preset.test.ts`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/catan/catan-board-preset.ts" "apps/web/src/components/features/session-live/flavors/catan/__tests__/catan-board-preset.test.ts"
git commit -m "feat(session-live): #3033 Catan L2 standard board preset generator"
```

---

## Task 3: Host-edit hook

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/use-catan-state-editor.ts`
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/use-catan-state-editor.test.tsx`

**Interfaces:**
- Consumes: `CatanGameState`, `CatanPiece`, `CATAN_PIECE_TOTALS`, `emptyCatanPlayerState`, `parseCatanGameState` from `./catan-state`; `generateStandardBoard` from `./catan-board-preset`; `useLiveSessionStore` from `@/lib/stores/live-session-store`; `useUpdateLiveGameState` from `@/hooks/mutations/useUpdateLiveGameState`; `useDebouncedCallback` from `@/lib/session-live/use-debounced-callback`.
- Produces: `useCatanStateEditor(sessionId: string, playerIds: readonly string[]): CatanStateEditor` where
  ```ts
  interface CatanStateEditor {
    state: CatanGameState | null;
    initializeState: () => void;
    regenerateBoard: () => void;
    setDiceRoll: (sum: number) => void;
    moveRobber: (hexId: string) => void;
    bumpBuilt: (playerId: string, piece: CatanPiece, delta: 1 | -1) => void;
    setDevCount: (playerId: string, delta: 1 | -1) => void;
    setHandSize: (playerId: string, delta: 1 | -1) => void;
    toggleBadge: (playerId: string, badge: 'longestRoad' | 'largestArmy') => void;
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// use-catan-state-editor.test.tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { useCatanStateEditor } from '../use-catan-state-editor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

const mutateMock = vi.fn<[unknown], void>();
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: mutateMock }),
}));

const SID = 'sess-1';

beforeEach(() => {
  mutateMock.mockReset();
  useLiveSessionStore.getState().reset();
});

function current() {
  return useLiveSessionStore.getState().gameState as import('../catan-state').CatanGameState | null;
}

describe('useCatanStateEditor', () => {
  it('initializeState seeds a board + zeroed players and writes the store optimistically', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1', 'p2']));
    act(() => result.current.initializeState());
    const s = current();
    expect(s?.game).toBe('catan');
    expect(s?.board.hexes).toHaveLength(19);
    expect(Object.keys(s?.players ?? {})).toEqual(['p1', 'p2']);
    expect(s?.players.p1?.handSize).toBe(0);
  });

  it('setDiceRoll sets last + prepends history', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.setDiceRoll(8));
    act(() => result.current.setDiceRoll(6));
    expect(current()?.dice.last).toBe(6);
    expect(current()?.dice.history).toEqual([6, 8]);
  });

  it('moveRobber updates robberHexId', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.moveRobber('h5'));
    expect(current()?.board.robberHexId).toBe('h5');
  });

  it('bumpBuilt clamps to [0, total]', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.bumpBuilt('p1', 'cities', -1));
    expect(current()?.players.p1?.built.cities).toBe(0); // clamp at 0
    for (let i = 0; i < 6; i++) act(() => result.current.bumpBuilt('p1', 'cities', 1));
    expect(current()?.players.p1?.built.cities).toBe(4); // clamp at total
  });

  it('toggleBadge is exclusive across players', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1', 'p2']));
    act(() => result.current.initializeState());
    act(() => result.current.toggleBadge('p1', 'longestRoad'));
    expect(current()?.players.p1?.badges.longestRoad).toBe(true);
    act(() => result.current.toggleBadge('p2', 'longestRoad'));
    expect(current()?.players.p1?.badges.longestRoad).toBe(false);
    expect(current()?.players.p2?.badges.longestRoad).toBe(true);
  });

  it('mutators are no-ops when state is null (host has not initialized)', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1']));
    act(() => result.current.setDiceRoll(8));
    expect(current()).toBeNull();
  });

  it('eventually PUTs the state (debounced) via the mutation', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => vi.advanceTimersByTime(600));
    expect(mutateMock).toHaveBeenCalled();
    const lastArg = mutateMock.mock.calls.at(-1)?.[0] as import('../catan-state').CatanGameState;
    expect(lastArg.game).toBe('catan');
    vi.useRealTimers();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/use-catan-state-editor.test.tsx`
Expected: FAIL — `Cannot find module '../use-catan-state-editor'`.

- [ ] **Step 3: Write the implementation**

```ts
// use-catan-state-editor.ts
'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import { generateStandardBoard } from './catan-board-preset';
import {
  CATAN_PIECE_TOTALS,
  CATAN_STATE_VERSION,
  emptyCatanPlayerState,
  parseCatanGameState,
  type CatanGameState,
  type CatanPiece,
  type CatanPlayerState,
} from './catan-state';

export interface CatanStateEditor {
  state: CatanGameState | null;
  initializeState: () => void;
  regenerateBoard: () => void;
  setDiceRoll: (sum: number) => void;
  moveRobber: (hexId: string) => void;
  bumpBuilt: (playerId: string, piece: CatanPiece, delta: 1 | -1) => void;
  setDevCount: (playerId: string, delta: 1 | -1) => void;
  setHandSize: (playerId: string, delta: 1 | -1) => void;
  toggleBadge: (playerId: string, badge: 'longestRoad' | 'largestArmy') => void;
}

const clampMin = (n: number, min: number) => (n < min ? min : n);
const clampRange = (n: number, min: number, max: number) => (n < min ? min : n > max ? max : n);

export function useCatanStateEditor(
  sessionId: string,
  playerIds: readonly string[]
): CatanStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parseCatanGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback((next: CatanGameState) => mutate(next), 500);

  // Flush any pending PUT on unmount so a fast edit is not lost.
  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: CatanGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  // Read the freshest parsed state at call time (avoids stale closures across rapid edits).
  const readState = useCallback(
    (): CatanGameState | null => parseCatanGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const editPlayer = useCallback(
    (playerId: string, fn: (p: CatanPlayerState) => CatanPlayerState) => {
      const cur = readState();
      if (cur == null) return;
      const prev = cur.players[playerId] ?? emptyCatanPlayerState();
      commit({ ...cur, players: { ...cur.players, [playerId]: fn(prev) } });
    },
    [commit, readState]
  );

  const initializeState = useCallback(() => {
    const board = generateStandardBoard();
    const players: Record<string, CatanPlayerState> = {};
    for (const id of playerIds) players[id] = emptyCatanPlayerState();
    commit({
      v: CATAN_STATE_VERSION,
      game: 'catan',
      board,
      dice: { last: null, history: [] },
      players,
    });
  }, [commit, playerIds]);

  const regenerateBoard = useCallback(() => {
    const cur = readState();
    if (cur == null) return;
    commit({ ...cur, board: generateStandardBoard() });
  }, [commit, readState]);

  const setDiceRoll = useCallback(
    (sum: number) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, dice: { last: sum, history: [sum, ...cur.dice.history].slice(0, 20) } });
    },
    [commit, readState]
  );

  const moveRobber = useCallback(
    (hexId: string) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, board: { ...cur.board, robberHexId: hexId } });
    },
    [commit, readState]
  );

  const bumpBuilt = useCallback(
    (playerId: string, piece: CatanPiece, delta: 1 | -1) =>
      editPlayer(playerId, p => ({
        ...p,
        built: { ...p.built, [piece]: clampRange(p.built[piece] + delta, 0, CATAN_PIECE_TOTALS[piece]) },
      })),
    [editPlayer]
  );

  const setDevCount = useCallback(
    (playerId: string, delta: 1 | -1) =>
      editPlayer(playerId, p => ({ ...p, devCount: clampMin(p.devCount + delta, 0) })),
    [editPlayer]
  );

  const setHandSize = useCallback(
    (playerId: string, delta: 1 | -1) =>
      editPlayer(playerId, p => ({ ...p, handSize: clampMin(p.handSize + delta, 0) })),
    [editPlayer]
  );

  const toggleBadge = useCallback(
    (playerId: string, badge: 'longestRoad' | 'largestArmy') => {
      const cur = readState();
      if (cur == null) return;
      const nextHolds = !(cur.players[playerId]?.badges[badge] ?? false);
      const players: Record<string, CatanPlayerState> = {};
      for (const [id, p] of Object.entries(cur.players)) {
        players[id] = { ...p, badges: { ...p.badges, [badge]: id === playerId ? nextHolds : false } };
      }
      if (cur.players[playerId] == null) {
        players[playerId] = { ...emptyCatanPlayerState(), badges: { longestRoad: false, largestArmy: false, [badge]: nextHolds } };
      }
      commit({ ...cur, players });
    },
    [commit, readState]
  );

  return {
    state,
    initializeState,
    regenerateBoard,
    setDiceRoll,
    moveRobber,
    bumpBuilt,
    setDevCount,
    setHandSize,
    toggleBadge,
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/use-catan-state-editor.test.tsx`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/catan/use-catan-state-editor.ts" "apps/web/src/components/features/session-live/flavors/catan/__tests__/use-catan-state-editor.test.tsx"
git commit -m "feat(session-live): #3033 Catan L2 host-edit hook (optimistic + debounced PUT)"
```

---

## Task 4: CatanHexBoard (SVG)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/CatanHexBoard.tsx`
- Modify: `apps/web/src/components/features/session-live/flavors/catan/catan-palette.ts` (add terrain colors)
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanHexBoard.test.tsx`

**Interfaces:**
- Consumes: `CatanGameState`, `CatanTerrain` from `./catan-state`; `catanTerrainColor` from `./catan-palette`.
- Produces: `CatanHexBoard` (default-ish named export) with props
  ```ts
  interface CatanHexBoardProps {
    board: CatanGameState['board'];
    editable: boolean;
    onMoveRobber?: (hexId: string) => void;
    hexAriaTemplate: string;   // "{terrain} {number}"
    robberLabel: string;       // "Ladro"
  }
  ```

- [ ] **Step 1: Add terrain colors to the palette**

In `catan-palette.ts`, append:

```ts
export const CATAN_TERRAIN_HSL: Record<CatanTerrain, string> = {
  wood: 'hsl(140, 40%, 40%)',
  brick: 'hsl(8, 55%, 52%)',
  sheep: 'hsl(80, 45%, 58%)',
  wheat: 'hsl(42, 80%, 57%)',
  ore: 'hsl(215, 12%, 58%)',
  desert: 'hsl(43, 42%, 70%)',
};

export function catanTerrainColor(terrain: CatanTerrain): string {
  return CATAN_TERRAIN_HSL[terrain];
}
```

Add the import at the top of `catan-palette.ts`: `import type { CatanTerrain } from './catan-state';`.

- [ ] **Step 2: Write the failing test**

```tsx
// CatanHexBoard.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CatanHexBoard } from '../CatanHexBoard';
import { generateStandardBoard } from '../catan-board-preset';

const board = generateStandardBoard();
const labels = { hexAriaTemplate: '{terrain} {number}', robberLabel: 'Ladro' };

describe('CatanHexBoard', () => {
  it('renders all 19 hex tiles', () => {
    const { container } = render(<CatanHexBoard board={board} editable={false} {...labels} />);
    expect(container.querySelectorAll('[data-slot="catan-hex"]')).toHaveLength(19);
  });

  it('marks the robber on the robber hex', () => {
    const { container } = render(<CatanHexBoard board={board} editable={false} {...labels} />);
    const robber = container.querySelector('[data-slot="catan-robber"]');
    expect(robber).not.toBeNull();
    expect(robber?.getAttribute('data-hex')).toBe(board.robberHexId);
  });

  it('read-only mode exposes no hex buttons', () => {
    render(<CatanHexBoard board={board} editable={false} {...labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host mode: clicking a hex fires onMoveRobber with its id', async () => {
    const onMoveRobber = vi.fn();
    const { container } = render(
      <CatanHexBoard board={board} editable onMoveRobber={onMoveRobber} {...labels} />
    );
    const firstHexButton = container.querySelector('[data-slot="catan-hex"] button, button[data-slot="catan-hex"]') as HTMLElement;
    await userEvent.click(firstHexButton);
    expect(onMoveRobber).toHaveBeenCalledOnce();
    expect(onMoveRobber.mock.calls[0][0]).toMatch(/^h\d+$/);
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/CatanHexBoard.test.tsx`
Expected: FAIL — `Cannot find module '../CatanHexBoard'`.

- [ ] **Step 4: Write the implementation**

```tsx
// CatanHexBoard.tsx
'use client';

import { type ReactElement } from 'react';

import { catanTerrainColor } from './catan-palette';
import type { CatanGameState, CatanHex } from './catan-state';

const R = 34; // hex circumradius (px)
const COL_STEP = 1.5 * R;
const ROW_STEP = Math.sqrt(3) * R;
const MAX_H = 5;
const PAD = 8;

/** Flat-top hex vertices centred at (cx, cy). */
function hexPoints(cx: number, cy: number): string {
  const pts: string[] = [];
  for (let i = 0; i < 6; i++) {
    const a = (Math.PI / 180) * (60 * i);
    pts.push(`${(cx + R * Math.cos(a)).toFixed(2)},${(cy + R * Math.sin(a)).toFixed(2)}`);
  }
  return pts.join(' ');
}

function center(hex: CatanHex, colHeight: number): { cx: number; cy: number } {
  const cx = PAD + R + hex.col * COL_STEP;
  const yOffset = ((MAX_H - colHeight) / 2 + hex.row) * ROW_STEP + ROW_STEP / 2;
  return { cx, cy: PAD + yOffset };
}

const HOT = new Set([6, 8]);

export interface CatanHexBoardProps {
  readonly board: CatanGameState['board'];
  readonly editable: boolean;
  readonly onMoveRobber?: (hexId: string) => void;
  readonly hexAriaTemplate: string; // "{terrain} {number}"
  readonly robberLabel: string;
}

export function CatanHexBoard({
  board,
  editable,
  onMoveRobber,
  hexAriaTemplate,
  robberLabel,
}: CatanHexBoardProps): ReactElement {
  const colHeights = [0, 1, 2, 3, 4].map(c => board.hexes.filter(h => h.col === c).length);
  const width = PAD * 2 + R + 4 * COL_STEP + R;
  const height = PAD * 2 + MAX_H * ROW_STEP;

  return (
    <svg
      data-slot="catan-board"
      viewBox={`0 0 ${width.toFixed(0)} ${height.toFixed(0)}`}
      className="h-auto w-full max-w-md"
      role="img"
      aria-label="Catan board"
    >
      {board.hexes.map(hex => {
        const { cx, cy } = center(hex, colHeights[hex.col] ?? MAX_H);
        const isRobber = hex.id === board.robberHexId;
        const aria = hexAriaTemplate
          .replace('{terrain}', hex.terrain)
          .replace('{number}', hex.number == null ? '' : String(hex.number))
          .trim();
        const tile = (
          <>
            <polygon
              points={hexPoints(cx, cy)}
              fill={catanTerrainColor(hex.terrain)}
              stroke="hsl(0,0%,100%)"
              strokeWidth={1.5}
            />
            {hex.number != null && (
              <text
                x={cx}
                y={cy + 4}
                textAnchor="middle"
                className={HOT.has(hex.number) ? 'catan-hot' : undefined}
                style={{ fontWeight: 800, fontSize: 15, fill: HOT.has(hex.number) ? 'hsl(0,72%,42%)' : 'hsl(0,0%,15%)' }}
              >
                {hex.number}
              </text>
            )}
            {isRobber && (
              <circle
                data-slot="catan-robber"
                data-hex={hex.id}
                cx={cx}
                cy={cy - 12}
                r={7}
                fill="hsl(0,0%,12%)"
                stroke="hsl(0,0%,100%)"
                strokeWidth={1.5}
              >
                <title>{robberLabel}</title>
              </circle>
            )}
          </>
        );

        if (editable) {
          return (
            <g
              key={hex.id}
              data-slot="catan-hex"
              data-hex={hex.id}
              role="button"
              tabIndex={0}
              aria-label={aria}
              style={{ cursor: 'pointer' }}
              onClick={() => onMoveRobber?.(hex.id)}
              onKeyDown={e => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  onMoveRobber?.(hex.id);
                }
              }}
            >
              {tile}
            </g>
          );
        }
        return (
          <g key={hex.id} data-slot="catan-hex" data-hex={hex.id} aria-label={aria}>
            {tile}
          </g>
        );
      })}
    </svg>
  );
}
```

> Note: in `editable` mode each hex `<g>` uses `role="button"`; the test's selector `button[data-slot="catan-hex"]` will not match a `<g>`, so update the test click target to `container.querySelector('[data-slot="catan-hex"]')` (the `<g role="button">`). Adjust the Step 2 test's host-mode selector to `container.querySelector('[data-slot="catan-hex"]') as HTMLElement` before running.

- [ ] **Step 5: Fix the host-mode test selector and run**

Edit the host-mode test to click `container.querySelector('[data-slot="catan-hex"]')`.

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/CatanHexBoard.test.tsx`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/catan/CatanHexBoard.tsx" "apps/web/src/components/features/session-live/flavors/catan/catan-palette.ts" "apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanHexBoard.test.tsx"
git commit -m "feat(session-live): #3033 Catan L3 SVG hex board + terrain palette"
```

---

## Task 5: CatanDiceControl

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/CatanDiceControl.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanDiceControl.test.tsx`

**Interfaces:**
- Consumes: `CatanGameState` from `./catan-state`.
- Produces: `CatanDiceControl` with props
  ```ts
  interface CatanDiceControlProps {
    dice: CatanGameState['dice'];
    editable: boolean;
    onRoll?: (sum: number) => void;
    lastLabel: string;    // "Ultimo tiro"
    historyLabel: string; // "Cronologia"
    rollAriaTemplate: string; // "Registra tiro {n}"
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// CatanDiceControl.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CatanDiceControl } from '../CatanDiceControl';

const labels = { lastLabel: 'Ultimo tiro', historyLabel: 'Cronologia', rollAriaTemplate: 'Registra tiro {n}' };

describe('CatanDiceControl', () => {
  it('shows the last roll', () => {
    render(<CatanDiceControl dice={{ last: 8, history: [8, 6] }} editable={false} {...labels} />);
    expect(screen.getByText('8')).toBeInTheDocument();
  });

  it('read-only mode has no roll buttons', () => {
    render(<CatanDiceControl dice={{ last: null, history: [] }} editable={false} {...labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host mode: tapping a value fires onRoll(sum)', async () => {
    const onRoll = vi.fn();
    render(<CatanDiceControl dice={{ last: null, history: [] }} editable onRoll={onRoll} {...labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Registra tiro 8' }));
    expect(onRoll).toHaveBeenCalledWith(8);
  });

  it('host mode renders quick-tap buttons 2..12', () => {
    render(<CatanDiceControl dice={{ last: null, history: [] }} editable onRoll={vi.fn()} {...labels} />);
    expect(screen.getAllByRole('button')).toHaveLength(11); // 2..12
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/CatanDiceControl.test.tsx`
Expected: FAIL — `Cannot find module '../CatanDiceControl'`.

- [ ] **Step 3: Write the implementation**

```tsx
// CatanDiceControl.tsx
'use client';

import { type ReactElement } from 'react';

import type { CatanGameState } from './catan-state';

const ROLLS = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

export interface CatanDiceControlProps {
  readonly dice: CatanGameState['dice'];
  readonly editable: boolean;
  readonly onRoll?: (sum: number) => void;
  readonly lastLabel: string;
  readonly historyLabel: string;
  readonly rollAriaTemplate: string;
}

export function CatanDiceControl({
  dice,
  editable,
  onRoll,
  lastLabel,
  historyLabel,
  rollAriaTemplate,
}: CatanDiceControlProps): ReactElement {
  return (
    <div data-slot="catan-dice" className="flex flex-col gap-2 rounded-lg border border-border bg-card p-3">
      <div className="flex items-center gap-2">
        <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {lastLabel}
        </span>
        <span data-slot="catan-dice-last" className="text-2xl font-bold tabular-nums text-foreground">
          {dice.last ?? '—'}
        </span>
      </div>

      {dice.history.length > 0 && (
        <div className="flex flex-wrap items-center gap-1">
          <span className="text-[10px] uppercase tracking-wide text-muted-foreground">{historyLabel}</span>
          {dice.history.slice(0, 12).map((n, i) => (
            <span
              key={`${i}-${n}`}
              className="rounded bg-muted px-1.5 py-0.5 text-[11px] tabular-nums text-muted-foreground"
            >
              {n}
            </span>
          ))}
        </div>
      )}

      {editable && (
        <div className="grid grid-cols-6 gap-1" data-slot="catan-dice-pad">
          {ROLLS.map(n => (
            <button
              key={n}
              type="button"
              aria-label={rollAriaTemplate.replace('{n}', String(n))}
              onClick={() => onRoll?.(n)}
              className="rounded-md border border-border bg-background py-1 text-sm font-semibold tabular-nums text-foreground hover:bg-muted"
            >
              {n}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/CatanDiceControl.test.tsx`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/catan/CatanDiceControl.tsx" "apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanDiceControl.test.tsx"
git commit -m "feat(session-live): #3033 Catan L3 dice control (host quick-tap 2-12)"
```

---

## Task 6: CatanPlayerCard

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/catan/CatanPlayerCard.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanPlayerCard.test.tsx`

**Interfaces:**
- Consumes: `CatanPlayerState`, `CatanPiece`, `CATAN_PIECE_TOTALS` from `./catan-state`; `catanPieceColor` from `./catan-palette`; `LiveSessionPlayerDto` from `@/lib/api/schemas/live-sessions.schemas`.
- Produces: `CatanPlayerCard` with props
  ```ts
  interface CatanPlayerCardProps {
    player: LiveSessionPlayerDto;
    state: CatanPlayerState;
    vp: number;
    editable: boolean;
    onBumpBuilt?: (piece: CatanPiece, delta: 1 | -1) => void;
    onSetDev?: (delta: 1 | -1) => void;
    onSetHand?: (delta: 1 | -1) => void;
    onToggleBadge?: (badge: 'longestRoad' | 'largestArmy') => void;
    labels: CatanPlayerCardLabels;
  }
  interface CatanPlayerCardLabels {
    vpLabel: string; handLabel: string; devLabel: string;
    settlementsLabel: string; citiesLabel: string; roadsLabel: string;
    longestRoadLabel: string; largestArmyLabel: string;
    incAriaTemplate: string; decAriaTemplate: string; // "{field} +1" / "{field} -1"
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// CatanPlayerCard.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CatanPlayerCard } from '../CatanPlayerCard';
import { emptyCatanPlayerState } from '../catan-state';

const player = { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 8, currentRank: 1, joinedAt: '', isActive: true } as const;
const labels = {
  vpLabel: 'PV', handLabel: 'Mano', devLabel: 'Sviluppo',
  settlementsLabel: 'Insediamenti', citiesLabel: 'Città', roadsLabel: 'Strade',
  longestRoadLabel: 'Strada+', largestArmyLabel: 'Armata+',
  incAriaTemplate: '{field} +1', decAriaTemplate: '{field} -1',
};

describe('CatanPlayerCard', () => {
  it('shows name, VP and hand size (read-only)', () => {
    render(<CatanPlayerCard player={player} state={{ ...emptyCatanPlayerState(), handSize: 7 }} vp={8} editable={false} labels={labels} />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument(); // VP
    expect(screen.getByText('7')).toBeInTheDocument(); // hand
  });

  it('read-only mode exposes no steppers', () => {
    render(<CatanPlayerCard player={player} state={emptyCatanPlayerState()} vp={0} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host mode: stepper fires onBumpBuilt', async () => {
    const onBumpBuilt = vi.fn();
    render(<CatanPlayerCard player={player} state={emptyCatanPlayerState()} vp={0} editable onBumpBuilt={onBumpBuilt} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Insediamenti +1' }));
    expect(onBumpBuilt).toHaveBeenCalledWith('settlements', 1);
  });

  it('host mode: badge toggle fires onToggleBadge', async () => {
    const onToggleBadge = vi.fn();
    render(<CatanPlayerCard player={player} state={emptyCatanPlayerState()} vp={0} editable onToggleBadge={onToggleBadge} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Strada+' }));
    expect(onToggleBadge).toHaveBeenCalledWith('longestRoad');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/CatanPlayerCard.test.tsx`
Expected: FAIL — `Cannot find module '../CatanPlayerCard'`.

- [ ] **Step 3: Write the implementation**

```tsx
// CatanPlayerCard.tsx
'use client';

import { type ReactElement } from 'react';

import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { catanPieceColor } from './catan-palette';
import { CATAN_PIECE_TOTALS, type CatanPiece, type CatanPlayerState } from './catan-state';

export interface CatanPlayerCardLabels {
  readonly vpLabel: string;
  readonly handLabel: string;
  readonly devLabel: string;
  readonly settlementsLabel: string;
  readonly citiesLabel: string;
  readonly roadsLabel: string;
  readonly longestRoadLabel: string;
  readonly largestArmyLabel: string;
  readonly incAriaTemplate: string; // "{field} +1"
  readonly decAriaTemplate: string; // "{field} -1"
}

export interface CatanPlayerCardProps {
  readonly player: LiveSessionPlayerDto;
  readonly state: CatanPlayerState;
  readonly vp: number;
  readonly editable: boolean;
  readonly onBumpBuilt?: (piece: CatanPiece, delta: 1 | -1) => void;
  readonly onSetDev?: (delta: 1 | -1) => void;
  readonly onSetHand?: (delta: 1 | -1) => void;
  readonly onToggleBadge?: (badge: 'longestRoad' | 'largestArmy') => void;
  readonly labels: CatanPlayerCardLabels;
}

function Stepper({
  label,
  value,
  editable,
  incAria,
  decAria,
  onDelta,
}: {
  label: string;
  value: string;
  editable: boolean;
  incAria: string;
  decAria: string;
  onDelta?: (delta: 1 | -1) => void;
}): ReactElement {
  return (
    <div className="flex items-center justify-between gap-1 text-xs">
      <span className="text-muted-foreground">{label}</span>
      <span className="flex items-center gap-1">
        {editable && (
          <button
            type="button"
            aria-label={decAria}
            onClick={() => onDelta?.(-1)}
            className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
          >
            −
          </button>
        )}
        <span className="min-w-4 text-center font-semibold tabular-nums text-foreground">{value}</span>
        {editable && (
          <button
            type="button"
            aria-label={incAria}
            onClick={() => onDelta?.(1)}
            className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
          >
            +
          </button>
        )}
      </span>
    </div>
  );
}

export function CatanPlayerCard({
  player,
  state,
  vp,
  editable,
  onBumpBuilt,
  onSetDev,
  onSetHand,
  onToggleBadge,
  labels,
}: CatanPlayerCardProps): ReactElement {
  const inc = (field: string) => labels.incAriaTemplate.replace('{field}', field);
  const dec = (field: string) => labels.decAriaTemplate.replace('{field}', field);
  const remaining = (piece: CatanPiece) => CATAN_PIECE_TOTALS[piece] - state.built[piece];

  const badgeBtn = (
    badge: 'longestRoad' | 'largestArmy',
    label: string,
    held: boolean
  ): ReactElement => {
    const cls = [
      'rounded px-1.5 py-0.5 text-[11px] font-semibold',
      held ? 'bg-entity-session/20 text-entity-session' : 'bg-muted text-muted-foreground',
    ].join(' ');
    return editable ? (
      <button type="button" aria-label={label} aria-pressed={held} onClick={() => onToggleBadge?.(badge)} className={cls}>
        {label}
      </button>
    ) : (
      <span className={cls} aria-hidden={!held}>
        {label}
      </span>
    );
  };

  return (
    <div
      data-slot="catan-player-card"
      data-active={player.isActive ? 'true' : 'false'}
      className={[
        'flex flex-col gap-1.5 rounded-lg border p-2',
        player.isActive ? 'border-entity-session/40 bg-entity-session/8' : 'border-border bg-card',
      ].join(' ')}
    >
      <div className="flex items-center gap-2">
        <span
          aria-hidden="true"
          className="h-3.5 w-3.5 shrink-0 rounded-full border border-border-strong"
          style={{ backgroundColor: catanPieceColor(player.color) }}
        />
        <span className="min-w-0 flex-1 truncate text-xs font-semibold text-foreground">{player.displayName}</span>
        <span className="text-[10px] uppercase text-muted-foreground">{labels.vpLabel}</span>
        <span className="text-sm font-bold tabular-nums text-foreground">{vp}</span>
      </div>

      <Stepper label={labels.handLabel} value={String(state.handSize)} editable={editable} incAria={inc(labels.handLabel)} decAria={dec(labels.handLabel)} onDelta={onSetHand} />
      <Stepper label={labels.settlementsLabel} value={`${state.built.settlements}/${remaining('settlements')}`} editable={editable} incAria={inc(labels.settlementsLabel)} decAria={dec(labels.settlementsLabel)} onDelta={d => onBumpBuilt?.('settlements', d)} />
      <Stepper label={labels.citiesLabel} value={`${state.built.cities}/${remaining('cities')}`} editable={editable} incAria={inc(labels.citiesLabel)} decAria={dec(labels.citiesLabel)} onDelta={d => onBumpBuilt?.('cities', d)} />
      <Stepper label={labels.roadsLabel} value={`${state.built.roads}/${remaining('roads')}`} editable={editable} incAria={inc(labels.roadsLabel)} decAria={dec(labels.roadsLabel)} onDelta={d => onBumpBuilt?.('roads', d)} />
      <Stepper label={labels.devLabel} value={String(state.devCount)} editable={editable} incAria={inc(labels.devLabel)} decAria={dec(labels.devLabel)} onDelta={onSetDev} />

      <div className="flex gap-1">
        {badgeBtn('longestRoad', labels.longestRoadLabel, state.badges.longestRoad)}
        {badgeBtn('largestArmy', labels.largestArmyLabel, state.badges.largestArmy)}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/CatanPlayerCard.test.tsx`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/catan/CatanPlayerCard.tsx" "apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanPlayerCard.test.tsx"
git commit -m "feat(session-live): #3033 Catan L3 player card (host steppers + badges)"
```

---

## Task 7: CatanLiveFlavor container (rewrite)

**Files:**
- Modify (rewrite): `apps/web/src/components/features/session-live/flavors/catan/CatanLiveFlavor.tsx`
- Modify (rewrite): `apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanLiveFlavor.test.tsx`

**Interfaces:**
- Consumes: `CatanHexBoard`, `CatanDiceControl`, `CatanPlayerCard` (+ their label types); `useCatanStateEditor`; `parseCatanGameState`, `emptyCatanPlayerState`; `useLiveSessionStore`; `ParticipantRole`, `hasRequiredRole` from `@/lib/session-live/participant-role`; `LiveSessionDto` from `@/lib/api/schemas/live-sessions.schemas`.
- Produces: `CatanLiveFlavor` + `CatanLiveFlavorLabels` + `CatanLiveFlavorProps` (props gain `viewerRole: ParticipantRole` and `sessionId: string`; keep existing `session`, `labels`, `className`, `livePoints`, `phaseName`). Extend `CatanLiveFlavorLabels` with the sub-component label groups + `initBoardCta` + `viewerWaiting`.

- [ ] **Step 1: Write the failing test (rewrite)**

```tsx
// CatanLiveFlavor.test.tsx  (replace the file)
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';

import { CatanLiveFlavor, type CatanLiveFlavorLabels } from '../CatanLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { generateStandardBoard } from '../catan-board-preset';
import { emptyCatanPlayerState } from '../catan-state';

expect.extend(toHaveNoViolations);

vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

const labels: CatanLiveFlavorLabels = {
  panelAriaLabel: 'Catan',
  roundTemplate: 'Round {n}',
  activePlayerTemplate: 'Turno di {name}',
  phaseTemplate: 'Fase: {name}',
  initBoardCta: 'Genera board Catan',
  viewerWaiting: 'In attesa dell’host',
  hexAriaTemplate: '{terrain} {number}',
  robberLabel: 'Ladro',
  diceLastLabel: 'Ultimo tiro',
  diceHistoryLabel: 'Cronologia',
  rollAriaTemplate: 'Registra tiro {n}',
  vpLabel: 'PV', handLabel: 'Mano', devLabel: 'Sviluppo',
  settlementsLabel: 'Insediamenti', citiesLabel: 'Città', roadsLabel: 'Strade',
  longestRoadLabel: 'Strada+', largestArmyLabel: 'Armata+',
  incAriaTemplate: '{field} +1', decAriaTemplate: '{field} -1',
};

const session = {
  id: 's1', sessionCode: 'ABC', gameId: null, gameName: 'Catan', gameSlug: 'catan',
  createdByUserId: 'u1', status: 'InProgress', visibility: 'Private', groupId: null,
  createdAt: '', startedAt: '', pausedAt: null, completedAt: null, updatedAt: '', lastSavedAt: null,
  currentTurnIndex: 0, currentTurnPlayerId: 'p1', agentMode: 'None', notes: null,
  players: [
    { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 8, currentRank: 1, joinedAt: '', isActive: true },
    { id: 'p2', userId: null, displayName: 'Anna', avatarUrl: null, color: 'Blue', role: 'Player', teamId: null, totalScore: 7, currentRank: 2, joinedAt: '', isActive: false },
  ],
  teams: [], roundScores: [], scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

beforeEach(() => useLiveSessionStore.getState().reset());

describe('CatanLiveFlavor', () => {
  it('empty state — host sees the "Genera board" CTA', () => {
    render(<CatanLiveFlavor session={session} labels={labels} viewerRole="Host" sessionId="s1" />);
    expect(screen.getByRole('button', { name: 'Genera board Catan' })).toBeInTheDocument();
  });

  it('empty state — non-host sees the waiting message, no CTA', () => {
    render(<CatanLiveFlavor session={session} labels={labels} viewerRole="Player" sessionId="s1" />);
    expect(screen.queryByRole('button', { name: 'Genera board Catan' })).toBeNull();
    expect(screen.getByText('In attesa dell’host')).toBeInTheDocument();
  });

  it('populated — renders board + dice + one card per player', () => {
    useLiveSessionStore.getState().setGameState({
      v: 1, game: 'catan', board: generateStandardBoard(),
      dice: { last: 8, history: [8] },
      players: { p1: emptyCatanPlayerState(), p2: emptyCatanPlayerState() },
    });
    const { container } = render(<CatanLiveFlavor session={session} labels={labels} viewerRole="Player" sessionId="s1" />);
    expect(container.querySelector('[data-slot="catan-board"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="catan-dice"]')).not.toBeNull();
    expect(container.querySelectorAll('[data-slot="catan-player-card"]')).toHaveLength(2);
  });

  it('has no axe violations in the populated host view', async () => {
    useLiveSessionStore.getState().setGameState({
      v: 1, game: 'catan', board: generateStandardBoard(),
      dice: { last: 8, history: [8] },
      players: { p1: emptyCatanPlayerState(), p2: emptyCatanPlayerState() },
    });
    const { container } = render(<CatanLiveFlavor session={session} labels={labels} viewerRole="Host" sessionId="s1" />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/CatanLiveFlavor.test.tsx`
Expected: FAIL — old props/exports don't match (`viewerRole`/`sessionId` unknown, `initBoardCta` missing).

- [ ] **Step 3: Write the implementation (rewrite the file)**

```tsx
// CatanLiveFlavor.tsx  (replace the file)
'use client';

import { type ReactElement } from 'react';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import { CatanDiceControl } from './CatanDiceControl';
import { CatanHexBoard } from './CatanHexBoard';
import { CatanPlayerCard, type CatanPlayerCardLabels } from './CatanPlayerCard';
import { emptyCatanPlayerState, parseCatanGameState } from './catan-state';
import { useCatanStateEditor } from './use-catan-state-editor';

export interface CatanLiveFlavorLabels extends CatanPlayerCardLabels {
  readonly panelAriaLabel: string;
  readonly roundTemplate: string; // "Round {n}"
  readonly activePlayerTemplate: string; // "Turno di {name}"
  readonly phaseTemplate: string; // "Fase: {name}"
  readonly initBoardCta: string;
  readonly viewerWaiting: string;
  readonly hexAriaTemplate: string; // "{terrain} {number}"
  readonly robberLabel: string;
  readonly diceLastLabel: string;
  readonly diceHistoryLabel: string;
  readonly rollAriaTemplate: string; // "Registra tiro {n}"
}

export interface CatanLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly labels: CatanLiveFlavorLabels;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

export function CatanLiveFlavor({
  session,
  labels,
  viewerRole,
  sessionId,
  className,
  livePoints,
  phaseName,
}: CatanLiveFlavorProps): ReactElement {
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const playerIds = session.players.map(p => p.id);
  const editor = useCatanStateEditor(sessionId, playerIds);
  const state = editor.state;

  const rawGameState = useLiveSessionStore(s => s.gameState);
  // Parse defensively; a non-catan gameState (or none) → empty view.
  const parsed = parseCatanGameState(rawGameState);

  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;

  // ── Empty state ────────────────────────────────────────────────────────────
  if (parsed == null) {
    return (
      <section
        aria-label={labels.panelAriaLabel}
        data-slot="catan-flavor-empty"
        className={`flex flex-col items-start gap-3 ${className ?? ''}`.trim()}
      >
        {isHost ? (
          <button
            type="button"
            onClick={editor.initializeState}
            className="rounded-lg border border-entity-session/40 bg-entity-session/10 px-3 py-2 text-sm font-semibold text-entity-session hover:bg-entity-session/20"
          >
            {labels.initBoardCta}
          </button>
        ) : (
          <p role="status" aria-live="polite" className="text-xs text-muted-foreground">
            {labels.viewerWaiting}
          </p>
        )}
      </section>
    );
  }

  const activePlayer = session.players.find(p => p.id === session.currentTurnPlayerId) ?? null;
  const subHeader = [
    activePlayer ? labels.activePlayerTemplate.replace('{name}', activePlayer.displayName) : null,
    phaseName ? labels.phaseTemplate.replace('{name}', phaseName) : null,
  ].filter((s): s is string => s != null);

  return (
    <section
      aria-label={labels.panelAriaLabel}
      data-slot="catan-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      <header data-slot="catan-flavor-turn" aria-live="polite" className="flex flex-col gap-0.5">
        <span className="text-sm font-semibold text-foreground">
          {labels.roundTemplate.replace('{n}', String(session.currentTurnIndex + 1))}
        </span>
        {subHeader.length > 0 && <span className="text-xs text-muted-foreground">{subHeader.join(' · ')}</span>}
      </header>

      <CatanHexBoard
        board={parsed.board}
        editable={isHost}
        onMoveRobber={editor.moveRobber}
        hexAriaTemplate={labels.hexAriaTemplate}
        robberLabel={labels.robberLabel}
      />

      <CatanDiceControl
        dice={parsed.dice}
        editable={isHost}
        onRoll={editor.setDiceRoll}
        lastLabel={labels.diceLastLabel}
        historyLabel={labels.diceHistoryLabel}
        rollAriaTemplate={labels.rollAriaTemplate}
      />

      <div data-slot="catan-flavor-players" className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        {session.players.map(player => (
          <CatanPlayerCard
            key={player.id}
            player={player}
            state={parsed.players[player.id] ?? emptyCatanPlayerState()}
            vp={scoreOf(player.id)}
            editable={isHost}
            onBumpBuilt={(piece, delta) => editor.bumpBuilt(player.id, piece, delta)}
            onSetDev={delta => editor.setDevCount(player.id, delta)}
            onSetHand={delta => editor.setHandSize(player.id, delta)}
            onToggleBadge={badge => editor.toggleBadge(player.id, badge)}
            labels={labels}
          />
        ))}
      </div>

      {isHost && (
        <button
          type="button"
          onClick={editor.regenerateBoard}
          className="self-start text-xs text-muted-foreground underline hover:text-foreground"
        >
          {labels.initBoardCta}
        </button>
      )}
    </section>
  );
}
```

> Note: `state` from the editor and `parsed` from the store selector are the same value; the component uses `parsed` for rendering (subscribes to store updates) and the editor for mutators. This is intentional — the editor's `state` field is available for consumers that only need reads.

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/catan/__tests__/CatanLiveFlavor.test.tsx`
Expected: PASS (4 tests, incl. axe).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/catan/CatanLiveFlavor.tsx" "apps/web/src/components/features/session-live/flavors/catan/__tests__/CatanLiveFlavor.test.tsx"
git commit -m "feat(session-live): #3033 Catan L3 flavor container rewrite (board+dice+cards)"
```

---

## Task 8: Wire the flavor into the shell (FlavorRenderer + SessionLiveView + i18n)

**Files:**
- Modify: `apps/web/src/components/features/session-live/FlavorRenderer.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Test: `apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx` (create if absent)

**Interfaces:**
- Consumes: `CatanLiveFlavorLabels`/`CatanLiveFlavorProps` (now with `viewerRole`, `sessionId`) from Task 7.
- Produces: `FlavorRendererProps` gains `viewerRole: ParticipantRole` + `sessionId: string`, forwarded to the lazy flavor.

- [ ] **Step 1: Write the failing FlavorRenderer test**

```tsx
// FlavorRenderer.test.tsx
import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';

import { hasFlavor } from '../FlavorRenderer';

describe('FlavorRenderer', () => {
  it('hasFlavor is true for catan, false otherwise', () => {
    expect(hasFlavor('catan')).toBe(true);
    expect(hasFlavor('wingspan')).toBe(false);
    expect(hasFlavor(null)).toBe(false);
    expect(hasFlavor(undefined)).toBe(false);
  });
});
```

Run: `pnpm exec vitest run src/components/features/session-live/__tests__/FlavorRenderer.test.tsx`
Expected: PASS already (hasFlavor unchanged) — this is a guard test to keep green through the prop change.

- [ ] **Step 2: Thread `viewerRole` + `sessionId` through FlavorRenderer**

In `FlavorRenderer.tsx`, import the type and extend props + forward:

```tsx
import type { ParticipantRole } from '@/lib/session-live/participant-role';
```

Add to `FlavorRendererProps`:
```tsx
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
```

Destructure `viewerRole, sessionId` in the component signature and pass them to `<LazyFlavor ... viewerRole={viewerRole} sessionId={sessionId} />`.

- [ ] **Step 3: Pass the new props at both SessionLiveView render sites**

In `SessionLiveView.tsx`, both `<FlavorRenderer ...>` blocks (mobile ~L1389, desktop ~L1613) add:

```tsx
            viewerRole={activeSession.viewerRole}
            sessionId={liveSessionDto.id}
```

(Both blocks are already guarded by `liveSessionDto != null`, so `liveSessionDto.id` is safe.)

- [ ] **Step 4: Extend the `catanFlavorLabels` memo**

Replace the `catanFlavorLabels` `useMemo` body (SessionLiveView ~L1134) so it returns the full `CatanLiveFlavorLabels` shape. Keep the existing keys and add:

```tsx
      initBoardCta: t('pages.sessionLive.flavor.catan.initBoardCta'),
      viewerWaiting: t('pages.sessionLive.flavor.catan.viewerWaiting'),
      hexAriaTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.hexAriaTemplate'] as string) ?? '{terrain} {number}',
      robberLabel: t('pages.sessionLive.flavor.catan.robberLabel'),
      diceLastLabel: t('pages.sessionLive.flavor.catan.diceLastLabel'),
      diceHistoryLabel: t('pages.sessionLive.flavor.catan.diceHistoryLabel'),
      rollAriaTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.rollAriaTemplate'] as string) ?? 'Registra tiro {n}',
      vpLabel: t('pages.sessionLive.flavor.catan.vpLabel'),
      handLabel: t('pages.sessionLive.flavor.catan.handLabel'),
      devLabel: t('pages.sessionLive.flavor.catan.devLabel'),
      settlementsLabel: t('pages.sessionLive.flavor.catan.settlementsLabel'),
      citiesLabel: t('pages.sessionLive.flavor.catan.citiesLabel'),
      roadsLabel: t('pages.sessionLive.flavor.catan.roadsLabel'),
      longestRoadLabel: t('pages.sessionLive.flavor.catan.longestRoadLabel'),
      largestArmyLabel: t('pages.sessionLive.flavor.catan.largestArmyLabel'),
      incAriaTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.incAriaTemplate'] as string) ?? '{field} +1',
      decAriaTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.decAriaTemplate'] as string) ?? '{field} -1',
```

The old keys (`leaderboardHeading`, `leaderBadgeLabel`, `scoreAriaTemplate`, `dimensionsHeading`, `emptyLabel`) are no longer on `CatanLiveFlavorLabels` — remove them from the memo. TypeScript will error on any leftover; delete those five lines.

- [ ] **Step 5: Add the i18n keys**

In `src/locales/it.json`, under `pages.sessionLive.flavor.catan` (keep `panelAriaLabel`, `roundTemplate`, `activePlayerTemplate`, `phaseTemplate`), set:

```json
"initBoardCta": "Genera board Catan",
"viewerWaiting": "In attesa dell'host…",
"hexAriaTemplate": "{terrain} {number}",
"robberLabel": "Ladro",
"diceLastLabel": "Ultimo tiro",
"diceHistoryLabel": "Cronologia",
"rollAriaTemplate": "Registra tiro {n}",
"vpLabel": "PV",
"handLabel": "Mano",
"devLabel": "Sviluppo",
"settlementsLabel": "Insediamenti",
"citiesLabel": "Città",
"roadsLabel": "Strade",
"longestRoadLabel": "Strada+",
"largestArmyLabel": "Armata+",
"incAriaTemplate": "{field} +1",
"decAriaTemplate": "{field} −1"
```

Remove the now-unused `leaderboardHeading`, `leaderBadgeLabel`, `scoreAriaTemplate`, `dimensionsHeading`, `emptyLabel` keys under `catan`. Mirror the same key set in `src/locales/en.json` with English copy (`"initBoardCta": "Generate Catan board"`, `"viewerWaiting": "Waiting for the host…"`, `"robberLabel": "Robber"`, `"diceLastLabel": "Last roll"`, `"diceHistoryLabel": "History"`, `"rollAriaTemplate": "Record roll {n}"`, `"vpLabel": "VP"`, `"handLabel": "Hand"`, `"devLabel": "Dev"`, `"settlementsLabel": "Settlements"`, `"citiesLabel": "Cities"`, `"roadsLabel": "Roads"`, `"longestRoadLabel": "Long. Road"`, `"largestArmyLabel": "Big Army"`, `hexAriaTemplate`/`incAriaTemplate`/`decAriaTemplate` identical).

- [ ] **Step 6: Typecheck + run the affected suites**

```bash
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run \
  "src/components/features/session-live/__tests__/FlavorRenderer.test.tsx" \
  "src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx"
```
Expected: typecheck clean; both suites PASS. If `SessionLiveView.test.tsx` references removed label keys, update those references to the new keys.

- [ ] **Step 7: Commit**

```bash
git add "apps/web/src/components/features/session-live/FlavorRenderer.tsx" "apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx" "apps/web/src/locales/it.json" "apps/web/src/locales/en.json" "apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx"
git commit -m "feat(session-live): #3033 wire Catan rich flavor (viewerRole+sessionId+i18n)"
```

---

## Task 9: E2E regression guard

**Files:**
- Create: `apps/web/e2e/catan-flavor-l2-l3.spec.ts`

**Interfaces:**
- Consumes: nothing new; mirrors the existing #2787 flavor E2E (regression-guard + `test.fixme()` positive path).

- [ ] **Step 1: Write the E2E spec**

```ts
// catan-flavor-l2-l3.spec.ts
import { test, expect } from '@playwright/test';

// The Catan flavor tab is gated on liveSessionDto (sessionQuery.data), which is
// null in ?fixture=host (the LiveSessionFixture is minimal, no gameSlug). So the
// positive path can only be exercised against a real Catan session — deferred
// (same known limitation as the SSE smoke test, #3033 spec § Testing).
test.describe('Catan rich flavor (#3033)', () => {
  test('regression guard: a non-Catan fixture session shows no flavor tab', async ({ page }) => {
    await page.goto('/sessions/demo/live?fixture=host');
    await expect(page.getByRole('tab', { name: /catan|flavor/i })).toHaveCount(0);
  });

  test.fixme('host can generate the board and record a dice roll (needs a real Catan session)', async () => {
    // Requires a seeded Catan live session over HTTP; not reachable in fixture mode.
  });
});
```

- [ ] **Step 2: Run the guard (headless)**

Run: `pnpm exec playwright test e2e/catan-flavor-l2-l3.spec.ts --project=chromium`
Expected: 1 passed, 1 skipped (`fixme`). If the local Playwright env is not set up, skip execution and rely on CI; the file compiles under `pnpm typecheck`.

- [ ] **Step 3: Commit**

```bash
git add "apps/web/e2e/catan-flavor-l2-l3.spec.ts"
git commit -m "test(session-live): #3033 Catan flavor E2E regression guard"
```

---

## Task 10: Final verification

- [ ] **Step 1: Full typecheck + all Catan-flavor suites**

```bash
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run src/components/features/session-live/flavors/catan
```
Expected: typecheck clean; all catan-flavor tests PASS.

- [ ] **Step 2: Lint the touched files**

```bash
pnpm exec eslint --max-warnings=0 "src/components/features/session-live/flavors/catan/**/*.{ts,tsx}" "src/components/features/session-live/FlavorRenderer.tsx"
```
Expected: no errors (in particular `local/no-hardcoded-color-utility` — terrain/piece colors are inline `hsl()` via the palette module, not Tailwind utilities).

- [ ] **Step 3: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-3033-catan-l2-l3
gh pr create --base main-dev --head feature/issue-3033-catan-l2-l3 \
  --title "feat(session-live): #3033 Catan L2+L3 rich flavor (pilot)" \
  --body "Implements the Catan pilot of epic #3025 per docs/superpowers/specs/2026-07-16-catan-l2-l3-rich-flavor-design.md. FE-only (L1 covers write/persist/stream/expose). Closes #3033."
```

---

## Self-Review

**1. Spec coverage:**
- L2 schema + parser → Task 1. ✅
- Board preset → Task 2. ✅
- Host-edit (optimistic + debounced, exclusive badges, clamps, initialize/regenerate) → Task 3. ✅
- HexBoard / DiceControl / PlayerCard → Tasks 4/5/6. ✅
- Container rewrite + empty state (host CTA / viewer waiting) + VP-from-scoring → Task 7. ✅
- Wiring (FlavorRenderer + SessionLiveView + i18n) → Task 8. ✅
- Error handling (parse null → empty) → Tasks 1 + 7. ✅
- Testing (unit + component + jest-axe + E2E guard) → Tasks 1–9. ✅
- No backend changes → whole plan is FE-only. ✅

**2. Placeholder scan:** No TBD/TODO/"implement later"; every code step has complete code. ✅

**3. Type consistency:** `CatanGameState`, `CatanPlayerState`, `CatanPiece`, `CATAN_PIECE_TOTALS`, `parseCatanGameState`, `emptyCatanPlayerState`, `generateStandardBoard`, `useCatanStateEditor` signatures, and the three component prop/label interfaces are used identically across Tasks 1→8. `CatanLiveFlavorLabels extends CatanPlayerCardLabels` so the container's label object satisfies the card. `catanTerrainColor`/`catanPieceColor` both from the palette module. ✅

**Known follow-ups (out of scope, documented in the spec):** port glyph rendering polish; per-resource hands; shared bank; trade/dev-deck panels; a real-Catan-session E2E positive path.
