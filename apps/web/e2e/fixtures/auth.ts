import { test as base, Page } from '@playwright/test';

/**
 * Login as admin user (admin@meepleai.dev)
 * Handles page load timing and navigation properly
 */
export async function loginAsAdmin(page: Page) {
  await page.goto('/');
  await page.waitForLoadState('networkidle'); // Wait for page ready

  // Click login with specificity (handle multiple matches)
  const loginButton = page.getByRole('button', { name: 'Login', exact: true });
  await loginButton.first().click();

  // Fill credentials
  await page.fill('input[type="email"]', 'admin@meepleai.dev');
  await page.fill('input[type="password"]', 'Demo123!');

  // Submit and wait for redirect
  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL('/');
}

/**
 * Login as editor user (editor@meepleai.dev)
 */
export async function loginAsEditor(page: Page) {
  await page.goto('/');
  await page.waitForLoadState('networkidle');

  const loginButton = page.getByRole('button', { name: 'Login', exact: true });
  await loginButton.first().click();

  await page.fill('input[type="email"]', 'editor@meepleai.dev');
  await page.fill('input[type="password"]', 'Demo123!');

  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL('/');
}

/**
 * Login as regular user (user@meepleai.dev)
 */
export async function loginAsUser(page: Page) {
  await page.goto('/');
  await page.waitForLoadState('networkidle');

  const loginButton = page.getByRole('button', { name: 'Login', exact: true });
  await loginButton.first().click();

  await page.fill('input[type="email"]', 'user@meepleai.dev');
  await page.fill('input[type="password"]', 'Demo123!');

  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL('/');
}

/**
 * Extended test with pre-authenticated pages
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
