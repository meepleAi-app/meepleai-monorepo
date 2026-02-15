/**
 * useCatalogTrending Hook - Issue #3921
 *
 * React Query hook for fetching trending catalog games
 *
 * @see Issue #3918 - Backend Trending Analytics Service
 * @see Issue #3921 - Frontend: Catalog Trending Widget
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import type { TrendingGame } from '@/components/dashboard/CatalogTrending';
import { fetchCatalogTrending } from '@/lib/api/catalog';

/** 12 hours in milliseconds - matches backend cache TTL */
const TWELVE_HOURS_MS = 12 * 60 * 60 * 1000;

/**
 * Fetch and cache catalog trending games
 *
 * Features:
 * - 12-hour stale time (matches backend cache TTL)
 * - Auto-refresh every 12 hours
 * - Retry on failure (2 attempts)
 * - Background refetch on window focus
 */
export function useCatalogTrending(limit = 5): UseQueryResult<TrendingGame[], Error> {
  return useQuery({
    queryKey: ['catalog', 'trending', limit],
    queryFn: () => fetchCatalogTrending(limit),
    select: (data) =>
      data.map((g, i) => ({
        id: g.gameId,
        name: g.title,
        trend: g.percentageChange ?? 0,
        rank: i + 1,
        previousRank: i + 1,
        imageUrl: g.imageUrl,
      })),
    staleTime: TWELVE_HOURS_MS,
    refetchInterval: TWELVE_HOURS_MS,
    retry: 2,
    refetchOnWindowFocus: false,
  });
}
