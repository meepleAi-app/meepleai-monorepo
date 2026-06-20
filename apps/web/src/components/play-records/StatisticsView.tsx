'use client';

/**
 * StatisticsView — Play Records statistics (Task 5 reskin · #2438 trend/range)
 *
 * Reusable stats body rendered both by the standalone `/play-records/stats`
 * route (legacy, redirected) AND inline by `/play-records?tab=stats` (the
 * canonical entry per route-consolidation #5039 — next.config redirects the
 * standalone path to the tab).
 *
 * - Date-range preset filter (Tutto · 30g · 90g · 12 mesi) → narrows stats (#2438)
 * - StatsHero: 4-col KPI (Partite/Giochi/Win rate/Preferito)
 * - MostPlayedBar: top 5 giochi, barre proporzionali
 * - WinByGameBar: win-rate per gioco, sorted descending
 * - TrendChart: win-rate trend (recharts area) — full width below grid (#2438)
 * - Loading/Error/Empty states · Responsive 1-col mobile / 2-col desktop
 */

import { useState } from 'react';

import { useRouter } from 'next/navigation';

import { MostPlayedBar } from '@/components/play-records/stats/MostPlayedBar';
import { StatsHero } from '@/components/play-records/stats/StatsHero';
import { TrendChart } from '@/components/play-records/stats/TrendChart';
import { WinByGameBar } from '@/components/play-records/stats/WinByGameBar';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { useTranslation } from '@/hooks/useTranslation';
import { usePlayerStatistics, type StatsRange } from '@/lib/domain-hooks/usePlayRecords';

type RangePreset = 'all' | '30d' | '90d' | '12m';

const PRESETS: RangePreset[] = ['all', '30d', '90d', '12m'];

function rangeForPreset(preset: RangePreset): StatsRange | undefined {
  if (preset === 'all') return undefined;
  const now = new Date();
  const from = new Date(now);
  if (preset === '30d') from.setDate(now.getDate() - 30);
  else if (preset === '90d') from.setDate(now.getDate() - 90);
  else if (preset === '12m') from.setMonth(now.getMonth() - 12);
  return { startDate: from.toISOString() };
}

export function StatisticsView() {
  const router = useRouter();
  const { t } = useTranslation();
  const [preset, setPreset] = useState<RangePreset>('all');
  const { data: stats, isLoading, error } = usePlayerStatistics(rangeForPreset(preset));

  return (
    <div className="flex flex-col min-h-full bg-background" data-testid="stats-page">
      <MobileHeader
        title={t('playRecords.stats.headerTitle')}
        onBack={() => router.push('/play-records')}
      />

      {/* Date-range preset filter (#2438) */}
      <div className="px-4 pt-4" data-testid="stats-range-filter">
        <div
          className="inline-flex rounded-lg border border-border bg-card p-1"
          role="group"
          aria-label={t('playRecords.stats.range.label')}
        >
          {PRESETS.map(p => (
            <button
              key={p}
              type="button"
              onClick={() => setPreset(p)}
              aria-pressed={preset === p}
              data-testid={`range-${p}`}
              className={
                preset === p
                  ? 'rounded-md bg-entity-game px-3 py-1.5 text-[11px] font-bold text-white'
                  : 'rounded-md px-3 py-1.5 text-[11px] font-bold text-muted-foreground hover:text-foreground'
              }
            >
              {t(`playRecords.stats.range.${p}`)}
            </button>
          ))}
        </div>
      </div>

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
          <div className="flex-1 px-4 pt-6 pb-12 flex flex-col gap-4 md:gap-6">
            <div
              className="grid grid-cols-1 md:grid-cols-2 gap-4 md:gap-6"
              data-testid="stats-grid"
            >
              <MostPlayedBar stats={stats} />
              <WinByGameBar stats={stats} />
            </div>
            <TrendChart stats={stats} />
          </div>
        </>
      )}
    </div>
  );
}
