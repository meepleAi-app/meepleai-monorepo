import { test as base, Page, Route } from '@playwright/test';

const API_BASE = 'http://localhost:5080';

/**
 * Setup mock auth routes for testing (based on authenticated.spec.ts pattern)
 * This is more reliable than trying to use real login which has UI/timing issues
 */
async function setupMockAuth(page: Page, role: 'Admin' | 'Editor' | 'User' = 'Admin', email: string = 'admin@meepleai.dev') {
  const userResponse = {
    user: {
      id: `${role.toLowerCase()}-test-id`,
      email,
      displayName: `Test ${role}`,
      role
    },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
  };

  // Mock /auth/me to return authenticated user
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse)
    });
  });

  // Mock login endpoint (in case it's called)
  await page.route(`${API_BASE}/api/v1/auth/login`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse)
    });
  });

  return userResponse;
}

/**
 * Setup mock games API routes (similar to pdf-preview.spec.ts pattern)
 */
async function setupGamesRoutes(page: Page) {
  const games = [
    {
      id: 'chess',
      name: 'Chess',
      description: 'Classic chess game',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    },
    {
      id: 'tic-tac-toe',
      name: 'Tic-Tac-Toe',
      description: 'Simple game',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
  ];

  // Mock GET /api/v1/games endpoint
  await page.route(`${API_BASE}/api/v1/games`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(games)
      });
    } else {
      await route.continue();
    }
  });

  // Mock setup guide generation API
  await page.route(`${API_BASE}/api/v1/setup-guide/**`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          steps: [
            { id: '1', description: 'Set up the board', isOptional: false, references: [] },
            { id: '2', description: 'Place the pieces', isOptional: false, references: [] },
            { id: '3', description: 'Learn special moves', isOptional: true, references: [{ page: 5 }] }
          ],
          estimatedTime: '5 minutes',
          confidenceScore: 0.92
        })
      });
    } else {
      await route.continue();
    }
  });

  return { games };
}

/**
 * Login as admin user - uses mock authentication for reliability
 * Note: This only sets up auth mocking. Tests should set up their own API endpoint mocks
 * BEFORE navigating to pages that need those endpoints.
 */
export async function loginAsAdmin(page: Page, skipNavigation: boolean = false) {
  // Setup mock auth before navigating
  await setupMockAuth(page, 'Admin', 'admin@meepleai.dev');

  // Only navigate if not skipped (allows tests to set up additional mocks first)
  if (!skipNavigation) {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    console.log('✅ Mock admin authentication setup complete');
  }
}

/**
 * Login as editor user - uses mock authentication
 */
export async function loginAsEditor(page: Page, skipNavigation: boolean = false) {
  await setupMockAuth(page, 'Editor', 'editor@meepleai.dev');

  if (!skipNavigation) {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    console.log('✅ Mock editor authentication setup complete');
  }
}

/**
 * Login as regular user - uses mock authentication
 */
export async function loginAsUser(page: Page, skipNavigation: boolean = false) {
  await setupMockAuth(page, 'User', 'user@meepleai.dev');

  if (!skipNavigation) {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    console.log('✅ Mock user authentication setup complete');
  }
}

/**
 * Extended test with pre-authenticated pages using mock auth
 * Usage: test.describe('My tests', () => { test('...', async ({ adminPage }) => { ... }) })
 */
export const test = base.extend<{
  adminPage: Page;
  editorPage: Page;
  userPage: Page;
  setupUserPage: Page;
}>({
  adminPage: async ({ page }, use) => {
    await loginAsAdmin(page);
    await use(page);
  },
  editorPage: async ({ page }, use) => {
    await loginAsEditor(page);
    await use(page);
  },
  userPage: async ({ page }, use) => {
    await loginAsUser(page);
    await use(page);
  },
  setupUserPage: async ({ page }, use) => {
    // Setup auth WITHOUT navigation
    await loginAsUser(page, true);
    
    // Setup games routes BEFORE navigation (like pdf-preview pattern)
    await setupGamesRoutes(page);

    await use(page);
  },
});

export { expect } from '@playwright/test';
