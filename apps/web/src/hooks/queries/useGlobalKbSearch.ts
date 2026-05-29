/**
 * useGlobalKbSearch — TanStack Query useInfiniteQuery hook for cross-game KB search.
 *
 * Backend contract: `POST /api/v1/knowledge-base/search/global`.
 *  - Body: `{ query, limit?, cursor?, mode? }`
 *  - Response: `{ results, hasMore, nextCursor }`
 *
 * Gate: only fires when `query.trim().length >= 2` to avoid single-char noise.
 * Debounce: 250ms (override useDebounce default of 2000ms).
 * Cache: staleTime 5min (aligns with useUserKbDocs from Issue #1592).
 * Cursor: cursor-based pagination; queryKey change (query or mode) auto-resets cursor.
 * hasMore: computed from last page only (no totalCount cross-game per Nygard D6).
 *
 * @see apps/web/src/lib/api/clients/kbDocsClient.ts  — kbDocsClient.searchGlobal
 * @see apps/web/src/lib/api/schemas/kb-globale.schemas.ts  — Zod schemas (Task 0)
 * @see Issue #1482 Phase 1 Foundation
 */

import { useInfiniteQuery } from '@tanstack/react-query';

import { useDebounce } from '@/hooks/useDebounce';
import { api } from '@/lib/api';
import type {
  GlobalKbSearchResult,
  GlobalKbSearchResponse,
  SearchMode,
} from '@/lib/api/schemas/kb-globale.schemas';

export const KB_GLOBALE_SEARCH_STALE_TIME_MS = 5 * 60 * 1000; // 5 min.

/** Query key factory for cache coordination / invalidation. */
export const kbGlobaleSearchKeys = {
  all: ['kb-globale', 'search'] as const,
  byQuery: (query: string, mode: SearchMode | null) =>
    [...kbGlobaleSearchKeys.all, { query, mode }] as const,
};

export interface UseGlobalKbSearchOptions {
  /** The search query string (will be trimmed internally). */
  query: string;
  /** Search mode — defaults to BE-side Semantic when undefined. */
  mode?: SearchMode;
  /** Debounce delay in ms. Defaults to 250 (overrides useDebounce's 2000ms default). */
  debounceMs?: number;
  /** Override the auto-enable gate (`query.trim().length >= 2`). */
  enabled?: boolean;
}

export interface UseGlobalKbSearchResult {
  /** Flat list of results across all fetched pages. */
  results: readonly GlobalKbSearchResult[];
  /** Whether the last fetched page has more items (cursor pagination). */
  hasMore: boolean;
  /** True during the initial fetch (no cached data yet). */
  isLoading: boolean;
  /** True while fetching the next page. */
  isFetchingNextPage: boolean;
  /** The fetch error, if any; null otherwise. */
  error: Error | null;
  /** Trigger fetch of the next page (cursor forwarded automatically). */
  fetchNextPage: () => void;
}

/**
 * Infinite-query hook for cross-game KB semantic search.
 *
 * @example
 * ```tsx
 * const { results, hasMore, isLoading, fetchNextPage } = useGlobalKbSearch({ query });
 * ```
 */
export function useGlobalKbSearch(opts: UseGlobalKbSearchOptions): UseGlobalKbSearchResult {
  const { query, mode, debounceMs = 250, enabled } = opts;

  // Debounce the trimmed query to avoid firing on every keystroke.
  const debouncedQuery = useDebounce(query.trim(), debounceMs);

  // Auto-enable gate: fire only when there are at least 2 meaningful characters.
  const effectiveEnabled = enabled ?? debouncedQuery.length >= 2;

  const q = useInfiniteQuery<
    GlobalKbSearchResponse,
    Error,
    { pages: GlobalKbSearchResponse[] },
    ReturnType<typeof kbGlobaleSearchKeys.byQuery>,
    string | null
  >({
    queryKey: kbGlobaleSearchKeys.byQuery(debouncedQuery, mode ?? null),
    queryFn: ({ pageParam }) =>
      api.kbDocs.searchGlobal({
        query: debouncedQuery,
        mode,
        cursor: pageParam,
      }),
    initialPageParam: null,
    getNextPageParam: (lastPage: GlobalKbSearchResponse) =>
      lastPage.hasMore ? lastPage.nextCursor : undefined,
    enabled: effectiveEnabled,
    staleTime: KB_GLOBALE_SEARCH_STALE_TIME_MS,
  });

  // Flatten pages into a single results array.
  const results: readonly GlobalKbSearchResult[] = q.data?.pages.flatMap(p => p.results) ?? [];

  // hasMore is derived exclusively from the last page to avoid stale intermediate state.
  const pages = q.data?.pages;
  const hasMore =
    pages != null && pages.length > 0 ? (pages[pages.length - 1]?.hasMore ?? false) : false;

  return {
    results,
    hasMore,
    isLoading: q.isLoading,
    isFetchingNextPage: q.isFetchingNextPage,
    error: q.error,
    fetchNextPage: () => {
      void q.fetchNextPage();
    },
  };
}
