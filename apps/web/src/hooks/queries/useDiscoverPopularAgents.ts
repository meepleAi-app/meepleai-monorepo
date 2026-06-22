/**
 * useDiscoverPopularAgents — TanStack Query hook for the /discover route's
 * "Popular agents" rail (Wave 3 Phase 1, Issue #805 / PR #732 §4.3.3).
 *
 * Backend contract: `GET /api/v1/agents/popular?limit={n}` returns
 * `{ items: PopularAgent[] }`. Empty state is a 200 with `{ items: [] }`
 * per the PR #732 §3.4 empty-state contract.
 *
 * Schema reality v1 carryover (Gate B): `installCount` is always 0 until
 * the AgentInstallation tracking surface lands. The wire shape is stable —
 * the rail can adopt the real metric without a fetch shape change.
 *
 * Cache: backend caches 15min via HybridCache; FE staleTime mirrors that.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  PopularAgentsResponseSchema,
  type PopularAgent,
  type PopularAgentsResponse,
} from '@/lib/api/schemas/discover.schemas';

export const DISCOVER_POPULAR_AGENTS_DEFAULT_LIMIT = 10;
export const DISCOVER_POPULAR_AGENTS_STALE_TIME_MS = 15 * 60 * 1000; // 15min, mirrors backend cache

export const discoverPopularAgentsKeys = {
  all: ['discover', 'popularAgents'] as const,
  list: (limit: number) => [...discoverPopularAgentsKeys.all, limit] as const,
};

function clampLimit(limit: number): number {
  if (!Number.isFinite(limit) || limit < 1) return DISCOVER_POPULAR_AGENTS_DEFAULT_LIMIT;
  return Math.min(50, Math.floor(limit));
}

export interface UseDiscoverPopularAgentsOptions {
  readonly limit?: number;
  readonly enabled?: boolean;
}

/**
 * Fetch the top agents for the /discover "Popular agents" rail.
 *
 * @example
 * const { data, isLoading } = useDiscoverPopularAgents({ limit: 8 });
 * // data — PopularAgent[] (empty array on empty state)
 */
export function useDiscoverPopularAgents(
  options: UseDiscoverPopularAgentsOptions = {}
): UseQueryResult<PopularAgent[], Error> {
  const { limit = DISCOVER_POPULAR_AGENTS_DEFAULT_LIMIT, enabled = true } = options;
  const safeLimit = clampLimit(limit);

  return useQuery<PopularAgent[], Error>({
    queryKey: discoverPopularAgentsKeys.list(safeLimit),
    queryFn: async () => {
      const response = await apiClient.get<PopularAgentsResponse>(
        `/api/v1/agents/popular?limit=${safeLimit}`,
        PopularAgentsResponseSchema
      );
      return response?.items ?? [];
    },
    enabled,
    staleTime: DISCOVER_POPULAR_AGENTS_STALE_TIME_MS,
  });
}
