import { navIcons } from './icons';

import type { NavFooterItem } from '../types';

export interface ToolkitNavCounts {
  toolCount: number;
  deckCount: number;
  phaseCount: number;
  useCount: number;
}

export interface ToolkitNavHandlers {
  onToolsClick?: () => void;
  onDecksClick?: () => void;
  onPhasesClick?: () => void;
  onHistoryClick?: () => void;
}

/**
 * Build the 4-slot nav-footer for toolkit entity cards.
 *
 * Slots: Tools | Decks | Phases | Storico (use count)
 */
export function buildToolkitNavItems(
  counts: ToolkitNavCounts,
  handlers: ToolkitNavHandlers
): NavFooterItem[] {
  return [
    {
      icon: navIcons.tools,
      label: 'Tools',
      entity: 'tool',
      count: counts.toolCount > 0 ? counts.toolCount : undefined,
      disabled: !handlers.onToolsClick,
      onClick: handlers.onToolsClick,
    },
    {
      icon: navIcons.decks,
      label: 'Decks',
      entity: 'toolkit',
      count: counts.deckCount > 0 ? counts.deckCount : undefined,
      disabled: !handlers.onDecksClick,
      onClick: handlers.onDecksClick,
    },
    {
      icon: navIcons.phases,
      label: 'Phases',
      entity: 'toolkit',
      count: counts.phaseCount > 0 ? counts.phaseCount : undefined,
      disabled: !handlers.onPhasesClick,
      onClick: handlers.onPhasesClick,
    },
    {
      icon: navIcons.history,
      label: 'Storico',
      entity: 'session',
      count: counts.useCount > 0 ? counts.useCount : undefined,
      disabled: !handlers.onHistoryClick,
      onClick: handlers.onHistoryClick,
    },
  ];
}
