import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from './_helpers/auth';

test.describe('SMOKE — /games?tab=library real backend round-trip', () => {
  test('frontend /games?tab=library renders without kind="error"', async ({ page, request }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/games?tab=library', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="games-library-view"]', { timeout: 30_000 });
    const errorState = await page
      .locator('[data-slot="games-empty-state"][data-kind="error"]')
      .count();
    expect(errorState).toBe(0);
  });
});
