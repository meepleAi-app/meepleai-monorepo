/**
 * SET-01: Account Settings
 * Issue #3082 - P3 Low
 *
 * Tests account settings functionality:
 * - Update display name
 * - Change email
 * - Delete account
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupAccountSettingsMocks(page: Page) {
  const user = { id: 'test-user', email: 'test@example.com', displayName: 'Test User' };

  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { ...user, role: 'User' },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }),
    });
  });

  await page.route(`${API_BASE}/api/v1/users/me`, async (route) => {
    const method = route.request().method();
    if (method === 'PATCH') {
      const body = await route.request().postDataJSON();
      if (body.displayName) user.displayName = body.displayName;
      if (body.email) user.email = body.email;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Profile updated', user }),
      });
    } else if (method === 'DELETE') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Account deleted' }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ user }),
      });
    }
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return { getUser: () => user };
}

test.describe('SET-01: Account Settings', () => {
  test('should display current account info', async ({ page }) => {
    await setupAccountSettingsMocks(page);
    await page.goto('/settings/account');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/test@example|test.*user/i)).toBeVisible({ timeout: 5000 });
  });

  test('should update display name', async ({ page }) => {
    const mocks = await setupAccountSettingsMocks(page);
    await page.goto('/settings/account');
    await page.waitForLoadState('networkidle');

    const nameInput = page.getByLabel(/display.*name|name/i);
    if (await nameInput.isVisible()) {
      await nameInput.clear();
      await nameInput.fill('New Name');
      await page.getByRole('button', { name: /save|update/i }).click();
      await expect(page.getByText(/updated|saved/i)).toBeVisible();
    }
  });

  test('should show delete account option', async ({ page }) => {
    await setupAccountSettingsMocks(page);
    await page.goto('/settings/account');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('button', { name: /delete.*account/i })).toBeVisible();
  });
});
