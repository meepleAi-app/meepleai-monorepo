/**
 * Dashboard priority-driven flow (Asse C, Issue #1898 WP7 T7) — fixtures-wired.
 *
 * Originally shipped as a "tolerant redirect" smoke skeleton (waiting for the
 * Asse-D follow-up P4 cross-asse E2E harness, see PR #1930). Issue #1927
 * (Task A) wires the pre-existing `seedAuthSession` + `mockAuthEndpoints` +
 * `seedCookieConsent` helpers (Wave B.1 #633) so we always exercise the
 * authenticated DOM tree and assert directly on the priority-sections layout.
 *
 * Scope kept FE-only — BE entity seeding (and data-driven card content
 * assertions) ride on Issue #1928 (Task B). Until then we assert structural
 * slots that always render (DashboardHero / StatsRow / priority-sections
 * container / Prossimi section), since Recenti/Suggested/Friends silently
 * fall back to `null` when the upstream queries return empty.
 */

import { expect, test } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

test.describe('Dashboard priority-driven flow (asse C)', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
    await seedAuthSession(page);
    await mockAuthEndpoints(page);
  });

  test('mounts priority sections container with Prossimi always rendered', async ({ page }) => {
    await page.goto('/dashboard');

    // Authenticated path enforced by fixtures — no `/login` fallback branch.
    await expect(page).not.toHaveURL(/\/(login|auth|sign-in)/);

    const grid = page.locator('[data-slot="dashboard-priority-sections"]');
    await expect(grid).toBeVisible({ timeout: 10_000 });

    // Prossimi is the only section that ALWAYS renders (empty → EmptySection
    // inside, default → cards). Recenti/Suggested/Friends may return `null`
    // when their respective queries are empty (silent fallback per MAJ-6).
    const prossimi = page.locator('[data-section-id="prossimi"]');
    await expect(prossimi).toBeVisible();
  });

  test('renders DashboardHero + StatsRow entry surfaces (preserved by C refactor)', async ({
    page,
  }) => {
    await page.goto('/dashboard');

    // Hero kicker is unconditional (data-testid hard-coded in
    // `components/dashboard/DashboardHero.tsx`). The KPI navigation row is
    // also unconditional; its `nav` element exposes a fixed Italian aria-label.
    await expect(page.locator('[data-testid="hero-kicker"]').first()).toBeVisible({
      timeout: 10_000,
    });
    await expect(page.locator('nav[aria-label="Statistiche personali"]')).toBeVisible();
  });

  test('exposes section ordering: priority-sections renders BEFORE legacy sections', async ({
    page,
  }) => {
    await page.goto('/dashboard');

    // DEC-1 refactor-in-place: 4 priority sections appear in fixed order
    // ABOVE any preserved entry surface. The priority container must
    // appear at least once, and the Prossimi section sits inside it.
    const grid = page.locator('[data-slot="dashboard-priority-sections"]');
    await expect(grid).toBeVisible();

    const prossimiInsideGrid = grid.locator('[data-section-id="prossimi"]');
    await expect(prossimiInsideGrid).toHaveCount(1);
  });
});
