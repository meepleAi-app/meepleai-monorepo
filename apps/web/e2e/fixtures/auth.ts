import { test as base, Page, Route } from '@playwright/test';

// Issue #841: Make API_BASE configurable via environment variables
const API_BASE = process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080';

// Log configuration in CI for debugging
if (process.env.CI) {
  console.log(`[E2E Config] API_BASE: ${API_BASE}`);
}

/**
 * Authenticate via real backend API and get session cookie
 * Use this for E2E tests that require real backend integration
 *
 * @param page - Playwright page object
 * @param email - User email
 * @param password - User password
 * @returns true if authentication successful, false otherwise
 */
export async function authenticateViaAPI(
  page: Page,
  email: string,
  password: string
): Promise<boolean> {
  try {
    // Call login API directly
    const response = await page.request.post(`${API_BASE}/api/v1/auth/login`, {
      headers: {
        'Content-Type': 'application/json',
      },
      data: {
        email: email,
        password: password,
      },
    });

    if (response.ok()) {
      console.log('✅ Authentication successful via API');
      // Cookies should be automatically set by the response
      return true;
    } else {
      const body = await response.text();
      console.error('❌ Authentication failed:', response.status(), body);
      return false;
    }
  } catch (error) {
    console.error('❌ Authentication error:', error);
    return false;
  }
}

/**
 * Setup mock auth routes for testing (based on authenticated.spec.ts pattern)
 * This is more reliable than trying to use real login which has UI/timing issues
 */
export async function setupMockAuth(page: Page, role: 'Admin' | 'Editor' | 'User' = 'Admin', email: string = 'admin@meepleai.dev') {
  const userResponse = {
    user: {
      id: `${role.toLowerCase()}-test-id`,
      email,
      displayName: `Test ${role}`,
      role
    },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
  };

  // Catch-all for any unmocked API calls - MUST BE FIRST
  // More specific route mocks below will override this
  // Issue #841: Improve catch-all with logging and better error handling
  await page.route(`${API_BASE}/api/**`, async (route) => {
    const url = route.request().url();
    const method = route.request().method();

    // Log unmocked calls for debugging (visible in CI logs)
    if (process.env.CI || process.env.DEBUG_MOCKS) {
      console.warn(`⚠️  Unmocked API call: ${method} ${url}`);
    }

    // Return appropriate response based on HTTP method
    if (method === 'GET') {
      // GET requests: return empty array (safe default)
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    } else if (method === 'POST' || method === 'PUT' || method === 'PATCH') {
      // Mutation requests: return success response
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true })
      });
    } else if (method === 'DELETE') {
      // DELETE requests: return 204 No Content
      await route.fulfill({
        status: 204
      });
    } else {
      // Other methods: return generic success
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: [] })
      });
    }
  });

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

  // Mock games endpoint (commonly needed by authenticated pages)
  await page.route(`${API_BASE}/api/v1/games**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([])
    });
  });

  // Mock chat/threads endpoint
  await page.route(`${API_BASE}/api/v1/chat/threads**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([])
    });
  });

  // Mock user profile endpoint
  await page.route(`${API_BASE}/api/v1/users/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: userResponse.user.id,
        email: userResponse.user.email,
        displayName: userResponse.user.displayName,
        role: userResponse.user.role,
        createdAt: new Date().toISOString()
      })
    });
  });

  // Mock settings/profile endpoints
  await page.route(`${API_BASE}/api/v1/settings/**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true })
    });
  });

  // Mock RuleSpec/editor endpoints (for Editor role)
  if (role === 'Editor' || role === 'Admin') {
    await page.route(`${API_BASE}/api/v1/rulespecs**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route(`${API_BASE}/api/v1/versions**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });
  }

  // Mock admin endpoints (for admin role)
  if (role === 'Admin') {
    await page.route(`${API_BASE}/api/v1/admin/**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: [] })
      });
    });

    await page.route(`${API_BASE}/api/v1/configuration**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route(`${API_BASE}/api/v1/users**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route(`${API_BASE}/api/v1/analytics**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ metrics: [] })
      });
    });
  }

  return userResponse;
}

/**
 * Setup mock auth with 403 Forbidden for specific routes (RBAC testing)
 * Use this for testing that users are properly denied access to protected routes
 *
 * @param page - Playwright page object
 * @param role - User role (Admin, Editor, User)
 * @param forbiddenRoutes - Array of route patterns that should return 403 (e.g., ['/admin/**'])
 * @param email - User email
 */
export async function setupMockAuthWithForbidden(
  page: Page,
  role: 'Admin' | 'Editor' | 'User',
  forbiddenRoutes: string[] = [],
  email: string = `${role.toLowerCase()}@meepleai.dev`
) {
  const userResponse = {
    user: {
      id: `${role.toLowerCase()}-test-id`,
      email,
      displayName: `Test ${role}`,
      role
    },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
  };

  // Setup base auth mocking
  await setupMockAuth(page, role, email);

  // Override specific routes to return 403 Forbidden
  for (const routePattern of forbiddenRoutes) {
    // Convert route pattern to regex (e.g., '/admin/**' -> /\/admin\/.*/
    const regexPattern = routePattern
      .replace(/\*\*/g, '.*')
      .replace(/\*/g, '[^/]*')
      .replace(/\//g, '\\/');

    await page.route(new RegExp(regexPattern), async (route) => {
      // Return 403 Forbidden for unauthorized access
      await route.fulfill({
        status: 403,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Forbidden',
          message: `User with role '${role}' is not authorized to access this resource`,
          statusCode: 403
        })
      });
    });
  }

  return userResponse;
}

/**
 * Setup mock games API routes (similar to pdf-preview.spec.ts pattern)
 */
export async function setupGamesRoutes(page: Page) {
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
