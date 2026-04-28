/**
 * SharedGamesGrid — orchestrator for the V2 `/shared-games` page body.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Composes filters + grid + sidebar and switches between the 5 rendering
 * states defined in spec §3.2:
 *   1. default          — has games to render.
 *   2. loading          — first fetch (`isLoading`), no `placeholderData` yet.
 *   3. empty-search     — `q` set but zero hits.
 *   4. filtered-empty   — chips/genre set but zero hits.
 *   5. api-error        — query failed (no previous data to fall back on).
 *
 * The component is **presentation-only**: it does not own state, does not
 * own the URL hash, and does not subscribe to React Query directly. The
 * page client owns those concerns and wires `state + onChange` callbacks
 * here so the grid stays SSR-friendly and easy to unit-test in isolation.
 */
import type { JSX } from 'react';

import clsx from 'clsx';

import type { SharedGamesChip, SharedGamesSort } from '@/hooks/queries/useSharedGamesSearch';
import type { SharedGame, TopContributor } from '@/lib/api';

import { ContributorsSidebar, type ContributorsSidebarProps } from './contributors-sidebar';
import { EmptyState } from './empty-state';
import { MeepleCardGame } from './meeple-card-game';
import { MeepleCardGameSkeleton } from './meeple-card-game-skeleton';
import {
  SharedGamesFilters,
  type GenreOption,
  type SharedGamesFiltersLabels,
} from './shared-games-filters';

/** Labels for the empty / error states — passed in for i18n. */
export interface SharedGamesGridLabels {
  /** Variant `empty-search` — user typed a query and got zero hits. */
  readonly emptySearchTitle: string;
  readonly emptySearchDescription?: string;
  /** Variant `filtered-empty` — toggles excluded everything. */
  readonly filteredEmptyTitle: string;
  readonly filteredEmptyDescription?: string;
  readonly filteredEmptyAction: string;
  /** Variant `api-error`. */
  readonly errorTitle: string;
  readonly errorDescription?: string;
  readonly errorAction: string;
}

export interface SharedGamesGridProps {
  // ---- Filter state (controlled by parent) ----
  readonly q: string;
  readonly chips: ReadonlyArray<SharedGamesChip>;
  readonly genre: string;
  readonly sort: SharedGamesSort;
  readonly genres: ReadonlyArray<GenreOption>;
  readonly filterLabels: SharedGamesFiltersLabels;
  readonly onQChange: (next: string) => void;
  readonly onChipToggle: (chip: SharedGamesChip) => void;
  readonly onGenreChange: (slug: string) => void;
  readonly onSortChange: (sort: SharedGamesSort) => void;

  // ---- Grid data ----
  readonly games: ReadonlyArray<SharedGame> | undefined;
  readonly isLoading: boolean;
  readonly isError: boolean;
  /**
   * `true` when `q`, `chips`, or `genre` differ from defaults — used to
   * pick between `empty-search` (q only) and `filtered-empty` variants.
   */
  readonly hasActiveSearch: boolean;
  readonly hasActiveFilters: boolean;
  readonly gridLabels: SharedGamesGridLabels;
  readonly onClearFilters: () => void;
  readonly onRetry: () => void;

  // ---- Contributors sidebar ----
  readonly contributors: ReadonlyArray<TopContributor> | undefined;
  readonly contributorsLoading: boolean;
  readonly contributorsError: boolean;
  readonly contributorsTitle?: ContributorsSidebarProps['title'];
  readonly contributorsEmptyLabel?: ContributorsSidebarProps['emptyLabel'];

  readonly className?: string;
  /** Number of skeleton cards to render while loading (default 8). */
  readonly skeletonCount?: number;
}

/**
 * Decide which body to render given the current query state. Encapsulated
 * so the JSX stays flat and the decision tree is easy to unit-test.
 */
function pickBodyVariant(args: {
  isLoading: boolean;
  isError: boolean;
  games: ReadonlyArray<SharedGame> | undefined;
  hasActiveSearch: boolean;
  hasActiveFilters: boolean;
}): 'loading' | 'error' | 'empty-search' | 'filtered-empty' | 'default' {
  // Errors win — even over `placeholderData`, since stale results would be
  // misleading after a hard failure.
  if (args.isError && (!args.games || args.games.length === 0)) return 'error';
  if (args.isLoading && !args.games) return 'loading';

  const empty = !args.games || args.games.length === 0;
  if (!empty) return 'default';

  // Both empty states need an active filter; bare-defaults zero hits is
  // unreachable in practice (catalog has games) but we still fall through
  // to `filtered-empty` rather than crash.
  if (args.hasActiveSearch && !args.hasActiveFilters) return 'empty-search';
  return 'filtered-empty';
}

export function SharedGamesGrid({
  q,
  chips,
  genre,
  sort,
  genres,
  filterLabels,
  onQChange,
  onChipToggle,
  onGenreChange,
  onSortChange,
  games,
  isLoading,
  isError,
  hasActiveSearch,
  hasActiveFilters,
  gridLabels,
  onClearFilters,
  onRetry,
  contributors,
  contributorsLoading,
  contributorsError,
  contributorsTitle,
  contributorsEmptyLabel,
  className,
  skeletonCount = 8,
}: SharedGamesGridProps): JSX.Element {
  const variant = pickBodyVariant({
    isLoading,
    isError,
    games,
    hasActiveSearch,
    hasActiveFilters,
  });

  return (
    <div
      data-testid="shared-games-grid"
      data-variant={variant}
      className={clsx('flex flex-col gap-6', className)}
    >
      <SharedGamesFilters
        q={q}
        chips={chips}
        genre={genre}
        sort={sort}
        genres={genres}
        labels={filterLabels}
        onQChange={onQChange}
        onChipToggle={onChipToggle}
        onGenreChange={onGenreChange}
        onSortChange={onSortChange}
      />

      <div className="flex flex-col gap-6 md:flex-row md:items-start">
        {/* Main column — grid or empty / error / loading state */}
        <div className="min-w-0 flex-1">
          {variant === 'loading' && (
            <ul
              data-testid="shared-games-grid-loading"
              className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4"
              aria-busy="true"
            >
              {Array.from({ length: skeletonCount }).map((_, idx) => (
                <li key={idx}>
                  <MeepleCardGameSkeleton />
                </li>
              ))}
            </ul>
          )}

          {variant === 'error' && (
            <EmptyState
              variant="api-error"
              title={gridLabels.errorTitle}
              description={gridLabels.errorDescription}
              actionLabel={gridLabels.errorAction}
              onAction={onRetry}
            />
          )}

          {variant === 'empty-search' && (
            <EmptyState
              variant="empty-search"
              title={gridLabels.emptySearchTitle}
              description={gridLabels.emptySearchDescription}
            />
          )}

          {variant === 'filtered-empty' && (
            <EmptyState
              variant="filtered-empty"
              title={gridLabels.filteredEmptyTitle}
              description={gridLabels.filteredEmptyDescription}
              actionLabel={gridLabels.filteredEmptyAction}
              onAction={onClearFilters}
            />
          )}

          {variant === 'default' && games && games.length > 0 && (
            <ul
              data-testid="shared-games-grid-list"
              className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4"
            >
              {games.map(g => (
                <li key={g.id}>
                  <MeepleCardGame game={g} />
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Sidebar — non-critical, fail-silent */}
        <ContributorsSidebar
          contributors={contributors}
          isLoading={contributorsLoading}
          isError={contributorsError}
          title={contributorsTitle}
          emptyLabel={contributorsEmptyLabel}
        />
      </div>
    </div>
  );
}
