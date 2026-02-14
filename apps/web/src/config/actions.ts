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
 *
 * Issue #3479 - Extended contexts for Layout System v2
 */
export const CONTEXT_ACTIONS: Record<LayoutContext, Action[]> = {
  // ─── Core Domain ─────────────────────────────────────────────────────
  default: [],

  dashboard: [
    {
      id: 'quick-play',
      icon: 'play',
      label: 'Gioca',
      priority: 1,
      action: 'dashboard:quick-play',
      variant: 'primary',
    },
    {
      id: 'add-game',
      icon: 'plus',
      label: 'Aggiungi',
      priority: 2,
      action: 'dashboard:add-game',
    },
    {
      id: 'recent',
      icon: 'history',
      label: 'Recenti',
      priority: 3,
      action: 'dashboard:recent',
    },
  ],

  // ─── Library Domain ───────────────────────────────────────────────────
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

  library_empty: [
    {
      id: 'add',
      icon: 'plus',
      label: 'Aggiungi primo gioco',
      priority: 1,
      action: 'library:add',
      variant: 'primary',
    },
    {
      id: 'browse',
      icon: 'gamepad-2',
      label: 'Sfoglia catalogo',
      priority: 2,
      action: 'library:browse-catalog',
    },
    {
      id: 'import',
      icon: 'download',
      label: 'Importa',
      priority: 3,
      action: 'library:import',
    },
  ],

  library_selection: [
    {
      id: 'compare',
      icon: 'bar-chart-2',
      label: 'Confronta',
      priority: 1,
      action: 'library:compare',
    },
    {
      id: 'batch-play',
      icon: 'play',
      label: 'Gioca insieme',
      priority: 2,
      action: 'library:batch-play',
    },
    {
      id: 'batch-export',
      icon: 'download',
      label: 'Esporta',
      priority: 3,
      action: 'library:batch-export',
    },
    {
      id: 'batch-delete',
      icon: 'trash-2',
      label: 'Rimuovi',
      priority: 4,
      action: 'library:batch-delete',
      variant: 'destructive',
    },
  ],

  // ─── Game Domain ─────────────────────────────────────────────────────
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

  game_detail_not_owned: [
    {
      id: 'add',
      icon: 'plus',
      label: 'Aggiungi',
      priority: 1,
      action: 'game:add-to-library',
      variant: 'primary',
    },
    {
      id: 'wishlist',
      icon: 'heart',
      label: 'Wishlist',
      priority: 2,
      action: 'game:wishlist',
    },
    {
      id: 'ask-ai',
      icon: 'message-square',
      label: 'Chiedi AI',
      priority: 3,
      action: 'game:ask-ai',
    },
    {
      id: 'share',
      icon: 'share',
      label: 'Condividi',
      priority: 4,
      action: 'game:share',
    },
  ],

  // ─── Session Domain ──────────────────────────────────────────────────
  session_setup: [
    {
      id: 'start',
      icon: 'play',
      label: 'Inizia',
      priority: 1,
      action: 'session:start',
      variant: 'primary',
    },
    {
      id: 'add-player',
      icon: 'user-plus',
      label: 'Giocatore',
      priority: 2,
      action: 'session:add-player',
    },
    {
      id: 'rules',
      icon: 'book-open',
      label: 'Regole',
      priority: 3,
      action: 'session:rules',
    },
    {
      id: 'cancel',
      icon: 'x',
      label: 'Annulla',
      priority: 4,
      action: 'session:cancel',
      variant: 'destructive',
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

  session_end: [
    {
      id: 'save',
      icon: 'save',
      label: 'Salva',
      priority: 1,
      action: 'session:save-results',
      variant: 'primary',
    },
    {
      id: 'share',
      icon: 'share',
      label: 'Condividi',
      priority: 2,
      action: 'session:share-results',
    },
    {
      id: 'rematch',
      icon: 'rotate-ccw',
      label: 'Rivincita',
      priority: 3,
      action: 'session:rematch',
    },
    {
      id: 'back',
      icon: 'home',
      label: 'Home',
      priority: 4,
      action: 'session:back-home',
    },
  ],

  // ─── Content Domain ──────────────────────────────────────────────────
  document_viewer: [
    {
      id: 'ask-ai',
      icon: 'message-square',
      label: 'Chiedi AI',
      priority: 1,
      action: 'document:ask-ai',
      variant: 'primary',
    },
    {
      id: 'zoom-in',
      icon: 'zoom-in',
      label: 'Zoom +',
      priority: 2,
      action: 'document:zoom-in',
    },
    {
      id: 'zoom-out',
      icon: 'zoom-out',
      label: 'Zoom -',
      priority: 3,
      action: 'document:zoom-out',
    },
    {
      id: 'bookmark',
      icon: 'bookmark',
      label: 'Segnalibro',
      priority: 4,
      action: 'document:bookmark',
    },
    {
      id: 'download',
      icon: 'download',
      label: 'Scarica',
      priority: 5,
      action: 'document:download',
    },
  ],

  // (catalog is part of Game domain but listed separately for context mapping)
  catalog: [
    {
      id: 'filter',
      icon: 'filter',
      label: 'Filtra',
      priority: 1,
      action: 'catalog:filter',
    },
    {
      id: 'sort',
      icon: 'arrow-up-down',
      label: 'Ordina',
      priority: 2,
      action: 'catalog:sort',
    },
    {
      id: 'view',
      icon: 'layout-grid',
      label: 'Vista',
      priority: 3,
      action: 'catalog:view',
    },
  ],

  // ─── User Domain ─────────────────────────────────────────────────────
  wishlist: [
    {
      id: 'sort',
      icon: 'arrow-up-down',
      label: 'Ordina',
      priority: 1,
      action: 'wishlist:sort',
    },
    {
      id: 'share',
      icon: 'share',
      label: 'Condividi',
      priority: 2,
      action: 'wishlist:share',
    },
    {
      id: 'export',
      icon: 'download',
      label: 'Esporta',
      priority: 3,
      action: 'wishlist:export',
    },
  ],

  notifications: [
    {
      id: 'mark-read',
      icon: 'check-check',
      label: 'Letti tutti',
      priority: 1,
      action: 'notifications:mark-all-read',
    },
    {
      id: 'filter',
      icon: 'filter',
      label: 'Filtra',
      priority: 2,
      action: 'notifications:filter',
    },
    {
      id: 'settings',
      icon: 'settings',
      label: 'Impostazioni',
      priority: 3,
      action: 'notifications:settings',
    },
  ],

  profile: [
    {
      id: 'edit',
      icon: 'edit',
      label: 'Modifica',
      priority: 1,
      action: 'profile:edit',
    },
    {
      id: 'share',
      icon: 'share',
      label: 'Condividi',
      priority: 2,
      action: 'profile:share',
    },
    {
      id: 'stats',
      icon: 'bar-chart-2',
      label: 'Statistiche',
      priority: 3,
      action: 'profile:stats',
    },
  ],

  // ─── Communication Domain ─────────────────────────────────────────────
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
  // eslint-disable-next-line security/detect-object-injection -- context is from typed LayoutContext union
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
  // eslint-disable-next-line security/detect-object-injection -- deviceType is from typed literal union
  return MAX_VISIBLE_ACTIONS[deviceType];
}
