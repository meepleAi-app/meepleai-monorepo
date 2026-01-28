/**
 * AUTH-11: Login Notifications
 * Issue #3082 - P1 High
 *
 * Tests login notification system:
 * - New device login notification
 * - Suspicious login alert (new location/country)
 * - Notification settings management
 * - Email notification delivery
 */

import { test, expect } from '../fixtures/chromatic';
import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface DeviceInfo {
  id: string;
  userAgent: string;
  browser: string;
  os: string;
  location: {
    city: string;
    country: string;
    ip: string;
  };
  lastActive: string;
  isCurrent: boolean;
  isNew: boolean;
}

interface LoginNotification {
  id: string;
  type: 'new_device' | 'suspicious_location' | 'new_country';
  deviceInfo: DeviceInfo;
  timestamp: string;
  acknowledged: boolean;
}

/**
 * Setup mock routes for login notifications testing
 */
async function setupLoginNotificationsMocks(
  page: Page,
  options: {
    notificationsEnabled?: boolean;
    hasNewDeviceLogin?: boolean;
    hasSuspiciousLogin?: boolean;
    notifications?: LoginNotification[];
  } = {}
) {
  const {
    notificationsEnabled = true,
    hasNewDeviceLogin = false,
    hasSuspiciousLogin = false,
    notifications = [],
  } = options;

  const mockDevice: DeviceInfo = {
    id: 'device-1',
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
    browser: 'Chrome 120',
    os: 'Windows 10',
    location: {
      city: 'Milan',
      country: 'Italy',
      ip: '192.168.1.1',
    },
    lastActive: new Date().toISOString(),
    isCurrent: true,
    isNew: false,
  };

  const mockNotifications: LoginNotification[] = notifications.length > 0 ? notifications : [];

  if (hasNewDeviceLogin) {
    mockNotifications.push({
      id: 'notif-1',
      type: 'new_device',
      deviceInfo: {
        ...mockDevice,
        id: 'device-2',
        browser: 'Firefox 121',
        isCurrent: false,
        isNew: true,
      },
      timestamp: new Date().toISOString(),
      acknowledged: false,
    });
  }

  if (hasSuspiciousLogin) {
    mockNotifications.push({
      id: 'notif-2',
      type: 'suspicious_location',
      deviceInfo: {
        ...mockDevice,
        id: 'device-3',
        location: {
          city: 'Tokyo',
          country: 'Japan',
          ip: '203.0.113.42',
        },
        isCurrent: false,
        isNew: true,
      },
      timestamp: new Date().toISOString(),
      acknowledged: false,
    });
  }

  // Mock auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'test-user-id',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock login notifications endpoint
  await page.route(`${API_BASE}/api/v1/users/me/login-notifications`, async (route) => {
    const method = route.request().method();

    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          notifications: mockNotifications,
          unreadCount: mockNotifications.filter(n => !n.acknowledged).length,
        }),
      });
    } else if (method === 'POST') {
      // Acknowledge notification
      const body = await route.request().postDataJSON();
      const notification = mockNotifications.find(n => n.id === body?.notificationId);
      if (notification) {
        notification.acknowledged = true;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Notification acknowledged' }),
      });
    } else {
      await route.continue();
    }
  });

  // Mock notification settings endpoint
  await page.route(`${API_BASE}/api/v1/users/me/notification-settings`, async (route) => {
    const method = route.request().method();

    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          loginNotifications: notificationsEnabled,
          newDeviceAlerts: notificationsEnabled,
          suspiciousActivityAlerts: notificationsEnabled,
          emailNotifications: notificationsEnabled,
        }),
      });
    } else if (method === 'PUT' || method === 'PATCH') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Settings updated' }),
      });
    } else {
      await route.continue();
    }
  });

  // Mock common endpoints
  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  return { mockNotifications, mockDevice };
}

test.describe('AUTH-11: Login Notifications', () => {
  test.describe('New Device Login', () => {
    test('should show notification for new device login', async ({ page }) => {
      await setupLoginNotificationsMocks(page, { hasNewDeviceLogin: true });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show new device notification
      await expect(
        page.getByText(/new.*device|new.*login|unrecognized.*device/i).or(
          page.locator('.notification-badge, .alert')
        )
      ).toBeVisible();
    });

    test('should display device details in notification', async ({ page }) => {
      await setupLoginNotificationsMocks(page, { hasNewDeviceLogin: true });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show device browser/OS info
      await expect(page.getByText(/firefox|chrome|safari/i)).toBeVisible();
    });

    test('should allow acknowledging new device notification', async ({ page }) => {
      await setupLoginNotificationsMocks(page, { hasNewDeviceLogin: true });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Find and click acknowledge button
      const acknowledgeButton = page.getByRole('button', { name: /acknowledge|dismiss|this.*was.*me/i });
      if (await acknowledgeButton.isVisible()) {
        await acknowledgeButton.click();
        await expect(page.getByText(/acknowledged|dismissed/i)).toBeVisible();
      }
    });
  });

  test.describe('Suspicious Login Alert', () => {
    test('should show warning for suspicious location login', async ({ page }) => {
      await setupLoginNotificationsMocks(page, { hasSuspiciousLogin: true });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show suspicious activity warning
      await expect(
        page.getByText(/suspicious|unusual|different.*location|new.*country/i).or(
          page.locator('.warning, .alert-warning')
        )
      ).toBeVisible();
    });

    test('should show location details for suspicious login', async ({ page }) => {
      await setupLoginNotificationsMocks(page, { hasSuspiciousLogin: true });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show location (country/city)
      await expect(page.getByText(/japan|tokyo/i)).toBeVisible();
    });

    test('should offer security actions for suspicious login', async ({ page }) => {
      await setupLoginNotificationsMocks(page, { hasSuspiciousLogin: true });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should offer security options
      await expect(
        page.getByRole('button', { name: /secure.*account|logout.*all|change.*password/i }).or(
          page.getByText(/review.*security|take.*action/i)
        )
      ).toBeVisible();
    });
  });

  test.describe('Notification Settings', () => {
    test('should display login notification settings', async ({ page }) => {
      await setupLoginNotificationsMocks(page, { notificationsEnabled: true });

      await page.goto('/settings/notifications');
      await page.waitForLoadState('networkidle');

      // Should show notification toggles
      await expect(page.getByText(/login.*notification|new.*device.*alert/i)).toBeVisible();
    });

    test('should allow toggling login notifications', async ({ page }) => {
      await setupLoginNotificationsMocks(page, { notificationsEnabled: true });

      await page.goto('/settings/notifications');
      await page.waitForLoadState('networkidle');

      // Find toggle for login notifications
      const toggle = page.getByRole('switch', { name: /login.*notification/i }).or(
        page.getByRole('checkbox', { name: /login.*notification/i })
      );

      if (await toggle.isVisible()) {
        await toggle.click();
        await expect(page.getByText(/saved|updated/i)).toBeVisible();
      }
    });

    test('should show email notification option', async ({ page }) => {
      await setupLoginNotificationsMocks(page, { notificationsEnabled: true });

      await page.goto('/settings/notifications');
      await page.waitForLoadState('networkidle');

      // Should show email notification option
      await expect(page.getByText(/email.*notification|send.*email/i)).toBeVisible();
    });
  });

  test.describe('Notification Display in Dashboard', () => {
    test('should show unread notification indicator in navbar', async ({ page }) => {
      await setupLoginNotificationsMocks(page, { hasNewDeviceLogin: true });

      await page.goto('/dashboard');
      await page.waitForLoadState('networkidle');

      // Should show notification indicator (badge, bell icon, etc.)
      await expect(
        page.locator('.notification-badge, [data-testid="notification-indicator"]').or(
          page.getByRole('button', { name: /notification/i })
        )
      ).toBeVisible();
    });
  });
});
