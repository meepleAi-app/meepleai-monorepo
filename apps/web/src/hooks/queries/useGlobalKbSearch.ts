import { useInfiniteQuery } from '@tanstack/react-query';

import { useDebounce } from '@/hooks/useDebounce';
import { api } from '@/lib/api';
import type {
  GlobalKbSearchFilters,
  GlobalKbSearchResult,
  GlobalKbSearchResponse,
  SearchMode,
} from '@/lib/api/schemas/kb-globale.schemas';

export const KB_GLOBALE_SEARCH_STALE_TIME_MS = 5 * 60 * 1000;

/** Stable serialized form of filters for queryKey identity. */
function serializeFilters(filters?: GlobalKbSearchFilters): string {
  if (!filters) return '';
  const parts: string[] = [];
  if (filters.docType && filters.docType.length > 0) {
    parts.push(`docType:${[...filters.docType].sort().join(',')}`);
  }
  if (filters.gameId && filters.gameId.length > 0) {
    parts.push(`gameId:${[...filters.gameId].sort().join(',')}`);
  }
  if (filters.language) parts.push(`language:${filters.language}`);
  return parts.join('|');
}

export const kbGlobaleSearchKeys = {
  all: ['kb-globale', 'search'] as const,
  byQuery: (query: string, mode: SearchMode | null, filtersKey: string) =>
    [...kbGlobaleSearchKeys.all, { query, mode, filtersKey }] as const,
};

export interface UseGlobalKbSearchOptions {
  query: string;
  mode?: SearchMode;
  debounceMs?: number;
  enabled?: boolean;
  /** Phase 3 (#1737): optional server-side facet filters. */
  filters?: GlobalKbSearchFilters;
}

export interface UseGlobalKbSearchResult {
  results: readonly GlobalKbSearchResult[];
  hasMore: boolean;
  isLoading: boolean;
  isFetchingNextPage: boolean;
  error: Error | null;
  fetchNextPage: () => void;
}

export function useGlobalKbSearch(opts: UseGlobalKbSearchOptions): UseGlobalKbSearchResult {
  const { query, mode, debounceMs = 250, enabled, filters } = opts;

  const debouncedQuery = useDebounce(query.trim(), debounceMs);
  const effectiveEnabled = enabled ?? debouncedQuery.length >= 2;
  const filtersKey = serializeFilters(filters);

  const q = useInfiniteQuery<
    GlobalKbSearchResponse,
    Error,
    { pages: GlobalKbSearchResponse[] },
    ReturnType<typeof kbGlobaleSearchKeys.byQuery>,
    string | null
  >({
    queryKey: kbGlobaleSearchKeys.byQuery(debouncedQuery, mode ?? null, filtersKey),
    queryFn: ({ pageParam }) =>
      api.kbDocs.searchGlobal({
        query: debouncedQuery,
        mode,
        cursor: pageParam,
        // Phase 3: only include non-empty filters to keep wire format clean.
        ...(filters?.docType && filters.docType.length > 0
          ? { docType: [...filters.docType] }
          : {}),
        ...(filters?.gameId && filters.gameId.length > 0 ? { gameId: [...filters.gameId] } : {}),
        ...(filters?.language ? { language: filters.language } : {}),
      }),
    initialPageParam: null,
    getNextPageParam: (lastPage: GlobalKbSearchResponse) =>
      lastPage.hasMore ? lastPage.nextCursor : undefined,
    enabled: effectiveEnabled,
    staleTime: KB_GLOBALE_SEARCH_STALE_TIME_MS,
  });

  const results: readonly GlobalKbSearchResult[] = q.data?.pages.flatMap(p => p.results) ?? [];
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
