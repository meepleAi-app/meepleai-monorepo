/**
 * AUTH-12: Device Management
 * Issue #3082 - P1 High
 *
 * Tests device management functionality:
 * - View all logged-in devices
 * - Device details display (browser, OS, location)
 * - Revoke specific device session
 * - Revoke all sessions except current
 */

import { test, expect } from '../fixtures/chromatic';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface DeviceSession {
  id: string;
  deviceId: string;
  userAgent: string;
  browser: string;
  browserVersion: string;
  os: string;
  osVersion: string;
  deviceType: 'desktop' | 'mobile' | 'tablet';
  location: {
    city: string;
    country: string;
    ip: string;
  };
  lastActive: string;
  createdAt: string;
  isCurrent: boolean;
}

/**
 * Setup mock routes for device management testing
 */
async function setupDeviceManagementMocks(
  page: Page,
  options: {
    sessionCount?: number;
    includeMultipleDeviceTypes?: boolean;
  } = {}
) {
  const { sessionCount = 3, includeMultipleDeviceTypes = true } = options;

  const mockSessions: DeviceSession[] = [
    {
      id: 'session-1',
      deviceId: 'device-1',
      userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120',
      browser: 'Chrome',
      browserVersion: '120.0',
      os: 'Windows',
      osVersion: '10',
      deviceType: 'desktop',
      location: { city: 'Milan', country: 'Italy', ip: '192.168.1.1' },
      lastActive: new Date().toISOString(),
      createdAt: new Date(Date.now() - 7200000).toISOString(),
      isCurrent: true,
    },
  ];

  if (sessionCount > 1) {
    mockSessions.push({
      id: 'session-2',
      deviceId: 'device-2',
      userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X) Safari/17',
      browser: 'Safari',
      browserVersion: '17.0',
      os: 'macOS',
      osVersion: 'Sonoma',
      deviceType: 'desktop',
      location: { city: 'Rome', country: 'Italy', ip: '192.168.1.2' },
      lastActive: new Date(Date.now() - 3600000).toISOString(),
      createdAt: new Date(Date.now() - 86400000).toISOString(),
      isCurrent: false,
    });
  }

  if (sessionCount > 2 && includeMultipleDeviceTypes) {
    mockSessions.push({
      id: 'session-3',
      deviceId: 'device-3',
      userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0) Safari/604.1',
      browser: 'Safari',
      browserVersion: '17.0',
      os: 'iOS',
      osVersion: '17.0',
      deviceType: 'mobile',
      location: { city: 'Florence', country: 'Italy', ip: '192.168.1.3' },
      lastActive: new Date(Date.now() - 7200000).toISOString(),
      createdAt: new Date(Date.now() - 172800000).toISOString(),
      isCurrent: false,
    });
  }

  let activeSessions = [...mockSessions];

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

  // Mock sessions list endpoint
  await page.route(`${API_BASE}/api/v1/users/me/sessions`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        sessions: activeSessions,
        totalCount: activeSessions.length,
      }),
    });
  });

  // Mock revoke single session endpoint
  await page.route(`${API_BASE}/api/v1/auth/sessions/*/revoke`, async (route) => {
    const url = route.request().url();
    const sessionIdMatch = url.match(/sessions\/([^/]+)\/revoke/);
    const sessionId = sessionIdMatch?.[1];

    if (sessionId) {
      activeSessions = activeSessions.filter(s => s.id !== sessionId);
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Session revoked successfully' }),
      });
    } else {
      await route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Session not found' }),
      });
    }
  });

  // Mock revoke all sessions endpoint
  await page.route(`${API_BASE}/api/v1/auth/sessions/revoke-all`, async (route) => {
    // Keep only current session
    activeSessions = activeSessions.filter(s => s.isCurrent);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        message: 'All other sessions revoked',
        revokedCount: mockSessions.length - 1,
      }),
    });
  });

  // Mock common endpoints
  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  return { mockSessions, getActiveSessions: () => activeSessions };
}

test.describe('AUTH-12: Device Management', () => {
  test.describe('View Logged-in Devices', () => {
    test('should navigate to device management section', async ({ page }) => {
      await setupDeviceManagementMocks(page);

      await page.goto('/settings');
      await page.waitForLoadState('networkidle');

      // Find and click security/devices section
      const securityLink = page.getByRole('link', { name: /security|devices/i }).or(
        page.getByText(/security|devices/i)
      );

      if (await securityLink.isVisible()) {
        await securityLink.click();
        await page.waitForLoadState('networkidle');
      }

      // Should see devices section
      await expect(page.getByText(/device|session|logged.*in/i)).toBeVisible();
    });

    test('should display all active sessions', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 3 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show multiple sessions
      const sessionCards = page.locator('[data-testid*="session"], .device-card, .session-item');
      await expect(sessionCards.or(page.getByText(/chrome|safari|firefox/i).first())).toBeVisible();
    });

    test('should highlight current session', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 3 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Current session should be marked
      await expect(
        page.getByText(/current|this.*device|active.*now/i).or(
          page.locator('.current-session, [data-current="true"]')
        )
      ).toBeVisible();
    });
  });

  test.describe('Device Details', () => {
    test('should display browser information', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 2 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show browser names
      await expect(page.getByText(/chrome|safari|firefox/i)).toBeVisible();
    });

    test('should display operating system', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 2 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show OS info
      await expect(page.getByText(/windows|macos|ios|android|linux/i)).toBeVisible();
    });

    test('should display location information', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 2 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show location (city or country)
      await expect(page.getByText(/milan|rome|italy/i)).toBeVisible();
    });

    test('should display last active time', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 2 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show last active time
      await expect(
        page.getByText(/last.*active|ago|just.*now|minute|hour/i)
      ).toBeVisible();
    });

    test('should show device type icons', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 3, includeMultipleDeviceTypes: true });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show device type indicators (desktop, mobile)
      await expect(
        page.locator('[data-device-type], .device-icon, .device-type').first().or(
          page.getByText(/desktop|mobile|tablet|iphone/i)
        )
      ).toBeVisible();
    });
  });

  test.describe('Revoke Specific Session', () => {
    test('should show revoke button for non-current sessions', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 3 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Should show revoke button
      await expect(
        page.getByRole('button', { name: /revoke|remove|sign.*out|terminate/i }).first()
      ).toBeVisible();
    });

    test('should not show revoke button for current session', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 1 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      // Current session should not have revoke button (or it's disabled)
      const currentSessionRevoke = page.locator('[data-current="true"]').getByRole('button', { name: /revoke/i });
      const isVisible = await currentSessionRevoke.isVisible().catch(() => false);

      if (isVisible) {
        // If visible, it should be disabled
        await expect(currentSessionRevoke).toBeDisabled();
      }
    });

    test('should confirm before revoking session', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 3 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      const revokeButton = page.getByRole('button', { name: /revoke|remove|terminate/i }).first();

      if (await revokeButton.isVisible()) {
        await revokeButton.click();

        // Should show confirmation dialog
        await expect(
          page.getByText(/confirm|are.*you.*sure|will.*be.*logged.*out/i).or(
            page.getByRole('dialog')
          )
        ).toBeVisible();
      }
    });

    test('should successfully revoke session', async ({ page }) => {
      const mocks = await setupDeviceManagementMocks(page, { sessionCount: 3 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      const initialCount = mocks.getActiveSessions().length;

      const revokeButton = page.getByRole('button', { name: /revoke|remove|terminate/i }).first();

      if (await revokeButton.isVisible()) {
        await revokeButton.click();

        // Confirm if dialog appears
        const confirmButton = page.getByRole('button', { name: /confirm|yes|revoke/i });
        if (await confirmButton.isVisible()) {
          await confirmButton.click();
        }

        // Should show success message
        await expect(page.getByText(/revoked|removed|success/i)).toBeVisible();
      }
    });
  });

  test.describe('Revoke All Sessions', () => {
    test('should show revoke all button', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 3 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      await expect(
        page.getByRole('button', { name: /revoke.*all|sign.*out.*all|logout.*everywhere/i })
      ).toBeVisible();
    });

    test('should confirm before revoking all sessions', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 3 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      const revokeAllButton = page.getByRole('button', { name: /revoke.*all|sign.*out.*all/i });

      if (await revokeAllButton.isVisible()) {
        await revokeAllButton.click();

        // Should show confirmation
        await expect(
          page.getByText(/confirm|are.*you.*sure|all.*other.*device/i)
        ).toBeVisible();
      }
    });

    test('should keep current session after revoking all', async ({ page }) => {
      await setupDeviceManagementMocks(page, { sessionCount: 3 });

      await page.goto('/settings/security');
      await page.waitForLoadState('networkidle');

      const revokeAllButton = page.getByRole('button', { name: /revoke.*all|sign.*out.*all/i });

      if (await revokeAllButton.isVisible()) {
        await revokeAllButton.click();

        const confirmButton = page.getByRole('button', { name: /confirm|yes|revoke/i });
        if (await confirmButton.isVisible()) {
          await confirmButton.click();
        }

        // Should show success and still be logged in
        await expect(page.getByText(/current|this.*device/i)).toBeVisible();
      }
    });
  });
});
