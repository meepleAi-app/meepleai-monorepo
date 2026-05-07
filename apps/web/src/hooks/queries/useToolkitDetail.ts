/**
 * useToolkitDetail — TanStack Query hook for the /toolkits/[id] hero
 * + meta sections (Wave 3 Phase 2, Issue #805 / PR #732 §5.3.1).
 *
 * Backend contract: `GET /api/v1/toolkits/{toolkitId}` returns
 * `{ toolkit, viewerContext }`. 404 when the toolkit is unpublished or
 * yanked AND the viewer is not the author (server-enforced security
 * boundary per PR #732 §5.2). 401 when unauthenticated.
 *
 * Cache: backend caches 10min via HybridCache (per-viewer ETag); FE
 * staleTime mirrors that to avoid wasteful re-fetches while users browse
 * the marketplace.
 *
 * Schema reality v1 carryovers documented in the schema file (Gate B):
 * installCount/ratingAverage/etc. are stubbed — wire shape is stable so
 * the hook contract survives the Phase 4 schema work without changes.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  ToolkitDetailResponseSchema,
  type ToolkitDetailResponse,
} from '@/lib/api/schemas/toolkit-marketplace.schemas';

export const TOOLKIT_DETAIL_STALE_TIME_MS = 10 * 60 * 1000; // 10 min, mirrors backend cache.

export const toolkitDetailKeys = {
  all: ['toolkits', 'detail'] as const,
  byId: (toolkitId: string) => [...toolkitDetailKeys.all, toolkitId] as const,
};

export interface UseToolkitDetailOptions {
  readonly toolkitId: string | null | undefined;
  readonly enabled?: boolean;
}

/**
 * Fetch the marketplace detail envelope for a single toolkit.
 *
 * @example
 * const { data, isLoading, isError } = useToolkitDetail({ toolkitId: 'xxx' });
 * // data — { toolkit: ToolkitDetail, viewerContext: ViewerContext } | null
 */
export function useToolkitDetail(
  options: UseToolkitDetailOptions
): UseQueryResult<ToolkitDetailResponse | null, Error> {
  const { toolkitId, enabled = true } = options;
  const isValid = typeof toolkitId === 'string' && toolkitId.length > 0;

  return useQuery<ToolkitDetailResponse | null, Error>({
    queryKey: isValid ? toolkitDetailKeys.byId(toolkitId) : toolkitDetailKeys.all,
    queryFn: async () => {
      if (!isValid) return null;
      const response = await apiClient.get<ToolkitDetailResponse>(
        `/toolkits/${toolkitId}`,
        ToolkitDetailResponseSchema
      );
      // null is returned by apiClient on 401/404/circuit-open — surface as
      // null so the FE can render the not-found state.
      return response ?? null;
    },
    enabled: enabled && isValid,
    staleTime: TOOLKIT_DETAIL_STALE_TIME_MS,
  });
}
