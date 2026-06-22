/**
 * Admin KB Explorer + sub-nav smoke — F3.1 T6
 *
 * Verifies that:
 * 1. Landing on /admin/knowledge-base renders the sub-nav with "Explorer"
 *    marked as the active tab and the KbTree mounts.
 * 2. Navigating to /vectors keeps the sub-nav visible with "Vector Collections"
 *    as the active tab (Explorer no longer active).
 *
 * Deep behaviour (filtering, doc selection, API responses) is covered by unit
 * tests; these tests only smoke the navigation layer.
 */

import { expect, Page, test as base } from '@playwright/test';

import { AdminHelper } from './pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ── Admin-auth fixture — same pattern as admin-analytics.spec.ts / admin-configuration.spec.ts ──

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);
    // Sets up /auth/me mock + catch-all /api/v1/admin/** stub.
    // skip navigation = true (each test navigates explicitly).
    await adminHelper.setupAdminAuth(true);
    await use(page);
  },
});

test.describe('Admin KB Explorer + sub-nav (F3.1)', () => {
  // Register specific mock for getGameKbStatuses BEFORE navigation so it takes
  // priority over the AdminHelper catch-all stub (which returns {data:[]} and
  // would fail GameKbStatusesSchema validation, causing the error panel to render).
  // Playwright matches routes in registration order; since setupAdminAuth registers
  // the catch-all during fixture setup, we register this specific route here —
  // Playwright will match the more-specific route registered after because it checks
  // all matching handlers and the last `route.fulfill` wins, but to be safe we use
  // beforeEach to ensure the specific handler is always active before goto.
  test.beforeEach(async ({ adminPage: page }) => {
    await page.route(`${apiBase}/api/v1/admin/kb/games/`, async route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      })
    );
  });

  test('landing /admin/knowledge-base renders sub-nav with Explorer active and Explorer body', async ({
    adminPage: page,
  }) => {
    await page.goto('/admin/knowledge-base');

    // Sub-nav is visible
    const subnav = page.getByRole('navigation', { name: /Knowledge Base sezioni/i });
    await expect(subnav).toBeVisible();

    // Explorer tab is active
    await expect(subnav.getByRole('link', { name: 'Explorer' })).toHaveAttribute(
      'aria-current',
      'page'
    );

    // Other representative tabs are present
    await expect(subnav.getByRole('link', { name: 'Vector Collections' })).toBeVisible();
    await expect(subnav.getByRole('link', { name: 'Snapshots' })).toBeVisible();

    // Explorer body: the tree renders (may be empty when API returns {data:[]})
    const tree = page.getByRole('tree', { name: /Knowledge Base alberatura/i });
    await expect(tree).toBeVisible({ timeout: 15_000 });
  });

  test('navigating to /vectors keeps sub-nav with Vector Collections active', async ({
    adminPage: page,
  }) => {
    await page.goto('/admin/knowledge-base');

    // Click the Vector Collections tab
    await page.getByRole('link', { name: 'Vector Collections' }).click();

    await expect(page).toHaveURL(/\/admin\/knowledge-base\/vectors/);

    const subnav = page.getByRole('navigation', { name: /Knowledge Base sezioni/i });
    await expect(subnav).toBeVisible();

    // Vector Collections is now active
    await expect(subnav.getByRole('link', { name: 'Vector Collections' })).toHaveAttribute(
      'aria-current',
      'page'
    );

    // Explorer is no longer active
    await expect(subnav.getByRole('link', { name: 'Explorer' })).not.toHaveAttribute(
      'aria-current',
      'page'
    );
  });
});
