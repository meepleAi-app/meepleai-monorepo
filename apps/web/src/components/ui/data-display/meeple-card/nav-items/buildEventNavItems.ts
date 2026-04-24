import type { ConnectionChipProps } from '../types';

export interface EventConnectionsCounts {
  participantCount: number;
  gameCount: number;
}

export interface EventConnectionsHandlers {
  onParticipantsClick?: () => void;
  onLocationClick?: () => void;
  onGamesClick?: () => void;
  onDateClick?: () => void;
}

/**
 * Build the 4-slot connection channel for event (game night) entity cards.
 *
 * Slots: Partecipanti | Luogo (link) | Giochi | Data (link)
 *
 * Step 2 (2026-04-24): renamed from buildEventNavItems to buildEventConnections.
 * Return shape changed from NavFooterItem[] to ConnectionChipProps[].
 * Old name retained as deprecated re-export until cleanup commit 8.
 */
export function buildEventConnections(
  counts: EventConnectionsCounts,
  handlers: EventConnectionsHandlers
): ConnectionChipProps[] {
  return [
    {
      label: 'Partecipanti',
      entityType: 'player',
      count: counts.participantCount > 0 ? counts.participantCount : undefined,
      disabled: !handlers.onParticipantsClick,
      onClick: handlers.onParticipantsClick,
    },
    {
      label: 'Luogo',
      entityType: 'event',
      disabled: !handlers.onLocationClick,
      onClick: handlers.onLocationClick,
    },
    {
      label: 'Giochi',
      entityType: 'game',
      count: counts.gameCount > 0 ? counts.gameCount : undefined,
      disabled: !handlers.onGamesClick,
      onClick: handlers.onGamesClick,
    },
    {
      label: 'Data',
      entityType: 'event',
      disabled: !handlers.onDateClick,
      onClick: handlers.onDateClick,
    },
  ];
}

/**
 * @deprecated Use `buildEventConnections` instead. Will be removed in commit 8
 * of the Step 2 migration PR.
 */
export const buildEventNavItems = buildEventConnections;

/** @deprecated Use `EventConnectionsCounts` instead. Removed in commit 8. */
export type EventNavCounts = EventConnectionsCounts;

/** @deprecated Use `EventConnectionsHandlers` instead. Removed in commit 8. */
export type EventNavHandlers = EventConnectionsHandlers;
