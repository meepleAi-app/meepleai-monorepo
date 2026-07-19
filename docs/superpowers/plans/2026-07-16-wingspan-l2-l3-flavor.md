# Wingspan L2+L3 Flavor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Wingspan live flavor — a themed category-scoring breakdown (reusing the existing scoring system) plus a small round-context `gameState` — on the L1 game-state layer, and generalize the flavor plumbing so flavors self-build their i18n labels.

**Architecture:** FE-only, reuses the proven Catan L2/L3 pattern under `flavors/wingspan/` but lighter (Wingspan's live richness is scoring, not a board). The 6 Wingspan VP categories are existing scoring dimensions; the new `gameState` carries only round context. `FlavorRenderer` becomes game-agnostic and each flavor self-builds its labels via `useIntl` (Catan refactored to match).

**Tech Stack:** Next.js 16 · React 19 · TypeScript · Zod · Zustand (`useLiveSessionStore`) · TanStack Query (`useUpdateLiveGameState`) · react-intl (`useIntl`) + `@/hooks/useTranslation` · Vitest + Testing Library + jest-axe · Tailwind semantic tokens.

## Global Constraints

- **Issue:** #2788 (G6b, epic #3025). Spec: `docs/superpowers/specs/2026-07-16-wingspan-l2-l3-flavor-design.md`.
- **Zero backend changes.** Scores use the existing scoring editor/endpoints; `gameState` is the opaque L1 blob.
- **State schema:** `v: 1`, discriminator `game: 'wingspan'`. `parseWingspanGameState` returns `null` (never throws) on wrong game/version/shape.
- **`gameState` carries round context ONLY:** `{ v, game, round: 1..4, roundGoals: {label}[] (≤4) }`. Never scores (VP stays in scoring).
- **Round turn budget** (derived constant, not stored): `[8, 7, 6, 5]` for rounds 1..4.
- **6 canonical VP category ids** (match scoring dimension names): `birds · bonusCards · endOfRoundGoals · eggs · cachedFood · tuckedCards`.
- **Host-edit only** (`viewerRole === 'Host'`); autosave debounced **500 ms**; optimistic `setGameState` first; flush on unmount.
- **Scoring renders unconditionally** (from `roundScores`/`livePoints`); only the round tracker is `gameState`-gated (host CTA when null).
- **Flavors self-build i18n labels** via `useIntl` + `useTranslation`; `FlavorRenderer` passes only game-agnostic props (`session`, `viewerRole`, `sessionId`, `className`, `livePoints`, `phaseName`).
- **Colors:** semantic Tailwind tokens + entity utilities; no hardcoded color utilities. i18n templates (`{n}`/`{name}`) read via `intl.messages[id] as string ?? fallback` (react-intl does not ICU-interpolate these); static labels via `t(id)`.
- **Tests:** Vitest, TDD, output pristine. Query via `data-slot`/roles, not `getByTestId`. Files under `apps/web/src/components/features/session-live/flavors/wingspan/`. Run from `apps/web`.
- **Windows:** pre-commit runs `pnpm typecheck` (~2 min) — allow ≥5 min for commits; if TS2307 on stale `.next/types`, `rm -rf .next/types` first (never `--no-verify`).

## File Structure

Create:
- `flavors/wingspan/wingspan-state.ts` — schema/types/`parseWingspanGameState`/`initialWingspanState`/`WINGSPAN_CATEGORIES`/`WINGSPAN_ROUND_TURN_BUDGET`.
- `flavors/wingspan/use-wingspan-state-editor.ts` — host mutators + optimistic + debounced PUT.
- `flavors/wingspan/WingspanRoundTracker.tsx` — round + goals (pure).
- `flavors/wingspan/WingspanCategoryBreakdown.tsx` — per-player category sums (pure).
- `flavors/wingspan/WingspanLiveFlavor.tsx` — container (self-builds labels).
- `flavors/wingspan/__tests__/*`.

Modify (Task 6, one commit):
- `session-live/FlavorRenderer.tsx` — game-agnostic `FlavorProps`; `wingspan` in `FLAVOR_MAP`; drop `labels`.
- `flavors/catan/CatanLiveFlavor.tsx` — self-build labels via `useIntl`; drop the `labels` prop.
- `sessions/[id]/live/_components/SessionLiveView.tsx` — remove `catanFlavorLabels` memo + `labels=` prop at both `FlavorRenderer` sites (~L1409, ~L1635).
- `src/locales/it.json` + `en.json` — add `pages.sessionLive.flavor.wingspan.*`.

---

## Task 1: L2 state schema + parser + categories

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/wingspan/wingspan-state.ts`
- Test: `apps/web/src/components/features/session-live/flavors/wingspan/__tests__/wingspan-state.test.ts`

**Interfaces:**
- Produces: `WingspanGameState`, `WingspanRoundGoal` types; `parseWingspanGameState(raw): WingspanGameState | null`; `initialWingspanState(): WingspanGameState`; `WINGSPAN_STATE_VERSION = 1`; `WINGSPAN_ROUND_TURN_BUDGET = [8,7,6,5]`; `WINGSPAN_CATEGORIES: ReadonlyArray<{ id: string; emoji: string }>`.

- [ ] **Step 1: Write the failing test**

```ts
// wingspan-state.test.ts
import { describe, expect, it } from 'vitest';

import {
  WINGSPAN_CATEGORIES,
  WINGSPAN_ROUND_TURN_BUDGET,
  initialWingspanState,
  parseWingspanGameState,
} from '../wingspan-state';

const VALID = {
  v: 1,
  game: 'wingspan',
  round: 3,
  roundGoals: [{ label: 'Nidi' }, { label: 'Uova nel forest' }],
};

describe('parseWingspanGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parseWingspanGameState(VALID);
    expect(parsed?.round).toBe(3);
    expect(parsed?.roundGoals).toHaveLength(2);
  });

  it('returns null for a different game', () => {
    expect(parseWingspanGameState({ ...VALID, game: 'catan' })).toBeNull();
  });

  it('returns null for a future version', () => {
    expect(parseWingspanGameState({ ...VALID, v: 2 })).toBeNull();
  });

  it('returns null for a round out of range', () => {
    expect(parseWingspanGameState({ ...VALID, round: 0 })).toBeNull();
    expect(parseWingspanGameState({ ...VALID, round: 5 })).toBeNull();
  });

  it('returns null for malformed / non-object input', () => {
    expect(parseWingspanGameState(null)).toBeNull();
    expect(parseWingspanGameState('nope')).toBeNull();
    expect(parseWingspanGameState({ v: 1, game: 'wingspan' })).toBeNull();
  });

  it('accepts empty roundGoals', () => {
    expect(parseWingspanGameState({ ...VALID, roundGoals: [] })?.roundGoals).toEqual([]);
  });
});

describe('initialWingspanState', () => {
  it('starts at round 1 with no goals', () => {
    expect(initialWingspanState()).toEqual({ v: 1, game: 'wingspan', round: 1, roundGoals: [] });
  });
});

describe('constants', () => {
  it('has the standard 4-round turn budget', () => {
    expect(WINGSPAN_ROUND_TURN_BUDGET).toEqual([8, 7, 6, 5]);
  });

  it('exposes the 6 canonical VP category ids', () => {
    expect(WINGSPAN_CATEGORIES.map(c => c.id)).toEqual([
      'birds',
      'bonusCards',
      'endOfRoundGoals',
      'eggs',
      'cachedFood',
      'tuckedCards',
    ]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/wingspan/__tests__/wingspan-state.test.ts`
Expected: FAIL — `Cannot find module '../wingspan-state'`.

- [ ] **Step 3: Write the implementation**

```ts
// wingspan-state.ts
import { z } from 'zod';

export const WINGSPAN_STATE_VERSION = 1;

/** Turns available per round (base game), rounds 1..4. Derived constant, never stored. */
export const WINGSPAN_ROUND_TURN_BUDGET = [8, 7, 6, 5] as const;

/**
 * The 6 canonical Wingspan VP categories. `id` is the scoring dimension name the flavor
 * sums over `roundScores`; `emoji` is language-agnostic display. Labels come from i18n.
 */
export const WINGSPAN_CATEGORIES: ReadonlyArray<{ id: string; emoji: string }> = [
  { id: 'birds', emoji: '🐦' },
  { id: 'bonusCards', emoji: '🎴' },
  { id: 'endOfRoundGoals', emoji: '🎯' },
  { id: 'eggs', emoji: '🥚' },
  { id: 'cachedFood', emoji: '🌰' },
  { id: 'tuckedCards', emoji: '🍃' },
];

export const WingspanRoundGoalSchema = z.object({ label: z.string() });
export type WingspanRoundGoal = z.infer<typeof WingspanRoundGoalSchema>;

export const WingspanGameStateSchema = z.object({
  v: z.literal(WINGSPAN_STATE_VERSION),
  game: z.literal('wingspan'),
  round: z.number().int().min(1).max(4),
  roundGoals: z.array(WingspanRoundGoalSchema).max(4),
});
export type WingspanGameState = z.infer<typeof WingspanGameStateSchema>;

/** Safe-parse the opaque L1 gameState. Returns null (never throws) on wrong game/version/shape. */
export function parseWingspanGameState(raw: unknown): WingspanGameState | null {
  const result = WingspanGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function initialWingspanState(): WingspanGameState {
  return { v: WINGSPAN_STATE_VERSION, game: 'wingspan', round: 1, roundGoals: [] };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/wingspan/__tests__/wingspan-state.test.ts`
Expected: PASS (10 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/wingspan/wingspan-state.ts" "apps/web/src/components/features/session-live/flavors/wingspan/__tests__/wingspan-state.test.ts"
git commit -m "feat(session-live): #2788 Wingspan L2 state schema + categories"
```

---

## Task 2: Host-edit hook

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/wingspan/use-wingspan-state-editor.ts`
- Test: `apps/web/src/components/features/session-live/flavors/wingspan/__tests__/use-wingspan-state-editor.test.tsx`

**Interfaces:**
- Consumes: `WingspanGameState`, `parseWingspanGameState`, `initialWingspanState` from `./wingspan-state`; `useLiveSessionStore` from `@/lib/stores/live-session-store`; `useUpdateLiveGameState` from `@/hooks/mutations/useUpdateLiveGameState`; `useDebouncedCallback` from `@/lib/session-live/use-debounced-callback`.
- Produces: `useWingspanStateEditor(sessionId: string): WingspanStateEditor` where
  ```ts
  interface WingspanStateEditor {
    state: WingspanGameState | null;
    initializeState: () => void;
    setRound: (round: number) => void;      // clamp 1..4
    advanceRound: () => void;               // min(round+1, 4)
    setRoundGoal: (index: number, label: string) => void; // index 0..3
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// use-wingspan-state-editor.test.tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { useWingspanStateEditor } from '../use-wingspan-state-editor';
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
  return useLiveSessionStore.getState().gameState as import('../wingspan-state').WingspanGameState | null;
}

describe('useWingspanStateEditor', () => {
  it('initializeState writes round 1 + empty goals optimistically', () => {
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.initializeState());
    expect(current()).toEqual({ v: 1, game: 'wingspan', round: 1, roundGoals: [] });
  });

  it('advanceRound increments and caps at 4', () => {
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.advanceRound());
    expect(current()?.round).toBe(2);
    act(() => result.current.setRound(4));
    act(() => result.current.advanceRound());
    expect(current()?.round).toBe(4);
  });

  it('setRound clamps to 1..4', () => {
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setRound(9));
    expect(current()?.round).toBe(4);
    act(() => result.current.setRound(0));
    expect(current()?.round).toBe(1);
  });

  it('setRoundGoal writes the label at the index (padding earlier slots)', () => {
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setRoundGoal(1, 'Uova nel forest'));
    expect(current()?.roundGoals).toEqual([{ label: '' }, { label: 'Uova nel forest' }]);
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.advanceRound());
    expect(current()).toBeNull();
  });

  it('eventually PUTs the state (debounced)', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => vi.advanceTimersByTime(600));
    expect(mutateMock).toHaveBeenCalled();
    vi.useRealTimers();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/wingspan/__tests__/use-wingspan-state-editor.test.tsx`
Expected: FAIL — `Cannot find module '../use-wingspan-state-editor'`.

- [ ] **Step 3: Write the implementation**

```ts
// use-wingspan-state-editor.ts
'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import {
  initialWingspanState,
  parseWingspanGameState,
  type WingspanGameState,
  type WingspanRoundGoal,
} from './wingspan-state';

export interface WingspanStateEditor {
  state: WingspanGameState | null;
  initializeState: () => void;
  setRound: (round: number) => void;
  advanceRound: () => void;
  setRoundGoal: (index: number, label: string) => void;
}

const clampRound = (n: number) => (n < 1 ? 1 : n > 4 ? 4 : n);

export function useWingspanStateEditor(sessionId: string): WingspanStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parseWingspanGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: WingspanGameState) => mutate(next),
    500
  );

  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: WingspanGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  const readState = useCallback(
    (): WingspanGameState | null => parseWingspanGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const initializeState = useCallback(() => commit(initialWingspanState()), [commit]);

  const setRound = useCallback(
    (round: number) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, round: clampRound(round) });
    },
    [commit, readState]
  );

  const advanceRound = useCallback(() => {
    const cur = readState();
    if (cur == null) return;
    commit({ ...cur, round: clampRound(cur.round + 1) });
  }, [commit, readState]);

  const setRoundGoal = useCallback(
    (index: number, label: string) => {
      const cur = readState();
      if (cur == null || index < 0 || index > 3) return;
      const goals: WingspanRoundGoal[] = [...cur.roundGoals];
      while (goals.length <= index) goals.push({ label: '' });
      goals[index] = { label };
      commit({ ...cur, roundGoals: goals });
    },
    [commit, readState]
  );

  return { state, initializeState, setRound, advanceRound, setRoundGoal };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/wingspan/__tests__/use-wingspan-state-editor.test.tsx`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/wingspan/use-wingspan-state-editor.ts" "apps/web/src/components/features/session-live/flavors/wingspan/__tests__/use-wingspan-state-editor.test.tsx"
git commit -m "feat(session-live): #2788 Wingspan L2 host-edit hook (round + goals)"
```

---

## Task 3: WingspanRoundTracker

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/wingspan/WingspanRoundTracker.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/wingspan/__tests__/WingspanRoundTracker.test.tsx`

**Interfaces:**
- Consumes: `WingspanGameState`, `WINGSPAN_ROUND_TURN_BUDGET` from `./wingspan-state`.
- Produces: `WingspanRoundTracker` with props
  ```ts
  interface WingspanRoundTrackerLabels {
    heading: string; roundTemplate: string; /* "Round {n}/4" */ turnBudgetTemplate: string; /* "{n} turni" */
    goalsHeading: string; goalPlaceholderTemplate: string; /* "Obiettivo round {n}" */ advanceRoundLabel: string;
  }
  interface WingspanRoundTrackerProps {
    state: WingspanGameState;
    editable: boolean;
    onAdvanceRound?: () => void;
    onSetRoundGoal?: (index: number, label: string) => void;
    labels: WingspanRoundTrackerLabels;
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// WingspanRoundTracker.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { WingspanRoundTracker } from '../WingspanRoundTracker';

const labels = {
  heading: 'Round',
  roundTemplate: 'Round {n}/4',
  turnBudgetTemplate: '{n} turni',
  goalsHeading: 'Obiettivi',
  goalPlaceholderTemplate: 'Obiettivo round {n}',
  advanceRoundLabel: 'Avanza round',
};
const state = { v: 1 as const, game: 'wingspan' as const, round: 2, roundGoals: [{ label: 'Nidi' }] };

describe('WingspanRoundTracker', () => {
  it('shows the current round and its turn budget', () => {
    render(<WingspanRoundTracker state={state} editable={false} labels={labels} />);
    expect(screen.getByText('Round 2/4')).toBeInTheDocument();
    expect(screen.getByText('7 turni')).toBeInTheDocument(); // budget[1] = 7
  });

  it('read-only mode exposes no controls', () => {
    render(<WingspanRoundTracker state={state} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.queryByRole('textbox')).toBeNull();
  });

  it('host mode: advance-round button fires onAdvanceRound', async () => {
    const onAdvanceRound = vi.fn();
    render(
      <WingspanRoundTracker state={state} editable onAdvanceRound={onAdvanceRound} labels={labels} />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Avanza round' }));
    expect(onAdvanceRound).toHaveBeenCalledOnce();
  });

  it('host mode: editing a goal input fires onSetRoundGoal', async () => {
    const onSetRoundGoal = vi.fn();
    render(
      <WingspanRoundTracker
        state={{ ...state, roundGoals: [] }}
        editable
        onSetRoundGoal={onSetRoundGoal}
        labels={labels}
      />
    );
    const firstGoal = screen.getAllByRole('textbox')[0];
    await userEvent.type(firstGoal, 'X');
    expect(onSetRoundGoal).toHaveBeenCalledWith(0, 'X');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/wingspan/__tests__/WingspanRoundTracker.test.tsx`
Expected: FAIL — `Cannot find module '../WingspanRoundTracker'`.

- [ ] **Step 3: Write the implementation**

```tsx
// WingspanRoundTracker.tsx
'use client';

import { type ReactElement } from 'react';

import { WINGSPAN_ROUND_TURN_BUDGET, type WingspanGameState } from './wingspan-state';

export interface WingspanRoundTrackerLabels {
  readonly heading: string;
  readonly roundTemplate: string; // "Round {n}/4"
  readonly turnBudgetTemplate: string; // "{n} turni"
  readonly goalsHeading: string;
  readonly goalPlaceholderTemplate: string; // "Obiettivo round {n}"
  readonly advanceRoundLabel: string;
}

export interface WingspanRoundTrackerProps {
  readonly state: WingspanGameState;
  readonly editable: boolean;
  readonly onAdvanceRound?: () => void;
  readonly onSetRoundGoal?: (index: number, label: string) => void;
  readonly labels: WingspanRoundTrackerLabels;
}

export function WingspanRoundTracker({
  state,
  editable,
  onAdvanceRound,
  onSetRoundGoal,
  labels,
}: WingspanRoundTrackerProps): ReactElement {
  const budget = WINGSPAN_ROUND_TURN_BUDGET[Math.min(Math.max(state.round, 1), 4) - 1];
  // Always render 4 goal slots for the host; read-only shows only the entered ones.
  const slots = editable ? 4 : state.roundGoals.length;

  return (
    <section
      data-slot="wingspan-round-tracker"
      className="flex flex-col gap-2 rounded-lg border border-border bg-card p-3"
    >
      <div className="flex items-center gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {labels.heading}
        </h3>
        <span data-slot="wingspan-round" className="text-sm font-bold text-foreground">
          {labels.roundTemplate.replace('{n}', String(state.round))}
        </span>
        <span className="text-xs text-muted-foreground">
          {labels.turnBudgetTemplate.replace('{n}', String(budget))}
        </span>
        {editable && (
          <button
            type="button"
            onClick={() => onAdvanceRound?.()}
            className="ml-auto rounded-md border border-border bg-background px-2 py-1 text-xs font-semibold text-foreground hover:bg-muted"
          >
            {labels.advanceRoundLabel}
          </button>
        )}
      </div>

      <div data-slot="wingspan-goals" className="flex flex-col gap-1">
        <span className="text-[10px] uppercase tracking-wide text-muted-foreground">
          {labels.goalsHeading}
        </span>
        {Array.from({ length: slots }, (_, i) => {
          const label = state.roundGoals[i]?.label ?? '';
          const placeholder = labels.goalPlaceholderTemplate.replace('{n}', String(i + 1));
          return editable ? (
            <input
              key={i}
              type="text"
              aria-label={placeholder}
              placeholder={placeholder}
              value={label}
              onChange={e => onSetRoundGoal?.(i, e.target.value)}
              className="rounded-md border border-border bg-background px-2 py-1 text-xs text-foreground"
            />
          ) : (
            <span key={i} className="rounded bg-muted px-2 py-1 text-xs text-foreground">
              {label || placeholder}
            </span>
          );
        })}
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/wingspan/__tests__/WingspanRoundTracker.test.tsx`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/wingspan/WingspanRoundTracker.tsx" "apps/web/src/components/features/session-live/flavors/wingspan/__tests__/WingspanRoundTracker.test.tsx"
git commit -m "feat(session-live): #2788 Wingspan L3 round tracker"
```

---

## Task 4: WingspanCategoryBreakdown

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/wingspan/WingspanCategoryBreakdown.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/wingspan/__tests__/WingspanCategoryBreakdown.test.tsx`

**Interfaces:**
- Consumes: `WINGSPAN_CATEGORIES` from `./wingspan-state`; `LiveSessionDto`, `LiveSessionPlayerDto` from `@/lib/api/schemas/live-sessions.schemas`.
- Produces: `WingspanCategoryBreakdown` with props
  ```ts
  interface WingspanCategoryBreakdownProps {
    players: ReadonlyArray<LiveSessionPlayerDto>;
    roundScores: LiveSessionDto['roundScores'];
    categoryLabels: Record<string, string>; // by category id
    heading: string;
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// WingspanCategoryBreakdown.test.tsx
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { WingspanCategoryBreakdown } from '../WingspanCategoryBreakdown';

const players = [
  { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 12, currentRank: 1, joinedAt: '', isActive: true },
] as const;

const roundScores = [
  { playerId: 'p1', round: 1, dimension: 'eggs', value: 3, unit: null, recordedAt: '' },
  { playerId: 'p1', round: 2, dimension: 'eggs', value: 4, unit: null, recordedAt: '' },
  { playerId: 'p1', round: 1, dimension: 'birds', value: 5, unit: null, recordedAt: '' },
];

const categoryLabels = {
  birds: 'Uccelli', bonusCards: 'Bonus', endOfRoundGoals: 'Obiettivi',
  eggs: 'Uova', cachedFood: 'Cibo', tuckedCards: 'Infilate',
};

describe('WingspanCategoryBreakdown', () => {
  it('sums roundScores per player per category', () => {
    const { container } = render(
      <WingspanCategoryBreakdown players={players} roundScores={roundScores} categoryLabels={categoryLabels} heading="Categorie" />
    );
    // eggs = 3 + 4 = 7 for p1
    const eggs = container.querySelector('[data-player="p1"][data-category="eggs"]');
    expect(eggs?.textContent).toContain('7');
    const birds = container.querySelector('[data-player="p1"][data-category="birds"]');
    expect(birds?.textContent).toContain('5');
    // a category with no scores shows 0
    const food = container.querySelector('[data-player="p1"][data-category="cachedFood"]');
    expect(food?.textContent).toContain('0');
  });

  it('renders the player name and all 6 categories', () => {
    const { container } = render(
      <WingspanCategoryBreakdown players={players} roundScores={roundScores} categoryLabels={categoryLabels} heading="Categorie" />
    );
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(container.querySelectorAll('[data-player="p1"][data-category]')).toHaveLength(6);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/wingspan/__tests__/WingspanCategoryBreakdown.test.tsx`
Expected: FAIL — `Cannot find module '../WingspanCategoryBreakdown'`.

- [ ] **Step 3: Write the implementation**

```tsx
// WingspanCategoryBreakdown.tsx
'use client';

import { type ReactElement } from 'react';

import type { LiveSessionDto, LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { WINGSPAN_CATEGORIES } from './wingspan-state';

export interface WingspanCategoryBreakdownProps {
  readonly players: ReadonlyArray<LiveSessionPlayerDto>;
  readonly roundScores: LiveSessionDto['roundScores'];
  readonly categoryLabels: Record<string, string>;
  readonly heading: string;
}

function sumCategory(
  roundScores: LiveSessionDto['roundScores'],
  playerId: string,
  categoryId: string
): number {
  return roundScores
    .filter(rs => rs.playerId === playerId && rs.dimension === categoryId)
    .reduce((sum, rs) => sum + rs.value, 0);
}

export function WingspanCategoryBreakdown({
  players,
  roundScores,
  categoryLabels,
  heading,
}: WingspanCategoryBreakdownProps): ReactElement {
  return (
    <section data-slot="wingspan-breakdown" className="flex flex-col gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {heading}
      </h3>
      <ul role="list" className="flex flex-col gap-1">
        {players.map(player => (
          <li key={player.id} className="flex flex-col gap-1 rounded-lg bg-card px-2 py-1.5">
            <span className="text-xs font-semibold text-foreground">{player.displayName}</span>
            <span className="flex flex-wrap gap-x-3 gap-y-0.5">
              {WINGSPAN_CATEGORIES.map(cat => (
                <span
                  key={cat.id}
                  data-player={player.id}
                  data-category={cat.id}
                  className="inline-flex items-center gap-1 text-xs text-muted-foreground"
                  title={categoryLabels[cat.id] ?? cat.id}
                >
                  <span aria-hidden="true">{cat.emoji}</span>
                  <span className="font-semibold tabular-nums text-foreground">
                    {sumCategory(roundScores, player.id, cat.id)}
                  </span>
                </span>
              ))}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/wingspan/__tests__/WingspanCategoryBreakdown.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/wingspan/WingspanCategoryBreakdown.tsx" "apps/web/src/components/features/session-live/flavors/wingspan/__tests__/WingspanCategoryBreakdown.test.tsx"
git commit -m "feat(session-live): #2788 Wingspan L3 category breakdown"
```

---

## Task 5: WingspanLiveFlavor container (self-builds labels)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/wingspan/WingspanLiveFlavor.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/wingspan/__tests__/WingspanLiveFlavor.test.tsx`

**Interfaces:**
- Consumes: `WingspanRoundTracker`, `WingspanCategoryBreakdown`, `WINGSPAN_CATEGORIES`, `parseWingspanGameState`, `useWingspanStateEditor`; `useLiveSessionStore`; `hasRequiredRole`, `ParticipantRole`; `LiveSessionDto`; `useIntl` (react-intl) + `useTranslation` (`@/hooks/useTranslation`).
- Produces: `WingspanLiveFlavor` + `WingspanLiveFlavorProps` (the game-agnostic flavor props):
  ```ts
  interface WingspanLiveFlavorProps {
    session: LiveSessionDto;
    viewerRole: ParticipantRole;
    sessionId: string;
    className?: string;
    livePoints?: ReadonlyMap<string, number> | null;
    phaseName?: string | null;
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// WingspanLiveFlavor.test.tsx
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { WingspanLiveFlavor } from '../WingspanLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

expect.extend(toHaveNoViolations);

vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

const session = {
  id: 's1', sessionCode: 'ABC', gameId: null, gameName: 'Wingspan', gameSlug: 'wingspan',
  createdByUserId: 'u1', status: 'InProgress', visibility: 'Private', groupId: null,
  createdAt: '', startedAt: '', pausedAt: null, completedAt: null, updatedAt: '', lastSavedAt: null,
  currentTurnIndex: 0, currentTurnPlayerId: 'p1', agentMode: 'None', notes: null,
  players: [
    { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 12, currentRank: 1, joinedAt: '', isActive: true },
    { id: 'p2', userId: null, displayName: 'Anna', avatarUrl: null, color: 'Blue', role: 'Player', teamId: null, totalScore: 9, currentRank: 2, joinedAt: '', isActive: false },
  ],
  teams: [], roundScores: [{ playerId: 'p1', round: 1, dimension: 'eggs', value: 3, unit: null, recordedAt: '' }],
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

function renderFlavor(props: Partial<Parameters<typeof WingspanLiveFlavor>[0]> = {}) {
  return render(
    // onError swallows react-intl MISSING_TRANSLATION noise (empty messages → t() returns the key).
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <WingspanLiveFlavor session={session} viewerRole="Player" sessionId="s1" {...props} />
    </IntlProvider>
  );
}

beforeEach(() => useLiveSessionStore.getState().reset());

describe('WingspanLiveFlavor', () => {
  it('renders the leaderboard + category breakdown even with null gameState', () => {
    const { container } = renderFlavor();
    expect(container.querySelector('[data-slot="wingspan-breakdown"]')).not.toBeNull();
    expect(container.querySelectorAll('[data-slot="wingspan-leaderboard-row"]')).toHaveLength(2);
    // no round tracker (gameState null) — but for a host, a CTA appears
    expect(container.querySelector('[data-slot="wingspan-round-tracker"]')).toBeNull();
  });

  it('host sees the init-round CTA when gameState is null', () => {
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="wingspan-round-init"]')).not.toBeNull();
  });

  it('renders the round tracker when gameState is present', () => {
    useLiveSessionStore.getState().setGameState({ v: 1, game: 'wingspan', round: 2, roundGoals: [] });
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="wingspan-round-tracker"]')).not.toBeNull();
  });

  it('has no axe violations (host, populated)', async () => {
    useLiveSessionStore.getState().setGameState({ v: 1, game: 'wingspan', round: 2, roundGoals: [{ label: 'Nidi' }] });
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/wingspan/__tests__/WingspanLiveFlavor.test.tsx`
Expected: FAIL — `Cannot find module '../WingspanLiveFlavor'`.

- [ ] **Step 3: Write the implementation**

```tsx
// WingspanLiveFlavor.tsx
'use client';

import { type ReactElement } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { WingspanCategoryBreakdown } from './WingspanCategoryBreakdown';
import { WingspanRoundTracker } from './WingspanRoundTracker';
import { WINGSPAN_CATEGORIES } from './wingspan-state';
import { useWingspanStateEditor } from './use-wingspan-state-editor';

export interface WingspanLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.wingspan';

export function WingspanLiveFlavor({
  session,
  viewerRole,
  sessionId,
  className,
  livePoints,
}: WingspanLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const editor = useWingspanStateEditor(sessionId);
  const state = editor.state;

  const tmpl = (id: string, fallback: string) =>
    (intl.messages[`${K}.${id}`] as string) ?? fallback;

  const categoryLabels: Record<string, string> = Object.fromEntries(
    WINGSPAN_CATEGORIES.map(c => [c.id, t(`${K}.category.${c.id}`)])
  );

  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));

  return (
    <section
      aria-label={t(`${K}.panelAriaLabel`)}
      data-slot="wingspan-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      {/* Leaderboard (always rendered — from scoring, not gameState) */}
      <div data-slot="wingspan-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t(`${K}.leaderboardHeading`)}
        </h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li
              key={player.id}
              data-slot="wingspan-leaderboard-row"
              className={[
                'flex items-center gap-2 rounded-lg px-2 py-1.5',
                idx === 0 ? 'border border-entity-session/40 bg-entity-session/10' : 'border border-transparent bg-card',
              ].join(' ')}
            >
              <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                {player.displayName}
                {idx === 0 && <span aria-hidden="true"> 🏆</span>}
              </span>
              <span
                aria-label={tmpl('scoreAriaTemplate', '{name}: {score}')
                  .replace('{name}', player.displayName)
                  .replace('{score}', String(scoreOf(player.id)))}
                className="shrink-0 tabular-nums text-sm font-bold text-foreground"
              >
                {scoreOf(player.id)}
              </span>
            </li>
          ))}
        </ul>
      </div>

      {/* Category breakdown (always rendered — from roundScores) */}
      <WingspanCategoryBreakdown
        players={session.players}
        roundScores={session.roundScores}
        categoryLabels={categoryLabels}
        heading={t(`${K}.categoriesHeading`)}
      />

      {/* Round tracker (gameState-gated) */}
      {state != null ? (
        <WingspanRoundTracker
          state={state}
          editable={isHost}
          onAdvanceRound={editor.advanceRound}
          onSetRoundGoal={editor.setRoundGoal}
          labels={{
            heading: t(`${K}.roundHeading`),
            roundTemplate: tmpl('roundTemplate', 'Round {n}/4'),
            turnBudgetTemplate: tmpl('turnBudgetTemplate', '{n} turni'),
            goalsHeading: t(`${K}.goalsHeading`),
            goalPlaceholderTemplate: tmpl('goalPlaceholderTemplate', 'Obiettivo round {n}'),
            advanceRoundLabel: t(`${K}.advanceRoundLabel`),
          }}
        />
      ) : isHost ? (
        <button
          type="button"
          data-slot="wingspan-round-init"
          onClick={editor.initializeState}
          className="self-start rounded-lg border border-entity-session/40 bg-entity-session/10 px-3 py-2 text-sm font-semibold text-entity-session hover:bg-entity-session/20"
        >
          {t(`${K}.initRoundCta`)}
        </button>
      ) : (
        <p role="status" aria-live="polite" className="text-xs text-muted-foreground">
          {t(`${K}.viewerWaiting`)}
        </p>
      )}
    </section>
  );
}
```

> Note: `WingspanLiveFlavor` is standalone here (not yet in `FLAVOR_MAP`) — it compiles unused. Task 6 wires it in. The test wraps it in `IntlProvider` (empty messages → `t()` returns the key, `intl.messages` templates fall back), which is enough to assert structure + axe.

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/wingspan/__tests__/WingspanLiveFlavor.test.tsx`
Expected: PASS (4 tests). If `t()` returning the raw key trips the axe check (unlikely), assert on `data-slot`s only.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/wingspan/WingspanLiveFlavor.tsx" "apps/web/src/components/features/session-live/flavors/wingspan/__tests__/WingspanLiveFlavor.test.tsx"
git commit -m "feat(session-live): #2788 Wingspan L3 flavor container (self-builds labels)"
```

---

## Task 6: Generalize FlavorRenderer + Catan self-builds labels + wire Wingspan + i18n (ONE commit)

**Why one commit:** changing `FlavorRenderer`'s props (dropping `labels`) and `CatanLiveFlavor`'s props (dropping `labels`) breaks `tsc` on `SessionLiveView` unless all land together; the pre-commit hook runs `pnpm typecheck`.

**Files:**
- Modify: `apps/web/src/components/features/session-live/FlavorRenderer.tsx`
- Modify: `apps/web/src/components/features/session-live/flavors/catan/CatanLiveFlavor.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`

**Interfaces:**
- Produces: `FlavorProps` (game-agnostic) exported from `FlavorRenderer.tsx`:
  ```ts
  interface FlavorProps {
    session: LiveSessionDto; viewerRole: ParticipantRole; sessionId: string;
    className?: string; livePoints?: ReadonlyMap<string, number> | null; phaseName?: string | null;
  }
  ```

- [ ] **Step 1: Make `FlavorRenderer` game-agnostic**

Rewrite `FlavorRenderer.tsx` to:
- Export `interface FlavorProps { session: LiveSessionDto; viewerRole: ParticipantRole; sessionId: string; className?: string; livePoints?: ReadonlyMap<string, number> | null; phaseName?: string | null; }`.
- `type FlavorComponent = ComponentType<FlavorProps>;` (drop the `CatanLiveFlavorProps` import).
- Add `WingspanLiveFlavorLazy` (module-scope `dynamic(() => import('./flavors/wingspan/WingspanLiveFlavor').then(m => ({ default: m.WingspanLiveFlavor })), { ssr: false, loading: () => <FlavorLoadingSkeleton /> })`).
- `FLAVOR_MAP = { catan: { live: CatanLiveFlavorLazy }, wingspan: { live: WingspanLiveFlavorLazy } }`.
- `FlavorRendererProps` = `FlavorProps & { gameSlug; view }` (drop `labels`).
- Forward only `session, viewerRole, sessionId, className, livePoints, phaseName` to `<LazyFlavor>`.

- [ ] **Step 2: Refactor `CatanLiveFlavor` to self-build labels**

In `CatanLiveFlavor.tsx`:
- Change `CatanLiveFlavorProps` to the game-agnostic shape: `{ session, viewerRole, sessionId, className?, livePoints?, phaseName? }` (drop `labels`). Keep `CatanLiveFlavorLabels` as an INTERNAL type (no longer a prop).
- Add `import { useIntl } from 'react-intl';` and `import { useTranslation } from '@/hooks/useTranslation';`.
- Inside the component, build `const labels: CatanLiveFlavorLabels = { … }` by moving the object literal currently in `SessionLiveView.tsx:1139-1171` verbatim (uses `t(...)` + `intl.messages[...] as string ?? fallback`). Declare `const { t } = useTranslation(); const intl = useIntl();` at the top of the component.
- Everywhere the component read `labels` from props, it now reads the locally-built `labels`.

- [ ] **Step 3: Clean up `SessionLiveView`**

In `SessionLiveView.tsx`:
- Delete the `catanFlavorLabels` `useMemo` (currently `:1138-1173`) and its entry in any dependency array (`:1498`).
- Remove the `import type { CatanLiveFlavorLabels }` if now unused, and the `type CatanLiveFlavorLabels` usage.
- At both `<FlavorRenderer ...>` sites (~`:1409` mobile, ~`:1635` desktop), delete the `labels={catanFlavorLabels}` line. Keep `gameSlug`, `view`, `session`, `viewerRole`, `sessionId`, `className`, `livePoints`, `phaseName`.

- [ ] **Step 4: Add Wingspan i18n keys**

In `src/locales/it.json`, under `pages.sessionLive.flavor`, add a `"wingspan"` sibling to `"catan"`:

```json
"wingspan": {
  "panelAriaLabel": "Wingspan",
  "leaderboardHeading": "Classifica",
  "scoreAriaTemplate": "{name}: {score} PV",
  "categoriesHeading": "Punti per categoria",
  "roundHeading": "Round",
  "roundTemplate": "Round {n}/4",
  "turnBudgetTemplate": "{n} turni",
  "goalsHeading": "Obiettivi di round",
  "goalPlaceholderTemplate": "Obiettivo round {n}",
  "advanceRoundLabel": "Avanza round",
  "initRoundCta": "Inizia il round",
  "viewerWaiting": "In attesa dell'host…",
  "category": {
    "birds": "Uccelli",
    "bonusCards": "Carte bonus",
    "endOfRoundGoals": "Obiettivi di fine round",
    "eggs": "Uova",
    "cachedFood": "Cibo accumulato",
    "tuckedCards": "Carte infilate"
  }
}
```

Mirror in `src/locales/en.json` with English copy (`"leaderboardHeading": "Standings"`, `"categoriesHeading": "Points by category"`, `"roundHeading": "Round"`, `"turnBudgetTemplate": "{n} turns"`, `"goalsHeading": "Round goals"`, `"goalPlaceholderTemplate": "Round {n} goal"`, `"advanceRoundLabel": "Advance round"`, `"initRoundCta": "Start the round"`, `"viewerWaiting": "Waiting for the host…"`, categories: `Birds / Bonus cards / End-of-round goals / Eggs / Cached food / Tucked cards`; `roundTemplate`/`scoreAriaTemplate` identical).

- [ ] **Step 5: Typecheck + run affected suites**

```bash
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run \
  src/components/features/session-live/flavors/wingspan \
  src/components/features/session-live/flavors/catan/__tests__/CatanLiveFlavor.test.tsx \
  src/components/features/session-live/__tests__/FlavorRenderer.test.tsx \
  "src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx"
```
Expected: typecheck clean; all suites PASS. `CatanLiveFlavor.test.tsx` currently passes a `labels` prop — update it to wrap the component in `<IntlProvider locale="en" messages={{}} onError={() => {}}>` (the `onError` swallows MISSING_TRANSLATION noise) and drop the `labels` prop (the flavor now self-builds labels; its assertions on `data-slot`s / roles hold with `t()` returning raw keys). If `SessionLiveView.test.tsx` referenced `catanFlavorLabels`, remove those references.

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/components/features/session-live/FlavorRenderer.tsx" "apps/web/src/components/features/session-live/flavors/catan/CatanLiveFlavor.tsx" "apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx" "apps/web/src/locales/it.json" "apps/web/src/locales/en.json"
# include the updated Catan/FlavorRenderer/SessionLiveView test files if you touched them
git commit -m "feat(session-live): #2788 game-agnostic FlavorRenderer + wire Wingspan + Catan self-builds labels"
```

---

## Task 7: Final verification

- [ ] **Step 1: Full typecheck + all flavor suites**

```bash
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run src/components/features/session-live/flavors src/components/features/session-live/__tests__/FlavorRenderer.test.tsx
```
Expected: typecheck clean; all Catan + Wingspan flavor tests PASS.

- [ ] **Step 2: Lint the touched files**

```bash
pnpm exec eslint --max-warnings=0 "src/components/features/session-live/flavors/wingspan/**/*.{ts,tsx}" "src/components/features/session-live/FlavorRenderer.tsx" "src/components/features/session-live/flavors/catan/CatanLiveFlavor.tsx"
```
Expected: no errors.

- [ ] **Step 3: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2788-wingspan-l2-l3
gh pr create --base main-dev --head feature/issue-2788-wingspan-l2-l3 \
  --title "feat(session-live): #2788 Wingspan L2+L3 flavor (themed scoring + round context)" \
  --body "Implements the Wingspan flavor per docs/superpowers/specs/2026-07-16-wingspan-l2-l3-flavor-design.md. FE-only. Generalizes FlavorRenderer (flavors self-build i18n labels). Closes #2788."
```

---

## Self-Review

**1. Spec coverage:**
- L2 schema + parser + categories + turn budget → Task 1. ✅
- Host-edit (round/goals, optimistic + debounced) → Task 2. ✅
- Round tracker → Task 3. Category breakdown (sums roundScores) → Task 4. ✅
- Container (scoring ungated, round tracker gated, self-builds labels) → Task 5. ✅
- Generalization (FlavorRenderer game-agnostic, Catan self-builds labels, SessionLiveView cleanup, wire Wingspan, i18n) → Task 6. ✅
- VP-from-scoring invariant → Tasks 4/5 (reads `roundScores`/`livePoints`, never `gameState`). ✅
- Known-seam (category id ↔ dimension name) → breakdown shows 0 gracefully (Task 4 test covers `cachedFood`=0). ✅
- Testing (unit + component + jest-axe) → Tasks 1–5. ✅
- No backend changes → whole plan FE-only. ✅

**2. Placeholder scan:** No TBD/TODO; every code step has complete code. Task 6 references exact existing lines to move (the `catanFlavorLabels` memo) — the code being moved already exists verbatim in the repo. ✅

**3. Type consistency:** `WingspanGameState`, `parseWingspanGameState`, `initialWingspanState`, `WINGSPAN_CATEGORIES` (`{id,emoji}`), `WINGSPAN_ROUND_TURN_BUDGET`, `useWingspanStateEditor` signature, the three component prop interfaces, and `FlavorProps` are used consistently across Tasks 1→6. Category ids match between `wingspan-state.ts`, the breakdown, and the i18n `category.*` keys. ✅

**Known follow-ups (out of scope):** aligning Wingspan session creation to the 6 canonical dimension names; per-player habitat/bird tableau; Wingspan summary flavor.
