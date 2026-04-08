import { navIcons } from './icons';

import type { NavFooterItem } from '../types';

export interface PlayerNavCounts {
  totalWins: number;
  totalSessions: number;
  /** v1: not implemented — slot rendered disabled */
  favoriteCount?: number;
  /** v1: not implemented — slot rendered disabled */
  achievementCount?: number;
}

export interface PlayerNavHandlers {
  onWinsClick?: () => void;
  onSessionsClick?: () => void;
  onFavoritesClick?: () => void;
  onAchievementsClick?: () => void;
}

/**
 * Build the 4-slot nav-footer for player entity cards.
 *
 * Slots: Vittorie | Partite | Preferiti (v1 disabled) | Trofei (v1 disabled)
 */
export function buildPlayerNavItems(
  counts: PlayerNavCounts,
  handlers: PlayerNavHandlers
): NavFooterItem[] {
  return [
    {
      icon: navIcons.trophy,
      label: 'Vittorie',
      entity: 'player',
      count: counts.totalWins > 0 ? counts.totalWins : undefined,
      disabled: !handlers.onWinsClick,
      onClick: handlers.onWinsClick,
    },
    {
      icon: navIcons.partite,
      label: 'Partite',
      entity: 'session',
      count: counts.totalSessions > 0 ? counts.totalSessions : undefined,
      disabled: !handlers.onSessionsClick,
      onClick: handlers.onSessionsClick,
    },
    {
      icon: navIcons.favorites,
      label: 'Preferiti',
      entity: 'game',
      count: counts.favoriteCount,
      disabled: counts.favoriteCount === undefined || !handlers.onFavoritesClick,
      onClick: handlers.onFavoritesClick,
    },
    {
      icon: navIcons.achievement,
      label: 'Trofei',
      entity: 'player',
      count: counts.achievementCount,
      disabled: counts.achievementCount === undefined || !handlers.onAchievementsClick,
      onClick: handlers.onAchievementsClick,
    },
  ];
}
