/**
 * Knowledge Base Status API Schemas (Issue #4065)
 *
 * Zod schemas for validating Knowledge Base embedding status responses.
 * Used by useEmbeddingStatus hook for RAG readiness polling.
 */

import { z } from 'zod';

// ========== Embedding Status Enum ==========

export const EmbeddingStatusSchema = z.enum([
  'Pending',
  'Extracting',
  'Chunking',
  'Embedding',
  'Completed',
  'Failed',
]);

export type EmbeddingStatus = z.infer<typeof EmbeddingStatusSchema>;

// ========== KB Status Response ==========

/**
 * Knowledge Base embedding status response.
 * Returned by GET /api/v1/knowledge-base/{gameId}/status
 */
export const KnowledgeBaseStatusSchema = z.object({
  /** Current embedding pipeline status */
  status: EmbeddingStatusSchema,
  /** Overall progress percentage (0-100) */
  progress: z.number().min(0).max(100),
  /** Total number of chunks to process */
  totalChunks: z.number().int().nonnegative(),
  /** Number of chunks processed so far */
  processedChunks: z.number().int().nonnegative(),
  /** Error message if status is Failed */
  errorMessage: z.string().nullable().optional(),
  /** Game name for notification display */
  gameName: z.string().optional(),
});

export type KnowledgeBaseStatus = z.infer<typeof KnowledgeBaseStatusSchema>;
