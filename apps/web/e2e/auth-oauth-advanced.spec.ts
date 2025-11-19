import { test, expect } from '@playwright/test';
import { AuthPage } from './pages/auth/AuthPage';

const API_BASE = 'http://localhost:5080';

/**
 * E2E Tests for OAuth Advanced Scenarios (Issue #843 Phase 3)
 *
 * Test Coverage:
 * - Link multiple OAuth providers
 * - Unlink OAuth provider from profile
 * - Cannot unlink last authentication method
 * - OAuth account conflict (email already exists)
 * - Link OAuth to existing account
 * - OAuth callback error handling
 * - Provider button states (linked/unlinked)
 * - Re-link previously unlinked provider
 * - OAuth with existing session (upgrade account)
 * - Profile page shows all linked accounts
 * - OAuth token refresh handling
 * - Session persistence after OAuth login
 *
 * Testing Strategy:
 * - Use Page Object Model (AuthPage with extended OAuth methods)
 * - Mock OAuth endpoints and callbacks
 * - Test account linking, unlinking, and conflict scenarios
 * - Verify last auth method protection
 * - Test session persistence and token handling
 *
 * Expected Pass Rate: 80%+ (10/12 tests passing)
 */

test.describe('OAuth Advanced Scenarios', () => {
  let authPage: AuthPage;

  test.beforeEach(async ({ page }) => {
    authPage = new AuthPage(page);

    // Disable animations for stable tests
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Mock authentication endpoint
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
        }),
      });
    });
  });

  /**
   * Test Group 1: Linking Multiple Providers
   */
  test.describe('Linking Multiple Providers', () => {
    test('link multiple OAuth providers (Google + Discord)', async ({ page }) => {
      // Mock OAuth accounts endpoint - initially no linked accounts
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });

      await authPage.gotoProfile();

      // Verify initially no providers linked
      const initialCount = await authPage.getLinkedProvidersCount();
      expect(initialCount).toBe(0);

      // Mock Google OAuth link flow
      await page.route(`${API_BASE}/api/v1/auth/oauth/google/login`, async (route) => {
        await route.fulfill({
          status: 302,
          headers: {
            Location: 'https://accounts.google.com/o/oauth2/v2/auth?state=mock-state',
          },
        });
      });

      // Mock Google callback success
      await page.route(`${API_BASE}/api/v1/auth/oauth/google/callback*`, async (route) => {
        await route.fulfill({
          status: 302,
          headers: {
            Location: '/profile?oauth_success=google',
          },
        });
      });

      // Update mock to show Google linked
      await page.unroute(`${API_BASE}/api/v1/users/me/oauth-accounts`);
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { provider: 'google', createdAt: new Date().toISOString() },
          ]),
        });
      });

      await authPage.clickLinkProvider('google');
      await page.waitForURL('**/profile?oauth_success=google');
      await authPage.gotoProfile(); // Reload to see updated state

      // Verify Google is linked
      await authPage.assertProviderLinked('google');

      // Now link Discord
      await page.route(`${API_BASE}/api/v1/auth/oauth/discord/login`, async (route) => {
        await route.fulfill({
          status: 302,
          headers: {
            Location: 'https://discord.com/api/oauth2/authorize?state=mock-state',
          },
        });
      });

      await page.route(`${API_BASE}/api/v1/auth/oauth/discord/callback*`, async (route) => {
        await route.fulfill({
          status: 302,
          headers: {
            Location: '/profile?oauth_success=discord',
          },
        });
      });

      // Update mock to show both providers
      await page.unroute(`${API_BASE}/api/v1/users/me/oauth-accounts`);
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { provider: 'google', createdAt: new Date().toISOString() },
            { provider: 'discord', createdAt: new Date().toISOString() },
          ]),
        });
      });

      await authPage.clickLinkProvider('discord');
      await page.waitForURL('**/profile?oauth_success=discord');
      await authPage.gotoProfile();

      // Verify both providers linked
      await authPage.assertProviderLinked('google');
      await authPage.assertProviderLinked('discord');
      const finalCount = await authPage.getLinkedProvidersCount();
      expect(finalCount).toBe(2);
    });
  });

  /**
   * Test Group 2: Unlinking Providers
   */
  test.describe('Unlinking Providers', () => {
    test('unlink OAuth provider from profile', async ({ page }) => {
      // Mock OAuth accounts endpoint - Google linked
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { provider: 'google', createdAt: new Date().toISOString() },
            { provider: 'discord', createdAt: new Date().toISOString() },
          ]),
        });
      });

      await authPage.gotoProfile();

      // Verify Google is linked
      await authPage.assertProviderLinked('google');

      // Mock unlink endpoint
      await page.route(`${API_BASE}/api/v1/auth/oauth/google/unlink`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      });

      // Update mock to show Google unlinked
      await page.unroute(`${API_BASE}/api/v1/users/me/oauth-accounts`);
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { provider: 'discord', createdAt: new Date().toISOString() },
          ]),
        });
      });

      await authPage.clickUnlinkProvider('google');

      // Wait for UI update
      await page.waitForTimeout(1000);

      // Verify Google is no longer linked
      await authPage.assertProviderNotLinked('google');
      await authPage.assertProviderLinked('discord');
    });

    test('cannot unlink last authentication method', async ({ page }) => {
      // Mock OAuth accounts endpoint - only Google linked
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { provider: 'google', createdAt: new Date().toISOString() },
          ]),
        });
      });

      await authPage.gotoProfile();

      // Mock unlink endpoint to return error
      await page.route(`${API_BASE}/api/v1/auth/oauth/google/unlink`, async (route) => {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Cannot unlink last authentication method',
          }),
        });
      });

      // Attempt to unlink (should show error or button disabled)
      const unlinkButton = page
        .getByRole('button', { name: /google/i })
        .filter({ hasText: /unlink/i });

      // Check if button is disabled or shows warning
      const isDisabled = await unlinkButton.isDisabled();
      if (!isDisabled) {
        page.once('dialog', (dialog) => dialog.accept());
        await unlinkButton.click();
        // Should show error message
        await expect(page.getByText(/cannot unlink|last authentication/i)).toBeVisible();
      }

      // Google should still be linked
      await authPage.assertProviderLinked('google');
    });
  });

  /**
   * Test Group 3: OAuth Conflicts
   */
  test.describe('OAuth Conflicts', () => {
    test('OAuth account conflict (email already exists)', async ({ page }) => {
      await authPage.goto();

      // Mock OAuth callback with conflict error
      await page.route(`${API_BASE}/api/v1/auth/oauth/google/callback*`, async (route) => {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'An account with this email already exists',
          }),
        });
      });

      // Navigate to callback URL with error
      await page.goto('/auth/callback?error=email_conflict&provider=google');

      // Should show error message
      await authPage.waitForOAuthCallbackError();
    });

    test('link OAuth to existing account', async ({ page }) => {
      // Mock OAuth accounts endpoint - initially no linked accounts
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });

      await authPage.gotoProfile();

      // Mock OAuth link callback
      await page.route(`${API_BASE}/api/v1/auth/oauth/github/callback*`, async (route) => {
        await route.fulfill({
          status: 302,
          headers: {
            Location: '/profile?oauth_success=github',
          },
        });
      });

      // Update mock to show GitHub linked
      await page.unroute(`${API_BASE}/api/v1/users/me/oauth-accounts`);
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { provider: 'github', createdAt: new Date().toISOString() },
          ]),
        });
      });

      await authPage.clickLinkProvider('github');
      await page.waitForURL('**/profile?oauth_success=github');
      await authPage.gotoProfile();

      // Verify GitHub is now linked to existing account
      await authPage.assertProviderLinked('github');
    });
  });

  /**
   * Test Group 4: OAuth Callback Handling
   */
  test.describe('OAuth Callback Handling', () => {
    test('OAuth callback error handling', async ({ page }) => {
      // Navigate to callback with error
      await page.goto('/auth/callback?error=access_denied&provider=google');

      // Should show error message and provide option to retry
      await expect(page.getByText(/failed to link|error|access denied/i)).toBeVisible();
      await expect(page.getByRole('link', { name: /try again|return to profile/i })).toBeVisible();
    });

    test('OAuth callback success redirects to profile', async ({ page }) => {
      // Mock successful callback
      await page.goto('/auth/callback?code=mock-auth-code&state=mock-state&provider=google');

      // Should redirect to profile with success message
      await page.waitForURL('**/profile');
      // Success message might be transient, so we just check we're on profile
      expect(page.url()).toContain('/profile');
    });
  });

  /**
   * Test Group 5: Provider Button States
   */
  test.describe('Provider Button States', () => {
    test('provider button states (linked/unlinked)', async ({ page }) => {
      // Mock OAuth accounts endpoint - Google linked, others not
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { provider: 'google', createdAt: new Date().toISOString() },
          ]),
        });
      });

      await authPage.gotoProfile();

      // Google should show "Unlink" button
      await authPage.assertProviderLinked('google');

      // Discord and GitHub should show "Link" buttons
      await authPage.assertProviderNotLinked('discord');
      await authPage.assertProviderNotLinked('github');
    });

    test('re-link previously unlinked provider', async ({ page }) => {
      // Mock OAuth accounts endpoint - no linked accounts (previously unlinked)
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });

      await authPage.gotoProfile();

      // Verify Google is not linked
      await authPage.assertProviderNotLinked('google');

      // Mock re-link flow
      await page.route(`${API_BASE}/api/v1/auth/oauth/google/callback*`, async (route) => {
        await route.fulfill({
          status: 302,
          headers: {
            Location: '/profile?oauth_success=google',
          },
        });
      });

      // Update mock to show Google linked again
      await page.unroute(`${API_BASE}/api/v1/users/me/oauth-accounts`);
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { provider: 'google', createdAt: new Date().toISOString() },
          ]),
        });
      });

      await authPage.clickLinkProvider('google');
      await page.waitForURL('**/profile?oauth_success=google');
      await authPage.gotoProfile();

      // Verify Google is linked again
      await authPage.assertProviderLinked('google');
    });
  });

  /**
   * Test Group 6: OAuth with Existing Session
   */
  test.describe('OAuth with Existing Session', () => {
    test('OAuth with existing session (upgrade account)', async ({ page }) => {
      // User is already logged in with email/password
      await authPage.gotoProfile();

      // Mock OAuth accounts - no providers linked yet
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });

      // Add OAuth provider to upgrade account security
      await page.route(`${API_BASE}/api/v1/auth/oauth/google/callback*`, async (route) => {
        await route.fulfill({
          status: 302,
          headers: {
            Location: '/profile?oauth_success=google',
          },
        });
      });

      // Update mock to show Google linked
      await page.unroute(`${API_BASE}/api/v1/users/me/oauth-accounts`);
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { provider: 'google', createdAt: new Date().toISOString() },
          ]),
        });
      });

      await authPage.clickLinkProvider('google');
      await page.waitForURL('**/profile?oauth_success=google');
      await authPage.gotoProfile();

      // Verify account now has OAuth option while keeping email/password
      await authPage.assertProviderLinked('google');
      // User info should still be visible (session persisted)
      await expect(page.getByText('test@example.com')).toBeVisible();
    });
  });

  /**
   * Test Group 7: Profile Page Display
   */
  test.describe('Profile Page Display', () => {
    test('profile page shows all linked accounts', async ({ page }) => {
      // Mock OAuth accounts endpoint - multiple providers
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { provider: 'google', createdAt: '2025-01-01T00:00:00Z' },
            { provider: 'discord', createdAt: '2025-01-02T00:00:00Z' },
            { provider: 'github', createdAt: '2025-01-03T00:00:00Z' },
          ]),
        });
      });

      await authPage.gotoProfile();

      // Verify all three providers are shown as linked
      await authPage.assertProviderLinked('google');
      await authPage.assertProviderLinked('discord');
      await authPage.assertProviderLinked('github');

      const count = await authPage.getLinkedProvidersCount();
      expect(count).toBe(3);
    });
  });

  /**
   * Test Group 8: Token Refresh (Basic Mock)
   */
  test.describe('OAuth Token Handling', () => {
    test('OAuth token refresh handling (mock)', async ({ page }) => {
      // Mock OAuth accounts endpoint
      await page.route(`${API_BASE}/api/v1/users/me/oauth-accounts`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              provider: 'google',
              createdAt: new Date().toISOString(),
              tokenExpiresAt: new Date(Date.now() + 3600000).toISOString(), // 1 hour from now
            },
          ]),
        });
      });

      await authPage.gotoProfile();

      // Verify Google is linked and token info is available
      await authPage.assertProviderLinked('google');

      // In real implementation, token refresh would happen automatically
      // Here we just verify the provider stays linked after page reload
      await page.reload();
      await authPage.assertProviderLinked('google');
    });
  });

  /**
   * Test Group 9: Session Persistence
   */
  test.describe('Session Persistence', () => {
    test('session persistence after OAuth login', async ({ page }) => {
      // Mock OAuth login flow
      await page.route(`${API_BASE}/api/v1/auth/oauth/google/callback*`, async (route) => {
        await route.fulfill({
          status: 302,
          headers: {
            Location: '/?oauth_login_success=google',
            'Set-Cookie': 'session=mock-session-token; Path=/; HttpOnly',
          },
        });
      });

      // Simulate OAuth callback
      await page.goto('/auth/callback?code=mock-code&state=mock-state&provider=google');

      // Should redirect to home with session cookie
      await page.waitForURL('**/?oauth_login_success=google');

      // Verify session persisted - user info should be accessible
      await authPage.gotoProfile();
      await expect(page.getByText('test@example.com')).toBeVisible();

      // Reload page and verify session still valid
      await page.reload();
      await expect(page.getByText('test@example.com')).toBeVisible();
    });
  });
});
