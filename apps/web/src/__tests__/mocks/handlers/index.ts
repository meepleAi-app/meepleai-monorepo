/**
 * MSW handlers index
 *
 * Exports all request handlers for MSW server
 *
 * Issue #2760: Extended with library, shared-games, and catalog handlers
 * Issue #2914: Extended with admin dashboard handlers
 */

import { authHandlers } from './auth.handlers';
import { gamesHandlers } from './games.handlers';
import { chatHandlers } from './chat.handlers';
import { documentsHandlers } from './documents.handlers';
import { libraryHandlers } from './library.handlers';
import { sharedGamesHandlers } from './shared-games.handlers';
import { catalogHandlers } from './catalog.handlers';
import { adminHandlers } from './admin.handlers';
import { adminShareRequestsHandlers } from './admin-share-requests.handlers';

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
  ...adminShareRequestsHandlers,
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
export {
  adminShareRequestsHandlers,
  resetAdminShareRequestsState,
  addMockShareRequest,
  mockShareRequests,
} from './admin-share-requests.handlers';
