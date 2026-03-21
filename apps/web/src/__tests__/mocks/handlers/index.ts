/**
 * MSW handlers index
 *
 * Exports all request handlers for MSW server
 *
 * Issue #2760: Extended with library, shared-games, and catalog handlers
 * Issue #2914: Extended with admin dashboard handlers
 * Flow tests: Extended with sessions, game-nights, players, notifications, badges handlers
 */

import { authHandlers } from './auth.handlers';
import { gamesHandlers } from './games.handlers';
import { chatHandlers } from './chat.handlers';
import { documentsHandlers } from './documents.handlers';
import { libraryHandlers } from './library.handlers';
import { sharedGamesHandlers } from './shared-games.handlers';
import { catalogHandlers } from './catalog.handlers';
import { adminHandlers } from './admin.handlers';
import { sessionsHandlers } from './sessions.handlers';
import { gameNightsHandlers } from './game-nights.handlers';
import { playersHandlers } from './players.handlers';
import { notificationsHandlers } from './notifications.handlers';
import { badgesHandlers } from './badges.handlers';

/**
 * All MSW request handlers
 *
 * These handlers intercept network requests in tests and provide
 * mock responses. Handlers are applied in order, so more specific
 * handlers should come before general ones.
 */
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

// Re-export individual handler groups for selective use
export { authHandlers } from './auth.handlers';
export { gamesHandlers, resetGamesState } from './games.handlers';
export { chatHandlers, resetChatState, createSSEStream } from './chat.handlers';
export { documentsHandlers, resetDocumentsState } from './documents.handlers';
export { libraryHandlers, resetLibraryState, addLibraryItem } from './library.handlers';
export { sharedGamesHandlers, resetSharedGamesState, addSharedGame } from './shared-games.handlers';
export {
  catalogHandlers,
  mockCategories,
  mockMechanics,
  mockComplexityRanges,
  mockPlayerCounts,
  mockPlayingTimeRanges,
} from './catalog.handlers';
export {
  adminHandlers,
  setAdminApiFailures,
  setAdminNetworkLatency,
  updateDashboardStats,
  updateRecentActivity,
  updateInfrastructureDetails,
  resetAdminState,
} from './admin.handlers';
export { sessionsHandlers, resetSessionsState, getSessionsState } from './sessions.handlers';
export {
  gameNightsHandlers,
  resetGameNightsState,
  getGameNightsState,
} from './game-nights.handlers';
export { playersHandlers, resetPlayersState, getPlayersState } from './players.handlers';
export {
  notificationsHandlers,
  resetNotificationsState,
  addNotification,
} from './notifications.handlers';
export { badgesHandlers, resetBadgesState } from './badges.handlers';
