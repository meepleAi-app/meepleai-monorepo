/**
 * E2E Test Assertion Helpers
 *
 * Issue #1612: DRY utilities to reduce test duplication
 *
 * Provides reusable assertion functions for common test patterns:
 * - Forbidden/redirect verification
 * - Page content loading verification
 */

import { Page, expect } from '@playwright/test';

/**
 * Asserts that a page either shows a forbidden error or redirects away from the forbidden path.
 *
 * This helper reduces duplication of the common pattern:
 * ```typescript
 * const currentUrl = page.url();
 * const isForbidden = await page.locator('text=/forbidden|403|not authorized/i').isVisible().catch(() => false);
 * const isRedirected = !currentUrl.includes(forbiddenPath) || isForbidden;
 * expect(isForbidden || isRedirected).toBe(true);
 * ```
 *
 * @param page - Playwright Page object
 * @param forbiddenPath - The path that should be forbidden (e.g., '/admin', '/editor')
 *
 * @example
 * ```typescript
 * test('User forbidden from /admin', async ({ page }) => {
 *   await setupMockAuthWithForbidden(page, 'User', ['/admin/**']);
 *   await page.goto('/admin');
 *   await expectForbiddenOrRedirect(page, '/admin');
 * });
 * ```
 */
export async function expectForbiddenOrRedirect(
  page: Page,
  forbiddenPath: string
): Promise<void> {
  await page.waitForLoadState('networkidle');

  const currentUrl = page.url();
  const isForbidden = await page
    .locator('text=/forbidden|403|not authorized/i')
    .isVisible()
    .catch(() => false);
  const isRedirected = !currentUrl.includes(forbiddenPath) || isForbidden;

  expect(isForbidden || isRedirected).toBe(true);
}

/**
 * Asserts that a page has loaded with visible content (heading or main content).
 *
 * This helper reduces duplication of the common pattern:
 * ```typescript
 * const hasHeading = await page.locator('h1, h2, h3').first().isVisible().catch(() => false);
 * const hasMainContent = await page.locator('main, [role="main"]').isVisible().catch(() => false);
 * expect(hasHeading || hasMainContent).toBe(true);
 * ```
 *
 * @param page - Playwright Page object
 *
 * @example
 * ```typescript
 * test('Admin can access /admin', async ({ page }) => {
 *   await setupMockAuth(page, 'Admin');
 *   await page.goto('/admin');
 *   await expect(page).toHaveURL('/admin');
 *   await expectPageLoaded(page);
 * });
 * ```
 */
export async function expectPageLoaded(page: Page): Promise<void> {
  const hasHeading = await page
    .locator('h1, h2, h3')
    .first()
    .isVisible()
    .catch(() => false);
  const hasMainContent = await page
    .locator('main, [role="main"]')
    .isVisible()
    .catch(() => false);

  expect(hasHeading || hasMainContent).toBe(true);
}
