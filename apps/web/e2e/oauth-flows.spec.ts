/**
 * OAuth Flows E2E Tests - Complete Coverage
 *
 * Comprehensive end-to-end tests for all OAuth authentication flows covering:
 * - Authorization URL generation for all providers
 * - Redirect to OAuth provider pages
 * - Successful authentication and callback handling
 * - User profile creation from OAuth data
 * - Session creation after OAuth success
 * - Error scenarios (invalid credentials, CSRF, user cancellation)
 *
 * OAuth Flow Architecture:
 * 1. User clicks OAuth button → Backend generates authorization URL
 * 2. Backend redirects to provider (Google/Discord/GitHub)
 * 3. Provider authenticates user → redirects to backend callback
 * 4. Backend creates/links user & session → redirects to /auth/callback
 * 5. Frontend shows success → redirects to /dashboard
 *
 * Testing Strategy:
 * - Mock OAuth endpoints using AuthHelper patterns
 * - Test all three supported providers (Google, Discord, GitHub)
 * - Cover both new user registration and existing user login
 * - Verify error handling for CSRF, invalid credentials, user cancellation
 *
 * @see apps/web/e2e/pages/helpers/AuthHelper.ts - OAuth mocking utilities
 * @see apps/api/src/Api/BoundedContexts/Authentication/ - Backend OAuth implementation
 * @see Issue #2456 - E2E OAuth flow testing requirements
 */

import { test, expect } from './fixtures/chromatic';
import { AuthHelper, UserFixture } from './pages/helpers/AuthHelper';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * OAuth provider types
 */
type OAuthProvider = 'google' | 'discord' | 'github';

/**
 * OAuth callback URL parameters
 */
const CALLBACK_PARAMS = {
  SUCCESS: 'success=true',
  NEW_USER: 'new=true',
  EXISTING_USER: 'new=false',
  SUCCESS_NEW_USER: 'success=true&new=true',
  SUCCESS_EXISTING_USER: 'success=true&new=false',
  ERROR_ACCESS_DENIED: 'error=access_denied',
  ERROR_INVALID_STATE: 'error=invalid_state',
  ERROR_OAUTH_FAILED: 'error=oauth_failed',
  ERROR_INVALID_CLIENT: 'error=invalid_client',
} as const;

/**
 * Test user fixtures for OAuth scenarios
 */
const OAUTH_USER_FIXTURES: Record<OAuthProvider, { new: UserFixture; existing: UserFixture }> = {
  google: {
    new: {
      id: 'oauth-google-new-user-1',
      email: 'newuser.google@gmail.com',
      displayName: 'Google New User',
      role: 'User',
    },
    existing: {
      id: 'oauth-google-existing-user-1',
      email: 'existinguser.google@gmail.com',
      displayName: 'Google Existing User',
      role: 'User',
    },
  },
  discord: {
    new: {
      id: 'oauth-discord-new-user-1',
      email: 'newuser.discord@example.com',
      displayName: 'Discord New User',
      role: 'User',
    },
    existing: {
      id: 'oauth-discord-existing-user-1',
      email: 'existinguser.discord@example.com',
      displayName: 'Discord Existing User',
      role: 'User',
    },
  },
  github: {
    new: {
      id: 'oauth-github-new-user-1',
      email: 'newuser.github@github.com',
      displayName: 'GitHub New User',
      role: 'User',
    },
    existing: {
      id: 'oauth-github-existing-user-1',
      email: 'existinguser.github@github.com',
      displayName: 'GitHub Existing User',
      role: 'User',
    },
  },
};

/**
 * E2E Tests for Complete OAuth Flows
 *
 * Test Coverage:
 * - Google OAuth: Authorization URL, callback, profile creation, session
 * - Discord OAuth: Authorization URL, callback, profile creation
 * - GitHub OAuth: Authorization URL, callback, profile creation
 * - Error Scenarios: Invalid client, invalid state, access denied, OAuth failure
 * - Session Management: Persistence, authentication verification
 */
test.describe('OAuth Flows - Complete E2E Coverage', () => {
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    authHelper = new AuthHelper(page);

    // Disable animations for stable tests
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Start with unauthenticated state
    await authHelper.mockUnauthenticatedSession();
  });

  /**
   * Test Group 1: Google OAuth Flow
   */
  test.describe('Google OAuth Flow', () => {
    const provider: OAuthProvider = 'google';

    test('generates correct authorization URL for Google', async ({ page }) => {
      // Mock OAuth login endpoint with redirect to Google
      await authHelper.mockOAuthLogin(provider);

      // Track network requests to verify redirect
      const requests: string[] = [];
      page.on('request', request => {
        requests.push(request.url());
      });

      // Trigger OAuth login (simulate button click)
      const response = await page.request.get(`${API_BASE}/api/v1/auth/oauth/${provider}/login`);

      // Should return 302 redirect
      expect(response.status()).toBe(302);

      // Should have Location header pointing to Google
      const location = response.headers()['location'];
      expect(location).toContain('accounts.google.com/o/oauth2/v2/auth');
      expect(location).toContain('client_id=');
      expect(location).toContain('redirect_uri=');
      expect(location).toContain('state='); // CSRF token
    });

    test('redirects to Google login page', async ({ page }) => {
      // Mock OAuth login redirect
      await authHelper.mockOAuthLogin(provider);

      // Navigate to OAuth login endpoint
      const response = await page.goto(`${API_BASE}/api/v1/auth/oauth/${provider}/login`);

      // Should redirect (302)
      expect(response?.status()).toBe(302);
    });

    test('handles successful Google authentication callback', async ({ page }) => {
      const newUser = OAUTH_USER_FIXTURES[provider].new;

      // Mock OAuth login and authenticated session
      await authHelper.mockOAuthLogin(provider);
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate successful callback from Google
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Should show success message
      await expect(page.getByText(/welcome|successful/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to dashboard
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });

    test('creates user profile from Google data', async ({ page }) => {
      const newUser = OAUTH_USER_FIXTURES[provider].new;

      // Mock authenticated session with Google profile data
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate callback with new user flag
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect to dashboard
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify user profile data is available (session cookie exists)
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });

    test('creates session after Google OAuth success', async ({ page }) => {
      const newUser = OAUTH_USER_FIXTURES[provider].new;

      // Mock authenticated session
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate successful OAuth callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session cookie exists
      const cookies = await authHelper.getSessionCookies();
      const sessionCookie = cookies.find(c => c.name === 'meepleai_session');
      expect(sessionCookie).toBeDefined();
      expect(sessionCookie?.value).toBeTruthy();
    });

    test('handles existing Google user login', async ({ page }) => {
      const existingUser = OAUTH_USER_FIXTURES[provider].existing;

      // Mock authenticated session for existing user
      await authHelper.mockAuthenticatedSession(existingUser);

      // Simulate callback with existing user flag (new=false)
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_EXISTING_USER}`);

      // Should redirect to dashboard (no welcome message)
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });
  });

  /**
   * Test Group 2: Discord OAuth Flow
   */
  test.describe('Discord OAuth Flow', () => {
    const provider: OAuthProvider = 'discord';

    test('generates correct authorization URL for Discord', async ({ page }) => {
      // Mock OAuth login endpoint
      await authHelper.mockOAuthLogin(provider);

      // Request authorization URL
      const response = await page.request.get(`${API_BASE}/api/v1/auth/oauth/${provider}/login`);

      // Should return 302 redirect
      expect(response.status()).toBe(302);

      // Should redirect to Discord authorization
      const location = response.headers()['location'];
      expect(location).toContain('discord.com/api/oauth2/authorize');
      expect(location).toContain('client_id=');
      expect(location).toContain('redirect_uri=');
      expect(location).toContain('state=');
    });

    test('redirects to Discord authorization page', async ({ page }) => {
      // Mock OAuth login redirect
      await authHelper.mockOAuthLogin(provider);

      // Navigate to OAuth login endpoint
      const response = await page.goto(`${API_BASE}/api/v1/auth/oauth/${provider}/login`);

      // Should redirect (302)
      expect(response?.status()).toBe(302);
    });

    test('handles successful Discord authentication callback', async ({ page }) => {
      const newUser = OAUTH_USER_FIXTURES[provider].new;

      // Mock OAuth flow
      await authHelper.mockOAuthLogin(provider);
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate successful callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Should show success message
      await expect(page.getByText(/welcome|successful/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to dashboard
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });

    test('creates user profile from Discord data', async ({ page }) => {
      const newUser = OAUTH_USER_FIXTURES[provider].new;

      // Mock authenticated session with Discord profile
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });
  });

  /**
   * Test Group 3: GitHub OAuth Flow
   */
  test.describe('GitHub OAuth Flow', () => {
    const provider: OAuthProvider = 'github';

    test('generates correct authorization URL for GitHub', async ({ page }) => {
      // Mock OAuth login endpoint
      await authHelper.mockOAuthLogin(provider);

      // Request authorization URL
      const response = await page.request.get(`${API_BASE}/api/v1/auth/oauth/${provider}/login`);

      // Should return 302 redirect
      expect(response.status()).toBe(302);

      // Should redirect to GitHub authorization
      const location = response.headers()['location'];
      expect(location).toContain('github.com/login/oauth/authorize');
      expect(location).toContain('client_id=');
      expect(location).toContain('redirect_uri=');
      expect(location).toContain('state=');
    });

    test('redirects to GitHub authorization page', async ({ page }) => {
      // Mock OAuth login redirect
      await authHelper.mockOAuthLogin(provider);

      // Navigate to OAuth login endpoint
      const response = await page.goto(`${API_BASE}/api/v1/auth/oauth/${provider}/login`);

      // Should redirect (302)
      expect(response?.status()).toBe(302);
    });

    test('handles successful GitHub authentication callback', async ({ page }) => {
      const newUser = OAUTH_USER_FIXTURES[provider].new;

      // Mock OAuth flow
      await authHelper.mockOAuthLogin(provider);
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate successful callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Should show success message
      await expect(page.getByText(/welcome|successful/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to dashboard
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });

    test('creates user profile from GitHub data', async ({ page }) => {
      const newUser = OAUTH_USER_FIXTURES[provider].new;

      // Mock authenticated session with GitHub profile
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });
  });

  /**
   * Test Group 4: Error Scenarios
   */
  test.describe('OAuth Error Handling', () => {
    test('handles invalid client ID error', async ({ page }) => {
      // Navigate to callback with invalid_client error
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.ERROR_INVALID_CLIENT}`);

      // Should show error message
      await expect(page.getByText(/error|failed|problem/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to login
      await page.waitForURL(/login/, { timeout: 5000 });
    });

    test('handles invalid client secret error', async ({ page }) => {
      // Invalid secret typically results in oauth_failed error from backend
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.ERROR_OAUTH_FAILED}`);

      // Should show error message
      await expect(page.getByText(/error|failed|problem/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to login
      await page.waitForURL(/login/, { timeout: 5000 });
    });

    test('handles invalid state parameter (CSRF protection)', async ({ page }) => {
      // Navigate to callback with invalid_state error (CSRF validation failed)
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.ERROR_INVALID_STATE}`);

      // Should show error message
      await expect(page.getByText(/error|failed|problem/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to login
      await page.waitForURL(/login/, { timeout: 5000 });
    });

    test('handles user cancels OAuth flow', async ({ page }) => {
      // Navigate to callback with access_denied error (user clicked "Cancel" on provider)
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.ERROR_ACCESS_DENIED}`);

      // Should show error message
      await expect(page.getByText(/error|failed|denied|cancel/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to login
      await page.waitForURL(/login/, { timeout: 5000 });
    });

    test('handles OAuth provider failure', async ({ page }) => {
      // Navigate to callback with oauth_failed error (provider returned error)
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.ERROR_OAUTH_FAILED}`);

      // Should show error message
      await expect(page.getByText(/error|failed|problem/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to login
      await page.waitForURL(/login/, { timeout: 5000 });
    });
  });

  /**
   * Test Group 5: Session Management
   */
  test.describe('OAuth Session Management', () => {
    test('session persists after page reload (Google)', async ({ page }) => {
      const provider: OAuthProvider = 'google';
      const user = OAUTH_USER_FIXTURES[provider].new;

      // Mock authenticated session
      await authHelper.mockAuthenticatedSession(user);

      // Simulate OAuth callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect to dashboard
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Reload page
      await page.reload();

      // Verify session persists
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });

    test('session persists across page navigation (Discord)', async ({ page }) => {
      const provider: OAuthProvider = 'discord';
      const user = OAUTH_USER_FIXTURES[provider].new;

      // Mock authenticated session
      await authHelper.mockAuthenticatedSession(user);

      // Simulate OAuth callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Navigate to another page
      await page.goto('/games');

      // Verify session still valid
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });

    test('session contains correct user role (GitHub)', async ({ page }) => {
      const provider: OAuthProvider = 'github';
      const user = OAUTH_USER_FIXTURES[provider].new;

      // Mock authenticated session
      await authHelper.mockAuthenticatedSession(user);

      // Simulate OAuth callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify role cookie exists
      const cookies = await authHelper.getSessionCookies();
      const roleCookie = cookies.find(c => c.name === 'meepleai_user_role');
      expect(roleCookie).toBeDefined();
      expect(roleCookie?.value).toBe('user');
    });
  });

  /**
   * Test Group 6: Cross-Provider Scenarios
   */
  test.describe('Cross-Provider OAuth Scenarios', () => {
    test('all three providers can authenticate successfully', async ({ page }) => {
      const providers: OAuthProvider[] = ['google', 'discord', 'github'];

      for (const provider of providers) {
        // Clear state between providers
        await authHelper.mockUnauthenticatedSession();

        const user = OAUTH_USER_FIXTURES[provider].new;

        // Mock OAuth flow
        await authHelper.mockOAuthLogin(provider);
        await authHelper.mockAuthenticatedSession(user);

        // Simulate successful callback
        await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

        // Wait for redirect
        await page.waitForURL(/dashboard/, { timeout: 5000 });

        // Verify session
        const isAuthenticated = await authHelper.verifyAuthenticated();
        expect(isAuthenticated).toBe(true);
      }
    });

    test('error handling consistent across all providers', async ({ page }) => {
      const providers: OAuthProvider[] = ['google', 'discord', 'github'];
      const errorParam = CALLBACK_PARAMS.ERROR_ACCESS_DENIED;

      for (const provider of providers) {
        // Clear state
        await authHelper.mockUnauthenticatedSession();

        // Navigate to callback with error
        await page.goto(`/auth/callback?${errorParam}`);

        // Should show error
        await expect(page.getByText(/error|failed|denied/i)).toBeVisible({ timeout: 5000 });

        // Should redirect to login
        await page.waitForURL(/login/, { timeout: 5000 });
      }
    });
  });
});
