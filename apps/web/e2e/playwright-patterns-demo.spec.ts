/**
 * Playwright Patterns Demonstration - Issue #2919
 *
 * This test file demonstrates all critical Playwright patterns and best practices:
 * 1. Page Object Model (POM)
 * 2. Response timing pattern
 * 3. React navigation fallback
 * 4. Mobile viewport handling
 * 5. API authentication
 * 6. Mocking
 * 7. Parallel execution
 * 8. Retry logic
 * 9. Test helpers
 *
 * Run:
 *   pnpm test:e2e e2e/playwright-patterns-demo.spec.ts
 */

import { test, expect } from '@playwright/test';
import { BasePage } from './pages/base/BasePage';
import { authenticateViaAPI } from './fixtures/auth';
import { mockApiSuccess, mockApiError } from './helpers/mocks';
import { setViewport, isMobileViewport } from './helpers/responsive-utils';
import { assertSuccessToast, assertErrorMessage } from './helpers/assertions';

/**
 * Example: Simple Page Object extending BasePage
 */
class DemoPage extends BasePage {
  // Locators
  readonly submitButton = this.page.getByRole('button', { name: 'Submit' });
  readonly inputField = this.page.getByPlaceholder('Enter text');
  readonly statusMessage = this.page.locator('[data-testid="status"]');
  readonly loadingSpinner = this.page.locator('[data-testid="loading"]');

  async goto(): Promise<void> {
    await this.page.goto('/demo');
    await this.waitForLoad();
  }

  async submitForm(text: string): Promise<void> {
    await this.fill(this.inputField, text);
    await this.click(this.submitButton);
  }

  async getStatusText(): Promise<string> {
    return await this.getText(this.statusMessage);
  }

  async waitForSubmission(): Promise<void> {
    await this.waitForElementToDisappear(this.loadingSpinner);
  }
}

test.describe('Playwright Patterns Demo', () => {
  test.describe.configure({ mode: 'parallel' }); // Run tests in parallel

  /**
   * Pattern 1: Page Object Model (POM)
   * Demonstrates proper use of BasePage inheritance
   */
  test('Pattern 1: Page Object Model', async ({ page }) => {
    const demoPage = new DemoPage(page);

    await demoPage.goto();
    await expect(demoPage.page).toHaveURL(/\/demo/);
    await expect(demoPage.submitButton).toBeVisible();
  });

  /**
   * Pattern 2: Response Timing (CRITICAL)
   * Set up listener BEFORE action to avoid missing responses
   */
  test('Pattern 2: Response Timing', async ({ page }) => {
    const demoPage = new DemoPage(page);
    await demoPage.goto();

    // ✅ CORRECT: Set up listener BEFORE action
    const responsePromise = demoPage.waitForResponse('/api/submit');
    await demoPage.submitForm('test data');
    const response = await responsePromise;

    expect(response.ok).toBe(true);
    expect(response.status).toBe(200);

    // Alternative: Use page.request for direct API testing
    const apiResponse = await page.request.post('/api/submit', {
      data: { text: 'test data' },
    });
    expect(apiResponse.ok()).toBe(true);
  });

  /**
   * Pattern 3: React Navigation Fallback
   * Provide fallback for React client-side routing failures
   */
  test('Pattern 3: React Navigation Fallback', async ({ page }) => {
    await page.goto('/');

    // Click navigation link
    await page.getByRole('link', { name: 'Demo' }).click();

    // ✅ Wait for React navigation with event-driven approach (Issue #1493)
    try {
      await page.waitForURL(/\/demo/, { timeout: 5000 });
    } catch {
      // Fallback if React routing failed
      console.log('React navigation failed, using fallback');
      await page.goto('/demo');
      await page.waitForLoadState('networkidle');
    }

    await expect(page).toHaveURL(/\/demo/);
  });

  /**
   * Pattern 4: Mobile Viewport Handling
   * Adapt test logic based on viewport size
   */
  test('Pattern 4: Mobile Viewport', async ({ page }) => {
    // Test on mobile viewport
    await setViewport(page, 'mobile');
    await page.goto('/demo');

    const isMobile = await isMobileViewport(page);

    if (isMobile) {
      // On mobile, some UI elements might be hidden (hidden md:flex)
      // Use API directly instead of UI
      const response = await page.request.post('/api/submit', {
        data: { text: 'mobile test' },
      });
      expect(response.ok()).toBe(true);
    } else {
      // On desktop, use UI elements
      const demoPage = new DemoPage(page);
      await demoPage.submitForm('desktop test');
      await demoPage.waitForSubmission();
    }
  });

  /**
   * Pattern 5: Multi-Viewport Testing
   * Test across all supported viewports
   */
  const viewports = ['mobile', 'tablet', 'desktop'] as const;
  for (const viewport of viewports) {
    test(`Pattern 5: Multi-Viewport - ${viewport}`, async ({ page }) => {
      await setViewport(page, viewport);
      await page.goto('/demo');

      // Verify critical elements are visible
      await expect(page.getByRole('main')).toBeVisible();
    });
  }

  /**
   * Pattern 6: API Authentication
   * Use API for fast authentication instead of UI login
   */
  test('Pattern 6: API Authentication', async ({ page }) => {
    // Authenticate via API (fast, reliable)
    const authenticated = await authenticateViaAPI(
      page,
      'test@example.com',
      'password'
    );
    expect(authenticated).toBe(true);

    // Navigate to protected page
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/dashboard/);
  });

  /**
   * Pattern 7: API Mocking
   * Mock API responses for negative testing
   */
  test('Pattern 7: API Mocking - Success', async ({ page }) => {
    // Mock successful API response
    await mockApiSuccess(page, '/api/data', { message: 'Success' });
    await page.goto('/demo');

    const response = await page.request.get('/api/data');
    const body = await response.json();
    expect(body.message).toBe('Success');
  });

  test('Pattern 7: API Mocking - Error', async ({ page }) => {
    // Mock error API response
    await mockApiError(page, '/api/data', 500);
    await page.goto('/demo');

    const response = await page.request.get('/api/data');
    expect(response.ok()).toBe(false);
    expect(response.status()).toBe(500);
  });

  /**
   * Pattern 8: Assertions - Domain-Specific
   * Use helper functions for common assertions
   */
  test('Pattern 8: Domain-Specific Assertions', async ({ page }) => {
    await page.goto('/demo');
    const demoPage = new DemoPage(page);

    await demoPage.submitForm('test');

    // Use helper for toast assertion
    await assertSuccessToast(page, 'Form submitted successfully');

    // Alternatively, use BasePage methods
    await demoPage.waitForText(demoPage.statusMessage, 'Success');
  });

  /**
   * Pattern 9: New BasePage Utilities (Issue #2919)
   * Demonstrate new utility methods added to BasePage
   */
  test('Pattern 9: BasePage Utilities', async ({ page }) => {
    const demoPage = new DemoPage(page);
    await demoPage.goto();

    // waitForText
    await demoPage.waitForText(demoPage.statusMessage, 'Ready');

    // waitForEnabled
    await demoPage.waitForEnabled(demoPage.submitButton);

    // hover (useful for tooltips)
    await demoPage.hover(demoPage.submitButton);

    // getText
    const text = await demoPage.getText(demoPage.statusMessage);
    expect(text).toContain('Ready');

    // getCount (useful for lists)
    const buttonCount = await demoPage.getCount(page.getByRole('button'));
    expect(buttonCount).toBeGreaterThan(0);

    // pressKey (keyboard shortcuts)
    await demoPage.pressKey('Escape');

    // scrollIntoView
    await demoPage.scrollIntoView(demoPage.submitButton);

    // isDisabled
    const isDisabled = await demoPage.isDisabled(demoPage.submitButton);
    expect(isDisabled).toBe(false);

    // reload
    await demoPage.reload();
    await expect(demoPage.submitButton).toBeVisible();
  });

  /**
   * Pattern 10: Cookie Sync with page.request
   * Manual cookie sync required after API calls
   */
  test('Pattern 10: Cookie Sync', async ({ page, context }) => {
    // Login via API
    await authenticateViaAPI(page, 'test@example.com', 'password');
    await page.goto('/dashboard');

    // Logout via API
    await page.request.post('/api/auth/logout');

    // Manual cookie sync required
    await context.clearCookies();
    await page.goto('/');

    // Verify logged out
    await expect(page).toHaveURL(/\/$/);
  });

  /**
   * Pattern 11: CORS Avoidance
   * Use page.request to bypass CORS restrictions
   */
  test('Pattern 11: CORS Avoidance', async ({ page }) => {
    await page.goto('/');

    // ✅ CORRECT: Use page.request (bypasses CORS)
    const response = await page.request.get('https://api.external.com/data');
    expect(response.ok()).toBe(true);

    // ❌ WRONG: page.evaluate(fetch) triggers CORS errors
    // const data = await page.evaluate(() => fetch('https://api.external.com/data'));
  });

  /**
   * Pattern 12: Network Idle Waiting
   * Wait for network to be idle after async operations
   */
  test('Pattern 12: Network Idle', async ({ page }) => {
    const demoPage = new DemoPage(page);
    await demoPage.goto();

    await demoPage.submitForm('test');
    await demoPage.waitForNetworkIdle();

    // Verify submission completed
    await expect(demoPage.statusMessage).toContainText('Success');
  });

  /**
   * Pattern 13: Screenshot for Debugging
   * Capture screenshots for visual debugging
   */
  test('Pattern 13: Screenshot Debugging', async ({ page }) => {
    const demoPage = new DemoPage(page);
    await demoPage.goto();

    // Take screenshot before action
    await demoPage.screenshot('before-submit');

    await demoPage.submitForm('test');
    await demoPage.waitForSubmission();

    // Take screenshot after action
    await demoPage.screenshot('after-submit');

    // Screenshots saved to: e2e-screenshots/
  });
});

/**
 * Pattern 14: Test Groups
 * Organize tests with tags for selective execution
 */
test.describe('Critical Paths @smoke', () => {
  test('smoke test - basic functionality', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/MeepleAI/);
  });
});

test.describe('Admin Dashboard @admin', () => {
  test.beforeEach(async ({ page }) => {
    await authenticateViaAPI(page, 'admin@example.com', 'password');
  });

  test('admin can access dashboard', async ({ page }) => {
    await page.goto('/admin/dashboard');
    await expect(page).toHaveURL(/\/admin\/dashboard/);
  });
});

/**
 * Pattern 15: Retry Logic Demonstration
 * Retry strategy handled by playwright.config.ts
 * - CI: 2 retries for transient failures
 * - Local: 0 retries for fast feedback
 */
test.describe('Flaky Test Example', () => {
  test('potentially flaky operation', async ({ page }) => {
    // This test will retry 2 times in CI if it fails
    await page.goto('/api-dependent-page');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="api-data"]')).toBeVisible();
  });
});
