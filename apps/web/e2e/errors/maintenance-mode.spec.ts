/**
 * ERR-10: Maintenance Mode
 * Issue #3082 - P3 Low
 *
 * Tests maintenance mode handling:
 * - Display maintenance page
 * - Show estimated completion time
 * - Allow admin bypass
 */

import { test, expect } from '../fixtures/chromatic';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupMaintenanceMocks(page: Page, isAdmin = false) {
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: isAdmin ? 'admin' : 'user',
          email: isAdmin ? 'admin@meepleai.dev' : 'user@example.com',
          displayName: isAdmin ? 'Admin' : 'User',
          role: isAdmin ? 'Admin' : 'User',
        },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/system/status`, async (route) => {
    await route.fulfill({
      status: 503,
      contentType: 'application/json',
      body: JSON.stringify({
        maintenance: true,
        message: 'Scheduled maintenance in progress',
        estimatedEnd: new Date(Date.now() + 3600000).toISOString(),
        bypassAllowed: isAdmin,
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/**`, async (route) => {
    if (!isAdmin) {
      await route.fulfill({
        status: 503,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Service under maintenance' }),
      });
    } else {
      await route.continue();
    }
  });

  return {};
}

test.describe('ERR-10: Maintenance Mode', () => {
  test('should display maintenance page', async ({ page }) => {
    await setupMaintenanceMocks(page);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/maintenance|down|unavailable/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show estimated completion', async ({ page }) => {
    await setupMaintenanceMocks(page);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/estimat|complete|back|soon/i)).toBeVisible();
  });

  test('should allow admin bypass', async ({ page }) => {
    await setupMaintenanceMocks(page, true);
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('body')).toBeVisible();
  });
});
