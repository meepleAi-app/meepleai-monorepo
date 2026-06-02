/**
 * KB Quality (#1675) — Zod schemas mirroring the backend DTOs.
 *
 * Source of truth: apps/api/src/Api/BoundedContexts/KbQuality/Application/
 * (StartEvaluationCommand, EvaluationStartedResult, EvaluationDetailDto,
 *  EvaluationMetricsDto, EvaluationRunListItemDto, PagedEvaluationsDto).
 *
 * ASP.NET Core's default System.Text.Json serializer emits camelCase property
 * names, so these schemas use camelCase keys matching the wire format.
 *
 * `goldsetGenerationSeed` is a C# `long` — JS number is safe up to 2^53;
 * the BE generates seeds via Random.Shared.NextInt64() which can exceed
 * that boundary. We accept a numeric schema here (loses precision past
 * 2^53) because the FE only displays the seed for human reference; it is
 * never round-tripped back to the BE in a precision-sensitive context.
 */

import { z } from 'zod';

export const QualityBandSchema = z.enum(['Red', 'Yellow', 'Green']);
export type QualityBand = z.infer<typeof QualityBandSchema>;

export const EvaluationStatusSchema = z.enum([
  'Pending',
  'GoldsetGenerating',
  'Running',
  'Completed',
  'Failed',
  'RateLimited',
  'CostCapped',
]);
export type EvaluationStatus = z.infer<typeof EvaluationStatusSchema>;

export const EvaluationMetricsDtoSchema = z.object({
  precision: z.object({ at1: z.number(), at3: z.number(), at5: z.number() }),
  ranking: z.object({ mrr: z.number() }),
  latency: z.object({ p50Ms: z.number().int(), p95Ms: z.number().int() }),
  queryCount: z.number().int().nonnegative(),
  costUsd: z.number(),
  qualityBand: QualityBandSchema,
});
export type EvaluationMetricsDto = z.infer<typeof EvaluationMetricsDtoSchema>;

export const EvaluationDetailDtoSchema = z.object({
  evaluationId: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  startedAt: z.string(),
  completedAt: z.string().nullable(),
  status: EvaluationStatusSchema,
  goldsetVersion: z.string(),
  goldsetGenerationSeed: z.number(),
  metrics: EvaluationMetricsDtoSchema.nullable(),
  costUsd: z.number().nullable(),
  triggeredByAdminId: z.string().uuid(),
  errorMessage: z.string().nullable(),
});
export type EvaluationDetailDto = z.infer<typeof EvaluationDetailDtoSchema>;

export const EvaluationRunListItemSchema = z.object({
  evaluationId: z.string().uuid(),
  startedAt: z.string(),
  completedAt: z.string().nullable(),
  status: EvaluationStatusSchema,
  goldsetVersion: z.string(),
  precisionAt5: z.number().nullable(),
  mrr: z.number().nullable(),
  latencyP95Ms: z.number().int().nullable(),
  costUsd: z.number().nullable(),
  qualityBand: QualityBandSchema.nullable(),
});
export type EvaluationRunListItem = z.infer<typeof EvaluationRunListItemSchema>;

export const PagedEvaluationsSchema = z.object({
  items: z.array(EvaluationRunListItemSchema),
  totalCount: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});
export type PagedEvaluations = z.infer<typeof PagedEvaluationsSchema>;

export const StartEvaluationRequestSchema = z.object({
  goldsetVersion: z.string().optional(),
  overrideCostCap: z.boolean().optional(),
});
export type StartEvaluationRequest = z.infer<typeof StartEvaluationRequestSchema>;

export const EvaluationStartedResultSchema = z.object({
  evaluationId: z.string().uuid(),
  locationCreatedAt: z.string(),
  rateLimitRemaining: z.number().int(),
  rateLimitReset: z.string(),
  costCapRemaining: z.number(),
  costCapEstimate: z.number(),
});
export type EvaluationStartedResult = z.infer<typeof EvaluationStartedResultSchema>;
