# Puerto Rico L2+L3 Flavor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Puerto Rico live flavor — coarse per-player counters (doubloons/colonists/5-good storehouse/plantation-quarry-building counts) + 3 shared pools (galleons, trading house, colonist ship) — on the L1 layer, reusing the generalized flavor plumbing.

**Architecture:** FE-only. Reuses the game-agnostic pattern (`flavors/<game>/`; `FlavorRenderer` dispatches on `FLAVOR_MAP` with `FlavorProps`; each flavor self-builds i18n labels). Counters + pools live in `gameState`; VP stays in the existing scoring system. Continuous ± adjustments PUT debounced (500 ms). The 8-role board + tile-tableau layout are CUT.

**Tech Stack:** Next.js 16 · React 19 · TypeScript · Zod · Zustand (`useLiveSessionStore`) · TanStack Query (`useUpdateLiveGameState`) · `useDebouncedCallback` · react-intl (`useIntl`) + `@/hooks/useTranslation` · Vitest + Testing Library + jest-axe · Tailwind semantic tokens (goods colours inline `hsl()` via a palette module).

## Global Constraints

- **Issue:** #2792 (G6f, epic #3025). Spec: `docs/superpowers/specs/2026-07-17-puerto-rico-l2-l3-flavor-design.md`.
- **Zero backend changes.** Scores use the existing scoring editor; `gameState` is the opaque L1 blob.
- **State schema:** `v: 1`, `game: 'puerto-rico'` (note the hyphen — the exact `gameSlug`). `parsePuertoRicoGameState` returns `null` (never throws) on wrong game/version/shape.
- **`gameState` shape:** `{ v, game, players: Record<id, {doubloons, colonists, storehouse:{corn,indigo,sugar,tobacco,coffee}, plantations, quarries, buildings}>, galleons: {good,loaded,cap}[], tradingHouse:{slots:(good|null)[4]}, colonistShip:{onShip,supply} }`. Never scores.
- **`storehouse` is a fixed `z.object` with all 5 goods required** (not `z.record`). The 5 goods: `corn, indigo, sugar, tobacco, coffee`.
- **Galleon caps** by player count: `[n+1, n+2, n+3]` (n = players).
- **Continuous ± edits → DEBOUNCED PUT (500 ms)** + optimistic `setGameState` first + flush on unmount (like Catan/Wingspan). Clamps: all counters ≥0; galleon `loaded` in `[0, cap]`; `setGalleonGood` resets that ship's `loaded` to 0.
- **VP stays in scoring** (`livePoints`/`totalScore`); `gameState` never carries scores. **Leaderboard renders ungated**; only the panels gate on gameState (host CTA when null). **Host-edit only** (`viewerRole === 'Host'`).
- **Flavors self-build i18n labels** via `useIntl` + `useTranslation`; templates via `intl.messages[id] as string ?? fallback`, static via `t(id)`.
- **Colours:** semantic Tailwind tokens EXCEPT the 5 goods colours → inline `hsl()` via a palette module. `text-white` only on cells that ALSO set an inline coloured `backgroundColor` (put the white in inline `style.color`, do NOT use the `text-white` utility — the `.e-bg` eslint exemption is className-only).
- **LINT GATE (critical — implementers + the pre-commit hook miss it):** after each component/palette task, run `pnpm exec eslint --max-warnings=0 <file>`. The pre-commit hook does NOT run `meepleai/no-inline-hsl-v2` nor the `style`-prop case of `local/no-hardcoded-color-utility`. Inline `hsl()` that trips `no-inline-hsl-v2` gets a line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- <reason>` (mirroring `catan-palette`).
- **Tests:** Vitest, TDD, output pristine. Query via `data-slot`/roles, not `getByTestId`. Files under `apps/web/src/components/features/session-live/flavors/puerto-rico/`. Run from `apps/web`.
- **Windows:** pre-commit runs `pnpm typecheck` (~2 min, occasionally slower) — allow ≥9 min for commits; if TS2307 on stale `.next/types`, `rm -rf .next/types` first (never `--no-verify`).

## File Structure

Create under `flavors/puerto-rico/`: `puerto-rico-state.ts` (schema + helpers), `puerto-rico-palette.ts` (goods colours), `use-puerto-rico-state-editor.ts` (debounced mutators), `PuertoRicoPlayerMatSummary.tsx`, `PuertoRicoGalleonsPanel.tsx`, `PuertoRicoTradingHousePanel.tsx`, `PuertoRicoColonistShipPanel.tsx`, `PuertoRicoLiveFlavor.tsx`, `__tests__/*`.
Modify: `session-live/FlavorRenderer.tsx` (one `FLAVOR_MAP` entry), `src/locales/it.json` + `en.json` (`flavor.puerto-rico.*`).

---

## Task 1: L2 state schema + helpers

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/puerto-rico/puerto-rico-state.ts`
- Test: `apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/puerto-rico-state.test.ts`

**Interfaces:**
- Produces: `PuertoRicoGameState`, `PuertoRicoPlayerState`, `PuertoRicoGalleon`, `PuertoRicoGood` types; `parsePuertoRicoGameState(raw): PuertoRicoGameState | null`; `emptyPuertoRicoPlayerState(): PuertoRicoPlayerState`; `initialPuertoRicoState(playerIds: readonly string[]): PuertoRicoGameState`; `PUERTO_RICO_GOODS` (readonly 5-tuple); `PUERTO_RICO_STATE_VERSION = 1`.

- [ ] **Step 1: Write the failing test**

```ts
// puerto-rico-state.test.ts
import { describe, expect, it } from 'vitest';

import {
  PUERTO_RICO_GOODS,
  emptyPuertoRicoPlayerState,
  initialPuertoRicoState,
  parsePuertoRicoGameState,
} from '../puerto-rico-state';

const PLAYER = {
  doubloons: 3, colonists: 2,
  storehouse: { corn: 1, indigo: 0, sugar: 2, tobacco: 0, coffee: 1 },
  plantations: 4, quarries: 1, buildings: 3,
};
const VALID = {
  v: 1, game: 'puerto-rico',
  players: { p1: PLAYER },
  galleons: [{ good: 'corn', loaded: 2, cap: 5 }, { good: null, loaded: 0, cap: 6 }, { good: null, loaded: 0, cap: 7 }],
  tradingHouse: { slots: ['indigo', null, null, null] },
  colonistShip: { onShip: 3, supply: 20 },
};

describe('parsePuertoRicoGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parsePuertoRicoGameState(VALID);
    expect(parsed?.players.p1?.doubloons).toBe(3);
    expect(parsed?.galleons).toHaveLength(3);
  });
  it('returns null for a different game', () => {
    expect(parsePuertoRicoGameState({ ...VALID, game: 'catan' })).toBeNull();
  });
  it('returns null for a future version', () => {
    expect(parsePuertoRicoGameState({ ...VALID, v: 2 })).toBeNull();
  });
  it('returns null when tradingHouse has != 4 slots', () => {
    expect(parsePuertoRicoGameState({ ...VALID, tradingHouse: { slots: ['corn', null] } })).toBeNull();
  });
  it('returns null when a storehouse good is missing', () => {
    const bad = { ...PLAYER, storehouse: { corn: 1, indigo: 0, sugar: 2, tobacco: 0 } };
    expect(parsePuertoRicoGameState({ ...VALID, players: { p1: bad } })).toBeNull();
  });
  it('returns null for malformed / non-object', () => {
    expect(parsePuertoRicoGameState(null)).toBeNull();
    expect(parsePuertoRicoGameState('x')).toBeNull();
  });
});

describe('emptyPuertoRicoPlayerState', () => {
  it('is fully zeroed with all 5 goods', () => {
    expect(emptyPuertoRicoPlayerState()).toEqual({
      doubloons: 0, colonists: 0,
      storehouse: { corn: 0, indigo: 0, sugar: 0, tobacco: 0, coffee: 0 },
      plantations: 0, quarries: 0, buildings: 0,
    });
  });
});

describe('initialPuertoRicoState', () => {
  it('seeds a zeroed player per id + galleon caps [n+1, n+2, n+3]', () => {
    const s = initialPuertoRicoState(['p1', 'p2', 'p3']); // n = 3
    expect(Object.keys(s.players)).toEqual(['p1', 'p2', 'p3']);
    expect(s.players.p1?.doubloons).toBe(0);
    expect(s.galleons.map(g => g.cap)).toEqual([4, 5, 6]);
    expect(s.tradingHouse.slots).toEqual([null, null, null, null]);
    expect(s.colonistShip).toEqual({ onShip: 0, supply: 0 });
  });
  it('scales galleon caps with player count', () => {
    expect(initialPuertoRicoState(['a', 'b', 'c', 'd', 'e']).galleons.map(g => g.cap)).toEqual([6, 7, 8]);
  });
});

describe('constants', () => {
  it('has the 5 canonical goods in order', () => {
    expect(PUERTO_RICO_GOODS).toEqual(['corn', 'indigo', 'sugar', 'tobacco', 'coffee']);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/puerto-rico-state.test.ts`
Expected: FAIL — `Cannot find module '../puerto-rico-state'`.

- [ ] **Step 3: Write the implementation**

```ts
// puerto-rico-state.ts
import { z } from 'zod';

export const PUERTO_RICO_STATE_VERSION = 1;
export const PUERTO_RICO_GOODS = ['corn', 'indigo', 'sugar', 'tobacco', 'coffee'] as const;

export const PuertoRicoGoodSchema = z.enum(PUERTO_RICO_GOODS);
export type PuertoRicoGood = z.infer<typeof PuertoRicoGoodSchema>;

const nn = () => z.number().int().min(0);

export const PuertoRicoStorehouseSchema = z.object({
  corn: nn(), indigo: nn(), sugar: nn(), tobacco: nn(), coffee: nn(),
});

export const PuertoRicoPlayerStateSchema = z.object({
  doubloons: nn(), colonists: nn(),
  storehouse: PuertoRicoStorehouseSchema,
  plantations: nn(), quarries: nn(), buildings: nn(),
});
export type PuertoRicoPlayerState = z.infer<typeof PuertoRicoPlayerStateSchema>;

export const PuertoRicoGalleonSchema = z.object({
  good: PuertoRicoGoodSchema.nullable(),
  loaded: nn(),
  cap: nn(),
});
export type PuertoRicoGalleon = z.infer<typeof PuertoRicoGalleonSchema>;

export const PuertoRicoGameStateSchema = z.object({
  v: z.literal(PUERTO_RICO_STATE_VERSION),
  game: z.literal('puerto-rico'),
  players: z.record(z.string(), PuertoRicoPlayerStateSchema),
  galleons: z.array(PuertoRicoGalleonSchema),
  tradingHouse: z.object({ slots: z.array(PuertoRicoGoodSchema.nullable()).length(4) }),
  colonistShip: z.object({ onShip: nn(), supply: nn() }),
});
export type PuertoRicoGameState = z.infer<typeof PuertoRicoGameStateSchema>;

export function parsePuertoRicoGameState(raw: unknown): PuertoRicoGameState | null {
  const result = PuertoRicoGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function emptyPuertoRicoPlayerState(): PuertoRicoPlayerState {
  return {
    doubloons: 0, colonists: 0,
    storehouse: { corn: 0, indigo: 0, sugar: 0, tobacco: 0, coffee: 0 },
    plantations: 0, quarries: 0, buildings: 0,
  };
}

export function initialPuertoRicoState(playerIds: readonly string[]): PuertoRicoGameState {
  const n = playerIds.length;
  const players: Record<string, PuertoRicoPlayerState> = {};
  for (const id of playerIds) players[id] = emptyPuertoRicoPlayerState();
  return {
    v: PUERTO_RICO_STATE_VERSION,
    game: 'puerto-rico',
    players,
    galleons: [
      { good: null, loaded: 0, cap: n + 1 },
      { good: null, loaded: 0, cap: n + 2 },
      { good: null, loaded: 0, cap: n + 3 },
    ],
    tradingHouse: { slots: [null, null, null, null] },
    colonistShip: { onShip: 0, supply: 0 },
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/puerto-rico-state.test.ts`
Expected: PASS (11 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/puerto-rico/puerto-rico-state.ts" "apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/puerto-rico-state.test.ts"
git commit -m "feat(session-live): #2792 Puerto Rico L2 state schema + helpers"
```

---

## Task 2: Host-edit hook (debounced)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/puerto-rico/use-puerto-rico-state-editor.ts`
- Test: `apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/use-puerto-rico-state-editor.test.tsx`

**Interfaces:**
- Consumes: `PuertoRicoGameState`, `PuertoRicoGood`, `parsePuertoRicoGameState`, `initialPuertoRicoState`, `emptyPuertoRicoPlayerState` from `./puerto-rico-state`; `useLiveSessionStore`, `useUpdateLiveGameState`, `useDebouncedCallback`.
- Produces: `usePuertoRicoStateEditor(sessionId: string, playerIds: readonly string[]): PuertoRicoStateEditor` where
  ```ts
  type PlayerCounter = 'doubloons' | 'colonists' | 'plantations' | 'quarries' | 'buildings';
  interface PuertoRicoStateEditor {
    state: PuertoRicoGameState | null;
    initializeState: () => void;
    bumpPlayerCounter: (playerId: string, field: PlayerCounter, delta: 1 | -1) => void;
    bumpPlayerGood: (playerId: string, good: PuertoRicoGood, delta: 1 | -1) => void;
    setGalleonGood: (index: number, good: PuertoRicoGood | null) => void;
    bumpGalleonLoaded: (index: number, delta: 1 | -1) => void;
    setTradingSlot: (index: number, good: PuertoRicoGood | null) => void;
    bumpColonistShip: (field: 'onShip' | 'supply', delta: 1 | -1) => void;
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// use-puerto-rico-state-editor.test.tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { usePuertoRicoStateEditor } from '../use-puerto-rico-state-editor';
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
  return useLiveSessionStore.getState().gameState as import('../puerto-rico-state').PuertoRicoGameState | null;
}

describe('usePuertoRicoStateEditor', () => {
  it('initializeState seeds players + 3 galleons', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1', 'p2']));
    act(() => result.current.initializeState());
    expect(Object.keys(current()?.players ?? {})).toEqual(['p1', 'p2']);
    expect(current()?.galleons).toHaveLength(3);
  });

  it('bumpPlayerCounter clamps at 0', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.bumpPlayerCounter('p1', 'doubloons', -1));
    expect(current()?.players.p1?.doubloons).toBe(0);
    act(() => result.current.bumpPlayerCounter('p1', 'doubloons', 1));
    expect(current()?.players.p1?.doubloons).toBe(1);
  });

  it('bumpPlayerGood clamps at 0 and targets the right good', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.bumpPlayerGood('p1', 'sugar', 1));
    expect(current()?.players.p1?.storehouse.sugar).toBe(1);
    expect(current()?.players.p1?.storehouse.corn).toBe(0);
  });

  it('setGalleonGood resets that ship loaded to 0; bumpGalleonLoaded caps at cap', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1', 'p2'])); // caps [3,4,5]
    act(() => result.current.initializeState());
    act(() => result.current.bumpGalleonLoaded(0, 1));
    act(() => result.current.setGalleonGood(0, 'corn'));
    expect(current()?.galleons[0]).toEqual({ good: 'corn', loaded: 0, cap: 3 });
    for (let i = 0; i < 9; i++) act(() => result.current.bumpGalleonLoaded(0, 1));
    expect(current()?.galleons[0]?.loaded).toBe(3); // capped at cap
  });

  it('setTradingSlot writes the good at the slot', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.setTradingSlot(2, 'coffee'));
    expect(current()?.tradingHouse.slots).toEqual([null, null, 'coffee', null]);
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1']));
    act(() => result.current.bumpPlayerCounter('p1', 'doubloons', 1));
    expect(current()).toBeNull();
  });

  it('eventually PUTs (debounced)', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => vi.advanceTimersByTime(600));
    expect(mutateMock).toHaveBeenCalled();
    vi.useRealTimers();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/use-puerto-rico-state-editor.test.tsx`
Expected: FAIL — `Cannot find module '../use-puerto-rico-state-editor'`.

- [ ] **Step 3: Write the implementation**

```ts
// use-puerto-rico-state-editor.ts
'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import {
  emptyPuertoRicoPlayerState,
  initialPuertoRicoState,
  parsePuertoRicoGameState,
  type PuertoRicoGameState,
  type PuertoRicoGood,
  type PuertoRicoPlayerState,
} from './puerto-rico-state';

type PlayerCounter = 'doubloons' | 'colonists' | 'plantations' | 'quarries' | 'buildings';

export interface PuertoRicoStateEditor {
  state: PuertoRicoGameState | null;
  initializeState: () => void;
  bumpPlayerCounter: (playerId: string, field: PlayerCounter, delta: 1 | -1) => void;
  bumpPlayerGood: (playerId: string, good: PuertoRicoGood, delta: 1 | -1) => void;
  setGalleonGood: (index: number, good: PuertoRicoGood | null) => void;
  bumpGalleonLoaded: (index: number, delta: 1 | -1) => void;
  setTradingSlot: (index: number, good: PuertoRicoGood | null) => void;
  bumpColonistShip: (field: 'onShip' | 'supply', delta: 1 | -1) => void;
}

const clampMin = (n: number) => (n < 0 ? 0 : n);
const clampRange = (n: number, max: number) => (n < 0 ? 0 : n > max ? max : n);

export function usePuertoRicoStateEditor(
  sessionId: string,
  playerIds: readonly string[]
): PuertoRicoStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parsePuertoRicoGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: PuertoRicoGameState) => mutate(next),
    500
  );

  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: PuertoRicoGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  const readState = useCallback(
    (): PuertoRicoGameState | null =>
      parsePuertoRicoGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const editPlayer = useCallback(
    (playerId: string, fn: (p: PuertoRicoPlayerState) => PuertoRicoPlayerState) => {
      const cur = readState();
      if (cur == null) return;
      const prev = cur.players[playerId] ?? emptyPuertoRicoPlayerState();
      commit({ ...cur, players: { ...cur.players, [playerId]: fn(prev) } });
    },
    [commit, readState]
  );

  const initializeState = useCallback(
    () => commit(initialPuertoRicoState(playerIds)),
    [commit, playerIds]
  );

  const bumpPlayerCounter = useCallback(
    (playerId: string, field: PlayerCounter, delta: 1 | -1) =>
      editPlayer(playerId, p => ({ ...p, [field]: clampMin(p[field] + delta) })),
    [editPlayer]
  );

  const bumpPlayerGood = useCallback(
    (playerId: string, good: PuertoRicoGood, delta: 1 | -1) =>
      editPlayer(playerId, p => ({
        ...p,
        storehouse: { ...p.storehouse, [good]: clampMin(p.storehouse[good] + delta) },
      })),
    [editPlayer]
  );

  const setGalleonGood = useCallback(
    (index: number, good: PuertoRicoGood | null) => {
      const cur = readState();
      if (cur == null || index < 0 || index >= cur.galleons.length) return;
      const galleons = cur.galleons.map((g, i) => (i === index ? { ...g, good, loaded: 0 } : g));
      commit({ ...cur, galleons });
    },
    [commit, readState]
  );

  const bumpGalleonLoaded = useCallback(
    (index: number, delta: 1 | -1) => {
      const cur = readState();
      if (cur == null || index < 0 || index >= cur.galleons.length) return;
      const galleons = cur.galleons.map((g, i) =>
        i === index ? { ...g, loaded: clampRange(g.loaded + delta, g.cap) } : g
      );
      commit({ ...cur, galleons });
    },
    [commit, readState]
  );

  const setTradingSlot = useCallback(
    (index: number, good: PuertoRicoGood | null) => {
      const cur = readState();
      if (cur == null || index < 0 || index >= cur.tradingHouse.slots.length) return;
      const slots = cur.tradingHouse.slots.map((s, i) => (i === index ? good : s));
      commit({ ...cur, tradingHouse: { slots } });
    },
    [commit, readState]
  );

  const bumpColonistShip = useCallback(
    (field: 'onShip' | 'supply', delta: 1 | -1) => {
      const cur = readState();
      if (cur == null) return;
      commit({
        ...cur,
        colonistShip: { ...cur.colonistShip, [field]: clampMin(cur.colonistShip[field] + delta) },
      });
    },
    [commit, readState]
  );

  return {
    state,
    initializeState,
    bumpPlayerCounter,
    bumpPlayerGood,
    setGalleonGood,
    bumpGalleonLoaded,
    setTradingSlot,
    bumpColonistShip,
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/use-puerto-rico-state-editor.test.tsx`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/puerto-rico/use-puerto-rico-state-editor.ts" "apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/use-puerto-rico-state-editor.test.tsx"
git commit -m "feat(session-live): #2792 Puerto Rico L2 host-edit hook (debounced)"
```

---

## Task 3: Goods palette + PlayerMatSummary

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/puerto-rico/puerto-rico-palette.ts`
- Create: `apps/web/src/components/features/session-live/flavors/puerto-rico/PuertoRicoPlayerMatSummary.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoPlayerMatSummary.test.tsx`

**Interfaces:**
- Consumes: `PuertoRicoGood`, `PuertoRicoPlayerState`, `PUERTO_RICO_GOODS` from `./puerto-rico-state`; `puertoRicoGoodColor` from `./puerto-rico-palette`; `LiveSessionPlayerDto` from `@/lib/api/schemas/live-sessions.schemas`.
- Produces: `PuertoRicoPlayerMatSummary` with props
  ```ts
  type PlayerCounter = 'doubloons' | 'colonists' | 'plantations' | 'quarries' | 'buildings';
  interface PuertoRicoPlayerMatSummaryProps {
    player: LiveSessionPlayerDto;
    state: PuertoRicoPlayerState;
    editable: boolean;
    onBumpCounter?: (field: PlayerCounter, delta: 1 | -1) => void;
    onBumpGood?: (good: PuertoRicoGood, delta: 1 | -1) => void;
    labels: { doubloonsLabel: string; colonistsLabel: string; plantationsLabel: string; quarriesLabel: string; buildingsLabel: string; incAria: string; decAria: string /* "{field} +1" / "-1" */ };
  }
  ```

- [ ] **Step 1: Write the palette**

```ts
// puerto-rico-palette.ts
import type { PuertoRicoGood } from './puerto-rico-state';

// The 5 Puerto Rico goods — inline hsl() applied via `style` (like catan/codenames palettes).
// Any hue that trips meepleai/no-inline-hsl-v2 carries a line-level disable with a reason.
const GOOD_HSL: Record<PuertoRicoGood, string> = {
  corn: 'hsl(48, 85%, 55%)',
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Puerto Rico indigo good colour, not the chat/document entity token
  indigo: 'hsl(230, 55%, 52%)',
  sugar: 'hsl(0, 0%, 88%)',
  tobacco: 'hsl(28, 45%, 44%)',
  coffee: 'hsl(25, 40%, 26%)',
};

export function puertoRicoGoodColor(good: PuertoRicoGood): string {
  return GOOD_HSL[good];
}
```

> After writing the palette, run `pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/puerto-rico/puerto-rico-palette.ts`. If a hue OTHER than `indigo` also trips `meepleai/no-inline-hsl-v2`, add the same line-level disable above it (with a reason). The pre-commit hook will NOT catch these.

- [ ] **Step 2: Write the failing test**

```tsx
// PuertoRicoPlayerMatSummary.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PuertoRicoPlayerMatSummary } from '../PuertoRicoPlayerMatSummary';
import { emptyPuertoRicoPlayerState } from '../puerto-rico-state';

const player = { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 0, currentRank: 1, joinedAt: '', isActive: true } as const;
const labels = {
  doubloonsLabel: 'Dobloni', colonistsLabel: 'Coloni', plantationsLabel: 'Piantagioni',
  quarriesLabel: 'Cave', buildingsLabel: 'Edifici', incAria: '{field} +1', decAria: '{field} -1',
};

describe('PuertoRicoPlayerMatSummary', () => {
  it('renders the name + all 5 goods', () => {
    const { container } = render(
      <PuertoRicoPlayerMatSummary player={player} state={{ ...emptyPuertoRicoPlayerState(), storehouse: { corn: 2, indigo: 0, sugar: 1, tobacco: 0, coffee: 3 } }} editable={false} labels={labels} />
    );
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(container.querySelectorAll('[data-good]')).toHaveLength(5);
  });

  it('read-only mode exposes no steppers', () => {
    render(<PuertoRicoPlayerMatSummary player={player} state={emptyPuertoRicoPlayerState()} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: doubloons +1 fires onBumpCounter', async () => {
    const onBumpCounter = vi.fn();
    render(<PuertoRicoPlayerMatSummary player={player} state={emptyPuertoRicoPlayerState()} editable onBumpCounter={onBumpCounter} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Dobloni +1' }));
    expect(onBumpCounter).toHaveBeenCalledWith('doubloons', 1);
  });

  it('host: a good stepper fires onBumpGood', async () => {
    const onBumpGood = vi.fn();
    const { container } = render(<PuertoRicoPlayerMatSummary player={player} state={emptyPuertoRicoPlayerState()} editable onBumpGood={onBumpGood} labels={labels} />);
    const cornInc = container.querySelector('[data-good="corn"] [data-dir="inc"]') as HTMLElement;
    await userEvent.click(cornInc);
    expect(onBumpGood).toHaveBeenCalledWith('corn', 1);
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoPlayerMatSummary.test.tsx`
Expected: FAIL — `Cannot find module '../PuertoRicoPlayerMatSummary'`.

- [ ] **Step 4: Write the implementation**

```tsx
// PuertoRicoPlayerMatSummary.tsx
'use client';

import { type ReactElement } from 'react';

import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { puertoRicoGoodColor } from './puerto-rico-palette';
import { PUERTO_RICO_GOODS, type PuertoRicoGood, type PuertoRicoPlayerState } from './puerto-rico-state';

type PlayerCounter = 'doubloons' | 'colonists' | 'plantations' | 'quarries' | 'buildings';

export interface PuertoRicoPlayerMatSummaryProps {
  readonly player: LiveSessionPlayerDto;
  readonly state: PuertoRicoPlayerState;
  readonly editable: boolean;
  readonly onBumpCounter?: (field: PlayerCounter, delta: 1 | -1) => void;
  readonly onBumpGood?: (good: PuertoRicoGood, delta: 1 | -1) => void;
  readonly labels: {
    doubloonsLabel: string; colonistsLabel: string; plantationsLabel: string;
    quarriesLabel: string; buildingsLabel: string; incAria: string; decAria: string;
  };
}

function Stepper({
  label, value, editable, incAria, decAria, onDelta, data,
}: {
  label: string; value: number; editable: boolean; incAria: string; decAria: string;
  onDelta?: (d: 1 | -1) => void; data?: Record<string, string>;
}): ReactElement {
  return (
    <span className="inline-flex items-center gap-1 text-xs" {...data}>
      <span className="text-muted-foreground">{label}</span>
      {editable && (
        <button type="button" data-dir="dec" aria-label={decAria} onClick={() => onDelta?.(-1)}
          className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted">−</button>
      )}
      <span className="min-w-4 text-center font-semibold tabular-nums text-foreground">{value}</span>
      {editable && (
        <button type="button" data-dir="inc" aria-label={incAria} onClick={() => onDelta?.(1)}
          className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted">+</button>
      )}
    </span>
  );
}

export function PuertoRicoPlayerMatSummary({
  player, state, editable, onBumpCounter, onBumpGood, labels,
}: PuertoRicoPlayerMatSummaryProps): ReactElement {
  const inc = (f: string) => labels.incAria.replace('{field}', f);
  const dec = (f: string) => labels.decAria.replace('{field}', f);
  const counters: Array<[PlayerCounter, string]> = [
    ['doubloons', labels.doubloonsLabel], ['colonists', labels.colonistsLabel],
    ['plantations', labels.plantationsLabel], ['quarries', labels.quarriesLabel],
    ['buildings', labels.buildingsLabel],
  ];

  return (
    <div data-slot="pr-player-mat" className="flex flex-col gap-1.5 rounded-lg border border-border bg-card p-2">
      <span className="text-xs font-semibold text-foreground">{player.displayName}</span>

      <div className="flex flex-wrap gap-x-3 gap-y-1">
        {counters.map(([field, label]) => (
          <Stepper key={field} label={label} value={state[field]} editable={editable}
            incAria={inc(label)} decAria={dec(label)}
            onDelta={d => onBumpCounter?.(field, d)} />
        ))}
      </div>

      <div data-slot="pr-storehouse" className="flex flex-wrap gap-2">
        {PUERTO_RICO_GOODS.map(good => (
          <span key={good} data-good={good} className="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs"
            style={{ backgroundColor: puertoRicoGoodColor(good), color: good === 'sugar' || good === 'corn' ? 'hsl(0,0%,15%)' : 'hsl(0,0%,100%)' }}>
            <span className="font-semibold tabular-nums">{state.storehouse[good]}</span>
            {editable && (
              <>
                <button type="button" data-dir="dec" aria-label={dec(good)} onClick={() => onBumpGood?.(good, -1)}
                  className="h-4 w-4 rounded bg-black/20 leading-none">−</button>
                <button type="button" data-dir="inc" aria-label={inc(good)} onClick={() => onBumpGood?.(good, 1)}
                  className="h-4 w-4 rounded bg-black/20 leading-none">+</button>
              </>
            )}
          </span>
        ))}
      </div>
    </div>
  );
}
```

> `text-white` is NOT used (white is inline `style.color`). The goods chips set an inline coloured `backgroundColor`; `bg-black/20` on the tiny +/- buttons is a neutral alpha overlay (allowed — it's `black`, not a neutral-palette utility). Run `pnpm exec eslint --max-warnings=0` on both new files before committing.

- [ ] **Step 5: Run eslint + test**

```bash
cd apps/web && pnpm exec eslint --max-warnings=0 \
  src/components/features/session-live/flavors/puerto-rico/puerto-rico-palette.ts \
  src/components/features/session-live/flavors/puerto-rico/PuertoRicoPlayerMatSummary.tsx
pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoPlayerMatSummary.test.tsx
```
Expected: eslint clean (add a line-level `no-inline-hsl-v2` disable if a good hue trips it); test PASS (4 tests). If `bg-black/20` trips `local/no-hardcoded-color-utility`, replace with an inline `style` overlay or drop the overlay (use `border border-border` instead).

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/puerto-rico/puerto-rico-palette.ts" "apps/web/src/components/features/session-live/flavors/puerto-rico/PuertoRicoPlayerMatSummary.tsx" "apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoPlayerMatSummary.test.tsx"
git commit -m "feat(session-live): #2792 Puerto Rico L3 player mat summary + goods palette"
```

---

## Task 4: GalleonsPanel

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/puerto-rico/PuertoRicoGalleonsPanel.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoGalleonsPanel.test.tsx`

**Interfaces:**
- Consumes: `PuertoRicoGalleon`, `PuertoRicoGood`, `PUERTO_RICO_GOODS` from `./puerto-rico-state`; `puertoRicoGoodColor` from `./puerto-rico-palette`.
- Produces: `PuertoRicoGalleonsPanel` with props
  ```ts
  interface PuertoRicoGalleonsPanelProps {
    galleons: PuertoRicoGalleon[];
    editable: boolean;
    onSetGood?: (index: number, good: PuertoRicoGood | null) => void;
    onBumpLoaded?: (index: number, delta: 1 | -1) => void;
    labels: { heading: string; emptyGood: string; loadedAria: string /* "Carica nave {n}" */; unloadAria: string; capTemplate: string /* "{loaded}/{cap}" */ };
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// PuertoRicoGalleonsPanel.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PuertoRicoGalleonsPanel } from '../PuertoRicoGalleonsPanel';

const galleons = [
  { good: 'corn' as const, loaded: 2, cap: 5 },
  { good: null, loaded: 0, cap: 6 },
  { good: null, loaded: 0, cap: 7 },
];
const labels = { heading: 'Galeoni', emptyGood: '—', loadedAria: 'Carica nave {n}', unloadAria: 'Scarica nave {n}', capTemplate: '{loaded}/{cap}' };

describe('PuertoRicoGalleonsPanel', () => {
  it('renders one row per galleon with loaded/cap', () => {
    const { container } = render(<PuertoRicoGalleonsPanel galleons={galleons} editable={false} labels={labels} />);
    const rows = container.querySelectorAll('[data-slot="pr-galleon"]');
    expect(rows).toHaveLength(3);
    expect(rows[0]?.textContent).toContain('2/5');
  });

  it('read-only exposes no controls', () => {
    const { container } = render(<PuertoRicoGalleonsPanel galleons={galleons} editable={false} labels={labels} />);
    expect(container.querySelector('button')).toBeNull();
    expect(container.querySelector('select')).toBeNull();
  });

  it('host: loading a galleon fires onBumpLoaded with its index', async () => {
    const onBumpLoaded = vi.fn();
    const { container } = render(<PuertoRicoGalleonsPanel galleons={galleons} editable onBumpLoaded={onBumpLoaded} labels={labels} />);
    const load0 = container.querySelector('[data-slot="pr-galleon"][data-index="0"] [data-dir="inc"]') as HTMLElement;
    await userEvent.click(load0);
    expect(onBumpLoaded).toHaveBeenCalledWith(0, 1);
  });

  it('host: choosing a good fires onSetGood', async () => {
    const onSetGood = vi.fn();
    const { container } = render(<PuertoRicoGalleonsPanel galleons={galleons} editable onSetGood={onSetGood} labels={labels} />);
    const select = container.querySelector('[data-slot="pr-galleon"][data-index="1"] select') as HTMLSelectElement;
    await userEvent.selectOptions(select, 'sugar');
    expect(onSetGood).toHaveBeenCalledWith(1, 'sugar');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoGalleonsPanel.test.tsx`
Expected: FAIL — `Cannot find module '../PuertoRicoGalleonsPanel'`.

- [ ] **Step 3: Write the implementation**

```tsx
// PuertoRicoGalleonsPanel.tsx
'use client';

import { type ReactElement } from 'react';

import { puertoRicoGoodColor } from './puerto-rico-palette';
import { PUERTO_RICO_GOODS, type PuertoRicoGalleon, type PuertoRicoGood } from './puerto-rico-state';

export interface PuertoRicoGalleonsPanelProps {
  readonly galleons: PuertoRicoGalleon[];
  readonly editable: boolean;
  readonly onSetGood?: (index: number, good: PuertoRicoGood | null) => void;
  readonly onBumpLoaded?: (index: number, delta: 1 | -1) => void;
  readonly labels: { heading: string; emptyGood: string; loadedAria: string; unloadAria: string; capTemplate: string };
}

export function PuertoRicoGalleonsPanel({
  galleons, editable, onSetGood, onBumpLoaded, labels,
}: PuertoRicoGalleonsPanelProps): ReactElement {
  return (
    <section data-slot="pr-galleons" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{labels.heading}</h3>
      <ul role="list" className="flex flex-col gap-1">
        {galleons.map((g, i) => (
          <li key={i} data-slot="pr-galleon" data-index={String(i)}
            className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1">
            {g.good != null ? (
              <span aria-hidden="true" className="h-3 w-3 rounded-full" style={{ backgroundColor: puertoRicoGoodColor(g.good) }} />
            ) : (
              <span aria-hidden="true" className="text-xs text-muted-foreground">{labels.emptyGood}</span>
            )}
            {editable ? (
              <select value={g.good ?? ''} onChange={e => onSetGood?.(i, e.target.value === '' ? null : (e.target.value as PuertoRicoGood))}
                className="rounded border border-border bg-background px-1 py-0.5 text-xs text-foreground">
                <option value="">{labels.emptyGood}</option>
                {PUERTO_RICO_GOODS.map(good => <option key={good} value={good}>{good}</option>)}
              </select>
            ) : (
              <span className="text-xs text-foreground">{g.good ?? labels.emptyGood}</span>
            )}
            <span className="ml-auto tabular-nums text-sm font-bold text-foreground">
              {labels.capTemplate.replace('{loaded}', String(g.loaded)).replace('{cap}', String(g.cap))}
            </span>
            {editable && (
              <span className="flex items-center gap-1">
                <button type="button" data-dir="dec" aria-label={labels.unloadAria.replace('{n}', String(i + 1))}
                  onClick={() => onBumpLoaded?.(i, -1)} className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted">−</button>
                <button type="button" data-dir="inc" aria-label={labels.loadedAria.replace('{n}', String(i + 1))}
                  onClick={() => onBumpLoaded?.(i, 1)} className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted">+</button>
              </span>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
```

- [ ] **Step 4: Run eslint + test**

```bash
cd apps/web && pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/puerto-rico/PuertoRicoGalleonsPanel.tsx
pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoGalleonsPanel.test.tsx
```
Expected: eslint clean; test PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/puerto-rico/PuertoRicoGalleonsPanel.tsx" "apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoGalleonsPanel.test.tsx"
git commit -m "feat(session-live): #2792 Puerto Rico L3 galleons panel"
```

---

## Task 5: TradingHousePanel

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/puerto-rico/PuertoRicoTradingHousePanel.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoTradingHousePanel.test.tsx`

**Interfaces:**
- Consumes: `PuertoRicoGood`, `PUERTO_RICO_GOODS` from `./puerto-rico-state`; `puertoRicoGoodColor` from `./puerto-rico-palette`.
- Produces: `PuertoRicoTradingHousePanel` with props
  ```ts
  interface PuertoRicoTradingHousePanelProps {
    slots: (PuertoRicoGood | null)[];
    editable: boolean;
    onSetSlot?: (index: number, good: PuertoRicoGood | null) => void;
    labels: { heading: string; emptyGood: string; slotAria: string /* "Slot {n}" */ };
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// PuertoRicoTradingHousePanel.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PuertoRicoTradingHousePanel } from '../PuertoRicoTradingHousePanel';

const labels = { heading: 'Casa commerciale', emptyGood: '—', slotAria: 'Slot {n}' };

describe('PuertoRicoTradingHousePanel', () => {
  it('renders 4 slots', () => {
    const { container } = render(<PuertoRicoTradingHousePanel slots={['corn', null, null, null]} editable={false} labels={labels} />);
    expect(container.querySelectorAll('[data-slot="pr-trade-slot"]')).toHaveLength(4);
  });

  it('read-only exposes no selects', () => {
    const { container } = render(<PuertoRicoTradingHousePanel slots={[null, null, null, null]} editable={false} labels={labels} />);
    expect(container.querySelector('select')).toBeNull();
  });

  it('host: setting a slot fires onSetSlot', async () => {
    const onSetSlot = vi.fn();
    const { container } = render(<PuertoRicoTradingHousePanel slots={[null, null, null, null]} editable onSetSlot={onSetSlot} labels={labels} />);
    const select = container.querySelector('[data-slot="pr-trade-slot"][data-index="0"] select') as HTMLSelectElement;
    await userEvent.selectOptions(select, 'tobacco');
    expect(onSetSlot).toHaveBeenCalledWith(0, 'tobacco');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoTradingHousePanel.test.tsx`
Expected: FAIL — `Cannot find module '../PuertoRicoTradingHousePanel'`.

- [ ] **Step 3: Write the implementation**

```tsx
// PuertoRicoTradingHousePanel.tsx
'use client';

import { type ReactElement } from 'react';

import { puertoRicoGoodColor } from './puerto-rico-palette';
import { PUERTO_RICO_GOODS, type PuertoRicoGood } from './puerto-rico-state';

export interface PuertoRicoTradingHousePanelProps {
  readonly slots: (PuertoRicoGood | null)[];
  readonly editable: boolean;
  readonly onSetSlot?: (index: number, good: PuertoRicoGood | null) => void;
  readonly labels: { heading: string; emptyGood: string; slotAria: string };
}

export function PuertoRicoTradingHousePanel({
  slots, editable, onSetSlot, labels,
}: PuertoRicoTradingHousePanelProps): ReactElement {
  return (
    <section data-slot="pr-trading" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{labels.heading}</h3>
      <div className="flex gap-1">
        {slots.map((good, i) => (
          <div key={i} data-slot="pr-trade-slot" data-index={String(i)}
            className="flex flex-1 flex-col items-center gap-1 rounded-lg border border-border bg-card p-1">
            <span aria-hidden="true" className="h-3 w-3 rounded-full"
              style={{ backgroundColor: good != null ? puertoRicoGoodColor(good) : 'transparent', borderWidth: good == null ? 1 : 0 }} />
            {editable ? (
              <select aria-label={labels.slotAria.replace('{n}', String(i + 1))} value={good ?? ''}
                onChange={e => onSetSlot?.(i, e.target.value === '' ? null : (e.target.value as PuertoRicoGood))}
                className="w-full rounded border border-border bg-background px-0.5 py-0.5 text-[10px] text-foreground">
                <option value="">{labels.emptyGood}</option>
                {PUERTO_RICO_GOODS.map(g => <option key={g} value={g}>{g}</option>)}
              </select>
            ) : (
              <span className="text-[10px] text-foreground">{good ?? labels.emptyGood}</span>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run eslint + test**

```bash
cd apps/web && pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/puerto-rico/PuertoRicoTradingHousePanel.tsx
pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoTradingHousePanel.test.tsx
```
Expected: eslint clean; test PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/puerto-rico/PuertoRicoTradingHousePanel.tsx" "apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoTradingHousePanel.test.tsx"
git commit -m "feat(session-live): #2792 Puerto Rico L3 trading house panel"
```

---

## Task 6: ColonistShipPanel

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/puerto-rico/PuertoRicoColonistShipPanel.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoColonistShipPanel.test.tsx`

**Interfaces:**
- Produces: `PuertoRicoColonistShipPanel` with props
  ```ts
  interface PuertoRicoColonistShipPanelProps {
    colonistShip: { onShip: number; supply: number };
    editable: boolean;
    onBump?: (field: 'onShip' | 'supply', delta: 1 | -1) => void;
    labels: { heading: string; onShipLabel: string; supplyLabel: string; incAria: string; decAria: string };
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// PuertoRicoColonistShipPanel.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PuertoRicoColonistShipPanel } from '../PuertoRicoColonistShipPanel';

const labels = { heading: 'Nave coloni', onShipLabel: 'Sulla nave', supplyLabel: 'Riserva', incAria: '{field} +1', decAria: '{field} -1' };

describe('PuertoRicoColonistShipPanel', () => {
  it('shows onShip + supply', () => {
    render(<PuertoRicoColonistShipPanel colonistShip={{ onShip: 3, supply: 20 }} editable={false} labels={labels} />);
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('20')).toBeInTheDocument();
  });

  it('read-only exposes no buttons', () => {
    render(<PuertoRicoColonistShipPanel colonistShip={{ onShip: 0, supply: 0 }} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: bumping onShip fires onBump', async () => {
    const onBump = vi.fn();
    render(<PuertoRicoColonistShipPanel colonistShip={{ onShip: 0, supply: 0 }} editable onBump={onBump} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Sulla nave +1' }));
    expect(onBump).toHaveBeenCalledWith('onShip', 1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoColonistShipPanel.test.tsx`
Expected: FAIL — `Cannot find module '../PuertoRicoColonistShipPanel'`.

- [ ] **Step 3: Write the implementation**

```tsx
// PuertoRicoColonistShipPanel.tsx
'use client';

import { type ReactElement } from 'react';

export interface PuertoRicoColonistShipPanelProps {
  readonly colonistShip: { onShip: number; supply: number };
  readonly editable: boolean;
  readonly onBump?: (field: 'onShip' | 'supply', delta: 1 | -1) => void;
  readonly labels: { heading: string; onShipLabel: string; supplyLabel: string; incAria: string; decAria: string };
}

export function PuertoRicoColonistShipPanel({
  colonistShip, editable, onBump, labels,
}: PuertoRicoColonistShipPanelProps): ReactElement {
  const rows: Array<['onShip' | 'supply', string]> = [
    ['onShip', labels.onShipLabel], ['supply', labels.supplyLabel],
  ];
  return (
    <section data-slot="pr-colonist-ship" className="flex flex-col gap-1 rounded-lg border border-border bg-card p-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{labels.heading}</h3>
      {rows.map(([field, label]) => (
        <div key={field} className="flex items-center gap-2 text-xs">
          <span className="text-muted-foreground">{label}</span>
          {editable && (
            <button type="button" aria-label={labels.decAria.replace('{field}', label)} onClick={() => onBump?.(field, -1)}
              className="ml-auto h-5 w-5 rounded border border-border text-foreground hover:bg-muted">−</button>
          )}
          <span className={`${editable ? '' : 'ml-auto'} min-w-4 text-center font-semibold tabular-nums text-foreground`}>{colonistShip[field]}</span>
          {editable && (
            <button type="button" aria-label={labels.incAria.replace('{field}', label)} onClick={() => onBump?.(field, 1)}
              className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted">+</button>
          )}
        </div>
      ))}
    </section>
  );
}
```

- [ ] **Step 4: Run eslint + test**

```bash
cd apps/web && pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/puerto-rico/PuertoRicoColonistShipPanel.tsx
pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoColonistShipPanel.test.tsx
```
Expected: eslint clean; test PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/puerto-rico/PuertoRicoColonistShipPanel.tsx" "apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoColonistShipPanel.test.tsx"
git commit -m "feat(session-live): #2792 Puerto Rico L3 colonist ship panel"
```

---

## Task 7: PuertoRicoLiveFlavor container (self-builds labels)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/puerto-rico/PuertoRicoLiveFlavor.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoLiveFlavor.test.tsx`

**Interfaces:**
- Consumes: the 4 panels; `emptyPuertoRicoPlayerState`, `parsePuertoRicoGameState`; `usePuertoRicoStateEditor`; `hasRequiredRole`, `ParticipantRole`; `LiveSessionDto`; `useIntl` + `useTranslation`.
- Produces: `PuertoRicoLiveFlavor` + `PuertoRicoLiveFlavorProps` (game-agnostic `FlavorProps`): `{ session, viewerRole, sessionId, className?, livePoints?, phaseName? }`.

- [ ] **Step 1: Write the failing test**

```tsx
// PuertoRicoLiveFlavor.test.tsx
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { PuertoRicoLiveFlavor } from '../PuertoRicoLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { initialPuertoRicoState } from '../puerto-rico-state';

expect.extend(toHaveNoViolations);
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({ useUpdateLiveGameState: () => ({ mutate: vi.fn() }) }));

const session = {
  id: 's1', sessionCode: 'ABC', gameId: null, gameName: 'Puerto Rico', gameSlug: 'puerto-rico',
  createdByUserId: 'u1', status: 'InProgress', visibility: 'Private', groupId: null,
  createdAt: '', startedAt: '', pausedAt: null, completedAt: null, updatedAt: '', lastSavedAt: null,
  currentTurnIndex: 0, currentTurnPlayerId: 'p1', agentMode: 'None', notes: null,
  players: [
    { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 8, currentRank: 1, joinedAt: '', isActive: true },
    { id: 'p2', userId: null, displayName: 'Anna', avatarUrl: null, color: 'Blue', role: 'Player', teamId: null, totalScore: 5, currentRank: 2, joinedAt: '', isActive: false },
  ],
  teams: [], roundScores: [], scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

function renderFlavor(props: Partial<Parameters<typeof PuertoRicoLiveFlavor>[0]> = {}) {
  return render(
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <PuertoRicoLiveFlavor session={session} viewerRole="Player" sessionId="s1" {...props} />
    </IntlProvider>
  );
}
beforeEach(() => useLiveSessionStore.getState().reset());

describe('PuertoRicoLiveFlavor', () => {
  it('renders the leaderboard even with null gameState; no panels', () => {
    const { container } = renderFlavor();
    expect(container.querySelectorAll('[data-slot="pr-leaderboard-row"]')).toHaveLength(2);
    expect(container.querySelector('[data-slot="pr-galleons"]')).toBeNull();
  });

  it('host sees the init CTA when gameState is null', () => {
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="pr-init"]')).not.toBeNull();
  });

  it('renders the panels + a mat per player when gameState is present', () => {
    useLiveSessionStore.getState().setGameState(initialPuertoRicoState(['p1', 'p2']));
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="pr-galleons"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="pr-trading"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="pr-colonist-ship"]')).not.toBeNull();
    expect(container.querySelectorAll('[data-slot="pr-player-mat"]')).toHaveLength(2);
  });

  it('has no axe violations (host, populated)', async () => {
    useLiveSessionStore.getState().setGameState(initialPuertoRicoState(['p1', 'p2']));
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoLiveFlavor.test.tsx`
Expected: FAIL — `Cannot find module '../PuertoRicoLiveFlavor'`.

- [ ] **Step 3: Write the implementation**

```tsx
// PuertoRicoLiveFlavor.tsx
'use client';

import { type ReactElement } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { PuertoRicoColonistShipPanel } from './PuertoRicoColonistShipPanel';
import { PuertoRicoGalleonsPanel } from './PuertoRicoGalleonsPanel';
import { PuertoRicoPlayerMatSummary } from './PuertoRicoPlayerMatSummary';
import { PuertoRicoTradingHousePanel } from './PuertoRicoTradingHousePanel';
import { emptyPuertoRicoPlayerState } from './puerto-rico-state';
import { usePuertoRicoStateEditor } from './use-puerto-rico-state-editor';

export interface PuertoRicoLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.puerto-rico';

export function PuertoRicoLiveFlavor({
  session, viewerRole, sessionId, className, livePoints,
}: PuertoRicoLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const playerIds = session.players.map(p => p.id);
  const editor = usePuertoRicoStateEditor(sessionId, playerIds);
  const state = editor.state;

  const tmpl = (id: string, fallback: string) => (intl.messages[`${K}.${id}`] as string) ?? fallback;
  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));

  const matLabels = {
    doubloonsLabel: t(`${K}.doubloons`), colonistsLabel: t(`${K}.colonists`),
    plantationsLabel: t(`${K}.plantations`), quarriesLabel: t(`${K}.quarries`),
    buildingsLabel: t(`${K}.buildings`),
    incAria: tmpl('incAria', '{field} +1'), decAria: tmpl('decAria', '{field} -1'),
  };

  return (
    <section aria-label={t(`${K}.panelAriaLabel`)} data-slot="pr-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}>
      {/* Leaderboard (ungated — from scoring) */}
      <div data-slot="pr-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t(`${K}.leaderboardHeading`)}</h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li key={player.id} data-slot="pr-leaderboard-row"
              className={['flex items-center gap-2 rounded-lg px-2 py-1.5', idx === 0 ? 'border border-entity-session/40 bg-entity-session/10' : 'border border-transparent bg-card'].join(' ')}>
              <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                {player.displayName}{idx === 0 && <span aria-hidden="true"> 🏆</span>}
              </span>
              <span className="shrink-0 tabular-nums text-sm font-bold text-foreground">{scoreOf(player.id)}</span>
            </li>
          ))}
        </ul>
      </div>

      {state != null ? (
        <>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            <PuertoRicoGalleonsPanel galleons={state.galleons} editable={isHost}
              onSetGood={editor.setGalleonGood} onBumpLoaded={editor.bumpGalleonLoaded}
              labels={{ heading: t(`${K}.galleonsHeading`), emptyGood: t(`${K}.emptyGood`), goodAria: tmpl('goodAria', 'Ship {n} good'), loadedAria: tmpl('loadAria', 'Load ship {n}'), unloadAria: tmpl('unloadAria', 'Unload ship {n}'), capTemplate: tmpl('capTemplate', '{loaded}/{cap}') }} />
            <PuertoRicoTradingHousePanel slots={state.tradingHouse.slots} editable={isHost}
              onSetSlot={editor.setTradingSlot}
              labels={{ heading: t(`${K}.tradingHeading`), emptyGood: t(`${K}.emptyGood`), slotAria: tmpl('slotAria', 'Slot {n}') }} />
            <PuertoRicoColonistShipPanel colonistShip={state.colonistShip} editable={isHost}
              onBump={editor.bumpColonistShip}
              labels={{ heading: t(`${K}.colonistShipHeading`), onShipLabel: t(`${K}.onShip`), supplyLabel: t(`${K}.supply`), incAria: tmpl('incAria', '{field} +1'), decAria: tmpl('decAria', '{field} -1') }} />
          </div>

          <div data-slot="pr-players" className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            {session.players.map(player => (
              <PuertoRicoPlayerMatSummary key={player.id} player={player}
                state={state.players[player.id] ?? emptyPuertoRicoPlayerState()} editable={isHost}
                onBumpCounter={(field, delta) => editor.bumpPlayerCounter(player.id, field, delta)}
                onBumpGood={(good, delta) => editor.bumpPlayerGood(player.id, good, delta)}
                labels={matLabels} />
            ))}
          </div>

          {isHost && (
            <button type="button" onClick={editor.initializeState}
              className="self-start text-xs text-muted-foreground underline hover:text-foreground">
              {t(`${K}.resetCta`)}
            </button>
          )}
        </>
      ) : isHost ? (
        <button type="button" data-slot="pr-init" onClick={editor.initializeState}
          className="self-start rounded-lg border border-entity-session/40 bg-entity-session/10 px-3 py-2 text-sm font-semibold text-entity-session hover:bg-entity-session/20">
          {t(`${K}.initBoardCta`)}
        </button>
      ) : (
        <p role="status" aria-live="polite" className="text-xs text-muted-foreground">{t(`${K}.viewerWaiting`)}</p>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Run eslint + test**

```bash
cd apps/web && pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/puerto-rico/PuertoRicoLiveFlavor.tsx
pnpm exec vitest run src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoLiveFlavor.test.tsx
```
Expected: eslint clean; test PASS (4 tests incl. axe). If the axe check fails on the `<select>` elements lacking a label in read-only mode — note read-only renders no selects, so this shouldn't arise; if it does, add the `slotAria`/`loadAria` aria-labels (already present on editable selects).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/puerto-rico/PuertoRicoLiveFlavor.tsx" "apps/web/src/components/features/session-live/flavors/puerto-rico/__tests__/PuertoRicoLiveFlavor.test.tsx"
git commit -m "feat(session-live): #2792 Puerto Rico L3 flavor container (self-builds labels)"
```

---

## Task 8: Wire into the registry + i18n

**Files:**
- Modify: `apps/web/src/components/features/session-live/FlavorRenderer.tsx`
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Test: `apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx` (extend)

**Interfaces:**
- Consumes: `PuertoRicoLiveFlavor` (a `FlavorProps` component) from Task 7. `FlavorRenderer` is already game-agnostic — additive entry, no interface change.

- [ ] **Step 1: Add the FLAVOR_MAP entry**

In `FlavorRenderer.tsx`, add a module-scope lazy component alongside the existing ones and add the map entry (note the hyphenated key):

```tsx
const PuertoRicoLiveFlavorLazy: FlavorComponent = dynamic(
  () => import('./flavors/puerto-rico/PuertoRicoLiveFlavor').then(m => ({ default: m.PuertoRicoLiveFlavor })),
  { ssr: false, loading: () => <FlavorLoadingSkeleton /> }
);
```
and add `'puerto-rico': { live: PuertoRicoLiveFlavorLazy }` to `FLAVOR_MAP`.

- [ ] **Step 2: Extend the FlavorRenderer guard test**

In `FlavorRenderer.test.tsx`, add:

```ts
expect(hasFlavor('puerto-rico')).toBe(true);
```

- [ ] **Step 3: Add the i18n keys**

In `src/locales/it.json`, under `pages.sessionLive.flavor`, add a `"puerto-rico"` sibling:

```json
"puerto-rico": {
  "panelAriaLabel": "Puerto Rico",
  "leaderboardHeading": "Classifica",
  "initBoardCta": "Inizia partita",
  "resetCta": "Reimposta stato",
  "viewerWaiting": "In attesa dell'host…",
  "doubloons": "Dobloni",
  "colonists": "Coloni",
  "plantations": "Piantagioni",
  "quarries": "Cave",
  "buildings": "Edifici",
  "galleonsHeading": "Galeoni",
  "tradingHeading": "Casa commerciale",
  "colonistShipHeading": "Nave coloni",
  "onShip": "Sulla nave",
  "supply": "Riserva",
  "emptyGood": "—",
  "capTemplate": "{loaded}/{cap}",
  "goodAria": "Merce nave {n}",
  "loadAria": "Carica nave {n}",
  "unloadAria": "Scarica nave {n}",
  "slotAria": "Slot {n}",
  "incAria": "{field} +1",
  "decAria": "{field} -1"
}
```

Mirror in `src/locales/en.json` with English copy (`"leaderboardHeading": "Standings"`, `"initBoardCta": "Start game"`, `"resetCta": "Reset state"`, `"viewerWaiting": "Waiting for the host…"`, `"doubloons": "Doubloons"`, `"colonists": "Colonists"`, `"plantations": "Plantations"`, `"quarries": "Quarries"`, `"buildings": "Buildings"`, `"galleonsHeading": "Cargo ships"`, `"tradingHeading": "Trading house"`, `"colonistShipHeading": "Colonist ship"`, `"onShip": "On ship"`, `"supply": "Supply"`, `"goodAria": "Ship {n} good"`, `"loadAria": "Load ship {n}"`, `"unloadAria": "Unload ship {n}"`; `emptyGood`/`capTemplate`/`slotAria`/`incAria`/`decAria` identical). BOTH locales MUST have the identical key set.

- [ ] **Step 4: Typecheck + run affected suites + eslint**

```bash
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run \
  src/components/features/session-live/flavors/puerto-rico \
  src/components/features/session-live/__tests__/FlavorRenderer.test.tsx
pnpm exec eslint --max-warnings=0 src/components/features/session-live/FlavorRenderer.tsx
```
Expected: typecheck clean; all suites PASS; eslint clean.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/FlavorRenderer.tsx" "apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx" "apps/web/src/locales/it.json" "apps/web/src/locales/en.json"
git commit -m "feat(session-live): #2792 wire Puerto Rico flavor into the registry + i18n"
```

---

## Task 9: Final verification

- [ ] **Step 1: Full typecheck + all flavor suites**

```bash
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run src/components/features/session-live/flavors src/components/features/session-live/__tests__/FlavorRenderer.test.tsx
```
Expected: typecheck clean; all Catan + Wingspan + Codenames + Puerto Rico flavor tests PASS.

- [ ] **Step 2: Lint the whole flavor dir**

```bash
pnpm exec eslint --max-warnings=0 "src/components/features/session-live/flavors/puerto-rico/**/*.{ts,tsx}" src/components/features/session-live/FlavorRenderer.tsx
```
Expected: no errors (goods colours inline `hsl()` via the palette with any needed `no-inline-hsl-v2` disable; no `text-white` utility).

- [ ] **Step 3: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2792-puerto-rico-l2-l3
gh pr create --base main-dev --head feature/issue-2792-puerto-rico-l2-l3 \
  --title "feat(session-live): #2792 Puerto Rico L2+L3 flavor (counters + shared pools)" \
  --body "Implements the Puerto Rico flavor per docs/superpowers/specs/2026-07-17-puerto-rico-l2-l3-flavor-design.md. FE-only; reuses the game-agnostic plumbing. Closes #2792."
```

---

## Self-Review

**1. Spec coverage:**
- L2 schema + helpers (storehouse fixed object, galleon caps [n+1,n+2,n+3]) → Task 1. ✅
- Editor debounced (all clamps; setGalleonGood resets loaded; loaded caps at cap) → Task 2. ✅
- PlayerMatSummary + palette → Task 3. GalleonsPanel → Task 4. TradingHousePanel → Task 5. ColonistShipPanel → Task 6. ✅
- Container (leaderboard ungated, panels gated, self-builds labels) → Task 7. ✅
- Wiring (FLAVOR_MAP + i18n) → Task 8. ✅
- VP-from-scoring invariant → Tasks 3/7 (read `livePoints`/`totalScore`, never gameState). ✅
- Cut role board + tableau layout → not modeled anywhere. ✅
- Lint gate per task (controller runs `pnpm exec eslint`) → Steps in Tasks 3–8 + Task 9. ✅
- Testing (unit + component + jest-axe) → Tasks 1–7. ✅

**2. Placeholder scan:** No TBD/TODO; every code step has complete code. ✅

**3. Type consistency:** `PuertoRicoGameState`, `PuertoRicoPlayerState`, `PuertoRicoGalleon`, `PuertoRicoGood`, `parsePuertoRicoGameState`, `emptyPuertoRicoPlayerState`, `initialPuertoRicoState`, `PUERTO_RICO_GOODS`, `usePuertoRicoStateEditor` signature (incl. `playerIds` param), and the 4 component prop interfaces + `PlayerCounter` union are used consistently across Tasks 1→8. The container passes `matLabels` matching `PuertoRicoPlayerMatSummaryProps['labels']`. Editor mutator names (`bumpPlayerCounter`/`bumpPlayerGood`/`setGalleonGood`/`bumpGalleonLoaded`/`setTradingSlot`/`bumpColonistShip`) match the container's `on*` wiring. ✅

**Known follow-ups (out of scope):** the 8-role board as live state; the tile-tableau layout; a Puerto Rico summary flavor; galleon capacity rules beyond the init formula.
