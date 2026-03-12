/**
 * RAG Setup Schemas - Admin RAG Dashboard
 *
 * Zod schemas for cross-BC readiness aggregation and cost estimation DTOs.
 */

import { z } from 'zod';

// ========== Document Status ==========

export const DocumentStatusSchema = z.object({
  documentId: z.string(),
  fileName: z.string(),
  processingState: z.string(),
  progressPercentage: z.number(),
  isActiveForRag: z.boolean(),
  errorMessage: z.string().nullable().optional(),
});

export type DocumentStatus = z.infer<typeof DocumentStatusSchema>;

// ========== Agent Info ==========

export const AgentInfoSchema = z.object({
  agentId: z.string(),
  name: z.string(),
  type: z.string(),
  isActive: z.boolean(),
  isReady: z.boolean(),
});

export type AgentInfo = z.infer<typeof AgentInfoSchema>;

// ========== Game RAG Readiness ==========

export const GameRagReadinessSchema = z.object({
  gameId: z.string(),
  gameTitle: z.string(),
  gameStatus: z.string(),
  totalDocuments: z.number(),
  readyDocuments: z.number(),
  processingDocuments: z.number(),
  failedDocuments: z.number(),
  documents: z.array(DocumentStatusSchema),
  linkedAgent: AgentInfoSchema.nullable(),
  overallReadiness: z.string(),
});

export type GameRagReadiness = z.infer<typeof GameRagReadinessSchema>;

// ========== Agent Cost Estimate ==========

export const AgentCostEstimateSchema = z.object({
  totalChunks: z.number(),
  estimatedEmbeddingTokens: z.number(),
  estimatedCostPerQuery: z.number(),
  currency: z.string(),
  model: z.string(),
  note: z.string(),
});

export type AgentCostEstimate = z.infer<typeof AgentCostEstimateSchema>;

// ========== Overall Readiness States ==========

export const READINESS_STATES = {
  NO_DOCUMENTS: 'no_documents',
  DOCUMENTS_PROCESSING: 'documents_processing',
  DOCUMENTS_FAILED: 'documents_failed',
  READY_FOR_AGENT: 'ready_for_agent',
  FULLY_OPERATIONAL: 'fully_operational',
} as const;

export type ReadinessState = (typeof READINESS_STATES)[keyof typeof READINESS_STATES];
