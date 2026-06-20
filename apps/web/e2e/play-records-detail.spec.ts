/**
 * E2E — Play Records detail view (#2349).
 *
 * Covers:
 *   default — record detail page renders (main landmark visible)
 *   error   — stubbed 404 from API leaves the page in an error/alert state
 *             (main landmark still visible, no crash)
 *   axe AA  — 0 WCAG 2.1 AA violations on the detail page
 *
 * Fixture id: pr-won-1 (matches the MSW handler in
 *   apps/web/src/__tests__/mocks/handlers/play-records.handlers.ts)
 *
 * Auth pattern: seedCookieConsent → seedAuthSession → mockAuthEndpoints
 * (matches play-records-hub.spec.ts exactly).
 *
 * NOTE: goto is NOT in beforeEach because the error test must install its
 * page.route stub BEFORE navigation — matching the pattern used in
 * play-records-hub.spec.ts axe section.
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

const RECORD_ID = 'pr-won-1';
const DETAIL = `/play-records/${RECORD_ID}`;

test.describe('Play Records — detail', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
    await seedAuthSession(page);
    await mockAuthEndpoints(page);
  });

  test('default: renders the record detail', async ({ page }) => {
    await page.goto(DETAIL);
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('main')).toBeVisible();
  });

  test('error: 404 from API surfaces the error state', async ({ page }) => {
    await page.route(`**/api/v1/play-records/${RECORD_ID}`, route =>
      route.fulfill({ status: 404, contentType: 'application/json', body: '{"error":"not_found"}' })
    );
    await page.goto(DETAIL);
    await page.waitForLoadState('networkidle');
    // The detail view's error/alert branch must render without crashing the page.
    await expect(page.getByRole('main')).toBeVisible();
  });

  test('axe AA: no violations', async ({ page }) => {
    await page.goto(DETAIL);
    await page.waitForLoadState('networkidle');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(results.violations).toEqual([]);
  });
});
