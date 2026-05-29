/**
 * RecordsHero — Play Records Hero Section
 *
 * Display hero with 4 KPI stats (games, wins, unique games, total hours).
 * Stats are loaded via usePlayerStatistics with deferred rendering.
 * Issue #1488: Play Records Index Reskin (Task 1)
 *
 * AC-1.1: RecordsHero mostra 4 KPI inline (partite, vittorie, giochi, ore totali)
 * consumendo usePlayerStatistics (deferred load — non blocca render del hero).
 */

'use client';

import Link from 'next/link';
import { useTranslations } from 'next-intl';

import { usePlayerStatistics } from '@/lib/domain-hooks/usePlayRecords';
import { cn } from '@/lib/utils';

export interface RecordsHeroProps {
  isLoading?: boolean;
}

export function RecordsHero({ isLoading = false }: RecordsHeroProps) {
  const t = useTranslations('playRecords.index');
  const { data: stats } = usePlayerStatistics();

  const statsList = [
    {
      icon: '🎯',
      label: t('hero.stats.games'),
      value: stats?.totalSessions ?? 0,
      entity: 'session',
    },
    {
      icon: '🏆',
      label: t('hero.stats.wins'),
      value: stats?.totalWins ?? 0,
      entity: 'toolkit',
    },
    {
      icon: '🎲',
      label: t('hero.stats.count'),
      value: stats?.mostPlayedGames?.length ?? 0,
      entity: 'game',
    },
    {
      icon: '⏱',
      label: t('hero.stats.hours'),
      value: stats?.totalDurationMinutes ? Math.round(stats.totalDurationMinutes / 60) : 0,
      entity: 'event',
      suffix: 'h',
    },
  ];

  return (
    <div
      className="relative overflow-hidden border-b border-border-light bg-gradient-to-br from-transparent via-transparent to-transparent"
      style={{
        background: `radial-gradient(circle at 0% 0%, var(--c-session) 0%, transparent 60%), var(--bg)`,
      }}
    >
      {/* Decorative orb */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute -right-28 -top-32 h-56 w-56 rounded-full"
        style={{
          background: 'radial-gradient(circle, var(--c-session) 0%, transparent 70%)',
          opacity: 0.08,
        }}
      />

      {/* Hero content */}
      <div className="relative z-10 flex flex-col gap-6 px-4 py-6 sm:px-8 sm:py-8 lg:px-12 lg:py-10">
        {/* Header section */}
        <div className="flex flex-col gap-1 lg:gap-3">
          {/* Label badge */}
          <div className="inline-flex w-fit items-center gap-2 rounded-full border border-entity-session/25 bg-entity-session/12 px-3 py-1 text-xs font-extrabold uppercase tracking-wider text-entity-session">
            <span aria-hidden="true">🎯</span>
            {t('hero.label')}
          </div>

          {/* Title */}
          <h1 className="font-display text-2xl font-extrabold tracking-tight text-foreground sm:text-3xl lg:text-4xl">
            {t('hero.title')}
          </h1>

          {/* Subtitle */}
          <p className="font-body text-sm font-medium text-muted-foreground sm:text-base">
            {t('hero.subtitle')}
          </p>
        </div>

        {/* Stats + CTA grid */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between sm:gap-6 lg:gap-8">
          {/* Stats row */}
          <div className="flex flex-wrap gap-2 sm:gap-3">
            {isLoading ? (
              // Loading skeleton: 4 placeholder boxes
              <>
                {[...Array(4)].map((_, i) => (
                  <div
                    key={i}
                    className="h-10 w-20 animate-pulse rounded-md bg-muted sm:h-11 sm:w-24"
                  />
                ))}
              </>
            ) : (
              // Stats cards
              <>
                {statsList.map(stat => (
                  <div
                    key={stat.label}
                    className="inline-flex items-center gap-2 rounded-md border border-border bg-card px-3 py-2 sm:px-4 sm:py-2.5"
                  >
                    <span aria-hidden="true" className="text-base sm:text-lg">
                      {stat.icon}
                    </span>
                    <span
                      className={cn(
                        'font-mono text-sm font-extrabold tracking-tabular',
                        `text-entity-${stat.entity}`
                      )}
                    >
                      {stat.value}
                      {stat.suffix}
                    </span>
                    <span className="font-display text-xs font-bold text-muted-foreground sm:text-sm">
                      {stat.label}
                    </span>
                  </div>
                ))}
              </>
            )}
          </div>

          {/* CTA button */}
          <Link
            href="/play-records/new"
            className="inline-flex items-center justify-center gap-2 rounded-md bg-entity-session px-4 py-2 font-display text-sm font-extrabold text-white shadow-lg shadow-entity-session/40 transition-all hover:bg-entity-session/90 sm:px-5 sm:py-2.5"
          >
            <span aria-hidden="true">+</span>
            {t('hero.cta')}
          </Link>
        </div>
      </div>
    </div>
  );
}
