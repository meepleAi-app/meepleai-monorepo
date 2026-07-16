'use client';

import { useEffect, useRef, useState, type JSX } from 'react';

import { Loader2, Search as SearchIcon, X } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Popover,
  PopoverClose,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/overlays/popover';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

import {
  countActiveFilters,
  type Priority,
  type WishlistFilterState,
  type WishlistSort,
} from '../_lib/wishlist-filters';

/** Debounce delay (ms) applied to search input before it is committed via `onChange`. */
const SEARCH_DEBOUNCE_MS = 400;

const PRIORITY_ORDER: readonly Priority[] = ['high', 'medium', 'low'];

const SORT_OPTIONS: readonly WishlistSort[] = ['priority', 'recent', 'oldest', 'alpha', 'price'];

/**
 * Priority chip color classes. Only `destructive` and `muted` are registered
 * as Tailwind utilities in `@theme inline` (`globals.css`) — `--color-warning`
 * / `--color-success` live in `design-tokens.css`'s plain `:root` block, NOT
 * inside `@theme`/`@theme inline`, so `bg-warning`/`text-warning` never
 * generate real utility classes (dead no-op, same class of bug as the
 * `--e-*` finding in the FU-2 cleanup — see MEMORY.md). `medium` therefore
 * falls back to a neutral `text-foreground` treatment instead of a
 * non-existent "warning" token.
 */
const PRIORITY_CHIP_CLASSNAMES: Record<Priority, string> = {
  high: 'border-destructive/40 text-destructive hover:bg-destructive/10 aria-checked:border-destructive aria-checked:bg-destructive/10',
  medium:
    'border-border text-foreground hover:bg-muted aria-checked:border-foreground/60 aria-checked:bg-muted',
  low: 'border-border text-muted-foreground hover:bg-muted aria-checked:border-muted-foreground aria-checked:bg-muted',
};

const CHIP_BASE_CLASSNAME =
  'inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-semibold transition-colors';

export interface WishlistToolbarProps {
  state: WishlistFilterState;
  onChange: (next: WishlistFilterState) => void;
  priorityCounts: Record<Priority, number>;
  totalCount: number;
  resultCount: number;
  onClearAll: () => void;
}

/**
 * Filter/sort toolbar for /library/wishlist (Issue #3007, Task B5).
 *
 * Matches `admin-mockups/design_files/sp4-library-wishlist-ui.jsx`'s
 * `Toolbar`: `[ search ] [ Tutti | Alta | Media | Bassa chips ] [ Ordina ]`,
 * plus a conditional summary bar (`countActiveFilters(state) > 0`) showing
 * the active-filter count, the filtered/total result count, and a "Cancella
 * filtri" reset.
 *
 * Search is debounced 400ms client-side (`localSearch` state) — mirrors
 * `toolkit/history/_components/HistoryToolbar.tsx`'s pattern — so `onChange`
 * only fires once typing pauses; a "Cercando…" indicator is shown while the
 * debounce is pending.
 */
export function WishlistToolbar({
  state,
  onChange,
  priorityCounts,
  totalCount,
  resultCount,
  onClearAll,
}: WishlistToolbarProps): JSX.Element {
  const { t } = useTranslation();

  const [localSearch, setLocalSearch] = useState(state.search);
  const [isSearching, setIsSearching] = useState(false);
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const stateRef = useRef(state);
  const onChangeRef = useRef(onChange);

  useEffect(() => {
    stateRef.current = state;
  }, [state]);

  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  useEffect(() => {
    setLocalSearch(state.search);
  }, [state.search]);

  useEffect(
    () => () => {
      if (searchTimerRef.current) clearTimeout(searchTimerRef.current);
    },
    []
  );

  const commitSearch = (value: string) => {
    if (searchTimerRef.current) {
      clearTimeout(searchTimerRef.current);
      searchTimerRef.current = null;
    }
    setIsSearching(false);
    onChangeRef.current({ ...stateRef.current, search: value });
  };

  const handleSearchInput = (value: string) => {
    setLocalSearch(value);
    if (searchTimerRef.current) clearTimeout(searchTimerRef.current);
    setIsSearching(true);
    searchTimerRef.current = setTimeout(() => {
      searchTimerRef.current = null;
      commitSearch(value);
    }, SEARCH_DEBOUNCE_MS);
  };

  const handleClearSearch = () => {
    setLocalSearch('');
    commitSearch('');
  };

  const togglePriority = (priority: Priority) => {
    const next = state.priorities.includes(priority)
      ? state.priorities.filter(p => p !== priority)
      : [...state.priorities, priority];
    onChange({ ...state, priorities: next });
  };

  const clearPriorities = () => onChange({ ...state, priorities: [] });

  const allActive = state.priorities.length === 0;
  const activeCount = countActiveFilters(state);

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-3 lg:flex-row lg:flex-wrap lg:items-center">
        {/* search */}
        <div className="relative min-w-[240px] flex-1">
          <SearchIcon
            className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden="true"
          />
          <Input
            type="search"
            value={localSearch}
            onChange={e => handleSearchInput(e.target.value)}
            onKeyDown={e => {
              if (e.key === 'Escape') handleClearSearch();
            }}
            placeholder={t('pages.library.wishlist.filters.searchPlaceholder')}
            aria-label={t('pages.library.wishlist.filters.searchAriaLabel')}
            className="pl-9 pr-10"
          />
          <div className="absolute right-1 top-1/2 flex -translate-y-1/2 items-center gap-1">
            {isSearching && (
              <span className="flex items-center gap-1 pr-1 text-xs text-muted-foreground">
                <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />
                {t('pages.library.wishlist.filters.searching')}
              </span>
            )}
            {localSearch && (
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-7 w-7"
                onClick={handleClearSearch}
                aria-label={t('pages.library.wishlist.filters.clearSearch')}
              >
                <X className="h-3.5 w-3.5" />
              </Button>
            )}
          </div>
        </div>

        {/* priority chips */}
        <div
          className="flex flex-wrap items-center gap-2"
          role="group"
          aria-label={t('pages.library.wishlist.filters.priorityGroupAria')}
        >
          <button
            type="button"
            role="checkbox"
            aria-checked={allActive}
            aria-label={t('pages.library.wishlist.filters.all')}
            onClick={clearPriorities}
            className={cn(
              CHIP_BASE_CLASSNAME,
              allActive
                ? 'border-primary bg-primary/10 text-primary'
                : 'border-border text-muted-foreground hover:bg-muted'
            )}
          >
            {t('pages.library.wishlist.filters.all')}
            <span className="text-[10px] font-bold opacity-70">{totalCount}</span>
          </button>
          {PRIORITY_ORDER.map(priority => {
            const active = state.priorities.includes(priority);
            return (
              <button
                key={priority}
                type="button"
                role="checkbox"
                aria-checked={active}
                aria-label={t(`pages.library.wishlist.priority.${priority}`)}
                onClick={() => togglePriority(priority)}
                className={cn(CHIP_BASE_CLASSNAME, PRIORITY_CHIP_CLASSNAMES[priority])}
              >
                {t(`pages.library.wishlist.priority.${priority}`)}
                <span className="text-[10px] font-bold opacity-70">{priorityCounts[priority]}</span>
              </button>
            );
          })}
        </div>

        {/* sort */}
        <div className="lg:ml-auto">
          <Popover>
            <PopoverTrigger asChild>
              <Button
                type="button"
                variant="outline"
                size="sm"
                aria-label={t('pages.library.wishlist.filters.sortBy')}
                className="gap-1.5"
              >
                <span aria-hidden="true">↕</span>
                {t(`pages.library.wishlist.filters.sortOptions.${state.sort}`)}
                <span aria-hidden="true" className="text-[8px] opacity-70">
                  ▼
                </span>
              </Button>
            </PopoverTrigger>
            <PopoverContent
              align="end"
              className="w-56 p-2"
              role="listbox"
              aria-label={t('pages.library.wishlist.filters.sortAria')}
            >
              <div className="px-2 py-1 text-xs font-semibold text-muted-foreground">
                {t('pages.library.wishlist.filters.sort')}
              </div>
              {SORT_OPTIONS.map(opt => {
                const selected = state.sort === opt;
                return (
                  <PopoverClose key={opt} asChild>
                    <button
                      type="button"
                      role="option"
                      aria-selected={selected}
                      onClick={() => onChange({ ...state, sort: opt })}
                      className={cn(
                        'flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm hover:bg-muted',
                        selected && 'font-semibold text-foreground'
                      )}
                    >
                      <span aria-hidden="true" className="w-3">
                        {selected ? '●' : ''}
                      </span>
                      {t(`pages.library.wishlist.filters.sortOptions.${opt}`)}
                    </button>
                  </PopoverClose>
                );
              })}
            </PopoverContent>
          </Popover>
        </div>
      </div>

      {/* summary bar */}
      {activeCount > 0 && (
        <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-muted/40 px-3 py-2 text-sm">
          <Badge variant="secondary" className="gap-1">
            {t('pages.library.wishlist.summary.activeFilters', { n: activeCount })}
          </Badge>
          <span className="text-muted-foreground" aria-hidden="true">
            ·
          </span>
          <span className="text-muted-foreground">
            {t('pages.library.wishlist.summary.results', { n: resultCount, total: totalCount })}
          </span>
          <span className="flex-1" />
          <Button type="button" variant="ghost" size="sm" onClick={onClearAll}>
            {t('pages.library.wishlist.summary.clearAll')}
          </Button>
        </div>
      )}
    </div>
  );
}
