/**
 * API Keys E2E Flow Tests - Issue #914
 *
 * Complete end-to-end testing of API key lifecycle:
 * 1. Create API key
 * 2. Copy/extract plaintext key
 * 3. Use key in authenticated API request
 * 4. Revoke key
 * 5. Verify key is invalid after revocation
 *
 * @see apps/api/src/Api/BoundedContexts/Authentication
 * @see Issue #914 - E2E + Security audit + Stress test
 */

import { test, expect } from '@playwright/test';
import type { APIRequestContext } from '@playwright/test';

const BASE_URL = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('API Keys E2E Flow - Issue #914', () => {
  let apiContext: APIRequestContext;
  let adminAuthCookie: string;
  let testUserId: string;

  test.beforeAll(async ({ playwright }) => {
    // Create API context
    apiContext = await playwright.request.newContext({
      baseURL: BASE_URL,
      extraHTTPHeaders: {
        'Content-Type': 'application/json',
      },
    });

    // Login as admin to get authentication cookie
    const loginResponse = await apiContext.post('/api/v1/auth/login', {
      data: {
        email: 'demo@meepleai.dev',
        password: 'Demo123!',
      },
    });

    expect(loginResponse.status()).toBe(200);

    const loginData = await loginResponse.json();
    testUserId = loginData.user.id;

    // Extract session cookie
    const cookies = loginResponse.headers()['set-cookie'];
    expect(cookies).toBeDefined();
    const sessionCookie = cookies?.split(';')[0];
    adminAuthCookie = sessionCookie || '';
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test('E2E Flow: Create → Use → Revoke → Verify Invalid', async () => {
    // STEP 1: Create API key
    const createResponse = await apiContext.post('/api/v1/admin/api-keys', {
      headers: {
        Cookie: adminAuthCookie,
      },
      data: {
        keyName: `E2E Test Key ${Date.now()}`,
        scopes: 'read:games,read:rules',
        expiresAt: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString(), // 1 year
        metadata: JSON.stringify({ test: true, environment: 'e2e' }),
      },
    });

    expect(createResponse.status()).toBe(201);

    const createData = await createResponse.json();
    expect(createData.plaintextKey).toBeDefined();
    expect(createData.keyPrefix).toBeDefined();
    expect(createData.id).toBeDefined();
    expect(createData.keyName).toContain('E2E Test Key');

    // STEP 2: Extract plaintext key (simulates user copying it)
    const plaintextKey = createData.plaintextKey;
    const keyId = createData.id;

    expect(plaintextKey).toMatch(/^[A-Za-z0-9+/=]+$/); // Valid Base64
    expect(plaintextKey.length).toBeGreaterThan(40); // Secure length

    // STEP 3: Use API key for authenticated request
    const gameListResponse = await apiContext.get('/api/v1/games', {
      headers: {
        Authorization: `Bearer ${plaintextKey}`,
      },
    });

    expect(gameListResponse.status()).toBe(200);

    const games = await gameListResponse.json();
    expect(Array.isArray(games)).toBe(true);

    // STEP 4: Revoke API key
    const revokeResponse = await apiContext.delete(`/api/v1/admin/api-keys/${keyId}`, {
      headers: {
        Cookie: adminAuthCookie,
      },
    });

    expect(revokeResponse.status()).toBe(204); // No Content

    // STEP 5: Verify key is invalid after revocation
    const invalidKeyResponse = await apiContext.get('/api/v1/games', {
      headers: {
        Authorization: `Bearer ${plaintextKey}`,
      },
    });

    expect(invalidKeyResponse.status()).toBe(401); // Unauthorized

    const errorData = await invalidKeyResponse.json();
    expect(errorData.error).toBeDefined();
    expect(errorData.error).toContain('Invalid');
  });

  test('should create API key with minimal data', async () => {
    const createResponse = await apiContext.post('/api/v1/admin/api-keys', {
      headers: {
        Cookie: adminAuthCookie,
      },
      data: {
        keyName: `Minimal Key ${Date.now()}`,
        scopes: 'read:games',
        // No expiresAt (never expires)
        // No metadata
      },
    });

    expect(createResponse.status()).toBe(201);

    const data = await createResponse.json();
    expect(data.plaintextKey).toBeDefined();
    expect(data.expiresAt).toBeNull();
  });

  test('should reject API key creation without authentication', async () => {
    const createResponse = await apiContext.post('/api/v1/admin/api-keys', {
      // No auth cookie
      data: {
        keyName: 'Unauthorized Key',
        scopes: 'read:games',
      },
    });

    expect(createResponse.status()).toBe(401);
  });

  test('should reject request with invalid API key format', async () => {
    const response = await apiContext.get('/api/v1/games', {
      headers: {
        Authorization: 'Bearer invalid_key_format_123',
      },
    });

    expect(response.status()).toBe(401);
  });

  test('should reject request with expired API key', async () => {
    // Create key with past expiration
    const createResponse = await apiContext.post('/api/v1/admin/api-keys', {
      headers: {
        Cookie: adminAuthCookie,
      },
      data: {
        keyName: `Expired Key ${Date.now()}`,
        scopes: 'read:games',
        expiresAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(), // Yesterday
      },
    });

    expect(createResponse.status()).toBe(201);

    const data = await createResponse.json();
    const expiredKey = data.plaintextKey;

    // Try to use expired key
    const response = await apiContext.get('/api/v1/games', {
      headers: {
        Authorization: `Bearer ${expiredKey}`,
      },
    });

    expect(response.status()).toBe(401);
  });
});
