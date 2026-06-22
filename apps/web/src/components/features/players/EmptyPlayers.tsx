/**
 * EmptyPlayers — Wave 4 D1 v2 component (Issue #682).
 *
 * Mapped from `admin-mockups/design_files/sp4-players-index.jsx`
 * (empty/error/loading states).
 *
 * Pure component (mirror Wave B.2 EmptyAgents): all i18n strings injected via
 * `labels`. Discriminates 3 surface states via `kind`:
 *   - 'empty'          → 🎲 + "Inizia a giocare" CTA → no callback (navigation intent)
 *   - 'filtered-empty' → 🔎 + "Cancella ricerca" CTA → onClearFilters
 *   - 'error'          → ⚠️ + "Riprova" CTA → onRetry
 *
 * WCAG compliance:
 *   - role="alert" on error kind (live region, immediate announcement).
 *   - role="status" on empty + filtered-empty (polite live region).
 *   - CTA uses bg-emerald-700 (700-shade) for white text — WCAG AA pre-emption
 *     per Wave C.1 lesson learned (never 600-shade with white text).
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type EmptyPlayersKind = 'empty' | 'filtered-empty' | 'error';

export interface EmptyPlayersCopy {
  readonly title: string;
  readonly subtitle: string;
  readonly cta: string;
}

export interface EmptyPlayersLabels {
  readonly empty: EmptyPlayersCopy;
  readonly filteredEmpty: EmptyPlayersCopy;
  readonly error: EmptyPlayersCopy;
}

export interface EmptyPlayersProps {
  readonly kind: EmptyPlayersKind;
  readonly onClearFilters?: () => void;
  readonly onRetry?: () => void;
  readonly labels: EmptyPlayersLabels;
  readonly className?: string;
}

const KIND_ICON: Readonly<Record<EmptyPlayersKind, string>> = {
  empty: '🎲',
  'filtered-empty': '🔎',
  error: '⚠️',
};

export function EmptyPlayers({
  kind,
  onClearFilters,
  onRetry,
  labels,
  className,
}: EmptyPlayersProps): ReactElement {
  const copy =
    kind === 'empty'
      ? labels.empty
      : kind === 'filtered-empty'
        ? labels.filteredEmpty
        : labels.error;

  const onCtaClick =
    kind === 'filtered-empty' ? onClearFilters : kind === 'error' ? onRetry : undefined;

  const icon = KIND_ICON[kind];

  return (
    <div
      data-slot="players-empty"
      data-kind={kind}
      className={clsx(
        'flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border bg-muted/20 px-6 py-12 text-center',
        'mx-4 sm:mx-8',
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
          'bg-emerald-700 text-white shadow-sm transition-colors',
          'hover:bg-emerald-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
        )}
        data-slot="players-empty-cta"
      >
        {copy.cta}
      </button>
    </div>
  );
}
