/**
 * KB document detail API client (#2311 FE-2).
 *
 * Wraps the 4 already-shipped backend endpoints (verified by the BE-1 audit
 * on 2026-06-14, see `KnowledgeBaseEndpoints.cs:1108-1156`):
 *
 *   - GET  /api/v1/kb-docs/{id}                     → KbDocumentDto
 *   - GET  /api/v1/kb-docs/{id}/chunks              → KbChunksListResponse
 *   - GET  /api/v1/kb-docs/{id}/chunks/{chunkId}    → KbChunkDetailDto
 *   - POST /api/v1/kb-docs/{id}/chunks/search       → KbChunkSearchResultDto
 *
 * Zod schemas mirror the BE DTOs verbatim. The chunk summary surfaces the
 * #2311 BE-1 `usedInChats` counter so the FE can render the "usato in N chat"
 * pill without N+1 lookups.
 */

import { z } from 'zod';

import { apiClient } from './client';

// ───────────────────────── Document detail ────────────────────────────────

export const kbDocumentSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  docType: z.string(),
  gameId: z.string().uuid().nullable(),
  gameName: z.string().nullable(),
  uploaderName: z.string(),
  uploadedAt: z.string(),
  lastIngestedAt: z.string(),
  processingStatus: z.string(),
  chunkCount: z.number().int().nonnegative(),
  pageCount: z.number().int().nonnegative().nullable(),
  language: z.string(),
  tags: z.array(z.string()).default([]),
  fileSize: z.number().nonnegative(),
  indexerVersion: z.string().nullable(),
});

export type KbDocument = z.infer<typeof kbDocumentSchema>;

export async function fetchKbDocument(id: string): Promise<KbDocument> {
  const result = await apiClient.get<KbDocument>(`/api/v1/kb-docs/${encodeURIComponent(id)}`);
  if (result == null) throw new Error('KB document not found');
  return kbDocumentSchema.parse(result);
}

// ───────────────────────── Chunks list ────────────────────────────────────

export const kbChunkSummarySchema = z.object({
  id: z.string().uuid(),
  position: z.number().int().nonnegative(),
  headingPath: z.array(z.string()).default([]),
  snippet: z.string(),
  pageNumber: z.number().int().nonnegative().nullable(),
  vectorId: z.string(),
  // #2311 BE-1 — propagated from text_chunks.usage_count (start-from-0 per DEC-D2).
  usedInChats: z.number().int().nonnegative(),
});

export const kbChunksListResponseSchema = z.object({
  items: z.array(kbChunkSummarySchema).default([]),
  nextCursor: z.string().nullable(),
  totalCount: z.number().int().nonnegative(),
});

export type KbChunkSummary = z.infer<typeof kbChunkSummarySchema>;
export type KbChunksListResponse = z.infer<typeof kbChunksListResponseSchema>;

export interface KbChunksParams {
  limit?: number;
  cursor?: string;
}

export async function fetchKbChunks(
  docId: string,
  params: KbChunksParams = {}
): Promise<KbChunksListResponse> {
  const qs = new URLSearchParams();
  if (params.limit !== undefined) qs.set('limit', params.limit.toString());
  if (params.cursor) qs.set('cursor', params.cursor);
  const query = qs.toString();
  const path = `/api/v1/kb-docs/${encodeURIComponent(docId)}/chunks${query ? `?${query}` : ''}`;
  const result = await apiClient.get<KbChunksListResponse>(path);
  if (result == null) {
    return { items: [], nextCursor: null, totalCount: 0 };
  }
  return kbChunksListResponseSchema.parse(result);
}

// ───────────────────────── Single chunk detail ────────────────────────────

export const kbChunkDetailSchema = z.object({
  id: z.string().uuid(),
  docId: z.string().uuid(),
  position: z.number().int().nonnegative(),
  headingPath: z.array(z.string()).default([]),
  content: z.string(),
  pageNumber: z.number().int().nonnegative().nullable(),
  prevChunkId: z.string().uuid().nullable(),
  nextChunkId: z.string().uuid().nullable(),
  metadata: z.record(z.string(), z.unknown()).default({}),
});

export type KbChunkDetail = z.infer<typeof kbChunkDetailSchema>;

export async function fetchKbChunk(docId: string, chunkId: string): Promise<KbChunkDetail> {
  const result = await apiClient.get<KbChunkDetail>(
    `/api/v1/kb-docs/${encodeURIComponent(docId)}/chunks/${encodeURIComponent(chunkId)}`
  );
  if (result == null) throw new Error('KB chunk not found');
  return kbChunkDetailSchema.parse(result);
}

// ───────────────────────── Chunk search ───────────────────────────────────

export const kbChunkMatchSchema = z.object({
  chunkId: z.string().uuid(),
  headingPath: z.array(z.string()).default([]),
  snippet: z.string(),
  rank: z.number(),
  pageNumber: z.number().int().nonnegative().nullable(),
  position: z.number().int().nonnegative(),
});

export const kbChunkSearchResponseSchema = z.object({
  matches: z.array(kbChunkMatchSchema).default([]),
  totalCount: z.number().int().nonnegative(),
  skip: z.number().int().nonnegative(),
  take: z.number().int().nonnegative(),
});

export type KbChunkMatch = z.infer<typeof kbChunkMatchSchema>;
export type KbChunkSearchResponse = z.infer<typeof kbChunkSearchResponseSchema>;

export interface KbChunkSearchParams {
  query: string;
  skip?: number;
  take?: number;
}

export async function searchKbChunks(
  docId: string,
  params: KbChunkSearchParams
): Promise<KbChunkSearchResponse> {
  const result = await apiClient.post<KbChunkSearchResponse>(
    `/api/v1/kb-docs/${encodeURIComponent(docId)}/chunks/search`,
    {
      query: params.query,
      skip: params.skip ?? 0,
      take: params.take ?? 20,
    }
  );
  if (result == null) {
    return { matches: [], totalCount: 0, skip: 0, take: 0 };
  }
  return kbChunkSearchResponseSchema.parse(result);
}
