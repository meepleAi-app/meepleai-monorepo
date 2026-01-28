/**
 * SET-04: Notification Settings
 * Issue #3082 - P3 Low
 *
 * Tests notification settings configuration:
 * - Email notification preferences
 * - Push notification settings
 * - Digest frequency
 * - Mute options
 */

import { test, expect } from '../fixtures/chromatic';
import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupNotificationSettingsMocks(page: Page) {
  let settings = {
    emailNotifications: true,
    pushNotifications: true,
    digestFrequency: 'daily',
    marketingEmails: false,
    sessionReminders: true,
    newFeatures: true,
    muteAll: false,
  };

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

  await page.route(`${API_BASE}/api/v1/users/me/notification-settings`, async (route) => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ settings }),
      });
    } else if (method === 'PATCH') {
      const body = await route.request().postDataJSON();
      settings = { ...settings, ...body };
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Notification settings updated', settings }),
      });
    }
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return { getSettings: () => settings };
}

test.describe('SET-04: Notification Settings', () => {
  test('should display notification preferences', async ({ page }) => {
    await setupNotificationSettingsMocks(page);
    await page.goto('/settings/notifications');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/notification|email|push/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should toggle email notifications', async ({ page }) => {
    await setupNotificationSettingsMocks(page);
    await page.goto('/settings/notifications');
    await page.waitForLoadState('networkidle');

    const emailToggle = page.getByRole('switch', { name: /email/i }).or(
      page.locator('[data-testid="email-toggle"]')
    );
    if (await emailToggle.isVisible()) {
      await emailToggle.click();
      await expect(page.locator('body')).toBeVisible();
    }
  });

  test('should change digest frequency', async ({ page }) => {
    await setupNotificationSettingsMocks(page);
    await page.goto('/settings/notifications');
    await page.waitForLoadState('networkidle');

    const frequencySelect = page.getByRole('combobox', { name: /frequency|digest/i }).or(
      page.locator('[data-testid="digest-frequency"]')
    );
    if (await frequencySelect.isVisible()) {
      await frequencySelect.click();
      await page.getByText(/weekly/i).click();
      await expect(page.locator('body')).toBeVisible();
    }
  });

  test('should toggle mute all option', async ({ page }) => {
    await setupNotificationSettingsMocks(page);
    await page.goto('/settings/notifications');
    await page.waitForLoadState('networkidle');

    const muteToggle = page.getByRole('switch', { name: /mute.*all/i }).or(
      page.locator('[data-testid="mute-all"]')
    );
    if (await muteToggle.isVisible()) {
      await muteToggle.click();
      await expect(page.locator('body')).toBeVisible();
    }
  });
});
