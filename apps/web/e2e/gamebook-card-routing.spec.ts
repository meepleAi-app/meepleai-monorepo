/**
 * Routing fix — /gamebook card click navigates to /library/games/[gameId]/play.
 *
 * Issue #865: GamebookIndexView.tsx:174 previously pushed to `/gamebook/${gameId}`
 * which is a non-existent route → 404. The play surface lives at
 * `/library/games/[gameId]/play` (PR #794+). This spec asserts the routing fix.
 *
 * Why fixture override and not real backend:
 *   `useGamebooks` is a v1 carryover STUB (see
 *   `apps/web/src/hooks/queries/useGamebooks.ts`) — backend
 *   `GET /api/v1/gamebooks` is NOT exposed. The fixture override
 *   `?fixture=default` produces a deterministic ready card whose `gameId`
 *   matches `gamebookIndexFixtures.default.gamebooks[0].gameId`.
 *
 * Auth bypass: triple helper (Wave B.1 pattern, Issue #633) — `(authenticated)`
 * routes layout requires session-like cookie present.
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

test.describe('Gamebook card routing', () => {
  test.describe.configure({ retries: 0 });

  test('click on ready card navigates to /library/games/[gameId]/play', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuth(page);

    await page.goto('/gamebook?fixture=default', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="gamebook-index-view"]', { timeout: 30_000 });

    const readyCard = page.locator('[data-slot="gamebook-card"][data-status="ready"]').first();
    await expect(readyCard).toBeVisible();

    const gamebookId = await readyCard.getAttribute('data-gamebook-id');
    expect(gamebookId).toBeTruthy();

    await readyCard.click();

    await expect(page).toHaveURL(/\/library\/games\/[^/]+\/play(?:\?.*)?$/);
  });
});
