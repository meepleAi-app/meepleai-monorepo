/**
 * A/B Test schemas for blind model comparison.
 * Issue #5500: A/B Testing frontend — New Test page.
 */
import { z } from 'zod';

// ========== Evaluation DTO ==========

export const AbTestEvaluationDtoSchema = z.object({
  evaluatorId: z.string().uuid(),
  accuracy: z.number().int().min(1).max(5),
  completeness: z.number().int().min(1).max(5),
  clarity: z.number().int().min(1).max(5),
  tone: z.number().int().min(1).max(5),
  notes: z.string().nullable(),
  averageScore: z.number(),
  evaluatedAt: z.string(),
});

export type AbTestEvaluationDto = z.infer<typeof AbTestEvaluationDtoSchema>;

// ========== Variant DTOs ==========

export const AbTestVariantDtoSchema = z.object({
  id: z.string().uuid(),
  label: z.string(),
  response: z.string().nullable(),
  tokensUsed: z.number().int(),
  latencyMs: z.number().int(),
  costUsd: z.number(),
  failed: z.boolean(),
  errorMessage: z.string().nullable().optional(),
  evaluation: AbTestEvaluationDtoSchema.nullable().optional(),
});

export type AbTestVariantDto = z.infer<typeof AbTestVariantDtoSchema>;

export const AbTestVariantRevealedDtoSchema = z.object({
  id: z.string().uuid(),
  label: z.string(),
  provider: z.string(),
  modelId: z.string(),
  response: z.string().nullable(),
  tokensUsed: z.number().int(),
  latencyMs: z.number().int(),
  costUsd: z.number(),
  failed: z.boolean(),
  errorMessage: z.string().nullable().optional(),
  evaluation: AbTestEvaluationDtoSchema.nullable().optional(),
});

export type AbTestVariantRevealedDto = z.infer<typeof AbTestVariantRevealedDtoSchema>;

// ========== Session DTOs ==========

export const AbTestSessionDtoSchema = z.object({
  id: z.string().uuid(),
  createdBy: z.string().uuid(),
  query: z.string(),
  knowledgeBaseId: z.string().uuid().nullable().optional(),
  status: z.string(),
  createdAt: z.string(),
  completedAt: z.string().nullable().optional(),
  totalCost: z.number(),
  variants: z.array(AbTestVariantDtoSchema),
});

export type AbTestSessionDto = z.infer<typeof AbTestSessionDtoSchema>;

export const AbTestSessionRevealedDtoSchema = z.object({
  id: z.string().uuid(),
  createdBy: z.string().uuid(),
  query: z.string(),
  knowledgeBaseId: z.string().uuid().nullable().optional(),
  status: z.string(),
  createdAt: z.string(),
  completedAt: z.string().nullable().optional(),
  totalCost: z.number(),
  winnerLabel: z.string().nullable().optional(),
  winnerModelId: z.string().nullable().optional(),
  variants: z.array(AbTestVariantRevealedDtoSchema),
});

export type AbTestSessionRevealedDto = z.infer<typeof AbTestSessionRevealedDtoSchema>;

// ========== List & Analytics ==========

export const AbTestSessionListDtoSchema = z.object({
  items: z.array(AbTestSessionDtoSchema),
  totalCount: z.number().int(),
  page: z.number().int(),
  pageSize: z.number().int(),
});

export type AbTestSessionListDto = z.infer<typeof AbTestSessionListDtoSchema>;

export const ModelWinRateDtoSchema = z.object({
  modelId: z.string(),
  wins: z.number().int(),
  total: z.number().int(),
  winRate: z.number(),
});

export const ModelAvgScoreDtoSchema = z.object({
  modelId: z.string(),
  avgAccuracy: z.number(),
  avgCompleteness: z.number(),
  avgClarity: z.number(),
  avgTone: z.number(),
  avgOverall: z.number(),
  evaluationCount: z.number().int(),
});

export const AbTestAnalyticsDtoSchema = z.object({
  totalTests: z.number().int(),
  completedTests: z.number().int(),
  totalCost: z.number(),
  modelWinRates: z.array(ModelWinRateDtoSchema),
  modelAvgScores: z.array(ModelAvgScoreDtoSchema),
});

export type AbTestAnalyticsDto = z.infer<typeof AbTestAnalyticsDtoSchema>;

// ========== Request DTOs ==========

export const CreateAbTestRequestSchema = z.object({
  query: z.string().min(1).max(2000),
  modelIds: z.array(z.string().min(1).max(200)).min(2).max(4),
  knowledgeBaseId: z.string().uuid().optional(),
});

export type CreateAbTestRequest = z.infer<typeof CreateAbTestRequestSchema>;

export const VariantEvaluationRequestSchema = z.object({
  label: z.string(),
  accuracy: z.number().int().min(1).max(5),
  completeness: z.number().int().min(1).max(5),
  clarity: z.number().int().min(1).max(5),
  tone: z.number().int().min(1).max(5),
  notes: z.string().max(2000).optional(),
});

export const EvaluateAbTestRequestSchema = z.object({
  evaluations: z.array(VariantEvaluationRequestSchema).min(1),
});

export type EvaluateAbTestRequest = z.infer<typeof EvaluateAbTestRequestSchema>;
