/**
 * KB Docs Schemas (Issue #1592 Phase 2b)
 *
 * Zod schemas for the cross-game per-user KB documents listing endpoint
 * (BE-1 #1588): GET /api/v1/kb-docs?page=&pageSize=&sortBy=recent&state=ready|all.
 *
 * Matches `KbDocsListResponse` + `UserKbDocDto` from
 * apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/ListUserKbDocsQuery.cs.
 * The envelope field is `total` (NOT `totalCount`) — verified against the BE record.
 */

import { z } from 'zod';

/**
 * The 8 raw enum values of `PdfProcessingState` projected as strings by BE-1.
 * @see apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Enums/PdfProcessingState.cs
 */
export const ProcessingStateSchema = z.enum([
  'Pending',
  'Uploading',
  'Extracting',
  'Chunking',
  'Embedding',
  'Indexing',
  'Ready',
  'Failed',
]);

export type ProcessingState = z.infer<typeof ProcessingStateSchema>;

/**
 * UserKbDocDto — lightweight cross-game user-scoped projection (BE-1).
 * Does NOT include `filePath`, `fileSizeBytes`, `documentType`, etc. — see issue #1592.
 */
export const UserKbDocDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid().nullable(),
  gameName: z.string().nullable(),
  fileName: z.string().min(1),
  processingState: ProcessingStateSchema,
  pageCount: z.number().int().positive().nullable(),
  processedAt: z.string().datetime({ offset: true }).nullable(),
  uploadedAt: z.string().datetime({ offset: true }),
});

export type UserKbDocDto = z.infer<typeof UserKbDocDtoSchema>;

/**
 * KbDocsListResponse envelope — note `total`, not `totalCount`.
 */
export const KbDocsListResponseSchema = z
  .object({
    items: z.array(UserKbDocDtoSchema),
    total: z.number().int().nonnegative(),
    page: z.number().int().positive(),
    pageSize: z.number().int().positive(),
  })
  .strict();

export type KbDocsListResponse = z.infer<typeof KbDocsListResponseSchema>;
