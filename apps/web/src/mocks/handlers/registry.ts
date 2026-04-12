/**
 * MSW handler groups registry
 *
 * Wraps each domain handler array into a HandlerGroup for use with
 * MswHandlerRegistry. Groups can be toggled on/off via:
 *   NEXT_PUBLIC_MSW_ENABLE=auth,games        (allowlist — only these active)
 *   NEXT_PUBLIC_MSW_DISABLE=notifications    (denylist — all except these)
 *
 * Missing toggle entries are treated as enabled (safe default).
 */

import { adminHandlers } from './admin.handlers';
import { agentsHandlers } from './agents.handlers';
import { authHandlers } from './auth.handlers';
import { badgesHandlers } from './badges.handlers';
import { catalogHandlers } from './catalog.handlers';
import { chatHandlers } from './chat.handlers';
import { documentsHandlers } from './documents.handlers';
import { gameNightsHandlers } from './game-nights.handlers';
import { gamesHandlers } from './games.handlers';
import { libraryHandlers } from './library.handlers';
import { notificationsHandlers } from './notifications.handlers';
import { playRecordsHandlers } from './play-records.handlers';
import { playersHandlers } from './players.handlers';
import { sessionsHandlers } from './sessions.handlers';
import { sharedGamesHandlers } from './shared-games.handlers';

import type { HttpHandler } from 'msw';

/**
 * Local handler-group shape. Mirrors `@/dev-tools/mswHandlerRegistry.HandlerGroup`
 * but defined here so mocks/ has no static dependency on dev-tools/ — see
 * .github/workflows/dev-tools-isolation.yml.
 */
export interface HandlerGroupBase {
  name: string;
  handlers: HttpHandler[];
}

/**
 * All known MSW handler groups, ordered by domain priority.
 * Group names are stable identifiers used in env-var toggles.
 */
export const HANDLER_GROUPS: HandlerGroupBase[] = [
  { name: 'auth', handlers: authHandlers },
  { name: 'games', handlers: gamesHandlers },
  { name: 'agents', handlers: agentsHandlers },
  { name: 'chat', handlers: chatHandlers },
  { name: 'documents', handlers: documentsHandlers },
  { name: 'library', handlers: libraryHandlers },
  { name: 'shared-games', handlers: sharedGamesHandlers },
  { name: 'catalog', handlers: catalogHandlers },
  { name: 'admin', handlers: adminHandlers },
  { name: 'sessions', handlers: sessionsHandlers },
  { name: 'game-nights', handlers: gameNightsHandlers },
  { name: 'players', handlers: playersHandlers },
  { name: 'play-records', handlers: playRecordsHandlers },
  { name: 'notifications', handlers: notificationsHandlers },
  { name: 'badges', handlers: badgesHandlers },
];
