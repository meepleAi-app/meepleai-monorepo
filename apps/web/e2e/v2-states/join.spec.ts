/**
 * V2 State Coverage — /join route (Issue #589, Wave A.2).
 *
 * Captures 5 FSM states: default, submitting, success, error, already-on-list.
 * Uses test-only `?state=...` query param (guarded by NODE_ENV !== 'production'
 * in `app/(public)/join/page-client.tsx`).
 *
 * Snapshots written to `apps/web/e2e/v2-states/join.spec.ts-snapshots/`.
 * Run `pnpm test:v2-states -- --update-snapshots` once for first generation.
 */
import { test, expect, type Page } from '@playwright/test';

async function waitForJoinReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-testid="join-form-card"]', { timeout: 30_000 });

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

test.describe('Join — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('default state', async ({ page }) => {
    await page.goto('/join', { waitUntil: 'networkidle' });
    await waitForJoinReady(page);
    await expect(page).toHaveScreenshot('join-default.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('submitting state', async ({ page }) => {
    await page.goto('/join?state=submitting', { waitUntil: 'networkidle' });
    await waitForJoinReady(page);
    await expect(page).toHaveScreenshot('join-submitting.png', {
      fullPage: true,
      // Spinner is intentionally animated (animate-spin) — mask to avoid flake.
      mask: [page.locator('.animate-spin'), page.locator('[data-dynamic]')],
    });
  });

  test('success state', async ({ page }) => {
    await page.goto('/join?state=success', { waitUntil: 'networkidle' });
    await waitForJoinReady(page);
    await expect(page).toHaveScreenshot('join-success.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('error state', async ({ page }) => {
    await page.goto('/join?state=error', { waitUntil: 'networkidle' });
    await waitForJoinReady(page);
    await expect(page).toHaveScreenshot('join-error.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('already-on-list state', async ({ page }) => {
    await page.goto('/join?state=already-on-list', { waitUntil: 'networkidle' });
    await waitForJoinReady(page);
    await expect(page).toHaveScreenshot('join-already-on-list.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
