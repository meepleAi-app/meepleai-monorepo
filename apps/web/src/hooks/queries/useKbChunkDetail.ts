/**
 * useKbChunkDetail — TanStack Query hook for a single chunk's full content
 * (Wave 3 Phase 3, Issue #805 / PR #732 §6.3.3).
 *
 * Backend contract: `GET /api/v1/kb-docs/{id}/chunks/{chunkId}`.
 *  - 200: KbChunkDetail with sanitized markdown content + prev/nextChunkId
 *    navigation.
 *  - 404: chunk not found OR chunk belongs to a different document.
 *  - 403: private doc, non-owner, non-admin.
 *
 * Cache: backend 24h (chunks immutable post-ingest); FE staleTime mirrors.
 *
 * Markdown content is sanitized server-side to a strict subset:
 *  - H4-H6 demoted to **bold**.
 *  - Raw HTML tags stripped.
 *  - Images replaced with `[Image: alt]` placeholder.
 *  - Footnotes stripped.
 *
 * The FE can render `content` directly with a markdown library configured for
 * the same subset — no additional sanitization needed.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import { KbChunkDetailSchema, type KbChunkDetail } from '@/lib/api/schemas/kb-chunks.schemas';

export const KB_CHUNK_DETAIL_STALE_TIME_MS = 24 * 60 * 60 * 1000; // 24h, immutable post-ingest.

export const kbChunkDetailKeys = {
  all: ['kb', 'docs', 'chunk'] as const,
  byPair: (docId: string, chunkId: string) => [...kbChunkDetailKeys.all, docId, chunkId] as const,
};

export interface UseKbChunkDetailOptions {
  readonly docId: string | null | undefined;
  readonly chunkId: string | null | undefined;
  readonly enabled?: boolean;
}

/**
 * Fetch the full content of a single chunk. Returns `null` on 401/404.
 *
 * @example
 * const { data, isLoading } = useKbChunkDetail({ docId, chunkId });
 * if (data) renderMarkdown(data.content);
 */
export function useKbChunkDetail(
  options: UseKbChunkDetailOptions
): UseQueryResult<KbChunkDetail | null, Error> {
  const { docId, chunkId, enabled = true } = options;
  const isValid =
    typeof docId === 'string' &&
    docId.length > 0 &&
    typeof chunkId === 'string' &&
    chunkId.length > 0;

  return useQuery<KbChunkDetail | null, Error>({
    queryKey: isValid ? kbChunkDetailKeys.byPair(docId, chunkId) : kbChunkDetailKeys.all,
    queryFn: async () => {
      if (!isValid) return null;
      const response = await apiClient.get<KbChunkDetail>(
        `/kb-docs/${docId}/chunks/${chunkId}`,
        KbChunkDetailSchema
      );
      return response ?? null;
    },
    enabled: enabled && isValid,
    staleTime: KB_CHUNK_DETAIL_STALE_TIME_MS,
  });
}
