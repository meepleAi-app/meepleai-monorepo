/**
 * Barrel exports for v2 player-detail components — Wave 3 /players/[id].
 *
 * All components are pure presentational (no hooks, no useTranslation, no
 * useQuery). Orchestrator passes resolved data via props.
 */

export { PlayerHero } from './PlayerHero';
export type { PlayerHeroProps, PlayerHeroLabels } from './PlayerHero';

export { PlayerStatsGrid } from './PlayerStatsGrid';
export type { PlayerStatsGridProps, PlayerStatsGridLabels } from './PlayerStatsGrid';

export { PlayerLeaderboardCard } from './PlayerLeaderboardCard';
export type {
  PlayerLeaderboardCardProps,
  PlayerLeaderboardCardLabels,
} from './PlayerLeaderboardCard';

export { FavoriteAgentCard } from './FavoriteAgentCard';
export type { FavoriteAgentCardProps, FavoriteAgentCardLabels } from './FavoriteAgentCard';

export { AchievementBadgeGrid } from './AchievementBadgeGrid';
export type { AchievementBadgeGridProps, AchievementBadgeGridLabels } from './AchievementBadgeGrid';
