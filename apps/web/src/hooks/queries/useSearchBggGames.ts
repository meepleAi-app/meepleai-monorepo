/**
 * BGG Search Hook
 * Issue #4164: Step 3 BGG Match
 *
 * React Query hook for searching BoardGameGeek games.
 * Features:
 * - Auto-search when query length >= 2
 * - 5-minute cache for performance
 * - Error handling
 * - Pagination support
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { BggSearchResponse } from '@/lib/api/schemas/games.schemas';

export interface UseSearchBggGamesOptions {
  /** Search query string */
  query: string;
  /** Use exact name matching (default: false) */
  exact?: boolean;
  /** Page number (default: 1) */
  page?: number;
  /** Results per page (default: 20, max: 100) */
  pageSize?: number;
  /** Enable query (default: query.length >= 2) */
  enabled?: boolean;
}

/**
 * Search BoardGameGeek games using React Query
 *
 * @example
 * const { data, isLoading, error } = useSearchBggGames({
 *   query: 'Catan',
 *   exact: false,
 * });
 */
export function useSearchBggGames({
  query,
  exact = false,
  page = 1,
  pageSize = 20,
  enabled,
}: UseSearchBggGamesOptions) {
  const shouldEnable = enabled !== undefined ? enabled : query.trim().length >= 2;

  return useQuery<BggSearchResponse, Error>({
    queryKey: ['bgg', 'search', query, exact, page, pageSize],
    queryFn: () => api.bgg.search(query, exact, page, pageSize),
    enabled: shouldEnable,
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 10 * 60 * 1000, // 10 minutes (previously cacheTime)
    retry: 2,
    retryDelay: attemptIndex => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}
