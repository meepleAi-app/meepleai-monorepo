/**
 * useToolkitRatings — TanStack Query hook for the SP4 `/toolkits/[id]`
 * ratings tab (Wave 3 Phase 4b, Issue #805 / PR #732 §5.3.3).
 *
 * Backend contract: `GET /api/v1/toolkits/{toolkitId}/ratings?cursor=&limit=20`
 * returns `{ items, nextCursor, breakdown, averageStars, totalCount }`. Empty
 * state is a 200 with an empty stub envelope per the PR #732 §3.4 contract.
 *
 * Schema reality v1 carryover (Gate B): the `ToolkitRating` entity has not
 * shipped yet — the backend returns an empty stub once toolkit visibility
 * passes. The wire shape is stable so the FE can render today and adopt real
 * persistence in Phase 5 without a fetch shape change.
 *
 * Cache: backend caches 5min via HybridCache; FE staleTime mirrors that.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  ToolkitRatingsResponseSchema,
  type ToolkitRatingsResponse,
} from '@/lib/api/schemas/toolkit-ratings.schemas';

export const TOOLKIT_RATINGS_DEFAULT_LIMIT = 20;
export const TOOLKIT_RATINGS_STALE_TIME_MS = 5 * 60 * 1000; // 5min, mirrors backend cache

export const toolkitRatingsKeys = {
  all: ['toolkits', 'ratings'] as const,
  list: (toolkitId: string, cursor: string | null, limit: number) =>
    [...toolkitRatingsKeys.all, toolkitId, cursor ?? '_first', limit] as const,
};

function clampLimit(limit: number): number {
  if (!Number.isFinite(limit) || limit < 1) return TOOLKIT_RATINGS_DEFAULT_LIMIT;
  return Math.min(50, Math.floor(limit));
}

export interface UseToolkitRatingsOptions {
  readonly toolkitId: string | null | undefined;
  readonly cursor?: string | null;
  readonly limit?: number;
  readonly enabled?: boolean;
}

/**
 * Fetch toolkit ratings for the `/toolkits/[id]` ratings tab.
 *
 * @example
 * const { data } = useToolkitRatings({ toolkitId, limit: 20 });
 * // data?.items — ToolkitRating[] (empty array in v1 stub)
 * // data?.breakdown.star5 — distribution count (always 0 in v1)
 * // data?.averageStars — number (0 in v1)
 */
export function useToolkitRatings(
  options: UseToolkitRatingsOptions
): UseQueryResult<ToolkitRatingsResponse, Error> {
  const {
    toolkitId,
    cursor = null,
    limit = TOOLKIT_RATINGS_DEFAULT_LIMIT,
    enabled = true,
  } = options;
  const safeLimit = clampLimit(limit);

  return useQuery<ToolkitRatingsResponse, Error>({
    queryKey: toolkitRatingsKeys.list(toolkitId ?? '_disabled', cursor, safeLimit),
    queryFn: async () => {
      if (!toolkitId) {
        throw new Error('toolkitId is required');
      }
      const params = new URLSearchParams();
      params.set('limit', String(safeLimit));
      if (cursor) params.set('cursor', cursor);
      const response = await apiClient.get<ToolkitRatingsResponse>(
        `/api/v1/toolkits/${toolkitId}/ratings?${params.toString()}`,
        ToolkitRatingsResponseSchema
      );
      // apiClient returns null on 204; the backend guarantees a body for 200,
      // so non-null is the happy path. Fall back to an empty stub for safety.
      return (
        response ?? {
          items: [],
          nextCursor: null,
          breakdown: { star1: 0, star2: 0, star3: 0, star4: 0, star5: 0 },
          averageStars: 0,
          totalCount: 0,
        }
      );
    },
    enabled: enabled && !!toolkitId,
    staleTime: TOOLKIT_RATINGS_STALE_TIME_MS,
  });
}
