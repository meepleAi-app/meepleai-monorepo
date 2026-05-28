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
