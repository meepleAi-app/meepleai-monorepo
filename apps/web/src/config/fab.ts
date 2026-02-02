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
 */
export const FAB_CONFIG: Record<LayoutContext, FABActionConfig | null> = {
  default: null,

  library: {
    icon: 'plus',
    label: 'Aggiungi gioco',
    action: 'library:add',
    quickMenuItems: [
      { id: 'search', icon: 'search', label: 'Cerca', action: 'library:search' },
      { id: 'scan', icon: 'camera', label: 'Scansiona', action: 'library:scan' },
    ],
  },

  game_detail: {
    icon: 'play',
    label: 'Inizia sessione',
    action: 'game:start-session',
    quickMenuItems: [
      { id: 'ask-ai', icon: 'message-square', label: 'Chiedi AI', action: 'game:ask-ai' },
      { id: 'share', icon: 'share', label: 'Condividi', action: 'game:share' },
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

  chat: {
    icon: 'plus-circle',
    label: 'Nuova chat',
    action: 'chat:new',
    quickMenuItems: [
      { id: 'history', icon: 'history', label: 'Cronologia', action: 'chat:history' },
    ],
  },

  search: null,

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
