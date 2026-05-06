# Wave B.1 — `/games` Library Tab v2 — Implementation Plan

**Issue**: #633 (parent: #580 Wave B umbrella · #578 Phase 1)
**Spec**: [`docs/superpowers/specs/2026-04-29-v2-migration-wave-b-1-games.md`](../specs/2026-04-29-v2-migration-wave-b-1-games.md)
**Branch**: `feature/issue-633-games-fe-v2` (parent: `main-dev`)
**Date**: 2026-04-29
**Plan owner**: implementing engineer
**Plan status**: ready for execution (post-review)

---

## 0. Plan philosophy

This is a **TDD red→green→refactor** decomposition of the spec §8 `5-commit sequencing`. Each step lists precondition gates, command-set, validation criteria, and rollback procedure. Steps are designed for atomicity — every commit is independently revertible without leaving the tree in a broken state.

**Non-goals of this plan** (explicit):
- It does NOT redo the spec-panel review (already integrated in spec §10).
- It does NOT add scope beyond spec §1 goals.
- It does NOT pre-write code — only sequences the work.

---

## 1. Pre-flight gates (P-1 through P-4) — MUST PASS before branching

### P-1 — Branch hygiene

```bash
git fetch origin
git checkout main-dev
git pull --ff-only origin main-dev
git status   # MUST be clean (no untracked, no staged)
git log -1 --oneline   # confirm latest main-dev commit (e.g. PR #632 / #623 / #626 ancestors)
```

**Pass criteria**: working tree clean, on `main-dev`, fast-forward succeeded.
**Fail handling**: if dirty, stash with named ref `git stash push -m "pre-b1-stash"`. Do NOT proceed with rebase if main-dev has diverged unexpectedly — investigate.

### P-2 — Backend data contract verification (spec §3.4 BLOCKER #3 resolution)

Confirm whether `UserLibraryEntry` exposes `playCount` / `lastPlayedAt` fields, deciding **Caso A** (include "Giocati" filter) vs **Caso B** (hide it, default).

```bash
# Start API in dev mode (or use existing dev container)
cd apps/api/src/Api && dotnet run --launch-profile Development &
sleep 5

# Authenticate (or reuse cookie). Then curl library endpoint:
curl -s -b cookies.txt http://localhost:8080/api/v1/library?page=1&pageSize=1 | jq '.items[0]'

# Inspect for keys:
# Caso A: response includes "playCount" AND "lastPlayedAt" → "Giocati" filter ENABLED
# Caso B: keys absent → "Giocati" filter HIDDEN, child issue tracked for backend extension
```

Alternative file-only check (no backend boot needed):
```bash
# Already verified in prior session: schemas/library.schemas.ts confirms Caso B (default)
grep -E "playCount|lastPlayedAt|totalSessions" apps/web/src/lib/api/schemas/library.schemas.ts
# Expected: 0 matches → Caso B confirmed
```

**Pass criteria**: clear A/B decision documented in Commit 1 message + spec §3.4 reflection.
**Default**: Caso B (filter hidden) — confirmed by prior schema inspection.
**Fail handling**: if backend boot fails, fall back to file-only check (already done).

### P-3 — Reuse audit (spec §3.7 + AC-3 + AC-9 + R6 + R9 prerequisites)

Verify the components/hooks we plan to reuse actually exist and expose the expected API:

```bash
# MeepleCard variant="list" availability (R9)
grep -n "variant.*list\|variant:.*'list'" apps/web/src/components/ui/data-display/meeple-card.tsx
# Expected: 1+ matches → AC-2 list view supported. If absent, R9 mitigation: AC-2 reduced to grid-only.

# useTablistKeyboardNav orientation parameter (PR #626)
grep -n "orientation" apps/web/src/hooks/useTablistKeyboardNav.ts
# Expected: 1+ matches → "horizontal" / "vertical" / "both" supported.

# useGames hook signature stability (R6 — spec §6)
grep -n "export.*useGames\|export.*useLibrary" apps/web/src/lib/hooks/
# Capture current signature: returned shape (data/isLoading/error), input params.
# Document in pre-flight log. If signature changes mid-PR, rebase + adapt orchestrator §5.1.

# axe-core helper presence
ls apps/web/e2e/_helpers/
# Look for: seedCookieConsent.ts, axe-builder integration. If missing, scaffold in Commit 1.

# LT-3 guard: confirm meeple-card.tsx is reused, NOT modified (spec §2 + §10 LT-3)
git log -1 --format="%H" apps/web/src/components/ui/data-display/meeple-card.tsx
# Document baseline SHA. AT PR TIME, verify SHA unchanged: zero-fork mandate (AC-3).
```

**Pass criteria**: all 4 grep+ls checks confirm reuse availability AND meeple-card.tsx baseline SHA recorded.
**Fail handling**: spec adjustments per R9 (variant fallback) or scaffold helper in Commit 1. If `useGames` signature pre-flight differs from spec assumption, halt and update spec §3.2 BEFORE branching.

### P-4 — Lint/typecheck baseline

```bash
cd apps/web
pnpm typecheck   # MUST be 0 errors on main-dev (baseline)
pnpm lint        # capture baseline warning count
pnpm test --run apps/web/src/lib/games 2>&1 | tail -5   # may be empty (no existing tests under lib/games)
```

**Pass criteria**: typecheck green, lint baseline noted (any new warnings introduced by our code MUST be 0).
**Fail handling**: if main-dev has typecheck errors, STOP and surface to user — pre-existing main-dev breakage is out-of-scope.

---

## 2. Branch creation + worktree config

```bash
# From clean main-dev (post P-1):
git checkout -b feature/issue-633-games-fe-v2
git config branch.feature/issue-633-games-fe-v2.parent main-dev
git status   # confirm new branch, clean tree
```

**Validation**: `git config --get branch.feature/issue-633-games-fe-v2.parent` returns `main-dev`. This is REQUIRED by CLAUDE.md "PR Target Rule" — PR base will be detected from this config.

---

## 3. Commit 1 — Foundation: helpers + i18n + visual fixture

**Goal**: ship pure utilities + locale strings + visual-test fixture WITHOUT touching v2 components or page.

### 3.1 Step 1.1 — Helper module + tests (TDD red→green)

**Red** — write `apps/web/src/lib/games/__tests__/library-filters.test.ts`:
- Test cases (~15):
  - `filterByStatus(entries, 'all')` returns all
  - `filterByStatus(entries, 'owned')` filters `currentState === 'Owned'`
  - `filterByStatus(entries, 'wishlist')` filters `currentState === 'Wishlist'`
  - `filterByStatus(entries, 'played')` (Caso B: returns all OR is dead-code-eliminated; Caso A: filters where `playCount > 0`)
  - `sortLibraryEntries(entries, 'title')` ASC
  - `sortLibraryEntries(entries, 'rating')` DESC nullsLast
  - `sortLibraryEntries(entries, 'year')` DESC nullsLast
  - `sortLibraryEntries(entries, 'last-played')` DESC by `addedAt` fallback (Caso B) OR `lastPlayedAt` (Caso A)
  - `deriveStats(entries)` returns `{ owned, wishlist, totalEntries, kbDocs }` correctly
  - `deriveStats([])` returns `{ owned: 0, wishlist: 0, totalEntries: 0, kbDocs: 0 }`
  - Edge: undefined `averageRating` sorts after defined ones (DESC)
  - Edge: case-insensitive title sort
  - Edge: matchQuery (search) on title/publisher/year-as-string

```bash
pnpm test --run apps/web/src/lib/games/__tests__/library-filters.test.ts
# Expected: 15 fails (module not yet implemented)
```

**Green** — implement `apps/web/src/lib/games/library-filters.ts` minimal pure helpers:
```ts
export type GamesStatusKey = 'all' | 'owned' | 'wishlist' | 'played';
export type GamesSortKey = 'last-played' | 'rating' | 'title' | 'year';
// pure functions, no React, no API client
```

```bash
pnpm test --run apps/web/src/lib/games/__tests__/library-filters.test.ts
# Expected: 15/15 green
```

**Refactor**: extract type guards if needed; keep cyclomatic complexity ≤8 per function.

### 3.2 Step 1.2 — Visual-test fixture (sentinel pattern A.4)

Create `apps/web/src/lib/games/visual-test-fixture.ts`:
- Constant `VISUAL_TEST_FIXTURE_LIBRARY_ID = '00000000-0000-4000-8000-000000000633'` (UUID encoding issue #)
- `IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
- `tryLoadVisualTestFixture(state: GamesEmptyKind | 'default'): UserLibraryEntry[]` — returns deterministic mock entries when `IS_VISUAL_TEST_BUILD`, else `null`.

**Validation**: `pnpm typecheck` green. No test file (fixture is exercised via Playwright in Commit 5).

### 3.3 Step 1.3 — i18n keys (~30 × 2 locales)

Edit `apps/web/src/locales/it.json` + `apps/web/src/locales/en.json` adding `pages.games.library.*` namespace (per spec §3.5 schema):
- `hero.title`, `hero.subtitle`, `hero.cta.add`, `hero.stats.owned/wishlist/totalEntries/kbDocs`
- `filters.search.placeholder`, `filters.status.all/owned/wishlist/played`, `filters.sort.label/lastPlayed/rating/title/year`, `filters.view.grid/list`
- `empty.library.{title,subtitle,cta}`, `empty.filteredEmpty.{title,subtitle,cta}`, `empty.error.{title,subtitle,cta}`

**Validation**:
```bash
# Both locales must have identical key sets
node -e "
const it = require('./apps/web/src/locales/it.json');
const en = require('./apps/web/src/locales/en.json');
const flatten = (o, p='') => Object.entries(o).flatMap(([k,v]) =>
  typeof v === 'object' ? flatten(v, p?\`\${p}.\${k}\`:k) : [\`\${p}.\${k}\`]);
const itKeys = flatten(it).filter(k => k.includes('pages.games.library'));
const enKeys = flatten(en).filter(k => k.includes('pages.games.library'));
const diff = itKeys.filter(k => !enKeys.includes(k)).concat(enKeys.filter(k => !itKeys.includes(k)));
console.log('IT keys:', itKeys.length, 'EN keys:', enKeys.length, 'Diff:', diff);
"
# Expected: ~30 / ~30, Diff: []
```

### 3.4 Step 1.4 — Cookie-consent helper (if missing per P-3)

If `apps/web/e2e/_helpers/seedCookieConsent.ts` doesn't exist, scaffold per spec §4.1:
```ts
import type { Page } from '@playwright/test';
export const seedCookieConsent = (page: Page) =>
  page.addInitScript(() => {
    localStorage.setItem('meepleai-cookie-consent', JSON.stringify({
      version: '1.0', essential: true, analytics: true, functional: true, timestamp: Date.now(),
    }));
  });
```

**Skip if exists**: prior session may have created it during A.4. Do NOT duplicate.

### 3.5 Commit 1 close

```bash
git add apps/web/src/lib/games/ apps/web/src/locales/ \
        apps/web/e2e/_helpers/seedCookieConsent.ts
git diff --staged --stat   # review file list, expect ~6 files
pnpm typecheck && pnpm lint --filter=apps/web   # green
pnpm test --run apps/web/src/lib/games   # 15/15 green
git commit -m "$(cat <<'EOF'
feat(games): foundation helpers + i18n keys for /games library v2 (#633)

- lib/games/library-filters.ts: pure helpers (filterByStatus, sortLibraryEntries, deriveStats)
- lib/games/visual-test-fixture.ts: sentinel-pattern fixture (NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED gated)
- locales/{it,en}.json: ~30 keys per locale under pages.games.library
- e2e/_helpers/seedCookieConsent.ts: cookie banner suppression helper (A.4 lesson learned)

Backend verification (spec §3.4): Caso B confirmed — UserLibraryEntry lacks playCount/lastPlayedAt.
"Giocati" filter will be HIDDEN. Child issue trackable as LT-2 follow-up.

Refs #633 #580 #578
EOF
)"
```

**Rollback**: `git reset --hard HEAD~1` — pure additive commit, safe to discard.

---

## 4. Commit 2 — Component family TDD (4 v2 components)

**Goal**: ship 4 v2 components in `apps/web/src/components/v2/games/` with full unit-test coverage. Each component is an island — no cross-coupling. Drawer is SKIPPED (BLOCKER #1 deferred).

### 4.1 Step 2.1 — `GamesHero` (TDD red→green→refactor)

**Red** — `apps/web/src/components/v2/games/__tests__/GamesHero.test.tsx` (~5 tests):
- Renders title from i18n key
- Renders 4 stat tiles with values from props
- Falls back to 0 when stat missing
- Compact mode collapses subtitle (mobile)
- CTA "Aggiungi gioco" calls `onAddGame` callback

```bash
pnpm test --run apps/web/src/components/v2/games/__tests__/GamesHero.test.tsx
# Expected: 5 fails (stub returns null)
```

**Green** — replace stub `GamesHero.tsx` with implementation:
- Use existing `useTranslation()` hook (i18n)
- Use `tw-merge` + `cva` for variants if patterns established (mirror sibling components)
- Props: `{ stats, compact?, onAddGame? }`
- Markup: heading + subtitle + 4 stat tiles + CTA

**Refactor**: extract `StatTile` if duplication >2; keep public API stable.

### 4.2 Step 2.2 — `GamesFiltersInline` (most complex — keyboard nav)

**Red** — `apps/web/src/components/v2/games/__tests__/GamesFiltersInline.test.tsx` (~15 tests):
- Search input controlled (value/onChange)
- Search debounce 300ms trailing-edge (`vi.useFakeTimers()` — type "x" "xy" "xyz" within 100ms → callback fires once with "xyz" after 300ms)
- Status tablist renders 3 options (Caso B: all/owned/wishlist) OR 4 (Caso A)
- Status tablist ArrowRight wraps last → first
- Status tablist ArrowLeft wraps first → last
- Status tablist Home jumps to first
- Status tablist End jumps to last
- Status tablist roving tabindex (active = 0, others = -1)
- Status tablist activation: focus = onChange same tick (mirror Wave A.6 pattern)
- Sort dropdown renders 4 options
- Sort onChange callback fires
- View toggle (grid/list) renders aria-pressed correctly
- View toggle hidden via `compact` prop (mobile)
- Ignored keys (ArrowUp, char) no-op
- All labels from i18n

**Green** — implement `GamesFiltersInline.tsx`:
- Reuse `useTablistKeyboardNav<GamesStatusKey>({ orderedKeys, onChange, orientation: 'horizontal' })` from PR #623
- Reuse `useDebouncedCallback` if present, else inline `useEffect` + `setTimeout`
- Markup: search input + tablist (status) + select (sort) + toggle group (view, hidden when `compact`)

```bash
pnpm test --run apps/web/src/components/v2/games/__tests__/GamesFiltersInline.test.tsx
# Expected: 15/15 green
```

**Refactor**: factor `StatusTabButton` if needed.

### 4.3 Step 2.3 — `GamesResultsGrid` (MeepleCard reuse — AC-3)

**Red** — `apps/web/src/components/v2/games/__tests__/GamesResultsGrid.test.tsx` (~8 tests):
- Maps `entries[]` → `MeepleCard` props with correct mapping (entity='game', variant='grid', title=gameTitle, subtitle=gamePublisher, imageUrl=gameImageUrl, rating=averageRating, ratingMax=10)
- View `grid` renders MeepleCard with variant='grid'
- View `list` renders MeepleCard with variant='list' (Caso A) OR fallback grid (Caso B per R9 mitigation)
- `compact={true}` forces grid (mobile mandate)
- Empty entries renders nothing (parent handles EmptyState)
- Click card invokes `onSelect(entry.gameId)` callback
- Renders correct count of cards
- Memoized — no re-render when props unchanged

**Green** — implement `GamesResultsGrid.tsx`:
- Import `MeepleCard` from `@/components/ui/data-display/meeple-card`
- ZERO fork (AC-3 mandate)
- Map per spec §3.2 prop table

### 4.4 Step 2.4 — `GamesEmptyState`

**Red** — `apps/web/src/components/v2/games/__tests__/GamesEmptyState.test.tsx` (~8 tests):
- `kind='empty'` renders library-empty title + subtitle + "Aggiungi gioco" CTA
- `kind='filtered-empty'` renders no-results title + "Azzera filtri" CTA → invokes `onClearFilters`
- `kind='error'` renders error title + "Riprova" CTA → invokes `onRetry`
- `kind='loading'` renders 6 skeleton placeholders (mobile: 4)
- Missing required callback (e.g. filtered-empty without `onClearFilters`) throws in dev, silent in prod
- All copy from i18n
- aria-live="polite" for kind transitions

**Green** — implement `GamesEmptyState.tsx`: discriminated union switch + reuse v1 `Skeleton`.

### 4.5 Step 2.5 — Barrel + AdvancedFiltersDrawer untouched

```ts
// apps/web/src/components/v2/games/index.ts
export { GamesHero } from './GamesHero';
export { GamesFiltersInline } from './GamesFiltersInline';
export { GamesResultsGrid } from './GamesResultsGrid';
export { GamesEmptyState } from './GamesEmptyState';
// AdvancedFiltersDrawer NOT exported — stub remains, drawer deferred (BLOCKER #1)
```

### 4.6 Commit 2 close

```bash
git add apps/web/src/components/v2/games/
pnpm test --run apps/web/src/components/v2/games   # ~36/36 green
pnpm typecheck && pnpm lint --filter=apps/web   # green
git commit -m "$(cat <<'EOF'
feat(games): GamesHero/FiltersInline/ResultsGrid/EmptyState v2 components (#633)

- GamesHero: 4 stat tiles + CTA, compact mode for mobile
- GamesFiltersInline: search (300ms debounce) + status tablist (useTablistKeyboardNav, horizontal) + sort dropdown + view toggle (desktop-only)
- GamesResultsGrid: MeepleCard v1 reuse (entity='game', variant='grid'|'list'), zero fork (AC-3)
- GamesEmptyState: discriminated kind FSM (empty|filtered-empty|error|loading)
- AdvancedFiltersDrawer: stub untouched (BLOCKER #1 deferred per spec §6.1)

Refs #633 #580
EOF
)"
```

**Rollback**: `git reset --hard HEAD~1` — components are isolated, no integration yet.

---

## 5. Commit 3 — Page integration: `GamesLibraryView` orchestrator

**Goal**: wire the 4 components into the orchestrator + edit existing `page.tsx` to mount it on `?tab=library`.

### 5.1 Step 3.1 — `GamesLibraryView` (TDD)

**Red** — `apps/web/src/app/(authenticated)/games/_components/__tests__/GamesLibraryView.test.tsx` (~10 tests):
- FSM: `useLibrary` returns `{ data: undefined, isLoading: true }` → render `kind='loading'`
- FSM: `useLibrary` returns `{ error: ... }` → render `kind='error'`
- FSM: `data.items.length === 0` → render `kind='empty'`
- FSM: filters yield empty result → render `kind='filtered-empty'`
- FSM: default state renders `GamesHero + GamesFiltersInline + GamesResultsGrid`
- State override `?state=loading` (NODE_ENV !== 'production') bypasses real hook
- State override guarded in production (`process.env.NODE_ENV === 'production'` → ignored)
- `clearFilters` resets `query=''` + `status='all'` (sort/view preserved)
- Stats derived from `data.items` via `deriveStats`
- View toggle persists to URL via `useRouter` shallow update (optional — out-of-scope if costly)

**Green** — implement `_components/GamesLibraryView.tsx`:
- `'use client'`
- Compose: stats from `deriveStats` → `GamesHero`; filters state via `useState` → `GamesFiltersInline`; filtered/sorted entries via `useMemo` → `GamesResultsGrid` or `GamesEmptyState`
- Override hatch via `useSearchParams()` + `tryLoadVisualTestFixture`

### 5.2 Step 3.2 — Wire `page.tsx`

Edit `apps/web/src/app/(authenticated)/games/page.tsx`:
- Replace existing tab `library` body block with `<GamesLibraryView />`
- Keep `?tab=` routing untouched
- Catalog + KB tabs unchanged

```bash
pnpm test --run apps/web/src/app/(authenticated)/games   # ~10/10 green
pnpm typecheck && pnpm lint --filter=apps/web   # green
```

### 5.3 Step 3.3 — Manual smoke (AC-13 regression check)

```bash
pnpm dev &
sleep 8
# Browser checks (manual, document in commit message):
# 1. GET /games?tab=library → v2 layout renders
# 2. GET /games?tab=catalog → v1 layout UNCHANGED (regression check)
# 3. GET /games?tab=kb → v1 layout UNCHANGED (regression check)
# 4. Type in search → debounced filter
# 5. Click status tab → filter applies
# 6. Tab keyboard nav: Tab into tablist → ArrowRight wraps
# 7. Switch locale (?locale=en) → labels rerender in EN
kill %1
```

### 5.4 Commit 3 close

```bash
git add apps/web/src/app/(authenticated)/games/
git commit -m "$(cat <<'EOF'
feat(games): GamesLibraryView orchestrator + page integration (#633)

- _components/GamesLibraryView.tsx: 5-state FSM (default/loading/empty/filtered-empty/error)
- page.tsx edit: tab=library body wired to <GamesLibraryView>; catalog/kb tabs untouched (AC-13)
- ?state= URL override gated NODE_ENV !== 'production' (mirror A.2/A.3b/A.4 pattern)

Manual smoke verified: catalog + kb tabs unchanged.

Refs #633 #580
EOF
)"
```

**Rollback**: `git reset --hard HEAD~1` — page.tsx revert restores v1 library tab.

---

## 6. Commit 4 — E2E specs (visual + state + a11y) — NO baselines yet

**Goal**: write Playwright specs that EXPECT baselines but don't ship them. Visual specs will fail on first CI run (expected — bootstrap in Commit 5).

### 6.1 Step 4.1 — `visual-migrated/sp4-games-index.spec.ts`

```ts
// 1 desktop + 1 mobile = 2 PNG
test.describe('sp4-games-index visual', () => {
  test.beforeEach(async ({ page }) => { await seedCookieConsent(page); });
  test('desktop default', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/games?tab=library');
    await expect(page).toHaveScreenshot('games-library-desktop.png', { fullPage: true });
  });
  test('mobile default', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/games?tab=library');
    await expect(page).toHaveScreenshot('games-library-mobile.png', { fullPage: true });
  });
});
```

### 6.2 Step 4.2 — `v2-states/games-library.spec.ts`

```ts
// 4 states × 2 viewports = 8 PNG
const STATES = ['default', 'loading', 'empty', 'filtered-empty'] as const;
const VIEWPORTS = [['desktop', 1440, 900], ['mobile', 375, 812]] as const;
for (const state of STATES) {
  for (const [vp, w, h] of VIEWPORTS) {
    test(`${state} ${vp}`, async ({ page }) => {
      await seedCookieConsent(page);
      await page.setViewportSize({ width: w, height: h });
      await page.goto(`/games?tab=library&state=${state}`);
      await expect(page).toHaveScreenshot(`games-library-${state}-${vp}.png`, { fullPage: true });
    });
  }
}
```

### 6.3 Step 4.3 — `a11y/games-library.spec.ts`

```ts
test('default state passes axe-core WCAG 2.1 AA', async ({ page }) => {
  await seedCookieConsent(page);
  await page.goto('/games?tab=library');
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  expect(results.violations).toEqual([]);
});
test('filtered-empty state passes axe-core', async ({ page }) => { /* ... */ });
test('prefers-reduced-motion disables card hover transition', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/games?tab=library');
  const card = page.locator('[data-slot="meeple-card"]').first();
  await card.hover();
  const transition = await card.evaluate(el => getComputedStyle(el).transition);
  expect(transition).toMatch(/none|0s/);
});
```

### 6.4 Commit 4 close

```bash
# Run a11y locally (visual specs deferred to CI bootstrap)
pnpm test:e2e apps/web/e2e/a11y/games-library.spec.ts   # 3 green
git add apps/web/e2e/{visual-migrated,v2-states,a11y}/
git commit -m "$(cat <<'EOF'
test(games): visual-migrated + v2-states + a11y E2E for /games library (#633)

- visual-migrated/sp4-games-index.spec.ts: 2 PNG (desktop + mobile default)
- v2-states/games-library.spec.ts: 8 PNG (4 states × 2 viewports)
- a11y/games-library.spec.ts: axe-core WCAG 2.1 AA + prefers-reduced-motion

Visual specs will fail on first CI run (no baselines yet — bootstrap in next commit).
A11y specs MUST PASS locally and on CI.

Refs #633 #580
EOF
)"
```

---

## 7. Commit 5 — Bootstrap canonical baselines (CI roundtrip)

**Goal**: generate 10 PNG via `--update-snapshots` workflow on CI Linux runner, copy back, commit.

### 7.1 Step 5.1 — Push branch + trigger workflow

```bash
git push -u origin feature/issue-633-games-fe-v2

gh workflow run visual-regression-migrated.yml \
  --ref feature/issue-633-games-fe-v2 \
  -f update_snapshots=true

# Get run ID
RUN_ID=$(gh run list --workflow=visual-regression-migrated.yml --branch=feature/issue-633-games-fe-v2 --limit=1 --json databaseId --jq '.[0].databaseId')
echo "Run: $RUN_ID"
```

### 7.2 Step 5.2 — Monitor + download artifact

```bash
# Use Monitor (terminal-state filter) instead of polling sleep
# Pattern from Wave A.4 / A.5b / A.6 sessions
gh run watch $RUN_ID --exit-status   # blocks until terminal state
# OR via Monitor tool with bash event filter

# On success:
gh run download $RUN_ID -n visual-migrated-baselines-$RUN_ID -D ./tmp-baselines
```

**Iteration handling** (per A.4 lesson — strict-mode locator + cookie banner flake required 3 iter):
- If diff PNG shows cookie banner artifact → verify `seedCookieConsent` was applied (Commit 1.4 + spec injection per spec)
- If strict-mode locator violation → scope to active tabpanel via `data-kind` or unique attr
- If layout shift → check stability between consecutive captures, add `await page.waitForLoadState('networkidle')` or specific element wait

### 7.3 Step 5.3 — Filtered copy (NOT clobbering other routes)

```bash
mkdir -p apps/web/e2e/visual-migrated/sp4-games-index.spec.ts-snapshots/
mkdir -p apps/web/e2e/v2-states/games-library.spec.ts-snapshots/

cp tmp-baselines/visual-migrated/sp4-games-index.spec.ts-snapshots/*.png \
   apps/web/e2e/visual-migrated/sp4-games-index.spec.ts-snapshots/
cp tmp-baselines/v2-states/games-library.spec.ts-snapshots/*.png \
   apps/web/e2e/v2-states/games-library.spec.ts-snapshots/

ls apps/web/e2e/visual-migrated/sp4-games-index.spec.ts-snapshots/   # expect 2 PNG
ls apps/web/e2e/v2-states/games-library.spec.ts-snapshots/   # expect 8 PNG

rm -rf tmp-baselines
```

### 7.4 Step 5.4 — Update v2 migration matrix

Edit `docs/frontend/v2-migration-matrix.md`:
- Find rows for `GamesHero`, `GamesFiltersInline`, `GamesResultsGrid`, `GamesEmptyState`
- Update `Status: pending → done`
- Update `PR: TBD → #<number>` (after PR opens — see commit 5 reflow OR amend later)
- Add note to `AdvancedFiltersDrawer` row: "Deferred B.1 — see spec §6.1 (BLOCKER #1 Option B)"

### 7.5 Step 5.5 — Commit + re-run CI

```bash
git add apps/web/e2e/visual-migrated/ apps/web/e2e/v2-states/ docs/frontend/v2-migration-matrix.md
git commit -m "$(cat <<'EOF'
chore(visual): bootstrap canonical baselines for /games library v2 (#633)

- visual-migrated: 2 PNG (desktop + mobile sp4-games-index)
- v2-states: 8 PNG (default/loading/empty/filtered-empty × desktop/mobile)
- v2-migration-matrix.md: GamesHero/Filters/Results/EmptyState rows → done; AdvancedFiltersDrawer deferred note

Bootstrap run: <RUN_ID>
Refs #633 #580
EOF
)"
git push

# Verify all gates green
gh run watch $(gh run list --branch=feature/issue-633-games-fe-v2 --limit=1 --json databaseId --jq '.[0].databaseId')
```

**Pass criteria** (all gates):
- ✅ `Migrated Routes Baseline`
- ✅ `Frontend Build & Test`
- ✅ `frontend-a11y`
- ✅ `frontend-bundle-size` (Δ ≤ +50 KB)
- ✅ `E2E Critical Paths`
- ✅ `GitGuardian`
- ✅ `Detect Changes`
- ⚠️ `codecov/patch` may flake (non-blocking, Wave A pattern)

---

## 8. PR creation + merge

### 8.1 Step 8.1 — Open PR

```bash
gh pr create \
  --base main-dev \
  --head feature/issue-633-games-fe-v2 \
  --title "feat(games): migrate /games library tab to v2 design (#633)" \
  --body "$(cat <<'EOF'
## Summary

Wave B.1 — brownfield migration di `/games` tab `library` al pattern v2 `sp4-games-index`. Primo PR di Wave B (post Path A closeout #602/#630/#631/#632).

**Scope**:
- 4 v2 components in `components/v2/games/` (Hero, FiltersInline, ResultsGrid, EmptyState)
- `GamesLibraryView` orchestrator in `app/(authenticated)/games/_components/`
- `page.tsx` edit: tab `library` body → `<GamesLibraryView>`. Catalog/KB tabs UNCHANGED (AC-13).
- 5-state FSM (default/loading/empty/filtered-empty/error)
- WAI-ARIA tablist con `useTablistKeyboardNav` (PR #623 + orientation #626)
- 10 PNG canonical baselines (2 visual-migrated + 8 v2-states)
- axe-core a11y E2E + prefers-reduced-motion

**Out of scope** (per spec §2 + §7):
- ❌ `AdvancedFiltersDrawer` (BLOCKER #1 deferred — Option B)
- ❌ Tab Catalogo / KB migration (B.4+)
- ❌ Pagination UI
- ❌ Backend changes
- ❌ Server Component conversion
- ❌ MeepleCard extension

**Backend verification (spec §3.4 / BLOCKER #3)**: Caso B confermato — `UserLibraryEntry` non espone `playCount`/`lastPlayedAt`. Filter "Giocati" HIDDEN. Child issue tracking come LT-2 follow-up.

**Bundle delta**: <fill from CI report> KB First Load JS (target ≤+35, hard limit +50).

**Spec**: `docs/superpowers/specs/2026-04-29-v2-migration-wave-b-1-games.md`
**Plan**: `docs/superpowers/plans/2026-04-29-wave-b-1-games-implementation-plan.md`

## Test summary

- **Unit**: ~60 Vitest tests (15 helpers + ~36 components + ~10 orchestrator) — all green
- **Visual**: 10 PNG canonical baselines via CI bootstrap (`--update-snapshots` flow)
- **A11y**: 3 axe-core scans (default + filtered-empty + reduced-motion)
- **Manual smoke**: catalog + kb tabs unchanged (AC-13)

## Test plan

- [x] `pnpm test --run apps/web/src/lib/games` green
- [x] `pnpm test --run apps/web/src/components/v2/games` green
- [x] `pnpm test --run apps/web/src/app/(authenticated)/games` green
- [x] `pnpm typecheck` green
- [x] `pnpm lint --filter=apps/web` green
- [x] `frontend-a11y` CI gate green
- [x] `frontend-bundle-size` CI gate green
- [x] `Migrated Routes Baseline` CI gate green
- [x] Manual smoke: `/games?tab=catalog` + `/games?tab=kb` invariati

Closes #633
Refs #580 (Wave B umbrella) #578 (Phase 1 umbrella)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

### 8.2 Step 8.2 — Update matrix PR column

After PR opens, amend the matrix update commit OR ship a tiny followup commit setting `PR: #<n>`. Preferred: amend chore commit before squash.

```bash
PR_NUM=$(gh pr view --json number --jq .number)
sed -i "s/PR: TBD/PR: #$PR_NUM/g" docs/frontend/v2-migration-matrix.md   # adjust pattern as needed
git add docs/frontend/v2-migration-matrix.md
git commit --amend --no-edit
git push --force-with-lease
```

### 8.3 Step 8.3 — Monitor CI + merge

```bash
# Use Monitor pattern (terminal-state filter) — per Wave A precedent
# poll: gh pr view $PR_NUM --json mergeStateStatus,statusCheckRollup --jq '...'
# break on: mergeStateStatus = CLEAN || statusCheckRollup contains terminal FAILURE

# When all green except codecov/patch flake:
gh pr merge $PR_NUM --squash --admin --delete-branch
# --admin: bypass codecov/patch oscillatory flake (Wave A pattern, accepted)
# --delete-branch: cleanup
```

### 8.4 Step 8.4 — Post-merge cleanup

```bash
git checkout main-dev
git pull --ff-only origin main-dev   # auto fast-forward (squash commit)
git remote prune origin
git branch -D feature/issue-633-games-fe-v2 2>/dev/null || true
git status   # clean
```

### 8.5 Step 8.5 — Update issue + memory + LT follow-ups

- Verify `#633` auto-closed by `Closes #633` body
- Update memory file (`session_2026-04-29_*.md`) with:
  - PR # + merge SHA
  - Caso B confirmation (Giocati filter hidden)
  - Bundle delta actual
  - Iter count for visual baseline bootstrap
  - Patterns emerged worth saving

**LT-2 follow-up issue creation** (only if Caso B was confirmed in P-2):

```bash
# Open child issue under #580 Wave B umbrella for backend extension
gh issue create \
  --title "feat(library): expose playCount + lastPlayedAt on UserLibraryEntry (LT-2)" \
  --body "$(cat <<'EOF'
## Context

LT-2 follow-up from Wave B.1 (#633). Backend currently does not expose \`playCount\` / \`lastPlayedAt\` on \`UserLibraryEntry\`. The "Giocati" filter and "Ultima partita" sort key are HIDDEN in /games library v2 as a result (Caso B per spec §3.4).

## Goal

Extend \`UserLibraryEntry\` projection to include:
- \`playCount: number\` (derived from session-tracking aggregate)
- \`lastPlayedAt: ISO8601 string | null\` (most recent session timestamp)

## Acceptance

- [ ] Schema updated in \`library.schemas.ts\`
- [ ] Backend handler returns fields populated from session-tracking BC
- [ ] /games library v2 unhides "Giocati" status filter (Caso A activation)
- [ ] /games library v2 enables "Ultima partita" sort with real \`lastPlayedAt\` data

Refs #633 #580 (Wave B umbrella)
EOF
)" \
  --label "wave-b" --label "backend" --label "follow-up"
```

**LT-3 reminder**: NO new issue needed (out-of-scope per spec §2). If MeepleCard v2 variant becomes a real need (e.g. cover gradient mockup-accurate from sp4), open spec dedicato (`docs/superpowers/specs/<date>-meeple-card-v2-extension.md`) — do NOT bolt onto B.1 PR retroactively.

**LT-1 reminder**: Server Component conversion is Wave B.4+ candidate. No standalone issue yet; will be triaged when B.4 scope opens.

---

## 9. Quality gates summary (acceptance for plan completion)

| Gate | Source | Pass criteria |
|------|--------|--------------|
| Branch hygiene | P-1 | clean tree on main-dev fast-forwarded |
| Backend contract | P-2 / spec §3.4 | Caso A or B documented in commit 1 |
| Reuse audit | P-3 / spec §3.7 | MeepleCard list variant + tablist hook + axe helpers verified |
| Pre-existing baseline | P-4 | typecheck/lint baseline noted |
| Pure helpers | Commit 1 | 15/15 unit green |
| Components | Commit 2 | ~36/36 unit green |
| Orchestrator | Commit 3 | ~10/10 unit + manual smoke (AC-13) |
| E2E specs | Commit 4 | 3 a11y green locally; visual specs will fail until bootstrap |
| Baselines | Commit 5 | 10 PNG ship; all CI gates green |
| Bundle | AC-10 | Δ ≤ +50 KB First Load JS for `/games` |
| A11y | AC-9 | 0 axe violations WCAG 2.1 AA on default + filtered-empty |
| Reduced motion | AC-8 | hover transition disabled when `reducedMotion: reduce` |
| i18n | AC-11 | grep IT-only labels in `components/v2/games/*.tsx` returns 0 |
| Card reuse | AC-3 | `MeepleCard` import present; no fork |
| Regression | AC-13 | catalog + kb tabs visually unchanged |
| Squash merge | §8.3 | --admin --delete-branch on CLEAN/UNSTABLE-codecov-only |

---

## 10. Risk register checkpoints (mirror spec §6)

| Risk | Mitigation checkpoint | Step |
|------|----------------------|------|
| R1 enum drift | `GameStateTypeWithFallbackSchema` already defensive | n/a (existing) |
| R2 playCount missing | Caso B default | §1.P-2 + §3.5 commit msg |
| R3 bundle overflow | code-split EmptyState `next/dynamic` | §7.5 if gate fails |
| R4 cookie banner flake | `seedCookieConsent` helper | §3.4 + §6.1 + §6.2 |
| R5 strict-mode locator | scope to `data-kind` | §7.2 iter handling |
| R6 useGames signature change | grep `useGames`/`useLibrary` exports | §1.P-3 pre-flight |
| R7 i18n hot-reload stale | `pnpm dev` restart | §5.3 manual smoke |
| R8 catalog/kb regression | AC-13 manual smoke | §5.3 steps 2-3 |
| R9 MeepleCard list variant absent | grep audit | §1.P-3 (fallback grid-only) |
| LT-3 guard (no MeepleCard fork) | meeple-card.tsx baseline SHA recorded; assert unchanged at PR | §1.P-3 + §8.3 verify |

---

## 11. Effort allocation

| Step | Allocated | Notes |
|------|-----------|-------|
| Pre-flight (P-1..4) | 0.25 day | curl + grep audits |
| Commit 1 (foundation) | 0.5 day | helpers + i18n + fixture + helper scaffold |
| Commit 2 (components) | 2.0 days | 4 component × 0.5d (TDD discipline) |
| Commit 3 (orchestrator) | 0.5 day | view + page edit + manual smoke |
| Commit 4 (E2E specs) | 0.5 day | visual + state + a11y |
| Commit 5 (bootstrap) | 0.5 day | CI roundtrip + matrix update |
| PR + merge | 0.5-1 day | review buffer |
| **Total** | **5-7 days** | |

---

## 12. Plan-vs-spec traceability

| Spec section | Plan section |
|--------------|-------------|
| §1 Goals | §3.5 (commit 1 message), §4.6 (commit 2), §5.4 (commit 3) |
| §2 Non-goals | §8.1 PR body explicit list |
| §3.1 File map | §3.1, §4.1-4.5, §5.1-5.2 |
| §3.2 Component API | §4.1-4.4 (TDD test design) |
| §3.4 Data contract | §1.P-2 verification, §3.5 commit message |
| §3.5 i18n keys | §3.3 |
| §3.7 Bundle budget | §7.5 CI gate, §9 quality gates |
| §4.1 Visual baselines | §6.1, §6.2, §7 |
| §4.2 Unit tests | §3.1, §4.1-4.4, §5.1 |
| §4.3 A11y | §6.3 |
| §5 AC (13) | §9 quality gates table |
| §6 Risks (9) | §10 |
| §8 Sequencing (5 commits) | §3, §4, §5, §6, §7 |
| §9 Bootstrap workflow | §7.1-7.5 |
| §10 Panel review (BLOCKER+ST+LT) | §1.P-2, §4.1-4.4, §6.3, §9 |

---

**Plan ready**. Proceed to plan review (§4 of user directive: "esegui review piano"), then execute.
