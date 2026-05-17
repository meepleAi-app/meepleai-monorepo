/**
 * EmptyLibrary — Wave B.3 v2 component (Issue #574).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (loading skeletons + EmptyState + ErrorState).
 * Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md §3.2
 *
 * Pure component (mirror Wave B.2 EmptyAgents): all i18n strings injected via
 * `labels`. Discriminates 4 surface states via `kind`:
 *   - 'loading'        → 8 skeleton cards desktop / 4 mobile (denser than B.2 agents)
 *   - 'empty'          → 📚 + onAddGame CTA
 *   - 'filtered-empty' → 🔎 + onClearFilters CTA
 *   - 'error'          → ⚠️ + onRetry CTA
 *
 * Differs from EmptyAgents:
 *   - Skeleton count: 8 desktop / 4 mobile (vs B.2: 6/3) — library grids tend to
 *     be denser (4 cols at lg) and a higher skeleton count masks loading flicker
 *     better when the user lands on a populated library.
 *   - Skeleton layout matches LibraryHybridGrid grid columns so the loading
 *     state mirrors the eventual results layout.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export type EmptyLibraryKind = 'empty' | 'filtered-empty' | 'error' | 'loading';

export interface EmptyLibraryCopy {
  readonly title: string;
  readonly subtitle: string;
  readonly cta: string;
}

export interface EmptyLibraryLabels {
  readonly empty: EmptyLibraryCopy;
  readonly filteredEmpty: EmptyLibraryCopy;
  readonly error: EmptyLibraryCopy;
}

export interface EmptyLibraryProps {
  readonly kind: EmptyLibraryKind;
  readonly labels: EmptyLibraryLabels;
  readonly onAddGame?: () => void;
  readonly onClearFilters?: () => void;
  readonly onRetry?: () => void;
  readonly compact?: boolean;
  readonly className?: string;
}

const SKELETON_COUNT_DESKTOP = 8;
const SKELETON_COUNT_MOBILE = 4;

export function EmptyLibrary({
  kind,
  labels,
  onAddGame,
  onClearFilters,
  onRetry,
  compact = false,
  className,
}: EmptyLibraryProps): ReactElement {
  if (kind === 'loading') {
    const count = compact ? SKELETON_COUNT_MOBILE : SKELETON_COUNT_DESKTOP;
    return (
      <div
        data-slot="library-empty-state"
        data-kind="loading"
        className={clsx(
          compact
            ? 'grid grid-cols-2 gap-3 px-4'
            : 'grid grid-cols-2 gap-4 px-4 sm:grid-cols-3 sm:px-6 lg:grid-cols-4',
          className
        )}
        aria-busy="true"
        aria-live="polite"
      >
        {Array.from({ length: count }, (_, index) => (
          <Skeleton
            key={index}
            data-slot="library-empty-skeleton"
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

  const icon = kind === 'empty' ? '📚' : kind === 'filtered-empty' ? '🔎' : '⚠️';

  return (
    <div
      data-slot="library-empty-state"
      data-kind={kind}
      className={clsx(
        'flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border bg-muted/20 px-6 py-12 text-center',
        compact ? 'mx-4' : 'mx-4 sm:mx-8',
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
        data-slot="library-empty-state-cta"
      >
        {copy.cta}
      </button>
    </div>
  );
}
