/**
 * MSW handlers index
 *
 * Exports all request handlers for MSW server
 */

import { authHandlers } from './auth.handlers';
import { gamesHandlers } from './games.handlers';
import { chatHandlers } from './chat.handlers';
import { documentsHandlers } from './documents.handlers';

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
];

// Re-export individual handler groups for selective use
export { authHandlers } from './auth.handlers';
export { gamesHandlers, resetGamesState } from './games.handlers';
export { chatHandlers, resetChatState, createSSEStream } from './chat.handlers';
export { documentsHandlers, resetDocumentsState } from './documents.handlers';
