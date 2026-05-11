/**
 * PlayerStatsGrid — Wave 3 /players/[id] v2 component (Task 2).
 *
 * Mapped from `admin-mockups/design_files/sp4-player-detail.jsx` (KpiCard grid
 * in OverviewTab: Partite · Vittorie · Win rate · Streak/Achievement).
 *
 * Pure component: all i18n strings injected via `labels`. No hooks.
 *
 * Renders a responsive 2-col (mobile) / 4-col (sm+) grid of KPI tiles.
 * Visual hierarchy: large number + label below.
 *
 * Win rate rendered as integer percentage (culture-independent — no decimal
 * separator). Spec deviation flagged in mockup: "win rate 73% no decimali".
 *
 * WCAG:
 *   - Each tile has aria-hidden emoji icon (decorative).
 *   - No interactive elements — display only.
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface PlayerStatsGridLabels {
  readonly sessions: string;
  readonly wins: string;
  readonly winRate: string;
  readonly achievements: string;
}

export interface PlayerStatsGridProps {
  readonly totalSessions: number;
  readonly totalWins: number;
  /** Win rate as 0–1 decimal. Rendered as integer percentage (culture-independent). */
  readonly winRate: number;
  readonly achievementCount: number;
  readonly labels: PlayerStatsGridLabels;
  readonly className?: string;
}

interface KpiTileProps {
  readonly icon: string;
  readonly value: string;
  readonly label: string;
  readonly accentClass: string;
  readonly iconBgClass: string;
}

function KpiTile({ icon, value, label, accentClass, iconBgClass }: KpiTileProps): ReactElement {
  return (
    <div
      data-slot="player-detail-stats-tile"
      className={clsx('flex flex-col gap-2 rounded-2xl border border-border bg-card p-4 shadow-sm')}
    >
      {/* Icon chip */}
      <div
        aria-hidden="true"
        className={clsx('flex h-8 w-8 items-center justify-center rounded-md text-sm', iconBgClass)}
      >
        {icon}
      </div>
      {/* Value */}
      <div
        className={clsx(
          'font-display text-3xl font-extrabold leading-none tabular-nums tracking-tight',
          accentClass
        )}
      >
        {value}
      </div>
      {/* Label */}
      <div className="font-mono text-[10px] font-extrabold uppercase tracking-widest text-muted-foreground">
        {label}
      </div>
    </div>
  );
}

export function PlayerStatsGrid({
  totalSessions,
  totalWins,
  winRate,
  achievementCount,
  labels,
  className,
}: PlayerStatsGridProps): ReactElement {
  // Culture-independent integer percentage (mockup spec: no decimals, no comma)
  const winRatePct = `${Math.round(winRate * 100)}%`;

  return (
    <div
      data-slot="player-detail-stats-grid"
      className={clsx('grid grid-cols-2 gap-3 sm:grid-cols-4', className)}
    >
      <KpiTile
        icon="🎯"
        value={String(totalSessions)}
        label={labels.sessions}
        accentClass="text-foreground"
        iconBgClass="bg-sky-500/14 text-sky-700 dark:text-sky-300"
      />
      <KpiTile
        icon="🏆"
        value={String(totalWins)}
        label={labels.wins}
        accentClass="text-amber-700 dark:text-amber-400"
        iconBgClass="bg-amber-500/14 text-amber-700 dark:text-amber-400"
      />
      <KpiTile
        icon="📈"
        value={winRatePct}
        label={labels.winRate}
        accentClass="text-violet-700 dark:text-violet-300"
        iconBgClass="bg-violet-500/14 text-violet-700 dark:text-violet-300"
      />
      <KpiTile
        icon="🎖️"
        value={String(achievementCount)}
        label={labels.achievements}
        accentClass="text-emerald-700 dark:text-emerald-400"
        iconBgClass="bg-emerald-500/14 text-emerald-700 dark:text-emerald-400"
      />
    </div>
  );
}
