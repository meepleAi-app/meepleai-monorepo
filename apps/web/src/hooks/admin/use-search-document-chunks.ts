/**
 * Issue #1674 G.0 (FIX-2 post-review): TanStack mutation for per-doc chunks search.
 *
 * Wraps POST /api/v1/admin/kb/docs/{docId}/chunks/search.
 * Consumed by DocumentEmbeddingsDrawer search panel.
 *
 * FE hook was not shipped by FU-4 #1653 (BE-only). This file fills that gap.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  SearchDocumentChunksResultDtoSchema,
  type SearchDocumentChunksResultDto,
} from '@/lib/api/schemas/admin-kb-embeddings.schemas';

export interface SearchDocumentChunksInput {
  query: string;
  topK?: number;
  minScore?: number;
}

async function postSearchDocumentChunks(
  docId: string,
  input: SearchDocumentChunksInput,
  signal?: AbortSignal
): Promise<SearchDocumentChunksResultDto> {
  return apiClient.post<SearchDocumentChunksResultDto>(
    `/api/v1/admin/kb/docs/${docId}/chunks/search`,
    {
      query: input.query,
      topK: input.topK ?? 10,
      minScore: input.minScore ?? 0.0,
    },
    SearchDocumentChunksResultDtoSchema,
    { signal }
  );
}

export function useSearchDocumentChunks(
  docId: string | null
): UseMutationResult<SearchDocumentChunksResultDto, Error, SearchDocumentChunksInput> {
  return useMutation<SearchDocumentChunksResultDto, Error, SearchDocumentChunksInput>({
    mutationKey: ['admin', 'kb', 'docs', docId ?? 'noop', 'chunks', 'search'],
    mutationFn: input => {
      if (!docId) {
        return Promise.reject(new Error('docId is required'));
      }
      return postSearchDocumentChunks(docId, input);
    },
  });
}
