/**
 * useToolkitVersions — TanStack Query hook for the /toolkits/[id]
 * versions list (Wave 3 Phase 2, Issue #805 / PR #732 §5.3.2).
 *
 * Backend contract: `GET /api/v1/toolkits/{toolkitId}/versions` returns
 * `{ items: ToolkitVersion[] }` sorted by `publishedAt DESC`. 404 when
 * the toolkit is missing or hidden from the viewer.
 *
 * Cache: backend caches 10min via HybridCache (no per-viewer partition);
 * FE staleTime mirrors that.
 *
 * Schema reality v1 carryover (Gate B): backend currently synthesises a
 * single-row stub list because there is no ToolkitVersion entity — wire
 * shape is stable so the FE list can render today and pick up multi-row
 * history once Phase 4 ships the schema.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  ToolkitVersionsResponseSchema,
  type ToolkitVersion,
  type ToolkitVersionsResponse,
} from '@/lib/api/schemas/toolkit-marketplace.schemas';

export const TOOLKIT_VERSIONS_STALE_TIME_MS = 10 * 60 * 1000; // 10 min, mirrors backend cache.

export const toolkitVersionsKeys = {
  all: ['toolkits', 'versions'] as const,
  byId: (toolkitId: string) => [...toolkitVersionsKeys.all, toolkitId] as const,
};

export interface UseToolkitVersionsOptions {
  readonly toolkitId: string | null | undefined;
  readonly enabled?: boolean;
}

/**
 * Fetch the published version history for a toolkit.
 *
 * @example
 * const { data, isLoading } = useToolkitVersions({ toolkitId: 'xxx' });
 * // data — ToolkitVersion[] (empty array when not visible)
 */
export function useToolkitVersions(
  options: UseToolkitVersionsOptions
): UseQueryResult<ToolkitVersion[], Error> {
  const { toolkitId, enabled = true } = options;
  const isValid = typeof toolkitId === 'string' && toolkitId.length > 0;

  return useQuery<ToolkitVersion[], Error>({
    queryKey: isValid ? toolkitVersionsKeys.byId(toolkitId) : toolkitVersionsKeys.all,
    queryFn: async () => {
      if (!isValid) return [];
      const response = await apiClient.get<ToolkitVersionsResponse>(
        `/toolkits/${toolkitId}/versions`,
        ToolkitVersionsResponseSchema
      );
      return response?.items ?? [];
    },
    enabled: enabled && isValid,
    staleTime: TOOLKIT_VERSIONS_STALE_TIME_MS,
  });
}
