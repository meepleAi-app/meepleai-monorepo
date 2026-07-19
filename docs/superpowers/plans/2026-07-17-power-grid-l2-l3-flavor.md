# Power Grid L2+L3 Flavor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Power Grid live flavor — an 8-slot power-plant market (immediate PUT) + 4 resource-market counters (debounced PUT) — on the L1 game-state layer, reusing the generalized flavor plumbing.

**Architecture:** FE-only. Reuses the game-agnostic pattern (`flavors/<game>/`; `FlavorRenderer` dispatches on `FLAVOR_MAP` with `FlavorProps`; each flavor self-builds i18n labels). Plants + resources live in `gameState`; cities-powered/VP stays in the existing scoring system. Host-entered, no engine. **Mixed PUT cadence:** plant-slot edits PUT immediately, resource ± debounced 500 ms.

**Tech Stack:** Next.js 16 · React 19 · TypeScript · Zod · Zustand (`useLiveSessionStore`) · TanStack Query (`useUpdateLiveGameState`) · `useDebouncedCallback` (trailing debounce with `flush()`) · react-intl (`useIntl`) + `@/hooks/useTranslation` · Vitest + Testing Library + jest-axe · Tailwind semantic tokens (resource colours inline `hsl()` via a palette module).

## Global Constraints

- **Issue:** #2791 (G6e, epic #3025). Spec: `docs/superpowers/specs/2026-07-17-power-grid-l2-l3-flavor-design.md`.
- **Zero backend changes.** `gameState` is the opaque L1 blob; scores use the existing scoring editor.
- **State schema:** `v: 1`, `game: 'power-grid'` (exact hyphenated slug). `parsePowerGridGameState` returns `null` (never throws) on wrong game/version/shape via Zod `safeParse`.
- **`gameState` shape:** `{ v, game, plants:{current:(number|null)[4], future:(number|null)[4]}, resources:{coal,oil,garbage,uranium} }`. Never scores.
- **`resources` is a fixed `z.object` with all 4 keys required** (each `int>=0`), NOT `z.record`. Each plant bank is `z.array(z.number().int().min(0).nullable()).length(4)`.
- **Mixed cadence:** `bumpResource` → DEBOUNCED 500 ms; `setPlant` → IMMEDIATE (`commit(next)` then `flush()`). Both do optimistic `setGameState(next)` FIRST. `readState()` re-parses the store FRESH per call. `useEffect(()=>()=>flush(),[flush])` flushes on unmount. Resource bumps clamp `>=0`; plant clamps `>=0` when non-null; a NaN/negative `<input>` ⇒ `null`.
- **`useDebouncedCallback(fn, 500)` returns `[debouncedFn, flush]`** — a trailing debounce holding only the latest arg; `flush()` runs the pending call immediately. The immediate path relies on this: `commit(next)` schedules `debouncedMutate(next)` (replacing any pending), then `flush()` sends the full fresh state — no stale-PUT race.
- **VP/cities-powered stays in scoring** (`livePoints`/`totalScore`); gameState never carries it. **Leaderboard renders ungated**; panels gate on gameState (host CTA when null). **Host-edit only** (`viewerRole === 'Host'` via `hasRequiredRole`).
- **Flavors self-build i18n labels** via `useIntl` + `useTranslation`; templated strings via `intl.messages[id] as string ?? fallback`, static via `t(id)`.
- **Colours:** semantic Tailwind tokens EXCEPT the 4 resource colours → inline `hsl()` via the palette. `text-white` only on a cell that ALSO sets an inline coloured `backgroundColor` (put white in inline `style.color`, NOT the `text-white` utility).
- **LINT GATE (critical — implementers + the pre-commit hook miss it):** after each component/palette task run `pnpm exec eslint --max-warnings=0 <file>`. The pre-commit hook does NOT run `meepleai/no-inline-hsl-v2` nor the `style`-prop case of `local/no-hardcoded-color-utility`. Inline `hsl()` that trips `no-inline-hsl-v2` gets a line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- <reason>`.
- **Tests:** Vitest, TDD, pristine. Query via `data-slot`/roles, not `getByTestId`. Files under `apps/web/src/components/features/session-live/flavors/power-grid/`. Run from `apps/web`.
- **Windows:** pre-commit runs `pnpm typecheck` (~2 min, sometimes slower) — allow ≥9 min for commits; if TS2307 on stale `.next/types`, `rm -rf .next/types` first (never `--no-verify`).

## File Structure

Create under `flavors/power-grid/`: `power-grid-state.ts`, `power-grid-palette.ts`, `use-power-grid-state-editor.ts`, `PowerGridPlantMarketPanel.tsx`, `PowerGridResourceMarketPanel.tsx`, `PowerGridLiveFlavor.tsx`, `__tests__/*`.
Modify: `session-live/FlavorRenderer.tsx`, `src/locales/it.json` + `en.json`.

---

## Task 1: L2 state schema + helpers

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/power-grid/power-grid-state.ts`
- Test: `apps/web/src/components/features/session-live/flavors/power-grid/__tests__/power-grid-state.test.ts`

**Interfaces:**
- Produces: `PowerGridGameState`, `PowerGridResources`, `PowerGridResource`, `PowerGridPlantBank` types; `parsePowerGridGameState(raw): PowerGridGameState | null`; `emptyPowerGridResources(): PowerGridResources`; `initialPowerGridState(): PowerGridGameState`; `POWER_GRID_RESOURCES`, `POWER_GRID_PLANT_BANKS`, `POWER_GRID_STATE_VERSION = 1`.

- [ ] **Step 1: Write the failing test**

```ts
// power-grid-state.test.ts
import { describe, expect, it } from 'vitest';

import {
  POWER_GRID_PLANT_BANKS,
  POWER_GRID_RESOURCES,
  emptyPowerGridResources,
  initialPowerGridState,
  parsePowerGridGameState,
} from '../power-grid-state';

const VALID = {
  v: 1, game: 'power-grid',
  plants: { current: [3, 4, null, 6], future: [null, null, null, null] },
  resources: { coal: 5, oil: 2, garbage: 0, uranium: 1 },
};

describe('parsePowerGridGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parsePowerGridGameState(VALID);
    expect(parsed?.plants.current[0]).toBe(3);
    expect(parsed?.plants.current[2]).toBeNull();
    expect(parsed?.resources.coal).toBe(5);
  });
  it('returns null for a different game', () => {
    expect(parsePowerGridGameState({ ...VALID, game: 'catan' })).toBeNull();
  });
  it('returns null for a future version', () => {
    expect(parsePowerGridGameState({ ...VALID, v: 2 })).toBeNull();
  });
  it('returns null when a plant bank is not length 4', () => {
    expect(parsePowerGridGameState({ ...VALID, plants: { current: [1, 2], future: [null, null, null, null] } })).toBeNull();
  });
  it('returns null when a resource is missing', () => {
    expect(parsePowerGridGameState({ ...VALID, resources: { coal: 1, oil: 0, garbage: 2 } })).toBeNull();
  });
  it('returns null for malformed / non-object', () => {
    expect(parsePowerGridGameState(null)).toBeNull();
    expect(parsePowerGridGameState('x')).toBeNull();
  });
});

describe('emptyPowerGridResources', () => {
  it('is all zero', () => {
    expect(emptyPowerGridResources()).toEqual({ coal: 0, oil: 0, garbage: 0, uranium: 0 });
  });
});

describe('initialPowerGridState', () => {
  it('seeds 4 null slots per bank + 0 resources', () => {
    const s = initialPowerGridState();
    expect(s.plants.current).toEqual([null, null, null, null]);
    expect(s.plants.future).toEqual([null, null, null, null]);
    expect(s.resources).toEqual({ coal: 0, oil: 0, garbage: 0, uranium: 0 });
    expect(s.game).toBe('power-grid');
  });
});

describe('constants', () => {
  it('lists the 4 resources and 2 banks in order', () => {
    expect(POWER_GRID_RESOURCES).toEqual(['coal', 'oil', 'garbage', 'uranium']);
    expect(POWER_GRID_PLANT_BANKS).toEqual(['current', 'future']);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/power-grid/__tests__/power-grid-state.test.ts`
Expected: FAIL — `Cannot find module '../power-grid-state'`.

- [ ] **Step 3: Write the implementation**

```ts
// power-grid-state.ts
import { z } from 'zod';

export const POWER_GRID_STATE_VERSION = 1;
export const POWER_GRID_RESOURCES = ['coal', 'oil', 'garbage', 'uranium'] as const;
export const POWER_GRID_PLANT_BANKS = ['current', 'future'] as const;

export const PowerGridResourceSchema = z.enum(POWER_GRID_RESOURCES);
export type PowerGridResource = z.infer<typeof PowerGridResourceSchema>;
export type PowerGridPlantBank = (typeof POWER_GRID_PLANT_BANKS)[number];

const nn = () => z.number().int().min(0);
const bank = () => z.array(z.number().int().min(0).nullable()).length(4);

export const PowerGridResourcesSchema = z.object({
  coal: nn(), oil: nn(), garbage: nn(), uranium: nn(),
});
export type PowerGridResources = z.infer<typeof PowerGridResourcesSchema>;

export const PowerGridGameStateSchema = z.object({
  v: z.literal(POWER_GRID_STATE_VERSION),
  game: z.literal('power-grid'),
  plants: z.object({ current: bank(), future: bank() }),
  resources: PowerGridResourcesSchema,
});
export type PowerGridGameState = z.infer<typeof PowerGridGameStateSchema>;

export function parsePowerGridGameState(raw: unknown): PowerGridGameState | null {
  const result = PowerGridGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function emptyPowerGridResources(): PowerGridResources {
  return { coal: 0, oil: 0, garbage: 0, uranium: 0 };
}

export function initialPowerGridState(): PowerGridGameState {
  return {
    v: POWER_GRID_STATE_VERSION,
    game: 'power-grid',
    plants: { current: [null, null, null, null], future: [null, null, null, null] },
    resources: emptyPowerGridResources(),
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/power-grid/__tests__/power-grid-state.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/power-grid/power-grid-state.ts" "apps/web/src/components/features/session-live/flavors/power-grid/__tests__/power-grid-state.test.ts"
git commit -m "feat(session-live): #2791 Power Grid L2 state schema + helpers"
```

---

## Task 2: Host-edit hook (mixed cadence)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/power-grid/use-power-grid-state-editor.ts`
- Test: `apps/web/src/components/features/session-live/flavors/power-grid/__tests__/use-power-grid-state-editor.test.tsx`

**Interfaces:**
- Consumes: `PowerGridGameState`, `PowerGridResource`, `PowerGridPlantBank`, `parsePowerGridGameState`, `initialPowerGridState` from `./power-grid-state`; `useLiveSessionStore`, `useUpdateLiveGameState`, `useDebouncedCallback`.
- Produces: `usePowerGridStateEditor(sessionId: string): PowerGridStateEditor` where
  ```ts
  interface PowerGridStateEditor {
    state: PowerGridGameState | null;
    initializeState: () => void;
    bumpResource: (field: PowerGridResource, delta: 1 | -1) => void;       // debounced
    setPlant: (bank: PowerGridPlantBank, index: number, plant: number | null) => void; // immediate
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// use-power-grid-state-editor.test.tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { usePowerGridStateEditor } from '../use-power-grid-state-editor';
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
  return useLiveSessionStore.getState().gameState as import('../power-grid-state').PowerGridGameState | null;
}

describe('usePowerGridStateEditor', () => {
  it('initializeState seeds 8 null slots + 0 resources', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.initializeState());
    expect(current()?.plants.current).toEqual([null, null, null, null]);
    expect(current()?.resources).toEqual({ coal: 0, oil: 0, garbage: 0, uranium: 0 });
  });

  it('bumpResource clamps at 0', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.bumpResource('coal', -1));
    expect(current()?.resources.coal).toBe(0);
    act(() => result.current.bumpResource('coal', 1));
    expect(current()?.resources.coal).toBe(1);
  });

  it('setPlant sets a number, clears with null, clamps negative to 0', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setPlant('current', 1, 15));
    expect(current()?.plants.current[1]).toBe(15);
    act(() => result.current.setPlant('current', 1, null));
    expect(current()?.plants.current[1]).toBeNull();
    act(() => result.current.setPlant('future', 0, -4));
    expect(current()?.plants.future[0]).toBe(0);
  });

  it('setPlant out-of-range index is a no-op', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setPlant('current', 9, 5));
    expect(current()?.plants.current).toEqual([null, null, null, null]);
  });

  it('setPlant PUTs immediately (no debounce wait)', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.initializeState());
    mutateMock.mockClear();
    act(() => result.current.setPlant('current', 0, 7));
    expect(mutateMock).toHaveBeenCalled(); // immediate, without advancing timers
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.bumpResource('coal', 1));
    act(() => result.current.setPlant('current', 0, 3));
    expect(current()).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/power-grid/__tests__/use-power-grid-state-editor.test.tsx`
Expected: FAIL — `Cannot find module '../use-power-grid-state-editor'`.

- [ ] **Step 3: Write the implementation**

```ts
// use-power-grid-state-editor.ts
'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import {
  initialPowerGridState,
  parsePowerGridGameState,
  type PowerGridGameState,
  type PowerGridPlantBank,
  type PowerGridResource,
} from './power-grid-state';

export interface PowerGridStateEditor {
  state: PowerGridGameState | null;
  initializeState: () => void;
  bumpResource: (field: PowerGridResource, delta: 1 | -1) => void;
  setPlant: (bank: PowerGridPlantBank, index: number, plant: number | null) => void;
}

const clampMin = (n: number) => (n < 0 ? 0 : n);

export function usePowerGridStateEditor(sessionId: string): PowerGridStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parsePowerGridGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: PowerGridGameState) => mutate(next),
    500
  );

  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: PowerGridGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  const commitImmediate = useCallback(
    (next: PowerGridGameState) => {
      commit(next);
      flush(); // send the full fresh state now (plant edits must not be lost)
    },
    [commit, flush]
  );

  const readState = useCallback(
    (): PowerGridGameState | null =>
      parsePowerGridGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const initializeState = useCallback(
    () => commitImmediate(initialPowerGridState()),
    [commitImmediate]
  );

  const bumpResource = useCallback(
    (field: PowerGridResource, delta: 1 | -1) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, resources: { ...cur.resources, [field]: clampMin(cur.resources[field] + delta) } });
    },
    [commit, readState]
  );

  const setPlant = useCallback(
    (bankName: PowerGridPlantBank, index: number, plant: number | null) => {
      const cur = readState();
      if (cur == null || index < 0 || index >= cur.plants[bankName].length) return;
      const value = plant == null ? null : clampMin(Math.trunc(plant));
      const nextBank = cur.plants[bankName].map((p, i) => (i === index ? value : p));
      commitImmediate({ ...cur, plants: { ...cur.plants, [bankName]: nextBank } });
    },
    [commitImmediate, readState]
  );

  return { state, initializeState, bumpResource, setPlant };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/power-grid/__tests__/use-power-grid-state-editor.test.tsx`
Expected: PASS (6 tests). (`initializeState` uses the immediate path so the null-guard test's later `initializeState`-free calls stay null; the immediate-PUT test asserts `mutate` fires without advancing timers.)

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/power-grid/use-power-grid-state-editor.ts" "apps/web/src/components/features/session-live/flavors/power-grid/__tests__/use-power-grid-state-editor.test.tsx"
git commit -m "feat(session-live): #2791 Power Grid L2 host-edit hook (mixed cadence)"
```

---

## Task 3: Palette + ResourceMarketPanel

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/power-grid/power-grid-palette.ts`
- Create: `apps/web/src/components/features/session-live/flavors/power-grid/PowerGridResourceMarketPanel.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/power-grid/__tests__/PowerGridResourceMarketPanel.test.tsx`

**Interfaces:**
- Consumes: `PowerGridResource`, `PowerGridResources`, `POWER_GRID_RESOURCES` from `./power-grid-state`; `powerGridResourceColor` from `./power-grid-palette`.
- Produces: `powerGridResourceColor(resource: PowerGridResource): string`; `PowerGridResourceMarketPanel` with props
  ```ts
  interface PowerGridResourceMarketPanelProps {
    resources: PowerGridResources;
    editable: boolean;
    onBump?: (field: PowerGridResource, delta: 1 | -1) => void;
    labels: { heading: string; coal: string; oil: string; garbage: string; uranium: string; incAria: string; decAria: string };
  }
  ```

- [ ] **Step 1: Write the palette**

```ts
// power-grid-palette.ts
import type { PowerGridResource } from './power-grid-state';

// The 4 Power Grid resources — inline hsl() applied via `style` (like the sibling palettes).
// Any hue that trips meepleai/no-inline-hsl-v2 carries a line-level disable with a reason.
const RESOURCE_HSL: Record<PowerGridResource, string> = {
  coal: 'hsl(25, 30%, 30%)',
  oil: 'hsl(0, 0%, 18%)',
  garbage: 'hsl(75, 45%, 42%)',
  uranium: 'hsl(0, 70%, 48%)',
};

export function powerGridResourceColor(resource: PowerGridResource): string {
  return RESOURCE_HSL[resource];
}
```

> After writing the palette, run `pnpm exec eslint --max-warnings=0` on it. If a hue trips `meepleai/no-inline-hsl-v2`, add a line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Power Grid <resource> colour, not the <entity> token` above it (mirror `catan-palette`). The pre-commit hook does NOT catch this.

- [ ] **Step 2: Write the failing test**

```tsx
// PowerGridResourceMarketPanel.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PowerGridResourceMarketPanel } from '../PowerGridResourceMarketPanel';

const resources = { coal: 5, oil: 2, garbage: 0, uranium: 1 };
const labels = {
  heading: 'Mercato risorse', coal: 'Carbone', oil: 'Petrolio', garbage: 'Rifiuti', uranium: 'Uranio',
  incAria: '{field} +1', decAria: '{field} -1',
};

describe('PowerGridResourceMarketPanel', () => {
  it('renders all 4 resources with counts', () => {
    const { container } = render(<PowerGridResourceMarketPanel resources={resources} editable={false} labels={labels} />);
    expect(container.querySelectorAll('[data-resource]')).toHaveLength(4);
    expect(screen.getByText('Carbone').closest('[data-resource]')?.textContent).toContain('5');
  });

  it('read-only exposes no steppers', () => {
    render(<PowerGridResourceMarketPanel resources={resources} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: coal +1 fires onBump', async () => {
    const onBump = vi.fn();
    render(<PowerGridResourceMarketPanel resources={resources} editable onBump={onBump} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Carbone +1' }));
    expect(onBump).toHaveBeenCalledWith('coal', 1);
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/power-grid/__tests__/PowerGridResourceMarketPanel.test.tsx`
Expected: FAIL — `Cannot find module '../PowerGridResourceMarketPanel'`.

- [ ] **Step 4: Write the implementation**

```tsx
// PowerGridResourceMarketPanel.tsx
'use client';

import { type ReactElement } from 'react';

import { powerGridResourceColor } from './power-grid-palette';
import { POWER_GRID_RESOURCES, type PowerGridResource, type PowerGridResources } from './power-grid-state';

export interface PowerGridResourceMarketPanelProps {
  readonly resources: PowerGridResources;
  readonly editable: boolean;
  readonly onBump?: (field: PowerGridResource, delta: 1 | -1) => void;
  readonly labels: {
    heading: string; coal: string; oil: string; garbage: string; uranium: string;
    incAria: string; decAria: string;
  };
}

export function PowerGridResourceMarketPanel({
  resources, editable, onBump, labels,
}: PowerGridResourceMarketPanelProps): ReactElement {
  const inc = (f: string) => labels.incAria.replace('{field}', f);
  const dec = (f: string) => labels.decAria.replace('{field}', f);
  const rows: Array<[PowerGridResource, string]> = [
    ['coal', labels.coal], ['oil', labels.oil], ['garbage', labels.garbage], ['uranium', labels.uranium],
  ];

  return (
    <section data-slot="pg-resources" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{labels.heading}</h3>
      <div className="grid grid-cols-2 gap-2">
        {rows.map(([field, label]) => (
          <div key={field} data-resource={field}
            className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1 text-xs">
            <span aria-hidden="true" className="h-3 w-3 shrink-0 rounded-full"
              style={{ backgroundColor: powerGridResourceColor(field) }} />
            <span className="min-w-0 flex-1 truncate text-muted-foreground">{label}</span>
            {editable && (
              <button type="button" data-dir="dec" aria-label={dec(label)} onClick={() => onBump?.(field, -1)}
                className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted">−</button>
            )}
            <span className="min-w-4 text-center font-semibold tabular-nums text-foreground">{resources[field]}</span>
            {editable && (
              <button type="button" data-dir="inc" aria-label={inc(label)} onClick={() => onBump?.(field, 1)}
                className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted">+</button>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 5: Run eslint + test**

```bash
cd apps/web && pnpm exec eslint --max-warnings=0 \
  src/components/features/session-live/flavors/power-grid/power-grid-palette.ts \
  src/components/features/session-live/flavors/power-grid/PowerGridResourceMarketPanel.tsx
pnpm exec vitest run src/components/features/session-live/flavors/power-grid/__tests__/PowerGridResourceMarketPanel.test.tsx
```
Expected: eslint clean (add a `no-inline-hsl-v2` disable in the palette per resource hue that trips it — likely `garbage` at 75° near a lime/toolkit band and/or `uranium` red; check the output); test PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/power-grid/power-grid-palette.ts" "apps/web/src/components/features/session-live/flavors/power-grid/PowerGridResourceMarketPanel.tsx" "apps/web/src/components/features/session-live/flavors/power-grid/__tests__/PowerGridResourceMarketPanel.test.tsx"
git commit -m "feat(session-live): #2791 Power Grid L3 resource-market panel + palette"
```

---

## Task 4: PlantMarketPanel

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/power-grid/PowerGridPlantMarketPanel.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/power-grid/__tests__/PowerGridPlantMarketPanel.test.tsx`

**Interfaces:**
- Consumes: `PowerGridPlantBank` from `./power-grid-state`.
- Produces: `PowerGridPlantMarketPanel` with props
  ```ts
  interface PowerGridPlantMarketPanelProps {
    plants: { current: (number | null)[]; future: (number | null)[] };
    editable: boolean;
    onSetPlant?: (bank: PowerGridPlantBank, index: number, plant: number | null) => void;
    labels: { heading: string; currentBank: string; futureBank: string; emptySlot: string; slotAria: string /* "{bank} slot {n}" */ };
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// PowerGridPlantMarketPanel.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PowerGridPlantMarketPanel } from '../PowerGridPlantMarketPanel';

const plants = { current: [3, 4, null, 6], future: [null, null, null, null] };
const labels = { heading: 'Centrali', currentBank: 'Attuali', futureBank: 'Future', emptySlot: '—', slotAria: '{bank} slot {n}' };

describe('PowerGridPlantMarketPanel', () => {
  it('renders 8 slots across two banks', () => {
    const { container } = render(<PowerGridPlantMarketPanel plants={plants} editable={false} labels={labels} />);
    expect(container.querySelectorAll('[data-slot="pg-plant-slot"]')).toHaveLength(8);
  });

  it('read-only shows numbers / em-dash and no inputs', () => {
    const { container } = render(<PowerGridPlantMarketPanel plants={plants} editable={false} labels={labels} />);
    expect(container.querySelector('input')).toBeNull();
    const slots = container.querySelectorAll('[data-slot="pg-plant-slot"]');
    expect(slots[0]?.textContent).toContain('3');
    expect(slots[2]?.textContent).toContain('—');
  });

  it('host: typing a number fires onSetPlant with parsed value', async () => {
    const onSetPlant = vi.fn();
    const { container } = render(<PowerGridPlantMarketPanel plants={{ current: [null, null, null, null], future: [null, null, null, null] }} editable onSetPlant={onSetPlant} labels={labels} />);
    const input = container.querySelector('[data-slot="pg-plant-slot"][data-bank="current"][data-index="0"] input') as HTMLInputElement;
    await userEvent.type(input, '15');
    // last change event carries the full value
    expect(onSetPlant).toHaveBeenLastCalledWith('current', 0, 15);
  });

  it('host: clearing the input fires onSetPlant with null', async () => {
    const onSetPlant = vi.fn();
    const { container } = render(<PowerGridPlantMarketPanel plants={{ current: [7, null, null, null], future: [null, null, null, null] }} editable onSetPlant={onSetPlant} labels={labels} />);
    const input = container.querySelector('[data-slot="pg-plant-slot"][data-bank="current"][data-index="0"] input') as HTMLInputElement;
    await userEvent.clear(input);
    expect(onSetPlant).toHaveBeenLastCalledWith('current', 0, null);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/power-grid/__tests__/PowerGridPlantMarketPanel.test.tsx`
Expected: FAIL — `Cannot find module '../PowerGridPlantMarketPanel'`.

- [ ] **Step 3: Write the implementation**

```tsx
// PowerGridPlantMarketPanel.tsx
'use client';

import { type ReactElement } from 'react';

import { POWER_GRID_PLANT_BANKS, type PowerGridPlantBank } from './power-grid-state';

export interface PowerGridPlantMarketPanelProps {
  readonly plants: { current: (number | null)[]; future: (number | null)[] };
  readonly editable: boolean;
  readonly onSetPlant?: (bank: PowerGridPlantBank, index: number, plant: number | null) => void;
  readonly labels: {
    heading: string; currentBank: string; futureBank: string; emptySlot: string; slotAria: string;
  };
}

export function PowerGridPlantMarketPanel({
  plants, editable, onSetPlant, labels,
}: PowerGridPlantMarketPanelProps): ReactElement {
  const bankLabel = (bank: PowerGridPlantBank): string =>
    bank === 'current' ? labels.currentBank : labels.futureBank;

  const parseInput = (raw: string): number | null => {
    const trimmed = raw.trim();
    if (trimmed === '') return null;
    const n = Number.parseInt(trimmed, 10);
    return Number.isNaN(n) ? null : n;
  };

  return (
    <section data-slot="pg-plants" className="flex flex-col gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{labels.heading}</h3>
      {POWER_GRID_PLANT_BANKS.map(bank => (
        <div key={bank} data-slot="pg-plant-bank" data-bank={bank} className="flex flex-col gap-1">
          <span className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{bankLabel(bank)}</span>
          <div className="grid grid-cols-4 gap-1">
            {plants[bank].map((plant, i) => (
              <div key={i} data-slot="pg-plant-slot" data-bank={bank} data-index={String(i)}
                className="flex items-center justify-center rounded-lg border border-border bg-card p-1">
                {editable ? (
                  <input type="number" min={0} inputMode="numeric"
                    aria-label={labels.slotAria.replace('{bank}', bankLabel(bank)).replace('{n}', String(i + 1))}
                    defaultValue={plant ?? ''}
                    onChange={e => onSetPlant?.(bank, i, parseInput(e.target.value))}
                    className="w-full bg-transparent text-center text-sm font-semibold tabular-nums text-foreground outline-none" />
                ) : (
                  <span className="text-sm font-semibold tabular-nums text-foreground">{plant ?? labels.emptySlot}</span>
                )}
              </div>
            ))}
          </div>
        </div>
      ))}
    </section>
  );
}
```

> The `<input>` uses `defaultValue` (uncontrolled) so a host can type freely; each keystroke's parsed value is forwarded via `onChange`, and the editor's immediate-PUT + optimistic store keeps the source of truth. `data-bank`/`data-index` scope the test selectors. Run `pnpm exec eslint --max-warnings=0` before committing — the panel uses only semantic tokens (no colour), so it should lint clean.

- [ ] **Step 4: Run eslint + test**

```bash
cd apps/web && pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/power-grid/PowerGridPlantMarketPanel.tsx
pnpm exec vitest run src/components/features/session-live/flavors/power-grid/__tests__/PowerGridPlantMarketPanel.test.tsx
```
Expected: eslint clean; test PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/power-grid/PowerGridPlantMarketPanel.tsx" "apps/web/src/components/features/session-live/flavors/power-grid/__tests__/PowerGridPlantMarketPanel.test.tsx"
git commit -m "feat(session-live): #2791 Power Grid L3 plant-market panel"
```

---

## Task 5: PowerGridLiveFlavor container (self-builds labels)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/power-grid/PowerGridLiveFlavor.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/power-grid/__tests__/PowerGridLiveFlavor.test.tsx`

**Interfaces:**
- Consumes: the 2 panels; `usePowerGridStateEditor`; `hasRequiredRole`, `ParticipantRole`; `LiveSessionDto`; `useIntl` + `useTranslation`.
- Produces: `PowerGridLiveFlavor` + `PowerGridLiveFlavorProps` (game-agnostic `FlavorProps`): `{ session, viewerRole, sessionId, className?, livePoints?, phaseName? }`.

- [ ] **Step 1: Write the failing test**

```tsx
// PowerGridLiveFlavor.test.tsx
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { PowerGridLiveFlavor } from '../PowerGridLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { initialPowerGridState } from '../power-grid-state';

expect.extend(toHaveNoViolations);
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({ useUpdateLiveGameState: () => ({ mutate: vi.fn() }) }));

const session = {
  id: 's1', sessionCode: 'ABC', gameId: null, gameName: 'Power Grid', gameSlug: 'power-grid',
  createdByUserId: 'u1', status: 'InProgress', visibility: 'Private', groupId: null,
  createdAt: '', startedAt: '', pausedAt: null, completedAt: null, updatedAt: '', lastSavedAt: null,
  currentTurnIndex: 0, currentTurnPlayerId: 'p1', agentMode: 'None', notes: null,
  players: [
    { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 6, currentRank: 1, joinedAt: '', isActive: true },
    { id: 'p2', userId: null, displayName: 'Anna', avatarUrl: null, color: 'Blue', role: 'Player', teamId: null, totalScore: 3, currentRank: 2, joinedAt: '', isActive: false },
  ],
  teams: [], roundScores: [], scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

function renderFlavor(props: Partial<Parameters<typeof PowerGridLiveFlavor>[0]> = {}) {
  return render(
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <PowerGridLiveFlavor session={session} viewerRole="Player" sessionId="s1" {...props} />
    </IntlProvider>
  );
}
beforeEach(() => useLiveSessionStore.getState().reset());

describe('PowerGridLiveFlavor', () => {
  it('renders the leaderboard with null gameState; no panels', () => {
    const { container } = renderFlavor();
    expect(container.querySelectorAll('[data-slot="pg-leaderboard-row"]')).toHaveLength(2);
    expect(container.querySelector('[data-slot="pg-plants"]')).toBeNull();
  });

  it('host sees the init CTA when gameState is null', () => {
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="pg-init"]')).not.toBeNull();
  });

  it('renders plant + resource panels when gameState is present', () => {
    useLiveSessionStore.getState().setGameState(initialPowerGridState());
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="pg-plants"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="pg-resources"]')).not.toBeNull();
    expect(container.querySelectorAll('[data-slot="pg-plant-slot"]')).toHaveLength(8);
  });

  it('has no axe violations (host, populated)', async () => {
    useLiveSessionStore.getState().setGameState(initialPowerGridState());
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/power-grid/__tests__/PowerGridLiveFlavor.test.tsx`
Expected: FAIL — `Cannot find module '../PowerGridLiveFlavor'`.

- [ ] **Step 3: Write the implementation**

```tsx
// PowerGridLiveFlavor.tsx
'use client';

import { type ReactElement } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { PowerGridPlantMarketPanel } from './PowerGridPlantMarketPanel';
import { PowerGridResourceMarketPanel } from './PowerGridResourceMarketPanel';
import { usePowerGridStateEditor } from './use-power-grid-state-editor';

export interface PowerGridLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.power-grid';

export function PowerGridLiveFlavor({
  session, viewerRole, sessionId, className, livePoints,
}: PowerGridLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const editor = usePowerGridStateEditor(sessionId);
  const state = editor.state;

  const tmpl = (id: string, fallback: string) => (intl.messages[`${K}.${id}`] as string) ?? fallback;
  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));

  return (
    <section aria-label={t(`${K}.panelAriaLabel`)} data-slot="pg-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}>
      {/* Leaderboard (ungated — from scoring) */}
      <div data-slot="pg-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t(`${K}.leaderboardHeading`)}</h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li key={player.id} data-slot="pg-leaderboard-row"
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
          <PowerGridPlantMarketPanel plants={state.plants} editable={isHost}
            onSetPlant={editor.setPlant}
            labels={{ heading: t(`${K}.plantsHeading`), currentBank: t(`${K}.currentBank`), futureBank: t(`${K}.futureBank`), emptySlot: t(`${K}.emptySlot`), slotAria: tmpl('slotAria', '{bank} slot {n}') }} />
          <PowerGridResourceMarketPanel resources={state.resources} editable={isHost}
            onBump={editor.bumpResource}
            labels={{ heading: t(`${K}.resourcesHeading`), coal: t(`${K}.coal`), oil: t(`${K}.oil`), garbage: t(`${K}.garbage`), uranium: t(`${K}.uranium`), incAria: tmpl('incAria', '{field} +1'), decAria: tmpl('decAria', '{field} -1') }} />
          {isHost && (
            <button type="button" onClick={editor.initializeState}
              className="self-start text-xs text-muted-foreground underline hover:text-foreground">
              {t(`${K}.resetCta`)}
            </button>
          )}
        </>
      ) : isHost ? (
        <button type="button" data-slot="pg-init" onClick={editor.initializeState}
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
cd apps/web && pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/power-grid/PowerGridLiveFlavor.tsx
pnpm exec vitest run src/components/features/session-live/flavors/power-grid/__tests__/PowerGridLiveFlavor.test.tsx
```
Expected: eslint clean; test PASS (4 tests incl. axe).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/power-grid/PowerGridLiveFlavor.tsx" "apps/web/src/components/features/session-live/flavors/power-grid/__tests__/PowerGridLiveFlavor.test.tsx"
git commit -m "feat(session-live): #2791 Power Grid L3 flavor container (self-builds labels)"
```

---

## Task 6: Wire into the registry + i18n

**Files:**
- Modify: `apps/web/src/components/features/session-live/FlavorRenderer.tsx`
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Test: `apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx` (extend)

- [ ] **Step 1: Add the FLAVOR_MAP entry**

In `FlavorRenderer.tsx`, add a module-scope lazy component (match the puerto-rico/paleo pattern), then add the map entry (hyphenated key):

```tsx
const PowerGridLiveFlavorLazy: FlavorComponent = dynamic(
  () => import('./flavors/power-grid/PowerGridLiveFlavor').then(m => ({ default: m.PowerGridLiveFlavor })),
  { ssr: false, loading: () => <FlavorLoadingSkeleton /> }
);
```
and add `'power-grid': { live: PowerGridLiveFlavorLazy }` to `FLAVOR_MAP`.

- [ ] **Step 2: Extend the FlavorRenderer guard test**

Add inside the `hasFlavor` describe: `expect(hasFlavor('power-grid')).toBe(true);`

- [ ] **Step 3: Add the i18n keys**

In `src/locales/it.json`, under `pages.sessionLive.flavor`, add a `"power-grid"` sibling:

```json
"power-grid": {
  "panelAriaLabel": "Power Grid",
  "leaderboardHeading": "Classifica",
  "initBoardCta": "Inizia partita",
  "resetCta": "Reimposta stato",
  "viewerWaiting": "In attesa dell'host…",
  "plantsHeading": "Centrali",
  "currentBank": "Attuali",
  "futureBank": "Future",
  "emptySlot": "—",
  "slotAria": "{bank} slot {n}",
  "resourcesHeading": "Mercato risorse",
  "coal": "Carbone",
  "oil": "Petrolio",
  "garbage": "Rifiuti",
  "uranium": "Uranio",
  "incAria": "{field} +1",
  "decAria": "{field} -1"
}
```

Mirror in `src/locales/en.json` with English copy (`"leaderboardHeading": "Standings"`, `"initBoardCta": "Start game"`, `"resetCta": "Reset state"`, `"viewerWaiting": "Waiting for the host…"`, `"plantsHeading": "Power plants"`, `"currentBank": "Current"`, `"futureBank": "Future"`, `"resourcesHeading": "Resource market"`, `"coal": "Coal"`, `"oil": "Oil"`, `"garbage": "Garbage"`, `"uranium": "Uranium"`; `emptySlot`/`slotAria`/`incAria`/`decAria` identical). BOTH locales MUST have the identical key set.

- [ ] **Step 4: Typecheck + run affected suites + eslint**

```bash
cd apps/web
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run src/components/features/session-live/flavors/power-grid src/components/features/session-live/__tests__/FlavorRenderer.test.tsx
pnpm exec eslint --max-warnings=0 src/components/features/session-live/FlavorRenderer.tsx
```
Expected: typecheck clean; all power-grid suites pass + the guard test's new assertion passes; eslint clean. (`FlavorRenderer.test.tsx > "lazy-loads the Catan flavor"` is a KNOWN pre-existing baseline flake in isolated single-file runs — unrelated.)

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/FlavorRenderer.tsx" "apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx" "apps/web/src/locales/it.json" "apps/web/src/locales/en.json"
git commit -m "feat(session-live): #2791 wire Power Grid flavor into the registry + i18n"
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
Expected: typecheck clean; all catan + wingspan + codenames + puerto-rico + paleo + power-grid flavor tests pass (the Catan lazy-load isolated flake aside).

- [ ] **Step 2: Lint the whole flavor dir**

```bash
pnpm exec eslint --max-warnings=0 "src/components/features/session-live/flavors/power-grid/**/*.{ts,tsx}" src/components/features/session-live/FlavorRenderer.tsx
```
Expected: no errors.

- [ ] **Step 3: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2791-power-grid-l2-l3
gh pr create --base main-dev --head feature/issue-2791-power-grid-l2-l3 \
  --title "feat(session-live): #2791 Power Grid L2+L3 live flavor (plant market + resources)" \
  --body "Implements the Power Grid live flavor per docs/superpowers/specs/2026-07-17-power-grid-l2-l3-flavor-design.md. FE-only; reuses the game-agnostic plumbing; first mixed-cadence editor (plant immediate / resource debounced). Closes #2791."
```

---

## Self-Review

**1. Spec coverage:**
- L2 schema + helpers (fixed resources object, 4-length plant banks) → Task 1. ✅
- Editor MIXED cadence (bumpResource debounced clamp, setPlant immediate via commit+flush, out-of-range no-op, NaN/negative → null/clamp) → Task 2. ✅
- ResourceMarketPanel + palette → Task 3. PlantMarketPanel (numeric inputs) → Task 4. ✅
- Container (leaderboard ungated, panels gated, self-builds labels) → Task 5. ✅
- Wiring (FLAVOR_MAP + i18n) → Task 6. ✅
- VP-from-scoring invariant → Tasks 5 (read `livePoints`/`totalScore`, never gameState). ✅
- Lint gate per task → Steps in Tasks 3–6 + Task 7. ✅
- Testing (unit + component + jest-axe) → Tasks 1–5. ✅

**2. Placeholder scan:** No TBD/TODO; every code step has complete code. ✅

**3. Type consistency:** `PowerGridGameState`, `PowerGridResources`, `PowerGridResource`, `PowerGridPlantBank`, `parsePowerGridGameState`, `emptyPowerGridResources`, `initialPowerGridState`, `POWER_GRID_RESOURCES`, `POWER_GRID_PLANT_BANKS`, `usePowerGridStateEditor(sessionId)` signature (no playerIds), and the 2 component prop interfaces are used consistently Task 1→6. The container's panel labels match `PowerGridResourceMarketPanelProps['labels']` / `PowerGridPlantMarketPanelProps['labels']`; editor mutator names (`bumpResource`/`setPlant`) match the container's `onBump`/`onSetPlant` wiring. ✅

**Known follow-ups (out of scope):** the resource price-ladder bracket; the auction overlay; the network map; a Power Grid summary flavor.
