/**
 * EmptyState — three-variant empty / error placeholder for the V2 catalog grid.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Variants:
 * - `empty-search`   : the user typed a query and got 0 hits.
 * - `filtered-empty` : the user toggled chips/genre and excluded everything.
 * - `api-error`      : the search call failed.
 *
 * The component is intentionally text-first (no illustrations) so the page
 * stays under the +50KB bundle delta budget. Action buttons are optional —
 * the page-client passes `onClearFilters` for the filtered state and
 * `onRetry` for the error state.
 */
import type { JSX } from 'react';

import clsx from 'clsx';

export type EmptyStateVariant = 'empty-search' | 'filtered-empty' | 'api-error';

export interface EmptyStateProps {
  readonly variant: EmptyStateVariant;
  readonly title: string;
  readonly description?: string;
  readonly actionLabel?: string;
  readonly onAction?: () => void;
  readonly className?: string;
}

export function EmptyState({
  variant,
  title,
  description,
  actionLabel,
  onAction,
  className,
}: EmptyStateProps): JSX.Element {
  return (
    <div
      role={variant === 'api-error' ? 'alert' : 'status'}
      aria-live={variant === 'api-error' ? 'assertive' : 'polite'}
      data-testid={`shared-games-empty-${variant}`}
      className={clsx(
        'flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border bg-muted/20 px-6 py-16 text-center',
        className
      )}
    >
      <h2 className="text-lg font-semibold text-foreground">{title}</h2>
      {description && <p className="max-w-md text-sm text-muted-foreground">{description}</p>}
      {actionLabel && onAction && (
        <button
          type="button"
          onClick={onAction}
          className="mt-2 inline-flex h-9 items-center rounded-lg bg-amber-500/20 px-4 text-sm font-medium text-amber-400 transition-colors hover:bg-amber-500/30"
        >
          {actionLabel}
        </button>
      )}
    </div>
  );
}
