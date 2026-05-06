/**
 * V2 State Coverage — /gamebook index route (Issue #788, SP6 Phase B Tier M).
 *
 * Captures fixture-driven visual variants of the 6-cell FSM. Per Phase 0.5
 * contract, this spec contributes 5 of the 6 visual baselines:
 *   - `empty` (desktop) — no gamebooks, EmptyGamebooks CTA visible
 *   - `quota-soft` (desktop) — usage ≥90%, QuotaWidget variant='soft'
 *   - `quota-hard` (desktop) — usage 100%, QuotaWidget variant='hard',
 *     hero CTA disabled
 *   - `loading` (desktop) — skeleton grid (orchestrator forces loading cell)
 *   - `error` (desktop) — error banner + retry CTA
 *
 * The remaining baseline (`default-desktop`, `default-mobile`) lives in
 * `e2e/visual-migrated/sp6-gamebook-index.spec.ts`.
 *
 * NOT covered visually (gating to existing unit tests):
 *   - Mobile variants of empty/loading/error — single-tree responsive layout,
 *     covered by component unit tests in
 *     `apps/web/src/app/(authenticated)/gamebook/_components/__tests__/`.
 *
 * URL override hatch (`?fixture=…`):
 *   Gated by `STATE_OVERRIDE_ENABLED` (`IS_VISUAL_TEST_BUILD ||
 *   NODE_ENV !== 'production'`). In production builds the bundler dead-code-
 *   eliminates the lookup so this hatch is a build-time concern only.
 *
 * Auth bypass: triple helper (seedAuthSession + seedCookieConsent +
 * mockAuthEndpoints) — Wave B.1 pattern (Issue #633) for `(authenticated)`
 * routes. No `networkidle` (Wave B.1 lesson) — `domcontentloaded` +
 * explicit `waitForSelector`.
 *
 * Snapshots written to
 * `apps/web/e2e/v2-states/gamebook-index.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml --ref <branch> \
 *     -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function gotoGamebook(page: Page, fixture: string): Promise<void> {
  await seedAuth(page);
  await page.goto(`/gamebook?fixture=${fixture}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="gamebook-index-view"]', { timeout: 30_000 });
}

test.describe('Gamebook index — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('desktop empty (no gamebooks)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await gotoGamebook(page, 'empty');
    await expect(
      page.locator('[data-slot="gamebook-index-view"][data-ui-state="empty"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="empty-gamebooks"]')).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-index-empty-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  test('desktop quota-soft (>=90% used)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await gotoGamebook(page, 'quota-soft');
    await expect(
      page.locator('[data-slot="gamebook-index-view"][data-ui-state="quota-soft"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="quota-widget"][data-variant="soft"]')).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-index-quota-soft-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  test('desktop quota-hard (100% blocked)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await gotoGamebook(page, 'quota-hard');
    await expect(
      page.locator('[data-slot="gamebook-index-view"][data-ui-state="quota-hard"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="quota-widget"][data-variant="hard"]')).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-index-quota-hard-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  test('desktop loading (skeleton grid)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await gotoGamebook(page, 'loading');
    await expect(
      page.locator('[data-slot="gamebook-index-view"][data-ui-state="loading"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="gamebook-card-skeleton"]').first()).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-index-loading-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });

  test('desktop error', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await gotoGamebook(page, 'error');
    await expect(
      page.locator('[data-slot="gamebook-index-view"][data-ui-state="error"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="gamebook-index-error"]')).toBeVisible();
    await expect(page.locator('[data-slot="gamebook-index-error-retry"]')).toBeVisible();

    await expect(page).toHaveScreenshot('gamebook-index-error-desktop.png', {
      fullPage: true,
      animations: 'disabled',
    });
  });
});
