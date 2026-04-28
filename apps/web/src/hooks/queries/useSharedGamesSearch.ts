/**
 * useSharedGamesSearch - TanStack Query hook for the V2 `/shared-games` page.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596)
 *
 * Owns the V2 state shape (see `SharedGamesState`) and translates it to the
 * existing `SearchSharedGamesParams` contract before hitting the backend.
 * Behavioural notes:
 * - `placeholderData: keepPreviousData` keeps the grid visible during filter
 *   transitions, avoiding a flash of skeletons on every keystroke.
 * - `initialData` is honoured **only when state matches `defaultsState`** so
 *   that SSR-hydrated content doesn't leak into filtered queries.
 */
import { keepPreviousData, useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api, type PagedSharedGames, type SearchSharedGamesParams } from '@/lib/api';

/** V2 chip filter ids — see spec §3.2. */
export type SharedGamesChip = 'tk' | 'ag' | 'top' | 'new';

/** V2 sort ids — narrower than backend's enum (no `Title-asc`). */
export type SharedGamesSort = 'rating' | 'contrib' | 'new' | 'title';

/**
 * V2 state shape. Mirrors the URL hash schema in `page-client.tsx`.
 * `genre` is a category slug; the page client resolves it to an id list.
 */
export interface SharedGamesState {
  q: string;
  chips: SharedGamesChip[];
  genre: string;
  sort: SharedGamesSort;
}

/** Canonical defaults — must match `page.tsx` SSR fetch parameters. */
export const defaultsState: SharedGamesState = {
  q: '',
  chips: [],
  genre: '',
  sort: 'rating',
};

/** Page size for the V2 grid — kept in sync with SSR fetch (`pageSize: 100`). */
export const SHARED_GAMES_PAGE_SIZE = 100;

/**
 * Map the V2 `sort` token to the backend `sortBy` value.
 * `Title` is ascending by convention; everything else descending.
 */
const SORT_MAP: Record<SharedGamesSort, string> = {
  rating: 'AverageRating',
  contrib: 'Contrib',
  new: 'New',
  title: 'Title',
};

/**
 * Translate V2 state to the backend `SearchSharedGamesParams` shape.
 *
 * @param state - V2 state from the URL hash.
 * @param genreToCategoryIds - Resolver from genre slug to category id(s).
 *   Pass `undefined` when no resolver is wired yet — the genre filter is
 *   skipped in that case.
 */
export function buildSearchParams(
  state: SharedGamesState,
  genreToCategoryIds?: (slug: string) => string[] | undefined
): SearchSharedGamesParams {
  const categoryIdList =
    state.genre && genreToCategoryIds ? genreToCategoryIds(state.genre) : undefined;
  // Backend accepts a comma-separated GUID string; join only when non-empty.
  const categoryIds =
    categoryIdList && categoryIdList.length > 0 ? categoryIdList.join(',') : undefined;

  return {
    searchTerm: state.q || undefined,
    hasToolkit: state.chips.includes('tk') ? true : undefined,
    hasAgent: state.chips.includes('ag') ? true : undefined,
    isTopRated: state.chips.includes('top') ? true : undefined,
    isNew: state.chips.includes('new') ? true : undefined,
    categoryIds,
    sortBy: SORT_MAP[state.sort],
    sortDescending: state.sort !== 'title',
    pageSize: SHARED_GAMES_PAGE_SIZE,
    page: 1,
  };
}

/** Deep-equal check for the V2 state — order-sensitive on `chips`. */
function isDefaultsState(state: SharedGamesState): boolean {
  return (
    state.q === defaultsState.q &&
    state.genre === defaultsState.genre &&
    state.sort === defaultsState.sort &&
    state.chips.length === defaultsState.chips.length &&
    state.chips.every((chip, idx) => chip === defaultsState.chips[idx])
  );
}

/**
 * Query-key factory for the V2 search hook. The key depends on the *backend
 * params* rather than the raw state so that distinct UI states which produce
 * the same backend query share a cache entry.
 */
export const sharedGamesSearchKeys = {
  all: ['sharedGamesSearch'] as const,
  byParams: (params: SearchSharedGamesParams) => [...sharedGamesSearchKeys.all, params] as const,
};

export interface UseSharedGamesSearchOptions {
  /** SSR-fetched initial data. Honoured only if `state === defaultsState`. */
  initialData?: PagedSharedGames;
  /** Resolver from genre slug to backend category id(s). */
  genreToCategoryIds?: (slug: string) => string[] | undefined;
  /** Disable the query (default `true`). */
  enabled?: boolean;
}

/**
 * V2-flavoured shared games search.
 *
 * @param state - The current V2 filter/sort state.
 * @param opts - Optional initial data and genre resolver.
 */
export function useSharedGamesSearch(
  state: SharedGamesState,
  opts: UseSharedGamesSearchOptions = {}
): UseQueryResult<PagedSharedGames, Error> {
  const { initialData, genreToCategoryIds, enabled = true } = opts;
  const params = buildSearchParams(state, genreToCategoryIds);

  return useQuery({
    queryKey: sharedGamesSearchKeys.byParams(params),
    queryFn: async (): Promise<PagedSharedGames> => {
      return api.sharedGames.search(params);
    },
    enabled,
    placeholderData: keepPreviousData,
    initialData: isDefaultsState(state) ? initialData : undefined,
    // Catalog content changes infrequently — 5 minutes matches `useSharedGames`.
    staleTime: 5 * 60 * 1000,
  });
}
