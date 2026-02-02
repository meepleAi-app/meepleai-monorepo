/**
 * E2E Test Fixtures - Central Export
 * Issue #3082: Implement Missing E2E Test Flows
 *
 * Provides all fixtures and utilities for E2E testing:
 * - API Client for real backend integration
 * - Test Data factories and cleanup
 * - Authentication fixtures (mock and real)
 * - Page Object helpers
 *
 * Usage:
 *   import { test, expect, ApiClient, TestDataManager } from '../fixtures';
 */

// Re-export from auth.ts (mock-based fixtures)
export {
  test as mockAuthTest,
  setupMockAuth,
  setupMockAuthWithForbidden,
  setupGamesRoutes,
  loginAsAdmin as mockLoginAsAdmin,
  loginAsEditor as mockLoginAsEditor,
  loginAsUser as mockLoginAsUser,
  authenticateViaAPI,
} from './auth';

// Re-export API client
export {
  ApiClient,
  createApiClient,
  API_BASE,
  TEST_CREDENTIALS,
  type ApiResponse,
  type AuthResponse,
  type SessionInfo,
} from './api-client';

// Re-export test data utilities
export {
  TestDataManager,
  UserFactory,
  GameFactory,
  ChatFactory,
  generateTestId,
  generateTestEmail,
  generateTestName,
  isTestData,
  withTestData,
  TEST_PREFIX,
} from './test-data';

// Re-export role fixtures
export { test as roleTest } from './roles';

// Re-export i18n utilities
export {
  t,
  getCurrentLanguage,
  getTextMatcher,
  getFlexibleMatcher,
  matchesTranslation,
  getAllVariants,
  addTranslation,
} from './i18n';

// Re-export email fixtures (if needed for email verification tests)
export * from './email';

// Re-export two-factor fixtures
export * from './twoFactor';

// Re-export robust selectors
export * from './robust-selectors';

// ============================================================================
// Combined Real Backend Test Fixture
// ============================================================================

import { test as base } from '@playwright/test';

import { ApiClient } from './api-client';
import { TestDataManager } from './test-data';

/**
 * Extended test with real backend integration
 *
 * Provides:
 * - apiClient: Direct backend API access
 * - testData: Test data manager with automatic cleanup
 * - authenticatedPage: Page with real backend authentication
 *
 * Usage:
 *   import { test } from '../fixtures';
 *
 *   test('my test', async ({ page, apiClient, testData }) => {
 *     await apiClient.authenticateAsAdmin();
 *     const game = await testData.createTestGame();
 *     // ... test code ...
 *   }); // testData automatically cleaned up
 */
export const test = base.extend<{
  apiClient: ApiClient;
  testData: TestDataManager;
  adminApiClient: ApiClient;
  editorApiClient: ApiClient;
  userApiClient: ApiClient;
}>({
  /**
   * API Client for the current page context
   */
  apiClient: async ({ page }, use) => {
    const client = new ApiClient(page);
    await use(client);
  },

  /**
   * Test data manager with automatic cleanup
   */
  testData: async ({ apiClient }, use) => {
    const manager = new TestDataManager(apiClient);
    await use(manager);

    // Automatic cleanup after each test
    const result = await manager.cleanup();
    if (!result.success) {
      console.warn('Test data cleanup errors:', result.errors);
    }
  },

  /**
   * API Client pre-authenticated as admin
   */
  adminApiClient: async ({ page }, use) => {
    const client = new ApiClient(page);
    const auth = await client.authenticateAsAdmin();
    if (!auth.success) {
      throw new Error(`Admin authentication failed: ${auth.error}`);
    }
    await use(client);
    await client.logout();
  },

  /**
   * API Client pre-authenticated as editor
   */
  editorApiClient: async ({ page }, use) => {
    const client = new ApiClient(page);
    const auth = await client.authenticateAsEditor();
    if (!auth.success) {
      throw new Error(`Editor authentication failed: ${auth.error}`);
    }
    await use(client);
    await client.logout();
  },

  /**
   * API Client pre-authenticated as regular user
   */
  userApiClient: async ({ page }, use) => {
    const client = new ApiClient(page);
    const auth = await client.authenticateAsUser();
    if (!auth.success) {
      throw new Error(`User authentication failed: ${auth.error}`);
    }
    await use(client);
    await client.logout();
  },
});

// Re-export expect and types from base
export { expect, type Page, type APIRequestContext, type Route } from '@playwright/test';
