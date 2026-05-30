'use client';

/**
 * StatisticsView — Play Records statistics (Task 5 reskin)
 *
 * Reusable stats body rendered both by the standalone `/play-records/stats`
 * route (legacy, redirected) AND inline by `/play-records?tab=stats` (the
 * canonical entry per route-consolidation #5039 — next.config redirects the
 * standalone path to the tab).
 *
 * - StatsHero: 4-col KPI (Partite/Giochi/Win rate/Preferito)
 * - MostPlayedBar: top 5 giochi, barre proporzionali
 * - WinByGameBar: win-rate per gioco, sorted descending
 * - Loading/Error/Empty states · Responsive 1-col mobile / 2-col desktop
 */

import { useRouter } from 'next/navigation';

import { MostPlayedBar } from '@/components/play-records/stats/MostPlayedBar';
import { StatsHero } from '@/components/play-records/stats/StatsHero';
import { WinByGameBar } from '@/components/play-records/stats/WinByGameBar';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { useTranslation } from '@/hooks/useTranslation';
import { usePlayerStatistics } from '@/lib/domain-hooks/usePlayRecords';

export function StatisticsView() {
  const router = useRouter();
  const { t } = useTranslation();
  const { data: stats, isLoading, error } = usePlayerStatistics();

  return (
    <div className="flex flex-col min-h-full bg-background" data-testid="stats-page">
      <MobileHeader
        title={t('playRecords.stats.headerTitle')}
        onBack={() => router.push('/play-records')}
      />

      {/* Loading State */}
      {isLoading && (
        <div className="flex-1 px-4 pt-6 pb-12 flex flex-col gap-6" data-testid="stats-loading">
          {/* Hero skeleton */}
          <div className="h-40 animate-pulse rounded-lg bg-muted" />

          {/* Section skeletons */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 md:gap-6" data-testid="stats-grid">
            <div
              className="h-56 animate-pulse rounded-lg bg-muted"
              data-testid="section-skeleton"
            />
            <div
              className="h-56 animate-pulse rounded-lg bg-muted"
              data-testid="section-skeleton"
            />
          </div>
        </div>
      )}

      {/* Error State */}
      {!isLoading && error && (
        <>
          {stats && <StatsHero stats={stats} />}
          <div className="flex-1 px-4 pt-6 pb-12">
            <div
              className="rounded-lg border border-danger/30 bg-danger/10 px-6 py-6 text-center"
              data-testid="stats-error"
            >
              <div className="text-4xl mb-3" aria-hidden="true">
                ⚠️
              </div>
              <h3 className="font-bold text-foreground">{t('playRecords.stats.error.title')}</h3>
              <p className="mt-2 text-sm text-muted-foreground">
                {t('playRecords.stats.error.description')}
              </p>
            </div>
          </div>
        </>
      )}

      {/* Content */}
      {!isLoading && !error && stats && (
        <>
          <StatsHero stats={stats} />
          <div
            className="flex-1 px-4 pt-6 pb-12 grid grid-cols-1 md:grid-cols-2 gap-4 md:gap-6"
            data-testid="stats-grid"
          >
            <MostPlayedBar stats={stats} />
            <WinByGameBar stats={stats} />
          </div>
        </>
      )}
    </div>
  );
}
