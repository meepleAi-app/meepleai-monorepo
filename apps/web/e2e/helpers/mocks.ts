/**
 * E2E Test Mock Helpers
 *
 * Issue #1612: DRY utilities to reduce API test duplication
 *
 * Provides reusable mock functions for common API testing patterns:
 * - API forbidden responses for unauthorized roles
 */

import { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE ||
  process.env.NEXT_PUBLIC_API_BASE ||
  'http://localhost:8080';

/**
 * Mocks an API endpoint to return 403 Forbidden for a specific role.
 *
 * This helper reduces duplication of the common pattern:
 * ```typescript
 * await page.context().route(`${API_BASE}/api/v1/endpoint**`, async (route) => {
 *   await route.fulfill({
 *     status: 403,
 *     contentType: 'application/json',
 *     body: JSON.stringify({ error: 'Forbidden', message: '...', statusCode: 403 })
 *   });
 * });
 * ```
 *
 * Note: Uses `page.context().route()` instead of `page.route()` because:
 * - `page.route()` only intercepts requests from the page itself
 * - `page.request` (APIRequestContext) bypasses page-level routes
 * - `page.context().route()` intercepts ALL requests at browser context level
 *
 * @param page - Playwright Page object
 * @param endpoint - API endpoint path (e.g., '/api/v1/admin/users')
 * @param method - HTTP method to intercept ('GET', 'POST', 'PUT', 'DELETE')
 * @param role - Role name for error message (e.g., 'User', 'Editor')
 *
 * @example
 * ```typescript
 * test('User cannot POST /api/v1/games', async ({ page }) => {
 *   await setupMockAuth(page, 'User');
 *   await mockApiForbidden(page, '/api/v1/games', 'POST', 'User');
 *
 *   const response = await page.request.post(`${API_BASE}/api/v1/games`, {
 *     data: { name: 'Test' }
 *   });
 *
 *   expect(response.status()).toBe(403);
 * });
 * ```
 */
export async function mockApiForbidden(
  page: Page,
  endpoint: string,
  method: 'GET' | 'POST' | 'PUT' | 'DELETE',
  role: string
): Promise<void> {
  await page.context().route(`${API_BASE}${endpoint}**`, async (route) => {
    if (route.request().method() === method) {
      await route.fulfill({
        status: 403,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Forbidden',
          message: `${role} role not authorized`,
          statusCode: 403,
        }),
      });
    } else {
      await route.continue();
    }
  });
}
