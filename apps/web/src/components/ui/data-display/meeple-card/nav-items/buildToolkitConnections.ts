import type { ConnectionChipProps } from '../types';

export interface ToolkitConnectionsCounts {
  toolCount: number;
  deckCount: number;
  phaseCount: number;
  useCount: number;
}

export interface ToolkitConnectionsHandlers {
  onToolsClick?: () => void;
  onDecksClick?: () => void;
  onPhasesClick?: () => void;
  onHistoryClick?: () => void;
}

/**
 * Build the 4-slot connection channel for toolkit entity cards.
 *
 * Slots: Tools | Decks | Phases | Storico (use count)
 */
export function buildToolkitConnections(
  counts: ToolkitConnectionsCounts,
  handlers: ToolkitConnectionsHandlers
): ConnectionChipProps[] {
  return [
    {
      label: 'Tools',
      entityType: 'tool',
      count: counts.toolCount > 0 ? counts.toolCount : undefined,
      disabled: !handlers.onToolsClick,
      onClick: handlers.onToolsClick,
    },
    {
      label: 'Decks',
      entityType: 'toolkit',
      count: counts.deckCount > 0 ? counts.deckCount : undefined,
      disabled: !handlers.onDecksClick,
      onClick: handlers.onDecksClick,
    },
    {
      label: 'Phases',
      entityType: 'toolkit',
      count: counts.phaseCount > 0 ? counts.phaseCount : undefined,
      disabled: !handlers.onPhasesClick,
      onClick: handlers.onPhasesClick,
    },
    {
      label: 'Storico',
      entityType: 'session',
      count: counts.useCount > 0 ? counts.useCount : undefined,
      disabled: !handlers.onHistoryClick,
      onClick: handlers.onHistoryClick,
    },
  ];
}
