import { test as base, Page, Route } from '@playwright/test';

const API_BASE = 'http://localhost:8080';

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
});

export { expect } from '@playwright/test';
