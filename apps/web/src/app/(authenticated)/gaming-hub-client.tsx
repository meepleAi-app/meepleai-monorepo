/**
 * Gaming Hub Client - Issue #4584
 * Epic #4575: Gaming Hub Dashboard - Phase 2
 *
 * Main dashboard for authenticated users with gaming focus
 */

'use client';

import { useEffect } from 'react';
import { Layout } from '@/components/layout';
import { Card } from '@/components/ui/card';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { useAuthUser } from '@/hooks/useAuthUser';
import {
  QuickStats,
  RecentSessions,
  GameCollectionGrid,
  FilterBar,
  EmptyState,
} from '@/components/dashboard-v2';

export function GamingHubClient() {
  const { user } = useAuthUser();

  const {
    stats,
    recentSessions,
    games,
    filters,
    isLoadingStats,
    isLoadingSessions,
    isLoadingGames,
    fetchStats,
    fetchRecentSessions,
    fetchGames,
    updateFilters,
  } = useDashboardStore();

  // Fetch data on mount
  useEffect(() => {
    fetchStats();
    fetchRecentSessions(3);
    fetchGames();
  }, [fetchStats, fetchRecentSessions, fetchGames]);

  // Calculate monthly change display
  const getChangeText = (change: number): string => {
    if (change === 0) return '';
    const sign = change > 0 ? '+' : '';
    return `${sign}${change}%`;
  };

  return (
    <Layout showActionBar>
      <div className="container mx-auto py-8 space-y-8">
        {/* Welcome Banner */}
        <Card className="bg-amber-100/50 backdrop-blur-md border border-amber-200 p-6">
          <h1 className="text-2xl font-quicksand font-bold text-amber-900">
            Benvenuto, {user?.displayName || 'Giocatore'}! 👋
          </h1>
          {stats && stats.monthlyPlays > 0 && (
            <p className="text-amber-900 font-nunito mt-2">
              Hai giocato {stats.monthlyPlays} partite questo mese
              {stats.monthlyPlaysChange !== 0 && (
                <span
                  className={
                    stats.monthlyPlaysChange > 0 ? 'text-green-700' : 'text-red-700'
                  }
                >
                  {' '}
                  ({getChangeText(stats.monthlyPlaysChange)} rispetto al mese scorso)
                </span>
              )}
            </p>
          )}
        </Card>

        {/* Quick Stats */}
        <section>
          <h2 className="text-xl font-quicksand font-semibold mb-4">📊 Panoramica</h2>
          <QuickStats stats={stats} isLoading={isLoadingStats} />
        </section>

        {/* Recent Sessions */}
        <section>
          <RecentSessions sessions={recentSessions} isLoading={isLoadingSessions} />
        </section>

        {/* Game Collection */}
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-xl font-quicksand font-semibold">📚 I Miei Giochi</h2>
            <span className="text-sm text-muted-foreground font-nunito">
              {stats?.totalGames || 0} giochi totali
            </span>
          </div>

          <FilterBar
            categories={['all', 'strategy', 'family', 'party', 'solo', 'cooperative']}
            currentCategory={filters.category}
            currentSort={filters.sort}
            onCategoryChange={(cat) => updateFilters({ category: cat })}
            onSortChange={(sort) =>
              updateFilters({
                sort: sort as 'alphabetical' | 'playCount',
              })
            }
          />

          <GameCollectionGrid games={games} isLoading={isLoadingGames} />
        </section>

        {/* Upcoming Games - Empty State */}
        <section>
          <h2 className="text-xl font-quicksand font-semibold mb-4">
            📅 Prossime Partite
          </h2>
          <EmptyState variant="no-upcoming" />
        </section>
      </div>
    </Layout>
  );
}
