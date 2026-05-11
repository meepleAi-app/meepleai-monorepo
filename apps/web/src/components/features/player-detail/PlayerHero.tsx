/**
 * PlayerHero — Wave 3 /players/[id] v2 component (Task 2).
 *
 * Mapped from `admin-mockups/design_files/sp4-player-detail.jsx` (PlayerHero).
 *
 * Pure component: all i18n strings injected via `labels`. No hooks, no
 * useTranslation, no useQuery — orchestrator passes resolved data.
 *
 * Renders:
 *   - Back button (optional — absent when onBack is not provided)
 *   - Player avatar initials badge
 *   - displayName as h1 heading
 *   - 3 KPI inline stat chips (totalSessions / totalWins / winRate)
 *
 * WCAG:
 *   - Back button aria-label from labels.backAriaLabel
 *   - Player badge aria-hidden (decorative)
 *   - 700-shade purple on white for accent (>= WCAG AA)
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface PlayerHeroLabels {
  readonly back: string;
  readonly backAriaLabel: string;
  /** Template: "{count} sessions" — substitute {count} with totalSessions */
  readonly totalSessions: string;
  /** Template: "{count} wins" — substitute {count} with totalWins */
  readonly totalWins: string;
  /** Template: "{rate}% win rate" — substitute {rate} with winRate pct integer */
  readonly winRate: string;
}

export interface PlayerHeroProps {
  readonly displayName: string;
  readonly totalSessions: number;
  readonly totalWins: number;
  /** Win rate as 0–1 decimal. Rendered as integer percentage (culture-independent). */
  readonly winRate: number;
  readonly onBack?: () => void;
  readonly labels: PlayerHeroLabels;
  readonly className?: string;
}

/** Substitutes {count} in a label template. */
function interpolateCount(template: string, count: number): string {
  return template.replace('{count}', String(count));
}

/** Substitutes {rate} in a win-rate label template. Returns integer percentage. */
function interpolateRate(template: string, rate: number): string {
  const pct = Math.round(rate * 100);
  return template.replace('{rate}', String(pct));
}

export function PlayerHero({
  displayName,
  totalSessions,
  totalWins,
  winRate,
  onBack,
  labels,
  className,
}: PlayerHeroProps): ReactElement {
  return (
    <section
      data-slot="player-detail-hero"
      className={clsx(
        'relative overflow-hidden bg-gradient-to-br from-violet-500/20 via-purple-500/10 to-background',
        'border-b border-border',
        className
      )}
    >
      {/* Back button row */}
      {onBack ? (
        <div className="px-4 pb-0 pt-4 sm:px-6">
          <button
            type="button"
            aria-label={labels.backAriaLabel}
            onClick={onBack}
            data-slot="player-detail-hero-back"
            className={clsx(
              'inline-flex items-center gap-1.5 rounded-full px-3 py-1.5',
              'border border-border bg-background/80 text-sm font-medium text-foreground',
              'transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
            )}
          >
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <polyline points="15 18 9 12 15 6" />
            </svg>
            <span>{labels.back}</span>
          </button>
        </div>
      ) : null}

      {/* Hero body */}
      <div className="flex flex-col items-center gap-4 px-4 py-6 text-center sm:flex-row sm:items-end sm:gap-6 sm:px-8 sm:pb-6 sm:pt-8 sm:text-left">
        {/* Avatar */}
        <div
          aria-hidden="true"
          data-slot="player-detail-hero-avatar"
          className={clsx(
            'flex h-24 w-24 flex-shrink-0 items-center justify-center rounded-full',
            'bg-gradient-to-br from-violet-500 to-purple-700 shadow-lg shadow-violet-500/30',
            'font-display text-3xl font-extrabold text-white',
            'sm:h-28 sm:w-28 sm:text-4xl'
          )}
        >
          {displayName
            .split(' ')
            .slice(0, 2)
            .map(w => w[0]?.toUpperCase() ?? '')
            .join('')}
        </div>

        {/* Identity + KPIs */}
        <div className="flex flex-1 flex-col items-center gap-3 sm:items-start">
          {/* Player type badge */}
          <span
            aria-hidden="true"
            className={clsx(
              'inline-flex items-center gap-1.5 rounded-full px-2.5 py-1',
              'bg-violet-500/14 font-mono text-[9px] font-extrabold uppercase tracking-widest text-violet-700',
              'dark:text-violet-300'
            )}
          >
            <span>👤</span>
            <span>Player</span>
          </span>

          {/* Name */}
          <h1
            className={clsx(
              'font-display text-3xl font-extrabold leading-none tracking-tight text-foreground',
              'sm:text-4xl'
            )}
          >
            {displayName}
          </h1>

          {/* KPI chips */}
          <div className="flex flex-wrap items-center justify-center gap-3 sm:justify-start">
            <span
              data-slot="player-detail-hero-kpi-sessions"
              className="font-mono text-sm font-bold tabular-nums text-muted-foreground"
            >
              {interpolateCount(labels.totalSessions, totalSessions)}
            </span>
            <span aria-hidden="true" className="text-border">
              ·
            </span>
            <span
              data-slot="player-detail-hero-kpi-wins"
              className="font-mono text-sm font-bold tabular-nums text-violet-700 dark:text-violet-300"
            >
              {interpolateCount(labels.totalWins, totalWins)}
            </span>
            <span aria-hidden="true" className="text-border">
              ·
            </span>
            <span
              data-slot="player-detail-hero-kpi-winrate"
              className="font-mono text-sm font-bold tabular-nums text-foreground"
            >
              {interpolateRate(labels.winRate, winRate)}
            </span>
          </div>
        </div>
      </div>
    </section>
  );
}
