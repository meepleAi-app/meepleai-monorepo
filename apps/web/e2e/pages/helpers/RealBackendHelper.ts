/**
 * Real Backend Helper - API-Aware Page Interactions
 * Issue #3082: Implement Missing E2E Test Flows
 *
 * Provides methods for tests using real backend instead of mocks:
 * - Wait for real API responses
 * - Handle backend-driven state changes
 * - Verify data persisted correctly
 *
 * Usage:
 *   const helper = new RealBackendHelper(page);
 *   await helper.waitForApiResponse('/api/v1/games', 200);
 *   await helper.loginAndVerify('test@example.com', 'password');
 */

import { Page, expect } from '@playwright/test';

import { ApiClient, createApiClient } from '../../fixtures/api-client';

// API base URL
const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Helper class for real backend interactions
 */
export class RealBackendHelper {
  private apiClient: ApiClient;

  constructor(private page: Page) {
    this.apiClient = createApiClient(page);
  }

  // ============================================================================
  // API Response Waiting
  // ============================================================================

  /**
   * Wait for an API response matching the pattern
   * @param urlPattern - URL pattern to match
   * @param expectedStatus - Expected HTTP status (default: 200)
   * @param timeout - Timeout in ms (default: 30000)
   */
  async waitForApiResponse(
    urlPattern: string | RegExp,
    expectedStatus: number = 200,
    timeout: number = 30000
  ): Promise<{ ok: boolean; status: number; body: unknown }> {
    const response = await this.page.waitForResponse(
      (response) => {
        const url = response.url();
        if (typeof urlPattern === 'string') {
          return url.includes(urlPattern);
        }
        return urlPattern.test(url);
      },
      { timeout }
    );

    const status = response.status();
    let body: unknown;
    try {
      body = await response.json();
    } catch {
      body = await response.text();
    }

    return { ok: response.ok(), status, body };
  }

  /**
   * Wait for multiple API responses (parallel requests)
   * @param patterns - Array of URL patterns to wait for
   */
  async waitForMultipleApiResponses(
    patterns: (string | RegExp)[],
    timeout: number = 30000
  ): Promise<{ pattern: string | RegExp; status: number }[]> {
    const results: { pattern: string | RegExp; status: number }[] = [];

    await Promise.all(
      patterns.map(async (pattern) => {
        const response = await this.waitForApiResponse(pattern, 200, timeout);
        results.push({ pattern, status: response.status });
      })
    );

    return results;
  }

  // ============================================================================
  // Authentication
  // ============================================================================

  /**
   * Login via UI and verify with API
   * @param email - User email
   * @param password - User password
   */
  async loginAndVerify(email: string, password: string): Promise<boolean> {
    // Fill login form
    await this.page.goto('/login');
    await this.page.waitForLoadState('networkidle');

    await this.page.getByLabel(/email/i).fill(email);
    await this.page.getByLabel(/password/i).fill(password);

    // Setup response listener before clicking
    const responsePromise = this.waitForApiResponse('/api/v1/auth/login');

    // Click login button
    await this.page.getByRole('button', { name: /login|accedi|entra/i }).click();

    // Wait for API response
    const response = await responsePromise;

    if (response.ok) {
      // Wait for redirect to dashboard or home
      await this.page.waitForURL(/\/(dashboard|home|chat)?/, { timeout: 10000 }).catch(() => {});
      return true;
    }

    return false;
  }

  /**
   * Login as admin and verify
   */
  async loginAsAdminAndVerify(): Promise<boolean> {
    const adminEmail = process.env.ADMIN_EMAIL || 'admin@meepleai.dev';
    const adminPassword = process.env.ADMIN_PASSWORD || 'Demo123!';
    return this.loginAndVerify(adminEmail, adminPassword);
  }

  /**
   * Verify user is authenticated by checking /auth/me endpoint
   */
  async verifyAuthenticated(): Promise<boolean> {
    const result = await this.apiClient.getCurrentSession();
    return result.success;
  }

  /**
   * Logout and verify session is cleared
   */
  async logoutAndVerify(): Promise<boolean> {
    // Click logout button
    const logoutButton = this.page.getByRole('button', { name: /logout|esci/i });
    if (await logoutButton.isVisible()) {
      await logoutButton.click();
      await this.page.waitForLoadState('networkidle');
    }

    // Verify session is cleared
    return !(await this.verifyAuthenticated());
  }

  // ============================================================================
  // Data Operations
  // ============================================================================

  /**
   * Wait for data to be loaded from API
   * @param selector - CSS selector for container that should have content
   * @param apiEndpoint - API endpoint that provides the data
   */
  async waitForDataLoaded(selector: string, apiEndpoint: string): Promise<void> {
    // Wait for API response
    await this.waitForApiResponse(apiEndpoint);

    // Wait for UI to reflect the data
    await this.page.waitForSelector(selector, { state: 'visible', timeout: 10000 });
  }

  /**
   * Wait for list to have items from API
   * @param listSelector - CSS selector for list container
   * @param itemSelector - CSS selector for list items
   * @param minCount - Minimum expected items
   */
  async waitForListPopulated(
    listSelector: string,
    itemSelector: string,
    minCount: number = 1
  ): Promise<number> {
    await this.page.waitForSelector(listSelector, { state: 'visible', timeout: 10000 });

    await this.page.waitForFunction(
      ({ list, item, count }) => {
        const container = document.querySelector(list);
        if (!container) return false;
        const items = container.querySelectorAll(item);
        return items.length >= count;
      },
      { list: listSelector, item: itemSelector, count: minCount },
      { timeout: 15000 }
    );

    return await this.page.locator(`${listSelector} ${itemSelector}`).count();
  }

  // ============================================================================
  // Error Handling
  // ============================================================================

  /**
   * Wait for error response and verify error message is displayed
   * @param apiEndpoint - API endpoint that should return error
   * @param expectedStatus - Expected error status
   */
  async waitForApiError(
    apiEndpoint: string,
    expectedStatus: number
  ): Promise<{ status: number; errorMessage: string }> {
    const response = await this.waitForApiResponse(apiEndpoint);

    expect(response.status).toBe(expectedStatus);

    // Extract error message from response body
    let errorMessage = '';
    if (typeof response.body === 'object' && response.body !== null) {
      const body = response.body as Record<string, unknown>;
      errorMessage = (body.message || body.error || JSON.stringify(body)) as string;
    } else {
      errorMessage = String(response.body);
    }

    return { status: response.status, errorMessage };
  }

  /**
   * Verify error toast/message is displayed in UI
   * @param message - Expected error message pattern
   */
  async verifyErrorDisplayed(message: string | RegExp): Promise<void> {
    const errorLocator = this.page
      .locator('[role="alert"], .toast-error, .error-message, .text-red-500, .text-destructive')
      .filter({ hasText: message });

    await expect(errorLocator.first()).toBeVisible({ timeout: 5000 });
  }

  /**
   * Verify success toast/message is displayed
   * @param message - Expected success message pattern
   */
  async verifySuccessDisplayed(message: string | RegExp): Promise<void> {
    const successLocator = this.page
      .locator('[role="alert"], .toast-success, .success-message, .text-green-500')
      .filter({ hasText: message });

    await expect(successLocator.first()).toBeVisible({ timeout: 5000 });
  }

  // ============================================================================
  // Form Submission
  // ============================================================================

  /**
   * Submit form and wait for API response
   * @param submitButton - Locator for submit button
   * @param apiEndpoint - Expected API endpoint
   * @returns API response details
   */
  async submitFormAndWait(
    submitButton: string | ReturnType<Page['locator']>,
    apiEndpoint: string
  ): Promise<{ ok: boolean; status: number; body: unknown }> {
    const button = typeof submitButton === 'string' ? this.page.locator(submitButton) : submitButton;

    // Setup response listener
    const responsePromise = this.waitForApiResponse(apiEndpoint);

    // Click submit
    await button.click();

    // Wait for response
    return await responsePromise;
  }

  // ============================================================================
  // Navigation with API Verification
  // ============================================================================

  /**
   * Navigate to page and wait for initial API calls
   * @param url - Page URL
   * @param expectedEndpoints - API endpoints expected to be called
   */
  async navigateAndWaitForApis(url: string, expectedEndpoints: string[]): Promise<void> {
    // Start listening for all expected endpoints
    const responsePromises = expectedEndpoints.map((endpoint) =>
      this.waitForApiResponse(endpoint, 200, 30000)
    );

    // Navigate
    await this.page.goto(url);

    // Wait for all API responses
    await Promise.all(responsePromises);

    // Wait for page to be idle
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Wait for page to be fully loaded with data
   * @param contentSelector - Selector for main content that indicates page is ready
   */
  async waitForPageReady(contentSelector: string): Promise<void> {
    await this.page.waitForLoadState('networkidle');
    await this.page.waitForSelector(contentSelector, { state: 'visible', timeout: 15000 });
  }

  // ============================================================================
  // Streaming/SSE
  // ============================================================================

  /**
   * Wait for SSE stream to start
   * @param urlPattern - SSE endpoint pattern
   */
  async waitForStreamStart(urlPattern: string): Promise<void> {
    await this.page.waitForResponse(
      (response) => response.url().includes(urlPattern) && response.status() === 200,
      { timeout: 10000 }
    );
  }

  /**
   * Wait for streaming indicator to appear and disappear (stream complete)
   * @param indicatorSelector - Selector for streaming indicator
   */
  async waitForStreamComplete(indicatorSelector: string): Promise<void> {
    // Wait for streaming to start
    await this.page.waitForSelector(indicatorSelector, { state: 'visible', timeout: 5000 }).catch(() => {
      // Stream may have already completed
    });

    // Wait for streaming to complete
    await this.page.waitForSelector(indicatorSelector, { state: 'hidden', timeout: 60000 });
  }

  // ============================================================================
  // Utility Methods
  // ============================================================================

  /**
   * Get API client for direct backend calls
   */
  getApiClient(): ApiClient {
    return this.apiClient;
  }

  /**
   * Check if backend is healthy
   */
  async isBackendHealthy(): Promise<boolean> {
    const health = await this.apiClient.checkHealth();
    return health.success;
  }

  /**
   * Wait for backend to be ready
   * @param timeoutMs - Timeout in milliseconds
   */
  async waitForBackend(timeoutMs: number = 30000): Promise<boolean> {
    return this.apiClient.waitForBackend(timeoutMs);
  }
}

/**
 * Create a RealBackendHelper instance
 */
export function createRealBackendHelper(page: Page): RealBackendHelper {
  return new RealBackendHelper(page);
}
