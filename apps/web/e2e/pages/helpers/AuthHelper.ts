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

/**
 * E2E Test User Credentials
 * These users should exist in the database with these credentials.
 * Use npm run e2e:seed to create them if they don't exist.
 */
export const E2E_CREDENTIALS = {
  admin: {
    email: 'admin@meepleai.dev',
    password: 'pVKOMQNK0tFNgGlX', // From admin.secret - real admin password
  },
  editor: {
    email: 'editor@meepleai.dev',
    password: 'Demo123!',
  },
  user: {
    email: 'user@meepleai.dev',
    password: 'Demo123!',
  },
};

export class AuthHelper {
  constructor(private readonly page: Page) {}

  /**
   * Mock authenticated session for a user
   * Sets both API mock and cookies for middleware compatibility
   * Also mocks common API endpoints that authenticated pages need
   */
  async mockAuthenticatedSession(user: UserFixture): Promise<void> {
    // Set cookies FIRST (before any routes, so middleware works)
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

    const userResponse = {
      user,
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    };

    // Mock API response for /api/v1/auth/me
    await this.page.route(`${apiBase}/api/v1/auth/me`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(userResponse),
      });
    });

    // Mock login endpoint (in case it's called)
    await this.page.route(`${apiBase}/api/v1/auth/login`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(userResponse),
      });
    });

    // Mock user profile endpoints
    await this.page.route(`${apiBase}/api/v1/users/me`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: user.id,
          email: user.email,
          displayName: user.displayName,
          role: user.role,
          createdAt: new Date().toISOString(),
        }),
      });
    });

    await this.page.route(`${apiBase}/api/v1/users/profile`, async route => {
      if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true, message: 'Profile updated' }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: user.id,
            email: user.email,
            displayName: user.displayName,
            role: user.role,
          }),
        });
      }
    });

    // Mock password change endpoint
    await this.page.route(`${apiBase}/api/v1/users/profile/password`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true, message: 'Password changed' }),
      });
    });

    // Mock games endpoint (commonly needed by authenticated pages)
    await this.page.route(`${apiBase}/api/v1/games**`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'game-1', title: 'Test Game', description: 'A test game' },
        ]),
      });
    });

    // Mock 2FA status endpoint
    await this.page.route(`${apiBase}/api/v1/auth/2fa/status`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ enabled: false, backupCodesRemaining: 0 }),
      });
    });

    // Mock 2FA enable/disable endpoints
    await this.page.route(`${apiBase}/api/v1/auth/2fa/enable`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          secret: 'JBSWY3DPEHPK3PXP',
          qrCode: 'data:image/png;base64,mock',
          backupCodes: ['code1', 'code2', 'code3', 'code4', 'code5'],
        }),
      });
    });

    await this.page.route(`${apiBase}/api/v1/auth/2fa/disable`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    });

    // Mock sessions endpoint
    await this.page.route(`${apiBase}/api/v1/users/me/sessions`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'session-1',
            deviceInfo: 'Chrome on Windows',
            lastActiveAt: new Date().toISOString(),
            createdAt: new Date().toISOString(),
            isCurrent: true,
          },
        ]),
      });
    });

    // Mock session revoke endpoint
    await this.page.route(`${apiBase}/api/v1/auth/sessions/revoke**`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    });

    // Mock OAuth accounts endpoint
    await this.page.route(`${apiBase}/api/v1/users/me/oauth-accounts`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    // Mock API keys endpoint
    await this.page.route(`${apiBase}/api/v1/auth/api-keys**`, async route => {
      if (route.request().method() === 'DELETE') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      } else if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'key-1',
            name: 'Test Key',
            key: 'meeple_test_key_123',
            createdAt: new Date().toISOString(),
          }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'key-1',
              name: 'Production Key',
              lastUsedAt: new Date().toISOString(),
              createdAt: new Date().toISOString(),
              usage: { calls: 100 },
            },
            {
              id: 'key-2',
              name: 'Development Key',
              lastUsedAt: new Date().toISOString(),
              createdAt: new Date().toISOString(),
              usage: { calls: 50 },
            },
          ]),
        });
      }
    });

    // Mock chat/threads endpoint
    await this.page.route(`${apiBase}/api/v1/chat/threads**`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    // Mock settings endpoints
    await this.page.route(`${apiBase}/api/v1/settings/**`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    });

    // Mock RuleSpec/editor endpoints (for Editor and Admin roles)
    if (user.role === 'Editor' || user.role === 'Admin') {
      await this.page.route(`${apiBase}/api/v1/rulespecs**`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });

      await this.page.route(`${apiBase}/api/v1/versions**`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });
    }

    // Mock admin endpoints (for Admin role only)
    if (user.role === 'Admin') {
      await this.page.route(`${apiBase}/api/v1/admin/**`, async route => {
        const url = route.request().url();
        if (url.includes('/stats')) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              totalUsers: 100,
              totalGames: 50,
              totalQuestions: 1000,
              activeUsers: 25,
            }),
          });
        } else if (url.includes('/users')) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              users: [
                { id: '1', email: 'user1@test.com', role: 'User', status: 'Active' },
                { id: '2', email: 'user2@test.com', role: 'Editor', status: 'Active' },
              ],
              total: 2,
              page: 1,
              pageSize: 20,
            }),
          });
        } else {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ data: [] }),
          });
        }
      });

      await this.page.route(`${apiBase}/api/v1/configuration**`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });

      await this.page.route(`${apiBase}/api/v1/users**`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });

      await this.page.route(`${apiBase}/api/v1/analytics**`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ metrics: [] }),
        });
      });
    }
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

  /**
   * Login with real API credentials (for tests requiring real session)
   * This creates a real session in the backend that passes middleware validation.
   *
   * @param role - Which test user to login as
   * @returns true if login successful, false otherwise
   */
  async loginWithRealCredentials(role: 'admin' | 'editor' | 'user'): Promise<boolean> {
    const credentials = E2E_CREDENTIALS[role];

    try {
      const response = await this.page.request.post(`${apiBase}/api/v1/auth/login`, {
        data: {
          email: credentials.email,
          password: credentials.password,
        },
      });

      if (response.ok()) {
        console.log(`✅ Real login successful for ${role}: ${credentials.email}`);
        return true;
      } else {
        const body = await response.text();
        console.error(`❌ Real login failed for ${role}:`, response.status(), body);
        return false;
      }
    } catch (error) {
      console.error(`❌ Real login error for ${role}:`, error);
      return false;
    }
  }

  /**
   * Setup real authenticated session for tests that need to pass middleware validation
   * This performs actual API login and stores the session cookies.
   *
   * @param role - Which test user to login as
   * @throws Error if login fails
   */
  async setupRealSession(role: 'admin' | 'editor' | 'user'): Promise<void> {
    const success = await this.loginWithRealCredentials(role);
    if (!success) {
      throw new Error(`Failed to login as ${role}. Make sure E2E test users exist in the database.`);
    }
  }
}
