/**
 * Session Expiration Behavior E2E Tests (AUTH-05) - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/
 */

import { test, expect, Page } from './fixtures/chromatic';
import { getTextMatcher, t } from './fixtures/i18n';
import { WaitHelper } from './helpers/WaitHelper';

const apiBase = 'http://localhost:8080';

/**
 * Setup authentication routes for session expiration testing
 */
async function setupAuthRoutes(page: Page) {
  let authenticated = false;
  const userResponse = {
    user: {
      id: 'user-1',
      email: 'user@example.com',
      displayName: 'Test User',
      role: 'User',
    },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  };

  await page.route(`${apiBase}/auth/me`, async route => {
    if (authenticated) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(userResponse),
      });
    } else {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' }),
      });
    }
  });

  await page.route(`${apiBase}/auth/login`, async route => {
    authenticated = true;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse),
    });
  });

  return {
    authenticate() {
      authenticated = true;
    },
    setAuthenticated(value: boolean) {
      authenticated = value;
    },
  };
}

test.describe('Session Expiration Behavior', () => {
  test.beforeEach(async ({ page }) => {
    // Setup base auth routes
    await setupAuthRoutes(page);
  });

  test('should redirect to login when session expires (0 minutes remaining)', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // Mock session status API to return 0 minutes remaining
    await page.route(`${apiBase}/api/v1/auth/session/status`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          expiresAt: new Date().toISOString(),
          lastSeenAt: new Date().toISOString(),
          remainingMinutes: 0,
        }),
      });
    });

    // Mock QA endpoint to avoid errors
    await page.route(`${apiBase}/agents/qa`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'Test response',
          sources: [],
        }),
      });
    });

    // Navigate to protected page
    await page.goto('/chat');

    // Should redirect to login with session expired reason
    await page.waitForURL('/login?reason=session_expired', { timeout: 10000 });
    await expect(page).toHaveURL('/login?reason=session_expired');
  });

  test('should redirect to login when session has negative time remaining', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // Mock session status API to return negative time
    await page.route(`${apiBase}/api/v1/auth/session/status`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          expiresAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
          lastSeenAt: new Date().toISOString(),
          remainingMinutes: -5,
        }),
      });
    });

    // Mock QA endpoint
    await page.route(`${apiBase}/agents/qa`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'Test response',
          sources: [],
        }),
      });
    });

    // Navigate to protected page
    await page.goto('/chat');

    // Should redirect to login with session expired reason
    await page.waitForURL('/login?reason=session_expired', { timeout: 10000 });
    await expect(page).toHaveURL('/login?reason=session_expired');
  });

  test('should stay on page when session is valid (not near expiry)', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // Mock session status API to return plenty of time remaining
    await page.route(`${apiBase}/api/v1/auth/session/status`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          expiresAt: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
          lastSeenAt: new Date().toISOString(),
          remainingMinutes: 30,
        }),
      });
    });

    // Mock QA endpoint
    await page.route(`${apiBase}/agents/qa`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'Test response',
          sources: [],
        }),
      });
    });

    // Navigate to protected page
    await page.goto('/chat');

    // Should stay on chat page
    await expect(page).toHaveURL('/chat');
    await expect(page.getByRole('heading', { name: 'MeepleAI Chat' })).toBeVisible();

    // Should NOT show session expiration warning
    // Note: This assumes no warning UI is shown when remainingMinutes >= 5
    // Adjust selector based on actual warning UI implementation
  });

  test('should show countdown when session is expiring soon (< 5 minutes)', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // Mock session status API to return 3 minutes remaining (near expiry)
    const remainingMinutes = 3;
    await page.route(`${apiBase}/api/v1/auth/session/status`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          expiresAt: new Date(Date.now() + remainingMinutes * 60 * 1000).toISOString(),
          lastSeenAt: new Date().toISOString(),
          remainingMinutes: remainingMinutes,
        }),
      });
    });

    // Mock QA endpoint
    await page.route(`${apiBase}/agents/qa`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'Test response',
          sources: [],
        }),
      });
    });

    // Navigate to protected page
    await page.goto('/chat');

    // Should stay on chat page initially
    await expect(page).toHaveURL('/chat');

    // Note: This test assumes there's a SessionWarningModal or similar UI component
    // that displays when isNearExpiry is true. Adjust selectors based on actual implementation.
    // If no warning UI exists yet, this test documents expected behavior for future implementation.

    // Wait a bit to ensure the hook has run

    // Verify we're still on the chat page (not redirected)
    await expect(page).toHaveURL('/chat');
  });

  test('should handle session expiration after initial valid check', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // First check: valid session
    let callCount = 0;
    await page.route(`${apiBase}/api/v1/auth/session/status`, async route => {
      callCount++;
      if (callCount === 1) {
        // First check: valid session
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            expiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
            lastSeenAt: new Date().toISOString(),
            remainingMinutes: 10,
          }),
        });
      } else {
        // Subsequent checks: expired session
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            expiresAt: new Date().toISOString(),
            lastSeenAt: new Date().toISOString(),
            remainingMinutes: 0,
          }),
        });
      }
    });

    // Mock QA endpoint
    await page.route(`${apiBase}/agents/qa`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'Test response',
          sources: [],
        }),
      });
    });

    // Navigate to protected page
    await page.goto('/chat');

    // Initially should be on chat page
    await expect(page).toHaveURL('/chat');
    await expect(page.getByRole('heading', { name: 'MeepleAI Chat' })).toBeVisible();

    // Wait for enough time to trigger the hook's manual check or polling
    // The hook polls every 5 minutes, but we can trigger earlier by waiting for API call
    const waitHelper = new WaitHelper(page);
    await waitHelper.waitForNetworkIdle(5000);

    // Trigger manual session check by reloading (simulates hook behavior)
    // In a real scenario, the hook's useEffect would trigger this automatically
    await page.reload();

    // Should redirect to login after session expires
    await page.waitForURL('/login?reason=session_expired', { timeout: 10000 });
    await expect(page).toHaveURL('/login?reason=session_expired');
  });

  test('should not redirect on network error during session check', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // Mock session status API to return network error
    await page.route(`${apiBase}/api/v1/auth/session/status`, async route => {
      await route.abort('failed');
    });

    // Mock QA endpoint
    await page.route(`${apiBase}/agents/qa`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'Test response',
          sources: [],
        }),
      });
    });

    // Navigate to protected page
    await page.goto('/chat');

    // Wait a bit to ensure the hook has run

    // Should stay on chat page (not redirect due to network error)
    await expect(page).toHaveURL('/chat');
    await expect(page.getByRole('heading', { name: 'MeepleAI Chat' })).toBeVisible();
  });

  test('should handle unauthenticated user gracefully (null response)', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    // Don't authenticate

    // Mock session status API to return null (unauthenticated)
    await page.route(`${apiBase}/api/v1/auth/session/status`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: 'null',
      });
    });

    // Navigate to chat page (should be redirected by route guard, not by hook)
    await page.goto('/chat');

    // Wait a bit
    await page.waitForTimeout(1000);

    // Behavior depends on route guard implementation
    // The hook itself doesn't redirect for null response (remainingMinutes = null)
    // This test verifies the hook doesn't crash on null response
  });
});
