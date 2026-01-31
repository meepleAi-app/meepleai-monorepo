/**
 * Chromatic visual regression for error states
 * Issue: #2261 - Dev server memory instability with Playwright error state mocks
 *
 * Visual regression tests for error state UI components:
 * - 500 Internal Server Error
 * - 403 Forbidden
 * - Network timeout
 * - Loading states
 *
 * Uses production server (FORCE_PRODUCTION_SERVER=true) to avoid dev server crashes.
 * Run with: pnpm test:a11y:e2e:errors
 */

import { test, expect } from './fixtures/chromatic';

import type { Page } from '@playwright/test';

// Desktop viewport for consistent error state rendering
test.use({
  viewport: { width: 1920, height: 1080 },
});

/**
 * Setup mock authentication for tests requiring auth context
 */
const setupMockAuth = async (page: Page, role: 'User' | 'Admin', email: string) => {
  await page.route('**/api/v1/auth/me', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'chromatic-test-id',
          email,
          displayName: 'Chromatic Test User',
          role,
        },
      }),
    });
  });
};

test.describe('Error States - Chromatic Visual Regression', () => {
  test('500 Internal Server Error state', { tag: '@error-state' }, async ({ page }) => {
    // Mock 500 error for games endpoint
    await page.route('**/api/v1/games', async route => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Internal Server Error' }),
      });
    });

    await page.goto('/games');

    // Wait for error state to render
    await page.waitForTimeout(2000);

    // Ensure error message is visible
    const errorContainer = page
      .locator('[data-testid="error-display"], .error-state, [role="alert"]')
      .first();
    await expect(errorContainer).toBeVisible({ timeout: 5000 });

    // Capture error state visual
    await expect(page).toHaveScreenshot('error-500-internal-server.png', {
      fullPage: false,
      animations: 'disabled',
    });
  });

  test('403 Forbidden error state', { tag: '@error-state' }, async ({ page }) => {
    // Setup user role (not admin)
    await setupMockAuth(page, 'User', 'user@chromatic-test.dev');

    // Mock 403 for admin endpoint
    await page.route('**/api/v1/admin/**', async route => {
      await route.fulfill({
        status: 403,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Forbidden', message: 'Insufficient permissions' }),
      });
    });

    await page.goto('/admin');

    // Wait for forbidden error to render
    await page.waitForTimeout(2000);

    // Ensure error message is visible
    const errorContainer = page
      .locator('[data-testid="error-display"], .error-state, [role="alert"]')
      .first();
    await expect(errorContainer).toBeVisible({ timeout: 5000 });

    // Capture forbidden error visual
    await expect(page).toHaveScreenshot('error-403-forbidden.png', {
      fullPage: false,
      animations: 'disabled',
    });
  });

  test('Network timeout error state', { tag: '@error-state' }, async ({ page }) => {
    // Mock timeout by aborting request
    await page.route('**/api/v1/games', async route => {
      await route.abort('timedout');
    });

    await page.goto('/games');

    // Wait for timeout error to render
    await page.waitForTimeout(2000);

    // Ensure error message is visible
    const errorContainer = page
      .locator('[data-testid="error-display"], .error-state, [role="alert"]')
      .first();
    await expect(errorContainer).toBeVisible({ timeout: 5000 });

    // Capture timeout error visual
    await expect(page).toHaveScreenshot('error-network-timeout.png', {
      fullPage: false,
      animations: 'disabled',
    });
  });

  test('Loading state', { tag: '@error-state' }, async ({ page }) => {
    // Setup mock auth
    await setupMockAuth(page, 'User', 'user@chromatic-test.dev');

    // Mock delayed response to capture loading state
    await page.route('**/api/v1/games', async route => {
      // Delay response to keep loading state visible
      await new Promise(resolve => setTimeout(resolve, 1500));
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    // Start navigation
    const navigationPromise = page.goto('/games');

    // Wait briefly for loading state to appear (before data loads)
    await page.waitForTimeout(500);

    // Ensure loading indicator is visible
    const loadingIndicator = page
      .locator('[data-testid="loading-spinner"], .loading-state, [aria-busy="true"]')
      .first();
    await expect(loadingIndicator).toBeVisible({ timeout: 3000 });

    // Capture loading state visual
    await expect(page).toHaveScreenshot('loading-state.png', {
      fullPage: false,
      animations: 'disabled',
    });

    // Complete navigation
    await navigationPromise;
  });
});
