'use client';

/**
 * Dashboard Bento — Slim Orchestrator
 *
 * Composes the 12-column bento grid from individual widget modules in ./widgets/.
 * 60px row-height, 8px gap, 200px sidebar (lg+).
 */

import { useEffect } from 'react';

import { OnboardingFlow } from '@/components/dashboard/OnboardingFlow';
import { useDashboardStore } from '@/lib/stores/dashboard-store';

import {
  BentoDashboardSidebar,
  C,
  ChatPreviewWidget,
  KpiWidget,
  LeaderboardWidget,
  LibraryWidget,
  LiveSessionWidget,
  TrendingWidget,
} from './widgets';

function parsePlayTimeHours(timeSpan: string): string {
  if (!timeSpan) return '—';
  const parts = timeSpan.split(':');
  const hours = parseInt(parts[0] ?? '0', 10);
  const minutes = parseInt(parts[1] ?? '0', 10);
  return hours === 0 ? `${minutes}m` : `${hours}h`;
}

export function DashboardClient() {
  const {
    stats,
    isLoadingStats,
    fetchStats,
    recentSessions,
    isLoadingSessions,
    sessionsError,
    fetchRecentSessions,
    updateFilters,
    games,
    isLoadingGames,
    gamesError,
    fetchGames,
    totalGamesCount,
    trendingGames,
    isLoadingTrending,
    trendingError,
    fetchTrendingGames,
  } = useDashboardStore();

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(8);
    fetchTrendingGames(6);
    updateFilters({ sort: 'alphabetical', pageSize: 8, page: 1 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const onVisibility = () => {
      if (!document.hidden) fetchRecentSessions(8);
    };
    document.addEventListener('visibilitychange', onVisibility);
    return () => document.removeEventListener('visibilitychange', onVisibility);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const latestSession = recentSessions[0];
  const monthlyPlays = stats?.monthlyPlays ?? 0;
  const playsChange = stats?.monthlyPlaysChange;
  const weeklyHours = stats ? parsePlayTimeHours(stats.weeklyPlayTime) : '—';
  const totalGames = stats?.totalGames ?? 0;

  return (
    <div className="flex h-full bg-background overflow-hidden">
      <BentoDashboardSidebar />
      <div className="flex-1 overflow-y-auto p-3.5">
        <div
          className="grid grid-cols-6 lg:grid-cols-12"
          style={{ gridAutoRows: '60px', gap: '8px' }}
        >
          {/* Onboarding — renders once (localStorage-gated), then self-dismisses */}
          <div className="col-span-6 lg:col-span-12">
            <OnboardingFlow />
          </div>

          {/* Row 1-2: Live Session (8×2) + Partite KPI (4×2) */}
          <LiveSessionWidget
            session={latestSession}
            isLoading={isLoadingSessions}
            error={sessionsError}
            onRetry={() => fetchRecentSessions(8)}
          />
          <KpiWidget
            label="Partite (mese)"
            value={isLoadingStats ? '…' : monthlyPlays}
            badge={
              playsChange !== null && playsChange !== undefined && playsChange !== 0
                ? `${playsChange > 0 ? '+' : ''}${playsChange}%`
                : undefined
            }
            badgePositive={(playsChange ?? 0) > 0}
            accentColor={C.game}
            colSpan={4}
            tabletColSpan={6}
            rowSpan={2}
            href="/sessions"
          />

          {/* Row 3-6: Library (6×4) + Ore KPI (3×2) + Giochi KPI (3×2) */}
          <LibraryWidget
            games={games}
            totalCount={totalGamesCount || totalGames}
            isLoading={isLoadingGames}
            error={gamesError}
            onRetry={fetchGames}
          />
          <KpiWidget
            label="Ore sett."
            value={isLoadingStats ? '…' : weeklyHours}
            sub="questa settimana"
            accentColor={C.session}
            colSpan={3}
            rowSpan={2}
          />
          <KpiWidget
            label="Giochi in lib."
            value={isLoadingStats ? '…' : totalGames}
            accentColor={C.event}
            colSpan={3}
            rowSpan={2}
            href="/library"
          />

          {/* Row 5-8: Chat AI (6×4) */}
          <ChatPreviewWidget />

          {/* Row 7-9: Leaderboard (6×3) */}
          <LeaderboardWidget sessions={recentSessions} />

          {/* Row 9-10: Trending (6×2) */}
          <TrendingWidget
            games={trendingGames}
            isLoading={isLoadingTrending}
            error={trendingError}
            onRetry={() => fetchTrendingGames(6)}
          />
        </div>
      </div>
    </div>
  );
}
