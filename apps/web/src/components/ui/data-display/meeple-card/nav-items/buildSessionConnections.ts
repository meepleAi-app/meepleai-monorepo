import type { ConnectionChipProps } from '../types';

export interface SessionConnectionsCounts {
  playerCount: number;
  hasNotes: boolean;
  toolCount: number;
  photoCount: number;
}

export interface SessionConnectionsHandlers {
  onPlayersClick?: () => void;
  onNotesClick?: () => void;
  onToolsClick?: () => void;
  onPhotosClick?: () => void;
}

/**
 * Build the 4-slot connection channel for session entity cards.
 *
 * Slots: Giocatori | Note (presence indicator) | Tools | Foto
 */
export function buildSessionConnections(
  counts: SessionConnectionsCounts,
  handlers: SessionConnectionsHandlers
): ConnectionChipProps[] {
  return [
    {
      label: 'Giocatori',
      entityType: 'player',
      count: counts.playerCount > 0 ? counts.playerCount : undefined,
      disabled: !handlers.onPlayersClick,
      onClick: handlers.onPlayersClick,
    },
    {
      label: 'Note',
      entityType: 'session',
      // Notes is presence-based (1 if any, undefined if none)
      count: counts.hasNotes ? 1 : undefined,
      disabled: !handlers.onNotesClick,
      onClick: handlers.onNotesClick,
    },
    {
      label: 'Tools',
      entityType: 'tool',
      count: counts.toolCount > 0 ? counts.toolCount : undefined,
      disabled: !handlers.onToolsClick,
      onClick: handlers.onToolsClick,
    },
    {
      label: 'Foto',
      entityType: 'session',
      count: counts.photoCount > 0 ? counts.photoCount : undefined,
      disabled: !handlers.onPhotosClick,
      onClick: handlers.onPhotosClick,
    },
  ];
}
