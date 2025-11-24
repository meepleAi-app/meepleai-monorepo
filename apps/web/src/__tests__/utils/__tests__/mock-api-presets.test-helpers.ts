/**
 * Test Helpers for Mock API Presets
 *
 * Shared utilities, mocks, and setup for mock-api-presets tests
 */

import { MockApiRouter } from '../mock-api-router';

/**
 * Create a fresh router instance for testing
 */
export function createTestRouter(): MockApiRouter {
  return new MockApiRouter();
}

/**
 * Helper to verify route was registered
 */
export function expectRouteRegistered(router: MockApiRouter, pattern: string): boolean {
  const routes = router.getRoutes();
  return routes.some((r) => r.pattern === pattern);
}

/**
 * Helper to verify multiple routes registered
 */
export function expectRoutesRegistered(router: MockApiRouter, patterns: string[]): boolean {
  const routes = router.getRoutes();
  return patterns.every((pattern) => routes.some((r) => r.pattern === pattern));
}

/**
 * Helper to test fluent API chaining
 */
export function expectFluentApi(
  presetFunction: (router: MockApiRouter, options?: any) => MockApiRouter,
  router: MockApiRouter
): void {
  const result = presetFunction(router);
  expect(result).toBe(router);
}

/**
 * Helper to test error response structure
 */
export async function expectErrorResponse(
  response: Response,
  expectedStatus: number,
  expectedMessage?: string
): Promise<void> {
  expect(response.status).toBe(expectedStatus);
  expect(response.ok).toBe(false);

  if (expectedMessage) {
    const data = await response.json();
    expect(data).toHaveProperty('error');
    expect(data.error).toBe(expectedMessage);
  }
}

/**
 * Helper to test success response structure
 */
export async function expectSuccessResponse(
  response: Response,
  expectedStatus: number = 200
): Promise<any> {
  expect(response.status).toBe(expectedStatus);
  expect(response.ok).toBe(true);
  return await response.json();
}
