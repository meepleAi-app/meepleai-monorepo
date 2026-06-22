/**
 * useKbChunksList — TanStack Query useInfiniteQuery hook for the /kb/[id]
 * chunks list (Wave 3 Phase 3, Issue #805 / PR #732 §6.3.2).
 *
 * Backend contract: `GET /api/v1/kb-docs/{id}/chunks?cursor=&limit=`.
 *  - 200: KbChunksListResponse with `items`, `nextCursor` (null on last page),
 *    `totalCount`.
 *  - 400: malformed cursor (apiClient throws).
 *  - 404: doc not found.
 *  - 403: private doc, non-owner, non-admin.
 *
 * Cache: backend 30min HybridCache; FE staleTime mirrors that. Cursor is
 * opaque — pass `nextCursor` from the previous page to fetch the next.
 */

import {
  useInfiniteQuery,
  type InfiniteData,
  type UseInfiniteQueryResult,
} from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  KbChunksListResponseSchema,
  type KbChunksListResponse,
} from '@/lib/api/schemas/kb-chunks.schemas';

export const KB_CHUNKS_LIST_STALE_TIME_MS = 30 * 60 * 1000; // 30 min.

export const kbChunksListKeys = {
  all: ['kb', 'docs', 'chunks'] as const,
  byDoc: (docId: string, limit: number) => [...kbChunksListKeys.all, docId, limit] as const,
};

export interface UseKbChunksListOptions {
  readonly docId: string | null | undefined;
  readonly limit?: number;
  readonly enabled?: boolean;
}

const DEFAULT_LIMIT = 50;

/**
 * Fetch the cursor-paginated chunks list for a single KB document.
 *
 * @example
 * const { data, fetchNextPage, hasNextPage, isFetchingNextPage } =
 *   useKbChunksList({ docId: 'xxx' });
 * const chunks = data?.pages.flatMap(p => p.items) ?? [];
 */
export function useKbChunksList(
  options: UseKbChunksListOptions
): UseInfiniteQueryResult<InfiniteData<KbChunksListResponse, string | null>, Error> {
  const { docId, limit = DEFAULT_LIMIT, enabled = true } = options;
  const isValid = typeof docId === 'string' && docId.length > 0;
  const safeDocId = isValid ? docId : 'noop';

  return useInfiniteQuery<
    KbChunksListResponse,
    Error,
    InfiniteData<KbChunksListResponse, string | null>,
    readonly unknown[],
    string | null
  >({
    queryKey: isValid ? kbChunksListKeys.byDoc(safeDocId, limit) : kbChunksListKeys.all,
    initialPageParam: null,
    queryFn: async ({ pageParam }) => {
      const params = new URLSearchParams();
      params.set('limit', String(limit));
      if (pageParam) params.set('cursor', pageParam);
      const path = `/api/v1/kb-docs/${safeDocId}/chunks?${params.toString()}`;
      const response = await apiClient.get<KbChunksListResponse>(path, KbChunksListResponseSchema);
      // apiClient may return null on 401 — translate to an empty page so the
      // infinite query terminates cleanly without leaking a non-throwing null.
      return (
        response ?? {
          items: [],
          nextCursor: null,
          totalCount: 0,
        }
      );
    },
    getNextPageParam: lastPage => lastPage.nextCursor,
    enabled: enabled && isValid,
    staleTime: KB_CHUNKS_LIST_STALE_TIME_MS,
  });
}
