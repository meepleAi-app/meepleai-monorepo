/**
 * Admin Game Creation E2E Tests
 * @slow - 39 test cases, API integration and authorization tests
 *
 * Tests for game creation by admin and editor users.
 * Covers the API-level game creation flow. The UI-level tests drove the game
 * picker of the retired PDF import wizard and were removed with it (#4257).
 *
 * Test Coverage:
 * - Admin can create game via API ✅ (Real Backend)
 * - Editor can create game via API ✅ (Real Backend)
 * - Regular user cannot create game (403) ⏭️ (Error injection - skipped)
 * - Validation errors are handled correctly ✅ (Real Backend)
 * - Game creation with all required fields ✅ (Real Backend)
 *
 * @see apps/api/src/Api/BoundedContexts/GameManagement
 */

import { test, expect } from './fixtures';

/**
 * API Configuration
 */
const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * API Endpoints
 */
const ENDPOINTS = {
  LOGIN: '/api/v1/auth/login',
  LOGOUT: '/api/v1/auth/logout',
  GAMES: '/api/v1/games',
} as const;

/**
 * HTTP Status Codes
 */
const HTTP_STATUS = {
  OK: 200,
  CREATED: 201,
  BAD_REQUEST: 400,
  UNAUTHORIZED: 401,
  FORBIDDEN: 403,
  NOT_FOUND: 404,
} as const;

/**
 * User Credentials
 */
const CREDENTIALS = {
  ADMIN: {
    email: 'admin@meepleai.dev',
    password: 'Demo123!',
  },
  EDITOR: {
    email: 'editor@meepleai.dev',
    password: 'Demo123!',
  },
  USER: {
    email: 'demo@meepleai.dev',
    password: 'Demo123!',
  },
} as const;

/**
 * Test Game Data
 */
const TEST_GAME = {
  title: 'Test Board Game',
  publisher: 'Test Publisher',
  yearPublished: 2024,
  minPlayers: 2,
  maxPlayers: 4,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 60,
} as const;

/**
 * Generate unique game title for tests
 */
const generateGameTitle = (prefix: string) => `${prefix} ${Date.now()}`;

test.describe('Admin Game Creation', () => {
  /**
   * Test Group 1: API-Level Game Creation with Real Backend
   * These tests require a running backend with seeded users
   */
  test.describe('API Game Creation (Real Backend)', () => {
    test.describe.configure({ mode: 'serial' });

    test('Admin can create a game via API', async ({ playwright }) => {
      const apiContext = await playwright.request.newContext({
        baseURL: API_BASE,
        extraHTTPHeaders: { 'Content-Type': 'application/json' },
      });

      try {
        // Login as admin
        const loginResponse = await apiContext.post(ENDPOINTS.LOGIN, {
          data: CREDENTIALS.ADMIN,
        });

        // Skip if admin login fails (not seeded)
        if (loginResponse.status() !== HTTP_STATUS.OK) {
          test.skip(true, 'Admin user not available in test environment');
          return;
        }

        const adminCookie = loginResponse.headers()['set-cookie']?.split(';')[0] || '';

        // Create game
        const gameTitle = generateGameTitle('Admin Test Game');
        const response = await apiContext.post(ENDPOINTS.GAMES, {
          headers: { Cookie: adminCookie },
          data: {
            ...TEST_GAME,
            title: gameTitle,
          },
        });

        expect(response.status()).toBe(HTTP_STATUS.CREATED);

        const game = await response.json();
        expect(game.id).toBeDefined();
        expect(game.title).toBe(gameTitle);
        expect(game.publisher).toBe(TEST_GAME.publisher);
        expect(game.minPlayers).toBe(TEST_GAME.minPlayers);
        expect(game.maxPlayers).toBe(TEST_GAME.maxPlayers);
      } finally {
        await apiContext.dispose();
      }
    });

    test('Editor can create a game via API', async ({ playwright }) => {
      const apiContext = await playwright.request.newContext({
        baseURL: API_BASE,
        extraHTTPHeaders: { 'Content-Type': 'application/json' },
      });

      try {
        // Login as editor
        const loginResponse = await apiContext.post(ENDPOINTS.LOGIN, {
          data: CREDENTIALS.EDITOR,
        });

        // Skip if editor login fails (not seeded)
        if (loginResponse.status() !== HTTP_STATUS.OK) {
          test.skip(true, 'Editor user not available in test environment');
          return;
        }

        const editorCookie = loginResponse.headers()['set-cookie']?.split(';')[0] || '';

        // Create game
        const gameTitle = generateGameTitle('Editor Test Game');
        const response = await apiContext.post(ENDPOINTS.GAMES, {
          headers: { Cookie: editorCookie },
          data: {
            ...TEST_GAME,
            title: gameTitle,
          },
        });

        expect(response.status()).toBe(HTTP_STATUS.CREATED);

        const game = await response.json();
        expect(game.id).toBeDefined();
        expect(game.title).toBe(gameTitle);
      } finally {
        await apiContext.dispose();
      }
    });

    test('Regular user cannot create a game (403 Forbidden)', async ({ playwright }) => {
      const apiContext = await playwright.request.newContext({
        baseURL: API_BASE,
        extraHTTPHeaders: { 'Content-Type': 'application/json' },
      });

      try {
        // Login as regular user
        const loginResponse = await apiContext.post(ENDPOINTS.LOGIN, {
          data: CREDENTIALS.USER,
        });

        // Skip if user login fails (not seeded)
        if (loginResponse.status() !== HTTP_STATUS.OK) {
          test.skip(true, 'Regular user not available in test environment');
          return;
        }

        const userCookie = loginResponse.headers()['set-cookie']?.split(';')[0] || '';

        // Try to create game
        const response = await apiContext.post(ENDPOINTS.GAMES, {
          headers: { Cookie: userCookie },
          data: {
            ...TEST_GAME,
            title: generateGameTitle('Unauthorized Game'),
          },
        });

        expect(response.status()).toBe(HTTP_STATUS.FORBIDDEN);
      } finally {
        await apiContext.dispose();
      }
    });

    test('Unauthenticated user cannot create a game (401 Unauthorized)', async ({ playwright }) => {
      const apiContext = await playwright.request.newContext({
        baseURL: API_BASE,
        extraHTTPHeaders: { 'Content-Type': 'application/json' },
      });

      try {
        const response = await apiContext.post(ENDPOINTS.GAMES, {
          data: {
            ...TEST_GAME,
            title: generateGameTitle('Unauthenticated Game'),
          },
        });

        expect(response.status()).toBe(HTTP_STATUS.UNAUTHORIZED);
      } finally {
        await apiContext.dispose();
      }
    });
  });

  /**
   * Test Group 2: API Validation Tests
   */
  test.describe('API Validation', () => {
    test('Game creation requires title', async ({ playwright }) => {
      const apiContext = await playwright.request.newContext({
        baseURL: API_BASE,
        extraHTTPHeaders: { 'Content-Type': 'application/json' },
      });

      try {
        // Login as admin
        const loginResponse = await apiContext.post(ENDPOINTS.LOGIN, {
          data: CREDENTIALS.ADMIN,
        });

        if (loginResponse.status() !== HTTP_STATUS.OK) {
          test.skip(true, 'Admin user not available in test environment');
          return;
        }

        const adminCookie = loginResponse.headers()['set-cookie']?.split(';')[0] || '';

        // Try to create game without title
        const response = await apiContext.post(ENDPOINTS.GAMES, {
          headers: { Cookie: adminCookie },
          data: {
            publisher: TEST_GAME.publisher,
            yearPublished: TEST_GAME.yearPublished,
          },
        });

        expect(response.status()).toBe(HTTP_STATUS.BAD_REQUEST);
      } finally {
        await apiContext.dispose();
      }
    });

    test('Game creation accepts only title (optional fields)', async ({ playwright }) => {
      const apiContext = await playwright.request.newContext({
        baseURL: API_BASE,
        extraHTTPHeaders: { 'Content-Type': 'application/json' },
      });

      try {
        // Login as admin
        const loginResponse = await apiContext.post(ENDPOINTS.LOGIN, {
          data: CREDENTIALS.ADMIN,
        });

        if (loginResponse.status() !== HTTP_STATUS.OK) {
          test.skip(true, 'Admin user not available in test environment');
          return;
        }

        const adminCookie = loginResponse.headers()['set-cookie']?.split(';')[0] || '';

        // Create game with only title
        const gameTitle = generateGameTitle('Minimal Game');
        const response = await apiContext.post(ENDPOINTS.GAMES, {
          headers: { Cookie: adminCookie },
          data: { title: gameTitle },
        });

        expect(response.status()).toBe(HTTP_STATUS.CREATED);

        const game = await response.json();
        expect(game.id).toBeDefined();
        expect(game.title).toBe(gameTitle);
      } finally {
        await apiContext.dispose();
      }
    });
  });
});
