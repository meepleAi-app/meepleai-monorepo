/**
 * useDiscoverTopContributors — TanStack Query hook for the /discover route's
 * "Top contributors" rail (Wave 3 Phase 4a, Issue #805 / PR #732 §4.3.6).
 *
 * Backend contract: `GET /api/v1/users/top-contributors?limit={n}` returns
 * `{ items: TopUserContributor[] }`. Empty state is a 200 with `{ items: [] }`
 * per the PR #732 §3.4 empty-state contract.
 *
 * Distinct from the existing /api/v1/shared-games/top-contributors endpoint
 * (which scores by sessions + wins for the public /shared-games sidebar).
 * This surface aggregates contribution sources (FAQs authored, KB documents
 * uploaded, AI agents created) for the SP4 /discover community editors rail.
 *
 * Schema reality v1 carryovers (Gate B): `breakdown.faqsCount` and
 * `breakdown.agentsCreatedCount` are stubbed to 0 (no creator FK columns on
 * GameFaqEntity / AgentDefinition). Only `breakdown.kbUploadsCount` carries
 * a real signal in v1, and `contributionCount` therefore equals
 * `breakdown.kbUploadsCount`.
 *
 * Cache: backend caches 1h via HybridCache; FE staleTime mirrors that.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  TopUserContributorsResponseSchema,
  type TopUserContributor,
  type TopUserContributorsResponse,
} from '@/lib/api/schemas/discover-cross-cutting.schemas';

export const DISCOVER_TOP_CONTRIBUTORS_DEFAULT_LIMIT = 10;
export const DISCOVER_TOP_CONTRIBUTORS_STALE_TIME_MS = 60 * 60 * 1000; // 1h, mirrors backend cache

export const discoverTopContributorsKeys = {
  all: ['discover', 'topContributors'] as const,
  list: (limit: number) => [...discoverTopContributorsKeys.all, limit] as const,
};

function clampLimit(limit: number): number {
  if (!Number.isFinite(limit) || limit < 1) return DISCOVER_TOP_CONTRIBUTORS_DEFAULT_LIMIT;
  return Math.min(50, Math.floor(limit));
}

export interface UseDiscoverTopContributorsOptions {
  readonly limit?: number;
  readonly enabled?: boolean;
}

/**
 * Fetch the top user contributors for the /discover "Top contributors" rail.
 *
 * @example
 * const { data, isLoading } = useDiscoverTopContributors({ limit: 5 });
 * // data — TopUserContributor[] (empty array on empty state)
 */
export function useDiscoverTopContributors(
  options: UseDiscoverTopContributorsOptions = {}
): UseQueryResult<TopUserContributor[], Error> {
  const { limit = DISCOVER_TOP_CONTRIBUTORS_DEFAULT_LIMIT, enabled = true } = options;
  const safeLimit = clampLimit(limit);

  return useQuery<TopUserContributor[], Error>({
    queryKey: discoverTopContributorsKeys.list(safeLimit),
    queryFn: async () => {
      const response = await apiClient.get<TopUserContributorsResponse>(
        `/users/top-contributors?limit=${safeLimit}`,
        TopUserContributorsResponseSchema
      );
      return response?.items ?? [];
    },
    enabled,
    staleTime: DISCOVER_TOP_CONTRIBUTORS_STALE_TIME_MS,
  });
}
