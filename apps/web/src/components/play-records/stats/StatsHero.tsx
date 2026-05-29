'use client';

import { useTranslations } from 'next-intl';

import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';
import { useSharedGames } from '@/lib/play-records/useSharedGames';
import { cn } from '@/lib/utils';

interface StatsHeroProps {
  stats: PlayerStatistics;
}

function KpiCard({
  label,
  value,
  unit,
  subLabel,
  icon,
  entity,
}: {
  label: string;
  value: string | number;
  unit?: string;
  subLabel?: string;
  icon: string;
  entity: 'session' | 'game' | 'toolkit' | 'player';
}) {
  const entityColors: Record<string, string> = {
    session: 'bg-entity-session/12 text-entity-session',
    game: 'bg-entity-game/12 text-entity-game',
    toolkit: 'bg-entity-toolkit/12 text-entity-toolkit',
    player: 'bg-entity-player/12 text-entity-player',
  };

  return (
    <article
      className={cn(
        'flex items-center gap-4 rounded-lg border border-border bg-card p-4',
        'md:gap-5 md:p-5'
      )}
      aria-label={`${label}: ${value}${unit || ''}`}
    >
      <div
        className={cn(
          'flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-md text-base',
          entityColors[entity]
        )}
        aria-hidden="true"
      >
        {icon}
      </div>
      <div className="min-w-0 flex-1">
        <div className="font-mono text-[9px] font-bold uppercase tracking-wider text-muted-foreground">
          {label}
        </div>
        <div className="mt-1 text-xl font-black text-foreground md:text-2xl">
          {value}
          {unit && <span className="ml-1 text-xs font-semibold text-muted-foreground">{unit}</span>}
        </div>
        {subLabel && (
          <div className="font-mono text-[8.5px] font-semibold text-muted-foreground">
            {subLabel}
          </div>
        )}
      </div>
    </article>
  );
}

export function StatsHero({ stats }: StatsHeroProps) {
  const t = useTranslations('playRecords.stats');

  // Get favorite game (mostPlayedGames[0])
  const favoriteGameId = stats.mostPlayedGames?.[0]?.gameId;
  const { data: sharedGamesMap } = useSharedGames(favoriteGameId ? [favoriteGameId] : []);

  const favoriteGame = favoriteGameId ? sharedGamesMap?.get(favoriteGameId) : null;

  // Calculate win rate
  const winRate =
    stats.totalSessions > 0 ? Math.round((stats.totalWins / stats.totalSessions) * 100) : 0;

  // Calculate hours from minutes (1 decimal place)
  const hoursPlayed = (stats.totalDurationMinutes || 0) / 60;
  const hoursFormatted = hoursPlayed.toFixed(1);

  // Count unique games
  const uniqueGames = stats.mostPlayedGames?.length || 0;

  const isEmpty = stats.totalSessions === 0;

  return (
    <header className="border-b border-border bg-gradient-to-r from-entity-session/8 via-entity-game/5 to-entity-toolkit/6 px-4 py-6 md:px-8 md:py-8">
      {/* Subtitle badge */}
      <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-entity-session/25 bg-entity-session/12 px-3 py-1.5">
        <span aria-hidden="true">🎯</span>
        <span className="font-mono text-[9px] font-bold uppercase tracking-wider text-entity-session">
          {t('badge.statistics')}
        </span>
      </div>

      {/* Title */}
      <h1 className="font-display text-2xl font-black tracking-tight text-foreground md:text-3xl">
        {t('title')} <span aria-hidden="true">📊</span>
      </h1>

      {/* Subtitle */}
      <p className="mt-2 max-w-2xl text-sm text-muted-foreground md:text-base">{t('subtitle')}</p>

      {/* KPI Grid */}
      <div className="mt-6 grid grid-cols-2 gap-3 md:grid-cols-4 md:gap-4">
        <KpiCard
          label={t('kpi.sessions')}
          value={isEmpty ? '—' : stats.totalSessions}
          subLabel={isEmpty ? '—' : `${hoursFormatted}h totali`}
          icon="🎯"
          entity="session"
        />
        <KpiCard
          label={t('kpi.uniqueGames')}
          value={isEmpty ? '—' : uniqueGames}
          subLabel={isEmpty ? '—' : undefined}
          icon="🎲"
          entity="game"
        />
        <KpiCard
          label={t('kpi.winRate')}
          value={isEmpty ? '—' : winRate}
          unit={isEmpty ? '' : '%'}
          subLabel={isEmpty ? '—' : `${stats.totalWins} ${t('kpi.winRateWins')}`}
          icon="🏆"
          entity="toolkit"
        />
        <KpiCard
          label={t('kpi.favorite')}
          value={(favoriteGame as { coverEmoji?: string } | null | undefined)?.coverEmoji || '—'}
          subLabel={favoriteGame?.title || '—'}
          icon="⭐"
          entity="player"
        />
      </div>
    </header>
  );
}
