/**
 * useCatalogTrending — TanStack Query hook for trending catalog games.
 *
 * Wraps the catalog trending endpoint with caching.
 * Trending data is refreshed every 5 minutes.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { getTrendingGames, type TrendingGame } from '@/lib/api/catalog';

export const catalogTrendingKeys = {
  all: ['catalogTrending'] as const,
  list: (limit: number) => [...catalogTrendingKeys.all, limit] as const,
};

/**
 * Hook to fetch trending games from the shared catalog.
 *
 * @param limit - Number of trending games to fetch (default: 10)
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with TrendingGame array
 */
export function useCatalogTrending(
  limit = 10,
  enabled = true
): UseQueryResult<TrendingGame[], Error> {
  return useQuery({
    queryKey: catalogTrendingKeys.list(limit),
    queryFn: () => getTrendingGames(limit),
    enabled,
    staleTime: 5 * 60 * 1000, // Trending changes infrequently (5 min)
  });
}
