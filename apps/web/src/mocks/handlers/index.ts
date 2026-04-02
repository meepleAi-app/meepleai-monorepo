/**
 * MSW handlers barrel export — browser-safe
 *
 * Aggregates all domain handlers for use in:
 * - src/mocks/browser.ts (MSW ServiceWorker — pnpm dev:mock)
 * - src/mocks/node.ts (MSW Node server)
 * - .storybook/preview.tsx (MSW Storybook addon)
 *
 * The existing __tests__/mocks/handlers/index.ts is NOT affected.
 *
 * TODO: add remaining domain handlers as they are implemented:
 *   library, shared-games, catalog, admin, sessions,
 *   game-nights, players, notifications, badges
 */

import { authHandlers } from './auth.handlers';
import { chatHandlers } from './chat.handlers';
import { documentsHandlers } from './documents.handlers';
import { gamesHandlers } from './games.handlers';

export const handlers = [...authHandlers, ...gamesHandlers, ...chatHandlers, ...documentsHandlers];

export { authHandlers } from './auth.handlers';
export { gamesHandlers } from './games.handlers';
export { chatHandlers } from './chat.handlers';
export { documentsHandlers } from './documents.handlers';
