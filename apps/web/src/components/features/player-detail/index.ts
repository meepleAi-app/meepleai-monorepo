/**
 * Barrel exports for v2 player-detail components — Wave 3 /players/[id].
 *
 * All components are pure presentational (no hooks, no useTranslation, no
 * useQuery). Orchestrator passes resolved data via props.
 */

export { PlayerHero } from '@/components/features/player-detail/PlayerHero';
export type { PlayerHeroProps, PlayerHeroLabels } from '@/components/features/player-detail/PlayerHero';

export { PlayerStatsGrid } from '@/components/features/player-detail/PlayerStatsGrid';
export type { PlayerStatsGridProps, PlayerStatsGridLabels } from '@/components/features/player-detail/PlayerStatsGrid';

export { PlayerLeaderboardCard } from '@/components/features/player-detail/PlayerLeaderboardCard';
export type {
  PlayerLeaderboardCardProps,
  PlayerLeaderboardCardLabels,
} from '@/components/features/player-detail/PlayerLeaderboardCard';

export { FavoriteAgentCard } from '@/components/features/player-detail/FavoriteAgentCard';
export type { FavoriteAgentCardProps, FavoriteAgentCardLabels } from '@/components/features/player-detail/FavoriteAgentCard';

export { AchievementBadgeGrid } from '@/components/features/player-detail/AchievementBadgeGrid';
export type { AchievementBadgeGridProps, AchievementBadgeGridLabels } from '@/components/features/player-detail/AchievementBadgeGrid';
