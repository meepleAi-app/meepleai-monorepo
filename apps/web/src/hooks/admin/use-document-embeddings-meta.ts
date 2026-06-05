/**
 * Issue #1674: TanStack Query hook for per-doc embeddings meta.
 *
 * Used by DocumentEmbeddingsDrawer to populate the meta-strip (Model · Dim ·
 * Total chunks · Indexed at). Disabled when drawer is closed (`enabled` flag).
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { getDocumentEmbeddingsMeta } from '@/lib/api/admin-kb-embeddings';
import type { DocumentEmbeddingsMetaDto } from '@/lib/api/schemas/admin-kb-embeddings.schemas';

export const documentEmbeddingsKeys = {
  all: ['admin', 'kb', 'embeddings'] as const,
  meta: (docId: string) => ['admin', 'kb', 'docs', docId, 'embeddings', 'meta'] as const,
};

const STALE_TIME_MS = 5 * 60 * 1000;
const GC_TIME_MS = 10 * 60 * 1000;

export function useDocumentEmbeddingsMeta(
  docId: string | null,
  enabled: boolean
): UseQueryResult<DocumentEmbeddingsMetaDto | null, Error> {
  const isValid = typeof docId === 'string' && docId.length > 0;
  return useQuery<DocumentEmbeddingsMetaDto | null, Error>({
    queryKey: isValid
      ? documentEmbeddingsKeys.meta(docId)
      : [...documentEmbeddingsKeys.all, 'noop'],
    queryFn: ({ signal }) => getDocumentEmbeddingsMeta(docId!, { signal }),
    enabled: enabled && isValid,
    staleTime: STALE_TIME_MS,
    gcTime: GC_TIME_MS,
    retry: 1,
  });
}
