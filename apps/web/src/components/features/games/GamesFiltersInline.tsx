/**
 * GamesFiltersInline — Wave B.1 v2 component (Issue #633).
 *
 * Mapped from `admin-mockups/design_files/sp4-games-index.jsx` (GameFilters).
 * Spec: docs/superpowers/specs/2026-04-29-v2-migration-wave-b-1-games.md §3.2
 *
 * Pure component (mirror Wave A.4): all i18n strings injected via `labels`.
 * Internal state is limited to the search input mirror (`localQuery`) so the
 * 300ms debounce stays trailing-edge correct while the orchestrator above
 * remains source of truth for `query`.
 *
 * WAI-ARIA contract:
 *   - Search: <input type="search"> with sr-only <label>; clear button is
 *     immediate (does NOT pass through the debounce).
 *   - Status: role="tablist" + 4 role="tab" with aria-selected. Roving
 *     tabindex driven by `useTablistKeyboardNav` (orientation horizontal,
 *     wrap), automatic activation (focus = onChange same tick).
 *   - Sort: native <select> with associated <label>.
 *   - View toggle: role="group" with 2 buttons + aria-pressed.
 */

'use client';

import { useEffect, useRef, useState, type ChangeEvent, type ReactElement } from 'react';

import clsx from 'clsx';

import { useDebounce } from '@/hooks/useDebounce';
import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';
import type { GamesSortKey, GamesStatusKey } from '@/lib/games/library-filters';

export type { GamesSortKey, GamesStatusKey };
export type GamesViewKey = 'grid' | 'list';

export interface GamesFiltersInlineLabels {
  readonly search: {
    readonly placeholder: string;
    readonly ariaLabel: string;
    readonly clearAriaLabel: string;
  };
  readonly status: {
    readonly label: string;
    readonly options: Readonly<Record<GamesStatusKey, string>>;
  };
  readonly sort: {
    readonly label: string;
    readonly options: Readonly<Record<GamesSortKey, string>>;
  };
  readonly view: {
    readonly label: string;
    readonly options: Readonly<Record<GamesViewKey, string>>;
  };
  readonly resultCount: (count: number) => string;
}

export interface GamesFiltersInlineProps {
  readonly labels: GamesFiltersInlineLabels;
  readonly query: string;
  readonly onQueryChange: (next: string) => void;
  readonly status: GamesStatusKey;
  readonly onStatusChange: (next: GamesStatusKey) => void;
  readonly sort: GamesSortKey;
  readonly onSortChange: (next: GamesSortKey) => void;
  readonly view: GamesViewKey;
  readonly onViewChange: (next: GamesViewKey) => void;
  readonly resultCount: number;
  readonly compact?: boolean;
  readonly className?: string;
}

const STATUS_KEYS: readonly GamesStatusKey[] = ['all', 'owned', 'wishlist', 'played'];
const SORT_KEYS: readonly GamesSortKey[] = ['last-played', 'rating', 'title', 'year'];
const VIEW_KEYS: readonly GamesViewKey[] = ['grid', 'list'];

const SEARCH_DEBOUNCE_MS = 300;

export function GamesFiltersInline({
  labels,
  query,
  onQueryChange,
  status,
  onStatusChange,
  sort,
  onSortChange,
  view,
  onViewChange,
  resultCount,
  compact = false,
  className,
}: GamesFiltersInlineProps): ReactElement {
  const [localQuery, setLocalQuery] = useState(query);
  const debouncedQuery = useDebounce(localQuery, SEARCH_DEBOUNCE_MS);
  const lastFiredRef = useRef(query);

  useEffect(() => {
    if (query !== lastFiredRef.current) {
      lastFiredRef.current = query;
      setLocalQuery(query);
    }
  }, [query]);

  useEffect(() => {
    if (debouncedQuery !== lastFiredRef.current) {
      lastFiredRef.current = debouncedQuery;
      onQueryChange(debouncedQuery);
    }
  }, [debouncedQuery, onQueryChange]);

  const handleClearSearch = () => {
    setLocalQuery('');
    lastFiredRef.current = '';
    onQueryChange('');
  };

  const { tabRefs: statusTabRefs, handleKeyDown: handleStatusKeyDown } =
    useTablistKeyboardNav<GamesStatusKey>({
      orderedKeys: STATUS_KEYS,
      onChange: onStatusChange,
      orientation: 'horizontal',
    });

  return (
    <section
      data-slot="games-filters-inline"
      className={clsx(
        'flex flex-col gap-4 rounded-2xl border border-border bg-card p-4 shadow-sm sm:p-5',
        className
      )}
    >
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <label className="relative flex w-full max-w-xl items-center">
          <span className="sr-only">{labels.search.ariaLabel}</span>
          <input
            type="search"
            value={localQuery}
            onChange={(event: ChangeEvent<HTMLInputElement>) => setLocalQuery(event.target.value)}
            placeholder={labels.search.placeholder}
            className={clsx(
              'h-10 w-full rounded-full border border-border bg-background px-4 pr-10 text-sm',
              'placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
            )}
            data-slot="games-filters-search-input"
          />
          {localQuery.length > 0 ? (
            <button
              type="button"
              aria-label={labels.search.clearAriaLabel}
              onClick={handleClearSearch}
              className={clsx(
                'absolute right-2 top-1/2 inline-flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded-full',
                'text-muted-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
              )}
              data-slot="games-filters-search-clear"
            >
              ×
            </button>
          ) : null}
        </label>

        <span
          className="text-xs font-medium text-muted-foreground sm:text-sm"
          data-slot="games-filters-result-count"
        >
          {labels.resultCount(resultCount)}
        </span>
      </div>

      <div
        className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center sm:gap-4"
        data-slot="games-filters-controls"
      >
        <div
          role="tablist"
          aria-label={labels.status.label}
          className="inline-flex items-center gap-1 rounded-full border border-border bg-muted/40 p-1"
          data-slot="games-filters-status-tablist"
        >
          {STATUS_KEYS.map(key => {
            const selected = key === status;
            return (
              <button
                key={key}
                ref={node => {
                  if (node) statusTabRefs.current.set(key, node);
                  else statusTabRefs.current.delete(key);
                }}
                type="button"
                role="tab"
                id={`games-filters-status-${key}`}
                aria-selected={selected}
                tabIndex={selected ? 0 : -1}
                onClick={() => onStatusChange(key)}
                onKeyDown={event => handleStatusKeyDown(event, key)}
                className={clsx(
                  'inline-flex h-8 items-center rounded-full px-3 text-xs font-medium transition-colors sm:text-sm',
                  selected
                    ? 'bg-primary text-primary-foreground shadow-sm'
                    : 'text-muted-foreground hover:text-foreground'
                )}
                data-slot="games-filters-status-tab"
                data-status={key}
              >
                {labels.status.options[key]}
              </button>
            );
          })}
        </div>

        <label
          className="flex items-center gap-2 text-xs font-medium text-muted-foreground sm:text-sm"
          data-slot="games-filters-sort"
        >
          <span>{labels.sort.label}</span>
          <select
            value={sort}
            onChange={(event: ChangeEvent<HTMLSelectElement>) =>
              onSortChange(event.target.value as GamesSortKey)
            }
            className={clsx(
              'h-8 rounded-full border border-border bg-background px-3 text-xs sm:text-sm',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
            )}
          >
            {SORT_KEYS.map(key => (
              <option key={key} value={key}>
                {labels.sort.options[key]}
              </option>
            ))}
          </select>
        </label>

        {compact ? null : (
          <div
            role="group"
            aria-label={labels.view.label}
            className="ml-auto inline-flex items-center gap-1 rounded-full border border-border bg-muted/40 p-1"
            data-slot="games-filters-view-toggle"
          >
            {VIEW_KEYS.map(key => {
              const pressed = view === key;
              return (
                <button
                  key={key}
                  type="button"
                  aria-pressed={pressed}
                  onClick={() => onViewChange(key)}
                  className={clsx(
                    'inline-flex h-7 items-center rounded-full px-3 text-xs font-medium transition-colors',
                    pressed
                      ? 'bg-primary text-primary-foreground shadow-sm'
                      : 'text-muted-foreground hover:text-foreground'
                  )}
                  data-slot="games-filters-view-button"
                  data-view={key}
                >
                  {labels.view.options[key]}
                </button>
              );
            })}
          </div>
        )}
      </div>
    </section>
  );
}
