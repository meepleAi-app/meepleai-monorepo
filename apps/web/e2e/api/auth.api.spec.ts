/**
 * Authentication API Tests - Playwright Native
 *
 * Native Playwright API tests for authentication endpoints.
 * Provides granular control and better debugging compared to Postman/Newman.
 *
 * @see apps/api/src/Api/BoundedContexts/Authentication
 */

import { test, expect, APIRequestContext } from './fixtures/chromatic';

const BASE_URL = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Authentication API', () => {
  let apiContext: APIRequestContext;

  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({
      baseURL: BASE_URL,
      extraHTTPHeaders: {
        'Content-Type': 'application/json',
      },
    });
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test.describe('POST /api/v1/auth/login', () => {
    test('should login successfully with valid credentials', async () => {
      const response = await apiContext.post('/api/v1/auth/login', {
        data: {
          email: 'demo@meepleai.dev',
          password: 'Demo123!',
        },
      });

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data.user).toBeDefined();
      expect(data.user.email).toBe('demo@meepleai.dev');
      expect(data.user.id).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/
      );
      expect(data.expiresAt).toBeDefined();

      // Verify session cookie is set
      const cookies = response.headers()['set-cookie'];
      expect(cookies).toBeDefined();
      expect(cookies).toContain('meepleai_session');
    });

    test('should fail with invalid email', async () => {
      const response = await apiContext.post('/api/v1/auth/login', {
        data: {
          email: 'nonexistent@example.com',
          password: 'Demo123!',
        },
      });

      expect(response.status()).toBe(401);

      const data = await response.json();
      expect(data.error).toBeDefined();
    });

    test('should fail with invalid password', async () => {
      const response = await apiContext.post('/api/v1/auth/login', {
        data: {
          email: 'demo@meepleai.dev',
          password: 'WrongPassword123!',
        },
      });

      expect(response.status()).toBe(401);
    });

    test('should fail with missing credentials', async () => {
      const response = await apiContext.post('/api/v1/auth/login', {
        data: {},
      });

      expect(response.status()).toBe(400);
    });

    test('should support 2FA flow when enabled', async () => {
      // This test assumes a user with 2FA enabled exists
      const response = await apiContext.post('/api/v1/auth/login', {
        data: {
          email: 'admin@meepleai.dev', // Admin might have 2FA
          password: 'Demo123!',
        },
      });

      // Could be 200 (no 2FA) or 200 with requiresTwoFactor flag
      expect([200, 401]).toContain(response.status());

      if (response.status() === 200) {
        const data = await response.json();

        if (data.requiresTwoFactor) {
          expect(data.sessionToken).toBeDefined();
          expect(data.message).toContain('Two-factor');
        } else {
          expect(data.user).toBeDefined();
        }
      }
    });
  });

  test.describe('POST /api/v1/auth/register', () => {
    test('should register new user with valid data', async () => {
      const timestamp = Date.now();
      const response = await apiContext.post('/api/v1/auth/register', {
        data: {
          email: `test-${timestamp}@example.com`,
          password: 'SecurePass123!',
          displayName: `Test User ${timestamp}`,
        },
      });

      expect(response.status()).toBe(201);

      const data = await response.json();
      expect(data.user).toBeDefined();
      expect(data.user.email).toBe(`test-${timestamp}@example.com`);
      expect(data.user.displayName).toBe(`Test User ${timestamp}`);
    });

    test('should fail with duplicate email', async () => {
      const response = await apiContext.post('/api/v1/auth/register', {
        data: {
          email: 'demo@meepleai.dev', // Existing user
          password: 'SecurePass123!',
          displayName: 'Duplicate User',
        },
      });

      expect(response.status()).toBe(409); // Conflict
    });

    test('should fail with weak password', async () => {
      const response = await apiContext.post('/api/v1/auth/register', {
        data: {
          email: 'newuser@example.com',
          password: '123', // Too weak
          displayName: 'New User',
        },
      });

      expect(response.status()).toBe(400);
    });

    test('should fail with invalid email format', async () => {
      const response = await apiContext.post('/api/v1/auth/register', {
        data: {
          email: 'invalid-email',
          password: 'SecurePass123!',
          displayName: 'New User',
        },
      });

      expect(response.status()).toBe(400);
    });
  });

  test.describe('GET /api/v1/auth/me', () => {
    let sessionCookie: string;

    test.beforeAll(async () => {
      // Login to get session
      const response = await apiContext.post('/api/v1/auth/login', {
        data: {
          email: 'demo@meepleai.dev',
          password: 'Demo123!',
        },
      });

      const cookies = response.headers()['set-cookie'];
      sessionCookie = cookies?.split(';')[0] || '';
    });

    test('should return current user with valid session', async () => {
      const response = await apiContext.get('/api/v1/auth/me', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data.user).toBeDefined();
      expect(data.user.email).toBe('demo@meepleai.dev');
    });

    test('should fail without authentication', async () => {
      const response = await apiContext.get('/api/v1/auth/me');

      expect(response.status()).toBe(401);
    });
  });

  test.describe('POST /api/v1/auth/logout', () => {
    test('should logout and clear session', async () => {
      // Login first
      const loginResponse = await apiContext.post('/api/v1/auth/login', {
        data: {
          email: 'demo@meepleai.dev',
          password: 'Demo123!',
        },
      });

      const sessionCookie = loginResponse.headers()['set-cookie']?.split(';')[0] || '';

      // Logout
      const logoutResponse = await apiContext.post('/api/v1/auth/logout', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      expect(logoutResponse.status()).toBe(200);

      // Verify session is invalid
      const meResponse = await apiContext.get('/api/v1/auth/me', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      expect(meResponse.status()).toBe(401);
    });
  });

  test.describe('GET /api/v1/auth/session/status', () => {
    test('should return session status for authenticated user', async () => {
      // Login first
      const loginResponse = await apiContext.post('/api/v1/auth/login', {
        data: {
          email: 'demo@meepleai.dev',
          password: 'Demo123!',
        },
      });

      const sessionCookie = loginResponse.headers()['set-cookie']?.split(';')[0] || '';

      // Get session status
      const response = await apiContext.get('/api/v1/auth/session/status', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data.isAuthenticated).toBe(true);
      expect(data.expiresAt).toBeDefined();
      expect(data.user).toBeDefined();
    });

    test('should return unauthenticated status without session', async () => {
      const response = await apiContext.get('/api/v1/auth/session/status');

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data.isAuthenticated).toBe(false);
    });
  });

  test.describe('Performance', () => {
    test('login should complete in under 1 second', async () => {
      const startTime = Date.now();

      await apiContext.post('/api/v1/auth/login', {
        data: {
          email: 'demo@meepleai.dev',
          password: 'Demo123!',
        },
      });

      const duration = Date.now() - startTime;
      console.log(`Login duration: ${duration}ms`);

      expect(duration).toBeLessThan(1000);
    });
  });
});
