/**
 * GamesEmptyState — Wave B.1 v2 component (Issue #633).
 *
 * Mapped from `admin-mockups/design_files/sp4-games-index.jsx` (EmptyLibrary +
 * ErrorState + LoadingState).
 * Spec: docs/superpowers/specs/2026-04-29-v2-migration-wave-b-1-games.md §3.2
 *
 * Pure component (mirror Wave A.4): all i18n strings injected via `labels`.
 * Discriminates 4 surface states via `kind`:
 *   - 'loading'        → 8 skeleton cards (4 mobile via `compact`)
 *   - 'empty'          → empty-library copy + "Aggiungi gioco" CTA → onAddGame
 *   - 'filtered-empty' → filtered copy + "Cancella filtri" CTA → onClearFilters
 *   - 'error'          → error copy + "Riprova" CTA → onRetry
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export type GamesEmptyKind = 'empty' | 'filtered-empty' | 'error' | 'loading';

export interface GamesEmptyStateCopy {
  readonly title: string;
  readonly subtitle: string;
  readonly cta: string;
}

export interface GamesEmptyStateLabels {
  readonly empty: GamesEmptyStateCopy;
  readonly filteredEmpty: GamesEmptyStateCopy;
  readonly error: GamesEmptyStateCopy;
}

export interface GamesEmptyStateProps {
  readonly kind: GamesEmptyKind;
  readonly labels: GamesEmptyStateLabels;
  readonly onAddGame?: () => void;
  readonly onClearFilters?: () => void;
  readonly onRetry?: () => void;
  readonly compact?: boolean;
  readonly className?: string;
}

const SKELETON_COUNT_DESKTOP = 8;
const SKELETON_COUNT_MOBILE = 4;

export function GamesEmptyState({
  kind,
  labels,
  onAddGame,
  onClearFilters,
  onRetry,
  compact = false,
  className,
}: GamesEmptyStateProps): ReactElement {
  if (kind === 'loading') {
    const count = compact ? SKELETON_COUNT_MOBILE : SKELETON_COUNT_DESKTOP;
    return (
      <div
        data-slot="games-empty-state"
        data-kind="loading"
        className={clsx(
          compact ? 'grid grid-cols-2 gap-3 px-4' : 'grid grid-cols-3 lg:grid-cols-4 gap-4 px-8',
          className
        )}
        aria-busy="true"
        aria-live="polite"
      >
        {Array.from({ length: count }, (_, index) => (
          <Skeleton
            key={index}
            data-slot="games-empty-skeleton"
            className="h-48 w-full rounded-2xl"
          />
        ))}
      </div>
    );
  }

  const copy =
    kind === 'empty'
      ? labels.empty
      : kind === 'filtered-empty'
        ? labels.filteredEmpty
        : labels.error;

  const onCtaClick =
    kind === 'empty' ? onAddGame : kind === 'filtered-empty' ? onClearFilters : onRetry;

  const icon = kind === 'empty' ? '🎲' : kind === 'filtered-empty' ? '🔎' : '⚠️';

  return (
    <div
      data-slot="games-empty-state"
      data-kind={kind}
      className={clsx(
        'flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border bg-muted/20 px-6 py-12 text-center',
        compact ? 'mx-4' : 'mx-8',
        className
      )}
      role={kind === 'error' ? 'alert' : 'status'}
    >
      <span className="text-4xl" aria-hidden="true">
        {icon}
      </span>
      <h2 className="text-lg font-semibold text-foreground sm:text-xl">{copy.title}</h2>
      <p className="max-w-md text-sm text-muted-foreground">{copy.subtitle}</p>
      <button
        type="button"
        onClick={onCtaClick}
        className={clsx(
          'mt-2 inline-flex h-10 items-center justify-center rounded-full px-5 text-sm font-medium',
          'bg-primary text-primary-foreground shadow-sm transition-colors',
          'hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
        )}
        data-slot="games-empty-state-cta"
      >
        {copy.cta}
      </button>
    </div>
  );
}
