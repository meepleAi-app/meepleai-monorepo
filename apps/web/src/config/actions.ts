/**
 * Action Configuration
 * Issue #3290 - Phase 4: ActionBar System
 *
 * Defines context-specific actions for the ActionBar system.
 */

import type { Action, LayoutContext } from '@/types/layout';

/**
 * Context-specific action configurations.
 * Actions are ordered by priority (lower number = higher priority = more visible).
 */
export const CONTEXT_ACTIONS: Record<LayoutContext, Action[]> = {
  default: [],

  library: [
    {
      id: 'add',
      icon: 'plus',
      label: 'Aggiungi',
      priority: 1,
      action: 'library:add',
    },
    {
      id: 'filter',
      icon: 'filter',
      label: 'Filtra',
      priority: 2,
      action: 'library:filter',
    },
    {
      id: 'sort',
      icon: 'arrow-up-down',
      label: 'Ordina',
      priority: 3,
      action: 'library:sort',
    },
    {
      id: 'view',
      icon: 'layout-grid',
      label: 'Vista',
      priority: 4,
      action: 'library:view',
    },
    {
      id: 'export',
      icon: 'download',
      label: 'Esporta',
      priority: 5,
      action: 'library:export',
    },
  ],

  game_detail: [
    {
      id: 'play',
      icon: 'play',
      label: 'Gioca',
      priority: 1,
      action: 'game:play',
      variant: 'primary',
    },
    {
      id: 'add',
      icon: 'plus',
      label: 'Aggiungi',
      priority: 2,
      action: 'game:add-to-library',
    },
    {
      id: 'ask-ai',
      icon: 'message-square',
      label: 'Chiedi AI',
      priority: 3,
      action: 'game:ask-ai',
    },
    {
      id: 'wishlist',
      icon: 'heart',
      label: 'Wishlist',
      priority: 4,
      action: 'game:wishlist',
    },
    {
      id: 'share',
      icon: 'share',
      label: 'Condividi',
      priority: 5,
      action: 'game:share',
    },
    {
      id: 'report',
      icon: 'flag',
      label: 'Segnala',
      priority: 6,
      action: 'game:report',
    },
  ],

  session_active: [
    {
      id: 'ask-ai',
      icon: 'message-square',
      label: 'Chiedi AI',
      priority: 1,
      action: 'session:ask-ai',
      variant: 'primary',
    },
    {
      id: 'timer',
      icon: 'timer',
      label: 'Timer',
      priority: 2,
      action: 'session:timer',
    },
    {
      id: 'scores',
      icon: 'trophy',
      label: 'Punteggi',
      priority: 3,
      action: 'session:scores',
    },
    {
      id: 'pause',
      icon: 'pause',
      label: 'Pausa',
      priority: 4,
      action: 'session:pause',
    },
    {
      id: 'end',
      icon: 'square',
      label: 'Fine',
      priority: 5,
      action: 'session:end',
      variant: 'destructive',
    },
  ],

  chat: [
    {
      id: 'send',
      icon: 'send',
      label: 'Invia',
      priority: 1,
      action: 'chat:send',
      variant: 'primary',
    },
    {
      id: 'attach',
      icon: 'paperclip',
      label: 'Allega',
      priority: 2,
      action: 'chat:attach',
    },
    {
      id: 'voice',
      icon: 'mic',
      label: 'Voce',
      priority: 3,
      action: 'chat:voice',
    },
    {
      id: 'new-chat',
      icon: 'plus-circle',
      label: 'Nuova Chat',
      priority: 4,
      action: 'chat:new',
    },
    {
      id: 'history',
      icon: 'history',
      label: 'Cronologia',
      priority: 5,
      action: 'chat:history',
    },
  ],

  search: [
    {
      id: 'filter',
      icon: 'filter',
      label: 'Filtra',
      priority: 1,
      action: 'search:filter',
    },
    {
      id: 'sort',
      icon: 'arrow-up-down',
      label: 'Ordina',
      priority: 2,
      action: 'search:sort',
    },
    {
      id: 'save',
      icon: 'bookmark',
      label: 'Salva',
      priority: 3,
      action: 'search:save',
    },
  ],

  settings: [
    {
      id: 'save',
      icon: 'save',
      label: 'Salva',
      priority: 1,
      action: 'settings:save',
      variant: 'primary',
    },
    {
      id: 'reset',
      icon: 'rotate-ccw',
      label: 'Reset',
      priority: 2,
      action: 'settings:reset',
    },
  ],
};

/**
 * Get actions for a specific context.
 * Returns actions sorted by priority (ascending).
 */
export function getActionsForContext(context: LayoutContext): Action[] {
  const actions = CONTEXT_ACTIONS[context] ?? CONTEXT_ACTIONS.default;
  return [...actions].sort((a, b) => a.priority - b.priority);
}

/**
 * Maximum visible actions per breakpoint.
 */
export const MAX_VISIBLE_ACTIONS = {
  mobile: 3,
  tablet: 4,
  desktop: 6,
} as const;

/**
 * Get the maximum visible actions for a device type.
 */
export function getMaxVisibleActions(
  deviceType: 'mobile' | 'tablet' | 'desktop'
): number {
  return MAX_VISIBLE_ACTIONS[deviceType];
}
