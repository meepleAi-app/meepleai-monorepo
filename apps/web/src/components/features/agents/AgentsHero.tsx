/**
 * AgentsHero — Wave B.2 v2 component (Issue #634).
 *
 * Mapped from `admin-mockups/design_files/sp4-agents-index.jsx` (AgentsHero).
 * Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-2-agents.md §3.2
 *
 * Pure component (mirror Wave B.1 GamesHero):
 *   labels and stats injected via props — no `useTranslation` import. The
 *   orchestrator (AgentsLibraryView) owns i18n resolution and passes resolved
 *   strings down. This keeps the component renderable without IntlProvider in
 *   tests and storybook.
 *
 * Differs from GamesHero:
 *   - Adds `eyebrow` line above the title (SP4 hero pattern).
 *   - 4 stats reflect agent-specific aggregates: attivo, in-setup, archiviato,
 *     totalInvocations (derived client-side via `lib/agents/library-filters`).
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface AgentsHeroLabels {
  readonly eyebrow: string;
  readonly title: string;
  readonly subtitle: string;
  readonly ctaCreate: string;
}

export interface AgentsHeroStat {
  readonly label: string;
  readonly value: number;
}

export interface AgentsHeroProps {
  readonly labels: AgentsHeroLabels;
  readonly stats: readonly AgentsHeroStat[];
  readonly compact?: boolean;
  readonly onCreateAgent?: () => void;
  readonly className?: string;
}

export function AgentsHero({
  labels,
  stats,
  compact = false,
  onCreateAgent,
  className,
}: AgentsHeroProps): ReactElement {
  return (
    <section
      data-slot="agents-library-hero"
      className={clsx(
        'relative overflow-hidden rounded-2xl border border-border bg-card px-6 py-8 shadow-sm',
        compact ? 'sm:px-8 sm:py-10' : 'sm:px-10 sm:py-12',
        className
      )}
    >
      <div className="flex flex-col gap-6 sm:flex-row sm:items-end sm:justify-between">
        <div className="flex flex-col gap-2">
          <p
            data-slot="agents-hero-eyebrow"
            className="text-xs font-medium uppercase tracking-wider text-muted-foreground"
          >
            {labels.eyebrow}
          </p>
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
          onClick={onCreateAgent}
          className={clsx(
            'inline-flex h-10 items-center justify-center self-start rounded-full px-5 text-sm font-medium',
            'bg-primary text-primary-foreground shadow-sm transition-colors',
            'hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            'sm:self-auto'
          )}
        >
          {labels.ctaCreate}
        </button>
      </div>

      <dl className={clsx('mt-6 grid grid-cols-2 gap-3 sm:mt-8 sm:grid-cols-4 sm:gap-4')}>
        {stats.map((stat, index) => (
          <div
            key={`${stat.label}-${index}`}
            data-slot="agents-hero-stat"
            className="flex flex-col gap-1 rounded-xl border border-border/60 bg-muted/30 px-4 py-3"
          >
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">{stat.label}</dt>
            <dd
              data-slot="agents-hero-stat-value"
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
