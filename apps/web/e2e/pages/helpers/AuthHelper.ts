/**
 * AuthHelper - Centralized authentication utilities
 *
 * Handles all authentication-related operations including:
 * - Mock authentication sessions
 * - Cookie management
 * - API route mocking for /auth/me
 * - User fixtures and role-based testing
 *
 * Replaces legacy mockAuth* functions scattered across test files.
 */

import { Page } from '@playwright/test';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export interface UserFixture {
  id: string;
  email: string;
  displayName: string;
  role: 'Admin' | 'Editor' | 'User';
}

/**
 * Default user fixtures for testing
 */
export const USER_FIXTURES = {
  admin: {
    id: 'admin-test-1',
    email: 'admin@meepleai.dev',
    displayName: 'Admin User',
    role: 'Admin' as const,
  },
  editor: {
    id: 'editor-test-1',
    email: 'editor@meepleai.dev',
    displayName: 'Editor User',
    role: 'Editor' as const,
  },
  user: {
    id: 'user-test-1',
    email: 'user@meepleai.dev',
    displayName: 'Regular User',
    role: 'User' as const,
  },
};

export class AuthHelper {
  constructor(private readonly page: Page) {}

  /**
   * Mock authenticated session for a user
   * Sets both API mock and cookies for middleware compatibility
   */
  async mockAuthenticatedSession(user: UserFixture): Promise<void> {
    // Mock API response for /api/v1/auth/me
    await this.page.route(`${apiBase}/api/v1/auth/me`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user,
          expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        }),
      });
    });

    // Set cookies for middleware (server-side) to work
    await this.page.context().addCookies([
      {
        name: 'meepleai_session',
        value: 'mock-session-token',
        domain: 'localhost',
        path: '/',
        httpOnly: true,
        secure: false,
        sameSite: 'Lax',
      },
      {
        name: 'meepleai_user_role',
        value: user.role.toLowerCase(),
        domain: 'localhost',
        path: '/',
        httpOnly: false,
        secure: false,
        sameSite: 'Lax',
      },
    ]);
  }

  /**
   * Mock unauthenticated session (401 response)
   * Clears cookies to ensure clean state
   */
  async mockUnauthenticatedSession(): Promise<void> {
    // Mock API response with 401
    await this.page.route(`${apiBase}/api/v1/auth/me`, async route => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' }),
      });
    });

    // Clear auth cookies to ensure middleware sees unauthenticated state
    await this.page.context().clearCookies();
  }

  /**
   * Mock login API endpoint
   */
  async mockLoginEndpoint(success: boolean = true, user?: UserFixture): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/auth/login`, async route => {
      if (success && user) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            user,
            expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          }),
        });
      } else {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Invalid credentials' }),
        });
      }
    });
  }

  /**
   * Mock logout API endpoint
   */
  async mockLogoutEndpoint(): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/auth/logout`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    });
  }

  /**
   * Mock 2FA verification endpoint
   */
  async mock2FAVerification(success: boolean = true): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/auth/2fa/verify`, async route => {
      if (success) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      } else {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Invalid TOTP code' }),
        });
      }
    });
  }

  /**
   * Mock OAuth callback
   */
  async mockOAuthCallback(provider: string, user: UserFixture): Promise<void> {
    await this.page.route(`${apiBase}/api/v1/auth/oauth/${provider}/callback*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user,
          expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        }),
      });
    });
  }

  /**
   * Mock OAuth login endpoints (302 redirects to OAuth providers)
   * Used for testing OAuth button clicks and redirect flows
   */
  async mockOAuthLogin(provider: 'google' | 'github' | 'discord'): Promise<void> {
    const redirectUrls = {
      google:
        'https://accounts.google.com/o/oauth2/v2/auth?client_id=mock-google-client&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fapi%2Fv1%2Fauth%2Foauth%2Fgoogle%2Fcallback&response_type=code&scope=openid+profile+email&state=mock-csrf-state-123',
      discord:
        'https://discord.com/api/oauth2/authorize?client_id=mock-discord-client&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fapi%2Fv1%2Fauth%2Foauth%2Fdiscord%2Fcallback&response_type=code&scope=identify+email&state=mock-csrf-state-456',
      github:
        'https://github.com/login/oauth/authorize?client_id=mock-github-client&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fapi%2Fv1%2Fauth%2Foauth%2Fgithub%2Fcallback&response_type=code&scope=read%3Auser+user%3Aemail&state=mock-csrf-state-789',
    };

    await this.page.route(`${apiBase}/api/v1/auth/oauth/${provider}/login`, async route => {
      await route.fulfill({
        status: 302,
        headers: {
          Location: redirectUrls[provider],
          'Content-Type': 'text/html',
        },
        body: `<html><body>Redirecting to ${provider}...</body></html>`,
      });
    });
  }

  /**
   * Mock all OAuth providers (google, github, discord) for testing
   */
  async mockAllOAuthProviders(): Promise<void> {
    await this.mockOAuthLogin('google');
    await this.mockOAuthLogin('github');
    await this.mockOAuthLogin('discord');
  }

  /**
   * Setup mock authentication for RBAC tests (simplified pattern)
   * Equivalent to setupMockAuth() from fixtures/auth.ts
   */
  async setupMockAuth(role: 'Admin' | 'Editor' | 'User' = 'Admin', email?: string): Promise<void> {
    const user = {
      id: `${role.toLowerCase()}-test-id`,
      email: email || `${role.toLowerCase()}@example.com`,
      displayName: `Test ${role}`,
      role,
    };

    await this.page.route(`${apiBase}/auth/me`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user,
          expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        }),
      });
    });

    // Catch-all for API calls
    await this.page.route(`${apiBase}/api/**`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      });
    });
  }

  /**
   * Clear all authentication state
   */
  async clearAuthState(): Promise<void> {
    await this.page.context().clearCookies();
    await this.page.unroute(`${apiBase}/api/v1/auth/**`);
  }

  /**
   * Get current session cookies
   */
  async getSessionCookies(): Promise<Array<{ name: string; value: string }>> {
    return await this.page.context().cookies();
  }

  /**
   * Verify user is redirected to login
   */
  async verifyRedirectToLogin(): Promise<void> {
    await this.page.waitForURL(/\/login/);
  }

  /**
   * Verify user is authenticated (has session cookie)
   */
  async verifyAuthenticated(): Promise<boolean> {
    const cookies = await this.getSessionCookies();
    return cookies.some(c => c.name === 'meepleai_session');
  }
}
