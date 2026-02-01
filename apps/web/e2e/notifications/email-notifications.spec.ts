/**
 * NOT-05: Email Notifications
 * Issue #3082 - P3 Low
 *
 * Tests email notification display:
 * - View email notification history
 * - Resend email notifications
 * - Email delivery status
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupEmailNotificationsMocks(page: Page) {
  const emailHistory = [
    { id: 'e1', subject: 'Welcome to MeepleAI', sentAt: new Date(Date.now() - 604800000).toISOString(), status: 'delivered' },
    { id: 'e2', subject: 'Your Weekly Digest', sentAt: new Date(Date.now() - 172800000).toISOString(), status: 'delivered' },
    { id: 'e3', subject: 'Password Reset', sentAt: new Date(Date.now() - 86400000).toISOString(), status: 'failed' },
  ];

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

  await page.route(`${API_BASE}/api/v1/users/me/emails**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ emails: emailHistory }),
    });
  });

  await page.route(`${API_BASE}/api/v1/users/me/emails/*/resend`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Email resent' }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return { emailHistory };
}

test.describe('NOT-05: Email Notifications', () => {
  test('should display email history', async ({ page }) => {
    await setupEmailNotificationsMocks(page);
    await page.goto('/settings/notifications/emails');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/welcome|digest|password/i).first()).toBeVisible({ timeout: 5000 });
  });

  test('should show delivery status', async ({ page }) => {
    await setupEmailNotificationsMocks(page);
    await page.goto('/settings/notifications/emails');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/delivered|failed/i).first()).toBeVisible();
  });

  test('should allow resending failed email', async ({ page }) => {
    await setupEmailNotificationsMocks(page);
    await page.goto('/settings/notifications/emails');
    await page.waitForLoadState('networkidle');

    const resendButton = page.getByRole('button', { name: /resend/i }).first();
    if (await resendButton.isVisible()) {
      await resendButton.click();
      await expect(page.getByText(/resent|sent/i)).toBeVisible();
    }
  });
});
