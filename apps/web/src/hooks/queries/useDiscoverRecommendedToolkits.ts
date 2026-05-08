/**
 * useDiscoverRecommendedToolkits — TanStack Query hook for the /discover route's
 * "Recommended toolkits" rail (Wave 3 Phase 4a, Issue #805 / PR #732 §4.3.4).
 *
 * Backend contract: `GET /api/v1/toolkits/recommended?limit={n}` returns
 * `{ items: RecommendedToolkit[] }`. Empty state is a 200 with `{ items: [] }`
 * per the PR #732 §3.4 empty-state contract.
 *
 * Schema reality v1 carryovers (Gate B): `installCount: 0`, `ratingAverage: null`,
 * `ratingCount: 0`, `coverImageUrl: null` until the rating + install entities ship
 * in Phase 4b. The wire shape is stable — the rail can adopt the real metrics
 * without a fetch shape change.
 *
 * Cache: backend caches 30min via HybridCache; FE staleTime mirrors that.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  RecommendedToolkitsResponseSchema,
  type RecommendedToolkit,
  type RecommendedToolkitsResponse,
} from '@/lib/api/schemas/discover-cross-cutting.schemas';

export const DISCOVER_RECOMMENDED_TOOLKITS_DEFAULT_LIMIT = 10;
export const DISCOVER_RECOMMENDED_TOOLKITS_STALE_TIME_MS = 30 * 60 * 1000; // 30min, mirrors backend cache

export const discoverRecommendedToolkitsKeys = {
  all: ['discover', 'recommendedToolkits'] as const,
  list: (limit: number) => [...discoverRecommendedToolkitsKeys.all, limit] as const,
};

function clampLimit(limit: number): number {
  if (!Number.isFinite(limit) || limit < 1) return DISCOVER_RECOMMENDED_TOOLKITS_DEFAULT_LIMIT;
  return Math.min(50, Math.floor(limit));
}

export interface UseDiscoverRecommendedToolkitsOptions {
  readonly limit?: number;
  readonly enabled?: boolean;
}

/**
 * Fetch the recommended toolkits for the /discover "Recommended toolkits" rail.
 *
 * @example
 * const { data, isLoading } = useDiscoverRecommendedToolkits({ limit: 8 });
 * // data — RecommendedToolkit[] (empty array on empty state)
 */
export function useDiscoverRecommendedToolkits(
  options: UseDiscoverRecommendedToolkitsOptions = {}
): UseQueryResult<RecommendedToolkit[], Error> {
  const { limit = DISCOVER_RECOMMENDED_TOOLKITS_DEFAULT_LIMIT, enabled = true } = options;
  const safeLimit = clampLimit(limit);

  return useQuery<RecommendedToolkit[], Error>({
    queryKey: discoverRecommendedToolkitsKeys.list(safeLimit),
    queryFn: async () => {
      const response = await apiClient.get<RecommendedToolkitsResponse>(
        `/toolkits/recommended?limit=${safeLimit}`,
        RecommendedToolkitsResponseSchema
      );
      return response?.items ?? [];
    },
    enabled,
    staleTime: DISCOVER_RECOMMENDED_TOOLKITS_STALE_TIME_MS,
  });
}
