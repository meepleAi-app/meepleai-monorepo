/**
 * useDiscoverRecentKbDocs — TanStack Query hook for the /discover route's
 * "Recent KB docs" rail (Wave 3 Phase 1, Issue #805 / PR #732 §4.3.5).
 *
 * Backend contract: `GET /api/v1/kb-docs/recent?limit={n}` returns
 * `{ items: RecentKbDoc[] }`. Empty state is a 200 with `{ items: [] }`
 * per the PR #732 §3.4 empty-state contract.
 *
 * Filter: backend only emits docs with `processingState == 'Ready'` —
 * in-flight ingests (Pending, Extracting, Failed, …) are excluded.
 *
 * Cache: backend caches 5min via HybridCache; FE staleTime mirrors that.
 * Shorter than New Games / Popular Agents because freshly indexed docs
 * benefit from quick visibility on the discover surface.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  RecentKbDocsResponseSchema,
  type RecentKbDoc,
  type RecentKbDocsResponse,
} from '@/lib/api/schemas/discover.schemas';

export const DISCOVER_RECENT_KB_DOCS_DEFAULT_LIMIT = 10;
export const DISCOVER_RECENT_KB_DOCS_STALE_TIME_MS = 5 * 60 * 1000; // 5min, mirrors backend cache

export const discoverRecentKbDocsKeys = {
  all: ['discover', 'recentKbDocs'] as const,
  list: (limit: number) => [...discoverRecentKbDocsKeys.all, limit] as const,
};

function clampLimit(limit: number): number {
  if (!Number.isFinite(limit) || limit < 1) return DISCOVER_RECENT_KB_DOCS_DEFAULT_LIMIT;
  return Math.min(50, Math.floor(limit));
}

export interface UseDiscoverRecentKbDocsOptions {
  readonly limit?: number;
  readonly enabled?: boolean;
}

/**
 * Fetch the most recently indexed KB documents for the /discover
 * "Recent KB docs" rail.
 *
 * @example
 * const { data, isLoading } = useDiscoverRecentKbDocs({ limit: 6 });
 * // data — RecentKbDoc[] (empty array on empty state)
 */
export function useDiscoverRecentKbDocs(
  options: UseDiscoverRecentKbDocsOptions = {}
): UseQueryResult<RecentKbDoc[], Error> {
  const { limit = DISCOVER_RECENT_KB_DOCS_DEFAULT_LIMIT, enabled = true } = options;
  const safeLimit = clampLimit(limit);

  return useQuery<RecentKbDoc[], Error>({
    queryKey: discoverRecentKbDocsKeys.list(safeLimit),
    queryFn: async () => {
      const response = await apiClient.get<RecentKbDocsResponse>(
        `/kb-docs/recent?limit=${safeLimit}`,
        RecentKbDocsResponseSchema
      );
      return response?.items ?? [];
    },
    enabled,
    staleTime: DISCOVER_RECENT_KB_DOCS_STALE_TIME_MS,
  });
}
