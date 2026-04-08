import { navIcons } from './icons';

import type { NavFooterItem } from '../types';

export interface EventNavCounts {
  participantCount: number;
  gameCount: number;
}

export interface EventNavHandlers {
  onParticipantsClick?: () => void;
  onLocationClick?: () => void;
  onGamesClick?: () => void;
  onDateClick?: () => void;
}

/**
 * Build the 4-slot nav-footer for event (game night) entity cards.
 *
 * Slots: Partecipanti | Luogo (link) | Giochi | Data (link)
 */
export function buildEventNavItems(
  counts: EventNavCounts,
  handlers: EventNavHandlers
): NavFooterItem[] {
  return [
    {
      icon: navIcons.players,
      label: 'Partecipanti',
      entity: 'player',
      count: counts.participantCount > 0 ? counts.participantCount : undefined,
      disabled: !handlers.onParticipantsClick,
      onClick: handlers.onParticipantsClick,
    },
    {
      icon: navIcons.location,
      label: 'Luogo',
      entity: 'event',
      disabled: !handlers.onLocationClick,
      onClick: handlers.onLocationClick,
    },
    {
      icon: navIcons.games,
      label: 'Giochi',
      entity: 'game',
      count: counts.gameCount > 0 ? counts.gameCount : undefined,
      disabled: !handlers.onGamesClick,
      onClick: handlers.onGamesClick,
    },
    {
      icon: navIcons.date,
      label: 'Data',
      entity: 'event',
      disabled: !handlers.onDateClick,
      onClick: handlers.onDateClick,
    },
  ];
}
