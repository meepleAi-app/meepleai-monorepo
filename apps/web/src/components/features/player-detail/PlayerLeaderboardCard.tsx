/**
 * PlayerLeaderboardCard — Wave 3 /players/[id] v2 component (Task 2).
 *
 * Mapped from `admin-mockups/design_files/sp4-player-detail.jsx` (leaderboard
 * rank section in the Overview tab sidebar / overview grid).
 *
 * Pure component: all i18n strings injected via `labels`. No hooks.
 *
 * Renders a card with:
 *   - Title + trophy icon
 *   - Rank display (e.g. "Rank #3") when rank is non-null
 *   - noRank fallback text when rank is null
 *
 * WCAG:
 *   - Rank element has aria-label for screen-reader clarity.
 *   - Trophy emoji aria-hidden (decorative).
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface PlayerLeaderboardCardLabels {
  readonly title: string;
  /** Template: "Rank #{rank}" — substitute {rank} with numeric value */
  readonly rank: string;
  /** Template: "Leaderboard position: {rank}" — substitute {rank} with numeric value */
  readonly rankAriaLabel: string;
  readonly noRank: string;
}

export interface PlayerLeaderboardCardProps {
  readonly rank: number | null;
  readonly labels: PlayerLeaderboardCardLabels;
  readonly className?: string;
}

/** Substitutes {rank} in a template. */
function interpolateRank(template: string, rank: number): string {
  return template.replace('{rank}', String(rank));
}

export function PlayerLeaderboardCard({
  rank,
  labels,
  className,
}: PlayerLeaderboardCardProps): ReactElement {
  return (
    <div
      data-slot="player-detail-leaderboard"
      className={clsx(
        'flex flex-col gap-3 rounded-2xl border border-border bg-card p-4 shadow-sm',
        className
      )}
    >
      {/* Header */}
      <div className="flex items-center gap-2">
        <span
          aria-hidden="true"
          className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-amber-500/14 text-sm text-amber-700 dark:text-amber-400"
        >
          🏆
        </span>
        <h3 className="font-display text-[15px] font-extrabold text-foreground">{labels.title}</h3>
      </div>

      {/* Rank or no-rank */}
      {rank !== null ? (
        <div
          data-slot="player-detail-leaderboard-rank"
          aria-label={interpolateRank(labels.rankAriaLabel, rank)}
          className="flex items-baseline gap-1"
        >
          <span className="font-display text-4xl font-extrabold leading-none tabular-nums tracking-tight text-amber-700 dark:text-amber-400">
            #{rank}
          </span>
          <span className="font-mono text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
            {interpolateRank(labels.rank, rank)}
          </span>
        </div>
      ) : (
        <p data-slot="player-detail-leaderboard-no-rank" className="text-sm text-muted-foreground">
          {labels.noRank}
        </p>
      )}
    </div>
  );
}
