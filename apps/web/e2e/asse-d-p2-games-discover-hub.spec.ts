/**
 * Asse D follow-up P2 — `/games` hub multi-tab + Discover default tab.
 *
 * Verifies invariante #20 of the GameNight/Session domain model spec
 * (`docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`):
 *
 *   > sidebar ha 2 voci game-related: Library (personale) + Games (esplorazione,
 *   > default tab Discover)
 *
 * Full data-driven assertions require an authenticated session fixture (ride
 * on the Asse-D follow-up P4 cross-asse E2E harness, not yet shipped). This
 * skeleton guards routing + DOM mount against silent regressions while we
 * wait for the harness, mirroring the `asse-d-p1-polymorphic-scoring.spec.ts`
 * pattern: assert DOM if authenticated, else verify auth redirect.
 */

import { expect, test } from '@playwright/test';

test.describe('Asse D P2 /games hub multi-tab', () => {
  test.skip(
    ({ browserName }) => browserName !== 'chromium',
    'Chromium-only for E2E skeleton speed'
  );

  test('/games default route renders Discover tab (invariante #20)', async ({ page }) => {
    await page.goto('/games');

    const hub = page.locator('[data-testid="games-hub"]');
    const isAuthenticated = (await hub.count()) > 0;

    if (isAuthenticated) {
      await expect(hub).toBeVisible();
      await expect(hub).toHaveAttribute('data-active-tab', 'discover');
    } else {
      // Unauthenticated path — verify redirect to auth (covers `/login`,
      // `/auth/*`, `/sign-in`, etc.).
      await expect(page).toHaveURL(/\/(login|auth|sign-in)/);
    }
  });

  test('/games?tab=discover renders Discover tab explicitly', async ({ page }) => {
    await page.goto('/games?tab=discover');

    const hub = page.locator('[data-testid="games-hub"]');
    const isAuthenticated = (await hub.count()) > 0;

    if (isAuthenticated) {
      await expect(hub).toHaveAttribute('data-active-tab', 'discover');
    } else {
      await expect(page).toHaveURL(/\/(login|auth|sign-in)/);
    }
  });

  test('/games?tab=catalogo renders Catalogo placeholder', async ({ page }) => {
    await page.goto('/games?tab=catalogo');

    const placeholder = page.locator('[data-testid="games-tab-catalogo-coming-soon"]');
    const isAuthenticated = (await placeholder.count()) > 0;

    if (isAuthenticated) {
      await expect(placeholder).toBeVisible();
    } else {
      await expect(page).toHaveURL(/\/(login|auth|sign-in)/);
    }
  });

  test('/games?tab=trending renders Trending placeholder', async ({ page }) => {
    await page.goto('/games?tab=trending');

    const placeholder = page.locator('[data-testid="games-tab-trending-coming-soon"]');
    const isAuthenticated = (await placeholder.count()) > 0;

    if (isAuthenticated) {
      await expect(placeholder).toBeVisible();
    } else {
      await expect(page).toHaveURL(/\/(login|auth|sign-in)/);
    }
  });

  test('/games?tab=community renders Community placeholder', async ({ page }) => {
    await page.goto('/games?tab=community');

    const placeholder = page.locator('[data-testid="games-tab-community-coming-soon"]');
    const isAuthenticated = (await placeholder.count()) > 0;

    if (isAuthenticated) {
      await expect(placeholder).toBeVisible();
    } else {
      await expect(page).toHaveURL(/\/(login|auth|sign-in)/);
    }
  });

  test('/games?tab=invalid falls back to Discover (parse fallback)', async ({ page }) => {
    await page.goto('/games?tab=not-a-real-tab');

    const hub = page.locator('[data-testid="games-hub"]');
    const isAuthenticated = (await hub.count()) > 0;

    if (isAuthenticated) {
      await expect(hub).toHaveAttribute('data-active-tab', 'discover');
    } else {
      await expect(page).toHaveURL(/\/(login|auth|sign-in)/);
    }
  });

  test('/discover standalone route preserved for backward compat', async ({ page }) => {
    await page.goto('/discover');

    // The standalone /discover route should NOT mount the games-hub testid;
    // it renders the DiscoverHub component directly (no tab orchestrator).
    const hubLayoutMarker = page.locator('[data-slot="discover-rows"]');
    const isAuthenticated = (await hubLayoutMarker.count()) > 0;

    if (isAuthenticated) {
      await expect(hubLayoutMarker).toBeVisible();
      // /discover must NOT render the multi-tab wrapper (otherwise the
      // backward-compat contract is broken).
      await expect(page.locator('[data-testid="games-hub"]')).toHaveCount(0);
    } else {
      await expect(page).toHaveURL(/\/(login|auth|sign-in)/);
    }
  });
});
