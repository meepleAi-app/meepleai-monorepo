/**
 * QuickStats Component - Issue #4581
 * 4-card stats overview grid
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
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {[...Array(4)].map((_, i) => (
          <div
            key={i}
            className="h-32 animate-pulse rounded-lg bg-muted"
            aria-label="Loading stats"
          />
        ))}
      </div>
    );
  }

  if (!stats) {
    return null;
  }

  // Format weekly playtime from TimeSpan "HH:MM:SS"
  const formatPlayTime = (timeSpan: string): string => {
    const parts = timeSpan.split(':');
    const hours = parseInt(parts[0] || '0', 10);
    const minutes = parseInt(parts[1] || '0', 10);
    return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
  };

  const changeIndicator =
    stats.monthlyPlaysChange > 0 ? `+${stats.monthlyPlaysChange}%` : `${stats.monthlyPlaysChange}%`;

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      <StatCard icon="🎲" value={stats.totalGames} label="Giochi Collezione" />

      <StatCard
        icon="🎯"
        value={stats.monthlyPlays}
        label="Partite"
        sublabel={`Questo Mese (${changeIndicator})`}
      />

      <StatCard
        icon="⏱️"
        value={formatPlayTime(stats.weeklyPlayTime)}
        label="Giocate"
        sublabel="Questa Settimana"
      />

      <StatCard
        icon="⭐"
        value={stats.monthlyFavorites}
        label="Preferiti"
        sublabel="Questo Mese"
      />
    </div>
  );
}
