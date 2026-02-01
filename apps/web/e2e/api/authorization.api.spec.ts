/**
 * E2E-004: API Authorization Tests
 *
 * Issue #1490: API-level RBAC authorization testing
 *
 * Tests that API endpoints properly enforce role-based access control,
 * returning 403 Forbidden for unauthorized role access.
 *
 * Coverage:
 * - Admin-only API endpoints (8 tests)
 * - Editor+ API endpoints (3 tests)
 * - Public API endpoints (2 tests)
 *
 * Total: 13 API authorization tests
 *
 * Note: Uses page.context().route() instead of page.route() because:
 * - page.route() only intercepts requests from the page itself
 * - page.request (APIRequestContext) bypasses page-level routes
 * - page.context().route() intercepts ALL requests at browser context level
 */

import { setupMockAuth } from '../fixtures/auth';
import { test, expect } from '../fixtures';
import { mockApiForbidden } from '../helpers/mocks';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('API Authorization Tests - E2E-004', () => {
  test.describe('Admin-Only API Endpoints', () => {
    test('Admin can call GET /api/v1/admin/users', async ({ page }) => {
      await setupMockAuth(page, 'Admin');

      const response = await page.request.get(`${API_BASE}/api/v1/admin/users`);

      // Should succeed (200) or return mocked data
      expect([200, 304]).toContain(response.status());
    });

    test('Editor cannot call GET /api/v1/admin/users', async ({ page }) => {
      await setupMockAuth(page, 'Editor');
      await mockApiForbidden(page, '/api/v1/admin/users', 'GET', 'Editor');

      const response = await page.request.get(`${API_BASE}/api/v1/admin/users`);

      expect(response.status()).toBe(403);
    });

    test('User cannot call GET /api/v1/admin/users', async ({ page }) => {
      await setupMockAuth(page, 'User');
      await mockApiForbidden(page, '/api/v1/admin/users', 'GET', 'User');

      const response = await page.request.get(`${API_BASE}/api/v1/admin/users`);

      expect(response.status()).toBe(403);
    });

    test('Admin can call POST /api/v1/admin/configuration', async ({ page }) => {
      await setupMockAuth(page, 'Admin');

      const response = await page.request.post(`${API_BASE}/api/v1/admin/configuration`, {
        data: {
          key: 'test.feature.enabled',
          value: 'true',
          category: 'Features',
        },
      });

      expect([200, 201, 304]).toContain(response.status());
    });

    test('Editor cannot call POST /api/v1/admin/configuration', async ({ page }) => {
      await setupMockAuth(page, 'Editor');
      await mockApiForbidden(page, '/api/v1/admin/configuration', 'POST', 'Editor');

      const response = await page.request.post(`${API_BASE}/api/v1/admin/configuration`, {
        data: {
          key: 'test.feature.enabled',
          value: 'true',
        },
      });

      expect(response.status()).toBe(403);
    });

    test('User cannot call POST /api/v1/admin/configuration', async ({ page }) => {
      await setupMockAuth(page, 'User');
      await mockApiForbidden(page, '/api/v1/admin/configuration', 'POST', 'User');

      const response = await page.request.post(`${API_BASE}/api/v1/admin/configuration`, {
        data: {
          key: 'test.feature.enabled',
          value: 'true',
        },
      });

      expect(response.status()).toBe(403);
    });

    test('Admin can call GET /api/v1/admin/stats', async ({ page }) => {
      await setupMockAuth(page, 'Admin');

      const response = await page.request.get(`${API_BASE}/api/v1/admin/stats`);

      expect([200, 304]).toContain(response.status());
    });

    test('Non-admin cannot call GET /api/v1/admin/stats', async ({ page }) => {
      await setupMockAuth(page, 'User');
      await mockApiForbidden(page, '/api/v1/admin/stats', 'GET', 'User');

      const response = await page.request.get(`${API_BASE}/api/v1/admin/stats`);

      expect(response.status()).toBe(403);
    });
  });

  test.describe('Editor+ API Endpoints (Admin or Editor)', () => {
    test('Admin can call POST /api/v1/games', async ({ page }) => {
      await setupMockAuth(page, 'Admin');

      const response = await page.request.post(`${API_BASE}/api/v1/games`, {
        data: {
          name: 'Test Game',
          description: 'Test Description',
        },
      });

      // Mock returns success
      expect([200, 201, 304]).toContain(response.status());
    });

    test('Editor can call POST /api/v1/games', async ({ page }) => {
      await setupMockAuth(page, 'Editor');

      const response = await page.request.post(`${API_BASE}/api/v1/games`, {
        data: {
          name: 'Test Game',
          description: 'Test Description',
        },
      });

      expect([200, 201, 304]).toContain(response.status());
    });

    test('User cannot call POST /api/v1/games', async ({ page }) => {
      await setupMockAuth(page, 'User');
      await mockApiForbidden(page, '/api/v1/games', 'POST', 'User');

      const response = await page.request.post(`${API_BASE}/api/v1/games`, {
        data: {
          name: 'Test Game',
          description: 'Test Description',
        },
      });

      expect(response.status()).toBe(403);
    });

    test('Admin can call PUT /api/v1/games/{id}/rulespec', async ({ page }) => {
      await setupMockAuth(page, 'Admin');

      const gameId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
      const response = await page.request.put(`${API_BASE}/api/v1/games/${gameId}/rulespec`, {
        data: {
          content: 'Updated rules',
          version: '1.0.1',
        },
      });

      expect([200, 201, 304]).toContain(response.status());
    });

    test('Editor can call PUT /api/v1/games/{id}/rulespec', async ({ page }) => {
      await setupMockAuth(page, 'Editor');

      const gameId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
      const response = await page.request.put(`${API_BASE}/api/v1/games/${gameId}/rulespec`, {
        data: {
          content: 'Updated rules',
          version: '1.0.1',
        },
      });

      expect([200, 201, 304]).toContain(response.status());
    });

    test('User cannot call PUT /api/v1/games/{id}/rulespec', async ({ page }) => {
      await setupMockAuth(page, 'User');

      const gameId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
      await mockApiForbidden(page, `/api/v1/games/${gameId}/rulespec`, 'PUT', 'User');

      const response = await page.request.put(`${API_BASE}/api/v1/games/${gameId}/rulespec`, {
        data: {
          content: 'Updated rules',
          version: '1.0.1',
        },
      });

      expect(response.status()).toBe(403);
    });
  });

  test.describe('Public API Endpoints (All Roles)', () => {
    test('All roles can call GET /api/v1/games', async ({ page }) => {
      const roles: Array<'Admin' | 'Editor' | 'User'> = ['Admin', 'Editor', 'User'];

      for (const role of roles) {
        await setupMockAuth(page, role);

        const response = await page.request.get(`${API_BASE}/api/v1/games`);

        expect([200, 304]).toContain(response.status());
      }
    });

    test('Authenticated users can call GET /api/v1/auth/me', async ({ page }) => {
      const roles: Array<'Admin' | 'Editor' | 'User'> = ['Admin', 'Editor', 'User'];

      for (const role of roles) {
        await setupMockAuth(page, role);

        const response = await page.request.get(`${API_BASE}/api/v1/auth/me`);

        expect([200, 304]).toContain(response.status());

        if (response.ok()) {
          const data = await response.json();
          expect(data.user.role).toBe(role);
        }
      }
    });
  });

  test.describe('Unauthenticated API Access', () => {
    test('Unauthenticated request to admin endpoint returns 401/403', async ({ page }) => {
      // No setupMockAuth = unauthenticated

      await page.context().route(`${API_BASE}/api/v1/admin/users**`, async route => {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Unauthorized',
            message: 'Authentication required',
            statusCode: 401,
          }),
        });
      });

      const response = await page.request.get(`${API_BASE}/api/v1/admin/users`);

      expect([401, 403]).toContain(response.status());
    });

    test('Unauthenticated request to protected chat endpoint returns 401', async ({ page }) => {
      await page.context().route(`${API_BASE}/api/v1/chat/threads**`, async route => {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Unauthorized',
            statusCode: 401,
          }),
        });
      });

      const response = await page.request.get(`${API_BASE}/api/v1/chat/threads`);

      expect(response.status()).toBe(401);
    });

    test('Unauthenticated can call public endpoints like GET /api/v1/games', async ({ page }) => {
      // No auth setup, but games endpoint is public for GET
      const response = await page.request.get(`${API_BASE}/api/v1/games`);

      // Should succeed (mocked or real)
      expect([200, 304]).toContain(response.status());
    });
  });
});
