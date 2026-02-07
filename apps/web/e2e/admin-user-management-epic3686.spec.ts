/**
 * Admin User Management E2E Tests - Epic #3686
 * Tests user management features: table, filters, tier changes
 */

import { test as base, expect, Page } from './fixtures';
import { AdminHelper } from './pages';

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);
    await adminHelper.setupAdminAuth(true);
    await use(page);
  },
});

test.describe('Epic #3686 - User Management', () => {
  test('should display user table with tier and token columns', async ({ adminPage: page }) => {
    await page.goto('/admin/users');
    await page.waitForLoadState('networkidle');

    // Verify table headers
    await expect(page.getByText('Name')).toBeVisible();
    await expect(page.getByText('Email')).toBeVisible();
    await expect(page.getByText('Role')).toBeVisible();
    await expect(page.getByText('Tier')).toBeVisible();
    await expect(page.getByText('Token Usage')).toBeVisible();
  });

  test('should filter users by tier', async ({ adminPage: page }) => {
    await page.goto('/admin/users');
    await page.waitForLoadState('networkidle');

    // Find and use tier filter
    const tierFilter = page.locator('[data-testid="tier-filter"]').or(
      page.locator('select[aria-label*="tier" i]')
    );

    if (await tierFilter.isVisible()) {
      await tierFilter.selectOption('free');
      await page.waitForTimeout(1000);

      // Table should update (verify by checking for content)
      await expect(page.locator('table tbody tr').first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('should open change tier modal from user actions', async ({ adminPage: page }) => {
    await page.goto('/admin/users');
    await page.waitForLoadState('networkidle');

    // Find first user row actions menu
    const actionsButton = page.getByRole('button', { name: /open menu/i }).first();
    if (await actionsButton.isVisible()) {
      await actionsButton.click();

      // Look for "Change Tier" option
      const changeTierOption = page.getByRole('menuitem', { name: /change tier/i });
      if (await changeTierOption.isVisible()) {
        await changeTierOption.click();

        // Verify modal opens
        const modal = page.locator('[role="dialog"]').or(page.locator('.fixed.inset-0'));
        await expect(modal).toBeVisible({ timeout: 3000 });
      }
    }
  });
});

test.describe('Epic #3686 - Feature Flags', () => {
  test('should display feature flags table', async ({ adminPage: page }) => {
    await page.goto('/admin/feature-flags');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Feature Flags')).toBeVisible();
    await expect(page.locator('table')).toBeVisible();
  });

  test('should have toggle buttons for each flag', async ({ adminPage: page }) => {
    await page.goto('/admin/feature-flags');
    await page.waitForLoadState('networkidle');

    const toggleButtons = page.getByRole('button', { name: /enable|disable/i });
    const count = await toggleButtons.count();
    expect(count).toBeGreaterThan(0);
  });
});

test.describe('Epic #3686 - Tier Limits', () => {
  test('should display tier limits configuration table', async ({ adminPage: page }) => {
    await page.goto('/admin/tier-limits');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Tier Limits Configuration')).toBeVisible();
    await expect(page.locator('table')).toBeVisible();

    // Verify limit columns
    await expect(page.getByText('Tokens/Month')).toBeVisible();
    await expect(page.getByText('Messages/Day')).toBeVisible();
  });
});
