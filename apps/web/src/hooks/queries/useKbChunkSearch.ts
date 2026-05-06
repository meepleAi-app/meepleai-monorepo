'use client';

/**
 * useKbChunkSearch — search chunks within a KB document (Issue #730 — G3)
 *
 * Calls POST /api/v1/kb-docs/{docId}/chunks/search.
 * Implemented as a mutation because each call carries a different query string.
 * Returns ranked list of matching chunks with snippets.
 */

import { useMutation } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { KbChunkSearchResult } from '@/lib/api/schemas/kb-document.schemas';

/**
 * Mutation hook for searching chunks within a KB document.
 *
 * Use `mutate` / `mutateAsync` passing `{ docId, query, skip?, take? }`.
 *
 * @returns UseMutationResult with ranked chunk matches
 *
 * @example
 * ```tsx
 * const { mutate: searchChunks, data, isPending } = useKbChunkSearch();
 *
 * searchChunks({ docId, query: 'setup rules', take: 10 });
 * ```
 */
export function useKbChunkSearch() {
  return useMutation<
    KbChunkSearchResult,
    Error,
    { docId: string; query: string; skip?: number; take?: number }
  >({
    mutationFn: ({ docId, query, skip, take }) =>
      api.knowledgeBase.searchKbChunks(docId, { query, skip, take }),
  });
}
