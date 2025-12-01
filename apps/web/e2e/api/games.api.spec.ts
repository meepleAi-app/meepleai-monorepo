/**
 * Games API Tests - Playwright Native
 *
 * Native Playwright API tests for game management endpoints.
 *
 * @see apps/api/src/Api/BoundedContexts/GameManagement
 */

import { test, expect, APIRequestContext } from './fixtures/chromatic';

const BASE_URL = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Games API', () => {
  let apiContext: APIRequestContext;
  let sessionCookie: string;
  let testGameId: string;

  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({
      baseURL: BASE_URL,
      extraHTTPHeaders: {
        'Content-Type': 'application/json',
      },
    });

    // Login to get session
    const loginResponse = await apiContext.post('/api/v1/auth/login', {
      data: {
        email: 'demo@meepleai.dev',
        password: 'Demo123!',
      },
    });

    sessionCookie = loginResponse.headers()['set-cookie']?.split(';')[0] || '';
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test.describe('GET /api/v1/games', () => {
    test('should return list of games', async () => {
      const response = await apiContext.get('/api/v1/games', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      expect(response.status()).toBe(200);

      const games = await response.json();
      expect(Array.isArray(games)).toBeTruthy();
      expect(games.length).toBeGreaterThan(0);

      // Save first game ID for other tests
      if (games.length > 0) {
        testGameId = games[0].id;
        console.log(`Using test game ID: ${testGameId}`);
      }
    });

    test('should return games with valid schema', async () => {
      const response = await apiContext.get('/api/v1/games', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      const games = await response.json();
      const game = games[0];

      expect(game).toBeDefined();
      expect(game.id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
      expect(game.title).toBeDefined();
      expect(typeof game.title).toBe('string');
      expect(game.minPlayers).toBeGreaterThan(0);
      expect(game.maxPlayers).toBeGreaterThanOrEqual(game.minPlayers);
    });

    test('should fail without authentication', async () => {
      const response = await apiContext.get('/api/v1/games');

      expect(response.status()).toBe(401);
    });

    test('should complete in under 500ms', async () => {
      const startTime = Date.now();

      await apiContext.get('/api/v1/games', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      const duration = Date.now() - startTime;
      console.log(`GET /api/v1/games duration: ${duration}ms`);

      expect(duration).toBeLessThan(500);
    });
  });

  test.describe('GET /api/v1/games/:id', () => {
    test('should return game details by ID', async () => {
      // First get a game ID
      const gamesResponse = await apiContext.get('/api/v1/games', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      const games = await gamesResponse.json();
      const gameId = games[0].id;

      // Get game details
      const response = await apiContext.get(`/api/v1/games/${gameId}`, {
        headers: {
          Cookie: sessionCookie,
        },
      });

      expect(response.status()).toBe(200);

      const game = await response.json();
      expect(game.id).toBe(gameId);
      expect(game.title).toBeDefined();
      expect(game.publisher).toBeDefined();
    });

    test('should return 404 for non-existent game', async () => {
      const fakeId = '00000000-0000-0000-0000-000000000000';
      const response = await apiContext.get(`/api/v1/games/${fakeId}`, {
        headers: {
          Cookie: sessionCookie,
        },
      });

      expect(response.status()).toBe(404);
    });

    test('should return 400 for invalid UUID', async () => {
      const response = await apiContext.get('/api/v1/games/invalid-uuid', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      expect(response.status()).toBe(400);
    });
  });

  test.describe('POST /api/v1/games (Admin/Editor only)', () => {
    test.skip('should create new game with valid data (admin)', async () => {
      // Login as admin
      const adminLoginResponse = await apiContext.post('/api/v1/auth/login', {
        data: {
          email: 'admin@meepleai.dev',
          password: 'Demo123!',
        },
      });

      const adminCookie = adminLoginResponse.headers()['set-cookie']?.split(';')[0] || '';

      const timestamp = Date.now();
      const response = await apiContext.post('/api/v1/games', {
        headers: {
          Cookie: adminCookie,
        },
        data: {
          title: `Test Game ${timestamp}`,
          publisher: 'Test Publisher',
          yearPublished: 2024,
          minPlayers: 2,
          maxPlayers: 4,
          minPlayTimeMinutes: 30,
          maxPlayTimeMinutes: 60,
        },
      });

      expect(response.status()).toBe(201);

      const game = await response.json();
      expect(game.id).toBeDefined();
      expect(game.title).toBe(`Test Game ${timestamp}`);
    });

    test('should fail to create game as regular user', async () => {
      const response = await apiContext.post('/api/v1/games', {
        headers: {
          Cookie: sessionCookie, // Regular user
        },
        data: {
          title: 'Unauthorized Game',
          publisher: 'Test',
          yearPublished: 2024,
          minPlayers: 2,
          maxPlayers: 4,
          minPlayTimeMinutes: 30,
          maxPlayTimeMinutes: 60,
        },
      });

      expect(response.status()).toBe(403); // Forbidden
    });
  });

  test.describe('Search and Filtering', () => {
    test('should support query parameters', async () => {
      const response = await apiContext.get('/api/v1/games?search=chess', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      expect(response.ok()).toBeTruthy();

      const games = await response.json();
      expect(Array.isArray(games)).toBeTruthy();
    });

    test('should filter by player count', async () => {
      const response = await apiContext.get('/api/v1/games?minPlayers=2&maxPlayers=4', {
        headers: {
          Cookie: sessionCookie,
        },
      });

      expect(response.ok()).toBeTruthy();

      const games = await response.json();
      games.forEach((game: any) => {
        expect(game.minPlayers).toBeGreaterThanOrEqual(2);
        expect(game.maxPlayers).toBeLessThanOrEqual(4);
      });
    });
  });

  test.describe('Performance & Caching', () => {
    test('second request should be faster (cache hit)', async () => {
      // First request (cold)
      const startTime1 = Date.now();
      await apiContext.get('/api/v1/games', {
        headers: {
          Cookie: sessionCookie,
        },
      });
      const duration1 = Date.now() - startTime1;

      // Second request (warm cache)
      const startTime2 = Date.now();
      await apiContext.get('/api/v1/games', {
        headers: {
          Cookie: sessionCookie,
        },
      });
      const duration2 = Date.now() - startTime2;

      console.log(`Cold request: ${duration1}ms, Warm request: ${duration2}ms`);

      // Warm request should be faster (or at least not significantly slower)
      expect(duration2).toBeLessThanOrEqual(duration1 * 1.5);
    });
  });
});
