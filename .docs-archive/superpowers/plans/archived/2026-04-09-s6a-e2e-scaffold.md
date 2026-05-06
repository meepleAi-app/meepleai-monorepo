# S6a — Playwright E2E Scaffold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Land the E2E test scaffolding (CI workflow + happy path skeleton) early in the epic so each subsequent sub-feature PR can incrementally "unskip" its own steps.

**Architecture:** Single new Playwright spec file in `e2e/flows/` with all sub-feature-dependent steps in `test.skip` blocks. A dedicated GitHub Actions workflow runs only on PRs into `epic/library-to-game`, uploads screenshots as artifacts.

**Tech Stack:** Playwright (existing config: `playwright.config.ts` with Pixel 5, iPhone 13, desktop-chrome projects), GitHub Actions.

**Reference spec:** `docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md` §4.6
**Branch:** `feature/s6a-e2e-scaffold` (from `epic/library-to-game`)

---

## File Structure

**New files:**
- `apps/web/e2e/flows/library-to-game-happy-path.spec.ts` — happy path skeleton with S1 verified + S2-S5 skipped
- `.github/workflows/e2e-library-to-game.yml` — dedicated CI workflow

**Modified files:** none

**Total estimate:** 2 new files, ~180 lines. ~30 minutes.

---

## Task 1 — Happy path skeleton spec

**Files:**
- Create: `apps/web/e2e/flows/library-to-game-happy-path.spec.ts`

- [ ] **Step 1.1: Write the skeleton spec**

  The spec iterates over `desktop-chrome`, `mobile-chrome`, `mobile-safari` viewports. Only S1 steps (login + toggle) are active — all other steps are wrapped in `test.skip` that each future sub-feature will unskip.

  Write `apps/web/e2e/flows/library-to-game-happy-path.spec.ts`:

  ```typescript
  /**
   * Library-to-Game happy path E2E scaffold.
   *
   * Exercises the full user journey from admin dashboard to game page across
   * multiple viewports. Sub-feature-specific steps are initially wrapped in
   * `test.skip` and unskipped by each sub-feature PR as it lands:
   *   S1: admin toggle — ENABLED (landed)
   *   S2: catalog KB filter — SKIPPED (pending)
   *   S3: MeepleCard + direct add — SKIPPED (pending)
   *   S4: game desktop split view — SKIPPED (pending)
   *   S5: game mobile drawer — SKIPPED (pending)
   *
   * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.6
   */
  import { test, expect } from '@playwright/test';

  import { loginAsAdmin } from '../fixtures/auth';

  // Screenshot base directory — will be uploaded as CI artifact
  const SCREENSHOT_DIR = 'test-results/library-to-game-happy-path';

  test.describe('Library-to-Game happy path', () => {
    test('admin → user → library → filter → add → game page', async ({
      page,
      context,
    }, testInfo) => {
      // The full happy path stays skipped until S2 lands. Each sub-feature PR
      // moves this skip call down past its own section and unskips the steps
      // above. The S1-only smoke test below always runs and covers current scope.
      test.skip(true, 'Full happy path pending S2-S5 landing (scaffold only)');

      const viewport = testInfo.project.name;

      // Clear stale view mode cookie
      const existing = await context.cookies();
      const keep = existing.filter(c => c.name !== 'meepleai_view_mode');
      await context.clearCookies();
      if (keep.length > 0) await context.addCookies(keep);

      // ===== S1: admin login + view toggle =====
      await loginAsAdmin(page);
      await page.goto('/admin/overview');
      await page.screenshot({ path: `${SCREENSHOT_DIR}/${viewport}/01-admin-dashboard.png` });

      const toggle = page.getByTestId('view-mode-toggle');
      await expect(toggle).toBeVisible();
      await toggle.click();
      await page.waitForURL(url => !url.pathname.startsWith('/admin'), { timeout: 5000 });
      await page.screenshot({ path: `${SCREENSHOT_DIR}/${viewport}/02-user-home.png` });

      // ===== S2: catalog KB filter =====
      await page.goto('/library?tab=catalogo');
      await page.screenshot({ path: `${SCREENSHOT_DIR}/${viewport}/03-catalog.png` });

      const kbFilter = page.getByRole('button', { name: /solo giochi ai-ready/i });
      await kbFilter.click();
      await expect(page).toHaveURL(/hasKb=true/);
      await page.screenshot({ path: `${SCREENSHOT_DIR}/${viewport}/04-catalog-filtered.png` });

      // ===== S3: MeepleCard + direct add =====
      const firstAddButton = page.locator('[data-meeple-card]').first().getByLabel(/aggiungi alla libreria/i);
      await firstAddButton.click();
      await expect(page.getByText(/aggiunto alla libreria/i)).toBeVisible();
      await page.screenshot({ path: `${SCREENSHOT_DIR}/${viewport}/05-add-toast.png` });

      await expect(page).toHaveURL(/\/library\/games\/[\w-]+/, { timeout: 3000 });
      await page.screenshot({ path: `${SCREENSHOT_DIR}/${viewport}/06-game-page.png` });

      // ===== S4/S5: viewport-specific game page assertions =====
      const isMobile = viewport.startsWith('mobile');
      if (isMobile) {
        await expect(page.locator('[data-hand-stack]')).toBeVisible();
        await page.getByRole('button', { name: /dettagli/i }).click();
        await expect(page.locator('[role="dialog"][aria-modal="true"]')).toBeVisible();
        await page.screenshot({ path: `${SCREENSHOT_DIR}/${viewport}/07-mobile-drawer.png` });
      } else {
        await expect(page.locator('[role="tablist"]')).toBeVisible();
        await page.screenshot({ path: `${SCREENSHOT_DIR}/${viewport}/07-desktop-split.png` });
      }
    });

    test('cross-viewport S1 smoke (S1-only, always runs)', async ({ page }, testInfo) => {
      // S1-only variant that does NOT skip — runs on every viewport to verify
      // the view mode toggle works across all configured devices.
      const viewport = testInfo.project.name;

      await loginAsAdmin(page);
      await page.goto('/admin/overview');

      const toggle = page.getByTestId('view-mode-toggle');
      await expect(toggle).toBeVisible();
      await expect(toggle).toHaveAttribute('role', 'switch');

      await page.screenshot({
        path: `${SCREENSHOT_DIR}/${viewport}/s1-admin-baseline.png`,
      });

      await toggle.click();
      await page.waitForURL(url => !url.pathname.startsWith('/admin'), { timeout: 5000 });

      await page.screenshot({
        path: `${SCREENSHOT_DIR}/${viewport}/s1-user-after-toggle.png`,
      });
    });
  });
  ```

- [ ] **Step 1.2: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/e2e/flows/library-to-game-happy-path.spec.ts
  git commit -m "test(s6a): add library-to-game happy path E2E skeleton"
  ```

---

## Task 2 — GitHub Actions workflow

**Files:**
- Create: `.github/workflows/e2e-library-to-game.yml`

- [ ] **Step 2.1: Check existing E2E workflow patterns**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  ls .github/workflows/ | grep -i e2e
  ```
  Note any existing E2E workflow file names to avoid collision and reuse project conventions.

- [ ] **Step 2.2: Write the workflow file**

  Write `.github/workflows/e2e-library-to-game.yml`:

  ```yaml
  name: E2E — Library to Game Epic

  on:
    pull_request:
      branches:
        - epic/library-to-game
      paths:
        - 'apps/web/**'
        - '.github/workflows/e2e-library-to-game.yml'
    workflow_dispatch:

  concurrency:
    group: e2e-library-to-game-${{ github.ref }}
    cancel-in-progress: true

  jobs:
    e2e:
      name: Playwright (${{ matrix.project }})
      runs-on: ubuntu-latest
      timeout-minutes: 20

      strategy:
        fail-fast: false
        matrix:
          project:
            - desktop-chrome
            - mobile-chrome
            - mobile-safari

      steps:
        - uses: actions/checkout@v4

        - name: Setup Node
          uses: actions/setup-node@v4
          with:
            node-version: '20'

        - name: Setup pnpm
          uses: pnpm/action-setup@v4
          with:
            version: 9

        - name: Install dependencies
          working-directory: apps/web
          run: pnpm install --frozen-lockfile

        - name: Install Playwright browsers
          working-directory: apps/web
          run: pnpm exec playwright install --with-deps ${{ matrix.project == 'mobile-safari' && 'webkit' || 'chromium' }}

        - name: Run library-to-game E2E
          working-directory: apps/web
          run: pnpm exec playwright test e2e/flows/library-to-game-happy-path.spec.ts --project=${{ matrix.project }}
          env:
            CI: 'true'

        - name: Upload screenshots
          if: always()
          uses: actions/upload-artifact@v4
          with:
            name: library-to-game-screenshots-${{ matrix.project }}
            path: apps/web/test-results/library-to-game-happy-path/
            retention-days: 14
            if-no-files-found: warn

        - name: Upload Playwright HTML report
          if: always()
          uses: actions/upload-artifact@v4
          with:
            name: playwright-report-${{ matrix.project }}
            path: apps/web/playwright-report/
            retention-days: 14
            if-no-files-found: warn
  ```

- [ ] **Step 2.3: Lint the YAML**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  python -c "import yaml; yaml.safe_load(open('.github/workflows/e2e-library-to-game.yml'))" 2>&1 || echo "YAML parse issue"
  ```
  Expected: no output (valid YAML). If Python is unavailable, inspect visually for indentation.

- [ ] **Step 2.4: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add .github/workflows/e2e-library-to-game.yml
  git commit -m "ci(s6a): add library-to-game E2E workflow with screenshot artifacts"
  ```

---

## Task 3 — Push + PR

- [ ] **Step 3.1: Push**

  ```bash
  git push -u origin feature/s6a-e2e-scaffold
  ```

- [ ] **Step 3.2: Create PR into epic branch**

  ```bash
  gh pr create \
    --base epic/library-to-game \
    --head feature/s6a-e2e-scaffold \
    --title "test(s6a): Playwright E2E scaffold for library-to-game epic" \
    --body "..."
  ```

---

## Self-review checklist

- [x] Screenshot path includes viewport name → no filename collisions
- [x] `test.skip(true, 'S2 not yet landed')` causes the rest of the test body to be skipped
- [x] S1-only test always runs, S6a ships with green CI
- [x] Workflow triggers only on PRs targeting epic branch (not main-dev)
- [x] Matrix parallelizes 3 Playwright projects
- [x] Screenshot artifacts uploaded regardless of test outcome (`if: always()`)
- [x] Concurrency group cancels superseded runs
- [x] No placeholders, no TBDs
- [x] All file paths concrete

**Plan length:** 3 tasks, ~10 steps, ~30 minutes.
