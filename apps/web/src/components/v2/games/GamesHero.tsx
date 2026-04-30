/**
 * GamesHero — Wave B.1 v2 component (Issue #633).
 *
 * Mapped from `admin-mockups/design_files/sp4-games-index.jsx` (LibraryHero).
 * Spec: docs/superpowers/specs/2026-04-29-v2-migration-wave-b-1-games.md §3.2
 *
 * Pure component (mirror Wave A.4 `shared-game-detail/hero.tsx`):
 *   labels and stats injected via props — no `useTranslation` import. The
 *   orchestrator (GamesLibraryView) owns i18n resolution and passes resolved
 *   strings down. This keeps the component renderable without IntlProvider in
 *   tests and storybook.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface GamesHeroLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly ctaAdd: string;
}

export interface GamesHeroStat {
  readonly label: string;
  readonly value: number;
}

export interface GamesHeroProps {
  readonly labels: GamesHeroLabels;
  readonly stats: readonly GamesHeroStat[];
  readonly compact?: boolean;
  readonly onAddGame?: () => void;
  readonly className?: string;
}

export function GamesHero({
  labels,
  stats,
  compact = false,
  onAddGame,
  className,
}: GamesHeroProps): ReactElement {
  return (
    <section
      data-slot="games-library-hero"
      className={clsx(
        'relative overflow-hidden rounded-2xl border border-border bg-card px-6 py-8 shadow-sm',
        compact ? 'sm:px-8 sm:py-10' : 'sm:px-10 sm:py-12',
        className
      )}
    >
      <div className="flex flex-col gap-6 sm:flex-row sm:items-end sm:justify-between">
        <div className="flex flex-col gap-2">
          <h1 className="text-3xl font-semibold tracking-tight text-foreground sm:text-4xl">
            {labels.title}
          </h1>
          {compact ? null : (
            <p className="max-w-prose text-sm text-muted-foreground sm:text-base">
              {labels.subtitle}
            </p>
          )}
        </div>
        <button
          type="button"
          onClick={onAddGame}
          className={clsx(
            'inline-flex h-10 items-center justify-center self-start rounded-full px-5 text-sm font-medium',
            'bg-primary text-primary-foreground shadow-sm transition-colors',
            'hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            'sm:self-auto'
          )}
        >
          {labels.ctaAdd}
        </button>
      </div>

      <dl className={clsx('mt-6 grid grid-cols-2 gap-3 sm:mt-8 sm:grid-cols-4 sm:gap-4')}>
        {stats.map((stat, index) => (
          <div
            key={`${stat.label}-${index}`}
            data-slot="games-hero-stat"
            className="flex flex-col gap-1 rounded-xl border border-border/60 bg-muted/30 px-4 py-3"
          >
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">{stat.label}</dt>
            <dd
              data-slot="games-hero-stat-value"
              className="text-2xl font-semibold tabular-nums text-foreground"
            >
              {stat.value}
            </dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
