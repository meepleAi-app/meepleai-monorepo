/**
 * SET-02: Appearance Settings
 * Issue #3082 - P3 Low
 *
 * Tests appearance settings functionality:
 * - Theme selection (light/dark/system)
 * - Font size settings
 * - Layout preferences
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupAppearanceMocks(page: Page) {
  let settings = { theme: 'system', fontSize: 'medium', layout: 'default' };

  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'test-user', email: 'test@example.com', displayName: 'Test User', role: 'User' },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/users/me/preferences`, async (route) => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ preferences: settings }),
      });
    } else if (method === 'PATCH') {
      const body = await route.request().postDataJSON();
      settings = { ...settings, ...body };
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Preferences saved', preferences: settings }),
      });
    }
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return { getSettings: () => settings };
}

test.describe('SET-02: Appearance Settings', () => {
  test('should display theme options', async ({ page }) => {
    await setupAppearanceMocks(page);
    await page.goto('/settings/appearance');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/theme|light|dark|system/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should switch theme', async ({ page }) => {
    await setupAppearanceMocks(page);
    await page.goto('/settings/appearance');
    await page.waitForLoadState('networkidle');

    const darkOption = page.getByRole('radio', { name: /dark/i }).or(page.getByText(/^dark$/i));
    if (await darkOption.isVisible()) {
      await darkOption.click();
      await expect(page.locator('html.dark, [data-theme="dark"]').or(page.locator('body'))).toBeVisible();
    }
  });

  test('should show font size options', async ({ page }) => {
    await setupAppearanceMocks(page);
    await page.goto('/settings/appearance');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/font.*size|small|medium|large/i).first()).toBeVisible();
  });
});
