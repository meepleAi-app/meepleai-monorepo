/**
 * V2 State Coverage — /shared-games route (Issue #596, Wave A.3b).
 *
 * Captures 5 grid surface states: default, loading, error, empty-search,
 * filtered-empty. Uses test-only `?state=...` query param (guarded by
 * `NODE_ENV !== 'production'` in `app/(public)/shared-games/page-client.tsx`).
 *
 * Snapshots written to `apps/web/e2e/v2-states/shared-games.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run 266963272 --ref <branch> -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

async function waitForSharedGamesReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-testid="shared-games-page"]', { timeout: 30_000 });

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

test.describe('Shared games — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('default state', async ({ page }) => {
    await page.goto('/shared-games', { waitUntil: 'networkidle' });
    await waitForSharedGamesReady(page);
    await expect(page).toHaveScreenshot('shared-games-default.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('loading state', async ({ page }) => {
    await page.goto('/shared-games?state=loading', { waitUntil: 'networkidle' });
    await waitForSharedGamesReady(page);
    await expect(page).toHaveScreenshot('shared-games-loading.png', {
      fullPage: true,
      // Skeleton shimmer pulse animations — mask to avoid flake.
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('error state', async ({ page }) => {
    await page.goto('/shared-games?state=error', { waitUntil: 'networkidle' });
    await waitForSharedGamesReady(page);
    await expect(page).toHaveScreenshot('shared-games-error.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('empty-search state', async ({ page }) => {
    await page.goto('/shared-games?state=empty-search', { waitUntil: 'networkidle' });
    await waitForSharedGamesReady(page);
    await expect(page).toHaveScreenshot('shared-games-empty-search.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('filtered-empty state', async ({ page }) => {
    await page.goto('/shared-games?state=filtered-empty', { waitUntil: 'networkidle' });
    await waitForSharedGamesReady(page);
    await expect(page).toHaveScreenshot('shared-games-filtered-empty.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
