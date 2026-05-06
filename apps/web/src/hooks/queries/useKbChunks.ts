'use client';

/**
 * useKbChunks — fetch paginated list of chunks for a KB document (Issue #730 — G1)
 *
 * Calls GET /api/v1/kb-docs/{docId}/chunks?skip=&take=.
 * Returns paginated chunk summaries with heading paths and snippets.
 */

import { useQuery, keepPreviousData } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { KbChunkList } from '@/lib/api/schemas/kb-document.schemas';

export const kbChunksKeys = {
  all: ['kb-chunks'] as const,
  list: (docId: string, skip: number, take: number) =>
    ['kb-chunks', docId, { skip, take }] as const,
} as const;

/**
 * Fetch paginated chunks for a KB document.
 *
 * @param docId - KB document UUID, or undefined to skip the query
 * @param skip - Number of chunks to skip (default: 0)
 * @param take - Max chunks to return (default: 50)
 * @param enabled - Additional enable flag (default: true)
 * @returns React Query result with paginated chunk list
 *
 * @example
 * ```tsx
 * const { data, isLoading } = useKbChunks(docId, 0, 50);
 * ```
 */
export function useKbChunks(
  docId: string | undefined,
  skip: number = 0,
  take: number = 50,
  enabled: boolean = true
) {
  return useQuery<KbChunkList | null, Error>({
    queryKey: kbChunksKeys.list(docId ?? '', skip, take),
    queryFn: () => api.knowledgeBase.getKbChunks(docId!, { skip, take }),
    enabled: enabled && !!docId,
    staleTime: 5 * 60_000, // 5 minutes — chunk list stable post-ingest
    gcTime: 10 * 60_000, // 10 minutes cache
    placeholderData: keepPreviousData,
  });
}
