/**
 * Asse D follow-up P2 — `/games` hub multi-tab + Discover default tab — fixtures-wired (#1927).
 *
 * Verifies invariante #20 of the GameNight/Session domain model spec
 * (`docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`):
 *
 *   > sidebar ha 2 voci game-related: Library (personale) + Games (esplorazione,
 *   > default tab Discover)
 *
 * Originally tolerant (assert DOM if authenticated, else verify auth redirect).
 * Issue #1927 (Task A) wires the pre-existing `seedAuthSession`,
 * `mockAuthEndpoints` and `seedCookieConsent` helpers so we always exercise
 * the authenticated DOM tree. The auth-redirect fallback is dropped so any
 * route regression that breaks fixture wiring surfaces here loudly.
 */

import { expect, test } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

test.describe('Asse D P2 /games hub multi-tab', () => {
  test.skip(
    ({ browserName }) => browserName !== 'chromium',
    'Chromium-only for E2E skeleton speed'
  );

  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
    await seedAuthSession(page);
    await mockAuthEndpoints(page);
  });

  test('/games default route renders Discover tab (invariante #20)', async ({ page }) => {
    await page.goto('/games');

    await expect(page).not.toHaveURL(/\/(login|auth|sign-in)/);

    const hub = page.locator('[data-testid="games-hub"]');
    await expect(hub).toBeVisible({ timeout: 10_000 });
    await expect(hub).toHaveAttribute('data-active-tab', 'discover');
  });

  test('/games?tab=discover renders Discover tab explicitly', async ({ page }) => {
    await page.goto('/games?tab=discover');

    const hub = page.locator('[data-testid="games-hub"]');
    await expect(hub).toBeVisible({ timeout: 10_000 });
    await expect(hub).toHaveAttribute('data-active-tab', 'discover');
  });

  test('/games?tab=catalogo renders Catalogo placeholder', async ({ page }) => {
    await page.goto('/games?tab=catalogo');

    const placeholder = page.locator('[data-testid="games-tab-catalogo-coming-soon"]');
    await expect(placeholder).toBeVisible({ timeout: 10_000 });
  });

  test('/games?tab=trending renders Trending placeholder', async ({ page }) => {
    await page.goto('/games?tab=trending');

    const placeholder = page.locator('[data-testid="games-tab-trending-coming-soon"]');
    await expect(placeholder).toBeVisible({ timeout: 10_000 });
  });

  test('/games?tab=community renders Community placeholder', async ({ page }) => {
    await page.goto('/games?tab=community');

    const placeholder = page.locator('[data-testid="games-tab-community-coming-soon"]');
    await expect(placeholder).toBeVisible({ timeout: 10_000 });
  });

  test('/games?tab=invalid falls back to Discover (parse fallback)', async ({ page }) => {
    await page.goto('/games?tab=not-a-real-tab');

    const hub = page.locator('[data-testid="games-hub"]');
    await expect(hub).toBeVisible({ timeout: 10_000 });
    await expect(hub).toHaveAttribute('data-active-tab', 'discover');
  });

  test('/discover standalone route preserved for backward compat', async ({ page }) => {
    await page.goto('/discover');

    // The standalone /discover route should NOT mount the games-hub testid;
    // it renders the DiscoverHub component directly (no tab orchestrator).
    const hubLayoutMarker = page.locator('[data-slot="discover-rows"]');
    await expect(hubLayoutMarker).toBeVisible({ timeout: 10_000 });

    // /discover must NOT render the multi-tab wrapper (otherwise the
    // backward-compat contract is broken).
    await expect(page.locator('[data-testid="games-hub"]')).toHaveCount(0);
  });
});
