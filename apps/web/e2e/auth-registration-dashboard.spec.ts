/**
 * Auth Registration → Dashboard Flow Tests
 *
 * Issue #XXXX: After registration, dashboard shows "Nessun Utente" error
 *
 * These tests verify the registration-to-dashboard flow works correctly.
 * The bug was caused by:
 * 1. Desync between useAuth (local useState) and useCurrentUser (TanStack Query)
 * 2. refetchOnMount: false preventing data refresh after registration
 *
 * IMPORTANT: Uses data-testid for language-independent selectors
 */

import { test, expect } from './fixtures/chromatic';
import { AuthHelper } from './pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Registration → Dashboard Flow', () => {
  test('should show user content after registration, not "Nessun Utente" error', async ({
    page,
  }) => {
    const authHelper = new AuthHelper(page);
    const newUser = {
      id: '550e8400-e29b-41d4-a716-446655440001',
      email: 'newuser@test.com',
      displayName: 'New Test User',
      role: 'User' as const,
    };

    // 1. Start unauthenticated
    await authHelper.mockUnauthenticatedSession();

    // 2. Mock registration endpoint (returns user wrapped in { user: ... })
    await page.route(`${apiBase}/api/v1/auth/register`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: {
            id: newUser.id,
            email: newUser.email,
            displayName: newUser.displayName,
            role: newUser.role,
          },
        }),
      });
    });

    // 3. After registration succeeds, auth/me should return the user
    // This simulates what happens after server sets the session cookie
    let isRegistered = false;
    await page.route(`${apiBase}/api/v1/auth/me`, async route => {
      if (isRegistered) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            user: newUser,
            expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          }),
        });
      } else {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Unauthorized' }),
        });
      }
    });

    // 4. Mock games endpoint for dashboard
    await page.route(`${apiBase}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          totalCount: 0,
          pageNumber: 1,
          pageSize: 6,
          totalPages: 0,
        }),
      });
    });

    // 5. Navigate to home page and wait for it to fully load
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // 6. Click the "Get Started" CTA - using data-testid for language independence
    // Note: Button with asChild renders as <a> tag (Link)
    const ctaButton = page.locator('[data-testid="get-started-button"]').first();
    await expect(ctaButton).toBeVisible({ timeout: 10000 });
    await ctaButton.click();

    // 7. Wait for /register page to load (shows AuthModal with defaultMode="register")
    await page.waitForURL('**/register', { timeout: 10000 });
    await expect(page.locator('[data-testid="auth-modal"]')).toBeVisible();
    // Note: defaultMode="register" means register tab is already selected

    // 8. Fill registration form using data-testid
    await page.locator('[data-testid="register-email"]').fill('newuser@test.com');
    await page.locator('[data-testid="register-password"]').fill('TestPassword123!');
    await page.locator('[data-testid="register-confirm-password"]').fill('TestPassword123!');

    // Display name is optional, but fill it if present
    const displayNameInput = page.locator('[data-testid="register-display-name"]');
    if (await displayNameInput.isVisible({ timeout: 500 }).catch(() => false)) {
      await displayNameInput.fill('New Test User');
    }

    // 9. Mark as registered BEFORE clicking submit
    // This simulates server setting the session cookie
    isRegistered = true;

    // Also set cookies to simulate authenticated state
    await page.context().addCookies([
      {
        name: 'meepleai_session',
        value: 'mock-session-token-after-register',
        domain: 'localhost',
        path: '/',
        httpOnly: true,
        secure: false,
        sameSite: 'Lax',
      },
      {
        name: 'meepleai_user_role',
        value: 'user',
        domain: 'localhost',
        path: '/',
        httpOnly: false,
        secure: false,
        sameSite: 'Lax',
      },
    ]);

    // 10. Submit registration
    await page.locator('[data-testid="register-submit"]').click();

    // 11. Wait for redirect to dashboard
    await page.waitForURL('**/dashboard', { timeout: 10000 });

    // 12. CRITICAL ASSERTION: Dashboard should show user greeting, NOT error
    // This is the bug we're testing - after registration, dashboard should show user data
    const errorAlert = page.locator('[data-testid="dashboard-no-user-error"]');
    const userGreeting = page.locator('[data-testid="dashboard-greeting"]');

    // Should NOT show "Nessun Utente" error
    await expect(errorAlert).not.toBeVisible({ timeout: 5000 });

    // Should show user greeting
    await expect(userGreeting).toBeVisible({ timeout: 5000 });
  });

  test('should correctly hydrate user data on dashboard after registration redirect', async ({
    page,
  }) => {
    const authHelper = new AuthHelper(page);
    const newUser = {
      id: '550e8400-e29b-41d4-a716-446655440002',
      email: 'hydration@test.com',
      displayName: 'Hydration Test User',
      role: 'User' as const,
    };

    // 1. Mock authenticated state (simulates post-registration)
    await authHelper.mockAuthenticatedSession(newUser);

    // 2. Mock games endpoint
    await page.route(`${apiBase}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          totalCount: 0,
          pageNumber: 1,
          pageSize: 6,
          totalPages: 0,
        }),
      });
    });

    // 3. Navigate directly to dashboard (simulating redirect after registration)
    await page.goto('/dashboard');

    // 4. Verify user data is displayed
    const userGreeting = page.locator('[data-testid="dashboard-greeting"]');
    await expect(userGreeting).toBeVisible({ timeout: 5000 });

    // 5. Verify no error is shown
    const errorAlert = page.locator('[data-testid="dashboard-no-user-error"]');
    await expect(errorAlert).not.toBeVisible();
  });

  test('should invalidate TanStack Query cache after registration', async ({ page }) => {
    const authHelper = new AuthHelper(page);
    const newUser = {
      id: '550e8400-e29b-41d4-a716-446655440003',
      email: 'cache@test.com',
      displayName: 'Cache Test User',
      role: 'User' as const,
    };

    // Track API calls to verify cache invalidation
    let getMeCalls = 0;

    // 1. Start with stale cache state (user was previously logged out)
    await page.route(`${apiBase}/api/v1/auth/me`, async route => {
      getMeCalls++;
      // After first call, return authenticated user
      if (getMeCalls >= 2) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            user: newUser,
            expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          }),
        });
      } else {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Unauthorized' }),
        });
      }
    });

    // 2. Mock registration (response wrapped in { user: ... })
    await page.route(`${apiBase}/api/v1/auth/register`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ user: newUser }),
      });
    });

    // 3. Mock games
    await page.route(`${apiBase}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          games: [],
          totalCount: 0,
          pageNumber: 1,
          pageSize: 6,
          totalPages: 0,
        }),
      });
    });

    // 4. Navigate and register
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const ctaButton = page.locator('[data-testid="get-started-button"]').first();
    await expect(ctaButton).toBeVisible({ timeout: 10000 });
    await ctaButton.click();
    // Wait for /register page to load (shows AuthModal with defaultMode="register")
    await page.waitForURL('**/register', { timeout: 10000 });
    await expect(page.locator('[data-testid="auth-modal"]')).toBeVisible();
    // Note: defaultMode="register" means register tab is already selected

    await page.locator('[data-testid="register-email"]').fill('cache@test.com');
    await page.locator('[data-testid="register-password"]').fill('TestPassword123!');
    await page.locator('[data-testid="register-confirm-password"]').fill('TestPassword123!');

    // Set cookies before submit
    await page.context().addCookies([
      {
        name: 'meepleai_session',
        value: 'mock-session-token',
        domain: 'localhost',
        path: '/',
        httpOnly: true,
        secure: false,
        sameSite: 'Lax',
      },
    ]);

    await page.locator('[data-testid="register-submit"]').click();

    // 5. Wait for dashboard
    await page.waitForURL('**/dashboard', { timeout: 10000 });

    // 6. Give time for potential refetch
    await page.waitForTimeout(1000);

    // 7. CRITICAL: auth/me should have been called again after registration
    // If cache was properly invalidated, getMeCalls should be >= 2
    expect(getMeCalls).toBeGreaterThanOrEqual(2);

    // 8. Dashboard should show user, not error
    await expect(page.locator('[data-testid="dashboard-greeting"]')).toBeVisible();
    await expect(page.locator('[data-testid="dashboard-no-user-error"]')).not.toBeVisible();
  });
});
