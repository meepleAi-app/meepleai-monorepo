/**
 * useSharedGames — TanStack Query hook for /shared-games index search.
 *
 * Wave A.3b (Issue #596). Wraps `searchSharedGames()` from
 * `lib/api/shared-games.ts` with stable query keys and SSR-seed support.
 *
 * Contract (mirrors spec §3.2):
 *  - `queryKey: ['shared-games', { query, chips, genre, sort }]` — stable order
 *    (chips array sorted before keying) so React Query dedupes equivalent calls.
 *  - `initialData` from SSR seeds the cache; `staleTime: 60_000` aligns with
 *    backend HybridCache TTL (1 min) → no immediate background refetch on mount.
 *  - Filter-chip → backend-param mapping uses `FILTER_CHIPS` (single source of
 *    truth from `lib/shared-games/filters.ts`). Genre → categoryIds resolution
 *    happens upstream via `genreKeyToCategoryIds(genre, nameToId)` so the hook
 *    receives pre-resolved Guids.
 *  - `query` is expected to be already debounced by the caller (see
 *    `useDebounce` in `page-client.tsx`).
 */

'use client';

import { useQuery } from '@tanstack/react-query';

import {
  searchSharedGames,
  type PagedSharedGamesV2,
  type SearchSharedGamesArgs,
} from '@/lib/api/shared-games';
import { type ChipKey, type SortKey, sortKeyToBackendParams } from '@/lib/shared-games/filters';

const STALE_MS = 60_000;
const DEFAULT_PAGE_SIZE = 100;

export interface UseSharedGamesArgs {
  /** Free-text search; expected to be already debounced (300ms upstream). */
  readonly query: string;
  /** Active filter chips. Order-independent — hook sorts before query-keying. */
  readonly chips: readonly ChipKey[];
  /** Pre-resolved backend category Guids (caller maps genre key → ids). */
  readonly categoryIds: readonly string[];
  readonly sort: SortKey;
  /** SSR seed; passed through to React Query as `initialData`. */
  readonly initialData?: PagedSharedGamesV2;
  /** Pagination — defaults to single page of 100. */
  readonly pageNumber?: number;
  readonly pageSize?: number;
}

export interface UseSharedGamesResult {
  readonly data: PagedSharedGamesV2 | undefined;
  readonly isLoading: boolean;
  readonly isFetching: boolean;
  readonly isError: boolean;
  readonly error: Error | null;
  readonly refetch: () => Promise<void>;
}

function chipsToBackendArgs(chips: readonly ChipKey[]): Partial<SearchSharedGamesArgs> {
  // Build a mutable bag, then cast to the readonly partial when returning.
  const args: {
    hasToolkit?: boolean;
    hasAgent?: boolean;
    isTopRated?: boolean;
    isNew?: boolean;
  } = {};
  for (const chip of chips) {
    switch (chip) {
      case 'with-toolkit':
        args.hasToolkit = true;
        break;
      case 'with-agent':
        args.hasAgent = true;
        break;
      case 'top-rated':
        args.isTopRated = true;
        break;
      case 'new':
        args.isNew = true;
        break;
    }
  }
  return args;
}

export function useSharedGames(args: UseSharedGamesArgs): UseSharedGamesResult {
  const sortedChips = [...args.chips].sort();
  const sortedCategoryIds = [...args.categoryIds].sort();
  const sortParams = sortKeyToBackendParams(args.sort);

  const queryKey = [
    'shared-games',
    {
      query: args.query,
      chips: sortedChips,
      categoryIds: sortedCategoryIds,
      sort: args.sort,
      pageNumber: args.pageNumber ?? 1,
      pageSize: args.pageSize ?? DEFAULT_PAGE_SIZE,
    },
  ] as const;

  const result = useQuery<PagedSharedGamesV2, Error>({
    queryKey,
    queryFn: () =>
      searchSharedGames({
        search: args.query.length > 0 ? args.query : undefined,
        categoryIds: sortedCategoryIds.length > 0 ? sortedCategoryIds : undefined,
        ...chipsToBackendArgs(sortedChips),
        sortBy: sortParams.sortBy,
        sortDescending: sortParams.sortDescending,
        pageNumber: args.pageNumber ?? 1,
        pageSize: args.pageSize ?? DEFAULT_PAGE_SIZE,
      }),
    initialData: args.initialData,
    staleTime: STALE_MS,
  });

  const refetch = async (): Promise<void> => {
    await result.refetch();
  };

  return {
    data: result.data,
    isLoading: result.isLoading,
    isFetching: result.isFetching,
    isError: result.isError,
    error: result.error,
    refetch,
  };
}
