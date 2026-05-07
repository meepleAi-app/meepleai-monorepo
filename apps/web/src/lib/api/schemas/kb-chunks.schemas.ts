/**
 * /kb/[id] KB Chunks Schemas (Wave 3 Phase 3, Issue #805 / PR #732 §6.3)
 *
 * Zod schemas for the SP4 /kb/[id] split-view surface. Backend contract is
 * defined in PR #732 §6.3 verbatim:
 *   - §6.3.1 — GET /api/v1/kb-docs/{id}                        (KbDocDetail)
 *   - §6.3.2 — GET /api/v1/kb-docs/{id}/chunks?cursor=&limit=  (KbChunksList)
 *   - §6.3.3 — GET /api/v1/kb-docs/{id}/chunks/{chunkId}        (KbChunkDetail)
 *
 * Schema reality v1 carryovers (Gate B) — backend stubs documented inline:
 *   - tags: empty array (PdfDocumentEntity has no Tags column yet).
 *   - metadata: empty object (TextChunkEntity has no Metadata column yet).
 *
 * 423 Locked semantics (spec §6.3.1):
 *   - Returned when processingStatus !== 'ready'.
 *   - useKbDocDetail handles this status semantically (returns isLocked + status,
 *     rather than throwing) so the FE can render "Documento in elaborazione"
 *     instead of error-state.
 */

import { z } from 'zod';

// ========== Wire vocabularies (PR #732 §6.3.1) ==========

export const KbDocTypeSchema = z.enum(['rulebook', 'faq', 'errata', 'guide']);
export type KbDocType = z.infer<typeof KbDocTypeSchema>;

export const KbProcessingStatusSchema = z.enum(['queued', 'processing', 'ready', 'failed']);
export type KbProcessingStatus = z.infer<typeof KbProcessingStatusSchema>;

// ========== /api/v1/kb-docs/{id} (PR #732 §6.3.1) ==========

export const KbDocDetailSchema = z.object({
  id: z.string().uuid(),
  title: z.string().min(1),
  docType: KbDocTypeSchema,
  gameId: z.string().uuid().nullable(),
  gameName: z.string().nullable(),
  uploaderName: z.string().min(1),
  uploadedAt: z.string().datetime({ offset: true }),
  lastIngestedAt: z.string().datetime({ offset: true }),
  processingStatus: KbProcessingStatusSchema,
  chunkCount: z.number().int().nonnegative(),
  pageCount: z.number().int().nonnegative().nullable(),
  language: z.string().min(1),
  // Schema reality v1 carryover (Gate B): empty array until tags entity lands.
  tags: z.array(z.string()),
});
export type KbDocDetail = z.infer<typeof KbDocDetailSchema>;

// ========== /api/v1/kb-docs/{id}/chunks (PR #732 §6.3.2) ==========

export const KbChunkSummarySchema = z.object({
  id: z.string().uuid(),
  position: z.number().int().nonnegative(),
  headingPath: z.array(z.string()),
  snippet: z.string(),
  pageNumber: z.number().int().positive().nullable(),
  // Spec §6.3.2: vectorId always present, de-gated from admin per §3.5
  // versioning rationale (security review documented backend-side).
  vectorId: z.string().min(1),
});
export type KbChunkSummary = z.infer<typeof KbChunkSummarySchema>;

export const KbChunksListResponseSchema = z.object({
  items: z.array(KbChunkSummarySchema),
  nextCursor: z.string().nullable(),
  totalCount: z.number().int().nonnegative(),
});
export type KbChunksListResponse = z.infer<typeof KbChunksListResponseSchema>;

// ========== /api/v1/kb-docs/{id}/chunks/{chunkId} (PR #732 §6.3.3) ==========

export const KbChunkDetailSchema = z.object({
  id: z.string().uuid(),
  docId: z.string().uuid(),
  position: z.number().int().nonnegative(),
  headingPath: z.array(z.string()),
  // Spec markdown subset (server-enforced): H4-H6 demoted, raw HTML stripped,
  // images replaced with [Image: alt], footnotes stripped.
  content: z.string(),
  pageNumber: z.number().int().positive().nullable(),
  prevChunkId: z.string().uuid().nullable(),
  nextChunkId: z.string().uuid().nullable(),
  // Schema reality v1 carryover (Gate B): empty object until chunk metadata
  // entity lands.
  metadata: z.record(z.string(), z.unknown()),
});
export type KbChunkDetail = z.infer<typeof KbChunkDetailSchema>;

// ========== Locked-state envelope for useKbDocDetail ==========

/**
 * Hook-shaped result envelope for the doc-detail query.
 *
 * - Success (200): `{ status: 'ready', doc }`
 * - Locked (423): `{ status: 'locked', processingStatus, doc }` — the FE
 *   renders an "in elaborazione" panel rather than a hard error state.
 *
 * Implementation note: the backend embeds the partial DTO inside the 423
 * response message via the spec, but to keep the hook contract simple we
 * request the doc detail twice in the locked case (once to surface the 423,
 * once via the same payload returned in the LockedException). The hook
 * abstracts this so callers see a single non-throwing query result.
 */
export const KbDocLockedEnvelopeSchema = z.object({
  status: z.literal('locked'),
  processingStatus: KbProcessingStatusSchema,
  // The hook may surface a `null` doc when the 423 message-only path is taken.
  doc: KbDocDetailSchema.nullable(),
});
export type KbDocLockedEnvelope = z.infer<typeof KbDocLockedEnvelopeSchema>;

export const KbDocReadyEnvelopeSchema = z.object({
  status: z.literal('ready'),
  doc: KbDocDetailSchema,
});
export type KbDocReadyEnvelope = z.infer<typeof KbDocReadyEnvelopeSchema>;

export type KbDocEnvelope = KbDocReadyEnvelope | KbDocLockedEnvelope;
