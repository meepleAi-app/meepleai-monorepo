'use client';

/**
 * useKbChunk — fetch full content of a single KB chunk (Issue #730 — G2)
 *
 * Calls GET /api/v1/kb-docs/{docId}/chunks/{chunkId}.
 * Returns full chunk content with prev/next navigation pointers.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { KbChunkDetail } from '@/lib/api/schemas/kb-document.schemas';

export const kbChunkKeys = {
  all: ['kb-chunk'] as const,
  byId: (docId: string, chunkId: string) => ['kb-chunk', docId, chunkId] as const,
} as const;

/**
 * Fetch a single KB chunk by document and chunk UUID.
 *
 * @param docId - KB document UUID, or undefined to skip the query
 * @param chunkId - Chunk UUID, or undefined to skip the query
 * @param enabled - Additional enable flag (default: true)
 * @returns React Query result with full chunk content and navigation pointers
 *
 * @example
 * ```tsx
 * const { data: chunk, isLoading } = useKbChunk(docId, chunkId);
 * ```
 */
export function useKbChunk(
  docId: string | undefined,
  chunkId: string | undefined,
  enabled: boolean = true
) {
  return useQuery<KbChunkDetail | null, Error>({
    queryKey: kbChunkKeys.byId(docId ?? '', chunkId ?? ''),
    queryFn: () => api.knowledgeBase.getKbChunk(docId!, chunkId!),
    enabled: enabled && !!docId && !!chunkId,
    staleTime: 10 * 60_000, // 10 minutes — chunk content immutable post-ingest
    gcTime: 30 * 60_000, // 30 minutes cache
  });
}
