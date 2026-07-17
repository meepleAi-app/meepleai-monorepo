# Paleo L2+L3 Flavor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Paleo live flavor — 4 shared resource counters + a per-player tribe status (alive/wounded/dead) — on the L1 game-state layer, reusing the generalized flavor plumbing.

**Architecture:** FE-only. Reuses the game-agnostic pattern (`flavors/<game>/`; `FlavorRenderer` dispatches on `FLAVOR_MAP` with `FlavorProps`; each flavor self-builds i18n labels). Resources + statuses live in `gameState`; VP/win-loss stays in the existing scoring system. Host-entered, no engine. Debounced-500 ms PUT.

**Tech Stack:** Next.js 16 · React 19 · TypeScript · Zod · Zustand (`useLiveSessionStore`) · TanStack Query (`useUpdateLiveGameState`) · `useDebouncedCallback` · react-intl (`useIntl`) + `@/hooks/useTranslation` · Vitest + Testing Library + jest-axe · Tailwind semantic tokens (status colours inline `hsl()` via a palette module).

## Global Constraints

- **Issue:** #2789 (G6c, epic #3025). Spec: `docs/superpowers/specs/2026-07-17-paleo-l2-l3-flavor-design.md`.
- **Zero backend changes.** `gameState` is the opaque L1 blob; scores use the existing scoring editor.
- **State schema:** `v: 1`, `game: 'paleo'`. `parsePaleoGameState` returns `null` (never throws) on wrong game/version/shape via Zod `safeParse`.
- **`gameState` shape:** `{ v, game, resources:{wood,stone,food,knowledge}, survivors: Record<playerId, 'alive'|'wounded'|'dead'> }`. Never scores, never a win/loss flag.
- **`resources` is a fixed `z.object` with all 4 keys required** (each `int>=0`), NOT `z.record`. `survivors` is `z.record(z.string(), status-enum)`.
- **Continuous ± AND discrete status cycle → DEBOUNCED PUT (500 ms)** + optimistic `setGameState` first + flush on unmount (like Puerto Rico). Resource bumps clamp `>=0`.
- **VP/win-loss stays in scoring** (`livePoints`/`totalScore`); gameState never carries it. **Leaderboard renders ungated**; the panels gate on gameState (host CTA when null). **Host-edit only** (`viewerRole === 'Host'` via `hasRequiredRole`).
- **Tribe status keyed by `session.players`** (one per player, seeded `alive`), not a free roster.
- **Flavors self-build i18n labels** via `useIntl` + `useTranslation`; templated strings via `intl.messages[id] as string ?? fallback`, static via `t(id)`.
- **Colours:** semantic Tailwind tokens EXCEPT the 3 status colours → inline `hsl()` via the palette. `text-white` only on a cell that ALSO sets an inline coloured `backgroundColor` (put white in inline `style.color`, NOT the `text-white` utility).
- **LINT GATE (critical — implementers + the pre-commit hook miss it):** after each component/palette task run `pnpm exec eslint --max-warnings=0 <file>`. The pre-commit hook does NOT run `meepleai/no-inline-hsl-v2` nor the `style`-prop case of `local/no-hardcoded-color-utility`. Inline `hsl()` that trips `no-inline-hsl-v2` gets a line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- <reason>`.
- **Tests:** Vitest, TDD, pristine. Query via `data-slot`/roles, not `getByTestId`. Files under `apps/web/src/components/features/session-live/flavors/paleo/`. Run from `apps/web`.
- **Windows:** pre-commit runs `pnpm typecheck` (~2 min, sometimes slower) — allow ≥9 min for commits; if TS2307 on stale `.next/types`, `rm -rf .next/types` first (never `--no-verify`).

## File Structure

Create under `flavors/paleo/`: `paleo-state.ts`, `paleo-palette.ts`, `use-paleo-state-editor.ts`, `PaleoResourcePanel.tsx`, `PaleoTribePanel.tsx`, `PaleoLiveFlavor.tsx`, `__tests__/*`.
Modify: `session-live/FlavorRenderer.tsx` (one `FLAVOR_MAP` entry), `src/locales/it.json` + `en.json` (`flavor.paleo.*`).

---

## Task 1: L2 state schema + helpers

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/paleo/paleo-state.ts`
- Test: `apps/web/src/components/features/session-live/flavors/paleo/__tests__/paleo-state.test.ts`

**Interfaces:**
- Produces: `PaleoGameState`, `PaleoResources`, `PaleoResource`, `PaleoStatus` types; `parsePaleoGameState(raw): PaleoGameState | null`; `emptyPaleoResources(): PaleoResources`; `initialPaleoState(playerIds: readonly string[]): PaleoGameState`; `nextPaleoStatus(s: PaleoStatus): PaleoStatus`; `PALEO_RESOURCES`, `PALEO_STATUSES`, `PALEO_STATE_VERSION = 1`.

- [ ] **Step 1: Write the failing test**

```ts
// paleo-state.test.ts
import { describe, expect, it } from 'vitest';

import {
  PALEO_RESOURCES,
  PALEO_STATUSES,
  emptyPaleoResources,
  initialPaleoState,
  nextPaleoStatus,
  parsePaleoGameState,
} from '../paleo-state';

const VALID = {
  v: 1, game: 'paleo',
  resources: { wood: 2, stone: 0, food: 1, knowledge: 3 },
  survivors: { p1: 'alive', p2: 'wounded' },
};

describe('parsePaleoGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parsePaleoGameState(VALID);
    expect(parsed?.resources.knowledge).toBe(3);
    expect(parsed?.survivors.p2).toBe('wounded');
  });
  it('returns null for a different game', () => {
    expect(parsePaleoGameState({ ...VALID, game: 'catan' })).toBeNull();
  });
  it('returns null for a future version', () => {
    expect(parsePaleoGameState({ ...VALID, v: 2 })).toBeNull();
  });
  it('returns null when a resource is missing', () => {
    expect(parsePaleoGameState({ ...VALID, resources: { wood: 1, stone: 0, food: 2 } })).toBeNull();
  });
  it('returns null for an invalid status', () => {
    expect(parsePaleoGameState({ ...VALID, survivors: { p1: 'zombie' } })).toBeNull();
  });
  it('returns null for malformed / non-object', () => {
    expect(parsePaleoGameState(null)).toBeNull();
    expect(parsePaleoGameState('x')).toBeNull();
  });
});

describe('emptyPaleoResources', () => {
  it('is all zero', () => {
    expect(emptyPaleoResources()).toEqual({ wood: 0, stone: 0, food: 0, knowledge: 0 });
  });
});

describe('initialPaleoState', () => {
  it('seeds resources 0 + every player alive', () => {
    const s = initialPaleoState(['p1', 'p2']);
    expect(s.resources).toEqual({ wood: 0, stone: 0, food: 0, knowledge: 0 });
    expect(s.survivors).toEqual({ p1: 'alive', p2: 'alive' });
    expect(s.game).toBe('paleo');
  });
});

describe('nextPaleoStatus', () => {
  it('cycles alive → wounded → dead → alive', () => {
    expect(nextPaleoStatus('alive')).toBe('wounded');
    expect(nextPaleoStatus('wounded')).toBe('dead');
    expect(nextPaleoStatus('dead')).toBe('alive');
  });
});

describe('constants', () => {
  it('lists the 4 resources and 3 statuses in order', () => {
    expect(PALEO_RESOURCES).toEqual(['wood', 'stone', 'food', 'knowledge']);
    expect(PALEO_STATUSES).toEqual(['alive', 'wounded', 'dead']);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/paleo/__tests__/paleo-state.test.ts`
Expected: FAIL — `Cannot find module '../paleo-state'`.

- [ ] **Step 3: Write the implementation**

```ts
// paleo-state.ts
import { z } from 'zod';

export const PALEO_STATE_VERSION = 1;
export const PALEO_RESOURCES = ['wood', 'stone', 'food', 'knowledge'] as const;
export const PALEO_STATUSES = ['alive', 'wounded', 'dead'] as const;

export const PaleoResourceSchema = z.enum(PALEO_RESOURCES);
export type PaleoResource = z.infer<typeof PaleoResourceSchema>;
export const PaleoStatusSchema = z.enum(PALEO_STATUSES);
export type PaleoStatus = z.infer<typeof PaleoStatusSchema>;

const nn = () => z.number().int().min(0);

export const PaleoResourcesSchema = z.object({
  wood: nn(), stone: nn(), food: nn(), knowledge: nn(),
});
export type PaleoResources = z.infer<typeof PaleoResourcesSchema>;

export const PaleoGameStateSchema = z.object({
  v: z.literal(PALEO_STATE_VERSION),
  game: z.literal('paleo'),
  resources: PaleoResourcesSchema,
  survivors: z.record(z.string(), PaleoStatusSchema),
});
export type PaleoGameState = z.infer<typeof PaleoGameStateSchema>;

export function parsePaleoGameState(raw: unknown): PaleoGameState | null {
  const result = PaleoGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function emptyPaleoResources(): PaleoResources {
  return { wood: 0, stone: 0, food: 0, knowledge: 0 };
}

export function initialPaleoState(playerIds: readonly string[]): PaleoGameState {
  const survivors: Record<string, PaleoStatus> = {};
  for (const id of playerIds) survivors[id] = 'alive';
  return {
    v: PALEO_STATE_VERSION,
    game: 'paleo',
    resources: emptyPaleoResources(),
    survivors,
  };
}

export function nextPaleoStatus(status: PaleoStatus): PaleoStatus {
  const i = PALEO_STATUSES.indexOf(status);
  return PALEO_STATUSES[(i + 1) % PALEO_STATUSES.length];
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/paleo/__tests__/paleo-state.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/paleo/paleo-state.ts" "apps/web/src/components/features/session-live/flavors/paleo/__tests__/paleo-state.test.ts"
git commit -m "feat(session-live): #2789 Paleo L2 state schema + helpers"
```

---

## Task 2: Host-edit hook (debounced)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/paleo/use-paleo-state-editor.ts`
- Test: `apps/web/src/components/features/session-live/flavors/paleo/__tests__/use-paleo-state-editor.test.tsx`

**Interfaces:**
- Consumes: `PaleoGameState`, `PaleoResource`, `parsePaleoGameState`, `initialPaleoState`, `nextPaleoStatus` from `./paleo-state`; `useLiveSessionStore`, `useUpdateLiveGameState`, `useDebouncedCallback`.
- Produces: `usePaleoStateEditor(sessionId: string, playerIds: readonly string[]): PaleoStateEditor` where
  ```ts
  interface PaleoStateEditor {
    state: PaleoGameState | null;
    initializeState: () => void;
    bumpResource: (field: PaleoResource, delta: 1 | -1) => void;
    cycleSurvivorStatus: (playerId: string) => void;
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// use-paleo-state-editor.test.tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { usePaleoStateEditor } from '../use-paleo-state-editor';
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
  return useLiveSessionStore.getState().gameState as import('../paleo-state').PaleoGameState | null;
}

describe('usePaleoStateEditor', () => {
  it('initializeState seeds resources 0 + players alive', () => {
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1', 'p2']));
    act(() => result.current.initializeState());
    expect(current()?.resources).toEqual({ wood: 0, stone: 0, food: 0, knowledge: 0 });
    expect(current()?.survivors).toEqual({ p1: 'alive', p2: 'alive' });
  });

  it('bumpResource clamps at 0', () => {
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.bumpResource('wood', -1));
    expect(current()?.resources.wood).toBe(0);
    act(() => result.current.bumpResource('wood', 1));
    expect(current()?.resources.wood).toBe(1);
  });

  it('cycleSurvivorStatus advances alive → wounded', () => {
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.cycleSurvivorStatus('p1'));
    expect(current()?.survivors.p1).toBe('wounded');
  });

  it('cycleSurvivorStatus folds in a missing player (defaults alive → wounded)', () => {
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.cycleSurvivorStatus('pX'));
    expect(current()?.survivors.pX).toBe('wounded');
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1']));
    act(() => result.current.bumpResource('wood', 1));
    expect(current()).toBeNull();
  });

  it('eventually PUTs (debounced)', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => vi.advanceTimersByTime(600));
    expect(mutateMock).toHaveBeenCalled();
    vi.useRealTimers();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/paleo/__tests__/use-paleo-state-editor.test.tsx`
Expected: FAIL — `Cannot find module '../use-paleo-state-editor'`.

- [ ] **Step 3: Write the implementation**

```ts
// use-paleo-state-editor.ts
'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import {
  initialPaleoState,
  nextPaleoStatus,
  parsePaleoGameState,
  type PaleoGameState,
  type PaleoResource,
} from './paleo-state';

export interface PaleoStateEditor {
  state: PaleoGameState | null;
  initializeState: () => void;
  bumpResource: (field: PaleoResource, delta: 1 | -1) => void;
  cycleSurvivorStatus: (playerId: string) => void;
}

const clampMin = (n: number) => (n < 0 ? 0 : n);

export function usePaleoStateEditor(
  sessionId: string,
  playerIds: readonly string[]
): PaleoStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parsePaleoGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: PaleoGameState) => mutate(next),
    500
  );

  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: PaleoGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  const readState = useCallback(
    (): PaleoGameState | null => parsePaleoGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const initializeState = useCallback(
    () => commit(initialPaleoState(playerIds)),
    [commit, playerIds]
  );

  const bumpResource = useCallback(
    (field: PaleoResource, delta: 1 | -1) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, resources: { ...cur.resources, [field]: clampMin(cur.resources[field] + delta) } });
    },
    [commit, readState]
  );

  const cycleSurvivorStatus = useCallback(
    (playerId: string) => {
      const cur = readState();
      if (cur == null) return;
      const currentStatus = cur.survivors[playerId] ?? 'alive';
      commit({ ...cur, survivors: { ...cur.survivors, [playerId]: nextPaleoStatus(currentStatus) } });
    },
    [commit, readState]
  );

  return { state, initializeState, bumpResource, cycleSurvivorStatus };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/paleo/__tests__/use-paleo-state-editor.test.tsx`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/paleo/use-paleo-state-editor.ts" "apps/web/src/components/features/session-live/flavors/paleo/__tests__/use-paleo-state-editor.test.tsx"
git commit -m "feat(session-live): #2789 Paleo L2 host-edit hook (debounced)"
```

---

## Task 3: Palette + ResourcePanel

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/paleo/paleo-palette.ts`
- Create: `apps/web/src/components/features/session-live/flavors/paleo/PaleoResourcePanel.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/paleo/__tests__/PaleoResourcePanel.test.tsx`

**Interfaces:**
- Consumes: `PaleoResource`, `PaleoResources`, `PALEO_RESOURCES` from `./paleo-state`; `paleoStatusColor` from `./paleo-palette`.
- Produces: `paleoStatusColor(status: PaleoStatus): string`; `PaleoResourcePanel` with props
  ```ts
  interface PaleoResourcePanelProps {
    resources: PaleoResources;
    editable: boolean;
    onBump?: (field: PaleoResource, delta: 1 | -1) => void;
    labels: { heading: string; wood: string; stone: string; food: string; knowledge: string; incAria: string; decAria: string };
  }
  ```

- [ ] **Step 1: Write the palette**

```ts
// paleo-palette.ts
import type { PaleoStatus } from './paleo-state';

// The 3 Paleo tribe statuses — inline hsl() applied via `style` (like catan/puerto-rico palettes).
// Any hue that trips meepleai/no-inline-hsl-v2 carries a line-level disable with a reason.
const STATUS_HSL: Record<PaleoStatus, string> = {
  alive: 'hsl(142, 55%, 42%)',
  wounded: 'hsl(38, 90%, 50%)',
  dead: 'hsl(0, 0%, 45%)',
};

export function paleoStatusColor(status: PaleoStatus): string {
  return STATUS_HSL[status];
}
```

> After writing the palette, run `pnpm exec eslint --max-warnings=0` on it. If a hue trips `meepleai/no-inline-hsl-v2`, add a line-level `// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Paleo <status> tribe colour, not the <entity> token` above it (mirroring `catan-palette`). The pre-commit hook does NOT catch this.

- [ ] **Step 2: Write the failing test**

```tsx
// PaleoResourcePanel.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PaleoResourcePanel } from '../PaleoResourcePanel';

const resources = { wood: 3, stone: 1, food: 0, knowledge: 2 };
const labels = {
  heading: 'Risorse', wood: 'Legno', stone: 'Pietra', food: 'Cibo', knowledge: 'Conoscenza',
  incAria: '{field} +1', decAria: '{field} -1',
};

describe('PaleoResourcePanel', () => {
  it('renders all 4 resources with counts', () => {
    const { container } = render(<PaleoResourcePanel resources={resources} editable={false} labels={labels} />);
    expect(container.querySelectorAll('[data-resource]')).toHaveLength(4);
    expect(screen.getByText('Legno').closest('[data-resource]')?.textContent).toContain('3');
  });

  it('read-only exposes no steppers', () => {
    render(<PaleoResourcePanel resources={resources} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: wood +1 fires onBump', async () => {
    const onBump = vi.fn();
    render(<PaleoResourcePanel resources={resources} editable onBump={onBump} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Legno +1' }));
    expect(onBump).toHaveBeenCalledWith('wood', 1);
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/paleo/__tests__/PaleoResourcePanel.test.tsx`
Expected: FAIL — `Cannot find module '../PaleoResourcePanel'`.

- [ ] **Step 4: Write the implementation**

```tsx
// PaleoResourcePanel.tsx
'use client';

import { type ReactElement } from 'react';

import { PALEO_RESOURCES, type PaleoResource, type PaleoResources } from './paleo-state';

export interface PaleoResourcePanelProps {
  readonly resources: PaleoResources;
  readonly editable: boolean;
  readonly onBump?: (field: PaleoResource, delta: 1 | -1) => void;
  readonly labels: {
    heading: string; wood: string; stone: string; food: string; knowledge: string;
    incAria: string; decAria: string;
  };
}

export function PaleoResourcePanel({
  resources, editable, onBump, labels,
}: PaleoResourcePanelProps): ReactElement {
  const inc = (f: string) => labels.incAria.replace('{field}', f);
  const dec = (f: string) => labels.decAria.replace('{field}', f);
  const rows: Array<[PaleoResource, string]> = [
    ['wood', labels.wood], ['stone', labels.stone], ['food', labels.food], ['knowledge', labels.knowledge],
  ];

  return (
    <section data-slot="paleo-resources" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{labels.heading}</h3>
      <div className="grid grid-cols-2 gap-2">
        {rows.map(([field, label]) => (
          <div key={field} data-resource={field}
            className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1 text-xs">
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
  src/components/features/session-live/flavors/paleo/paleo-palette.ts \
  src/components/features/session-live/flavors/paleo/PaleoResourcePanel.tsx
pnpm exec vitest run src/components/features/session-live/flavors/paleo/__tests__/PaleoResourcePanel.test.tsx
```
Expected: eslint clean (add a `no-inline-hsl-v2` disable in the palette if a status hue trips it); test PASS (3 tests). The ResourcePanel uses only semantic tokens (no colour) so it lints clean.

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/paleo/paleo-palette.ts" "apps/web/src/components/features/session-live/flavors/paleo/PaleoResourcePanel.tsx" "apps/web/src/components/features/session-live/flavors/paleo/__tests__/PaleoResourcePanel.test.tsx"
git commit -m "feat(session-live): #2789 Paleo L3 resource panel + status palette"
```

---

## Task 4: TribePanel

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/paleo/PaleoTribePanel.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/paleo/__tests__/PaleoTribePanel.test.tsx`

**Interfaces:**
- Consumes: `PaleoStatus` from `./paleo-state`; `paleoStatusColor` from `./paleo-palette`; `LiveSessionPlayerDto` from `@/lib/api/schemas/live-sessions.schemas`.
- Produces: `PaleoTribePanel` with props
  ```ts
  interface PaleoTribePanelProps {
    players: LiveSessionPlayerDto[];
    survivors: Record<string, PaleoStatus>;
    editable: boolean;
    onCycle?: (playerId: string) => void;
    labels: { heading: string; statusAlive: string; statusWounded: string; statusDead: string; cycleAria: string /* "{name}: cambia stato" */ };
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// PaleoTribePanel.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PaleoTribePanel } from '../PaleoTribePanel';

const players = [
  { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 0, currentRank: 1, joinedAt: '', isActive: true },
  { id: 'p2', userId: null, displayName: 'Anna', avatarUrl: null, color: 'Blue', role: 'Player', teamId: null, totalScore: 0, currentRank: 2, joinedAt: '', isActive: true },
] as const;
const labels = { heading: 'Tribù', statusAlive: 'Vivo', statusWounded: 'Ferito', statusDead: 'Morto', cycleAria: '{name}: cambia stato' };

describe('PaleoTribePanel', () => {
  it('renders a row per player with a status badge', () => {
    const { container } = render(
      <PaleoTribePanel players={players} survivors={{ p1: 'alive', p2: 'wounded' }} editable={false} labels={labels} />
    );
    expect(container.querySelectorAll('[data-slot="paleo-tribe-row"]')).toHaveLength(2);
    expect(screen.getByText('Marco')).toBeInTheDocument();
  });

  it('defaults a missing player to alive', () => {
    render(<PaleoTribePanel players={players} survivors={{ p1: 'dead' }} editable={false} labels={labels} />);
    // p2 absent from survivors → shows the alive label
    expect(screen.getByText('Vivo')).toBeInTheDocument();
  });

  it('read-only exposes no buttons', () => {
    render(<PaleoTribePanel players={players} survivors={{ p1: 'alive', p2: 'alive' }} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: tapping a row fires onCycle with the player id', async () => {
    const onCycle = vi.fn();
    render(<PaleoTribePanel players={players} survivors={{ p1: 'alive', p2: 'alive' }} editable onCycle={onCycle} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Marco: cambia stato' }));
    expect(onCycle).toHaveBeenCalledWith('p1');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/paleo/__tests__/PaleoTribePanel.test.tsx`
Expected: FAIL — `Cannot find module '../PaleoTribePanel'`.

- [ ] **Step 3: Write the implementation**

```tsx
// PaleoTribePanel.tsx
'use client';

import { type ReactElement } from 'react';

import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { paleoStatusColor } from './paleo-palette';
import type { PaleoStatus } from './paleo-state';

export interface PaleoTribePanelProps {
  readonly players: LiveSessionPlayerDto[];
  readonly survivors: Record<string, PaleoStatus>;
  readonly editable: boolean;
  readonly onCycle?: (playerId: string) => void;
  readonly labels: {
    heading: string; statusAlive: string; statusWounded: string; statusDead: string; cycleAria: string;
  };
}

export function PaleoTribePanel({
  players, survivors, editable, onCycle, labels,
}: PaleoTribePanelProps): ReactElement {
  const statusLabel = (s: PaleoStatus): string =>
    s === 'alive' ? labels.statusAlive : s === 'wounded' ? labels.statusWounded : labels.statusDead;

  return (
    <section data-slot="paleo-tribe" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{labels.heading}</h3>
      <ul role="list" className="flex flex-col gap-1">
        {players.map(player => {
          const status = survivors[player.id] ?? 'alive';
          const badge = (
            <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold"
              style={{ backgroundColor: paleoStatusColor(status), color: 'hsl(0, 0%, 100%)' }}>
              {statusLabel(status)}
            </span>
          );
          return (
            <li key={player.id} data-slot="paleo-tribe-row" data-status={status}
              className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1">
              <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">{player.displayName}</span>
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
cd apps/web && pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/paleo/PaleoTribePanel.tsx
pnpm exec vitest run src/components/features/session-live/flavors/paleo/__tests__/PaleoTribePanel.test.tsx
```
Expected: eslint clean; test PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/paleo/PaleoTribePanel.tsx" "apps/web/src/components/features/session-live/flavors/paleo/__tests__/PaleoTribePanel.test.tsx"
git commit -m "feat(session-live): #2789 Paleo L3 tribe status panel"
```

---

## Task 5: PaleoLiveFlavor container (self-builds labels)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/paleo/PaleoLiveFlavor.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/paleo/__tests__/PaleoLiveFlavor.test.tsx`

**Interfaces:**
- Consumes: the 2 panels; `usePaleoStateEditor`; `hasRequiredRole`, `ParticipantRole`; `LiveSessionDto`; `useIntl` + `useTranslation`.
- Produces: `PaleoLiveFlavor` + `PaleoLiveFlavorProps` (game-agnostic `FlavorProps`): `{ session, viewerRole, sessionId, className?, livePoints?, phaseName? }`.

- [ ] **Step 1: Write the failing test**

```tsx
// PaleoLiveFlavor.test.tsx
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { PaleoLiveFlavor } from '../PaleoLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { initialPaleoState } from '../paleo-state';

expect.extend(toHaveNoViolations);
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({ useUpdateLiveGameState: () => ({ mutate: vi.fn() }) }));

const session = {
  id: 's1', sessionCode: 'ABC', gameId: null, gameName: 'Paleo', gameSlug: 'paleo',
  createdByUserId: 'u1', status: 'InProgress', visibility: 'Private', groupId: null,
  createdAt: '', startedAt: '', pausedAt: null, completedAt: null, updatedAt: '', lastSavedAt: null,
  currentTurnIndex: 0, currentTurnPlayerId: 'p1', agentMode: 'None', notes: null,
  players: [
    { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 4, currentRank: 1, joinedAt: '', isActive: true },
    { id: 'p2', userId: null, displayName: 'Anna', avatarUrl: null, color: 'Blue', role: 'Player', teamId: null, totalScore: 2, currentRank: 2, joinedAt: '', isActive: false },
  ],
  teams: [], roundScores: [], scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

function renderFlavor(props: Partial<Parameters<typeof PaleoLiveFlavor>[0]> = {}) {
  return render(
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <PaleoLiveFlavor session={session} viewerRole="Player" sessionId="s1" {...props} />
    </IntlProvider>
  );
}
beforeEach(() => useLiveSessionStore.getState().reset());

describe('PaleoLiveFlavor', () => {
  it('renders the leaderboard with null gameState; no panels', () => {
    const { container } = renderFlavor();
    expect(container.querySelectorAll('[data-slot="paleo-leaderboard-row"]')).toHaveLength(2);
    expect(container.querySelector('[data-slot="paleo-resources"]')).toBeNull();
  });

  it('host sees the init CTA when gameState is null', () => {
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="paleo-init"]')).not.toBeNull();
  });

  it('renders resource + tribe panels when gameState is present', () => {
    useLiveSessionStore.getState().setGameState(initialPaleoState(['p1', 'p2']));
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="paleo-resources"]')).not.toBeNull();
    expect(container.querySelectorAll('[data-slot="paleo-tribe-row"]')).toHaveLength(2);
  });

  it('has no axe violations (host, populated)', async () => {
    useLiveSessionStore.getState().setGameState(initialPaleoState(['p1', 'p2']));
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/paleo/__tests__/PaleoLiveFlavor.test.tsx`
Expected: FAIL — `Cannot find module '../PaleoLiveFlavor'`.

- [ ] **Step 3: Write the implementation**

```tsx
// PaleoLiveFlavor.tsx
'use client';

import { type ReactElement } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { PaleoResourcePanel } from './PaleoResourcePanel';
import { PaleoTribePanel } from './PaleoTribePanel';
import { usePaleoStateEditor } from './use-paleo-state-editor';

export interface PaleoLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.paleo';

export function PaleoLiveFlavor({
  session, viewerRole, sessionId, className, livePoints,
}: PaleoLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const playerIds = session.players.map(p => p.id);
  const editor = usePaleoStateEditor(sessionId, playerIds);
  const state = editor.state;

  const tmpl = (id: string, fallback: string) => (intl.messages[`${K}.${id}`] as string) ?? fallback;
  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));

  return (
    <section aria-label={t(`${K}.panelAriaLabel`)} data-slot="paleo-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}>
      {/* Leaderboard (ungated — from scoring) */}
      <div data-slot="paleo-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t(`${K}.leaderboardHeading`)}</h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li key={player.id} data-slot="paleo-leaderboard-row"
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
          <PaleoResourcePanel resources={state.resources} editable={isHost}
            onBump={editor.bumpResource}
            labels={{ heading: t(`${K}.resourcesHeading`), wood: t(`${K}.wood`), stone: t(`${K}.stone`), food: t(`${K}.food`), knowledge: t(`${K}.knowledge`), incAria: tmpl('incAria', '{field} +1'), decAria: tmpl('decAria', '{field} -1') }} />
          <PaleoTribePanel players={session.players} survivors={state.survivors} editable={isHost}
            onCycle={editor.cycleSurvivorStatus}
            labels={{ heading: t(`${K}.tribeHeading`), statusAlive: t(`${K}.statusAlive`), statusWounded: t(`${K}.statusWounded`), statusDead: t(`${K}.statusDead`), cycleAria: tmpl('cycleAria', '{name}: change status') }} />
          {isHost && (
            <button type="button" onClick={editor.initializeState}
              className="self-start text-xs text-muted-foreground underline hover:text-foreground">
              {t(`${K}.resetCta`)}
            </button>
          )}
        </>
      ) : isHost ? (
        <button type="button" data-slot="paleo-init" onClick={editor.initializeState}
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
cd apps/web && pnpm exec eslint --max-warnings=0 src/components/features/session-live/flavors/paleo/PaleoLiveFlavor.tsx
pnpm exec vitest run src/components/features/session-live/flavors/paleo/__tests__/PaleoLiveFlavor.test.tsx
```
Expected: eslint clean; test PASS (4 tests incl. axe).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/paleo/PaleoLiveFlavor.tsx" "apps/web/src/components/features/session-live/flavors/paleo/__tests__/PaleoLiveFlavor.test.tsx"
git commit -m "feat(session-live): #2789 Paleo L3 flavor container (self-builds labels)"
```

---

## Task 6: Wire into the registry + i18n

**Files:**
- Modify: `apps/web/src/components/features/session-live/FlavorRenderer.tsx`
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Test: `apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx` (extend)

**Interfaces:**
- Consumes: `PaleoLiveFlavor` (a `FlavorProps` component). `FlavorRenderer` is already game-agnostic — additive entry.

- [ ] **Step 1: Add the FLAVOR_MAP entry**

In `FlavorRenderer.tsx`, add a module-scope lazy component alongside the existing ones (match the codenames/puerto-rico pattern exactly), then add the map entry:

```tsx
const PaleoLiveFlavorLazy: FlavorComponent = dynamic(
  () => import('./flavors/paleo/PaleoLiveFlavor').then(m => ({ default: m.PaleoLiveFlavor })),
  { ssr: false, loading: () => <FlavorLoadingSkeleton /> }
);
```
and add `'paleo': { live: PaleoLiveFlavorLazy }` to `FLAVOR_MAP`.

- [ ] **Step 2: Extend the FlavorRenderer guard test**

In `FlavorRenderer.test.tsx`, add inside the `hasFlavor` describe: `expect(hasFlavor('paleo')).toBe(true);`

- [ ] **Step 3: Add the i18n keys**

In `src/locales/it.json`, under `pages.sessionLive.flavor`, add a `"paleo"` sibling:

```json
"paleo": {
  "panelAriaLabel": "Paleo",
  "leaderboardHeading": "Classifica",
  "initBoardCta": "Inizia partita",
  "resetCta": "Reimposta stato",
  "viewerWaiting": "In attesa dell'host…",
  "resourcesHeading": "Risorse",
  "wood": "Legno",
  "stone": "Pietra",
  "food": "Cibo",
  "knowledge": "Conoscenza",
  "tribeHeading": "Tribù",
  "statusAlive": "Vivo",
  "statusWounded": "Ferito",
  "statusDead": "Morto",
  "incAria": "{field} +1",
  "decAria": "{field} -1",
  "cycleAria": "{name}: cambia stato"
}
```

Mirror in `src/locales/en.json` with English copy (`"leaderboardHeading": "Standings"`, `"initBoardCta": "Start game"`, `"resetCta": "Reset state"`, `"viewerWaiting": "Waiting for the host…"`, `"resourcesHeading": "Resources"`, `"wood": "Wood"`, `"stone": "Stone"`, `"food": "Food"`, `"knowledge": "Knowledge"`, `"tribeHeading": "Tribe"`, `"statusAlive": "Alive"`, `"statusWounded": "Wounded"`, `"statusDead": "Dead"`, `"cycleAria": "{name}: change status"`; `incAria`/`decAria` identical). BOTH locales MUST have the identical key set.

- [ ] **Step 4: Typecheck + run affected suites + eslint**

```bash
cd apps/web
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run src/components/features/session-live/flavors/paleo src/components/features/session-live/__tests__/FlavorRenderer.test.tsx
pnpm exec eslint --max-warnings=0 src/components/features/session-live/FlavorRenderer.tsx
```
Expected: typecheck clean; all paleo suites pass + the guard test's new assertion passes; eslint clean. (Note: `FlavorRenderer.test.tsx > "lazy-loads the Catan flavor"` is a KNOWN pre-existing baseline flake in isolated single-file runs — unrelated to this change.)

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/FlavorRenderer.tsx" "apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx" "apps/web/src/locales/it.json" "apps/web/src/locales/en.json"
git commit -m "feat(session-live): #2789 wire Paleo flavor into the registry + i18n"
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
Expected: typecheck clean; all catan + wingspan + codenames + puerto-rico + paleo flavor tests pass (the Catan lazy-load isolated flake aside).

- [ ] **Step 2: Lint the whole flavor dir**

```bash
pnpm exec eslint --max-warnings=0 "src/components/features/session-live/flavors/paleo/**/*.{ts,tsx}" src/components/features/session-live/FlavorRenderer.tsx
```
Expected: no errors.

- [ ] **Step 3: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2789-paleo-l2-l3
gh pr create --base main-dev --head feature/issue-2789-paleo-l2-l3 \
  --title "feat(session-live): #2789 Paleo L2+L3 live flavor (resources + tribe status)" \
  --body "Implements the Paleo live flavor per docs/superpowers/specs/2026-07-17-paleo-l2-l3-flavor-design.md. FE-only; reuses the game-agnostic plumbing. Closes #2789."
```

---

## Self-Review

**1. Spec coverage:**
- L2 schema + helpers (fixed resources object, survivors record, nextPaleoStatus) → Task 1. ✅
- Editor debounced (bumpResource clamp, cycleSurvivorStatus folds missing player) → Task 2. ✅
- ResourcePanel + palette → Task 3. TribePanel → Task 4. ✅
- Container (leaderboard ungated, panels gated, self-builds labels) → Task 5. ✅
- Wiring (FLAVOR_MAP + i18n) → Task 6. ✅
- VP-from-scoring invariant → Tasks 4/5 (read `livePoints`/`totalScore`, never gameState). ✅
- Lint gate per task → Steps in Tasks 3–6 + Task 7. ✅
- Testing (unit + component + jest-axe) → Tasks 1–5. ✅

**2. Placeholder scan:** No TBD/TODO; every code step has complete code. ✅

**3. Type consistency:** `PaleoGameState`, `PaleoResources`, `PaleoResource`, `PaleoStatus`, `parsePaleoGameState`, `emptyPaleoResources`, `initialPaleoState`, `nextPaleoStatus`, `PALEO_RESOURCES`, `PALEO_STATUSES`, `usePaleoStateEditor` signature, and the 2 component prop interfaces are used consistently Task 1→6. The container's panel labels match `PaleoResourcePanelProps['labels']` / `PaleoTribePanelProps['labels']`; editor mutator names (`bumpResource`/`cycleSurvivorStatus`) match the container's `onBump`/`onCycle` wiring. ✅

**Known follow-ups (out of scope):** free-text tribe roster; mission progress trackers; a Paleo summary flavor.
