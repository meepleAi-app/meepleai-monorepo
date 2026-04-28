/**
 * V2 State Coverage — /shared-games/[id] route (Issue #603, Wave A.4).
 *
 * Captures 4 surface states: default, loading, error, empty-tab.
 * Uses test-only `?state=...` query param (guarded by
 * `NODE_ENV !== 'production'` in
 * `app/(public)/shared-games/[id]/page-client.tsx`).
 *
 * The 5th state from typical 5-state surfaces — `not-found` — is handled
 * server-side via Next.js `notFound()`, so it lives in a separate
 * `not-found.tsx` and is out of scope for this client-state suite.
 *
 * Test ID strategy:
 *   - Imports `VISUAL_TEST_FIXTURE_ID` — the SSR fetch is short-circuited
 *     by `loadInitialData()` so visual tests don't depend on a live
 *     backend API. See `src/lib/shared-games/visual-test-fixture.ts`.
 *
 * Snapshots written to `apps/web/e2e/v2-states/shared-game-detail.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run 266963272 --ref <branch> -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { VISUAL_TEST_FIXTURE_ID } from '../../src/lib/shared-games/visual-test-fixture';

async function waitForDetailReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-testid="shared-game-detail-page"]', { timeout: 30_000 });

  await page.evaluate(async () => {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await (document as Document & { fonts: { ready: Promise<void> } }).fonts.ready;
    }
  });

  await page.evaluate(
    () =>
      new Promise<void>(resolve => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      })
  );
}

test.describe('Shared game detail — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('default state', async ({ page }) => {
    await page.goto(`/shared-games/${VISUAL_TEST_FIXTURE_ID}`, { waitUntil: 'networkidle' });
    await waitForDetailReady(page);
    await expect(page).toHaveScreenshot('shared-game-detail-default.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('loading state', async ({ page }) => {
    await page.goto(`/shared-games/${VISUAL_TEST_FIXTURE_ID}?state=loading`, {
      waitUntil: 'networkidle',
    });
    await waitForDetailReady(page);
    await expect(page).toHaveScreenshot('shared-game-detail-loading.png', {
      fullPage: true,
      animations: 'disabled',
      // Skeleton pulse animations — mask to avoid flake.
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('error state', async ({ page }) => {
    await page.goto(`/shared-games/${VISUAL_TEST_FIXTURE_ID}?state=error`, {
      waitUntil: 'networkidle',
    });
    await waitForDetailReady(page);
    await expect(page).toHaveScreenshot('shared-game-detail-error.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('empty-tab state (no toolkits/agents/kbs)', async ({ page }) => {
    await page.goto(`/shared-games/${VISUAL_TEST_FIXTURE_ID}?state=empty-tab`, {
      waitUntil: 'networkidle',
    });
    await waitForDetailReady(page);
    // EmptyState renders by default in toolkits tab when override active.
    // Click toolkits tab to make the empty state visible (default tab is
    // overview which always renders description).
    const toolkitsTab = page.locator('[role="tab"]').nth(1);
    await toolkitsTab.scrollIntoViewIfNeeded();
    await toolkitsTab.click();
    // Wait for activation contract before screenshot — avoids flake on mobile
    // where tab click can trigger scroll-into-view and aria-selected flip
    // separately. `animations: 'disabled'` below freezes the residual 200ms
    // CSS transition on the tab `color/background-color`, which otherwise
    // cycles pixels during Playwright's pre-mask stability checks.
    await expect(toolkitsTab).toHaveAttribute('aria-selected', 'true');
    // Wait for the tabpanel content (EmptyState) to actually mount + paint.
    // Without this, mobile screenshots flake because the React commit that
    // renders the new tabpanel content can complete *after* aria-selected
    // flips, causing layout shift between consecutive raw stability captures.
    //
    // Scope to `data-kind="no-toolkits"` (unique): when `?state=empty-tab` is
    // active, all three tabpanels (toolkits/agents/kbs) render an EmptyState,
    // so `[data-slot="shared-game-detail-empty-state"]` would match 3 elements
    // and trigger Playwright strict-mode violation. The toolkits EmptyState is
    // the only visible one (others are inside `hidden` tabpanels).
    await expect(page.locator('[data-kind="no-toolkits"]')).toBeVisible({
      timeout: 5000,
    });
    // Quad-RAF settle: 2 frames to commit DOM updates, 2 more to paint and
    // settle compositor. Single double-RAF was insufficient on mobile under
    // CI virtualization where a render cycle can span 2-3 frames.
    await page.evaluate(
      () =>
        new Promise<void>(resolve => {
          requestAnimationFrame(() =>
            requestAnimationFrame(() =>
              requestAnimationFrame(() => requestAnimationFrame(() => resolve()))
            )
          );
        })
    );
    await expect(page).toHaveScreenshot('shared-game-detail-empty-tab.png', {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
