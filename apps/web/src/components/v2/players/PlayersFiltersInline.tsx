/**
 * PlayersFiltersInline — Wave 4 D1 v2 component (Issue #682).
 *
 * Mapped from `admin-mockups/design_files/sp4-players-index.jsx`
 * (PlayersFiltersInline search input area).
 *
 * Pure component (mirror Wave B.1 GamesFiltersInline — simplified):
 *   labels injected via props, no useTranslation import.
 *
 * Spec contract per task brief:
 *   - search input with searchPlaceholder + searchAriaLabel
 *   - clear button visible ONLY when hasFilters===true
 *   - clear button fires onClearFilters on click
 *   - NO debounce inside this component (orchestrator handles it or passes
 *     direct value — kept simple per spec: search input, clear button only)
 */

import { type ChangeEvent, type ReactElement } from 'react';

import clsx from 'clsx';

export interface PlayersFiltersInlineLabels {
  readonly searchPlaceholder: string;
  readonly searchAriaLabel: string;
  readonly clearFilters: string;
}

export interface PlayersFiltersInlineProps {
  readonly search: string;
  readonly onSearchChange: (value: string) => void;
  readonly onClearFilters: () => void;
  readonly hasFilters: boolean;
  readonly labels: PlayersFiltersInlineLabels;
  readonly className?: string;
}

export function PlayersFiltersInline({
  search,
  onSearchChange,
  onClearFilters,
  hasFilters,
  labels,
  className,
}: PlayersFiltersInlineProps): ReactElement {
  return (
    <div
      data-slot="players-filters-inline"
      className={clsx(
        'flex flex-col gap-3 rounded-2xl border border-border bg-card/80 p-4 shadow-sm',
        'sm:flex-row sm:items-center sm:p-5',
        className
      )}
    >
      <label className="relative flex flex-1 items-center">
        <span className="sr-only">{labels.searchAriaLabel}</span>
        <input
          type="search"
          value={search}
          onChange={(event: ChangeEvent<HTMLInputElement>) => onSearchChange(event.target.value)}
          placeholder={labels.searchPlaceholder}
          className={clsx(
            'w-full rounded-xl border border-input bg-background px-4 py-2 text-sm',
            'text-foreground placeholder:text-muted-foreground',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
          )}
          aria-label={labels.searchAriaLabel}
        />
      </label>

      {hasFilters && (
        <button
          type="button"
          onClick={onClearFilters}
          className={clsx(
            'inline-flex h-9 shrink-0 items-center justify-center rounded-full px-4 text-sm font-medium',
            'bg-muted text-muted-foreground transition-colors',
            'hover:bg-muted/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
          )}
          data-slot="players-filters-clear"
        >
          {labels.clearFilters}
        </button>
      )}
    </div>
  );
}
