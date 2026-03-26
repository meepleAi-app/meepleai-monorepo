/**
 * BGG Search Hook
 * Issue #4164: Step 3 BGG Match
 * Issue #4167: Network retry logic and error handling
 *
 * React Query hook for searching BoardGameGeek games.
 * Features:
 * - Auto-search when query length >= 2
 * - 5-minute cache for performance
 * - Automatic retry (3 attempts) on network errors
 * - Error handling with toast notifications
 * - Pagination support
 */

import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';
import type { BggSearchResponse } from '@/lib/api/schemas/games.schemas';
import { isRetryableError } from '@/lib/retryUtils';

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
    retry: (failureCount, error) => {
      // Retry up to 3 times, but only for retryable errors
      if (failureCount >= 3) return false;
      return isRetryableError(error);
    },
    retryDelay: attemptIndex => {
      // Exponential backoff: 1s, 2s, 4s (capped at 8s)
      const delay = Math.min(1000 * 2 ** attemptIndex, 8000);
      toast.info(`Ricerca fallita. Nuovo tentativo... (${attemptIndex + 1}/3)`);
      return delay;
    },
  });
}
