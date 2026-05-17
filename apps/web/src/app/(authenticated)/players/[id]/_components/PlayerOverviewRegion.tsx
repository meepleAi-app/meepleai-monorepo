/**
 * PlayerOverviewRegion — stats + leaderboard + favorite-agent composition for
 * /players/[id] Stage 3 cluster (Issue #1113).
 *
 * Sits between the PlayerHero and the PlayerTabs inside the DetailPageLayout
 * `hero` slot. Wave 3 components are consumed unchanged.
 */

'use client';

import type { JSX } from 'react';

import {
  FavoriteAgentCard,
  PlayerLeaderboardCard,
  PlayerStatsGrid,
  type FavoriteAgentCardLabels,
  type PlayerLeaderboardCardLabels,
  type PlayerStatsGridLabels,
} from '@/components/features/player-detail';
import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

export interface PlayerOverviewRegionLabels {
  readonly stats: PlayerStatsGridLabels;
  readonly leaderboard: PlayerLeaderboardCardLabels;
  readonly favoriteAgent: FavoriteAgentCardLabels;
}

export interface PlayerOverviewRegionProps {
  readonly stats: PlayerProfileFixture;
  readonly labels: PlayerOverviewRegionLabels;
  readonly onFavoriteAgentClick?: () => void;
}

export function PlayerOverviewRegion({
  stats,
  labels,
  onFavoriteAgentClick,
}: PlayerOverviewRegionProps): JSX.Element {
  return (
    <div
      data-slot="player-overview-region"
      className="mx-auto w-full max-w-4xl px-4 sm:px-8 flex flex-col gap-4"
    >
      <PlayerStatsGrid
        totalSessions={stats.totalSessions}
        totalWins={stats.totalWins}
        winRate={stats.winRate}
        achievementCount={stats.achievementCount}
        labels={labels.stats}
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <PlayerLeaderboardCard rank={stats.leaderboardRank} labels={labels.leaderboard} />
        <FavoriteAgentCard
          agentName={stats.favoriteAgentName}
          gameName={stats.favoriteGameName}
          onClick={stats.favoriteAgentName != null ? onFavoriteAgentClick : undefined}
          labels={labels.favoriteAgent}
        />
      </div>
    </div>
  );
}
