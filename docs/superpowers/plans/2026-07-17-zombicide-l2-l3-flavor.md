# Zombicide L2+L3 Flavor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Zombicide live flavor — a 6-type zombie horde counter + a per-player wound level (0/1/2) — on the L1 game-state layer, reusing the generalized flavor plumbing.

**Architecture:** FE-only. Reuses the game-agnostic pattern (`flavors/<game>/`; `FlavorRenderer` dispatches on `FLAVOR_MAP` with `FlavorProps`; each flavor self-builds i18n labels). Zombies + wounds live in `gameState`; mission-outcome/VP stays in the existing scoring system. Host-entered, no engine. **Mixed PUT cadence:** zombie ± debounced 500 ms, wound tap immediate.

**Tech Stack:** Next.js 16 · React 19 · TypeScript · Zod · Zustand (`useLiveSessionStore`) · TanStack Query (`useUpdateLiveGameState`) · `useDebouncedCallback` (trailing debounce with `flush()`) · react-intl (`useIntl`) + `@/hooks/useTranslation` · Vitest + Testing Library + jest-axe · Tailwind semantic tokens (wound colours inline `hsl()` via a palette module).

## Global Constraints

- **Issue:** #2793 (G6g, epic #3025) — the last of the 6 G6 games. Spec: `docs/superpowers/specs/2026-07-17-zombicide-l2-l3-flavor-design.md`.
- **Zero backend changes.** `gameState` is the opaque L1 blob; scores use the existing scoring editor.
- **State schema:** `v: 1`, `game: 'zombicide'`. `parseZombicideGameState` returns `null` (never throws) on wrong game/version/shape via Zod `safeParse`.
- **`gameState` shape:** `{ v, game, zombies:{walker,runner,fatty,berserker,abomination,necromancer}, survivors: Record<playerId, 0|1|2> }`. Never scores, never a win/loss flag.
- **`zombies` is a fixed `z.object` with all 6 keys required** (each `int>=0`), NOT `z.record`. `survivors` is `z.record(z.string(), z.union([z.literal(0),z.literal(1),z.literal(2)]))` — a wound level of `3` MUST reject.
- **Mixed cadence:** `bumpZombie` → DEBOUNCED 500 ms; `cycleWound` + `initializeState` → IMMEDIATE (`commit(next)` then `flush()`). Both do optimistic `setGameState(next)` FIRST. `readState()` re-parses the store FRESH per call. `useEffect(()=>()=>flush(),[flush])` flushes on unmount. Zombie bumps clamp `>=0`; wound cycles `0→1→2→0`.
- **`useDebouncedCallback(fn, 500)` returns `[debouncedFn, flush]`** — a trailing debounce holding only the latest arg; `flush()` runs the pending call immediately. The immediate path relies on this: `commit(next)` schedules `debouncedMutate(next)` (replacing any pending), then `flush()` sends the full fresh state — no stale-PUT race.
- **VP/mission-outcome stays in scoring** (`livePoints`/`totalScore`); gameState never carries it. **Leaderboard renders ungated**; panels gate on gameState (host CTA when null). **Host-edit only** (`viewerRole === 'Host'` via `hasRequiredRole`).
- **Wounds keyed by `session.players`** (one per player, seeded 0), not a free roster.
- **Flavors self-build i18n labels** via `useIntl` + `useTranslation`; templated strings via `intl.messages[id] as string ?? fallback`, static via `t(id)`.
- **Colours:** semantic Tailwind tokens EXCEPT the 3 wound colours → inline `hsl()` via the palette. `text-white` only on a cell that ALSO sets an inline coloured `backgroundColor` (put white in inline `style.color`, NOT the `text-white` utility).
- **LINT GATE (critical — implementers + the pre-commit hook miss it):** after each component/palette task run `pnpm exec eslint --max-warnings=0 <file>`. The pre-commit hook does NOT run `meepleai/no-inline-hsl-v2` nor the `style`-prop case of `local/no-hardcoded-color-utility`. Inline `hsl()` that trips `no-inline-hsl-v2` gets a line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- <reason>`.
- **Tests:** Vitest, TDD, pristine. Query via `data-slot`/roles, not `getByTestId`. Files under `apps/web/src/components/features/session-live/flavors/zombicide/`. Run from `apps/web`.
- **Windows:** pre-commit runs `pnpm typecheck` (~2 min, sometimes slower) — allow ≥9 min for commits; if TS2307 on stale `.next/types`, `rm -rf .next/types` first (never `--no-verify`).

## File Structure

Create under `flavors/zombicide/`: `zombicide-state.ts`, `zombicide-palette.ts`, `use-zombicide-state-editor.ts`, `ZombieHordePanel.tsx`, `ZombicideSurvivorsPanel.tsx`, `ZombicideLiveFlavor.tsx`, `__tests__/*`.
Modify: `session-live/FlavorRenderer.tsx`, `src/locales/it.json` + `en.json`.

---

## Task 1: L2 state schema + helpers

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/zombicide/zombicide-state.ts`
- Test: `apps/web/src/components/features/session-live/flavors/zombicide/__tests__/zombicide-state.test.ts`

**Interfaces:**
- Produces: `ZombicideGameState`, `ZombieCounts`, `ZombieType`, `WoundLevel` types; `parseZombicideGameState(raw): ZombicideGameState | null`; `emptyZombieCounts(): ZombieCounts`; `initialZombicideState(playerIds: readonly string[]): ZombicideGameState`; `nextWoundLevel(l: WoundLevel): WoundLevel`; `ZOMBIE_TYPES`, `ZOMBICIDE_WOUND_LEVELS`, `ZOMBICIDE_STATE_VERSION = 1`.

- [ ] **Step 1: Write the failing test**

```ts
// zombicide-state.test.ts
import { describe, expect, it } from 'vitest';

import {
  ZOMBIE_TYPES,
  emptyZombieCounts,
  initialZombicideState,
  nextWoundLevel,
  parseZombicideGameState,
} from '../zombicide-state';

const VALID = {
  v: 1, game: 'zombicide',
  zombies: { walker: 5, runner: 2, fatty: 0, berserker: 1, abomination: 0, necromancer: 1 },
  survivors: { p1: 0, p2: 2 },
};

describe('parseZombicideGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parseZombicideGameState(VALID);
    expect(parsed?.zombies.walker).toBe(5);
    expect(parsed?.survivors.p2).toBe(2);
  });
  it('returns null for a different game', () => {
    expect(parseZombicideGameState({ ...VALID, game: 'catan' })).toBeNull();
  });
  it('returns null for a future version', () => {
    expect(parseZombicideGameState({ ...VALID, v: 2 })).toBeNull();
  });
  it('returns null when a zombie type is missing', () => {
    expect(parseZombicideGameState({ ...VALID, zombies: { walker: 1, runner: 0, fatty: 2, berserker: 0, abomination: 0 } })).toBeNull();
  });
  it('returns null when a wound level is 3', () => {
    expect(parseZombicideGameState({ ...VALID, survivors: { p1: 3 } })).toBeNull();
  });
  it('returns null for malformed / non-object', () => {
    expect(parseZombicideGameState(null)).toBeNull();
    expect(parseZombicideGameState('x')).toBeNull();
  });
});

describe('emptyZombieCounts', () => {
  it('is all zero', () => {
    expect(emptyZombieCounts()).toEqual({ walker: 0, runner: 0, fatty: 0, berserker: 0, abomination: 0, necromancer: 0 });
  });
});

describe('initialZombicideState', () => {
  it('seeds zombies 0 + every player 0 wounds', () => {
    const s = initialZombicideState(['p1', 'p2']);
    expect(s.zombies).toEqual({ walker: 0, runner: 0, fatty: 0, berserker: 0, abomination: 0, necromancer: 0 });
    expect(s.survivors).toEqual({ p1: 0, p2: 0 });
    expect(s.game).toBe('zombicide');
  });
});

describe('nextWoundLevel', () => {
  it('cycles 0 → 1 → 2 → 0', () => {
    expect(nextWoundLevel(0)).toBe(1);
    expect(nextWoundLevel(1)).toBe(2);
    expect(nextWoundLevel(2)).toBe(0);
  });
});

describe('constants', () => {
  it('lists the 6 zombie types in order', () => {
    expect(ZOMBIE_TYPES).toEqual(['walker', 'runner', 'fatty', 'berserker', 'abomination', 'necromancer']);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/zombicide/__tests__/zombicide-state.test.ts`
Expected: FAIL — `Cannot find module '../zombicide-state'`.

- [ ] **Step 3: Write the implementation**

```ts
// zombicide-state.ts
import { z } from 'zod';

export const ZOMBICIDE_STATE_VERSION = 1;
export const ZOMBIE_TYPES = ['walker', 'runner', 'fatty', 'berserker', 'abomination', 'necromancer'] as const;
export const ZOMBICIDE_WOUND_LEVELS = [0, 1, 2] as const;

export const ZombieTypeSchema = z.enum(ZOMBIE_TYPES);
export type ZombieType = z.infer<typeof ZombieTypeSchema>;
export const WoundLevelSchema = z.union([z.literal(0), z.literal(1), z.literal(2)]);
export type WoundLevel = z.infer<typeof WoundLevelSchema>;

const nn = () => z.number().int().min(0);

export const ZombieCountsSchema = z.object({
  walker: nn(), runner: nn(), fatty: nn(), berserker: nn(), abomination: nn(), necromancer: nn(),
});
export type ZombieCounts = z.infer<typeof ZombieCountsSchema>;

export const ZombicideGameStateSchema = z.object({
  v: z.literal(ZOMBICIDE_STATE_VERSION),
  game: z.literal('zombicide'),
  zombies: ZombieCountsSchema,
  survivors: z.record(z.string(), WoundLevelSchema),
});
export type ZombicideGameState = z.infer<typeof ZombicideGameStateSchema>;

export function parseZombicideGameState(raw: unknown): ZombicideGameState | null {
  const result = ZombicideGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function emptyZombieCounts(): ZombieCounts {
  return { walker: 0, runner: 0, fatty: 0, berserker: 0, abomination: 0, necromancer: 0 };
}

export function initialZombicideState(playerIds: readonly string[]): ZombicideGameState {
  const survivors: Record<string, WoundLevel> = {};
  for (const id of playerIds) survivors[id] = 0;
  return {
    v: ZOMBICIDE_STATE_VERSION,
    game: 'zombicide',
    zombies: emptyZombieCounts(),
    survivors,
  };
}

export function nextWoundLevel(level: WoundLevel): WoundLevel {
  return level === 0 ? 1 : level === 1 ? 2 : 0;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/zombicide/__tests__/zombicide-state.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/zombicide/zombicide-state.ts" "apps/web/src/components/features/session-live/flavors/zombicide/__tests__/zombicide-state.test.ts"
git commit -m "feat(session-live): #2793 Zombicide L2 state schema + helpers"
```

---

## Task 2: Host-edit hook (mixed cadence)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/zombicide/use-zombicide-state-editor.ts`
- Test: `apps/web/src/components/features/session-live/flavors/zombicide/__tests__/use-zombicide-state-editor.test.tsx`

**Interfaces:**
- Consumes: `ZombicideGameState`, `ZombieType`, `parseZombicideGameState`, `initialZombicideState`, `nextWoundLevel` from `./zombicide-state`; `useLiveSessionStore`, `useUpdateLiveGameState`, `useDebouncedCallback`.
- Produces: `useZombicideStateEditor(sessionId: string, playerIds: readonly string[]): ZombicideStateEditor` where
  ```ts
  interface ZombicideStateEditor {
    state: ZombicideGameState | null;
    initializeState: () => void;                          // immediate
    bumpZombie: (type: ZombieType, delta: 1 | -1) => void; // debounced
    cycleWound: (playerId: string) => void;               // immediate
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// use-zombicide-state-editor.test.tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { useZombicideStateEditor } from '../use-zombicide-state-editor';
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
  return useLiveSessionStore.getState().gameState as import('../zombicide-state').ZombicideGameState | null;
}

describe('useZombicideStateEditor', () => {
  it('initializeState seeds zombies 0 + players 0 wounds', () => {
    const { result } = renderHook(() => useZombicideStateEditor(SID, ['p1', 'p2']));
    act(() => result.current.initializeState());
    expect(current()?.zombies.walker).toBe(0);
    expect(current()?.survivors).toEqual({ p1: 0, p2: 0 });
  });

  it('bumpZombie clamps at 0', () => {
    const { result } = renderHook(() => useZombicideStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.bumpZombie('walker', -1));
    expect(current()?.zombies.walker).toBe(0);
    act(() => result.current.bumpZombie('walker', 1));
    expect(current()?.zombies.walker).toBe(1);
  });

  it('cycleWound advances 0 → 1 and PUTs immediately', () => {
    const { result } = renderHook(() => useZombicideStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    mutateMock.mockClear();
    act(() => result.current.cycleWound('p1'));
    expect(current()?.survivors.p1).toBe(1);
    expect(mutateMock).toHaveBeenCalled(); // immediate, no timer advance
  });

  it('cycleWound folds in a missing player (0 → 1)', () => {
    const { result } = renderHook(() => useZombicideStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.cycleWound('pX'));
    expect(current()?.survivors.pX).toBe(1);
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => useZombicideStateEditor(SID, ['p1']));
    act(() => result.current.bumpZombie('walker', 1));
    act(() => result.current.cycleWound('p1'));
    expect(current()).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/zombicide/__tests__/use-zombicide-state-editor.test.tsx`
Expected: FAIL — `Cannot find module '../use-zombicide-state-editor'`.

- [ ] **Step 3: Write the implementation**

```ts
// use-zombicide-state-editor.ts
'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import {
  initialZombicideState,
  nextWoundLevel,
  parseZombicideGameState,
  type ZombicideGameState,
  type ZombieType,
} from './zombicide-state';

export interface ZombicideStateEditor {
  state: ZombicideGameState | null;
  initializeState: () => void;
  bumpZombie: (type: ZombieType, delta: 1 | -1) => void;
  cycleWound: (playerId: string) => void;
}

const clampMin = (n: number) => (n < 0 ? 0 : n);

export function useZombicideStateEditor(
  sessionId: string,
  playerIds: readonly string[]
): ZombicideStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parseZombicideGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: ZombicideGameState) => mutate(next),
    500
  );

  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: ZombicideGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  const commitImmediate = useCallback(
    (next: ZombicideGameState) => {
      commit(next);
      flush(); // wound taps must not be lost
    },
    [commit, flush]
  );

  const readState = useCallback(
    (): ZombicideGameState | null =>
      parseZombicideGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const initializeState = useCallback(
    () => commitImmediate(initialZombicideState(playerIds)),
    [commitImmediate, playerIds]
  );

  const bumpZombie = useCallback(
    (type: ZombieType, delta: 1 | -1) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, zombies: { ...cur.zombies, [type]: clampMin(cur.zombies[type] + delta) } });
    },
    [commit, readState]
  );

  const cycleWound = useCallback(
    (playerId: string) => {
      const cur = readState();
      if (cur == null) return;
      const currentLevel = cur.survivors[playerId] ?? 0;
      commitImmediate({ ...cur, survivors: { ...cur.survivors, [playerId]: nextWoundLevel(currentLevel) } });
    },
    [commitImmediate, readState]
  );

  return { state, initializeState, bumpZombie, cycleWound };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/zombicide/__tests__/use-zombicide-state-editor.test.tsx`
Expected: PASS (5 tests). (The `cycleWound` immediate-PUT test asserts `mutate` fires without advancing timers.)

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/zombicide/use-zombicide-state-editor.ts" "apps/web/src/components/features/session-live/flavors/zombicide/__tests__/use-zombicide-state-editor.test.tsx"
git commit -m "feat(session-live): #2793 Zombicide L2 host-edit hook (mixed cadence)"
```

---

## Task 3: Palette + ZombieHordePanel

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/zombicide/zombicide-palette.ts`
- Create: `apps/web/src/components/features/session-live/flavors/zombicide/ZombieHordePanel.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/zombicide/__tests__/ZombieHordePanel.test.tsx`

**Interfaces:**
- Consumes: `ZombieType`, `ZombieCounts`, `ZOMBIE_TYPES` from `./zombicide-state`; `zombicideWoundColor` from `./zombicide-palette`.
- Produces: `zombicideWoundColor(level: WoundLevel): string`; `ZombieHordePanel` with props
  ```ts
  interface ZombieHordePanelProps {
    zombies: ZombieCounts;
    editable: boolean;
    onBump?: (type: ZombieType, delta: 1 | -1) => void;
    labels: { heading: string; walker: string; runner: string; fatty: string; berserker: string; abomination: string; necromancer: string; incAria: string; decAria: string };
  }
  ```

- [ ] **Step 1: Write the palette**

```ts
// zombicide-palette.ts
import type { WoundLevel } from './zombicide-state';

// The 3 Zombicide wound levels — inline hsl() applied via `style` (like the sibling palettes).
// Any hue that trips meepleai/no-inline-hsl-v2 carries a line-level disable with a reason.
const WOUND_HSL: Record<WoundLevel, string> = {
  0: 'hsl(142, 55%, 42%)',
  1: 'hsl(38, 90%, 50%)',
  2: 'hsl(0, 70%, 48%)',
};

export function zombicideWoundColor(level: WoundLevel): string {
  return WOUND_HSL[level];
}
```

> After writing the palette, run `pnpm exec eslint --max-warnings=0` on it. If a hue trips `meepleai/no-inline-hsl-v2` (level 1 amber 38° is the likely one — it sits in the `agent` band), add a line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Zombicide wound level <n> colour, not the <entity> token` above it (mirror `catan-palette`). The pre-commit hook does NOT catch this.

- [ ] **Step 2: Write the failing test**

```tsx
// ZombieHordePanel.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ZombieHordePanel } from '../ZombieHordePanel';

const zombies = { walker: 5, runner: 2, fatty: 0, berserker: 1, abomination: 0, necromancer: 1 };
const labels = {
  heading: 'Orda', walker: 'Camminatore', runner: 'Corridore', fatty: 'Grasso',
  berserker: 'Berserker', abomination: 'Abominio', necromancer: 'Negromante',
  incAria: '{field} +1', decAria: '{field} -1',
};

describe('ZombieHordePanel', () => {
  it('renders all 6 zombie types with counts', () => {
    const { container } = render(<ZombieHordePanel zombies={zombies} editable={false} labels={labels} />);
    expect(container.querySelectorAll('[data-zombie]')).toHaveLength(6);
    expect(screen.getByText('Camminatore').closest('[data-zombie]')?.textContent).toContain('5');
  });

  it('read-only exposes no steppers', () => {
    render(<ZombieHordePanel zombies={zombies} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: walker +1 fires onBump', async () => {
    const onBump = vi.fn();
    render(<ZombieHordePanel zombies={zombies} editable onBump={onBump} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Camminatore +1' }));
    expect(onBump).toHaveBeenCalledWith('walker', 1);
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/zombicide/__tests__/ZombieHordePanel.test.tsx`
Expected: FAIL — `Cannot find module '../ZombieHordePanel'`.

- [ ] **Step 4: Write the implementation**

```tsx
// ZombieHordePanel.tsx
'use client';

import { type ReactElement } from 'react';

import { ZOMBIE_TYPES, type ZombieCounts, type ZombieType } from './zombicide-state';

export interface ZombieHordePanelProps {
  readonly zombies: ZombieCounts;
  readonly editable: boolean;
  readonly onBump?: (type: ZombieType, delta: 1 | -1) => void;
  readonly labels: {
    heading: string; walker: string; runner: string; fatty: string;
    berserker: string; abomination: string; necromancer: string;
    incAria: string; decAria: string;
  };
}

export function ZombieHordePanel({
  zombies, editable, onBump, labels,
}: ZombieHordePanelProps): ReactElement {
  const inc = (f: string) => labels.incAria.replace('{field}', f);
  const dec = (f: string) => labels.decAria.replace('{field}', f);
  const typeLabel: Record<ZombieType, string> = {
    walker: labels.walker, runner: labels.runner, fatty: labels.fatty,
    berserker: labels.berserker, abomination: labels.abomination, necromancer: labels.necromancer,
  };

  return (
    <section data-slot="zc-horde" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{labels.heading}</h3>
      <div className="grid grid-cols-2 gap-2">
        {ZOMBIE_TYPES.map(type => {
          const label = typeLabel[type];
          return (
            <div key={type} data-zombie={type}
              className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1 text-xs">
              <span className="min-w-0 flex-1 truncate text-muted-foreground">{label}</span>
              {editable && (
                <button type="button" data-dir="dec" aria-label={dec(label)} onClick={() => onBump?.(type, -1)}
                  className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted">−</button>
              )}
              <span className="min-w-4 text-center font-semibold tabular-nums text-foreground">{zombies[type]}</span>
              {editable && (
                <button type="button" data-dir="inc" aria-label={inc(label)} onClick={() => onBump?.(type, 1)}
                  className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted">+</button>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
}
```

- [ ] **Step 5: Run eslint + test**

```bash
cd apps/web && pnpm exec eslint --max-warnings=0 \
  src/components/features/session-live/flavors/zombicide/zombicide-palette.ts \
  src/components/features/session-live/flavors/zombicide/ZombieHordePanel.tsx
pnpm exec vitest run src/components/features/session-live/flavors/zombicide/__tests__/ZombieHordePanel.test.tsx
```
Expected: eslint clean (add a `no-inline-hsl-v2` disable in the palette for the amber level-1 hue if it trips — 38° is in the `agent` band, same as Paleo's wounded); test PASS (3 tests). The HordePanel uses only semantic tokens (no colour) so it lints clean.

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/zombicide/zombicide-palette.ts" "apps/web/src/components/features/session-live/flavors/zombicide/ZombieHordePanel.tsx" "apps/web/src/components/features/session-live/flavors/zombicide/__tests__/ZombieHordePanel.test.tsx"
git commit -m "feat(session-live): #2793 Zombicide L3 horde panel + wound palette"
```

---

## Task 4: SurvivorsPanel

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/zombicide/ZombicideSurvivorsPanel.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/zombicide/__tests__/ZombicideSurvivorsPanel.test.tsx`

**Interfaces:**
- Consumes: `WoundLevel` from `./zombicide-state`; `zombicideWoundColor` from `./zombicide-palette`; `LiveSessionPlayerDto` from `@/lib/api/schemas/live-sessions.schemas`.
- Produces: `ZombicideSurvivorsPanel` with props
  ```ts
  interface ZombicideSurvivorsPanelProps {
    players: LiveSessionPlayerDto[];
    survivors: Record<string, WoundLevel>;
    editable: boolean;
    onCycle?: (playerId: string) => void;
    labels: { heading: string; healthy: string; wounded: string; down: string; cycleAria: string /* "{name}: cambia ferite" */ };
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// ZombicideSurvivorsPanel.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ZombicideSurvivorsPanel } from '../ZombicideSurvivorsPanel';

const players = [
  { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 0, currentRank: 1, joinedAt: '', isActive: true },
  { id: 'p2', userId: null, displayName: 'Anna', avatarUrl: null, color: 'Blue', role: 'Player', teamId: null, totalScore: 0, currentRank: 2, joinedAt: '', isActive: true },
] as const;
const labels = { heading: 'Sopravvissuti', healthy: 'Illeso', wounded: 'Ferito', down: 'A terra', cycleAria: '{name}: cambia ferite' };

describe('ZombicideSurvivorsPanel', () => {
  it('renders a row per player with a wound badge', () => {
    const { container } = render(
      <ZombicideSurvivorsPanel players={players} survivors={{ p1: 0, p2: 1 }} editable={false} labels={labels} />
    );
    expect(container.querySelectorAll('[data-slot="zc-survivor-row"]')).toHaveLength(2);
    expect(screen.getByText('Marco')).toBeInTheDocument();
  });

  it('defaults a missing player to healthy (0)', () => {
    render(<ZombicideSurvivorsPanel players={players} survivors={{ p1: 2 }} editable={false} labels={labels} />);
    expect(screen.getByText('Illeso')).toBeInTheDocument(); // p2 absent → healthy
  });

  it('flags a down survivor (wounds=2)', () => {
    const { container } = render(<ZombicideSurvivorsPanel players={players} survivors={{ p1: 2, p2: 0 }} editable={false} labels={labels} />);
    expect(container.querySelector('[data-slot="zc-survivor-row"][data-down="true"]')).not.toBeNull();
  });

  it('read-only exposes no buttons', () => {
    render(<ZombicideSurvivorsPanel players={players} survivors={{ p1: 0, p2: 0 }} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: tapping a row fires onCycle with the player id', async () => {
    const onCycle = vi.fn();
    render(<ZombicideSurvivorsPanel players={players} survivors={{ p1: 0, p2: 0 }} editable onCycle={onCycle} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Marco: cambia ferite' }));
    expect(onCycle).toHaveBeenCalledWith('p1');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/zombicide/__tests__/ZombicideSurvivorsPanel.test.tsx`
Expected: FAIL — `Cannot find module '../ZombicideSurvivorsPanel'`.

- [ ] **Step 3: Write the implementation**

```tsx
// ZombicideSurvivorsPanel.tsx
'use client';

import { type ReactElement } from 'react';

import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { zombicideWoundColor } from './zombicide-palette';
import type { WoundLevel } from './zombicide-state';

export interface ZombicideSurvivorsPanelProps {
  readonly players: LiveSessionPlayerDto[];
  readonly survivors: Record<string, WoundLevel>;
  readonly editable: boolean;
  readonly onCycle?: (playerId: string) => void;
  readonly labels: {
    heading: string; healthy: string; wounded: string; down: string; cycleAria: string;
  };
}

export function ZombicideSurvivorsPanel({
  players, survivors, editable, onCycle, labels,
}: ZombicideSurvivorsPanelProps): ReactElement {
  const woundLabel = (w: WoundLevel): string =>
    w === 0 ? labels.healthy : w === 1 ? labels.wounded : labels.down;

  return (
    <section data-slot="zc-survivors" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{labels.heading}</h3>
      <ul role="list" className="flex flex-col gap-1">
        {players.map(player => {
          const wounds = survivors[player.id] ?? 0;
          const isDown = wounds === 2;
          const badge = (
            <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold"
              style={{ backgroundColor: zombicideWoundColor(wounds), color: 'hsl(0, 0%, 100%)' }}>
              {woundLabel(wounds)}
            </span>
          );
          return (
            <li key={player.id} data-slot="zc-survivor-row" data-wounds={String(wounds)} data-down={String(isDown)}
              className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1">
              <span className={`min-w-0 flex-1 truncate text-xs font-medium ${isDown ? 'text-muted-foreground line-through' : 'text-foreground'}`}>
                {player.displayName}
              </span>
              {editable ? (
                <button type="button" aria-label={labels.cycleAria.replace('{name}', player.displayName)}
                  onClick={() => onCycle?.(player.id)} className="rounded hover:opacity-80">
                  {badge}
                </button>
              ) : (
                badge
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
```

> `text-white` is NOT used — white is inline `style.color` on a coloured `backgroundColor` (the `.e-bg` pattern). Run `pnpm exec eslint --max-warnings=0` before committing.

- [ ] **Step 4: Run eslint + test**

```bash
cd apps/web && pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/zombicide/ZombicideSurvivorsPanel.tsx
pnpm exec vitest run src/components/features/session-live/flavors/zombicide/__tests__/ZombicideSurvivorsPanel.test.tsx
```
Expected: eslint clean; test PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/zombicide/ZombicideSurvivorsPanel.tsx" "apps/web/src/components/features/session-live/flavors/zombicide/__tests__/ZombicideSurvivorsPanel.test.tsx"
git commit -m "feat(session-live): #2793 Zombicide L3 survivors wound panel"
```

---

## Task 5: ZombicideLiveFlavor container (self-builds labels)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/zombicide/ZombicideLiveFlavor.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/zombicide/__tests__/ZombicideLiveFlavor.test.tsx`

**Interfaces:**
- Consumes: the 2 panels; `useZombicideStateEditor`; `hasRequiredRole`, `ParticipantRole`; `LiveSessionDto`; `useIntl` + `useTranslation`.
- Produces: `ZombicideLiveFlavor` + `ZombicideLiveFlavorProps` (game-agnostic `FlavorProps`): `{ session, viewerRole, sessionId, className?, livePoints?, phaseName? }`.

- [ ] **Step 1: Write the failing test**

```tsx
// ZombicideLiveFlavor.test.tsx
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { ZombicideLiveFlavor } from '../ZombicideLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { initialZombicideState } from '../zombicide-state';

expect.extend(toHaveNoViolations);
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({ useUpdateLiveGameState: () => ({ mutate: vi.fn() }) }));

const session = {
  id: 's1', sessionCode: 'ABC', gameId: null, gameName: 'Zombicide', gameSlug: 'zombicide',
  createdByUserId: 'u1', status: 'InProgress', visibility: 'Private', groupId: null,
  createdAt: '', startedAt: '', pausedAt: null, completedAt: null, updatedAt: '', lastSavedAt: null,
  currentTurnIndex: 0, currentTurnPlayerId: 'p1', agentMode: 'None', notes: null,
  players: [
    { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 5, currentRank: 1, joinedAt: '', isActive: true },
    { id: 'p2', userId: null, displayName: 'Anna', avatarUrl: null, color: 'Blue', role: 'Player', teamId: null, totalScore: 3, currentRank: 2, joinedAt: '', isActive: false },
  ],
  teams: [], roundScores: [], scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

function renderFlavor(props: Partial<Parameters<typeof ZombicideLiveFlavor>[0]> = {}) {
  return render(
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <ZombicideLiveFlavor session={session} viewerRole="Player" sessionId="s1" {...props} />
    </IntlProvider>
  );
}
beforeEach(() => useLiveSessionStore.getState().reset());

describe('ZombicideLiveFlavor', () => {
  it('renders the leaderboard with null gameState; no panels', () => {
    const { container } = renderFlavor();
    expect(container.querySelectorAll('[data-slot="zc-leaderboard-row"]')).toHaveLength(2);
    expect(container.querySelector('[data-slot="zc-horde"]')).toBeNull();
  });

  it('host sees the init CTA when gameState is null', () => {
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="zc-init"]')).not.toBeNull();
  });

  it('renders horde + survivors panels when gameState is present', () => {
    useLiveSessionStore.getState().setGameState(initialZombicideState(['p1', 'p2']));
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="zc-horde"]')).not.toBeNull();
    expect(container.querySelectorAll('[data-slot="zc-survivor-row"]')).toHaveLength(2);
  });

  it('has no axe violations (host, populated)', async () => {
    useLiveSessionStore.getState().setGameState(initialZombicideState(['p1', 'p2']));
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/zombicide/__tests__/ZombicideLiveFlavor.test.tsx`
Expected: FAIL — `Cannot find module '../ZombicideLiveFlavor'`.

- [ ] **Step 3: Write the implementation**

```tsx
// ZombicideLiveFlavor.tsx
'use client';

import { type ReactElement } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { useZombicideStateEditor } from './use-zombicide-state-editor';
import { ZombieHordePanel } from './ZombieHordePanel';
import { ZombicideSurvivorsPanel } from './ZombicideSurvivorsPanel';

export interface ZombicideLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.zombicide';

export function ZombicideLiveFlavor({
  session, viewerRole, sessionId, className, livePoints,
}: ZombicideLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const playerIds = session.players.map(p => p.id);
  const editor = useZombicideStateEditor(sessionId, playerIds);
  const state = editor.state;

  const tmpl = (id: string, fallback: string) => (intl.messages[`${K}.${id}`] as string) ?? fallback;
  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));

  return (
    <section aria-label={t(`${K}.panelAriaLabel`)} data-slot="zc-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}>
      {/* Leaderboard (ungated — from scoring) */}
      <div data-slot="zc-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t(`${K}.leaderboardHeading`)}</h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li key={player.id} data-slot="zc-leaderboard-row"
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
          <ZombieHordePanel zombies={state.zombies} editable={isHost}
            onBump={editor.bumpZombie}
            labels={{ heading: t(`${K}.hordeHeading`), walker: t(`${K}.walker`), runner: t(`${K}.runner`), fatty: t(`${K}.fatty`), berserker: t(`${K}.berserker`), abomination: t(`${K}.abomination`), necromancer: t(`${K}.necromancer`), incAria: tmpl('incAria', '{field} +1'), decAria: tmpl('decAria', '{field} -1') }} />
          <ZombicideSurvivorsPanel players={session.players} survivors={state.survivors} editable={isHost}
            onCycle={editor.cycleWound}
            labels={{ heading: t(`${K}.survivorsHeading`), healthy: t(`${K}.healthy`), wounded: t(`${K}.wounded`), down: t(`${K}.down`), cycleAria: tmpl('cycleAria', '{name}: change wounds') }} />
          {isHost && (
            <button type="button" onClick={editor.initializeState}
              className="self-start text-xs text-muted-foreground underline hover:text-foreground">
              {t(`${K}.resetCta`)}
            </button>
          )}
        </>
      ) : isHost ? (
        <button type="button" data-slot="zc-init" onClick={editor.initializeState}
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
cd apps/web && pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/zombicide/ZombicideLiveFlavor.tsx
pnpm exec vitest run src/components/features/session-live/flavors/zombicide/__tests__/ZombicideLiveFlavor.test.tsx
```
Expected: eslint clean; test PASS (4 tests incl. axe).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/zombicide/ZombicideLiveFlavor.tsx" "apps/web/src/components/features/session-live/flavors/zombicide/__tests__/ZombicideLiveFlavor.test.tsx"
git commit -m "feat(session-live): #2793 Zombicide L3 flavor container (self-builds labels)"
```

---

## Task 6: Wire into the registry + i18n

**⚠️ Run this task SEQUENTIALLY after Task 5 is committed — NOT in parallel with it (a shared worktree produces twin commits when both touch files the other type-checks against).**

**Files:**
- Modify: `apps/web/src/components/features/session-live/FlavorRenderer.tsx`
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Test: `apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx` (extend)

- [ ] **Step 1: Add the FLAVOR_MAP entry**

In `FlavorRenderer.tsx`, add a module-scope lazy component (match the paleo/power-grid pattern), then add the map entry:

```tsx
const ZombicideLiveFlavorLazy: FlavorComponent = dynamic(
  () => import('./flavors/zombicide/ZombicideLiveFlavor').then(m => ({ default: m.ZombicideLiveFlavor })),
  { ssr: false, loading: () => <FlavorLoadingSkeleton /> }
);
```
and add `'zombicide': { live: ZombicideLiveFlavorLazy }` to `FLAVOR_MAP`.

- [ ] **Step 2: Extend the FlavorRenderer guard test**

Add inside the `hasFlavor` describe: `expect(hasFlavor('zombicide')).toBe(true);`

- [ ] **Step 3: Add the i18n keys**

In `src/locales/it.json`, under `pages.sessionLive.flavor`, add a `"zombicide"` sibling:

```json
"zombicide": {
  "panelAriaLabel": "Zombicide",
  "leaderboardHeading": "Classifica",
  "initBoardCta": "Inizia partita",
  "resetCta": "Reimposta stato",
  "viewerWaiting": "In attesa dell'host…",
  "hordeHeading": "Orda",
  "walker": "Camminatore",
  "runner": "Corridore",
  "fatty": "Grasso",
  "berserker": "Berserker",
  "abomination": "Abominio",
  "necromancer": "Negromante",
  "survivorsHeading": "Sopravvissuti",
  "healthy": "Illeso",
  "wounded": "Ferito",
  "down": "A terra",
  "incAria": "{field} +1",
  "decAria": "{field} -1",
  "cycleAria": "{name}: cambia ferite"
}
```

Mirror in `src/locales/en.json` with English copy (`"leaderboardHeading": "Standings"`, `"initBoardCta": "Start game"`, `"resetCta": "Reset state"`, `"viewerWaiting": "Waiting for the host…"`, `"hordeHeading": "Horde"`, `"walker": "Walker"`, `"runner": "Runner"`, `"fatty": "Fatty"`, `"berserker": "Berserker"`, `"abomination": "Abomination"`, `"necromancer": "Necromancer"`, `"survivorsHeading": "Survivors"`, `"healthy": "Healthy"`, `"wounded": "Wounded"`, `"down": "Down"`, `"cycleAria": "{name}: change wounds"`; `incAria`/`decAria` identical). BOTH locales MUST have the identical key set.

- [ ] **Step 4: Typecheck + run affected suites + eslint**

```bash
cd apps/web
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run src/components/features/session-live/flavors/zombicide src/components/features/session-live/__tests__/FlavorRenderer.test.tsx
pnpm exec eslint --max-warnings=0 src/components/features/session-live/FlavorRenderer.tsx
```
Expected: typecheck clean; all zombicide suites pass + the guard test's new assertion passes; eslint clean. (`FlavorRenderer.test.tsx > "lazy-loads the Catan flavor"` is a KNOWN pre-existing baseline flake in isolated single-file runs — unrelated.)

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/FlavorRenderer.tsx" "apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx" "apps/web/src/locales/it.json" "apps/web/src/locales/en.json"
git commit -m "feat(session-live): #2793 wire Zombicide flavor into the registry + i18n"
```

---

## Task 7: Final verification

- [ ] **Step 1: Full typecheck + all flavor suites**

```bash
cd apps/web
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run src/components/features/session-live/flavors src/components/features/session-live/__tests__/FlavorRenderer.test.tsx
```
Expected: typecheck clean; all catan + wingspan + codenames + puerto-rico + paleo + power-grid + zombicide flavor tests pass (the Catan lazy-load isolated flake aside).

- [ ] **Step 2: Lint the whole flavor dir**

```bash
pnpm exec eslint --max-warnings=0 "src/components/features/session-live/flavors/zombicide/**/*.{ts,tsx}" src/components/features/session-live/FlavorRenderer.tsx
```
Expected: no errors.

- [ ] **Step 3: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2793-zombicide-l2-l3
gh pr create --base main-dev --head feature/issue-2793-zombicide-l2-l3 \
  --title "feat(session-live): #2793 Zombicide L2+L3 live flavor (horde + wounds)" \
  --body "Implements the Zombicide live flavor per docs/superpowers/specs/2026-07-17-zombicide-l2-l3-flavor-design.md. FE-only; reuses the game-agnostic plumbing; mixed-cadence editor (zombie debounced / wound immediate). Closes #2793."
```

---

## Self-Review

**1. Spec coverage:**
- L2 schema + helpers (fixed zombies object, wound-level union rejecting 3, nextWoundLevel) → Task 1. ✅
- Editor MIXED cadence (bumpZombie debounced clamp, cycleWound immediate + missing-player fold) → Task 2. ✅
- HordePanel + palette → Task 3. SurvivorsPanel (down flag) → Task 4. ✅
- Container (leaderboard ungated, panels gated, self-builds labels) → Task 5. ✅
- Wiring (FLAVOR_MAP + i18n, SEQUENTIAL after Task 5) → Task 6. ✅
- VP-from-scoring invariant → Tasks 4/5 (read `livePoints`/`totalScore`, never gameState). ✅
- Lint gate per task → Steps in Tasks 3–6 + Task 7. ✅
- Testing (unit + component + jest-axe) → Tasks 1–5. ✅

**2. Placeholder scan:** No TBD/TODO; every code step has complete code. ✅

**3. Type consistency:** `ZombicideGameState`, `ZombieCounts`, `ZombieType`, `WoundLevel`, `parseZombicideGameState`, `emptyZombieCounts`, `initialZombicideState`, `nextWoundLevel`, `ZOMBIE_TYPES`, `useZombicideStateEditor` signature, and the 2 component prop interfaces are used consistently Task 1→6. The container's panel labels match `ZombieHordePanelProps['labels']` / `ZombicideSurvivorsPanelProps['labels']`; editor mutator names (`bumpZombie`/`cycleWound`) match the container's `onBump`/`onCycle` wiring. ✅

**Known follow-ups (out of scope):** the skill tree / AP / equip / spawn deck (all cut); a Zombicide summary flavor.
