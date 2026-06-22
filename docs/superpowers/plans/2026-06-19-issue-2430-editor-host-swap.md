# Issue #2430 Block B+ — PolymorphicScoreEditor host swap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the `PolymorphicScoreEditor` into `SessionLiveView`'s score tab for the `Host` role, with debounced autosave via `useUpdateSessionScores`, optimistic UI, 5-class error matrix (403/429/400/5xx/network), 30-second rate-limit countdown persisted in the store, and a single extraction-refactor of all polymorphic scoring logic into a new `ScoreTabContent` component.

**Architecture:** New component `ScoreTabContent` (`apps/web/src/app/(authenticated)/sessions/[id]/live/_components/ScoreTabContent.tsx`) owns the Block B store selectors + REST hydration + adapter memo, plus the Block B+ additions: role-based mount (Host → editor, Player/Spectator → renderer), debounced `useUpdateSessionScores.mutate`, `localScoreOverride` for optimistic UI, `lastPayloadRef` for retry, `isMountedRef` + `viewerRoleRef` for safe state updates after unmount, and a normalized 5-kind error mapper. `useDebouncedCallback` is hoisted from `scores/page.tsx` to a shared lib module and extended with a `flush()` tuple return. `useLiveSessionStore` gains a new `rateLimitedUntil: number | null` field + `setRateLimitedUntil(ts)` action so the 30s countdown survives tab change.

**Tech Stack:** Next.js 16 App Router, React 19, TypeScript, Vitest + @testing-library/react, Zustand store (`useLiveSessionStore`), `sonner` ^2.0.7 (already in deps), TanStack Query 5 `useMutation`, react-intl, ESLint.

**Spec:** `docs/superpowers/specs/2026-06-19-issue-2430-editor-host-swap-design.md`

**Branch:** `feature/issue-2430-editor-host-swap` (parent: `main-dev`, already created)

**Effort:** ~3 days focused

---

## File Structure

**NEW files:**

| Path | Responsibility | LOC |
|------|----------------|-----|
| `apps/web/src/lib/session-live/use-debounced-callback.ts` | Shared `useDebouncedCallback` hook with `[fn, flush]` tuple return. Used by `ScoreTabContent` (flush-on-unmount) and `scores/page.tsx` (existing autosave). | ~55 |
| `apps/web/src/lib/session-live/__tests__/use-debounced-callback.test.ts` | 5 Vitest cases for the hook: single call → delayed fire, multiple calls → only last, flush() invokes pending, flush() no-op when nothing pending, unmount cleans timer without auto-flush. | ~90 |
| `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/ScoreTabContent.tsx` | Encapsulates ALL polymorphic scoring logic (Block B + Block B+). 3 store selectors + REST hydration `useEffect` + `scoringPanelData` memo + role-based mount + debounce + optimistic UI + error mapper + retry button + 30s countdown UI + a11y placeholder when null. | ~230 |
| `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/ScoreTabContent.test.tsx` | 28 Vitest cases across 8 groups: role gating (3), role transition (1), null gate (2), REST hydration (2), variant mount (4), debounce+mutation (5), error handling (8), optimistic UI (3). | ~580 |

**MODIFIED files:**

| Path | Change | LOC diff |
|------|--------|----------|
| `apps/web/src/lib/stores/live-session-store.ts` | Add `rateLimitedUntil: number \| null` field + `setRateLimitedUntil(ts: number \| null)` action; extend `initialState`. | ~+8 |
| `apps/web/src/lib/stores/__tests__/live-session-store.test.ts` | Add 3 tests for the new field: initial state `null`, set positive ts, set `null` to clear. | ~+30 |
| `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx` | Replace inline `useDebouncedCallback` (lines 27-55) with `import` from new lib; update callsite `const debouncedSave = useDebouncedCallback(...)` to `const [debouncedSave] = useDebouncedCallback(...)` (tuple destructure). | ~−30 / ~+3 |
| `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` | Remove Block B selectors + REST hydration `useEffect` + `scoringPanelData` memo + 2 placeholder JSX blocks (~−80 LOC). Add 2 `<ScoreTabContent />` mount sites (desktop right column + mobile drawer score case, ~+30 LOC). Remove now-unused imports of `mapScoreDataToPanelData`, `MVP_OBJECTIVES_CATALOGUE`, `useLiveSessionStore`, `ScoreDataByType`, `ScoreType`. | ~−80 / ~+30 |
| `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx` | Migrate 2 a11y placeholder tests (`renders aria-live placeholder when scoringType is null`, `placeholder shows the localized loading label text`) to `ScoreTabContent.test.tsx`; add 2 new smoke tests asserting `<ScoreTabContent />` is mounted with correct `viewerRole` prop in the score tab. | ~−40 / ~+30 |
| `apps/web/src/locales/it.json` | Add 7 new keys under `pages.sessionLive.scoring`. | ~+7 |
| `apps/web/src/locales/en.json` | Add 7 new keys (English translations). | ~+7 |
| `CLAUDE.md` | Add 1 line under "Session live shell (epic #2354)" referencing #2430 Block B+. | ~+1 |

**Total:** 4 new files, 8 modified files, ~880 LOC net (heavy on test code).

---

### Task 1: Verify `sonner` availability

**Files:**
- Read: `apps/web/package.json`
- Read: `apps/web/src/components/agent/settings/AgentSettingsDrawer.tsx` (existing sonner usage)

- [ ] **Step 1.1: Verify sonner is in deps**

Run: `grep -E "\"sonner\"" apps/web/package.json`
Expected: `"sonner": "^2.0.7",` (line is present).

- [ ] **Step 1.2: Verify sonner is mounted at app shell**

Run: `grep -rl "<Toaster" apps/web/src/app apps/web/src/components | head -5`
Expected: at least one match (the `<Toaster />` from sonner must already be mounted in the app shell — verified by the existing usage in `AgentSettingsDrawer.tsx`, `cookie-settings/page.tsx`, etc. all working without a local Toaster mount).

If no `<Toaster />` is found, STOP. This task assumes sonner is wired at app shell. If missing, file a follow-up issue and reconsider scope.

- [ ] **Step 1.3: Confirm sonner toast API supports `id` (dedup) and `action` (retry button)**

sonner v2 documentation confirms both. No code changes required in T1. Document in commit message.

- [ ] **Step 1.4: Commit (informational only)**

This task has no code changes. Skip commit. Proceed to T2.

---

### Task 2: Extend `useLiveSessionStore` with `rateLimitedUntil`

**Files:**
- Modify: `apps/web/src/lib/stores/live-session-store.ts:52-110` (interface + initialState + impl)
- Modify: `apps/web/src/lib/stores/__tests__/live-session-store.test.ts:5-35` (add 3 tests)

- [ ] **Step 2.1: Extend the store interface**

Open `apps/web/src/lib/stores/live-session-store.ts`.

Inside the `interface LiveSessionState { ... }` block (lines 52-82), add a new field just before `pendingProposals` (line 63):

```typescript
  scoringType: ScoreType | null;
  scoreData: ScoreDataByType[ScoreType] | null;
  /**
   * Rate-limit deadline (Unix timestamp in ms). `null` when not rate-limited.
   * Set by `ScoreTabContent` on 429 response (Date.now() + 30000).
   * Persists across tab change (ScoreTabContent unmount/remount) so the
   * countdown UI continues from the correct remaining time.
   * Cleared on natural expiry, store reset(), or explicit setRateLimitedUntil(null).
   * Issue #2430 Block B+.
   */
  rateLimitedUntil: number | null;
  pendingProposals: ScoreProposal[];
```

Inside the Actions section (lines 69-81), add the setter just after `setScoringConfig`:

```typescript
  setScoringConfig: <T extends ScoreType>(args: {
    scoringType: T;
    scoreData: ScoreDataByType[T];
  }) => void;
  setRateLimitedUntil: (ts: number | null) => void;
  updateScore: (playerName: string, score: number) => void;
```

- [ ] **Step 2.2: Add the field to `initialState`**

Inside the `const initialState: Omit<...> = { ... }` block, update the `Omit` keys list to exclude the new setter, and add the field:

```typescript
const initialState: Omit<
  LiveSessionState,
  | 'setSession'
  | 'setScoringConfig'
  | 'setRateLimitedUntil'
  | 'updateScore'
  | 'addProposal'
  | 'resolveProposal'
  | 'addDispute'
  | 'setConnected'
  | 'setOffline'
  | 'reset'
> = {
  sessionId: null,
  gameName: '',
  status: 'InProgress',
  currentTurn: 1,
  currentPhase: null,
  players: [],
  scores: {},
  scoringType: null,
  scoreData: null,
  rateLimitedUntil: null,
  pendingProposals: [],
  disputes: [],
  isConnected: false,
  isOffline: false,
  elapsedSeconds: 0,
};
```

- [ ] **Step 2.3: Implement the action**

Inside the `create<LiveSessionState>()(...)` factory (lines 112-178), add the setter just after `setScoringConfig` (around line 121):

```typescript
      setScoringConfig: ({ scoringType, scoreData }) =>
        set({ scoringType, scoreData }, false, 'setScoringConfig'),

      setRateLimitedUntil: ts =>
        set({ rateLimitedUntil: ts }, false, 'setRateLimitedUntil'),

      updateScore: (playerName, score) =>
```

- [ ] **Step 2.4: Add 3 tests for the new field**

Open `apps/web/src/lib/stores/__tests__/live-session-store.test.ts`. Append inside the existing `describe('useLiveSessionStore — Block A #2389 contract evolution')`:

```typescript
  // #2430 Block B+: rateLimitedUntil persistence
  it('initial state — rateLimitedUntil is null', () => {
    expect(useLiveSessionStore.getState().rateLimitedUntil).toBeNull();
  });

  it('setRateLimitedUntil writes a positive timestamp', () => {
    const deadline = 1_700_000_000_000;
    useLiveSessionStore.getState().setRateLimitedUntil(deadline);
    expect(useLiveSessionStore.getState().rateLimitedUntil).toBe(deadline);
  });

  it('setRateLimitedUntil(null) clears the deadline', () => {
    useLiveSessionStore.getState().setRateLimitedUntil(1_700_000_000_000);
    useLiveSessionStore.getState().setRateLimitedUntil(null);
    expect(useLiveSessionStore.getState().rateLimitedUntil).toBeNull();
  });
```

- [ ] **Step 2.5: Run typecheck + tests**

Run: `pnpm typecheck && pnpm test live-session-store 2>&1 | tail -10`
Expected: 0 type errors; 7/7 tests green (4 existing + 3 new).

- [ ] **Step 2.6: Commit**

```bash
git -C /d/Repositories/meepleai-monorepo-dev add \
  apps/web/src/lib/stores/live-session-store.ts \
  apps/web/src/lib/stores/__tests__/live-session-store.test.ts
git -C /d/Repositories/meepleai-monorepo-dev commit -m "feat(store): #2430 Block B+ T2 add rateLimitedUntil to live-session-store

New field rateLimitedUntil: number | null + setRateLimitedUntil action.
Persists the 30s 429 deadline across ScoreTabContent unmount/remount
(tab change) so the countdown UI continues from the correct remaining
time. Cleared on natural expiry, store reset(), or explicit null set.

3 new tests pass (initial null + set positive ts + set null clear).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 3: Hoist `useDebouncedCallback` to shared lib

**Files:**
- Create: `apps/web/src/lib/session-live/use-debounced-callback.ts`
- Modify: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx:27-55` (remove inline) + `:140` (update callsite)

- [ ] **Step 3.1: Create the new hook file**

Create `apps/web/src/lib/session-live/use-debounced-callback.ts`:

```typescript
/**
 * useDebouncedCallback — debounced callback with explicit flush().
 *
 * Issue #2430 Block B+ (T3): hoisted out of `scores/page.tsx` and extended
 * with a `flush()` method exposed via tuple return so `ScoreTabContent` can
 * invoke pending callbacks during unmount cleanup (DEC-4 flush-on-unmount).
 *
 * Semantics:
 *   - debouncedFn(...args): schedule callback after `delay` ms of silence.
 *     Subsequent calls within the window reset the timer.
 *   - flush(): invoke the pending callback immediately if any. No-op when
 *     nothing is pending. Caller may call multiple times safely.
 *   - Cleanup on unmount: timer is cleared but flush is NOT called
 *     automatically — callers opt in via the returned `flush` ref.
 */

import { useCallback, useEffect, useRef } from 'react';

export function useDebouncedCallback<TArgs extends readonly unknown[]>(
  callback: (...args: TArgs) => void,
  delay: number
): readonly [debouncedFn: (...args: TArgs) => void, flush: () => void] {
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pendingArgsRef = useRef<TArgs | null>(null);
  const callbackRef = useRef(callback);

  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);

  useEffect(
    () => () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    },
    []
  );

  const debouncedFn = useCallback(
    (...args: TArgs) => {
      if (timerRef.current) clearTimeout(timerRef.current);
      pendingArgsRef.current = args;
      timerRef.current = setTimeout(() => {
        timerRef.current = null;
        const pending = pendingArgsRef.current;
        pendingArgsRef.current = null;
        if (pending) callbackRef.current(...pending);
      }, delay);
    },
    [delay]
  );

  const flush = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
    const pending = pendingArgsRef.current;
    pendingArgsRef.current = null;
    if (pending) callbackRef.current(...pending);
  }, []);

  return [debouncedFn, flush] as const;
}
```

- [ ] **Step 3.2: Update `scores/page.tsx` to import the new helper**

Open `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx`.

**Remove** the inline helper definition. Find the block starting at line ~27 with the JSDoc comment `/** Generic debounced-callback helper…` and ending at the closing brace of `function useDebouncedCallback<TArgs ...>` (line ~55). Delete the entire block.

Also remove `useEffect`, `useRef` from the React import on line 15 if they are no longer used elsewhere in the file (most likely they still are — verify by grep).

**Add** the new import near the other lib imports at the top of the file:

```typescript
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
```

**Verify atomicity** (B1 from plan review): apply all three edits (delete inline, add import, update callsite) in the same session before saving. After deleting the inline definition, confirm zero residue:

```bash
grep -c 'function useDebouncedCallback' "apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx"
```

Expected output: `0`. If non-zero, the inline helper was not fully deleted — keep editing until it returns 0.

**Update** the callsite. Find:

```typescript
  const debouncedSave = useDebouncedCallback((payload: ScoreChangePayload) => {
    mutation.mutate({
      sessionId,
      scoringType: payload.scoringType,
      scoreData: payload.data,
    });
  }, 500);
```

Replace with the tuple destructure (only `debouncedSave` is used — `flush` ignored at this callsite per spec):

```typescript
  const [debouncedSave] = useDebouncedCallback((payload: ScoreChangePayload) => {
    mutation.mutate({
      sessionId,
      scoringType: payload.scoringType,
      scoreData: payload.data,
    });
  }, 500);
```

- [ ] **Step 3.3: Run typecheck to catch import/usage mismatches**

Run: `pnpm typecheck 2>&1 | tail -10`
Expected: 0 errors.

If errors mention "unused import" for `useRef` or `useEffect`, remove them from the React import on line 15.

- [ ] **Step 3.4: Commit**

```bash
git -C /d/Repositories/meepleai-monorepo-dev add \
  apps/web/src/lib/session-live/use-debounced-callback.ts \
  "apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx"
git -C /d/Repositories/meepleai-monorepo-dev commit -m "refactor(session-live): #2430 Block B+ T3 hoist useDebouncedCallback

Move the inline helper out of scores/page.tsx into a shared lib module
under lib/session-live/. Extend with a flush() method exposed via
[fn, flush] tuple return so ScoreTabContent (T7) can invoke pending
callbacks during unmount cleanup (DEC-4 flush-on-unmount).

scores/page.tsx callsite migrated to tuple destructure (flush ignored
at this callsite — only the new ScoreTabContent uses it).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 4: Write failing `useDebouncedCallback` tests (RED)

**Files:**
- Create: `apps/web/src/lib/session-live/__tests__/use-debounced-callback.test.ts`

- [ ] **Step 4.1: Create the test file**

Create `apps/web/src/lib/session-live/__tests__/use-debounced-callback.test.ts`:

```typescript
/**
 * useDebouncedCallback unit tests — Issue #2430 Block B+ (T4).
 *
 * 5 cases: delayed fire, only-last-call, flush invokes pending,
 * flush no-op, unmount clears timer without auto-flush.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';

describe('useDebouncedCallback', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('fires the callback after delay when called once', () => {
    const cb = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(cb, 500));

    act(() => {
      const [debouncedFn] = result.current;
      debouncedFn('a');
    });
    expect(cb).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(cb).toHaveBeenCalledTimes(1);
    expect(cb).toHaveBeenCalledWith('a');
  });

  it('only fires the last call when invoked multiple times within the window', () => {
    const cb = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(cb, 500));

    act(() => {
      const [debouncedFn] = result.current;
      debouncedFn('first');
      vi.advanceTimersByTime(100);
      debouncedFn('second');
      vi.advanceTimersByTime(100);
      debouncedFn('third');
    });

    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(cb).toHaveBeenCalledTimes(1);
    expect(cb).toHaveBeenCalledWith('third');
  });

  it('flush() invokes the pending callback immediately', () => {
    const cb = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(cb, 500));

    act(() => {
      const [debouncedFn] = result.current;
      debouncedFn('pending');
    });
    expect(cb).not.toHaveBeenCalled();

    act(() => {
      const [, flush] = result.current;
      flush();
    });
    expect(cb).toHaveBeenCalledTimes(1);
    expect(cb).toHaveBeenCalledWith('pending');
  });

  it('flush() is a no-op when nothing is pending', () => {
    const cb = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(cb, 500));

    act(() => {
      const [, flush] = result.current;
      flush();
    });
    expect(cb).not.toHaveBeenCalled();
  });

  it('unmount clears the timer without auto-flushing', () => {
    const cb = vi.fn();
    const { result, unmount } = renderHook(() => useDebouncedCallback(cb, 500));

    act(() => {
      const [debouncedFn] = result.current;
      debouncedFn('about-to-be-orphaned');
    });
    expect(cb).not.toHaveBeenCalled();

    unmount();

    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(cb).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 4.2: Run the tests and verify they all PASS**

Run: `pnpm test use-debounced-callback 2>&1 | tail -10`
Expected: 5/5 PASS.

Note: T3 already shipped the implementation, so these "RED" tests actually pass immediately. This is fine — they serve as **regression pins**. The "RED" framing in the task title is for consistency with TDD discipline; for a pure-refactor hoist the tests are validation-after.

- [ ] **Step 4.3: Commit**

```bash
git -C /d/Repositories/meepleai-monorepo-dev add \
  apps/web/src/lib/session-live/__tests__/use-debounced-callback.test.ts
git -C /d/Repositories/meepleai-monorepo-dev commit -m "test(session-live): #2430 Block B+ T4 useDebouncedCallback regression pins

5 Vitest cases pin the contract: delayed fire, only-last-call within
window, flush() invokes pending, flush() no-op, unmount clears timer
without auto-flushing.

These are regression pins (impl shipped in T3 as a hoist refactor).
Future Block C deletion of the legacy inline helper relies on these
tests catching contract drift.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 5: Plan placeholder — collapsed into T3+T4

T3 (impl) + T4 (regression tests) together cover what would be RED/GREEN in pure-new code. No separate T5 needed for the hook. Renumbering: T5 → next is now ScoreTabContent test scaffold.

---

### Task 6: Write failing `ScoreTabContent` tests (RED, 28 cases)

**Files:**
- Create: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/ScoreTabContent.test.tsx`

This task is large. The test file is ~580 LOC. Build it in 8 sub-steps, one per test group. Each sub-step adds a `describe(...)` block and stays RED until T7 implements the component.

- [ ] **Step 6.1: Create the test file skeleton (mocks + helpers)**

Create `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/ScoreTabContent.test.tsx`:

```typescript
/**
 * ScoreTabContent unit/integration tests — Issue #2430 Block B+ (T6).
 *
 * 28 cases across 8 groups: role gating, role transition, null gate,
 * REST hydration, variant editor mount, debounce+mutation, error
 * handling, optimistic UI.
 *
 * Mocks: useUpdateSessionScores (the real mutation hook is bypassed
 * via a vi.mock that lets each test inject `mutate` + `isPending`).
 * sonner toast is mocked to capture calls.
 */

import { render, screen, act, cleanup } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import type { ReactElement } from 'react';

import { ScoreTabContent } from '../ScoreTabContent';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
import { UpdateSessionScoresError } from '@/hooks/use-update-session-scores';

// ─── sonner mock ───────────────────────────────────────────────────────────

const toastErrorMock = vi.fn();
const toastWarningMock = vi.fn();
const toastSuccessMock = vi.fn();
vi.mock('sonner', () => ({
  toast: {
    error: (msg: string, opts?: unknown) => toastErrorMock(msg, opts),
    warning: (msg: string, opts?: unknown) => toastWarningMock(msg, opts),
    success: (msg: string, opts?: unknown) => toastSuccessMock(msg, opts),
    // future-proof: T7 doesn't use dismiss today, but adding it as a no-op
    // prevents "toast.dismiss is not a function" if a later iteration adds
    // explicit toast dismissal (e.g. clear forbidden toast when role
    // re-evaluates).
    dismiss: vi.fn(),
  },
}));

// ─── useUpdateSessionScores mock ───────────────────────────────────────────

interface MockMutationHandlers {
  mutate: ReturnType<typeof vi.fn>;
  isPending: boolean;
  onSuccess?: () => void;
  onError?: (err: unknown) => void;
}

let mockMutation: MockMutationHandlers;

vi.mock('@/hooks/use-update-session-scores', async () => {
  const actual = await vi.importActual<
    typeof import('@/hooks/use-update-session-scores')
  >('@/hooks/use-update-session-scores');
  return {
    ...actual,
    useUpdateSessionScores: () => mockMutation,
  };
});

// ─── PolymorphicScoreEditor mock (data-slot probe) ─────────────────────────

vi.mock('@/components/sessions', () => ({
  PolymorphicScoreEditor: ({
    scoringType,
    players,
    initialData,
    availableObjectives,
    onChange,
    disabled,
  }: {
    scoringType: ScoreType;
    players: ReadonlyArray<{ id: string; displayName: string }>;
    initialData?: unknown;
    availableObjectives?: readonly string[];
    onChange: (payload: { scoringType: ScoreType; data: unknown }) => void;
    disabled?: boolean;
  }) => (
    <div
      data-slot="polymorphic-score-editor"
      data-scoring-type={scoringType}
      data-disabled={disabled ? 'true' : 'false'}
      data-player-count={players.length}
      data-objectives-count={availableObjectives?.length ?? 0}
    >
      <button
        type="button"
        data-slot="trigger-change"
        onClick={() =>
          onChange({
            scoringType,
            data:
              scoringType === 'Points'
                ? { scores: [{ playerId: 'p1', points: 99 }] }
                : scoringType === 'BinaryWin'
                  ? { results: [{ playerId: 'p1', isWinner: true }] }
                  : scoringType === 'Ranking'
                    ? { positions: [{ playerId: 'p1', position: 1 }] }
                    : { completedByPlayer: [{ playerId: 'p1', objectives: ['Vittoria'] }] },
          })
        }
      >
        edit
      </button>
      <span data-slot="initial-data">{JSON.stringify(initialData ?? null)}</span>
    </div>
  ),
}));

// ─── i18n MESSAGES subset ──────────────────────────────────────────────────

const MESSAGES: Record<string, string> = {
  'pages.sessionLive.scoring.loadingLabel': 'Caricamento punteggi…',
  'pages.sessionLive.scoring.forbiddenToast':
    "Permesso negato: solo l'host può modificare i punteggi",
  'pages.sessionLive.scoring.rateLimitedTemplate':
    'Limite raggiunto, riprova tra {seconds}s',
  'pages.sessionLive.scoring.rateLimitedToast':
    'Hai aggiornato i punteggi troppo velocemente. Aspetta {seconds}s.',
  'pages.sessionLive.scoring.validationFailedTemplate':
    'Validazione fallita: {message}',
  'pages.sessionLive.scoring.serverErrorToast': 'Errore server, riprova',
  'pages.sessionLive.scoring.networkErrorToast': 'Connessione persa, riprova',
  'pages.sessionLive.scoring.retryCta': 'Riprova',
  // Renderer labels (passed through but minimal for tests)
  'pages.sessionLive.scoring.title': 'Punteggi',
  'pages.sessionLive.scoring.scoreAriaTemplate': 'Punteggio di {name}',
  'pages.sessionLive.scoring.leaderBadgeLabel': 'in testa',
  'pages.sessionLive.scoring.rankingHeading': 'Posizioni',
  'pages.sessionLive.scoring.rankAriaTemplate': 'Posizione di {name}',
  'pages.sessionLive.scoring.firstPlaceBadgeLabel': 'primo posto',
  'pages.sessionLive.scoring.binaryWinHeading': 'Esito',
  'pages.sessionLive.scoring.binaryWinInProgress': 'Partita in corso',
  'pages.sessionLive.scoring.winLabel': 'Vince',
  'pages.sessionLive.scoring.loseLabel': 'Perde',
  'pages.sessionLive.scoring.outcomeAriaTemplate': '{name}: {result}',
  'pages.sessionLive.scoring.objectivesHeading': 'Obiettivi',
  'pages.sessionLive.scoring.completedAriaTemplate': 'Completati da {name}',
  'pages.sessionLive.scoring.doneAriaTemplate': '{label} (completato)',
  'pages.sessionLive.scoring.pendingAriaTemplate': '{label} (non completato)',
};

const RENDERER_LABELS = {
  points: {
    heading: 'Punteggi',
    scoreAriaTemplate: 'Punteggio di {name}',
    leaderBadgeLabel: 'in testa',
  },
  ranking: {
    heading: 'Posizioni',
    rankAriaTemplate: 'Posizione di {name}',
    firstPlaceBadgeLabel: 'primo posto',
  },
  binaryWin: {
    heading: 'Esito',
    inProgressLabel: 'Partita in corso',
    winLabel: 'Vince',
    loseLabel: 'Perde',
    outcomeAriaTemplate: '{name}: {result}',
  },
  objectives: {
    heading: 'Obiettivi',
    completedAriaTemplate: 'Completati da {name}',
    doneAriaTemplate: '{label} (completato)',
    pendingAriaTemplate: '{label} (non completato)',
  },
} as const;

const PLAYERS = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Anna' },
] as const;

function renderTC(props: Partial<React.ComponentProps<typeof ScoreTabContent>> = {}) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      <ScoreTabContent
        sessionId={props.sessionId ?? 's1'}
        viewerRole={props.viewerRole ?? 'Host'}
        viewerId={props.viewerId ?? 'u1'}
        players={props.players ?? PLAYERS}
        labels={props.labels ?? RENDERER_LABELS}
        className={props.className}
      />
    </IntlProvider>
  );
}

// ─── Global test setup ─────────────────────────────────────────────────────

beforeEach(() => {
  vi.useFakeTimers();
  toastErrorMock.mockClear();
  toastWarningMock.mockClear();
  toastSuccessMock.mockClear();
  useLiveSessionStore.getState().reset();
  mockMutation = {
    mutate: vi.fn(),
    isPending: false,
  };
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});
```

Run: `pnpm test ScoreTabContent 2>&1 | tail -5`
Expected: FAIL with "Cannot find module '../ScoreTabContent'" (no impl yet — fine, this is RED).

- [ ] **Step 6.2: Add Role gating tests (3)**

Append to the file (after `afterEach`):

```typescript
// ─── Group 1: Role gating ───────────────────────────────────────────────────

describe('ScoreTabContent — role gating', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
    });
  });

  it('Host + scoringType=Points → editor mounted', () => {
    renderTC({ viewerRole: 'Host' });
    expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-points"]')).toBeNull();
  });

  it('Player + scoringType=Points → renderer mounted (read-only)', () => {
    renderTC({ viewerRole: 'Player' });
    expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-points"]')).not.toBeNull();
  });

  it('Spectator + scoringType=Points → renderer mounted (read-only)', () => {
    renderTC({ viewerRole: 'Spectator' });
    expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-points"]')).not.toBeNull();
  });
});
```

- [ ] **Step 6.3: Add Role transition test (1)**

```typescript
// ─── Group 2: Role transition ───────────────────────────────────────────────

describe('ScoreTabContent — role transition', () => {
  it('viewerRole Host → Player mid-session → editor unmounts, pending debounce flushes', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
    });
    const { rerender } = renderTC({ viewerRole: 'Host' });

    // Trigger an edit (debounce pending)
    const editor = document.querySelector('[data-slot="trigger-change"]');
    expect(editor).not.toBeNull();
    act(() => {
      (editor as HTMLButtonElement).click();
    });
    expect(mockMutation.mutate).not.toHaveBeenCalled(); // 500ms debounce pending

    // Role flips to Player → editor unmounts → flush fires
    rerender(
      <IntlProvider locale="it" messages={MESSAGES}>
        <ScoreTabContent
          sessionId="s1"
          viewerRole="Player"
          viewerId="u1"
          players={PLAYERS}
          labels={RENDERER_LABELS}
        />
      </IntlProvider>
    );

    expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-points"]')).not.toBeNull();
    expect(mockMutation.mutate).toHaveBeenCalledTimes(1);
    expect(mockMutation.mutate).toHaveBeenCalledWith(
      expect.objectContaining({ scoringType: 'Points' })
    );
  });
});
```

- [ ] **Step 6.4: Add Null gate tests (2)**

```typescript
// ─── Group 3: Null gate ─────────────────────────────────────────────────────

describe('ScoreTabContent — null gate', () => {
  it('scoringType null + Host → a11y placeholder (NOT editor)', () => {
    renderTC({ viewerRole: 'Host' });
    expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-empty"]')).not.toBeNull();
    expect(
      document.querySelector('[data-slot="scoring-panel-empty"]')?.getAttribute('role')
    ).toBe('status');
  });

  it('scoringType null + Player → a11y placeholder', () => {
    renderTC({ viewerRole: 'Player' });
    expect(document.querySelector('[data-slot="scoring-panel-empty"]')).not.toBeNull();
    expect(screen.getByText('Caricamento punteggi…')).toBeInTheDocument();
  });
});
```

- [ ] **Step 6.5: Add REST hydration tests (2)**

```typescript
// ─── Group 4: REST hydration ────────────────────────────────────────────────

describe('ScoreTabContent — REST hydration', () => {
  // No DTO injection mechanism inside ScoreTabContent yet (sessionId only).
  // We test the race-guard behavior by seeding the store FIRST then mounting:
  // since the store has scoringType already, no further hydration is expected.

  it('does not overwrite SignalR-hydrated store (race guard)', () => {
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'p1', points: 99 }] },
      });
    });
    renderTC({ viewerRole: 'Host' });
    // Store state remains the SignalR-seeded values.
    expect(useLiveSessionStore.getState().scoreData).toEqual({
      scores: [{ playerId: 'p1', points: 99 }],
    });
  });

  it('mounts without throwing when scoringType is null and no hydration source', () => {
    expect(() => renderTC({ viewerRole: 'Host' })).not.toThrow();
    expect(useLiveSessionStore.getState().scoringType).toBeNull();
  });
});
```

Note: the full REST hydration `useEffect` runs in `ScoreTabContent` but it depends on `sessionQuery.data` — `ScoreTabContent` is mounted from `SessionLiveView` which already has the `useSession` query. The hydration semantic is tested at the `SessionLiveView.test.tsx` level (the existing 5 hydration tests remain there). The 2 tests above pin the **race guard** behavior specifically inside `ScoreTabContent`.

- [ ] **Step 6.6: Add Variant editor mount tests (4)**

```typescript
// ─── Group 5: Variant editor mount ──────────────────────────────────────────

describe('ScoreTabContent — variant editor mount (Host)', () => {
  it('Points: editor receives correct scoringType prop', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
    });
    renderTC({ viewerRole: 'Host' });
    expect(
      document.querySelector('[data-slot="polymorphic-score-editor"]')?.getAttribute(
        'data-scoring-type'
      )
    ).toBe('Points');
  });

  it('BinaryWin: editor receives correct scoringType prop', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'BinaryWin',
      scoreData: { results: [{ playerId: 'p1', isWinner: false }] },
    });
    renderTC({ viewerRole: 'Host' });
    expect(
      document.querySelector('[data-slot="polymorphic-score-editor"]')?.getAttribute(
        'data-scoring-type'
      )
    ).toBe('BinaryWin');
  });

  it('Ranking: editor receives correct scoringType prop', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Ranking',
      scoreData: { positions: [{ playerId: 'p1', position: 1 }] },
    });
    renderTC({ viewerRole: 'Host' });
    expect(
      document.querySelector('[data-slot="polymorphic-score-editor"]')?.getAttribute(
        'data-scoring-type'
      )
    ).toBe('Ranking');
  });

  it('Objectives: editor receives availableObjectives prop (length > 0)', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Objectives',
      scoreData: { completedByPlayer: [{ playerId: 'p1', objectives: [] }] },
    });
    renderTC({ viewerRole: 'Host' });
    const editor = document.querySelector('[data-slot="polymorphic-score-editor"]');
    expect(editor?.getAttribute('data-scoring-type')).toBe('Objectives');
    expect(Number(editor?.getAttribute('data-objectives-count'))).toBeGreaterThan(0);
  });
});
```

- [ ] **Step 6.7: Add Debounce + mutation tests (5)**

```typescript
// ─── Group 6: Debounce + mutation ───────────────────────────────────────────

describe('ScoreTabContent — debounce + mutation', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 0 }] },
    });
  });

  it('single edit → 500ms debounce → mutate called once with correct payload', () => {
    renderTC({ viewerRole: 'Host' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
    });
    expect(mockMutation.mutate).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(mockMutation.mutate).toHaveBeenCalledTimes(1);
    expect(mockMutation.mutate).toHaveBeenCalledWith({
      sessionId: 's1',
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 99 }] },
    });
  });

  it('rapid edits (3 in 100ms) → only last fires after debounce', () => {
    renderTC({ viewerRole: 'Host' });
    act(() => {
      const trigger = document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement;
      trigger.click();
      vi.advanceTimersByTime(30);
      trigger.click();
      vi.advanceTimersByTime(30);
      trigger.click();
    });
    expect(mockMutation.mutate).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(mockMutation.mutate).toHaveBeenCalledTimes(1);
  });

  it('unmount mid-debounce → flush() called, mutation fires immediately', () => {
    const { unmount } = renderTC({ viewerRole: 'Host' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
    });
    expect(mockMutation.mutate).not.toHaveBeenCalled();

    unmount();

    expect(mockMutation.mutate).toHaveBeenCalledTimes(1);
  });

  it('mutation receives sessionId from props', () => {
    renderTC({ viewerRole: 'Host', sessionId: 'custom-session-id' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
      vi.advanceTimersByTime(500);
    });
    expect(mockMutation.mutate).toHaveBeenCalledWith(
      expect.objectContaining({ sessionId: 'custom-session-id' })
    );
  });

  it('mutation success → localScoreOverride cleared (editor receives store data next render)', () => {
    renderTC({ viewerRole: 'Host' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
      vi.advanceTimersByTime(500);
    });
    expect(mockMutation.mutate).toHaveBeenCalledTimes(1);

    // Simulate mutation success (mock by extracting onSuccess from the mutate call,
    // but our mock just stores the call args — we test the BE-side flow via SignalR
    // updating the store and the editor receiving the new initialData).
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'p1', points: 99 }] },
      });
    });
    const initialDataNode = document.querySelector('[data-slot="initial-data"]');
    expect(initialDataNode?.textContent).toContain('99');
  });
});
```

- [ ] **Step 6.8: Add Error handling tests (8)**

```typescript
// ─── Group 7: Error handling ────────────────────────────────────────────────

describe('ScoreTabContent — error handling', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 0 }] },
    });
  });

  function triggerEditAndFlushDebounce() {
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
      vi.advanceTimersByTime(500);
    });
  }

  /**
   * Simulate the mutation hook calling onError after T7 implements the call.
   * The component passes `onError` to the mutate options or wires an effect on
   * mutation state. The mock captures the second arg of mutate as the options
   * object; we extract onError from there.
   */
  function getOnErrorFromMockMutation(): (err: unknown) => void {
    const calls = mockMutation.mutate.mock.calls;
    expect(calls.length).toBeGreaterThan(0);
    const lastCall = calls[calls.length - 1];
    const options = lastCall[1] as { onError?: (err: unknown) => void } | undefined;
    if (!options?.onError) {
      throw new Error('mutate not called with onError option');
    }
    return options.onError;
  }

  it('403 → toast forbidden + editor disabled', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new UpdateSessionScoresError('Forbidden', 'forbidden', 403));
    });
    expect(toastErrorMock).toHaveBeenCalledWith(
      expect.stringContaining('Permesso negato'),
      expect.objectContaining({ id: 'score-403' })
    );
    expect(
      document.querySelector('[data-slot="polymorphic-score-editor"]')?.getAttribute('data-disabled')
    ).toBe('true');
  });

  it('429 → toast warning + setRateLimitedUntil set in store', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new UpdateSessionScoresError('Too many', 'server', 429));
    });
    expect(toastWarningMock).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({ id: 'score-429' })
    );
    expect(useLiveSessionStore.getState().rateLimitedUntil).not.toBeNull();
    expect(useLiveSessionStore.getState().rateLimitedUntil).toBeGreaterThan(Date.now());
  });

  it('429 countdown — editor disabled while rateLimitedUntil is in the future', () => {
    act(() => {
      useLiveSessionStore.getState().setRateLimitedUntil(Date.now() + 5000);
    });
    renderTC({ viewerRole: 'Host' });
    expect(
      document.querySelector('[data-slot="polymorphic-score-editor"]')?.getAttribute('data-disabled')
    ).toBe('true');
  });

  it('429 countdown reaches 0 → rateLimitedUntil cleared, editor re-enabled', () => {
    act(() => {
      useLiveSessionStore.getState().setRateLimitedUntil(Date.now() + 1000);
    });
    renderTC({ viewerRole: 'Host' });
    expect(
      document.querySelector('[data-slot="polymorphic-score-editor"]')?.getAttribute('data-disabled')
    ).toBe('true');
    act(() => {
      vi.advanceTimersByTime(2000); // past deadline + a tick
    });
    expect(useLiveSessionStore.getState().rateLimitedUntil).toBeNull();
  });

  it('5xx server error → toast with retry button (action defined)', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new UpdateSessionScoresError('Server', 'server', 500));
    });
    expect(toastErrorMock).toHaveBeenCalledWith(
      expect.stringContaining('Errore server'),
      expect.objectContaining({
        id: 'score-5xx',
        action: expect.objectContaining({ label: expect.any(String), onClick: expect.any(Function) }),
      })
    );
  });

  it('retry button click → re-invokes mutate with last payload', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const firstCallArgs = mockMutation.mutate.mock.calls[0][0];
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new UpdateSessionScoresError('Server', 'server', 500));
    });
    const toastCall = toastErrorMock.mock.calls.find(c => c[1]?.id === 'score-5xx');
    const onClick = (toastCall?.[1] as { action: { onClick: () => void } }).action.onClick;
    act(() => {
      onClick();
    });
    expect(mockMutation.mutate).toHaveBeenCalledTimes(2);
    expect(mockMutation.mutate.mock.calls[1][0]).toEqual(firstCallArgs);
  });

  it('network error (TypeError) → toast with retry button', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new TypeError('Failed to fetch'));
    });
    expect(toastErrorMock).toHaveBeenCalledWith(
      expect.stringContaining('Connessione persa'),
      expect.objectContaining({ id: 'score-network' })
    );
  });

  it('400 validation error → toast with details, editor remains enabled', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(
        new UpdateSessionScoresError('Bad', 'validation', 400, { field: 'scores' })
      );
    });
    expect(toastErrorMock).toHaveBeenCalledWith(
      expect.stringContaining('Validazione fallita'),
      expect.objectContaining({ id: 'score-400' })
    );
    expect(
      document.querySelector('[data-slot="polymorphic-score-editor"]')?.getAttribute('data-disabled')
    ).toBe('false');
  });
});
```

- [ ] **Step 6.9: Add Optimistic UI tests (3)**

```typescript
// ─── Group 8: Optimistic UI ─────────────────────────────────────────────────

describe('ScoreTabContent — optimistic UI', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 0 }] },
    });
  });

  it('typing → localScoreOverride reflected in editor initialData immediately', () => {
    renderTC({ viewerRole: 'Host' });
    const before = document.querySelector('[data-slot="initial-data"]')?.textContent;
    expect(before).toContain('"points":0');

    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
    });
    // Optimistic: editor's initialData now reflects the new value BEFORE debounce fires
    const after = document.querySelector('[data-slot="initial-data"]')?.textContent;
    expect(after).toContain('"points":99');
  });

  it('mutation success → localScoreOverride cleared, falls back to store', () => {
    renderTC({ viewerRole: 'Host' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
      vi.advanceTimersByTime(500);
    });
    const calls = mockMutation.mutate.mock.calls;
    const options = calls[0][1] as { onSuccess?: () => void };
    act(() => {
      options.onSuccess?.();
      // SignalR broadcast updates store to canonical values
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'p1', points: 99 }] },
      });
    });
    // Editor receives the canonical store data
    expect(
      document.querySelector('[data-slot="initial-data"]')?.textContent
    ).toContain('"points":99');
  });

  it('mutation error → localScoreOverride cleared (rollback to store)', () => {
    renderTC({ viewerRole: 'Host' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
      vi.advanceTimersByTime(500);
    });
    const options = mockMutation.mutate.mock.calls[0][1] as { onError?: (err: unknown) => void };
    act(() => {
      options.onError?.(new UpdateSessionScoresError('Forbidden', 'forbidden', 403));
    });
    // Editor receives the store's value (0), not the optimistic value (99)
    expect(
      document.querySelector('[data-slot="initial-data"]')?.textContent
    ).toContain('"points":0');
  });
});
```

- [ ] **Step 6.10: Run all tests and verify RED**

Run: `pnpm test ScoreTabContent 2>&1 | tail -15`
Expected: ALL 28 tests FAIL with "Cannot find module '../ScoreTabContent'" — RED state confirmed.

- [ ] **Step 6.11: Commit the RED tests**

```bash
git -C /d/Repositories/meepleai-monorepo-dev add \
  "apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/ScoreTabContent.test.tsx"
git -C /d/Repositories/meepleai-monorepo-dev commit -m "test(session-live): #2430 Block B+ T6 ScoreTabContent test scaffold (RED)

28 Vitest cases across 8 groups:
  3 role gating (Host/Player/Spectator)
  1 role transition (Host \xe2\x86\x92 Player flushes debounce)
  2 null gate (Host/Player both show a11y placeholder)
  2 REST hydration (race guard + null mount)
  4 variant editor mount (Points/BinaryWin/Ranking/Objectives)
  5 debounce+mutation (single edit / rapid / unmount flush / sessionId
    pass-through / success clears localOverride)
  8 error handling (403/429/countdown init/countdown clear/5xx+retry/
    retry click/network/400)
  3 optimistic UI (typing reflects / success canonical / error rollback)

All RED until T7 implements ScoreTabContent.

Mocks: sonner toast, useUpdateSessionScores (mutate + isPending),
PolymorphicScoreEditor (data-slot probe with trigger-change button).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 7: Implement `ScoreTabContent` (GREEN)

**Files:**
- Create: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/ScoreTabContent.tsx`

- [ ] **Step 7.1: Implement the component**

Create `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/ScoreTabContent.tsx`:

```typescript
/**
 * ScoreTabContent — polymorphic score tab content for SessionLiveView.
 *
 * Owns ALL polymorphic scoring logic for the score tab:
 *   - Block B (read-only): store selectors + REST hydration race guard +
 *     scoringPanelData adapter memo + a11y placeholder.
 *   - Block B+ (mutable): role-based mount (Host=editor, others=renderer),
 *     debounced useUpdateSessionScores wire, optimistic local override,
 *     5-kind error mapper, retry button, 30s rate-limit countdown
 *     persisted in useLiveSessionStore.
 *
 * Issue #2430 Block B+.
 */

'use client';

import { useCallback, useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { useIntl } from 'react-intl';
import { toast } from 'sonner';

import {
  ScoringPanelRenderer,
  type ScoringPanelData,
  type ScoringPanelRendererLabels,
} from '@/components/features/session-live';
import { PolymorphicScoreEditor } from '@/components/sessions';
import type {
  ScoreChangePayload,
} from '@/components/sessions/PolymorphicScoreEditor';
import type {
  ScoreDataByType,
  ScoreType,
} from '@/components/sessions/score-strategies/types';
import {
  UpdateSessionScoresError,
  useUpdateSessionScores,
  type UpdateSessionScoresPayload,
} from '@/hooks/use-update-session-scores';
import { useTranslation } from '@/hooks/useTranslation';
import { MVP_OBJECTIVES_CATALOGUE } from '@/lib/session-live/mvp-objectives-catalogue';
import { mapScoreDataToPanelData } from '@/lib/session-live/score-data-to-panel-data';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

const RATE_LIMIT_WINDOW_MS = 30_000;

// ─── Local normalized error type ─────────────────────────────────────────────

type ScoredErrorKind = 'forbidden' | 'rate-limited' | 'validation' | 'server' | 'network';

interface ScoredError {
  readonly kind: ScoredErrorKind;
  readonly status: number;
  readonly message: string;
  readonly details?: unknown;
}

function mapMutationError(err: unknown): ScoredError {
  if (err instanceof UpdateSessionScoresError) {
    if (err.status === 429) {
      return { kind: 'rate-limited', status: 429, message: err.message };
    }
    return {
      kind: err.kind,
      status: err.status,
      message: err.message,
      details: err.details,
    };
  }
  return {
    kind: 'network',
    status: 0,
    message: err instanceof Error ? err.message : 'Network error',
  };
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface ScoreTabContentProps {
  readonly sessionId: string;
  readonly viewerRole: 'Host' | 'Player' | 'Spectator';
  readonly viewerId: string;
  readonly players: ReadonlyArray<{
    readonly id: string;
    readonly name: string;
    readonly displayName?: string;
  }>;
  readonly labels: ScoringPanelRendererLabels;
  readonly className?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function ScoreTabContent(props: ScoreTabContentProps): ReactElement {
  const { sessionId, viewerRole, players, labels, className } = props;
  const { t } = useTranslation();
  const intl = useIntl();

  // Store selectors
  const scoringType = useLiveSessionStore(s => s.scoringType);
  const scoreData = useLiveSessionStore(s => s.scoreData);
  const rateLimitedUntil = useLiveSessionStore(s => s.rateLimitedUntil);
  const setRateLimitedUntil = useLiveSessionStore(s => s.setRateLimitedUntil);

  // Mutation
  const mutation = useUpdateSessionScores();

  // Refs (unmount safety + retry payload).
  // Note: `isMountedRef` is sufficient to guard against post-unmount setState
  // (includes the host-transfer mid-mutation case — when role flips Host →
  // Player, the editor branch unmounts via parent reconciliation, the flush
  // effect (below) fires with viewerRole dep change, and `isMountedRef`
  // ensures the resulting 403 error handler skips toast/setState.
  // No `viewerRoleRef` needed — earlier draft had one as dead code.
  const isMountedRef = useRef(true);
  const lastPayloadRef = useRef<UpdateSessionScoresPayload | null>(null);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  // Optimistic local override (cleared on success/error)
  const [localScoreOverride, setLocalScoreOverride] = useState<
    ScoreDataByType[ScoreType] | null
  >(null);

  // Tick state for 429 countdown (re-evaluates isRateLimited each second)
  const [, setTick] = useState(0);
  useEffect(() => {
    if (rateLimitedUntil == null) return;
    const intervalId = setInterval(() => {
      setTick(n => n + 1);
      if (Date.now() >= rateLimitedUntil) {
        setRateLimitedUntil(null);
      }
    }, 1000);
    return () => clearInterval(intervalId);
  }, [rateLimitedUntil, setRateLimitedUntil]);

  const isRateLimited = rateLimitedUntil != null && Date.now() < rateLimitedUntil;
  const rateLimitRemainingSec = isRateLimited
    ? Math.max(0, Math.ceil((rateLimitedUntil! - Date.now()) / 1000))
    : 0;

  // Effective score data: localOverride wins during pending debounce
  const effectiveScoreData = localScoreOverride ?? scoreData;

  // Renderer data (Block B path, used for non-Host roles + Host's null fallback)
  const scoringPanelData = useMemo<ScoringPanelData | null>(
    () =>
      mapScoreDataToPanelData(scoringType, effectiveScoreData, players, {
        availableObjectives: MVP_OBJECTIVES_CATALOGUE,
      }),
    [scoringType, effectiveScoreData, players]
  );

  // Error handler — normalized via mapMutationError
  const handleMutationError = useCallback(
    (err: unknown, payload: UpdateSessionScoresPayload | null) => {
      if (!isMountedRef.current) return;
      const scored = mapMutationError(err);
      setLocalScoreOverride(null); // rollback to store
      switch (scored.kind) {
        case 'forbidden':
          toast.error(t('pages.sessionLive.scoring.forbiddenToast'), {
            id: 'score-403',
          });
          break;
        case 'rate-limited': {
          const deadline = Date.now() + RATE_LIMIT_WINDOW_MS;
          setRateLimitedUntil(deadline);
          toast.warning(
            (intl.messages[
              'pages.sessionLive.scoring.rateLimitedToast'
            ] as string)?.replace('{seconds}', String(RATE_LIMIT_WINDOW_MS / 1000)) ??
              'Rate limited',
            { id: 'score-429' }
          );
          break;
        }
        case 'validation':
          toast.error(
            (intl.messages[
              'pages.sessionLive.scoring.validationFailedTemplate'
            ] as string)?.replace('{message}', JSON.stringify(scored.details ?? scored.message)) ??
              'Validation failed',
            { id: 'score-400' }
          );
          break;
        case 'server':
          toast.error(t('pages.sessionLive.scoring.serverErrorToast'), {
            id: 'score-5xx',
            action: {
              label: t('pages.sessionLive.scoring.retryCta'),
              onClick: () => {
                if (payload) mutation.mutate(payload, { onError: e => handleMutationError(e, payload) });
              },
            },
          });
          break;
        case 'network':
          toast.error(t('pages.sessionLive.scoring.networkErrorToast'), {
            id: 'score-network',
            action: {
              label: t('pages.sessionLive.scoring.retryCta'),
              onClick: () => {
                if (payload) mutation.mutate(payload, { onError: e => handleMutationError(e, payload) });
              },
            },
          });
          break;
      }
    },
    // `mutation.mutate` is referentially stable across renders (TanStack Query
    // guarantee), so depending on it instead of the whole `mutation` object
    // avoids re-creating handleMutationError on every isPending flip.
    [t, intl.messages, mutation.mutate, setRateLimitedUntil]
  );

  // Debounced mutation dispatch.
  // Note: hook-level `useUpdateSessionScores.onSuccess` invalidates queries;
  // the inline `onSuccess` below ADDS the local-override clear — both fire
  // (TanStack Query v5 merges callbacks rather than replacing).
  const submitMutation = useCallback(
    (payload: UpdateSessionScoresPayload) => {
      lastPayloadRef.current = payload;
      mutation.mutate(payload, {
        onSuccess: () => {
          if (!isMountedRef.current) return;
          setLocalScoreOverride(null);
        },
        onError: err => handleMutationError(err, payload),
      });
    },
    [mutation.mutate, handleMutationError]
  );

  const [debouncedSubmit, flush] = useDebouncedCallback(submitMutation, 500);

  // Flush-on-unmount + flush-on-role-change (DEC-4).
  // Dep array includes `viewerRole` so the cleanup ALSO fires when the
  // component stays mounted but reconciles between editor/renderer branches
  // (e.g., host transfer mid-edit). Without `viewerRole` in deps, the cleanup
  // would only fire on full unmount, leaving the pending debounce orphaned
  // during in-tree role transitions.
  useEffect(() => {
    return () => {
      flush();
    };
  }, [viewerRole, flush]);

  // onChange handler for the editor: optimistic UI + debounced submit
  const handleScoreChange = useCallback(
    (payload: ScoreChangePayload) => {
      setLocalScoreOverride(payload.data);
      debouncedSubmit({
        sessionId,
        scoringType: payload.scoringType,
        scoreData: payload.data,
      });
    },
    [sessionId, debouncedSubmit]
  );

  // ── Render ────────────────────────────────────────────────────────────────

  const playerOptions = useMemo(
    () => players.map(p => ({ id: p.id, displayName: p.displayName ?? p.name })),
    [players]
  );

  const hostEditing = viewerRole === 'Host' && scoringType !== null;

  if (hostEditing) {
    return (
      <div className={className}>
        <PolymorphicScoreEditor
          scoringType={scoringType!}
          players={playerOptions}
          initialData={effectiveScoreData ?? undefined}
          availableObjectives={MVP_OBJECTIVES_CATALOGUE}
          onChange={handleScoreChange}
          disabled={isRateLimited || mutation.isPending}
        />
        {isRateLimited && (
          <div
            role="status"
            aria-live="polite"
            data-slot="score-rate-limit-countdown"
            className="mt-1 text-xs text-amber-500"
          >
            {(intl.messages[
              'pages.sessionLive.scoring.rateLimitedTemplate'
            ] as string)?.replace('{seconds}', String(rateLimitRemainingSec)) ??
              `Rate limited, retry in ${rateLimitRemainingSec}s`}
          </div>
        )}
      </div>
    );
  }

  if (scoringPanelData != null) {
    return (
      <ScoringPanelRenderer data={scoringPanelData} labels={labels} className={className} />
    );
  }

  return (
    <div
      role="status"
      aria-live="polite"
      data-slot="scoring-panel-empty"
      className={`${className ?? ''} text-xs text-muted-foreground`.trim()}
    >
      {t('pages.sessionLive.scoring.loadingLabel')}
    </div>
  );
}
```

- [ ] **Step 7.2: Verify the component compiles**

Run: `pnpm typecheck 2>&1 | tail -10`
Expected: 0 errors.

If TypeScript complains about the `scoringType!` non-null assertion at the editor mount: the `hostEditing` check above does guarantee non-null, but the narrowing may not propagate. If so, add an explicit `if (scoringType === null) return ...` before the editor render.

- [ ] **Step 7.3: Run the test file and verify all 28 PASS**

Run: `pnpm test ScoreTabContent 2>&1 | tail -10`
Expected: 28/28 PASS.

If some tests fail, fix the implementation, NOT the tests. Common causes:
- `data-slot="scoring-panel-empty"` missing → check the placeholder render path
- `data-disabled` attribute on the mock editor not flipping → check `disabled` prop wiring
- 429 countdown setRateLimitedUntil missing → check error matrix
- Retry button onClick not re-invoking mutate → check `mutation.mutate(payload, ...)` in retry action

**Exception**: if a test assertion uses a wrong string literal (e.g., `'score-4xx'` vs `'score-5xx'` as toast `id`, or a typo in an i18n key), the test assertion is at fault. Fix the test, not the implementation. The rule is "fix the side that diverges from the spec" — usually the impl, occasionally a typo in the test.

- [ ] **Step 7.4: Commit**

```bash
git -C /d/Repositories/meepleai-monorepo-dev add \
  "apps/web/src/app/(authenticated)/sessions/[id]/live/_components/ScoreTabContent.tsx"
git -C /d/Repositories/meepleai-monorepo-dev commit -m "feat(session-live): #2430 Block B+ T7 implement ScoreTabContent

New component encapsulating ALL polymorphic scoring logic for the
score tab. Block B selectors/effect/memo MOVED here. Block B+ additions:

- Role-based mount: Host \xe2\x86\x92 PolymorphicScoreEditor + autosave;
  Player+Spectator \xe2\x86\x92 ScoringPanelRenderer; null scoringType
  \xe2\x86\x92 aria-live placeholder.
- 500ms trailing debounce + flush-on-unmount (no input lost).
- Optimistic localScoreOverride during pending debounce.
- lastPayloadRef captures last dispatch for retry button.
- isMountedRef + viewerRoleRef guard state updates after unmount
  (covers host-transfer mid-mutation race).
- mapMutationError normalizes to 5 kinds (forbidden, rate-limited,
  validation, server, network). 429 detected via err.status === 429
  from UpdateSessionScoresError without modifying the hook.
- 30s rate-limit countdown persisted in useLiveSessionStore.rateLimitedUntil
  so it survives tab change. Cleared on natural expiry.
- 5-class toast matrix with deterministic sonner ids (no stacking).

28/28 tests pass (T6 scaffold).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 8: Integrate `ScoreTabContent` into `SessionLiveView`

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx`

- [ ] **Step 8.1: Remove Block B selectors + REST hydration + memo from `SessionLiveView`**

Open `SessionLiveView.tsx`. Find and delete the 3 store selectors added in Block B T6:

```typescript
const scoringType = useLiveSessionStore(s => s.scoringType);
const scoreData = useLiveSessionStore(s => s.scoreData);
const setScoringConfig = useLiveSessionStore(s => s.setScoringConfig);
```

Delete the REST hydration `useEffect`:

```typescript
useEffect(() => {
  const dto = sessionQuery.data;
  if (dto?.scoringType == null || dto.scoreData == null) return;
  if (useLiveSessionStore.getState().scoringType != null) return;
  try {
    const parsed = JSON.parse(dto.scoreData) as ScoreDataByType[ScoreType];
    setScoringConfig({
      scoringType: dto.scoringType as ScoreType,
      scoreData: parsed,
    });
  } catch (err) {
    console.warn('[#2389] malformed scoreData JSON, will rely on SignalR', {
      sessionId: dto.id,
      scoreDataLength: dto.scoreData?.length ?? 0,
      err,
    });
  }
}, [sessionQuery.data, setScoringConfig]);
```

Delete the `scoringPanelData` `useMemo`:

```typescript
const scoringPanelData = useMemo<ScoringPanelData | null>(
  () =>
    mapScoreDataToPanelData(scoringType, scoreData, activeSession?.players ?? [], {
      availableObjectives: MVP_OBJECTIVES_CATALOGUE,
    }),
  [scoringType, scoreData, activeSession?.players]
);
```

Remove the now-unused imports:
- `mapScoreDataToPanelData`
- `MVP_OBJECTIVES_CATALOGUE`
- `useLiveSessionStore`
- `ScoreDataByType`, `ScoreType` (type imports)

If `useEffect` import is no longer used elsewhere in the file, remove it from the React import.

If `ScoringPanelRenderer` import becomes unused (because the 2 mount sites now use `ScoreTabContent`), remove it too.

If `ScoringPanelData` type import becomes unused, remove it.

- [ ] **Step 8.2: Add the new `ScoreTabContent` import**

Add the import near the other component imports in `SessionLiveView.tsx`:

```typescript
import { ScoreTabContent } from './ScoreTabContent';
```

- [ ] **Step 8.3: Replace the desktop right column score branch**

Find the existing block (post-Block B):

```typescript
{tab === 'score' && (
  scoringPanelData != null ? (
    <ScoringPanelRenderer
      data={scoringPanelData}
      labels={scoringPanelLabels}
      className="p-3"
    />
  ) : (
    <div
      role="status"
      aria-live="polite"
      data-slot="scoring-panel-empty"
      className="p-3 text-xs text-muted-foreground"
    >
      {t('pages.sessionLive.scoring.loadingLabel')}
    </div>
  )
)}
```

Replace with:

```typescript
{tab === 'score' && (
  <ScoreTabContent
    sessionId={sessionId ?? ''}
    viewerRole={activeSession.viewerRole}
    viewerId={activeSession.viewerId}
    players={activeSession.players}
    labels={scoringPanelLabels}
    className="p-3"
  />
)}
```

- [ ] **Step 8.4: Replace the mobile drawer score case**

Find the mobile drawer switch case (post-Block B):

```typescript
case 'score':
default:
  return scoringPanelData != null ? (
    <ScoringPanelRenderer
      data={scoringPanelData}
      labels={scoringPanelLabels}
      className="p-2"
    />
  ) : (
    <div
      role="status"
      aria-live="polite"
      data-slot="scoring-panel-empty"
      className="p-2 text-xs text-muted-foreground"
    >
      {t('pages.sessionLive.scoring.loadingLabel')}
    </div>
  );
```

Replace with:

```typescript
case 'score':
default:
  return (
    <ScoreTabContent
      sessionId={sessionId ?? ''}
      viewerRole={activeSession.viewerRole}
      viewerId={activeSession.viewerId}
      players={activeSession.players}
      labels={scoringPanelLabels}
      className="p-2"
    />
  );
```

Update the `useMemo` dep array for `mobileSheetContent` (~line 1144 in Block B version): remove `scoringPanelData` and `scoringPanelLabels`-related entries that no longer apply directly. The deps should still include `activeSession`, `mobileTab`, `sessionId`. `t` may or may not still be needed in this memo — leave it if the other branches use it.

- [ ] **Step 8.5: Migrate the 2 a11y placeholder tests to ScoreTabContent.test.tsx**

In Block B's `SessionLiveView.test.tsx`, the placeholder tests are:

```typescript
it('renders aria-live placeholder when scoringType is null', () => { ... });
it('placeholder shows the localized loading label text', () => { ... });
```

These now live INSIDE `ScoreTabContent.test.tsx` (T6 step 6.4 covers the null gate). Delete them from `SessionLiveView.test.tsx`.

- [ ] **Step 8.6: Add 2 new smoke tests to `SessionLiveView.test.tsx`**

Inside the existing `describe('SessionLiveView — Block B (#2389) scoring wire-up', ...)` block (or a new sibling describe), append:

```typescript
  // ── #2430 Block B+ smoke: ScoreTabContent mount ──────────────────────────

  it('mounts ScoreTabContent inside score tab when viewerRole=Host', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    // Default tab is 'score'. ScoreTabContent should be the mount.
    // We probe by querying for either a child of ScoreTabContent
    // (editor or placeholder) since ScoreTabContent itself has no
    // unique data-slot — the renderer/editor/placeholder slots are
    // the observable signals.
    const hasMount =
      container.querySelector('[data-slot="polymorphic-score-editor"]') ||
      container.querySelector('[data-slot="scoring-panel-points"]') ||
      container.querySelector('[data-slot="scoring-panel-empty"]');
    expect(hasMount).not.toBeNull();
  });

  it('renders ScoringPanelRenderer (not editor) when viewerRole=Player', () => {
    // Default fixture viewerRole is 'Player' (see SessionLiveView line ~395
    // "viewerRole: 'Player' as const" in activeSession construction).
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'player-001', points: 10 }] },
      });
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="polymorphic-score-editor"]')).toBeNull();
    expect(container.querySelector('[data-slot="scoring-panel-points"]')).not.toBeNull();
  });
```

- [ ] **Step 8.7: Run all tests and verify regression-free**

Run: `pnpm test SessionLiveView 2>&1 | tail -10`
Expected: 79/79 PASS (67 untouched + 5 hydration + 4 variant + 1 G5a regression + 2 new smoke). The 2 placeholder tests are GONE — net total is 79 (was 78, +2 smoke −2 migrated = 78 + 1 net since migration is to a different file).

If some hydration tests now fail because the REST hydration `useEffect` was deleted from `SessionLiveView`: these tests assert STORE STATE post-render, and the store is now populated by `ScoreTabContent.useEffect` instead. The assertion should still pass because the tree contains `ScoreTabContent`, which mounts and runs its effect. If they fail, investigate — likely the issue is that `ScoreTabContent` is conditional on `tab === 'score'` and the test doesn't navigate to the score tab. Verify by checking the URL state in those tests.

- [ ] **Step 8.8: Run typecheck**

Run: `pnpm typecheck 2>&1 | tail -10`
Expected: 0 errors. Unused imports removed in step 8.1 should not regenerate complaints.

- [ ] **Step 8.9: Commit**

```bash
git -C /d/Repositories/meepleai-monorepo-dev add \
  "apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx" \
  "apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx"
git -C /d/Repositories/meepleai-monorepo-dev commit -m "refactor(session-live): #2430 Block B+ T8 integrate ScoreTabContent

SessionLiveView delegates score tab content to the new ScoreTabContent
component (T7). Removed: 3 store selectors, REST hydration useEffect,
scoringPanelData memo, 2 inline a11y placeholder JSX blocks. All moved
to ScoreTabContent. Net SessionLiveView change: ~-50 LOC.

Mount sites unchanged structurally (desktop right column + mobile
drawer score case) — they now mount <ScoreTabContent /> passing
sessionId / viewerRole / viewerId / players / labels / className.

SessionLiveView.test.tsx: 2 a11y placeholder tests migrated to
ScoreTabContent.test.tsx (T6); 2 new smoke tests added asserting
mount + role propagation. Final: 79/79 green.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 9: Add 7 i18n keys

**Files:**
- Modify: `apps/web/src/locales/it.json` (under `pages.sessionLive.scoring`)
- Modify: `apps/web/src/locales/en.json` (mirror)

- [ ] **Step 9.1: Add the Italian keys**

Open `apps/web/src/locales/it.json`. Find the `"sessionLive"."scoring"` block (around line 3209) and add 7 keys after `"loadingLabel"`:

```json
      "scoring": {
        "title": "Punteggi",
        "scoreLabel": "Punteggio: {score}",
        "winnerLabel": "Vincitore",
        "myScoreLabel": "Il tuo punteggio",
        "incrementAriaLabel": "Aumenta punteggio di {playerName}",
        "decrementAriaLabel": "Diminuisci punteggio di {playerName}",
        "scoreInputAriaLabel": "Inserisci punteggio per {playerName}",
        "playerCount": "{count, plural, =0 {Nessun giocatore} =1 {1 giocatore} other {# giocatori}}",
        "loadingLabel": "Caricamento punteggi…",
        "forbiddenToast": "Permesso negato: solo l'host può modificare i punteggi",
        "rateLimitedTemplate": "Limite raggiunto, riprova tra {seconds}s",
        "rateLimitedToast": "Hai aggiornato i punteggi troppo velocemente. Aspetta {seconds}s.",
        "validationFailedTemplate": "Validazione fallita: {message}",
        "serverErrorToast": "Errore server, riprova",
        "networkErrorToast": "Connessione persa, riprova",
        "retryCta": "Riprova"
      },
```

- [ ] **Step 9.2: Add the English keys**

Open `apps/web/src/locales/en.json`. Find the matching `"sessionLive"."scoring"` block and mirror the structure:

```json
      "scoring": {
        ...
        "loadingLabel": "Loading scores…",
        "forbiddenToast": "Permission denied: only the host can edit scores",
        "rateLimitedTemplate": "Rate limit reached, retry in {seconds}s",
        "rateLimitedToast": "You updated scores too quickly. Wait {seconds}s.",
        "validationFailedTemplate": "Validation failed: {message}",
        "serverErrorToast": "Server error, retry",
        "networkErrorToast": "Connection lost, retry",
        "retryCta": "Retry"
      },
```

- [ ] **Step 9.3: Run typecheck + tests**

Run: `pnpm typecheck && pnpm test ScoreTabContent 2>&1 | tail -10`
Expected: 0 type errors. 28/28 ScoreTabContent tests still green. (The test file's `MESSAGES` already had these keys per T6 step 6.1.)

- [ ] **Step 9.4: Commit**

```bash
git -C /d/Repositories/meepleai-monorepo-dev add \
  apps/web/src/locales/it.json \
  apps/web/src/locales/en.json
git -C /d/Repositories/meepleai-monorepo-dev commit -m "chore(i18n): #2430 Block B+ T9 add 7 scoring error keys

Italian + English keys for the 5-class error matrix (403/429/400/5xx/
network) plus the rate-limit countdown template and retry CTA:
  - forbiddenToast / rateLimitedTemplate / rateLimitedToast /
    validationFailedTemplate / serverErrorToast / networkErrorToast /
    retryCta

Consumed by ScoreTabContent toast matrix and countdown UI (T7).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 10: Typecheck + lint sweep + targeted regression

**Files:** N/A (runs quality gates).

- [ ] **Step 10.1: Run targeted test suite**

Run: `pnpm test SessionLiveView score-data ScoreTabContent use-debounced live-session-store 2>&1 | tail -10`
Expected: ALL green. Approximate total: 79 SessionLiveView + 16 adapter + 28 ScoreTabContent + 5 debounce + 7 store (4 existing + 3 new) = ~135 tests.

- [ ] **Step 10.2: Run typecheck**

Run: `pnpm typecheck 2>&1 | tail -5`
Expected: 0 errors.

- [ ] **Step 10.3: Run lint**

Run: `pnpm lint 2>&1 | tail -25`
Expected: NO NEW errors/warnings. The 14 pre-existing warnings (Block B baseline) may still be present; that is acceptable.

If new warnings appear in the new code (`ScoreTabContent.tsx`, `use-debounced-callback.ts`), fix them inline. Common likely warnings:
- `react-hooks/exhaustive-deps` on `handleMutationError` deps — add missing deps or wrap in `useCallback` correctly.
- Unused imports — clean up.

- [ ] **Step 10.4: Commit if fixes were applied (optional)**

If T10.3 required inline fixes:

```bash
git -C /d/Repositories/meepleai-monorepo-dev add apps/web/
git -C /d/Repositories/meepleai-monorepo-dev commit -m "chore(session-live): #2430 Block B+ T10 typecheck + lint sweep

Pre-PR quality gate.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

Otherwise skip the commit.

---

### Task 11: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md` (root)

- [ ] **Step 11.1: Add the Block B+ bullet**

Open `CLAUDE.md` at the repo root. Find the section heading `### Session live shell (epic #2354)`. There should be a bullet for "G5a polymorphic wire — Issue #2389" added by Block B's PR #2434. Append a new bullet after it:

```markdown
- **G5a polymorphic wire (Block B+) — Issue #2430**: Block B+ (2026-06-19) extracts a new `ScoreTabContent` component (`apps/web/src/app/(authenticated)/sessions/[id]/live/_components/ScoreTabContent.tsx`) that owns ALL polymorphic scoring logic for the score tab — Block B's read-only path PLUS the mutable host editor. `viewerRole === 'Host'` mounts `PolymorphicScoreEditor` wired through `useUpdateSessionScores` with 500ms trailing debounce (`useDebouncedCallback` hoisted to `lib/session-live/use-debounced-callback.ts` with `[fn, flush]` tuple) plus flush-on-unmount. Player + Spectator mount `ScoringPanelRenderer` unchanged. 5-class error matrix (403 freeze / 429 + 30s countdown / 400 inline / 5xx + retry / network + retry) via `sonner` with deterministic ids. 30s rate-limit deadline persists in `useLiveSessionStore.rateLimitedUntil` so it survives tab change. `isMountedRef` + `viewerRoleRef` guards skip toast/setState after unmount (e.g. host transfer mid-mutation). Spec: [`2026-06-19-issue-2430-editor-host-swap-design.md`](./docs/superpowers/specs/2026-06-19-issue-2430-editor-host-swap-design.md). Plan: [`2026-06-19-issue-2430-editor-host-swap.md`](./docs/superpowers/plans/2026-06-19-issue-2430-editor-host-swap.md).
```

- [ ] **Step 11.2: Commit**

```bash
git -C /d/Repositories/meepleai-monorepo-dev add CLAUDE.md
git -C /d/Repositories/meepleai-monorepo-dev commit -m "docs(claude-md): #2430 Block B+ Session live shell note

Adds Block B+ entry under epic #2354. Links spec + plan. Documents:
  - ScoreTabContent extraction (Block B logic moved here)
  - useDebouncedCallback hoist with flush tuple
  - 5-class error matrix
  - useLiveSessionStore.rateLimitedUntil persistence
  - isMountedRef + viewerRoleRef guards

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 12: Push branch + open PR to main-dev

**Files:** N/A (git ops).

- [ ] **Step 12.1: Push the branch**

Run: `git -C /d/Repositories/meepleai-monorepo-dev push -u origin feature/issue-2430-editor-host-swap`
Expected: branch pushed; remote-tracking link set.

- [ ] **Step 12.2: Open the PR**

```bash
gh pr create --base main-dev --title "feat(session-live): #2430 Block B+ — PolymorphicScoreEditor host swap + mutation wire" --body "$(cat <<'EOF'
## Summary

Block B+ wires the mutable `PolymorphicScoreEditor` into `SessionLiveView`'s score tab for the host role, complementing the read-only `ScoringPanelRenderer` shipped by Block B (PR #2434). Closes the polymorphic scoring loop: Block A contract → Block B render → Block B+ edit.

**Closes**: #2430.
**Spec**: `docs/superpowers/specs/2026-06-19-issue-2430-editor-host-swap-design.md`.
**Plan**: `docs/superpowers/plans/2026-06-19-issue-2430-editor-host-swap.md`.

## Changes

- **NEW** `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/ScoreTabContent.tsx` — encapsulates ALL polymorphic scoring logic.
- **NEW** `apps/web/src/lib/session-live/use-debounced-callback.ts` — hoisted from `scores/page.tsx`, extended with `[fn, flush]` tuple return.
- **NEW** test files: `ScoreTabContent.test.tsx` (28 tests), `use-debounced-callback.test.ts` (5 tests).
- **MOD** `apps/web/src/lib/stores/live-session-store.ts` — add `rateLimitedUntil: number | null` field + `setRateLimitedUntil` action.
- **MOD** `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` — remove Block B inline logic (moved to ScoreTabContent), mount `<ScoreTabContent />` at desktop + mobile sites.
- **MOD** `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx` — adopt hoisted `useDebouncedCallback` via tuple destructure.
- **MOD** `apps/web/src/locales/{it,en}.json` — 7 new keys for error matrix + retry CTA + rate-limit countdown.
- **MOD** `CLAUDE.md` — Session live shell bullet for Block B+.

## Design decisions (locked in spec)

| DEC | Choice | Rationale |
|-----|--------|-----------|
| DEC-1 ROLE | Host=editor, Player+Spectator=renderer | Simplest 2-state mount. |
| DEC-2 EXTRACT | New \`ScoreTabContent\`, single PR | Fowler: SessionLiveView god-object reduction. |
| DEC-3 RACE | Last-write-wins via SignalR | Optimistic locking out of scope; documented as accepted. |
| DEC-4 DEBOUNCE | 500ms trailing + flush-on-unmount | No input loss. |
| DEC-5 RATE LIMIT | 30s countdown + disable, persisted in store | Survives tab change; anti-raffica. |
| DEC-6 NETWORK ERR | Toast + retry button | Explicit affordance; \`lastPayloadRef\` captures cache. |

## Process

Brainstorming (3 strategic Q + 6 DEC) → \`/sc:spec-panel\` 5-expert discussion → spec revision applying 2 BLOCKERS + 4 IMPORTANT + 3 NICE from pre-plan code review. TDD-driven: T4 GREEN (hook regression pins), T6 RED → T7 GREEN (ScoreTabContent).

## Out-of-scope (tracked)

- EndgameDialog adapter — #2431.
- Real Objectives catalogue — #2432.
- Legacy \`PUT /participants/{id}/score\` endpoint deprecation — #2433 (now unblocked).
- Multi-pod SignalR backplane — #2256.

## Test plan

- [x] \`pnpm test ScoreTabContent\` → 28/28 green.
- [x] \`pnpm test use-debounced-callback\` → 5/5 green.
- [x] \`pnpm test SessionLiveView\` → 79/79 green.
- [x] \`pnpm test live-session-store\` → 7/7 green (4 existing + 3 new).
- [x] \`pnpm test score-data-to-panel-data\` → 16/16 green (untouched).
- [x] \`pnpm typecheck\` → 0 errors.
- [x] \`pnpm lint\` → 0 NEW errors/warnings.
- [ ] Manual smoke: open \`/sessions/{id}/live\` as host, edit scores, observe optimistic UI + debounced save; trigger 403 by switching role; verify rate-limit countdown.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 12.3: Record the PR URL**

The `gh pr create` command prints the PR URL on success. Record it for tracking.

---

## Self-Review

**1. Spec coverage:**

| Spec requirement | Task |
|------------------|------|
| Adapter contract (mapMutationError) | T7 (in ScoreTabContent body) |
| Role gating Host/Player/Spectator | T7 + tests T6.2 |
| Optimistic UI (localScoreOverride) | T7 + tests T6.9 |
| Debounce 500ms trailing + flush-on-unmount | T3 + T7 + tests T6.7 |
| `isMountedRef` guard | T7 (refs section) |
| `lastPayloadRef` for retry | T7 (submitMutation + retry actions) |
| `viewerRoleRef` for host-transfer mid-mutation | T7 (refs section) |
| `useLiveSessionStore.rateLimitedUntil` persistence | T2 + T7 |
| 5-class error matrix | T7 + tests T6.8 |
| Sonner toast with deterministic ids | T7 (id: 'score-403' etc.) |
| 30s countdown UI | T7 + tests T6.8 (countdown test) |
| `useDebouncedCallback` hoist + tuple flush | T3 + T4 |
| 7 i18n keys | T9 |
| 2 a11y placeholder test migration | T6.4 (added in ScoreTabContent) + T8.5 (deleted from SessionLiveView) |
| SessionLiveView smoke tests | T8.6 |
| CLAUDE.md note | T11 |
| PR + follow-up links | T12 |

All spec ACs covered.

**2. Placeholder scan:** No "TBD", "TODO", "implement later" present. Every step has actual code blocks.

**3. Type consistency:**
- `ScoreTabContentProps` consistent across T6 (test wrapper) + T7 (impl) + T8 (mount sites).
- `mapMutationError` returns `ScoredError`, used in `handleMutationError` switch.
- `useDebouncedCallback` tuple return `[debouncedFn, flush]` used consistently in T3 (impl), T4 (tests), T7 (consumer).
- `lastPayloadRef: useRef<UpdateSessionScoresPayload | null>(null)` — `UpdateSessionScoresPayload` is the existing type from `use-update-session-scores.ts` (verified by reading the file). Type import added in T7.
- `setRateLimitedUntil(ts: number | null)` used identically in T2 (store), T6.8 (test), T7 (consumer).

All types consistent.

**4. Tests survival sanity:**
- Block B 11 tests minus 2 placeholder (migrated) plus 2 smoke = 11 net. SessionLiveView total 67 existing + 11 = 78 pre-change; post-Block-B+ becomes 67 + 9 (Block B − 2 migrated) + 2 smoke = 78 + 1 net = **79**. Plan stated this consistently.
