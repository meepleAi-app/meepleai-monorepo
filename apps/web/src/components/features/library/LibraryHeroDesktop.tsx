/**
 * LibraryHeroDesktop — Wave B.3 v2 component (Issue #574).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx` (LibraryHero).
 * Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md §3.2
 *
 * Pure component (mirror Wave B.2 AgentsHero):
 *   labels and stats injected via props — no `useTranslation` import. The
 *   orchestrator (LibraryHub) owns i18n resolution and passes resolved
 *   strings down. This keeps the component renderable without IntlProvider in
 *   tests and storybook.
 *
 * Differs from AgentsHero:
 *   - No `eyebrow` line (library hero is the primary surface, not a sub-section).
 *   - 4 stats reflect library-specific aggregates with explicit `key` discriminator
 *     (`totalGames | kbReady | wishlist | loaned`) — derived client-side via
 *     `lib/library/library-filters` from the entries collection.
 *   - Primary CTA "Aggiungi gioco" (vs Agents "Crea agente").
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

/**
 * Stat key discriminator for the hero stat chips. The 4 original game-only keys
 * (`totalGames|kbReady|wishlist|loaned`) are preserved for backward compatibility
 * with any code that still surfaces the legacy stats; Phase 2a (#1605) added the
 * hybrid hub stat keys (`agents|docs|chats`) — `LibraryHub` now passes those
 * alongside `totalGames` for the 4 cross-entity stat chips.
 */
export type LibraryHeroStatKey =
  | 'totalGames'
  | 'kbReady'
  | 'wishlist'
  | 'loaned'
  | 'agents'
  | 'docs'
  | 'chats';

export interface LibraryHeroDesktopLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly ctaAdd: string;
}

export interface LibraryHeroStat {
  readonly key: LibraryHeroStatKey;
  readonly label: string;
  readonly value: number;
}

export interface LibraryHeroDesktopProps {
  readonly labels: LibraryHeroDesktopLabels;
  readonly stats: readonly LibraryHeroStat[];
  readonly compact?: boolean;
  readonly onAddGame?: () => void;
  readonly className?: string;
}

export function LibraryHeroDesktop({
  labels,
  stats,
  compact = false,
  onAddGame,
  className,
}: LibraryHeroDesktopProps): ReactElement {
  return (
    <section
      data-slot="library-hero-desktop"
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
        {stats.map(stat => (
          <div
            key={stat.key}
            data-slot="library-hero-stat"
            data-stat-key={stat.key}
            className="flex flex-col gap-1 rounded-xl border border-border/60 bg-muted/30 px-4 py-3"
          >
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">{stat.label}</dt>
            <dd
              data-slot="library-hero-stat-value"
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
