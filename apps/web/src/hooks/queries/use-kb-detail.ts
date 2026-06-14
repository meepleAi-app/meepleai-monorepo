/**
 * KB document detail query hooks (#2311 FE-2).
 *
 * TanStack Query wrappers over `kb-detail-api`. Each hook is gated on `id`
 * length so router-transition empty states don't fire spurious requests.
 *
 *   - `useKbDocument(id)` — eager fetch of document metadata
 *   - `useKbChunks(id, params)` — paginated chunks list (cursor-based)
 *   - `useKbChunk(docId, chunkId)` — lazy fetch of a single chunk's full body
 *     (enabled only when `chunkId` is non-empty so the preview pane defers
 *     until selection)
 *   - `useSearchKbChunks(docId, query)` — debounced full-text search within
 *     the document. The hook itself does NOT debounce — callers pass an
 *     already-debounced `query` so the hook key stays stable.
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import {
  fetchKbChunk,
  fetchKbChunks,
  fetchKbDocument,
  searchKbChunks,
  type KbChunkDetail,
  type KbChunkSearchResponse,
  type KbChunksListResponse,
  type KbChunksParams,
  type KbDocument,
} from '@/lib/api/kb-detail-api';

const DOCUMENT_STALE_MS = 5 * 60 * 1000;
const CHUNKS_STALE_MS = 60 * 1000;
const CHUNK_DETAIL_STALE_MS = 5 * 60 * 1000;
const SEARCH_STALE_MS = 30 * 1000;

export function useKbDocument(id: string): UseQueryResult<KbDocument, Error> {
  return useQuery({
    queryKey: ['kb-detail', 'document', id] as const,
    queryFn: () => fetchKbDocument(id),
    enabled: id.length > 0,
    staleTime: DOCUMENT_STALE_MS,
  });
}

export function useKbChunks(
  docId: string,
  params: KbChunksParams = {}
): UseQueryResult<KbChunksListResponse, Error> {
  return useQuery({
    queryKey: ['kb-detail', 'chunks', docId, params.limit ?? null, params.cursor ?? null] as const,
    queryFn: () => fetchKbChunks(docId, params),
    enabled: docId.length > 0,
    staleTime: CHUNKS_STALE_MS,
  });
}

export function useKbChunk(docId: string, chunkId: string): UseQueryResult<KbChunkDetail, Error> {
  return useQuery({
    queryKey: ['kb-detail', 'chunk', docId, chunkId] as const,
    queryFn: () => fetchKbChunk(docId, chunkId),
    enabled: docId.length > 0 && chunkId.length > 0,
    staleTime: CHUNK_DETAIL_STALE_MS,
  });
}

export interface UseSearchKbChunksArgs {
  readonly docId: string;
  /** Already-debounced search input; pass `''` to disable the query. */
  readonly query: string;
  readonly skip?: number;
  readonly take?: number;
}

export function useSearchKbChunks(
  args: UseSearchKbChunksArgs
): UseQueryResult<KbChunkSearchResponse, Error> {
  const { docId, query, skip, take } = args;
  return useQuery({
    queryKey: ['kb-detail', 'chunks-search', docId, query, skip ?? 0, take ?? 20] as const,
    queryFn: () =>
      searchKbChunks(docId, {
        query,
        skip,
        take,
      }),
    enabled: docId.length > 0 && query.trim().length > 0,
    staleTime: SEARCH_STALE_MS,
  });
}
