import type { ConnectionChipProps } from '../types';

export interface ToolConnectionsHandlers {
  onUseClick?: () => void;
  onEditClick?: () => void;
  onDuplicateClick?: () => void;
  onHistoryClick?: () => void;
}

/**
 * Build the 4-slot connection channel for individual tool entity cards.
 *
 * Slots: Usa (action) | Modifica (action) | Duplica (action) | Storico
 *
 * Tools are pure action containers — no counts.
 */
export function buildToolConnections(handlers: ToolConnectionsHandlers): ConnectionChipProps[] {
  return [
    {
      label: 'Usa',
      entityType: 'tool',
      disabled: !handlers.onUseClick,
      onClick: handlers.onUseClick,
    },
    {
      label: 'Modifica',
      entityType: 'tool',
      disabled: !handlers.onEditClick,
      onClick: handlers.onEditClick,
    },
    {
      label: 'Duplica',
      entityType: 'tool',
      disabled: !handlers.onDuplicateClick,
      onClick: handlers.onDuplicateClick,
    },
    {
      label: 'Storico',
      entityType: 'tool',
      disabled: !handlers.onHistoryClick,
      onClick: handlers.onHistoryClick,
    },
  ];
}
