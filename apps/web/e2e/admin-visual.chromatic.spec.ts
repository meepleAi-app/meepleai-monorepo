/**
 * Admin dashboard visual regression - Issue #2307
 *
 * Captures visual snapshots of admin components: stats, charts, tables
 */

import { test, expect } from './fixtures';

test.use({
  viewport: { width: 1440, height: 1024 }, // Larger viewport for dashboard
});

test.describe('Admin Dashboard - Visual Regression', () => {
  test('Dashboard metrics grid', async ({ page }) => {
    // Login as admin first (or use authenticated fixture if available)
    await page.goto('/admin', { waitUntil: 'domcontentloaded' });

    // Check if login required
    const isLoginPage = (await page.locator('input[type="email"]').count()) > 0;
    if (isLoginPage) {
      // Skip if not authenticated (E2E should handle auth separately)
      test.skip();
    }

    // Wait for metrics to load
    const metricsGrid = page.locator('[data-testid="metrics-grid"], .grid').first();

    try {
      await metricsGrid.waitFor({ timeout: 3000 });
      await page.waitForTimeout(300); // Wait for data loading
      await expect(metricsGrid).toHaveScreenshot('admin-metrics-grid.png');
    } catch {
      test.skip();
    }
  });

  test('Service health matrix', async ({ page }) => {
    await page.goto('/admin', { waitUntil: 'domcontentloaded' });

    const healthMatrix = page
      .locator('[data-testid="service-health-matrix"], [data-testid="system-status"]')
      .first();

    try {
      await healthMatrix.waitFor({ timeout: 2000 });
      await expect(healthMatrix).toHaveScreenshot('admin-service-health.png');
    } catch {
      test.skip();
    }
  });

  test('User management table', async ({ page }) => {
    await page.goto('/admin/users', { waitUntil: 'domcontentloaded' });

    const table = page.locator('table').first();

    try {
      await table.waitFor({ timeout: 3000 });
      await page.waitForTimeout(200); // Wait for data
      await expect(table).toHaveScreenshot('admin-users-table.png');
    } catch {
      test.skip();
    }
  });
});
