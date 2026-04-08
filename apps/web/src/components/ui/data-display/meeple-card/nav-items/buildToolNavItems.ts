import { navIcons } from './icons';

import type { NavFooterItem } from '../types';

export interface ToolNavHandlers {
  onUseClick?: () => void;
  onEditClick?: () => void;
  onDuplicateClick?: () => void;
  onHistoryClick?: () => void;
}

/**
 * Build the 4-slot nav-footer for individual tool entity cards.
 *
 * Slots: Usa (action) | Modifica (action) | Duplica (action) | Storico
 *
 * Tools are pure action containers — no counts.
 */
export function buildToolNavItems(handlers: ToolNavHandlers): NavFooterItem[] {
  return [
    {
      icon: navIcons.use,
      label: 'Usa',
      entity: 'tool',
      disabled: !handlers.onUseClick,
      onClick: handlers.onUseClick,
    },
    {
      icon: navIcons.edit,
      label: 'Modifica',
      entity: 'tool',
      disabled: !handlers.onEditClick,
      onClick: handlers.onEditClick,
    },
    {
      icon: navIcons.copy,
      label: 'Duplica',
      entity: 'tool',
      disabled: !handlers.onDuplicateClick,
      onClick: handlers.onDuplicateClick,
    },
    {
      icon: navIcons.history,
      label: 'Storico',
      entity: 'tool',
      disabled: !handlers.onHistoryClick,
      onClick: handlers.onHistoryClick,
    },
  ];
}
