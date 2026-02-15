/**
 * RAG Execution Schemas (Issue #4459)
 *
 * Zod schemas for RAG execution replay and comparison.
 */

import { z } from 'zod';

// =============================================================================
// Replay Request
// =============================================================================

export const replayExecutionRequestSchema = z.object({
  strategy: z.string().optional(),
  topK: z.number().int().positive().optional(),
  model: z.string().optional(),
  temperature: z.number().min(0).max(2).optional(),
});

export type ReplayExecutionRequest = z.infer<typeof replayExecutionRequestSchema>;

// =============================================================================
// Compare Request / Response
// =============================================================================

export const compareExecutionsRequestSchema = z.object({
  executionIds: z.array(z.string().uuid()).length(2),
});

export type CompareExecutionsRequest = z.infer<typeof compareExecutionsRequestSchema>;

export const executionSummarySchema = z.object({
  id: z.string().uuid(),
  testQuery: z.string(),
  success: z.boolean(),
  totalDurationMs: z.number(),
  totalTokensUsed: z.number(),
  totalCost: z.number(),
  blocksExecuted: z.number(),
  blocksFailed: z.number(),
  finalResponse: z.string().nullable().optional(),
  configOverridesJson: z.string().nullable().optional(),
  parentExecutionId: z.string().uuid().nullable().optional(),
  executedAt: z.string(),
});

export type ExecutionSummary = z.infer<typeof executionSummarySchema>;

export const metricsDeltaSchema = z.object({
  durationDeltaMs: z.number(),
  tokensDelta: z.number(),
  costDelta: z.number(),
  overallAssessment: z.enum(['improved', 'degraded', 'unchanged']),
});

export type MetricsDelta = z.infer<typeof metricsDeltaSchema>;

export const blockMetricsSchema = z.object({
  success: z.boolean(),
  durationMs: z.number(),
  tokensUsed: z.number(),
  cost: z.number(),
  validationScore: z.number().nullable().optional(),
  documentCount: z.number().nullable().optional(),
});

export type BlockMetrics = z.infer<typeof blockMetricsSchema>;

export const blockComparisonSchema = z.object({
  blockId: z.string(),
  blockType: z.string(),
  blockName: z.string(),
  execution1: blockMetricsSchema.nullable().optional(),
  execution2: blockMetricsSchema.nullable().optional(),
  status: z.enum(['improved', 'degraded', 'unchanged', 'added', 'removed']),
});

export type BlockComparison = z.infer<typeof blockComparisonSchema>;

export const scoreChangeSchema = z.object({
  documentId: z.string(),
  score1: z.number(),
  score2: z.number(),
  delta: z.number(),
});

export type ScoreChange = z.infer<typeof scoreChangeSchema>;

export const documentDiffSchema = z.object({
  blockId: z.string(),
  onlyInExecution1: z.array(z.string()),
  onlyInExecution2: z.array(z.string()),
  inBoth: z.array(z.string()),
  scoreChanges: z.array(scoreChangeSchema),
});

export type DocumentDiff = z.infer<typeof documentDiffSchema>;

export const executionComparisonSchema = z.object({
  execution1: executionSummarySchema,
  execution2: executionSummarySchema,
  metricsDelta: metricsDeltaSchema,
  blockComparisons: z.array(blockComparisonSchema),
  documentDiffs: z.array(documentDiffSchema),
});

export type ExecutionComparison = z.infer<typeof executionComparisonSchema>;
