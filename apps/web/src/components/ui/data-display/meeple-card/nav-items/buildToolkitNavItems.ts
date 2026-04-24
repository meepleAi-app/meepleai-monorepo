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
 *
 * Step 2 (2026-04-24): renamed from buildToolkitNavItems to buildToolkitConnections.
 * Return shape changed from NavFooterItem[] to ConnectionChipProps[].
 * Old name retained as deprecated re-export until cleanup commit 8.
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

/**
 * @deprecated Use `buildToolkitConnections` instead. Will be removed in commit 8
 * of the Step 2 migration PR.
 */
export const buildToolkitNavItems = buildToolkitConnections;

/** @deprecated Use `ToolkitConnectionsCounts` instead. Removed in commit 8. */
export type ToolkitNavCounts = ToolkitConnectionsCounts;

/** @deprecated Use `ToolkitConnectionsHandlers` instead. Removed in commit 8. */
export type ToolkitNavHandlers = ToolkitConnectionsHandlers;
