/**
 * MSW server setup for Node.js test environment
 *
 * This server intercepts HTTP requests during Jest tests
 * and provides mock responses via the defined handlers.
 *
 * Usage in tests:
 * ```typescript
 * import { server } from '@/__tests__/mocks/server';
 *
 * // Override handler for specific test
 * server.use(
 *   http.get('/api/v1/custom', () => {
 *     return HttpResponse.json({ custom: 'data' });
 *   })
 * );
 * ```
 */

import { setupServer } from 'msw/node';
import { handlers } from './handlers';

/**
 * MSW server instance for Node.js (Jest) tests
 *
 * Automatically started in jest.setup.js before tests run
 * and stopped after all tests complete.
 */
export const server = setupServer(...handlers);

/**
 * Reset all handlers to initial state
 *
 * Call this in afterEach() to ensure test isolation:
 * ```typescript
 * afterEach(() => {
 *   server.resetHandlers();
 * });
 * ```
 */
export const resetHandlers = () => {
  server.resetHandlers();
};

/**
 * Reset all runtime request handlers
 *
 * This clears any handlers added via server.use() during tests
 * but keeps the initial handlers from the setup.
 */
export const resetRuntimeHandlers = () => {
  server.resetHandlers(...handlers);
};
