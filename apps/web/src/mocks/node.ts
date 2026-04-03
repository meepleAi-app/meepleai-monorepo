/**
 * MSW Node.js server setup
 *
 * Alternative to __tests__/mocks/server.ts using shared handlers from src/mocks/.
 * The existing __tests__/mocks/server.ts continues to work unchanged.
 *
 * Usage in new test files:
 * ```typescript
 * import { server } from '@/mocks/node';
 * beforeAll(() => server.listen());
 * afterEach(() => server.resetHandlers());
 * afterAll(() => server.close());
 * ```
 */
import { setupServer } from 'msw/node';

import { handlers } from './handlers';

export const server = setupServer(...handlers);
export const resetHandlers = () => server.resetHandlers();
export const resetRuntimeHandlers = () => server.resetHandlers(...handlers);
