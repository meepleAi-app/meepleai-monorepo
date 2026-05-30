/**
 * KB Ask SSE schemas (Issue #1482 Phase 2 Interactions)
 *
 * Wire format matches `apps/api/src/Api/Infrastructure/Serialization/SseJsonOptions.cs`:
 *   - camelCase property naming
 *   - NUMERIC enum values (NOT strings) — frontend MUST use z.literal(0/1/4/5/7)
 *   - Envelope shape: `{ type: number, data: {...}, timestamp?: string }`
 *
 * Updated 2026-05-31 (#1702 FE follow-up): Schema extended with optional `chunkId` / `chunkPosition`
 *   from BE Snippet. Backward-compatible with per-game endpoints that don't set the fields.
 *   Deep-link uses `?docId=&page=` for legacy, `?docId=&page=&chunkId=` for chunk-level.
 *
 * @see admin-mockups/design_files/sp4-kb-globale.jsx (Drawer 6a-6d)
 * @see apps/api/src/Api/Models/Contracts.cs:30 (Snippet) + 98 (RagStreamingEvent)
 * @see apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs
 */

import { z } from 'zod';

/**
 * Citation extracted from BE Snippet. Supports both page-level (legacy, no chunkId)
 * and chunk-level (new, with chunkId) deep-links.
 * Built from BE `Snippet(text, source, page, line, score, chunkId?, chunkPosition?)`:
 *   - `source` field carries the `PdfDocumentId` as string (used as `docId`).
 *   - `chunkId` + `chunkPosition` optional, set only by /ask/global cross-game retrieval.
 */
export const KbCitationSchema = z.object({
  docId: z.string(),
  source: z.string(),
  page: z.number().int().nonnegative(),
  snippet: z.string(),
  score: z.number(),
  /**
   * Chunk-level deep-link identifier (#1702). Composite "{docId}_{chunkIndex}" from
   * MultiGameSearchResultItem.ChunkId on the BE. Only set by /ask/global cross-game
   * retrieval; absent from per-game endpoints. When present, FE can resolve via
   * useKbChunkDetail and navigate to the chunk's exact page. Graceful degrade to
   * page-level when null/unresolvable.
   */
  chunkId: z.string().optional(),
  /**
   * Zero-based chunk index within the document (#1702). Mirrors TextChunkEntity.ChunkIndex.
   * Only present alongside chunkId.
   */
  chunkPosition: z.number().int().nonnegative().optional(),
});
export type KbCitation = z.infer<typeof KbCitationSchema>;

/** Event types (mirror BE StreamingEventType enum — numeric values).
 *  Only the subset emitted by `/ask/global` is parsed here. */
export const KbAskEventType = {
  StateUpdate: 0,
  Citations: 1,
  Complete: 4,
  Error: 5,
  Token: 7,
} as const;

/** Discriminated union — Zod validates per-type `data` payload. */
export const KbAskEventSchema = z.discriminatedUnion('type', [
  z.object({
    type: z.literal(0),
    data: z.object({
      message: z.string(),
      chatThreadId: z.string().nullable().optional(),
    }),
    timestamp: z.string().optional(),
  }),
  z.object({
    type: z.literal(1),
    data: z.object({
      citations: z.array(KbCitationSchema),
    }),
    timestamp: z.string().optional(),
  }),
  z.object({
    type: z.literal(4),
    data: z.object({
      totalTokens: z.number(),
      promptTokens: z.number().nullable().optional(),
      completionTokens: z.number().nullable().optional(),
      estimatedReadingTimeMinutes: z.number().nullable().optional(),
      confidence: z.number().nullable().optional(),
    }),
    timestamp: z.string().optional(),
  }),
  z.object({
    type: z.literal(5),
    data: z.object({
      message: z.string(),
      code: z.string(),
    }),
    timestamp: z.string().optional(),
  }),
  z.object({
    type: z.literal(7),
    data: z.object({
      token: z.string(),
    }),
    timestamp: z.string().optional(),
  }),
]);
export type KbAskEvent = z.infer<typeof KbAskEventSchema>;

/** Request body for POST /api/v1/knowledge-base/ask/global. */
export const KbAskRequestSchema = z.object({
  query: z.string().min(1),
  language: z.string().optional(),
  topK: z.number().int().positive().optional(),
});
export type KbAskRequest = z.infer<typeof KbAskRequestSchema>;
