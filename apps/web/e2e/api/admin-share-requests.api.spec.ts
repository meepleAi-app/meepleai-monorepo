/**
 * Admin Share Requests API Tests - Issue #2734
 *
 * Tests for admin share request review endpoints:
 * - GET /api/v1/admin/share-requests (list)
 * - GET /api/v1/admin/share-requests/{id} (details)
 * - POST /api/v1/admin/share-requests/{id}/approve
 * - POST /api/v1/admin/share-requests/{id}/reject
 * - POST /api/v1/admin/share-requests/{id}/request-changes
 *
 * @see apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs
 */

import { test, expect, APIRequestContext } from '../fixtures';

const BASE_URL = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Admin Share Requests API - Issue #2734', () => {
  let apiContext: APIRequestContext;
  let adminSessionCookie: string;
  let userSessionCookie: string;
  let testShareRequestId: string;
  let testGameId: string;

  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({
      baseURL: BASE_URL,
      extraHTTPHeaders: {
        'Content-Type': 'application/json',
      },
    });

    // Admin login
    const adminLoginResponse = await apiContext.post('/api/v1/auth/login', {
      data: {
        email: 'admin@meepleai.dev',
        password: 'Admin123!',
      },
    });

    expect(adminLoginResponse.ok()).toBeTruthy();
    adminSessionCookie =
      adminLoginResponse.headers()['set-cookie']?.split(';')[0] || '';

    // User login (for creating share request)
    const userLoginResponse = await apiContext.post('/api/v1/auth/login', {
      data: {
        email: 'demo@meepleai.dev',
        password: 'Demo123!',
      },
    });

    expect(userLoginResponse.ok()).toBeTruthy();
    userSessionCookie =
      userLoginResponse.headers()['set-cookie']?.split(';')[0] || '';
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test.describe('GET /api/v1/admin/share-requests', () => {
    test('should return paginated share requests for admin', async () => {
      const response = await apiContext.get('/api/v1/admin/share-requests', {
        headers: {
          Cookie: adminSessionCookie,
        },
        params: {
          pageNumber: 1,
          pageSize: 20,
        },
      });

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data).toHaveProperty('items');
      expect(data).toHaveProperty('totalCount');
      expect(data).toHaveProperty('pageNumber');
      expect(data).toHaveProperty('pageSize');
      expect(Array.isArray(data.items)).toBe(true);
    });

    test('should filter by status', async () => {
      const response = await apiContext.get('/api/v1/admin/share-requests', {
        headers: {
          Cookie: adminSessionCookie,
        },
        params: {
          status: 'Pending',
          pageNumber: 1,
          pageSize: 20,
        },
      });

      expect(response.status()).toBe(200);

      const data = await response.json();
      if (data.items.length > 0) {
        data.items.forEach((item: any) => {
          expect(item.status).toBe('Pending');
        });
      }
    });

    test('should filter by contribution type', async () => {
      const response = await apiContext.get('/api/v1/admin/share-requests', {
        headers: {
          Cookie: adminSessionCookie,
        },
        params: {
          type: 'NewGame',
          pageNumber: 1,
          pageSize: 20,
        },
      });

      expect(response.status()).toBe(200);

      const data = await response.json();
      if (data.items.length > 0) {
        data.items.forEach((item: any) => {
          expect(item.contributionType).toBe('NewGame');
        });
      }
    });

    test('should support full-text search', async () => {
      const response = await apiContext.get('/api/v1/admin/share-requests', {
        headers: {
          Cookie: adminSessionCookie,
        },
        params: {
          search: 'Catan',
          pageNumber: 1,
          pageSize: 20,
        },
      });

      expect(response.status()).toBe(200);
      const data = await response.json();
      expect(data).toHaveProperty('items');
    });

    test('should return 401 for unauthenticated requests', async () => {
      const response = await apiContext.get('/api/v1/admin/share-requests');

      expect(response.status()).toBe(401);
    });

    test('should return 403 for non-admin users', async () => {
      const response = await apiContext.get('/api/v1/admin/share-requests', {
        headers: {
          Cookie: userSessionCookie,
        },
      });

      expect(response.status()).toBe(403);
    });
  });

  test.describe('GET /api/v1/admin/share-requests/{id}', () => {
    test('should return detailed share request information', async () => {
      // First get a share request ID from the list
      const listResponse = await apiContext.get('/api/v1/admin/share-requests', {
        headers: {
          Cookie: adminSessionCookie,
        },
        params: {
          pageNumber: 1,
          pageSize: 1,
        },
      });

      const listData = await listResponse.json();
      if (listData.items.length === 0) {
        test.skip(true, 'No share requests available for testing');
        return;
      }

      const shareRequestId = listData.items[0].id;

      const response = await apiContext.get(
        `/api/v1/admin/share-requests/${shareRequestId}`,
        {
          headers: {
            Cookie: adminSessionCookie,
          },
        }
      );

      expect(response.status()).toBe(200);

      const data = await response.json();
      expect(data).toHaveProperty('id');
      expect(data).toHaveProperty('status');
      expect(data).toHaveProperty('contributionType');
      expect(data).toHaveProperty('game');
      expect(data).toHaveProperty('contributor');
      expect(data).toHaveProperty('attachedDocuments');
      expect(data).toHaveProperty('history');
      expect(data).toHaveProperty('lockStatus');
    });

    test('should return 404 for non-existent share request', async () => {
      const fakeId = '00000000-0000-0000-0000-000000000000';

      const response = await apiContext.get(
        `/api/v1/admin/share-requests/${fakeId}`,
        {
          headers: {
            Cookie: adminSessionCookie,
          },
        }
      );

      expect(response.status()).toBe(404);
    });

    test('should return 401 for unauthenticated requests', async () => {
      const response = await apiContext.get(
        '/api/v1/admin/share-requests/00000000-0000-0000-0000-000000000000'
      );

      expect(response.status()).toBe(401);
    });
  });

  test.describe('POST /api/v1/admin/share-requests/{id}/approve', () => {
    test('should approve share request with admin notes', async () => {
      test.skip(true, 'Requires active review lock setup - tested via E2E');
    });

    test('should return 409 when admin does not have lock', async () => {
      test.skip(true, 'Requires multi-admin setup - tested via E2E');
    });

    test('should return 404 for non-existent share request', async () => {
      const fakeId = '00000000-0000-0000-0000-000000000000';

      const response = await apiContext.post(
        `/api/v1/admin/share-requests/${fakeId}/approve`,
        {
          headers: {
            Cookie: adminSessionCookie,
          },
          data: {
            adminNotes: 'Approved',
          },
        }
      );

      expect([400, 404]).toContain(response.status());
    });
  });

  test.describe('POST /api/v1/admin/share-requests/{id}/reject', () => {
    test('should reject share request with reason', async () => {
      test.skip(true, 'Requires active review lock setup - tested via E2E');
    });

    test('should require reason field', async () => {
      const fakeId = '00000000-0000-0000-0000-000000000000';

      const response = await apiContext.post(
        `/api/v1/admin/share-requests/${fakeId}/reject`,
        {
          headers: {
            Cookie: adminSessionCookie,
          },
          data: {
            // Missing required 'reason' field
          },
        }
      );

      expect(response.status()).toBe(400);
    });
  });

  test.describe('POST /api/v1/admin/share-requests/{id}/request-changes', () => {
    test('should request changes with feedback', async () => {
      test.skip(true, 'Requires active review lock setup - tested via E2E');
    });

    test('should require feedback field', async () => {
      const fakeId = '00000000-0000-0000-0000-000000000000';

      const response = await apiContext.post(
        `/api/v1/admin/share-requests/${fakeId}/request-changes`,
        {
          headers: {
            Cookie: adminSessionCookie,
          },
          data: {
            // Missing required 'feedback' field
          },
        }
      );

      expect(response.status()).toBe(400);
    });
  });

  test.describe('Authorization Tests', () => {
    test('all endpoints should return 401 without authentication', async () => {
      const endpoints = [
        { method: 'get', path: '/api/v1/admin/share-requests' },
        { method: 'get', path: '/api/v1/admin/share-requests/00000000-0000-0000-0000-000000000000' },
        { method: 'post', path: '/api/v1/admin/share-requests/00000000-0000-0000-0000-000000000000/approve' },
        { method: 'post', path: '/api/v1/admin/share-requests/00000000-0000-0000-0000-000000000000/reject' },
        { method: 'post', path: '/api/v1/admin/share-requests/00000000-0000-0000-0000-000000000000/request-changes' },
      ];

      for (const endpoint of endpoints) {
        const response = await (apiContext as any)[endpoint.method](endpoint.path, {
          data: endpoint.method === 'post' ? {} : undefined,
        });

        expect(response.status()).toBe(401);
      }
    });

    test('all endpoints should return 403 for non-admin users', async () => {
      const endpoints = [
        { method: 'get', path: '/api/v1/admin/share-requests' },
        { method: 'get', path: '/api/v1/admin/share-requests/00000000-0000-0000-0000-000000000000' },
        { method: 'post', path: '/api/v1/admin/share-requests/00000000-0000-0000-0000-000000000000/approve' },
        { method: 'post', path: '/api/v1/admin/share-requests/00000000-0000-0000-0000-000000000000/reject' },
        { method: 'post', path: '/api/v1/admin/share-requests/00000000-0000-0000-0000-000000000000/request-changes' },
      ];

      for (const endpoint of endpoints) {
        const response = await (apiContext as any)[endpoint.method](endpoint.path, {
          headers: {
            Cookie: userSessionCookie,
          },
          data: endpoint.method === 'post' ? {} : undefined,
        });

        expect(response.status()).toBe(403);
      }
    });
  });

  test.describe('Rate Limiting', () => {
    test('should apply rate limiting to admin endpoints', async () => {
      test.skip(true, 'Rate limiting tests require load testing setup');
    });
  });
});
