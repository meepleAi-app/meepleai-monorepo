import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from './_helpers/auth';

test.describe('SMOKE — /library games tab (real backend)', () => {
  // The legacy `/games?tab=library` → `/library` redirect (PR #1567 / #1521) was
  // removed in the Asse D P2 refactor (2026-06-05): `/games` is now a multi-tab
  // hub orchestrator with Discover as the default tab. Unknown `?tab=` values
  // fall back to Discover, so `/games?tab=library` no longer leaves `/games`.
  // Hub coverage is owned by `apps/web/e2e/asse-d-p2-games-discover-hub.spec.ts`;
  // `/library` direct entry is covered by the test below.
  test('/library games tab renders GamesResultsGrid (or empty state) — #1566', async ({
    page,
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/library', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
    // LibraryHub does not read ?tab= from the URL; click the games tab to reach
    // the games surface (#1566 wired the Games* components into this tab).
    // Locale-agnostic selector: Playwright CI Chrome defaults to en-US which
    // renders the label as "Games" (not "Giochi"), so we target data-tab-key
    // exposed by LibraryTabs.tsx — same pattern as the unit suite (#1640).
    await page.locator('[role="tab"][data-tab-key="games"]').click();
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
