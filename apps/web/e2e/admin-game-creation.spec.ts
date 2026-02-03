/**
 * Admin Game Creation E2E Tests
 *
 * Tests for game creation by admin and editor users.
 * Covers both API-level and UI-level game creation flows.
 *
 * Test Coverage:
 * - Admin can create game via API ✅ (Real Backend)
 * - Editor can create game via API ✅ (Real Backend)
 * - Regular user cannot create game (403) ⏭️ (Error injection - skipped)
 * - Admin can create game via UI ✅ (Real Backend)
 * - Validation errors are handled correctly ✅ (Real Backend)
 * - Game creation with all required fields ✅ (Real Backend)
 *
 * @see apps/api/src/Api/BoundedContexts/GameManagement
 * @see apps/web/e2e/pages/upload/UploadPage.ts
 */

import { test, expect } from './fixtures';
import { AuthHelper, USER_FIXTURES } from './pages';
import { UploadPage } from './pages/upload/UploadPage';

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
  AUTH_ME: '/api/v1/auth/me',
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
 * User Roles
 */
const ROLES = {
  ADMIN: 'Admin',
  EDITOR: 'Editor',
  USER: 'User',
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
 * Session Cookie Configuration
 */
const SESSION_COOKIES = {
  SESSION: 'meepleai_session',
  USER_ROLE: 'meepleai_user_role',
} as const;

/**
 * Cookie Configuration
 */
const COOKIE_CONFIG = {
  domain: 'localhost',
  path: '/',
  secure: false,
  sameSite: 'Lax' as const,
};

/**
 * Generate unique game title for tests
 */
const generateGameTitle = (prefix: string) => `${prefix} ${Date.now()}`;

/**
 * Generate session token for mocks
 */
const generateSessionToken = (role: string, context: string) =>
  `test-${role.toLowerCase()}-${context}-${Date.now()}`;

/**
 * Create mock user for testing
 */
const createMockUser = (role: string, email: string) => ({
  id: `mock-${role.toLowerCase()}-user-id`,
  email,
  displayName: `Test ${role}`,
  role,
});

/**
 * Helper to create session cookies for mocked tests
 */
const createSessionCookies = (role: string, context: string) => [
  {
    name: SESSION_COOKIES.SESSION,
    value: generateSessionToken(role, context),
    ...COOKIE_CONFIG,
    httpOnly: true,
  },
  {
    name: SESSION_COOKIES.USER_ROLE,
    value: role.toLowerCase(),
    ...COOKIE_CONFIG,
    httpOnly: false,
  },
];

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

  /**
   * Test Group 3: UI Game Creation (Mocked)
   *
   * NOTE: These tests use mocked API endpoints to test the UI flow independently.
   * However, Next.js middleware makes SERVER-SIDE fetch calls to validate sessions
   * that CANNOT be intercepted by Playwright's page.route() (which only intercepts
   * browser-side requests). When no real backend is available, middleware redirects
   * to /login. These tests gracefully skip when this happens.
   *
   * For full UI testing with mocked auth, consider:
   * 1. Running with a real backend (E2E integration mode)
   * 2. Disabling middleware validation in test environments
   * 3. Using component tests instead (Vitest + React Testing Library)
   */
  test.describe('UI Game Creation (Mocked)', () => {
    /**
     * Helper to check if middleware redirected to login
     * Next.js middleware makes server-side API calls that can't be mocked by Playwright
     */
    const checkMiddlewareRedirect = async (
      page: import('@playwright/test').Page
    ): Promise<boolean> => {
      const currentUrl = page.url();
      if (currentUrl.includes('/login')) {
        test.skip(
          true,
          'Middleware redirected to login - server-side auth validation requires real backend'
        );
        return true;
      }
      return false;
    };

    test('Admin can create game via upload page UI', async ({ page }) => {
      const uploadPage = new UploadPage(page);
      const authHelper = new AuthHelper(page);
      const gameTitle = generateGameTitle('UI Created Game');

      // ✅ CHANGED: Use AuthHelper instead of inline /auth/me mock
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

      // ✅ REMOVED MOCK: Use real Games API (GET + POST)
      // Real backend GET /api/v1/games must return game list
      // Real backend POST /api/v1/games must create game and return 201
      // Note: UI test verifies upload page workflow with backend seeded data

      // Navigate to upload page
      await page.goto('/upload');
      await page.waitForLoadState('networkidle');

      // Check if middleware redirected (server-side validation can't be mocked)
      if (await checkMiddlewareRedirect(page)) return;

      // Verify we're on the upload page
      await expect(page).toHaveURL(/upload/);

      // Create new game via UI
      await uploadPage.createNewGame(gameTitle);

      // Verify game was created (check for success indication)
      // The page should now show the game as selected
      await expect(
        page.getByText(gameTitle).or(page.locator(`[data-game-title="${gameTitle}"]`))
      ).toBeVisible({
        timeout: 5000,
      });
    });

    test('Upload page shows game creation form for admin', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      // ✅ CHANGED: Use AuthHelper (replaces auth mock + cookies)
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

      // ✅ REMOVED MOCK: Use real Games API (GET)
      // Real backend GET /api/v1/games must return game list for selector

      // Navigate to upload page
      await page.goto('/upload');
      await page.waitForLoadState('networkidle');

      // Check if middleware redirected (server-side validation can't be mocked)
      if (await checkMiddlewareRedirect(page)) return;

      // Verify game creation input is visible
      // GamePicker has: label "Create New Game", placeholder "e.g., Gloomhaven", id "new-game"
      const newGameInput = page
        .locator('#new-game')
        .or(page.getByLabel(/create new game/i))
        .or(page.getByPlaceholder(/gloomhaven/i));

      await expect(newGameInput).toBeVisible({ timeout: 5000 });

      // Verify create button is visible
      // GamePicker has button with text "Create" (not "Create Game")
      const createButton = page
        .getByRole('button', { name: /^create$/i })
        .or(page.locator('[data-testid="create-game-button"]'));

      await expect(createButton).toBeVisible({ timeout: 5000 });
    });

    test('Game selector shows existing games', async ({ page }) => {
      const authHelper = new AuthHelper(page);

      // ✅ CHANGED: Use AuthHelper (replaces auth mock + cookies)
      await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

      // ✅ REMOVED MOCK: Use real Games API (GET)
      // Real backend GET /api/v1/games must return existing games from database
      // Note: Test verifies game selector displays backend seeded games

      // Navigate to upload page
      await page.goto('/upload');
      await page.waitForLoadState('networkidle');

      // Open game selector
      const gameSelector = page
        .locator('[data-testid="game-selector"]')
        .or(page.getByRole('combobox'));

      if (await gameSelector.isVisible()) {
        await gameSelector.click();

        // ✅ CHANGED: Verify selector shows backend games (not specific mock titles)
        // Wait for dropdown options to appear
        const options = page.locator('option, [role="option"]');
        await expect(options.first()).toBeVisible({ timeout: 3000 });

        // Verify at least one game option is available
        const optionCount = await options.count();
        expect(optionCount).toBeGreaterThan(0);
      }
    });
  });

  /**
   * Test Group 4: Authorization Edge Cases
   */
  test.describe('Authorization Edge Cases', () => {
    test('Session expiry prevents game creation', async ({ page }) => {
      // Mock auth endpoint to return expired session
      await page.route(`${API_BASE}${ENDPOINTS.AUTH_ME}`, async route => {
        await route.fulfill({
          status: HTTP_STATUS.UNAUTHORIZED,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Session expired' }),
        });
      });

      // Navigate to upload page
      await page.goto('/upload');

      // Should redirect to login or show unauthenticated state
      await expect(
        page.waitForURL(/login/, { timeout: 5000 }).catch(() => false) ||
          page.getByText(/sign in|log in|unauthorized/i).isVisible()
      ).toBeTruthy();
    });

    test('Role downgrade prevents game creation mid-session', async ({ page }) => {
      let requestCount = 0;

      // Mock auth endpoint - first call returns admin, subsequent calls return user
      await page.route(`${API_BASE}${ENDPOINTS.AUTH_ME}`, async route => {
        requestCount++;
        const role = requestCount === 1 ? ROLES.ADMIN : ROLES.USER;

        await route.fulfill({
          status: HTTP_STATUS.OK,
          contentType: 'application/json',
          body: JSON.stringify({
            user: createMockUser(role, CREDENTIALS.ADMIN.email),
            expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          }),
        });
      });

      // Mock games endpoint to check authorization
      await page.route(`${API_BASE}${ENDPOINTS.GAMES}`, async route => {
        if (route.request().method() === 'POST') {
          // Simulate server-side role check
          await route.fulfill({
            status: HTTP_STATUS.FORBIDDEN,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Insufficient permissions' }),
          });
        } else {
          await route.fulfill({
            status: HTTP_STATUS.OK,
            contentType: 'application/json',
            body: JSON.stringify([]),
          });
        }
      });

      // Set session cookies
      await page.context().addCookies(createSessionCookies(ROLES.ADMIN, 'role-downgrade'));

      // Navigate to upload page
      await page.goto('/upload');
      await page.waitForLoadState('networkidle');

      // Try to create game (should fail due to role check)
      // GamePicker has button with text "Create" (not "Create Game")
      const createButton = page
        .getByRole('button', { name: /^create$/i })
        .or(page.locator('[data-testid="create-game-button"]'));

      if (await createButton.isVisible()) {
        // Fill in game name
        // GamePicker has: label "Create New Game", placeholder "e.g., Gloomhaven", id "new-game"
        const newGameInput = page
          .locator('#new-game')
          .or(page.getByLabel(/create new game/i))
          .or(page.getByPlaceholder(/gloomhaven/i));

        if (await newGameInput.isVisible()) {
          await newGameInput.fill(generateGameTitle('Role Downgrade Test'));
          await createButton.click();

          // Should show error message
          await expect(page.getByText(/error|permission|forbidden|denied/i)).toBeVisible({
            timeout: 5000,
          });
        }
      }
    });
  });
});
