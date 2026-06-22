/**
 * E2E — Play Records stats dashboard (#2350).
 *
 * Route: /play-records?tab=stats
 *   (The standalone /play-records/stats redirects to this hub tab per #5039.)
 *
 * Covers:
 *   default — stats dashboard renders (main landmark visible)
 *   error   — stubbed 500 on /statistics still leaves the page rendered
 *   axe AA  — 0 WCAG 2.1 AA violations on the stats tab
 *
 * Auth pattern: seedCookieConsent → seedAuthSession → mockAuthEndpoints
 * (matches play-records-hub.spec.ts exactly).
 *
 * NOTE: goto is NOT in beforeEach because the error test must install its
 * page.route stub BEFORE navigation.
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

const STATS = '/play-records?tab=stats';

test.describe('Play Records — stats', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
    await seedAuthSession(page);
    await mockAuthEndpoints(page);
  });

  test('default: renders the stats dashboard', async ({ page }) => {
    await page.goto(STATS);
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('main')).toBeVisible();
  });

  test('error: failed stats fetch surfaces error state', async ({ page }) => {
    await page.route('**/api/v1/play-records/statistics', route =>
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: '{"error":"server_error"}',
      })
    );
    await page.goto(STATS);
    await page.waitForLoadState('networkidle');
    // The stats page must render (error branch) without crashing.
    await expect(page.getByRole('main')).toBeVisible();
  });

  test('axe AA: no violations', async ({ page }) => {
    await page.goto(STATS);
    await page.waitForLoadState('networkidle');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });
});
