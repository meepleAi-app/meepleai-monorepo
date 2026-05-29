'use client';

/**
 * Play Records Statistics Page — Task 5 reskin
 *
 * - StatsHero: 4-col KPI (Partite/Giochi/Win rate/Preferito)
 * - MostPlayedBar: top 5 giochi, barre proporzionali
 * - WinByGameBar: win-rate per gioco, sorted descending
 * - Loading/Error/Empty states
 * - Responsive: mobile 1-col stacked, desktop 2-col grid
 * - AC-5: all acceptance criteria implemented
 */

import { useRouter } from 'next/navigation';

import { MostPlayedBar } from '@/components/play-records/stats/MostPlayedBar';
import { StatsHero } from '@/components/play-records/stats/StatsHero';
import { WinByGameBar } from '@/components/play-records/stats/WinByGameBar';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { usePlayerStatistics } from '@/lib/domain-hooks/usePlayRecords';

export default function StatisticsPage() {
  const router = useRouter();
  const { data: stats, isLoading, error } = usePlayerStatistics();

  return (
    <div className="flex flex-col min-h-full bg-background" data-testid="stats-page">
      <MobileHeader title="Statistiche" onBack={() => router.back()} />

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
              <h3 className="font-bold text-foreground">Impossibile caricare le statistiche</h3>
              <p className="mt-2 text-sm text-muted-foreground">
                Si è verificato un errore di rete. Verifica la connessione e riprova.
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
