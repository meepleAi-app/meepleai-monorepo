/**
 * SESS-07: Session Limits Enforcement
 * Issue #3082 - P0 Critical
 *
 * Tests session limits enforcement by tier:
 * - Free user cannot exceed 3 sessions
 * - Normal user cannot exceed 10 sessions
 * - Premium user has unlimited sessions
 * - Proper error messages and upgrade prompts
 */

import { test, expect } from '../fixtures';
import { GamePage, ProfilePage } from '../pages';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface SessionLimits {
  Free: number;
  Normal: number;
  Premium: number | 'unlimited';
}

const SESSION_LIMITS: SessionLimits = {
  Free: 3,
  Normal: 10,
  Premium: 'unlimited',
};

/**
 * Setup mock routes for session limits testing
 */
async function setupSessionLimitsMocks(
  page: Page,
  options: {
    tier?: 'Free' | 'Normal' | 'Premium';
    currentSessions?: number;
    allowCreate?: boolean;
  } = {}
) {
  const { tier = 'Free', currentSessions = 0, allowCreate = true } = options;
  const limit = SESSION_LIMITS[tier];
  const isAtLimit = typeof limit === 'number' && currentSessions >= limit;

  // Generate mock sessions
  const sessions = Array.from({ length: currentSessions }, (_, i) => ({
    id: `session-${i + 1}`,
    gameId: `game-${i + 1}`,
    gameName: `Test Game ${i + 1}`,
    status: 'active',
    createdAt: new Date(Date.now() - i * 3600000).toISOString(),
    playerCount: 2 + (i % 4),
  }));

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

  // Mock sessions list endpoint
  await page.route(`${API_BASE}/api/v1/game-sessions`, async (route) => {
    const method = route.request().method();

    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(sessions),
      });
    } else if (method === 'POST') {
      if (isAtLimit && !allowCreate) {
        await route.fulfill({
          status: 403,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Session limit reached',
            message: `You have reached your session limit (${limit}/${limit}). Upgrade to ${
              tier === 'Free' ? 'Normal for 10 sessions' : 'Premium for unlimited sessions'
            }.`,
            currentSessions,
            limit,
            tier,
          }),
        });
      } else {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: `session-${currentSessions + 1}`,
            gameId: 'new-game-id',
            gameName: 'New Game',
            status: 'active',
            createdAt: new Date().toISOString(),
            playerCount: 1,
          }),
        });
      }
    } else {
      await route.continue();
    }
  });

  // Mock session quota endpoint
  await page.route(`${API_BASE}/api/v1/users/me/session-quota`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        current: currentSessions,
        max: typeof limit === 'number' ? limit : 999,
        tier,
        unlimited: limit === 'unlimited',
      }),
    });
  });

  // Mock games endpoint
  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: 'game-1', title: 'Chess', description: 'Classic strategy game' },
        { id: 'game-2', title: 'Catan', description: 'Resource trading game' },
        { id: 'game-3', title: 'Ticket to Ride', description: 'Railway building game' },
      ]),
    });
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
        tier,
      }),
    });
  });

  return { sessions, limit, isAtLimit };
}

test.describe('SESS-07: Session Limits Enforcement', () => {
  test.describe('Free Tier (3 sessions)', () => {
    test('should allow creating session when under limit', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Free', currentSessions: 2, allowCreate: true });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Find and click create session button
      const createButton = page.getByRole('button', { name: /create.*session|new.*session|start/i });
      await expect(createButton).toBeVisible();
      await expect(createButton).toBeEnabled();
    });

    test('should show limit reached message at 3 sessions', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Free', currentSessions: 3, allowCreate: false });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should show limit reached indicator
      await expect(page.getByText(/limit.*reached|3.*\/.*3|full/i)).toBeVisible();
    });

    test('should prevent creating session when at limit', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Free', currentSessions: 3, allowCreate: false });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Create button should be disabled or show upgrade prompt
      const createButton = page.getByRole('button', { name: /create.*session|new.*session|start/i });

      if (await createButton.isVisible()) {
        // Either button is disabled
        const isDisabled = await createButton.isDisabled();
        if (!isDisabled) {
          // Or clicking shows error/upgrade modal
          await createButton.click();
          await expect(page.getByText(/limit.*reached|upgrade|cannot.*create/i)).toBeVisible();
        }
      }
    });

    test('should show upgrade to Normal prompt at limit', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Free', currentSessions: 3, allowCreate: false });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should show upgrade prompt
      await expect(page.getByText(/upgrade.*normal|10.*session/i)).toBeVisible();
    });
  });

  test.describe('Normal Tier (10 sessions)', () => {
    test('should display 10 session limit for Normal tier', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Normal', currentSessions: 5 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should show quota with 10 limit
      await expect(page.getByText(/5.*\/.*10|5.*of.*10/i)).toBeVisible();
    });

    test('should allow creating session when under limit', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Normal', currentSessions: 9, allowCreate: true });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      const createButton = page.getByRole('button', { name: /create.*session|new.*session|start/i });
      await expect(createButton).toBeEnabled();
    });

    test('should show limit reached at 10 sessions', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Normal', currentSessions: 10, allowCreate: false });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      await expect(page.getByText(/limit.*reached|10.*\/.*10/i)).toBeVisible();
    });

    test('should show upgrade to Premium prompt at limit', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Normal', currentSessions: 10, allowCreate: false });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      await expect(page.getByText(/upgrade.*premium|unlimited/i)).toBeVisible();
    });
  });

  test.describe('Premium Tier (Unlimited)', () => {
    test('should show unlimited sessions indicator', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Premium', currentSessions: 20 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      await expect(page.getByText(/unlimited|∞|premium/i)).toBeVisible();
    });

    test('should always allow creating sessions', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Premium', currentSessions: 50, allowCreate: true });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      const createButton = page.getByRole('button', { name: /create.*session|new.*session|start/i });
      await expect(createButton).toBeEnabled();
    });

    test('should not show upgrade prompts', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Premium', currentSessions: 100 });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // No upgrade button should be visible
      await expect(page.getByRole('button', { name: /upgrade/i })).not.toBeVisible();
    });
  });

  test.describe('Error Handling', () => {
    test('should handle session creation failure gracefully', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Free', currentSessions: 2, allowCreate: true });

      // Override to simulate server error
      await page.route(`${API_BASE}/api/v1/game-sessions`, async (route) => {
        const method = route.request().method();
        if (method === 'POST') {
          await route.fulfill({
            status: 500,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Internal server error' }),
          });
        } else {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify([]),
          });
        }
      });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      const createButton = page.getByRole('button', { name: /create.*session|new.*session|start/i });
      if (await createButton.isVisible()) {
        await createButton.click();

        // Should show error message
        await expect(page.getByText(/error|failed|try.*again/i)).toBeVisible();
      }
    });

    test('should show specific error for concurrent session limit race', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Free', currentSessions: 2, allowCreate: true });

      // Override to simulate race condition (another device created session)
      await page.route(`${API_BASE}/api/v1/game-sessions`, async (route) => {
        const method = route.request().method();
        if (method === 'POST') {
          await route.fulfill({
            status: 403,
            contentType: 'application/json',
            body: JSON.stringify({
              error: 'Session limit reached',
              message: 'Session limit was reached while processing your request.',
            }),
          });
        } else {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify([]),
          });
        }
      });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      const createButton = page.getByRole('button', { name: /create.*session|new.*session|start/i });
      if (await createButton.isVisible()) {
        await createButton.click();

        // Should show limit reached message
        await expect(page.getByText(/limit.*reached/i)).toBeVisible();
      }
    });
  });

  test.describe('Session Management at Limit', () => {
    test('should allow ending session when at limit', async ({ page }) => {
      await setupSessionLimitsMocks(page, { tier: 'Free', currentSessions: 3 });

      // Mock session end endpoint
      await page.route(`${API_BASE}/api/v1/game-sessions/*/end`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ message: 'Session ended' }),
        });
      });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Should be able to end/delete a session
      const endButton = page.getByRole('button', { name: /end|close|delete/i }).first();
      if (await endButton.isVisible()) {
        await expect(endButton).toBeEnabled();
      }
    });
  });
});
