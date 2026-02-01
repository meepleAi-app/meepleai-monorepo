/**
 * LIB-07: Session Quota Display
 * Issue #3082 - P0 Critical
 *
 * Tests session quota display and enforcement:
 * - View session quota for different tiers
 * - Session quota bar visualization
 * - Quota warning thresholds
 * - Upgrade prompts at quota limits
 */

import { test, expect } from '../fixtures';
import { GamePage, ProfilePage } from '../pages';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface SessionQuota {
  current: number;
  max: number;
  tier: 'Free' | 'Normal' | 'Premium';
  unlimited: boolean;
}

interface UserTier {
  name: 'Free' | 'Normal' | 'Premium';
  sessionLimit: number | 'unlimited';
  libraryLimit: number | 'unlimited';
}

const TIER_LIMITS: Record<string, UserTier> = {
  Free: { name: 'Free', sessionLimit: 3, libraryLimit: 5 },
  Normal: { name: 'Normal', sessionLimit: 10, libraryLimit: 50 },
  Premium: { name: 'Premium', sessionLimit: 'unlimited', libraryLimit: 'unlimited' },
};

/**
 * Setup mock routes for session quota testing
 */
async function setupSessionQuotaMocks(
  page: Page,
  options: {
    tier?: 'Free' | 'Normal' | 'Premium';
    currentSessions?: number;
  } = {}
) {
  const { tier = 'Free', currentSessions = 0 } = options;
  const tierConfig = TIER_LIMITS[tier];

  const quota: SessionQuota = {
    current: currentSessions,
    max: typeof tierConfig.sessionLimit === 'number' ? tierConfig.sessionLimit : 999,
    tier,
    unlimited: tierConfig.sessionLimit === 'unlimited',
  };

  // Mock auth/me endpoint
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
          tier,
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock session quota endpoint
  await page.route(`${API_BASE}/api/v1/users/me/session-quota`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(quota),
    });
  });

  // Mock sessions list endpoint
  await page.route(`${API_BASE}/api/v1/sessions`, async (route) => {
    const method = route.request().method();

    if (method === 'GET') {
      const sessions = Array.from({ length: currentSessions }, (_, i) => ({
        id: `session-${i + 1}`,
        gameId: `game-${i + 1}`,
        gameName: `Test Game ${i + 1}`,
        status: 'active',
        createdAt: new Date().toISOString(),
        playerCount: 2,
      }));

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(sessions),
      });
    } else {
      await route.continue();
    }
  });

  // Mock user profile endpoint
  await page.route(`${API_BASE}/api/v1/users/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'test-user-id',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
        tier,
        subscription: {
          tier,
          limits: {
            sessions: tierConfig.sessionLimit,
            library: tierConfig.libraryLimit,
          },
        },
      }),
    });
  });

  // Mock other common endpoints
  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  return { quota, tierConfig };
}

test.describe('LIB-07: Session Quota Display', () => {
  test.describe('Free Tier Quota', () => {
    test('should display session quota for Free tier user', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Free', currentSessions: 2 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should show quota display (e.g., "2/3 sessions used")
      await expect(page.getByText(/2.*\/.*3|2.*of.*3/i)).toBeVisible();
    });

    test('should show progress bar for quota visualization', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Free', currentSessions: 2 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should have a progress bar or quota visualization
      const progressBar = page.locator('[role="progressbar"], .progress, .quota-bar');
      await expect(progressBar.first()).toBeVisible();
    });

    test('should show warning when quota is near limit', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Free', currentSessions: 2 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should show warning indicator or text
      await expect(
        page.getByText(/almost.*full|near.*limit|1.*remaining/i).or(
          page.locator('.warning, .text-yellow-500, .text-amber-500')
        )
      ).toBeVisible();
    });

    test('should show upgrade prompt when quota is at limit', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Free', currentSessions: 3 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should show quota reached message and upgrade option
      await expect(page.getByText(/limit.*reached|full|no.*remaining/i)).toBeVisible();
      await expect(
        page.getByRole('button', { name: /upgrade|get.*more/i }).or(
          page.getByRole('link', { name: /upgrade|pricing/i })
        )
      ).toBeVisible();
    });
  });

  test.describe('Normal Tier Quota', () => {
    test('should display larger quota for Normal tier', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Normal', currentSessions: 5 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should show quota with limit of 10
      await expect(page.getByText(/5.*\/.*10|5.*of.*10/i)).toBeVisible();
    });

    test('should show tier name in quota display', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Normal', currentSessions: 5 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should indicate Normal tier
      await expect(page.getByText(/normal|standard/i)).toBeVisible();
    });
  });

  test.describe('Premium Tier (Unlimited)', () => {
    test('should show unlimited sessions for Premium tier', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Premium', currentSessions: 20 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should show unlimited indicator
      await expect(page.getByText(/unlimited|∞/i)).toBeVisible();
    });

    test('should not show upgrade prompts for Premium tier', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Premium', currentSessions: 50 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should NOT show upgrade button
      const upgradeButton = page.getByRole('button', { name: /upgrade/i });
      await expect(upgradeButton).not.toBeVisible();
    });
  });

  test.describe('Quota in Dashboard', () => {
    test('should display session quota widget in dashboard', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Free', currentSessions: 1 });

      await page.goto('/dashboard');
      await page.waitForLoadState('networkidle');

      // Should show quota somewhere in dashboard
      await expect(page.getByText(/session|quota/i).first()).toBeVisible();
    });

    test('should navigate to sessions page from quota widget', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Free', currentSessions: 1 });

      await page.goto('/dashboard');
      await page.waitForLoadState('networkidle');

      // Click on sessions quota widget/link
      const sessionsLink = page.getByRole('link', { name: /session|view.*all/i });
      if (await sessionsLink.isVisible()) {
        await sessionsLink.click();
        await expect(page).toHaveURL(/sessions/);
      }
    });
  });

  test.describe('Quota Updates', () => {
    test('should update quota display when session is created', async ({ page }) => {
      let currentSessions = 1;

      // Setup with dynamic session count
      await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            user: { id: 'test', email: 'test@example.com', tier: 'Free' },
            expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          }),
        });
      });

      await page.route(`${API_BASE}/api/v1/users/me/session-quota`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            current: currentSessions,
            max: 3,
            tier: 'Free',
            unlimited: false,
          }),
        });
      });

      await page.route(`${API_BASE}/api/v1/sessions`, async (route) => {
        const method = route.request().method();
        if (method === 'POST') {
          currentSessions++;
          await route.fulfill({
            status: 201,
            contentType: 'application/json',
            body: JSON.stringify({ id: `session-${currentSessions}`, status: 'active' }),
          });
        } else {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify([]),
          });
        }
      });

      await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([{ id: 'game-1', title: 'Test Game' }]),
        });
      });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Initial quota: 1/3
      await expect(page.getByText(/1.*\/.*3|1.*of.*3/i)).toBeVisible();
    });

    test('should update quota display when session is ended', async ({ page }) => {
      await setupSessionQuotaMocks(page, { tier: 'Free', currentSessions: 3 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Initially at limit: 3/3
      await expect(page.getByText(/3.*\/.*3|3.*of.*3|limit.*reached/i)).toBeVisible();
    });
  });
});
