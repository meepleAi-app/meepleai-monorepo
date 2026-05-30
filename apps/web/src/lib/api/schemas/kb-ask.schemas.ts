/**
 * KB Ask SSE schemas (Issue #1482 Phase 2 Interactions)
 *
 * Wire format matches `apps/api/src/Api/Infrastructure/Serialization/SseJsonOptions.cs`:
 *   - camelCase property naming
 *   - NUMERIC enum values (NOT strings) — frontend MUST use z.literal(0/1/4/5/7)
 *   - Envelope shape: `{ type: number, data: {...}, timestamp?: string }`
 *
 * D-E (spec-panel 2026-05-30): NO `chunkId` / `chunkPosition` — BE Snippet shape is
 *   `(text, source, page, line, score)`. Deep-link uses `?docId=&page=` only.
 *
 * @see admin-mockups/design_files/sp4-kb-globale.jsx (Drawer 6a-6d)
 * @see apps/api/src/Api/Models/Contracts.cs:30 (Snippet) + 98 (RagStreamingEvent)
 * @see apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs
 */

import { z } from 'zod';

/**
 * Page-level citation (D-E: no chunkId; deep-link is `?docId=&page=`).
 * Built from BE `Snippet(text, source, page, line, score)`:
 *   - `source` field carries the `PdfDocumentId` as string (used as `docId`).
 */
export const KbCitationSchema = z.object({
  docId: z.string(),
  source: z.string(),
  page: z.number().int().nonnegative(),
  snippet: z.string(),
  score: z.number(),
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
