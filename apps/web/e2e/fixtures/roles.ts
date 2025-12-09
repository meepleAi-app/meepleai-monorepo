/**
 * Role-based page fixtures for E2E tests
 * Issue #2007: E2E Server Stability - Phase 1, Batch 3
 *
 * Provides authenticated page contexts for different user roles:
 * - editorPage: Page authenticated as editor role (can create/edit content)
 * - adminPage: Page authenticated as admin role (full permissions)
 */

import { test as base, Page } from '@playwright/test';

type RoleFixtures = {
  editorPage: Page;
  adminPage: Page;
};

export const test = base.extend<RoleFixtures>({
  editorPage: async ({ page }, use) => {
    // Navigate to login page
    await page.goto('/');

    // Login as editor (credentials from .env.test)
    const editorEmail = process.env.EDITOR_EMAIL || 'editor@meepleai.dev';
    const editorPassword = process.env.EDITOR_PASSWORD || 'Demo123!';

    await page.fill('input[type="email"]', editorEmail);
    await page.fill('input[type="password"]', editorPassword);
    await page.click('button[type="submit"]');

    // Wait for successful login (redirect to dashboard)
    await page.waitForURL('/', { timeout: 5000 });

    // Use the authenticated page
    await use(page);

    // Cleanup: Logout after test
    try {
      await page.goto('/');
      await page.click('button:has-text("Logout")', { timeout: 2000 });
    } catch {
      // Ignore logout errors (session may have expired)
    }
  },

  adminPage: async ({ page }, use) => {
    // Navigate to login page
    await page.goto('/');

    // Login as admin (credentials from .env.test)
    const adminEmail = process.env.ADMIN_EMAIL || 'admin@meepleai.dev';
    const adminPassword = process.env.ADMIN_PASSWORD || 'Demo123!';

    await page.fill('input[type="email"]', adminEmail);
    await page.fill('input[type="password"]', adminPassword);
    await page.click('button[type="submit"]');

    // Wait for successful login (redirect to dashboard)
    await page.waitForURL('/', { timeout: 5000 });

    // Use the authenticated page
    await use(page);

    // Cleanup: Logout after test
    try {
      await page.goto('/');
      await page.click('button:has-text("Logout")', { timeout: 2000 });
    } catch {
      // Ignore logout errors (session may have expired)
    }
  },
});

export { expect } from '@playwright/test';
