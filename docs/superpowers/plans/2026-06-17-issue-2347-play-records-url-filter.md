# Play Records URL Filter Persistence — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere round-trip sync URL ↔ Zustand store per il filtro `status` su `/play-records`, con validation silenziosa per status invalido, 4 unit test e 4 E2E Playwright. Effort ~0.5-0.7gg.

**Architecture:** Single component change in `PlayHistory.tsx` — aggiunge `useSearchParams()`/`useRouter()` import + 2 `useEffect` round-trip (URL→store on mount/change; store→URL on filter set) + helper `parseStatusParam(searchParams)` con allowlist `['all', 'InProgress', 'Completed', 'Planned']`. No BE changes. Spec doc: `docs/superpowers/specs/2026-06-17-issue-2347-play-records-url-filter-persistence.md`.

**Tech Stack:** Next.js 16 App Router (`next/navigation`), Zustand persist (`usePlayRecordsStore`), Vitest + Testing Library (unit), Playwright + @axe-core/playwright (E2E + a11y).

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `apps/web/src/components/play-records/PlayHistory.tsx` | MODIFY | Add `useSearchParams`/`useRouter` imports; add `parseStatusParam` helper; add 2 `useEffect` for round-trip sync. ~30 LOC added. |
| `apps/web/src/components/play-records/__tests__/PlayHistory.test.tsx` | MODIFY | Add `useSearchParams` mock alongside existing `useRouter` mock; add 4 unit tests in a new `describe('URL filter persistence', ...)` block. ~120 LOC added. |
| `apps/web/e2e/play-records-hub.spec.ts` | CREATE | New Playwright spec con 4 scenarios + axe AA smoke. ~150 LOC. |

**Skipped:**
- `admin-mockups/design_files/sp4-play-records-index.fidelity.json` — già contiene self-waiver dal Phase B audit (2026-06-15, format "user@meepleAi self-waiver P250"). No update needed.

---

## DEC user-locked (spec doc)

- **DEC-1**: minimal gap-fill scope (no BE refactor, no outcome chip)
- **DEC-2**: URL shape `?status=` only; search + view restano locali
- **DEC-5**: designer self-waiver P250 (già applicato da Phase B audit)
- **DEC-6**: 5 stati canonici minimal verification (default/empty/loading/error; sse N/A)

---

## Task 1: Add `parseStatusParam` helper + failing test

**Files:**
- Modify: `apps/web/src/components/play-records/__tests__/PlayHistory.test.tsx`
- Modify: `apps/web/src/components/play-records/PlayHistory.tsx` (top-level helper)

- [ ] **Step 1: Write failing test for `parseStatusParam` allowlist**

Add this `describe` block at the END of `PlayHistory.test.tsx` (after existing tests):

```typescript
describe('parseStatusParam (URL allowlist validation)', () => {
  // Helper export needed — see Step 3
  it('returns "all" when param is null', () => {
    const result = parseStatusParam(null);
    expect(result).toBe('all');
  });

  it('returns "all" when param is invalid', () => {
    const result = parseStatusParam('foo-bar');
    expect(result).toBe('all');
  });

  it('returns "all" when param is "Archived" (not exposed as chip)', () => {
    // Archived exists in PlayRecordStatus enum but is NOT a chip option
    // → URL validation must reject it to keep UX coherent.
    const result = parseStatusParam('Archived');
    expect(result).toBe('all');
  });

  it.each(['all', 'InProgress', 'Completed', 'Planned'])(
    'returns "%s" when param matches a chip-exposed status',
    (status) => {
      const result = parseStatusParam(status);
      expect(result).toBe(status);
    }
  );
});
```

Add import at the top of the test file (after existing imports):

```typescript
import { parseStatusParam } from '../PlayHistory';
```

- [ ] **Step 2: Run test to verify it fails**

Run from `apps/web/`:

```bash
pnpm vitest run src/components/play-records/__tests__/PlayHistory.test.tsx -t "parseStatusParam"
```

Expected output: `Test Files 1 failed (1)` with error `SyntaxError: The requested module '../PlayHistory' does not provide an export named 'parseStatusParam'`.

- [ ] **Step 3: Add `parseStatusParam` helper in PlayHistory.tsx**

In `apps/web/src/components/play-records/PlayHistory.tsx`, ADD this exported helper IMMEDIATELY AFTER the existing imports (before `// ── Component ────`):

```typescript
import type { PlayRecordStatus } from '@/lib/api/schemas/play-records.schemas';

const VALID_STATUS_PARAMS = new Set<string>(['all', 'InProgress', 'Completed', 'Planned']);

/**
 * Parse the `?status=` URL param against the chip-exposed allowlist.
 * `Archived` exists in `PlayRecordStatus` enum but is intentionally NOT a chip
 * option — direct URL navigation to `?status=Archived` falls back to `all`
 * silently to keep UX coherent with visible filter chips.
 */
export function parseStatusParam(param: string | null): PlayRecordStatus | 'all' {
  if (param === null) return 'all';
  if (!VALID_STATUS_PARAMS.has(param)) return 'all';
  return param as PlayRecordStatus | 'all';
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run from `apps/web/`:

```bash
pnpm vitest run src/components/play-records/__tests__/PlayHistory.test.tsx -t "parseStatusParam"
```

Expected output: `Tests 6 passed (6)` (one per allowed value + 3 fallback cases).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/play-records/PlayHistory.tsx \
        apps/web/src/components/play-records/__tests__/PlayHistory.test.tsx
git commit -m "feat(play-records): #2347 add parseStatusParam URL allowlist helper

Validation helper for the ?status= query param. Allowlist follows chip
exposure (all + InProgress + Completed + Planned); Archived enum value
exists but is not a chip → falls back to 'all' silently.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: Mock `useSearchParams` in test file

**Files:**
- Modify: `apps/web/src/components/play-records/__tests__/PlayHistory.test.tsx`

- [ ] **Step 1: Extend existing `next/navigation` mock**

Find the existing mock block (around line 41 of the current file):

```typescript
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));
```

REPLACE it with this expanded mock that exposes `useSearchParams` + `useRouter` controllable in each test:

```typescript
const searchParamsMap: Record<string, string> = {};
const routerReplace = vi.fn();
const routerPush = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: routerPush,
    replace: routerReplace,
  }),
  useSearchParams: () => ({
    get: (key: string) => searchParamsMap[key] ?? null,
    toString: () => new URLSearchParams(searchParamsMap).toString(),
  }),
}));
```

Then add this in the existing `beforeEach` (or create one if absent) of the top-level `describe('PlayHistory', ...)` block:

```typescript
beforeEach(() => {
  Object.keys(searchParamsMap).forEach((k) => delete searchParamsMap[k]);
  routerReplace.mockClear();
  routerPush.mockClear();
});
```

- [ ] **Step 2: Run full test file to verify no regression**

Run from `apps/web/`:

```bash
pnpm vitest run src/components/play-records/__tests__/PlayHistory.test.tsx
```

Expected: all pre-existing tests STILL PASS (no behavior change yet — only mock surface extended).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/play-records/__tests__/PlayHistory.test.tsx
git commit -m "test(play-records): #2347 expand next/navigation mock for useSearchParams

Prep for URL filter persistence tests — adds controllable searchParamsMap
+ routerReplace spy. Existing tests unchanged.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: URL → store sync (deep-link mount)

**Files:**
- Modify: `apps/web/src/components/play-records/PlayHistory.tsx`
- Modify: `apps/web/src/components/play-records/__tests__/PlayHistory.test.tsx`

- [ ] **Step 1: Write failing test "deep-link sets filter"**

Add this `describe` block in `PlayHistory.test.tsx` AFTER `describe('parseStatusParam ...)`:

```typescript
describe('URL → store sync (deep-link mount)', () => {
  it('sets filter status from ?status= on mount', async () => {
    // Arrange: simulate URL deep-link
    searchParamsMap['status'] = 'Completed';

    // Need to inspect what setFilter receives — re-mock store to capture calls
    const setFilterSpy = vi.fn();
    vi.doMock('@/lib/stores/play-records-store', () => ({
      usePlayRecordsStore: (selector: (state: any) => any) => {
        const state = {
          filters: { gameId: undefined, status: 'all' as const },
          sortBy: 'recent' as const,
          setFilter: setFilterSpy,
          resetFilters: vi.fn(),
          setSortBy: vi.fn(),
        };
        return selector(state);
      },
      selectFilters: (state: any) => state.filters,
      selectHasActiveFilters: (state: any) => false,
    }));

    // Re-import the component with the patched mock
    const { PlayHistory: PatchedPlayHistory } = await import('../PlayHistory');

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={queryClient}>
        <PatchedPlayHistory />
      </QueryClientProvider>
    );

    // Assert: setFilter called with ('status', 'Completed')
    await waitFor(() => {
      expect(setFilterSpy).toHaveBeenCalledWith('status', 'Completed');
    });
  });

  it('does NOT call setFilter when URL status matches current store state', async () => {
    searchParamsMap['status'] = 'all'; // matches default store filters.status = 'all'

    const setFilterSpy = vi.fn();
    vi.doMock('@/lib/stores/play-records-store', () => ({
      usePlayRecordsStore: (selector: (state: any) => any) => {
        const state = {
          filters: { gameId: undefined, status: 'all' as const },
          sortBy: 'recent' as const,
          setFilter: setFilterSpy,
          resetFilters: vi.fn(),
          setSortBy: vi.fn(),
        };
        return selector(state);
      },
      selectFilters: (state: any) => state.filters,
      selectHasActiveFilters: (state: any) => false,
    }));

    const { PlayHistory: PatchedPlayHistory } = await import('../PlayHistory');

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={queryClient}>
        <PatchedPlayHistory />
      </QueryClientProvider>
    );

    // Wait a tick to allow effects to run
    await new Promise((r) => setTimeout(r, 50));

    // Assert: setFilter NOT called (no-op when URL == store)
    expect(setFilterSpy).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run from `apps/web/`:

```bash
pnpm vitest run src/components/play-records/__tests__/PlayHistory.test.tsx -t "URL → store sync"
```

Expected: `Tests 2 failed (2)` — `setFilterSpy` is NOT called because the URL→store effect doesn't exist yet.

- [ ] **Step 3: Implement URL→store sync useEffect**

In `apps/web/src/components/play-records/PlayHistory.tsx`:

1. Change the import line at the top:
   ```typescript
   import { useEffect, useState } from 'react';
   ```
   to:
   ```typescript
   import { useEffect, useState } from 'react';
   import { useRouter, useSearchParams } from 'next/navigation';
   ```

2. INSIDE the `PlayHistory` function component, AFTER the existing `usePlayRecordsStore` selector hooks (around line 49 of current file, after `const resetFilters = ...`), ADD:

   ```typescript
   const router = useRouter();
   const searchParams = useSearchParams();

   // ── DEC-2 #2347: URL → store sync (deep-link entry point) ────────────────
   useEffect(() => {
     const urlStatus = parseStatusParam(searchParams?.get('status') ?? null);
     if (urlStatus !== filters.status) {
       setFilter('status', urlStatus);
     }
   }, [searchParams, filters.status, setFilter]);
   ```

- [ ] **Step 4: Run tests to verify pass**

Run from `apps/web/`:

```bash
pnpm vitest run src/components/play-records/__tests__/PlayHistory.test.tsx -t "URL → store sync"
```

Expected: `Tests 2 passed (2)`.

- [ ] **Step 5: Run full test file**

```bash
pnpm vitest run src/components/play-records/__tests__/PlayHistory.test.tsx
```

Expected: all tests pass (existing + new).

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/play-records/PlayHistory.tsx \
        apps/web/src/components/play-records/__tests__/PlayHistory.test.tsx
git commit -m "feat(play-records): #2347 URL → store sync for deep-link filter

useEffect reads ?status= and calls setFilter() on mount + URL change.
No-op when URL value matches current store state (idempotency).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: Store → URL sync (chip click)

**Files:**
- Modify: `apps/web/src/components/play-records/PlayHistory.tsx`
- Modify: `apps/web/src/components/play-records/__tests__/PlayHistory.test.tsx`

- [ ] **Step 1: Write failing test "chip click pushes URL"**

Add this `describe` block in `PlayHistory.test.tsx` AFTER `describe('URL → store sync ...)`:

```typescript
describe('store → URL sync (chip click)', () => {
  it('calls router.replace with ?status=InProgress when filter changes from "all"', async () => {
    // We simulate the store transition by spying on setFilter and re-rendering
    // with new state. Since the existing module mock has filters.status='all'
    // statically, we cover this transition via the dynamic doMock pattern.

    let currentStatus: PlayRecordStatus | 'all' = 'all';
    const setFilterMock = vi.fn((_field, value) => {
      currentStatus = value;
    });

    vi.doMock('@/lib/stores/play-records-store', () => ({
      usePlayRecordsStore: (selector: (state: any) => any) => {
        const state = {
          filters: { gameId: undefined, status: currentStatus },
          sortBy: 'recent' as const,
          setFilter: setFilterMock,
          resetFilters: vi.fn(),
          setSortBy: vi.fn(),
        };
        return selector(state);
      },
      selectFilters: (state: any) => state.filters,
      selectHasActiveFilters: (state: any) => false,
    }));

    const { PlayHistory: PatchedPlayHistory } = await import('../PlayHistory');

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { rerender } = render(
      <QueryClientProvider client={queryClient}>
        <PatchedPlayHistory />
      </QueryClientProvider>
    );

    // Simulate store change: currentStatus becomes 'InProgress' externally
    currentStatus = 'InProgress';
    rerender(
      <QueryClientProvider client={queryClient}>
        <PatchedPlayHistory />
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(routerReplace).toHaveBeenCalled();
      const callArg = (routerReplace.mock.calls[0]?.[0] ?? '') as string;
      expect(callArg).toContain('status=InProgress');
    });
  });

  it('calls router.replace without status param when filter is "all"', async () => {
    let currentStatus: PlayRecordStatus | 'all' = 'Completed';
    const setFilterMock = vi.fn((_field, value) => {
      currentStatus = value;
    });

    vi.doMock('@/lib/stores/play-records-store', () => ({
      usePlayRecordsStore: (selector: (state: any) => any) => {
        const state = {
          filters: { gameId: undefined, status: currentStatus },
          sortBy: 'recent' as const,
          setFilter: setFilterMock,
          resetFilters: vi.fn(),
          setSortBy: vi.fn(),
        };
        return selector(state);
      },
      selectFilters: (state: any) => state.filters,
      selectHasActiveFilters: (state: any) => false,
    }));

    const { PlayHistory: PatchedPlayHistory } = await import('../PlayHistory');

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { rerender } = render(
      <QueryClientProvider client={queryClient}>
        <PatchedPlayHistory />
      </QueryClientProvider>
    );

    // Reset back to 'all'
    currentStatus = 'all';
    rerender(
      <QueryClientProvider client={queryClient}>
        <PatchedPlayHistory />
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(routerReplace).toHaveBeenCalled();
      const allCalls = routerReplace.mock.calls.map((c) => c[0] as string);
      // The last call should NOT contain status=
      expect(allCalls[allCalls.length - 1]).not.toContain('status=');
    });
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run from `apps/web/`:

```bash
pnpm vitest run src/components/play-records/__tests__/PlayHistory.test.tsx -t "store → URL sync"
```

Expected: `Tests 2 failed (2)` — `routerReplace` is never called.

- [ ] **Step 3: Implement store→URL sync useEffect**

In `apps/web/src/components/play-records/PlayHistory.tsx`, IMMEDIATELY AFTER the URL→store useEffect added in Task 3, ADD:

```typescript
   // ── DEC-2 #2347: store → URL sync (chip click → shareable URL) ───────────
   useEffect(() => {
     const params = new URLSearchParams(searchParams?.toString() ?? '');
     if (filters.status === 'all') {
       params.delete('status');
     } else {
       params.set('status', filters.status);
     }
     const queryString = params.toString();
     router.replace(queryString ? `?${queryString}` : '/play-records', { scroll: false });
   }, [filters.status, router, searchParams]);
```

- [ ] **Step 4: Run tests to verify pass**

Run from `apps/web/`:

```bash
pnpm vitest run src/components/play-records/__tests__/PlayHistory.test.tsx -t "store → URL sync"
```

Expected: `Tests 2 passed (2)`.

- [ ] **Step 5: Run FULL test file**

```bash
pnpm vitest run src/components/play-records/__tests__/PlayHistory.test.tsx
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/play-records/PlayHistory.tsx \
        apps/web/src/components/play-records/__tests__/PlayHistory.test.tsx
git commit -m "feat(play-records): #2347 store → URL sync for chip click

useEffect on filters.status uses router.replace to mirror the active chip
into ?status= for shareable links. Resets to clean URL when filter='all'.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: E2E Playwright spec (4 scenari + axe AA)

**Files:**
- Create: `apps/web/e2e/play-records-hub.spec.ts`

- [ ] **Step 1: Read existing E2E patterns for reference**

Inspect `apps/web/e2e/` for an existing file that uses both Playwright + axe (pattern blueprint). Look for one of:

```bash
ls apps/web/e2e/ | head -20
grep -l "@axe-core/playwright" apps/web/e2e/*.spec.ts | head -3
```

If found, use its setup boilerplate (auth context, baseURL, axe scan helper). Otherwise fall back to the standalone pattern below.

- [ ] **Step 2: Create the E2E file**

Create `apps/web/e2e/play-records-hub.spec.ts` with the following content:

```typescript
/**
 * E2E — Play Records hub URL filter persistence (#2347).
 *
 * Covers the 4 scenarios locked in spec
 * `docs/superpowers/specs/2026-06-17-issue-2347-play-records-url-filter-persistence.md`
 * Acceptance §Testing.
 *
 * Also runs axe AA smoke on default + filter-empty states (DEC-6).
 */

import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

const HUB = '/play-records';

test.describe('Play Records hub — URL filter persistence', () => {
  test.beforeEach(async ({ page }) => {
    // Assume test fixture user is auto-authenticated by global setup
    // (matches existing E2E patterns in this repo). If not, add login here.
    await page.goto(HUB);
    await page.waitForLoadState('networkidle');
  });

  test('Scenario 1 — Default mount: "Tutte" chip pressed, URL clean', async ({ page }) => {
    await expect(page).toHaveURL(/\/play-records(?:\?tab=stats)?$/);
    const allChip = page.getByTestId('filter-status-all');
    await expect(allChip).toHaveAttribute('aria-pressed', 'true');
  });

  test('Scenario 2 — Filter chip click → URL updates', async ({ page }) => {
    await page.getByTestId('filter-status-Completed').click();
    await expect(page).toHaveURL(/\?status=Completed/);
    await expect(page.getByTestId('filter-status-Completed')).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  test('Scenario 3 — Deep-link ?status=InProgress: chip pre-pressed', async ({ page }) => {
    await page.goto(`${HUB}?status=InProgress`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('filter-status-InProgress')).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  test('Scenario 4 — Empty filter state retry: reset chip restores URL', async ({ page }) => {
    await page.goto(`${HUB}?status=Planned`);
    await page.waitForLoadState('networkidle');

    // Wait for either the empty state OR the records list — whichever the fixture provides.
    const emptyState = page.getByTestId('play-history-empty-filter');
    const recordsList = page.getByTestId('play-history').locator('[data-testid^="play-record-"]');
    await Promise.race([
      emptyState.waitFor({ state: 'visible', timeout: 5_000 }).catch(() => null),
      recordsList.first().waitFor({ state: 'visible', timeout: 5_000 }).catch(() => null),
    ]);

    if (await emptyState.isVisible()) {
      // Click reset CTA inside the empty state
      const resetCta = emptyState.getByRole('button');
      await resetCta.click();
      await expect(page).toHaveURL(/\/play-records$/);
      await expect(page.getByTestId('filter-status-all')).toHaveAttribute('aria-pressed', 'true');
    } else {
      // Fixture happened to have Planned records — verify default chip-click reset still works
      await page.getByTestId('filter-status-all').click();
      await expect(page).toHaveURL(/\/play-records$/);
    }
  });
});

test.describe('Play Records hub — axe AA smoke', () => {
  test('axe AA: default state has 0 violations', async ({ page }) => {
    await page.goto(HUB);
    await page.waitForLoadState('networkidle');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });

  test('axe AA: filter-empty state has 0 violations', async ({ page }) => {
    await page.goto(`${HUB}?status=Planned`);
    await page.waitForLoadState('networkidle');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });
});
```

- [ ] **Step 3: Run E2E to verify it passes (or to capture infra gaps)**

From `apps/web/`:

```bash
pnpm test:e2e play-records-hub
```

Expected outcomes:
- **Pass**: 6 tests green → proceed to Step 4.
- **Fail (auth setup)**: tests redirected to `/login`. Add `test.use({ storageState: 'e2e/.auth/user.json' })` if the repo uses a fixture storage state. Inspect any other E2E spec in `e2e/` for the auth pattern and replicate it.
- **Fail (selector mismatch)**: `data-testid="filter-status-..."` may have been renamed. Re-check `RecordFilters.tsx:85` (`data-testid={\`filter-status-${option.value}\`}`); if differing, update spec selectors to match. Do NOT change the component.

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/play-records-hub.spec.ts
git commit -m "test(e2e): #2347 play records hub URL filter + axe AA

4 scenarios (default mount, chip click → URL, deep-link mount, empty
state retry) + 2 axe AA smoke (default + filter-empty).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: Final verification + push + PR

**Files:** All previously modified.

- [ ] **Step 1: Full type + lint check**

From `apps/web/`:

```bash
pnpm typecheck && pnpm lint
```

Expected: 0 errors. Warnings are tolerated only if pre-existing (compare with `git diff` to ensure your changes did not add new warnings).

- [ ] **Step 2: Run the broader play-records test subtree**

```bash
pnpm vitest run src/components/play-records
```

Expected: all tests pass. If `PlayRecordDetailView.test.tsx` or `SessionCreateForm.test.tsx` fail with the new mock surface for `next/navigation`, the previously-defined `routerReplace`/`useSearchParams` mock may have leaked into shared module state. Fix by scoping the mock setup to a `vi.hoisted` block or `beforeEach` reset only in the specific file you modified.

- [ ] **Step 3: Push branch**

```bash
git push -u origin feature/issue-2347-play-records-url-filter
```

- [ ] **Step 4: Open PR**

```bash
gh pr create --base main-dev \
  --title "feat(play-records): #2347 URL filter persistence (minimal gap-fill)" \
  --body "$(cat <<'EOF'
## Summary

Implements `?status=` URL filter persistence for /play-records hub. Round-trip
sync URL ↔ Zustand store with silent fallback to 'all' on invalid status.

Scope locked to minimal gap-fill per spec brainstorming 2026-06-17:
- DEC-1: no BE refactor, no outcome chip
- DEC-2: URL shape `?status=` only (search + view stay local)
- DEC-5: designer self-waiver P250 (fidelity.json already self-waived in Phase B)
- DEC-6: 5 stati canonici verified (default/empty/loading/error; sse N/A)

## Changes

- `PlayHistory.tsx`: add `parseStatusParam` helper + 2 useEffect (URL→store + store→URL)
- `PlayHistory.test.tsx`: 8 new unit tests (4 helper + 2 URL→store + 2 store→URL)
- `e2e/play-records-hub.spec.ts`: 4 E2E + 2 axe AA smoke

## Test plan

- [x] Unit (Vitest): play-records subtree green
- [x] Typecheck + lint clean
- [ ] Playwright E2E green (verify CI)
- [ ] axe AA 0 violations

## Out of scope (future)

- Outcome filter chip (Vinti/Persi/Last-week)
- BE cursor-based pagination
- Search URL persistence (debounce)

## Closes

- Closes #2347

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 5: Verify PR URL returned**

The command prints a PR URL like `https://github.com/meepleAi-app/meepleai-monorepo/pull/NNNN`. Save this URL.

- [ ] **Step 6: Update task tracker**

Mark `Implement #2347 URL filter persistence` as completed in the TaskList. Mark `Open PR #2347 + close issue` as in_progress until the PR is merged (admin-squash pattern P145 — user action).

---

## Self-Review

### Spec coverage

| Spec section | Task(s) |
|---|---|
| §DEC-1 minimal gap-fill | All tasks — no BE work, no outcome chip |
| §DEC-2 URL shape status only | T3 (URL→store), T4 (store→URL), T5 (E2E Scenario 2) |
| §DEC-5 designer self-waiver | N/A — already in fidelity.json (Phase B audit 2026-06-15) |
| §DEC-6 5 stati canonici | T5 axe AA scenarios cover default + filter-empty |
| §Architettura Round-trip pattern | T3 + T4 implement both effects |
| §Validation input URL silent fallback | T1 parseStatusParam helper + tests |
| §Testing Unit (4 test) | T1 (4) + T3 (2) + T4 (2) = 8 total (exceeds spec target — better coverage) |
| §Testing E2E (4 scenari) | T5 4 scenari + 2 axe AA |
| §Testing axe AA | T5 |
| §DoD lint + typecheck clean | T6 Step 1 |
| §DoD CI green | T6 Step 4 PR opens → CI runs |
| §DoD PR aperta su main-dev | T6 Step 4 |

### Placeholder scan

No "TBD", "TODO", "fill in" anywhere. All code blocks contain executable code, all commands have expected output.

### Type consistency

- `parseStatusParam(param: string | null): PlayRecordStatus | 'all'` — defined T1, used T3.
- `useSearchParams()?.get('status')` returns `string | null` → matches helper signature.
- `setFilter('status', urlStatus)` — `setFilter` is generic `<K extends keyof PlayHistoryFilters>` per store type (verified via `play-records-store.ts:55` grep).
- `filters.status: PlayRecordStatus | 'all'` — already the existing store type (verified via `PlayHistory.tsx:81` `statusFilter={filters.status}` already typed).
- `router.replace(href, options)` — Next.js 16 App Router signature, matches usage in T4 Step 3.

### Decomposition check

- 6 tasks, each in 2–6 steps. Each step is a single action 2–5 minutes.
- Tasks committable independently (each ends with `git commit`).
- TDD discipline: tests first in T1, T3, T4. T2 is mock-prep (no test added until T3). T5 is E2E that runs against implemented behavior (post-T3+T4).

Plan ready for execution.
