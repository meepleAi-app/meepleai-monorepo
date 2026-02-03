/**
 * FAB Configuration
 * Issue #3291 - Phase 5: Smart FAB
 *
 * Defines context-specific FAB actions and quick menu items.
 */

import type { LayoutContext } from '@/types/layout';

/**
 * Quick menu item configuration
 */
export interface QuickMenuItem {
  id: string;
  icon: string;
  label: string;
  action: string;
}

/**
 * FAB action with quick menu items
 */
export interface FABActionConfig {
  icon: string;
  label: string;
  action: string;
  quickMenuItems: QuickMenuItem[];
}

/**
 * Context-specific FAB configurations.
 *
 * Issue #3479 - Extended contexts for Layout System v2
 */
export const FAB_CONFIG: Record<LayoutContext, FABActionConfig | null> = {
  default: null,

  dashboard: {
    icon: 'plus',
    label: 'Azione rapida',
    action: 'dashboard:quick-action',
    quickMenuItems: [
      { id: 'add-game', icon: 'plus', label: 'Aggiungi gioco', action: 'dashboard:add-game' },
      { id: 'quick-play', icon: 'play', label: 'Gioca', action: 'dashboard:quick-play' },
    ],
  },

  library: {
    icon: 'plus',
    label: 'Aggiungi gioco',
    action: 'library:add',
    quickMenuItems: [
      { id: 'search', icon: 'search', label: 'Cerca', action: 'library:search' },
      { id: 'scan', icon: 'camera', label: 'Scansiona', action: 'library:scan' },
    ],
  },

  library_empty: {
    icon: 'plus',
    label: 'Aggiungi primo gioco',
    action: 'library:add',
    quickMenuItems: [
      { id: 'browse', icon: 'gamepad-2', label: 'Sfoglia catalogo', action: 'library:browse-catalog' },
      { id: 'import', icon: 'download', label: 'Importa', action: 'library:import' },
    ],
  },

  library_selection: null, // No FAB in selection mode

  game_detail: {
    icon: 'play',
    label: 'Inizia sessione',
    action: 'game:start-session',
    quickMenuItems: [
      { id: 'ask-ai', icon: 'message-square', label: 'Chiedi AI', action: 'game:ask-ai' },
      { id: 'share', icon: 'share', label: 'Condividi', action: 'game:share' },
    ],
  },

  game_detail_not_owned: {
    icon: 'plus',
    label: 'Aggiungi alla libreria',
    action: 'game:add-to-library',
    quickMenuItems: [
      { id: 'wishlist', icon: 'heart', label: 'Wishlist', action: 'game:wishlist' },
      { id: 'ask-ai', icon: 'message-square', label: 'Chiedi AI', action: 'game:ask-ai' },
    ],
  },

  session_setup: {
    icon: 'play',
    label: 'Inizia partita',
    action: 'session:start',
    quickMenuItems: [
      { id: 'add-player', icon: 'user-plus', label: 'Aggiungi giocatore', action: 'session:add-player' },
      { id: 'rules', icon: 'book-open', label: 'Regole', action: 'session:rules' },
    ],
  },

  session_active: {
    icon: 'message-square',
    label: 'Chiedi AI',
    action: 'session:ask-ai',
    quickMenuItems: [
      { id: 'timer', icon: 'timer', label: 'Timer', action: 'session:timer' },
      { id: 'scores', icon: 'trophy', label: 'Punteggi', action: 'session:scores' },
    ],
  },

  session_end: {
    icon: 'save',
    label: 'Salva risultati',
    action: 'session:save-results',
    quickMenuItems: [
      { id: 'share', icon: 'share', label: 'Condividi', action: 'session:share-results' },
      { id: 'rematch', icon: 'rotate-ccw', label: 'Rivincita', action: 'session:rematch' },
    ],
  },

  document_viewer: {
    icon: 'message-square',
    label: 'Chiedi AI',
    action: 'document:ask-ai',
    quickMenuItems: [
      { id: 'bookmark', icon: 'bookmark', label: 'Segnalibro', action: 'document:bookmark' },
      { id: 'download', icon: 'download', label: 'Scarica', action: 'document:download' },
    ],
  },

  chat: {
    icon: 'plus-circle',
    label: 'Nuova chat',
    action: 'chat:new',
    quickMenuItems: [
      { id: 'history', icon: 'history', label: 'Cronologia', action: 'chat:history' },
    ],
  },

  catalog: null, // No FAB in catalog view

  search: null,

  wishlist: null, // No FAB in wishlist

  notifications: null, // No FAB in notifications

  profile: null, // No FAB in profile

  settings: null,
};

/**
 * Get FAB configuration for a specific context.
 */
export function getFABConfig(context: LayoutContext): FABActionConfig | null {
  // eslint-disable-next-line security/detect-object-injection -- context is from typed LayoutContext union
  return FAB_CONFIG[context] ?? null;
}

/**
 * FAB timing constants
 */
export const FAB_TIMING = {
  /** Long press threshold in milliseconds */
  longPressDelay: 500,
  /** Morph animation duration */
  morphDuration: 200,
  /** Quick menu animation duration */
  quickMenuDuration: 150,
  /** Delay before showing FAB after scroll stops */
  scrollShowDelay: 300,
} as const;

/**
 * FAB position configuration
 */
export const FAB_POSITION = {
  /** Distance from right edge */
  right: 16,
  /** Distance from bottom edge */
  bottom: 80,
  /** FAB size in pixels */
  size: 56,
} as const;
