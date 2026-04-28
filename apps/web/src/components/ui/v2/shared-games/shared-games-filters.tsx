/**
 * SharedGamesFilters — search + chip toggles + genre + sort controls.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Controlled component: parent owns the V2 state in the URL hash
 * (`useUrlHashState`) and wires `onChange` here. The component does **not**
 * debounce internally — debouncing happens at the page level so that the
 * URL hash is only updated after the user pauses typing (per spec §3.2).
 *
 * Accessibility:
 * - Search input has an explicit `<label>` (visually hidden).
 * - Chips are toggle buttons with `aria-pressed`.
 * - Genre / sort are native `<select>` elements with associated labels.
 */
import { useId, type JSX } from 'react';

import clsx from 'clsx';

import type { SharedGamesChip, SharedGamesSort } from '@/hooks/queries/useSharedGamesSearch';

export interface SharedGamesFiltersLabels {
  readonly searchLabel: string;
  readonly searchPlaceholder: string;
  readonly chipToolkit: string;
  readonly chipAgent: string;
  readonly chipTopRated: string;
  readonly chipNew: string;
  readonly genreLabel: string;
  readonly genreAll: string;
  readonly sortLabel: string;
  readonly sortRating: string;
  readonly sortContrib: string;
  readonly sortNew: string;
  readonly sortTitle: string;
}

export interface GenreOption {
  readonly slug: string;
  readonly label: string;
}

export interface SharedGamesFiltersProps {
  readonly q: string;
  readonly chips: ReadonlyArray<SharedGamesChip>;
  readonly genre: string;
  readonly sort: SharedGamesSort;
  readonly genres: ReadonlyArray<GenreOption>;
  readonly labels: SharedGamesFiltersLabels;
  readonly onQChange: (next: string) => void;
  readonly onChipToggle: (chip: SharedGamesChip) => void;
  readonly onGenreChange: (slug: string) => void;
  readonly onSortChange: (sort: SharedGamesSort) => void;
  readonly className?: string;
}

const CHIP_ORDER: ReadonlyArray<SharedGamesChip> = ['tk', 'ag', 'top', 'new'];

const SORT_ORDER: ReadonlyArray<SharedGamesSort> = ['rating', 'contrib', 'new', 'title'];

export function SharedGamesFilters({
  q,
  chips,
  genre,
  sort,
  genres,
  labels,
  onQChange,
  onChipToggle,
  onGenreChange,
  onSortChange,
  className,
}: SharedGamesFiltersProps): JSX.Element {
  const searchId = useId();
  const genreId = useId();
  const sortId = useId();

  const chipLabel: Record<SharedGamesChip, string> = {
    tk: labels.chipToolkit,
    ag: labels.chipAgent,
    top: labels.chipTopRated,
    new: labels.chipNew,
  };

  const sortLabel: Record<SharedGamesSort, string> = {
    rating: labels.sortRating,
    contrib: labels.sortContrib,
    new: labels.sortNew,
    title: labels.sortTitle,
  };

  return (
    <div data-testid="shared-games-filters" className={clsx('flex flex-col gap-3', className)}>
      {/* Row 1 — search */}
      <div>
        <label htmlFor={searchId} className="sr-only">
          {labels.searchLabel}
        </label>
        <input
          id={searchId}
          type="search"
          value={q}
          onChange={e => onQChange(e.target.value)}
          placeholder={labels.searchPlaceholder}
          autoComplete="off"
          spellCheck={false}
          data-testid="shared-games-search"
          className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-400/70"
        />
      </div>

      {/* Row 2 — chips */}
      <div
        role="group"
        aria-label={labels.searchLabel}
        data-testid="shared-games-chips"
        className="flex flex-wrap gap-2"
      >
        {CHIP_ORDER.map(chip => {
          const pressed = chips.includes(chip);
          return (
            <button
              key={chip}
              type="button"
              data-testid={`shared-games-chip-${chip}`}
              aria-pressed={pressed}
              onClick={() => onChipToggle(chip)}
              className={clsx(
                'inline-flex h-8 items-center rounded-full px-3 text-xs font-medium transition-colors',
                pressed
                  ? 'bg-amber-500/30 text-amber-300 ring-1 ring-amber-400/60'
                  : 'bg-muted/40 text-muted-foreground hover:bg-muted/60'
              )}
            >
              {chipLabel[chip]}
            </button>
          );
        })}
      </div>

      {/* Row 3 — genre + sort */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2">
          <label htmlFor={genreId} className="text-xs font-medium text-muted-foreground">
            {labels.genreLabel}
          </label>
          <select
            id={genreId}
            value={genre}
            onChange={e => onGenreChange(e.target.value)}
            data-testid="shared-games-genre"
            className="h-9 rounded-lg border border-border bg-background px-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-amber-400/70"
          >
            <option value="">{labels.genreAll}</option>
            {genres.map(g => (
              <option key={g.slug} value={g.slug}>
                {g.label}
              </option>
            ))}
          </select>
        </div>

        <div className="flex items-center gap-2">
          <label htmlFor={sortId} className="text-xs font-medium text-muted-foreground">
            {labels.sortLabel}
          </label>
          <select
            id={sortId}
            value={sort}
            onChange={e => onSortChange(e.target.value as SharedGamesSort)}
            data-testid="shared-games-sort"
            className="h-9 rounded-lg border border-border bg-background px-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-amber-400/70"
          >
            {SORT_ORDER.map(s => (
              <option key={s} value={s}>
                {sortLabel[s]}
              </option>
            ))}
          </select>
        </div>
      </div>
    </div>
  );
}
