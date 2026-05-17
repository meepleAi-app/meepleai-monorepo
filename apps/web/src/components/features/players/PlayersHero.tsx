/**
 * PlayersHero — Wave 4 D1 v2 component (Issue #682).
 *
 * Mapped from `admin-mockups/design_files/sp4-players-index.jsx` (PlayersHero).
 *
 * Pure component (mirror Wave B.2 AgentsHero): all i18n strings injected via
 * `labels` — no `useTranslation` import. The orchestrator (PlayersView) owns
 * i18n resolution and passes resolved strings down.
 *
 * Displays:
 *   - title + subtitle from labels
 *   - 2 KPI tiles: totalSessions + distinctGames
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface PlayersHeroLabels {
  readonly title: string;
  readonly subtitle: string;
  /** Label for the totalSessions KPI tile */
  readonly totalPlays: string;
  /** Label for the distinctGames KPI tile */
  readonly distinctGames: string;
}

export interface PlayersHeroProps {
  readonly totalSessions: number;
  readonly distinctGames: number;
  readonly labels: PlayersHeroLabels;
  readonly className?: string;
}

export function PlayersHero({
  totalSessions,
  distinctGames,
  labels,
  className,
}: PlayersHeroProps): ReactElement {
  return (
    <section
      data-slot="players-hero"
      className={clsx(
        'relative overflow-hidden rounded-2xl border border-border bg-card px-6 py-8 shadow-sm',
        'sm:px-10 sm:py-12',
        className
      )}
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <h1 className="text-3xl font-semibold tracking-tight text-foreground sm:text-4xl">
            {labels.title}
          </h1>
          <p className="max-w-prose text-sm text-muted-foreground sm:text-base">
            {labels.subtitle}
          </p>
        </div>

        <dl className="mt-2 grid grid-cols-2 gap-3 sm:mt-4 sm:gap-4">
          <div
            data-slot="players-hero-stat"
            className="flex flex-col gap-1 rounded-xl border border-border/60 bg-muted/30 px-4 py-3"
          >
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">
              {labels.totalPlays}
            </dt>
            <dd
              data-slot="players-hero-stat-value"
              className="text-2xl font-semibold tabular-nums text-foreground"
            >
              {totalSessions}
            </dd>
          </div>

          <div
            data-slot="players-hero-stat"
            className="flex flex-col gap-1 rounded-xl border border-border/60 bg-muted/30 px-4 py-3"
          >
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">
              {labels.distinctGames}
            </dt>
            <dd
              data-slot="players-hero-stat-value"
              className="text-2xl font-semibold tabular-nums text-foreground"
            >
              {distinctGames}
            </dd>
          </div>
        </dl>
      </div>
    </section>
  );
}
