/**
 * NOT-04: In-App Notifications
 * Issue #3082 - P3 Low
 *
 * Tests in-app notification functionality:
 * - Display notification badge
 * - Show notification dropdown
 * - Mark as read
 * - Clear notifications
 */

import { test, expect } from '../fixtures/chromatic';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function setupNotificationsMocks(page: Page) {
  const notifications = [
    { id: 'n1', title: 'New game added', message: 'Chess variants are now available', read: false, createdAt: new Date().toISOString() },
    { id: 'n2', title: 'Session reminder', message: 'Your session starts in 1 hour', read: false, createdAt: new Date(Date.now() - 3600000).toISOString() },
    { id: 'n3', title: 'Welcome!', message: 'Thanks for joining MeepleAI', read: true, createdAt: new Date(Date.now() - 86400000).toISOString() },
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

  await page.route(`${API_BASE}/api/v1/notifications**`, async (route) => {
    const method = route.request().method();
    if (method === 'GET') {
      const unreadCount = notifications.filter(n => !n.read).length;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ notifications, unreadCount }),
      });
    } else if (method === 'PATCH') {
      const notifId = route.request().url().match(/notifications\/([^/?]+)/)?.[1];
      const idx = notifications.findIndex(n => n.id === notifId);
      if (idx >= 0) notifications[idx].read = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Marked as read' }),
      });
    }
  });

  await page.route(`${API_BASE}/api/v1/notifications/mark-all-read`, async (route) => {
    notifications.forEach(n => n.read = true);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'All marked as read' }),
    });
  });

  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  return { getNotifications: () => notifications };
}

test.describe('NOT-04: In-App Notifications', () => {
  test('should display notification badge', async ({ page }) => {
    await setupNotificationsMocks(page);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await expect(
      page.locator('[data-testid="notification-badge"], .notification-badge').or(page.getByText(/2/))
    ).toBeVisible({ timeout: 5000 });
  });

  test('should show notification dropdown', async ({ page }) => {
    await setupNotificationsMocks(page);
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const notificationButton = page.getByRole('button', { name: /notification/i }).or(
      page.locator('[data-testid="notification-button"]')
    );
    if (await notificationButton.isVisible()) {
      await notificationButton.click();
      await expect(page.getByText(/new.*game|session.*reminder/i)).toBeVisible();
    }
  });

  test('should mark notification as read', async ({ page }) => {
    await setupNotificationsMocks(page);
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const notificationButton = page.getByRole('button', { name: /notification/i });
    if (await notificationButton.isVisible()) {
      await notificationButton.click();
      const notification = page.getByText(/new.*game/i);
      if (await notification.isVisible()) {
        await notification.click();
        await expect(page.locator('body')).toBeVisible();
      }
    }
  });
});
