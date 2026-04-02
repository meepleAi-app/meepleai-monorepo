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

// ========== RAG Config Schemas (Issue #5311) ==========

export const GenerationParamsSchema = z.object({
  temperature: z.number().min(0).max(2),
  topK: z.number().int().positive(),
  topP: z.number().min(0).max(1),
  maxTokens: z.number().int().positive(),
});

export const RetrievalParamsSchema = z.object({
  chunkSize: z.number().int().positive(),
  chunkOverlap: z.number().int().min(0).max(50),
  topResults: z.number().int().positive(),
  similarityThreshold: z.number().min(0).max(1),
});

export const RerankerSettingsSchema = z.object({
  enabled: z.boolean(),
  model: z.string(),
  topN: z.number().int().positive(),
});

export const ModelSelectionSchema = z.object({
  primaryModel: z.string(),
  fallbackModel: z.string().nullable(),
  evaluationModel: z.string().nullable(),
});

export const StrategySpecificSettingsSchema = z.object({
  hybridAlpha: z.number().min(0).max(1),
  contextWindow: z.number().int().positive(),
  maxHops: z.number().int().positive(),
});

export const RagConfigSchema = z.object({
  generation: GenerationParamsSchema,
  retrieval: RetrievalParamsSchema,
  reranker: RerankerSettingsSchema,
  models: ModelSelectionSchema,
  strategySpecific: StrategySpecificSettingsSchema,
  activeStrategy: z.string(),
});

export type RagConfigResponse = z.infer<typeof RagConfigSchema>;

// ========== KB Management Schemas (KB-01/02/03/06/08/10) ==========

/**
 * User-facing knowledge base status for a game.
 * Returned by GET /api/v1/games/{gameId}/knowledge-base
 */
export const UserGameKbStatusSchema = z.object({
  gameId: z.string(),
  isIndexed: z.boolean(),
  documentCount: z.number().int().nonnegative(),
  coverageScore: z.number().min(0).max(100),
  coverageLevel: z.enum(['None', 'Basic', 'Standard', 'Complete']),
  suggestedQuestions: z.array(z.string()),
});

export type UserGameKbStatus = z.infer<typeof UserGameKbStatusSchema>;

/**
 * Admin view of indexed documents for a game's KB.
 * Returned by GET /api/v1/admin/kb/games/{gameId}/documents
 */
export const AdminKbDocumentSchema = z.object({
  id: z.string(),
  pdfDocumentId: z.string(),
  language: z.string(),
  chunkCount: z.number().int().nonnegative(),
  indexingStatus: z.string(),
  indexedAt: z.string(),
  sharedGameId: z.string().optional(),
});

export const AdminGameKbDocumentsSchema = z.object({
  gameId: z.string(),
  documents: z.array(AdminKbDocumentSchema),
});

export type AdminGameKbDocuments = z.infer<typeof AdminGameKbDocumentsSchema>;

/**
 * Admin feedback list for a game's KB.
 * Returned by GET /api/v1/admin/kb/games/{gameId}/feedback
 */
export const AdminKbFeedbackItemSchema = z.object({
  id: z.string(),
  userId: z.string(),
  gameId: z.string(),
  chatSessionId: z.string(),
  messageId: z.string(),
  outcome: z.string(),
  comment: z.string().optional(),
  createdAt: z.string(),
});

export const AdminKbFeedbackSchema = z.object({
  total: z.number().int().nonnegative(),
  items: z.array(AdminKbFeedbackItemSchema),
});

export type AdminKbFeedback = z.infer<typeof AdminKbFeedbackSchema>;

/**
 * Per-game KB settings (admin override).
 * Returned by GET /PUT /api/v1/admin/kb/games/{gameId}/settings
 */
export const GameKbSettingsSchema = z.object({
  gameId: z.string(),
  maxChunksOverride: z.number().int().positive().optional(),
  chunkSizeOverride: z.number().int().positive().optional(),
  cacheEnabledOverride: z.boolean().optional(),
  languageOverride: z.string().optional(),
});

export type GameKbSettings = z.infer<typeof GameKbSettingsSchema>;
