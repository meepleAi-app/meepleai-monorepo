/**
 * @vitest-environment jsdom
 *
 * useKbDocActions unit tests — Issue #1653 Task 4 (TDD).
 *
 * Coverage:
 *   - useDeleteKbDoc: calls adminDeleteKbDoc(docId) and invalidates
 *     kbGameDocumentKeys.byGame(gameId) + kbDocDetailKeys.all.
 *   - useDeleteKbDoc with null gameId: still invalidates kbDocDetailKeys.all
 *     but skips game-tree invalidation.
 *   - useReindexDoc: calls reindexDocument(docId) and invalidates
 *     kbDocDetailKeys.byId(docId) + kbChunksListKeys.all.
 *   - useDocChunkSearch: calls searchDocChunks(docId, body) and returns results.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { kbDocDetailKeys } from '../useKbDocDetail';
import { kbChunksListKeys } from '../useKbChunksList';
import { kbGameDocumentKeys } from '../useGameDocuments';
import { useDeleteKbDoc, useReindexDoc, useDocChunkSearch } from '../useKbDocActions';

// ---------------------------------------------------------------------------
// Mock api module
// ---------------------------------------------------------------------------

const mockAdminDeleteKbDoc = vi.fn();
const mockReindexDocument = vi.fn();
const mockSearchDocChunks = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      adminDeleteKbDoc: (...args: unknown[]) => mockAdminDeleteKbDoc(...args),
      reindexDocument: (...args: unknown[]) => mockReindexDocument(...args),
      searchDocChunks: (...args: unknown[]) => mockSearchDocChunks(...args),
    },
  },
}));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const GAME_ID = 'game-aaaaaaaa-0000-0000-0000-000000000001';
const DOC_ID = 'doc-bbbbbbbb-0000-0000-0000-000000000002';

function makeWrapper() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return {
    qc,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={qc}>{children}</QueryClientProvider>
    ),
  };
}

describe('useKbDocActions (Issue #1653 T4)', () => {
  beforeEach(() => vi.clearAllMocks());

  // -------------------------------------------------------------------------
  // useDeleteKbDoc
  // -------------------------------------------------------------------------

  describe('useDeleteKbDoc', () => {
    it('calls adminDeleteKbDoc with the given docId', async () => {
      mockAdminDeleteKbDoc.mockResolvedValueOnce(undefined);
      const { wrapper } = makeWrapper();
      const { result } = renderHook(() => useDeleteKbDoc(GAME_ID), { wrapper });
      await result.current.mutateAsync(DOC_ID);
      expect(mockAdminDeleteKbDoc).toHaveBeenCalledWith(DOC_ID);
    });

    it('invalidates kbGameDocumentKeys.byGame and kbDocDetailKeys.all on success', async () => {
      mockAdminDeleteKbDoc.mockResolvedValueOnce(undefined);
      const { qc, wrapper } = makeWrapper();
      const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
      const { result } = renderHook(() => useDeleteKbDoc(GAME_ID), { wrapper });
      await result.current.mutateAsync(DOC_ID);
      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith(
        expect.objectContaining({ queryKey: kbGameDocumentKeys.byGame(GAME_ID) })
      );
      expect(invalidateSpy).toHaveBeenCalledWith(
        expect.objectContaining({ queryKey: kbDocDetailKeys.all })
      );
    });

    it('still invalidates kbDocDetailKeys.all when gameId is null (no game-tree invalidation)', async () => {
      mockAdminDeleteKbDoc.mockResolvedValueOnce(undefined);
      const { qc, wrapper } = makeWrapper();
      const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
      const { result } = renderHook(() => useDeleteKbDoc(null), { wrapper });
      await result.current.mutateAsync(DOC_ID);
      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith(
        expect.objectContaining({ queryKey: kbDocDetailKeys.all })
      );
      // Game-tree invalidation must NOT be called when gameId is null
      const gameKeyCallArgs = invalidateSpy.mock.calls.find(
        ([arg]) =>
          JSON.stringify((arg as { queryKey?: unknown }).queryKey) ===
          JSON.stringify(kbGameDocumentKeys.byGame(GAME_ID))
      );
      expect(gameKeyCallArgs).toBeUndefined();
    });
  });

  // -------------------------------------------------------------------------
  // useReindexDoc
  // -------------------------------------------------------------------------

  describe('useReindexDoc', () => {
    it('calls reindexDocument with the given docId', async () => {
      mockReindexDocument.mockResolvedValueOnce(undefined);
      const { wrapper } = makeWrapper();
      const { result } = renderHook(() => useReindexDoc(DOC_ID), { wrapper });
      await result.current.mutateAsync();
      expect(mockReindexDocument).toHaveBeenCalledWith(DOC_ID);
    });

    it('invalidates kbDocDetailKeys.byId(docId) and kbChunksListKeys.all on success', async () => {
      mockReindexDocument.mockResolvedValueOnce(undefined);
      const { qc, wrapper } = makeWrapper();
      const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
      const { result } = renderHook(() => useReindexDoc(DOC_ID), { wrapper });
      await result.current.mutateAsync();
      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith(
        expect.objectContaining({ queryKey: kbDocDetailKeys.byId(DOC_ID) })
      );
      expect(invalidateSpy).toHaveBeenCalledWith(
        expect.objectContaining({ queryKey: kbChunksListKeys.all })
      );
    });
  });

  // -------------------------------------------------------------------------
  // useDocChunkSearch
  // -------------------------------------------------------------------------

  describe('useDocChunkSearch', () => {
    const searchBody = { query: 'start player', topK: 5 };
    const searchResponse = {
      results: [
        { chunkIndex: 3, pageNumber: 7, score: 0.87, snippet: 'The start player takes...' },
      ],
      errorMessage: null,
    };

    it('calls searchDocChunks with docId and body', async () => {
      mockSearchDocChunks.mockResolvedValueOnce(searchResponse);
      const { wrapper } = makeWrapper();
      const { result } = renderHook(() => useDocChunkSearch(DOC_ID), { wrapper });
      await result.current.mutateAsync(searchBody);
      expect(mockSearchDocChunks).toHaveBeenCalledWith(DOC_ID, searchBody);
    });

    it('returns the search results from the mutation', async () => {
      mockSearchDocChunks.mockResolvedValueOnce(searchResponse);
      const { wrapper } = makeWrapper();
      const { result } = renderHook(() => useDocChunkSearch(DOC_ID), { wrapper });
      const data = await result.current.mutateAsync(searchBody);
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(data).toEqual(searchResponse);
      expect(result.current.data?.results[0]?.snippet).toBe('The start player takes...');
    });

    it('supports optional topK and minScore fields in body', async () => {
      const fullBody = { query: 'setup', topK: 10, minScore: 0.5 };
      mockSearchDocChunks.mockResolvedValueOnce({ results: [], errorMessage: null });
      const { wrapper } = makeWrapper();
      const { result } = renderHook(() => useDocChunkSearch(DOC_ID), { wrapper });
      await result.current.mutateAsync(fullBody);
      expect(mockSearchDocChunks).toHaveBeenCalledWith(DOC_ID, fullBody);
    });
  });
});
