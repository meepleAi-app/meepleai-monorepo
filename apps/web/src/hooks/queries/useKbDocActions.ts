/**
 * useKbDocActions — TanStack Query mutation hooks for KB document admin actions
 * (Issue #1653 F3-FU-4 Task 4).
 *
 * Hooks:
 *   - useDeleteKbDoc(gameId)  — DELETE /api/v1/admin/pdfs/{docId} (cascade)
 *   - useReindexDoc(docId)    — POST /api/v1/admin/pdfs/{docId}/reindex
 *   - useDocChunkSearch(docId)— POST /api/v1/admin/kb/docs/{docId}/chunks/search
 *
 * Cache invalidation strategy:
 *   - Delete:  kbGameDocumentKeys.byGame(gameId) + kbDocDetailKeys.all
 *   - Reindex: kbDocDetailKeys.byId(docId)       + kbChunksListKeys.all
 *   - Search:  mutation only — no cache side-effect needed
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { DocChunkSearchResult } from '@/lib/api/clients/pdfClient';

import { kbGameDocumentKeys } from './useGameDocuments';
import { kbChunksListKeys } from './useKbChunksList';
import { kbDocDetailKeys } from './useKbDocDetail';

export type { DocChunkSearchResult } from '@/lib/api/clients/pdfClient';

// ---------------------------------------------------------------------------
// useDeleteKbDoc
// ---------------------------------------------------------------------------

/**
 * Delete a KB document with cascade (admin).
 *
 * @param gameId - Game UUID to invalidate the game-document tree after deletion,
 *   or null when the caller does not know / care about the parent game scope.
 *
 * @example
 * const { mutateAsync } = useDeleteKbDoc(gameId);
 * await mutateAsync(docId);
 */
export function useDeleteKbDoc(gameId: string | null): UseMutationResult<void, Error, string> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (docId: string) => api.pdf.adminDeleteKbDoc(docId),
    onSuccess: () => {
      if (gameId) {
        qc.invalidateQueries({ queryKey: kbGameDocumentKeys.byGame(gameId) });
      }
      qc.invalidateQueries({ queryKey: kbDocDetailKeys.all });
    },
  });
}

// ---------------------------------------------------------------------------
// useReindexDoc
// ---------------------------------------------------------------------------

/**
 * Trigger a reindex for a specific document (admin).
 * Invalidates the detail view and the full chunks list for this doc.
 *
 * @example
 * const { mutateAsync } = useReindexDoc(docId);
 * await mutateAsync();
 */
export function useReindexDoc(docId: string): UseMutationResult<void, Error, void> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.pdf.reindexDocument(docId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: kbDocDetailKeys.byId(docId) });
      qc.invalidateQueries({ queryKey: kbChunksListKeys.all });
    },
  });
}

// ---------------------------------------------------------------------------
// useDocChunkSearch
// ---------------------------------------------------------------------------

export interface DocChunkSearchBody {
  query: string;
  topK?: number;
  minScore?: number;
}

/**
 * Run a scored similarity search against a document's embedded chunks (admin).
 * Fire-and-forget mutation — results are returned via `data` / `mutateAsync`.
 * No cache invalidation needed; each search is independent.
 *
 * @example
 * const { mutateAsync, data } = useDocChunkSearch(docId);
 * const result = await mutateAsync({ query: 'start player', topK: 5 });
 */
export function useDocChunkSearch(
  docId: string
): UseMutationResult<DocChunkSearchResult, Error, DocChunkSearchBody> {
  return useMutation({
    mutationFn: (body: DocChunkSearchBody) => api.pdf.searchDocChunks(docId, body),
  });
}
