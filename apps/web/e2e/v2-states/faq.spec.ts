/**
 * V2 State Coverage — /faq route (Issue #583, Wave A.1).
 *
 * Captures 4 visual states: default, empty, loading, error.
 * Uses test-only `?loading=1` / `?error=1` query params (guarded by
 * NODE_ENV !== 'production' in `app/(public)/faq/page.tsx`) and
 * URL hash `#q=zzznonexistent` for the empty state.
 *
 * Snapshots written to `apps/web/e2e/v2-states/faq.spec.ts-snapshots/`.
 * Run `pnpm test:v2-states -- --update-snapshots` once for first generation.
 */
import { test, expect, type Page } from '@playwright/test';

async function waitForFaqReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-testid="faq-hero"]', { timeout: 30_000 });
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

test.describe('FAQ — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('default state', async ({ page }) => {
    await page.goto('/faq', { waitUntil: 'networkidle' });
    await waitForFaqReady(page);
    await expect(page).toHaveScreenshot('faq-default.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('empty state (search no match)', async ({ page }) => {
    await page.goto('/faq#q=zzznonexistent', { waitUntil: 'networkidle' });
    await waitForFaqReady(page);
    // Wait useFaqHashQuery debounce (250ms) + RAF.
    await page.waitForTimeout(400);
    await expect(page).toHaveScreenshot('faq-empty.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('loading state', async ({ page }) => {
    await page.goto('/faq?loading=1', { waitUntil: 'networkidle' });
    await waitForFaqReady(page);
    await expect(page).toHaveScreenshot('faq-loading.png', {
      fullPage: true,
      // Skeleton shimmer is intentionally animated — mask the entire skeleton zone.
      mask: [page.locator('.mai-shimmer'), page.locator('[data-dynamic]')],
    });
  });

  test('error state', async ({ page }) => {
    await page.goto('/faq?error=1', { waitUntil: 'networkidle' });
    await waitForFaqReady(page);
    await expect(page).toHaveScreenshot('faq-error.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
