/**
 * KB Document & Chunk API Schemas (Issue #730)
 *
 * Zod schemas for validating KB chunk endpoints (G1-G4):
 *   G1: GET /api/v1/kb-docs/{docId}
 *   G2: GET /api/v1/kb-docs/{docId}/chunks
 *   G3: GET /api/v1/kb-docs/{docId}/chunks/{chunkId}
 *   G4: POST /api/v1/kb-docs/{docId}/chunks/search
 */

import { z } from 'zod';

// ========== KB Document ==========

/**
 * Full KB document record.
 * Returned by GET /api/v1/kb-docs/{docId}
 */
export const kbDocumentSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  gameId: z.string().uuid().nullable().optional(),
  sharedGameId: z.string().uuid().nullable().optional(),
  documentCategory: z.string(),
  processingState: z.enum([
    'pending',
    'uploading',
    'extracting',
    'chunking',
    'embedding',
    'indexing',
    'ready',
    'failed',
  ]),
  totalChunks: z.number().int().nonnegative(),
  pageCount: z.number().int().nonnegative(),
  indexedAt: z.string().datetime({ offset: true }).nullable().optional(),
  uploadedAt: z.string().datetime({ offset: true }),
  language: z.string(),
  versionLabel: z.string().nullable().optional(),
  // Admin-only fields (omitted for regular users)
  processingError: z.string().nullable().optional(),
  retryCount: z.number().int().nullable().optional(),
  failedAtState: z.string().nullable().optional(),
});

export type KbDocument = z.infer<typeof kbDocumentSchema>;

// ========== KB Chunk Summary (list item) ==========

/**
 * Summary of a single KB chunk as returned in paginated lists.
 * Returned within KbChunkList.
 */
export const kbChunkSummarySchema = z.object({
  chunkId: z.string().uuid(),
  pageNumber: z.number().int().nullable().optional(),
  position: z.number().int().nonnegative(),
  level: z.number().int().min(0).max(2),
  headingPath: z.array(z.string()),
  snippet: z.string(),
  // Admin-only fields
  vectorId: z.string().uuid().nullable().optional(),
  characterCount: z.number().int().nullable().optional(),
  elementType: z.string().nullable().optional(),
  embeddingStatus: z.string().nullable().optional(),
});

export type KbChunkSummary = z.infer<typeof kbChunkSummarySchema>;

// ========== KB Chunk List (G2 response) ==========

/**
 * Paginated list of KB chunks.
 * Returned by GET /api/v1/kb-docs/{docId}/chunks
 */
export const kbChunkListSchema = z.object({
  chunks: z.array(kbChunkSummarySchema),
  totalCount: z.number().int().nonnegative(),
  skip: z.number().int().nonnegative(),
  take: z.number().int().positive(),
  hasMore: z.boolean(),
  processingState: z.string(),
});

export type KbChunkList = z.infer<typeof kbChunkListSchema>;

// ========== KB Chunk Detail (G3 response) ==========

/**
 * Full content of a single KB chunk with navigation pointers.
 * Returned by GET /api/v1/kb-docs/{docId}/chunks/{chunkId}
 */
export const kbChunkDetailSchema = z.object({
  chunkId: z.string().uuid(),
  content: z.string(),
  pageNumber: z.number().int().nullable().optional(),
  position: z.number().int().nonnegative(),
  level: z.number().int().min(0).max(2),
  headingPath: z.array(z.string()),
  prevChunkId: z.string().uuid().nullable().optional(),
  nextChunkId: z.string().uuid().nullable().optional(),
  // Admin-only fields
  vectorId: z.string().uuid().nullable().optional(),
  characterCount: z.number().int().nullable().optional(),
  elementType: z.string().nullable().optional(),
  embeddingStatus: z.string().nullable().optional(),
  parentChunkId: z.string().uuid().nullable().optional(),
});

export type KbChunkDetail = z.infer<typeof kbChunkDetailSchema>;

// ========== KB Chunk Search (G4 response) ==========

/**
 * Single match item within a chunk search result.
 */
export const kbChunkMatchSchema = z.object({
  chunkId: z.string().uuid(),
  headingPath: z.array(z.string()),
  snippet: z.string(),
  rank: z.number(),
  pageNumber: z.number().int().nullable().optional(),
  position: z.number().int().nonnegative(),
});

export type KbChunkMatch = z.infer<typeof kbChunkMatchSchema>;

/**
 * Paginated search result for chunk full-text / semantic search.
 * Returned by POST /api/v1/kb-docs/{docId}/chunks/search
 */
export const kbChunkSearchResultSchema = z.object({
  matches: z.array(kbChunkMatchSchema),
  totalCount: z.number().int().nonnegative(),
  skip: z.number().int().nonnegative(),
  take: z.number().int().positive(),
});

export type KbChunkSearchResult = z.infer<typeof kbChunkSearchResultSchema>;
