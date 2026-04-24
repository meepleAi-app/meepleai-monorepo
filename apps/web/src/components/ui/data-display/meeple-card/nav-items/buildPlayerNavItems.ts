import type { ConnectionChipProps } from '../types';

export interface PlayerConnectionsCounts {
  totalWins: number;
  totalSessions: number;
  /** v1: not implemented — slot rendered disabled */
  favoriteCount?: number;
  /** v1: not implemented — slot rendered disabled */
  achievementCount?: number;
}

export interface PlayerConnectionsHandlers {
  onWinsClick?: () => void;
  onSessionsClick?: () => void;
  onFavoritesClick?: () => void;
  onAchievementsClick?: () => void;
}

/**
 * Build the 4-slot connection channel for player entity cards.
 *
 * Slots: Vittorie | Partite | Preferiti (v1 disabled) | Trofei (v1 disabled)
 *
 * Step 2 (2026-04-24): renamed from buildPlayerNavItems to buildPlayerConnections.
 * Return shape changed from NavFooterItem[] to ConnectionChipProps[].
 * Old name retained as deprecated re-export until cleanup commit 8.
 */
export function buildPlayerConnections(
  counts: PlayerConnectionsCounts,
  handlers: PlayerConnectionsHandlers
): ConnectionChipProps[] {
  return [
    {
      label: 'Vittorie',
      entityType: 'player',
      count: counts.totalWins > 0 ? counts.totalWins : undefined,
      disabled: !handlers.onWinsClick,
      onClick: handlers.onWinsClick,
    },
    {
      label: 'Partite',
      entityType: 'session',
      count: counts.totalSessions > 0 ? counts.totalSessions : undefined,
      disabled: !handlers.onSessionsClick,
      onClick: handlers.onSessionsClick,
    },
    {
      label: 'Preferiti',
      entityType: 'game',
      count: counts.favoriteCount,
      disabled: counts.favoriteCount === undefined || !handlers.onFavoritesClick,
      onClick: handlers.onFavoritesClick,
    },
    {
      label: 'Trofei',
      entityType: 'player',
      count: counts.achievementCount,
      disabled: counts.achievementCount === undefined || !handlers.onAchievementsClick,
      onClick: handlers.onAchievementsClick,
    },
  ];
}

/**
 * @deprecated Use `buildPlayerConnections` instead. Will be removed in commit 8
 * of the Step 2 migration PR.
 */
export const buildPlayerNavItems = buildPlayerConnections;

/** @deprecated Use `PlayerConnectionsCounts` instead. */
export type PlayerNavCounts = PlayerConnectionsCounts;

/** @deprecated Use `PlayerConnectionsHandlers` instead. */
export type PlayerNavHandlers = PlayerConnectionsHandlers;
