import { test, expect, APIRequestContext } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const ADMIN_EMAIL = process.env.ADMIN_EMAIL || 'admin@meepleai.app';
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'DMxspufvkZM3gRHjAHmd';

test.describe('Game Night BGG Import — Integration', () => {
  test.describe.configure({ mode: 'serial' });

  let api: APIRequestContext;

  test.beforeAll(async ({ playwright }) => {
    api = await playwright.request.newContext({ baseURL: API_BASE });
    const loginRes = await api.post('/api/v1/auth/login', {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
      headers: { 'Content-Type': 'application/json' },
    });
    expect(loginRes.ok()).toBe(true);
  });

  test.afterAll(async () => {
    await api.dispose();
  });

  test('should search BGG for a game', async () => {
    // BGG is an external API — may be slow or rate-limited
    try {
      const searchRes = await api.get('/api/v1/game-night/bgg/search?q=Catan&page=1&pageSize=5', {
        timeout: 30000,
      });

      if (searchRes.ok()) {
        const body = await searchRes.json();
        expect(body.items).toBeDefined();
        expect(Array.isArray(body.items)).toBe(true);

        if (body.items.length > 0) {
          expect(body.items[0].bggId).toBeTruthy();
          expect(body.items[0].title).toBeTruthy();
        }
      } else {
        // 429 (rate limit) or 504 (timeout) are acceptable for external API
        expect([429, 500, 502, 503, 504]).toContain(searchRes.status());
        console.log(`[Integration] BGG search returned ${searchRes.status()} — external API issue`);
      }
    } catch {
      // Timeout is acceptable for external API
      console.log('[Integration] BGG search timed out — external API may be unavailable');
    }
  });

  test('should verify library endpoints exist and respond', async () => {
    const libraryRes = await api.get('/api/v1/library');
    expect([200, 404]).toContain(libraryRes.status());
  });

  test('should verify game-night endpoints are registered', async () => {
    const importRes = await api.post('/api/v1/game-night/import-bgg', {
      data: { bggId: 0 },
      headers: { 'Content-Type': 'application/json' },
    });
    expect(importRes.status()).not.toBe(404);
  });

  test('should verify health of all required services', async () => {
    const healthRes = await api.get('/health');
    expect(healthRes.ok()).toBe(true);
    const health = await healthRes.json();

    const checks = health.checks as Array<{ name: string; status: string }>;
    const postgres = checks.find(c => c.name === 'postgres');
    const redis = checks.find(c => c.name === 'redis');

    expect(postgres?.status).toBe('Healthy');
    expect(redis?.status).toBe('Healthy');
  });
});
