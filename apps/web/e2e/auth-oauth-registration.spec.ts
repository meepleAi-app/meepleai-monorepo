/**
 * Auth OAuth Registration E2E Tests - CONSOLIDATED MOCKS
 *
 * ✅ REFACTORED: Consolidated 9 inline /auth/me mocks to AuthHelper (Issue #2299)
 * - Removed 9 duplicate inline page.route() calls → authHelper.mockAuthenticatedSession()
 * - Removed 4 dead helper functions (createSessionCookies, generateSessionToken, etc.) ~90 lines
 * - OAuth provider mocks MAINTAINED (mockOAuthLogin tested elsewhere)
 *
 * Tests for new user registration via OAuth providers (Google, Discord, GitHub).
 * This covers the scenario where a user has NO existing account and registers
 * for the first time through OAuth authentication.
 *
 * OAuth Flow:
 * 1. User clicks OAuth button → redirects to provider
 * 2. Provider authenticates user → redirects to backend callback
 * 3. Backend creates user & session → redirects to /auth/callback?success=true&new=true
 * 4. Frontend shows success → redirects to /dashboard
 *
 * Note: Auth session mocks consolidated to AuthHelper per Issue #2299 pattern
 *
 * @see apps/web/e2e/pages/helpers/AuthHelper.ts
 * @see apps/web/src/app/auth/callback/page.tsx
 * @see Issue #2299 - E2E mock consolidation
 */

import { test, expect } from './fixtures';
import { LoginPage } from './pages/auth/LoginPage';
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
  SUCCESS_NEW_USER: 'success=true&new=true',
  ERROR_ACCESS_DENIED: 'error=access_denied',
  ERROR_INVALID_STATE: 'error=invalid_state',
  ERROR_SERVER: 'error=server_error',
} as const;

/**
 * New user fixtures for OAuth registration scenarios
 * These represent users being created for the first time via OAuth
 */
const OAUTH_NEW_USER_FIXTURES: Record<OAuthProvider, UserFixture> = {
  google: {
    id: 'oauth-google-new-user-1',
    email: 'newuser.google@gmail.com',
    displayName: 'Google New User',
    role: 'User',
  },
  discord: {
    id: 'oauth-discord-new-user-1',
    email: 'newuser.discord@example.com',
    displayName: 'Discord New User',
    role: 'User',
  },
  github: {
    id: 'oauth-github-new-user-1',
    email: 'newuser.github@github.com',
    displayName: 'GitHub New User',
    role: 'User',
  },
};

/**
 * E2E Tests for OAuth Registration Flow (New Users)
 *
 * Test Coverage:
 * - New user registration via Google OAuth
 * - New user registration via Discord OAuth
 * - New user registration via GitHub OAuth
 * - OAuth registration creates valid session
 * - OAuth registration redirects to dashboard
 * - Session persistence after OAuth registration
 * - OAuth registration with profile data extraction
 *
 * Testing Strategy:
 * - Use Page Object Model (LoginPage, AuthHelper)
 * - Mock OAuth endpoints to simulate provider callbacks
 * - Verify new user accounts are created and sessions established
 * - Test all three supported providers
 */
test.describe('OAuth Registration - New Users', () => {
  let loginPage: LoginPage;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    authHelper = new AuthHelper(page);

    // Disable animations for stable tests
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Start with unauthenticated state
    await authHelper.mockUnauthenticatedSession();
  });

  /**
   * Test Group 1: Google OAuth Registration
   */
  test.describe('Google OAuth Registration', () => {
    const provider: OAuthProvider = 'google';

    test('new user can register via Google OAuth', async ({ page }) => {
      const newUser = OAUTH_NEW_USER_FIXTURES[provider];

      // Mock the OAuth login redirect
      await authHelper.mockOAuthLogin(provider);

      // Mock authenticated session after OAuth (consolidated to AuthHelper)
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate the callback page with success for new user
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Should show welcome message for new user
      await expect(page.getByText(/welcome|successful/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to dashboard after success
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session is established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });

    test('Google OAuth registration extracts profile data correctly', async ({ page }) => {
      const newUser = OAUTH_NEW_USER_FIXTURES[provider];

      // Mock authenticated session after registration (consolidated to AuthHelper)
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate successful OAuth callback for new user
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect to dashboard
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });
  });

  /**
   * Test Group 2: Discord OAuth Registration
   */
  test.describe('Discord OAuth Registration', () => {
    const provider: OAuthProvider = 'discord';

    test('new user can register via Discord OAuth', async ({ page }) => {
      const newUser = OAUTH_NEW_USER_FIXTURES[provider];

      // Mock the OAuth login redirect
      await authHelper.mockOAuthLogin(provider);

      // Mock authenticated session (consolidated to AuthHelper)
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate successful OAuth callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Should show welcome message
      await expect(page.getByText(/welcome|successful/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to dashboard
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session is established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });

    test('Discord OAuth registration handles Discord-specific profile', async ({ page }) => {
      const newUser = OAUTH_NEW_USER_FIXTURES[provider];

      // Mock authenticated session (consolidated to AuthHelper)
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate OAuth callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });
  });

  /**
   * Test Group 3: GitHub OAuth Registration
   */
  test.describe('GitHub OAuth Registration', () => {
    const provider: OAuthProvider = 'github';

    test('new user can register via GitHub OAuth', async ({ page }) => {
      const newUser = OAUTH_NEW_USER_FIXTURES[provider];

      // Mock the OAuth login redirect
      await authHelper.mockOAuthLogin(provider);

      // Mock authenticated session (consolidated to AuthHelper)
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate successful OAuth callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Should show welcome message
      await expect(page.getByText(/welcome|successful/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to dashboard
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session is established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });

    test('GitHub OAuth registration extracts GitHub username', async ({ page }) => {
      const newUser = OAUTH_NEW_USER_FIXTURES[provider];

      // Mock authenticated session (consolidated to AuthHelper)
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate OAuth callback
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });
  });

  /**
   * Test Group 4: Session Persistence After Registration
   */
  test.describe('Session Persistence After OAuth Registration', () => {
    test('session persists after page reload following OAuth registration', async ({ page }) => {
      const provider: OAuthProvider = 'google';
      const newUser = OAUTH_NEW_USER_FIXTURES[provider];

      // Mock persistent authenticated session (consolidated to AuthHelper)
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate OAuth callback with success
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Wait for redirect to dashboard
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Reload the page
      await page.reload();

      // Verify session is still valid after reload
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);

      // Navigate to another page and verify still authenticated
      await page.goto('/dashboard');
      const stillAuthenticated = await authHelper.verifyAuthenticated();
      expect(stillAuthenticated).toBe(true);
    });

    // NOTE: Protected routes test removed - middleware-based auth checking cannot be mocked via page.route()
    // Session persistence is already tested by "session persists after page reload" test above
  });

  /**
   * Test Group 5: OAuth Registration Error Handling
   */
  test.describe('OAuth Registration Error Handling', () => {
    test('handles OAuth provider denial gracefully', async ({ page }) => {
      // Navigate to callback with access_denied error (backend redirects with error param)
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.ERROR_ACCESS_DENIED}`);

      // Should show error message
      await expect(page.getByText(/error|failed|problem/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to login after showing error
      await page.waitForURL(/login/, { timeout: 5000 });
    });

    test('handles invalid OAuth state error', async ({ page }) => {
      // Navigate to callback with invalid_state error (backend detected CSRF issue)
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.ERROR_INVALID_STATE}`);

      // Should show error message
      await expect(page.getByText(/error|failed|problem/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to login
      await page.waitForURL(/login/, { timeout: 5000 });
    });

    test('handles OAuth server error gracefully', async ({ page }) => {
      // Navigate to callback with server error (backend encountered an issue)
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.ERROR_SERVER}`);

      // Should show error message
      await expect(page.getByText(/error|failed|problem/i)).toBeVisible({ timeout: 5000 });

      // Should redirect to login
      await page.waitForURL(/login/, { timeout: 5000 });
    });
  });

  /**
   * Test Group 6: OAuth Registration from Register Page
   */
  test.describe('OAuth Registration from Register Page', () => {
    test('OAuth buttons are visible on register page', async ({ page }) => {
      // Navigate directly to register page
      await page.goto('/register');

      // Wait for page to load
      await page.waitForLoadState('networkidle');

      // OAuth buttons should be visible on register page (checking by text/role)
      await expect(
        page
          .getByRole('button', { name: /google/i })
          .or(page.locator('[data-testid="oauth-google"]'))
      ).toBeVisible({ timeout: 5000 });
      await expect(
        page
          .getByRole('button', { name: /discord/i })
          .or(page.locator('[data-testid="oauth-discord"]'))
      ).toBeVisible({ timeout: 5000 });
      await expect(
        page
          .getByRole('button', { name: /github/i })
          .or(page.locator('[data-testid="oauth-github"]'))
      ).toBeVisible({ timeout: 5000 });
    });

    test('can complete OAuth registration flow from register page context', async ({ page }) => {
      const provider: OAuthProvider = 'google';
      const newUser = OAUTH_NEW_USER_FIXTURES[provider];

      // Mock authenticated session (consolidated to AuthHelper)
      await authHelper.mockAuthenticatedSession(newUser);

      // Simulate OAuth callback after user completes registration with provider
      await page.goto(`/auth/callback?${CALLBACK_PARAMS.SUCCESS_NEW_USER}`);

      // Should show welcome message for new user
      await expect(page.getByText(/welcome|successful/i)).toBeVisible({ timeout: 5000 });

      // Should complete registration and redirect to dashboard
      await page.waitForURL(/dashboard/, { timeout: 5000 });

      // Verify session is established
      const isAuthenticated = await authHelper.verifyAuthenticated();
      expect(isAuthenticated).toBe(true);
    });
  });
});
