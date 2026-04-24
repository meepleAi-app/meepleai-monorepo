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
