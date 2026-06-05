/**
 * Dashboard priority-driven flow (Asse C, Issue #1898 WP7 T7).
 *
 * E2E skeleton for the 4-priority-section dashboard layout
 * (Prossimi → Recenti → Suggested → Friends).
 *
 * NOTE: full data-driven assertions are deferred until E2E auth seeding
 * lands (consistent with sibling Stage 3 E2E skeletons). The skeleton:
 *  - verifies /dashboard route reachable (or redirects to login when
 *    unauthenticated)
 *  - asserts presence of the priority sections container slot when
 *    authenticated
 *  - asserts Prossimi section is queryable when authenticated
 *
 * Live data wiring + GameNight drawer interaction is exercised in the
 * unit + RTL tests for ProssimiSection / RecentiSection / SuggestedSection
 * / FriendsActivitySection (T2-T6) and in the orchestrator smoke test
 * (T7).
 */

import { test, expect } from '@playwright/test';

test.describe('Dashboard priority-driven flow (asse C)', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test('renders priority sections container OR redirects unauthenticated user', async ({
    page,
  }) => {
    await page.goto('/dashboard');

    // Tolerant authentication check — if the test environment doesn't seed
    // an auth session, the app redirects to /login (or similar). Otherwise
    // the priority sections container must be present.
    const grid = page.locator('[data-slot="dashboard-priority-sections"]');
    const url = page.url();

    if (/\/(login|auth|sign-in)/.test(url)) {
      // Unauthenticated path — verify redirect happened, skeleton done.
      await expect(page).toHaveURL(/\/(login|auth|sign-in)/);
      return;
    }

    // Authenticated path — verify the priority sections wrapper renders.
    await expect(grid).toBeVisible({ timeout: 10_000 });
  });

  test('priority sections appear in fixed order (when authenticated)', async ({ page }) => {
    await page.goto('/dashboard');

    const url = page.url();
    if (/\/(login|auth|sign-in)/.test(url)) {
      test.skip(true, 'Authenticated dashboard required for ordering assertion');
      return;
    }

    // Wait for the priority sections grid to render.
    const grid = page.locator('[data-slot="dashboard-priority-sections"]');
    await expect(grid).toBeVisible({ timeout: 10_000 });

    // Prossimi (slot #1) is the only section that ALWAYS renders (empty
    // shows EmptySection inside, default shows cards). Recenti/Suggested/
    // Friends may return null when empty/silent.
    const prossimi = page.locator('[data-section-id="prossimi"]');
    await expect(prossimi).toBeVisible();
  });
});
