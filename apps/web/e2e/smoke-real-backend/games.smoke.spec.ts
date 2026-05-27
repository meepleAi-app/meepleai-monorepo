import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from './_helpers/auth';

test.describe('SMOKE — /games redirect + /library games tab (real backend)', () => {
  test('GET /games?tab=library redirects to /library and renders LibraryHub', async ({
    page,
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    // PR #1567 (#1521) replaced /games with redirect('/library'); the ?tab=library
    // query is dropped by the redirect.
    await page.goto('/games?tab=library', { waitUntil: 'domcontentloaded' });
    expect(new URL(page.url()).pathname).toBe('/library');
    await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
    const errorState = await page.locator('[data-state="error"]').count();
    expect(errorState).toBe(0);
  });

  test('/library games tab renders GamesResultsGrid (or empty state) — #1566', async ({
    page,
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/library', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
    // LibraryHub does not read ?tab= from the URL; click the Giochi tab to reach
    // the games surface (#1566 wired the Games* components into this tab).
    await page.getByRole('tab', { name: /giochi/i }).click();
    // The smoke fixture user has 1 library entry → expect the grid; fall back to
    // empty-state if the fixture changes. Either proves the games branch mounted.
    await page.waitForSelector(
      '[data-slot="games-results-grid"], [data-slot="games-empty-state"]',
      { timeout: 30_000 }
    );
    const errorState = await page
      .locator('[data-slot="games-empty-state"][data-kind="error"]')
      .count();
    expect(errorState).toBe(0);
  });
});
