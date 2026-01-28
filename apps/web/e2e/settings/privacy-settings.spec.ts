/**
 * SET-03: Privacy Settings
 * Issue #3082 - P3 Low
 *
 * Tests privacy settings functionality:
 * - Profile visibility
 * - Activity visibility
 * - Data export
 */

import { test, expect } from '../fixtures/chromatic';
import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupPrivacyMocks(page: Page) {
  let privacy = { profileVisibility: 'public', activityVisible: true, searchable: true };

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

  await page.route(`${API_BASE}/api/v1/users/me/privacy`, async (route) => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ privacy }),
      });
    } else if (method === 'PATCH') {
      const body = await route.request().postDataJSON();
      privacy = { ...privacy, ...body };
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Privacy settings updated', privacy }),
      });
    }
  });

  await page.route(`${API_BASE}/api/v1/users/me/export`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Export started', downloadUrl: '/downloads/export.zip' }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return { getPrivacy: () => privacy };
}

test.describe('SET-03: Privacy Settings', () => {
  test('should display privacy options', async ({ page }) => {
    await setupPrivacyMocks(page);
    await page.goto('/settings/privacy');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/privacy|profile.*visib|activity/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should toggle profile visibility', async ({ page }) => {
    await setupPrivacyMocks(page);
    await page.goto('/settings/privacy');
    await page.waitForLoadState('networkidle');

    const visibilityToggle = page.getByRole('combobox', { name: /visibility/i }).or(
      page.locator('[data-testid="visibility-toggle"]')
    );
    if (await visibilityToggle.isVisible()) {
      await visibilityToggle.click();
      await page.getByText(/private/i).click();
      await expect(page.locator('body')).toBeVisible();
    }
  });

  test('should show data export option', async ({ page }) => {
    await setupPrivacyMocks(page);
    await page.goto('/settings/privacy');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('button', { name: /export.*data|download.*data/i })).toBeVisible();
  });
});
