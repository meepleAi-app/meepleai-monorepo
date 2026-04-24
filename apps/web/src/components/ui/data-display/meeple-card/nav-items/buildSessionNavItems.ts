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
 *
 * Step 2 (2026-04-24): renamed from buildSessionNavItems to buildSessionConnections.
 * Return shape changed from NavFooterItem[] to ConnectionChipProps[].
 * Old name retained as deprecated re-export until cleanup commit 8.
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

/**
 * @deprecated Use `buildSessionConnections` instead. Will be removed in commit 8
 * of the Step 2 migration PR.
 */
export const buildSessionNavItems = buildSessionConnections;

/** @deprecated Use `SessionConnectionsCounts` instead. */
export type SessionNavCounts = SessionConnectionsCounts;

/** @deprecated Use `SessionConnectionsHandlers` instead. */
export type SessionNavHandlers = SessionConnectionsHandlers;
