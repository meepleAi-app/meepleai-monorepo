/**
 * QuickStats Component - Issue #4936 (updated warm style)
 * Issue #4581 - originally created
 *
 * Horizontal 4-KPI stats row with warm glassmorphism StatCards.
 */

import type { UserStatsDto } from '@/lib/api/dashboard-client';

import { StatCard } from './stat-card';

interface QuickStatsProps {
  stats: UserStatsDto | null;
  isLoading?: boolean;
}

export function QuickStats({ stats, isLoading }: QuickStatsProps) {
  if (isLoading) {
    return (
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {[...Array(4)].map((_, i) => (
          <div
            key={i}
            className={[
              'h-28 rounded-2xl animate-pulse',
              'bg-[rgba(200,180,160,0.20)] dark:bg-[rgba(40,36,32,0.40)]',
            ].join(' ')}
            aria-label="Loading stats"
          />
        ))}
      </div>
    );
  }

  if (!stats) return null;

  // Format weekly playtime from TimeSpan "HH:MM:SS"
  const formatPlayTime = (timeSpan: string): string => {
    const parts = timeSpan.split(':');
    const hours = parseInt(parts[0] || '0', 10);
    const minutes = parseInt(parts[1] || '0', 10);
    return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
  };

  const monthChange = stats.monthlyPlaysChange;
  const changeBadge =
    monthChange !== 0 ? `${monthChange > 0 ? '+' : ''}${monthChange}%` : undefined;

  return (
    <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
      <StatCard
        icon="🎲"
        value={stats.totalGames}
        label="Giochi in collezione"
      />

      <StatCard
        icon="🎯"
        value={stats.monthlyPlays}
        label="Partite questo mese"
        badge={changeBadge}
        badgeTone={monthChange > 0 ? 'positive' : monthChange < 0 ? 'negative' : 'neutral'}
      />

      <StatCard
        icon="⏱️"
        value={formatPlayTime(stats.weeklyPlayTime)}
        label="Giocate questa settimana"
      />

      <StatCard
        icon="⭐"
        value={stats.monthlyFavorites}
        label="Preferiti questo mese"
      />
    </div>
  );
}
