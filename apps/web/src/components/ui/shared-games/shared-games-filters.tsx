/**
 * SharedGamesFilters — sticky search + chips + selects + result counter.
 *
 * Wave A.3b (Issue #596). Mirrors mockup `sp3-shared-games.jsx` lines 300-428.
 *
 * Layout: search input (left, with 🔍 icon) + chip row (4 toggleable chips) +
 * genre <select> (desktop only) + sort <select> (always) + result counter
 * (`${shown} di ${total}` or `${total} giochi`) emitted into an aria-live
 * region so screen readers announce filter changes.
 *
 * Chip definitions (key + backendParam) come from `lib/shared-games/filters`
 * and the caller injects active state. Each chip toggles via `onChipToggle(key)`.
 *
 * Sort options use `SortKey` from filters; Genre options use stable string keys
 * (the SSR layer translates them to `categoryIds[]`).
 */

'use client';

import type { ChangeEvent, JSX, KeyboardEvent } from 'react';

import clsx from 'clsx';

import type { ChipKey, SortKey } from '@/lib/shared-games/filters';

export interface SharedGamesFiltersChipDef {
  readonly key: ChipKey;
  readonly label: string;
}

export interface SharedGamesFiltersOption {
  readonly value: string;
  readonly label: string;
}

export interface SharedGamesFiltersLabels {
  readonly searchPlaceholder: string;
  readonly searchAriaLabel: string;
  readonly genreSelectAriaLabel: string;
  readonly sortSelectAriaLabel: string;
  /** Render fn: `${shown} di ${total} giochi`. */
  readonly resultCount: (shown: number, total: number) => string;
  /** Render fn used when query/filters are empty: `${total} giochi`. */
  readonly totalCount: (total: number) => string;
  readonly clearSearchAriaLabel: string;
}

export interface SharedGamesFiltersProps {
  readonly searchValue: string;
  readonly onSearchChange: (value: string) => void;

  readonly chips: readonly SharedGamesFiltersChipDef[];
  readonly activeChips: ReadonlySet<ChipKey>;
  readonly onChipToggle: (key: ChipKey) => void;

  readonly genreOptions: readonly SharedGamesFiltersOption[];
  readonly genreValue: string;
  readonly onGenreChange: (value: string) => void;

  readonly sortOptions: readonly SharedGamesFiltersOption[];
  readonly sortValue: SortKey;
  readonly onSortChange: (value: SortKey) => void;

  readonly shown: number;
  readonly total: number;
  readonly hasActiveFilters: boolean;

  readonly labels: SharedGamesFiltersLabels;
  readonly className?: string;
}

export function SharedGamesFilters({
  searchValue,
  onSearchChange,
  chips,
  activeChips,
  onChipToggle,
  genreOptions,
  genreValue,
  onGenreChange,
  sortOptions,
  sortValue,
  onSortChange,
  shown,
  total,
  hasActiveFilters,
  labels,
  className,
}: SharedGamesFiltersProps): JSX.Element {
  const counterText = hasActiveFilters
    ? labels.resultCount(shown, total)
    : labels.totalCount(total);

  const handleSearchKeyDown = (e: KeyboardEvent<HTMLInputElement>): void => {
    if (e.key === 'Escape' && searchValue !== '') {
      e.preventDefault();
      onSearchChange('');
    }
  };

  return (
    <div
      data-slot="shared-games-filters"
      role="region"
      aria-label="Filtri catalogo"
      className={clsx(
        'sticky z-30 flex flex-col gap-3 border-b border-border bg-background/95 px-4 py-3 backdrop-blur',
        'top-0 md:top-14',
        className
      )}
    >
      <div className="flex flex-col gap-3 md:flex-row md:items-center">
        {/* Search input */}
        <div className="relative flex-1">
          <span
            aria-hidden="true"
            className="pointer-events-none absolute left-[11px] top-1/2 -translate-y-1/2 text-[14px] text-[hsl(var(--text-muted))]"
          >
            🔍
          </span>
          <input
            type="search"
            value={searchValue}
            onChange={(e: ChangeEvent<HTMLInputElement>) => onSearchChange(e.target.value)}
            onKeyDown={handleSearchKeyDown}
            placeholder={labels.searchPlaceholder}
            aria-label={labels.searchAriaLabel}
            className={clsx(
              'h-9 w-full rounded-full border border-border bg-card pl-8 pr-3 text-[13px]',
              'placeholder:text-[hsl(var(--text-muted))]',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))]'
            )}
          />
          {searchValue !== '' ? (
            <button
              type="button"
              onClick={() => onSearchChange('')}
              aria-label={labels.clearSearchAriaLabel}
              className="absolute right-2 top-1/2 -translate-y-1/2 rounded-full p-1 text-[hsl(var(--text-muted))] hover:text-foreground"
            >
              ✕
            </button>
          ) : null}
        </div>

        {/* Genre select (desktop only) */}
        <select
          value={genreValue}
          onChange={(e: ChangeEvent<HTMLSelectElement>) => onGenreChange(e.target.value)}
          aria-label={labels.genreSelectAriaLabel}
          className={clsx(
            'hidden h-9 rounded-full border border-border bg-card px-3 text-[12px] font-medium text-foreground md:inline-flex',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))]'
          )}
        >
          {genreOptions.map(opt => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
        </select>

        {/* Sort select (always) */}
        <select
          value={sortValue}
          onChange={(e: ChangeEvent<HTMLSelectElement>) => onSortChange(e.target.value as SortKey)}
          aria-label={labels.sortSelectAriaLabel}
          className={clsx(
            'h-9 rounded-full border border-border bg-card px-3 text-[12px] font-medium text-foreground',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))]'
          )}
        >
          {sortOptions.map(opt => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
        </select>
      </div>

      {/* Chip row */}
      <div role="group" aria-label="Filtri rapidi" className="flex flex-wrap gap-2">
        {chips.map(chip => {
          const active = activeChips.has(chip.key);
          return (
            <button
              key={chip.key}
              type="button"
              role="switch"
              aria-checked={active}
              aria-pressed={active}
              data-chip={chip.key}
              data-active={active}
              onClick={() => onChipToggle(chip.key)}
              className={clsx(
                'inline-flex h-8 items-center rounded-full border px-3 font-mono text-[11px] font-bold uppercase tracking-[0.06em] transition-colors',
                active
                  ? 'border-[hsl(var(--c-game))] bg-[hsl(var(--c-game)/0.12)] text-[hsl(var(--c-game))]'
                  : 'border-border bg-card text-[hsl(var(--text-sec))] hover:border-[hsl(var(--c-game)/0.4)]',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))]'
              )}
            >
              {chip.label}
            </button>
          );
        })}
      </div>

      {/* Result counter (live region) */}
      <p
        data-slot="shared-games-result-counter"
        role="status"
        aria-live="polite"
        className="m-0 font-mono text-[11px] text-[hsl(var(--text-muted))]"
      >
        {counterText}
      </p>
    </div>
  );
}
