import { navIcons } from './icons';

import type { NavFooterItem } from '../types';

export interface SessionNavCounts {
  playerCount: number;
  hasNotes: boolean;
  toolCount: number;
  photoCount: number;
}

export interface SessionNavHandlers {
  onPlayersClick?: () => void;
  onNotesClick?: () => void;
  onToolsClick?: () => void;
  onPhotosClick?: () => void;
}

/**
 * Build the 4-slot nav-footer for session entity cards.
 *
 * Slots: Giocatori | Note (presence indicator) | Tools | Foto
 */
export function buildSessionNavItems(
  counts: SessionNavCounts,
  handlers: SessionNavHandlers
): NavFooterItem[] {
  return [
    {
      icon: navIcons.players,
      label: 'Giocatori',
      entity: 'player',
      count: counts.playerCount > 0 ? counts.playerCount : undefined,
      disabled: !handlers.onPlayersClick,
      onClick: handlers.onPlayersClick,
    },
    {
      icon: navIcons.notes,
      label: 'Note',
      entity: 'session',
      // Notes is presence-based (1 if any, undefined if none)
      count: counts.hasNotes ? 1 : undefined,
      disabled: !handlers.onNotesClick,
      onClick: handlers.onNotesClick,
    },
    {
      icon: navIcons.tools,
      label: 'Tools',
      entity: 'tool',
      count: counts.toolCount > 0 ? counts.toolCount : undefined,
      disabled: !handlers.onToolsClick,
      onClick: handlers.onToolsClick,
    },
    {
      icon: navIcons.photos,
      label: 'Foto',
      entity: 'session',
      count: counts.photoCount > 0 ? counts.photoCount : undefined,
      disabled: !handlers.onPhotosClick,
      onClick: handlers.onPhotosClick,
    },
  ];
}
