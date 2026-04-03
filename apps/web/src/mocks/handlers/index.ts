/**
 * MSW handlers barrel export — browser-safe
 *
 * Aggregates all 13 domain handlers for use in:
 * - src/mocks/browser.ts (MSW ServiceWorker — pnpm dev:mock)
 * - src/mocks/node.ts (MSW Node server — alternativo a __tests__/mocks/server.ts)
 * - .storybook/preview.tsx (MSW Storybook addon)
 *
 * The existing __tests__/mocks/handlers/index.ts is NOT affected.
 */

import { adminHandlers } from './admin.handlers';
import { authHandlers } from './auth.handlers';
import { badgesHandlers } from './badges.handlers';
import { catalogHandlers } from './catalog.handlers';
import { chatHandlers } from './chat.handlers';
import { documentsHandlers } from './documents.handlers';
import { gameNightsHandlers } from './game-nights.handlers';
import { gamesHandlers } from './games.handlers';
import { libraryHandlers } from './library.handlers';
import { notificationsHandlers } from './notifications.handlers';
import { playersHandlers } from './players.handlers';
import { sessionsHandlers } from './sessions.handlers';
import { sharedGamesHandlers } from './shared-games.handlers';

export const handlers = [
  ...authHandlers,
  ...gamesHandlers,
  ...chatHandlers,
  ...documentsHandlers,
  ...libraryHandlers,
  ...sharedGamesHandlers,
  ...catalogHandlers,
  ...adminHandlers,
  ...sessionsHandlers,
  ...gameNightsHandlers,
  ...playersHandlers,
  ...notificationsHandlers,
  ...badgesHandlers,
];

// Re-export individual handler groups for per-story overrides in Storybook
export { authHandlers } from './auth.handlers';
export { gamesHandlers } from './games.handlers';
export { chatHandlers } from './chat.handlers';
export { resetChatState } from './chat.handlers';
export { documentsHandlers } from './documents.handlers';
export { libraryHandlers } from './library.handlers';
export { sharedGamesHandlers } from './shared-games.handlers';
export { catalogHandlers } from './catalog.handlers';
export { adminHandlers } from './admin.handlers';
export { sessionsHandlers, resetSessionsState } from './sessions.handlers';
export { gameNightsHandlers, resetGameNightsState } from './game-nights.handlers';
export { playersHandlers, resetPlayersState } from './players.handlers';
export { notificationsHandlers, resetNotificationsState } from './notifications.handlers';
export { badgesHandlers, resetBadgesState } from './badges.handlers';
