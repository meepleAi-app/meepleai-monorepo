/**
 * Model Performance API Schemas (Issue #3716)
 *
 * Zod schemas for validating model performance analytics responses.
 * Covers: per-model metrics, usage breakdown, daily trends
 */

import { z } from 'zod';

// ========== Per-Model Metrics ==========

export const ModelMetricsDtoSchema = z.object({
  modelId: z.string().min(1),
  provider: z.string().min(1),
  requestCount: z.number().int().nonnegative(),
  usagePercent: z.number().nonnegative(),
  totalCost: z.number().nonnegative(),
  avgLatencyMs: z.number().nonnegative(),
  totalTokens: z.number().int().nonnegative(),
  successRate: z.number().nonnegative(),
  avgTokensPerRequest: z.number().nonnegative(),
});

export type ModelMetricsDto = z.infer<typeof ModelMetricsDtoSchema>;

// ========== Daily Model Stats ==========

export const DailyModelStatsSchema = z.object({
  date: z.string(), // DateOnly serialized as "YYYY-MM-DD"
  requestCount: z.number().int().nonnegative(),
  totalCost: z.number().nonnegative(),
  avgLatencyMs: z.number().nonnegative(),
});

export type DailyModelStats = z.infer<typeof DailyModelStatsSchema>;

// ========== Model Performance DTO ==========

export const ModelPerformanceDtoSchema = z.object({
  totalRequests: z.number().int().nonnegative(),
  totalCost: z.number().nonnegative(),
  totalTokens: z.number().int().nonnegative(),
  avgLatencyMs: z.number().nonnegative(),
  successRate: z.number().nonnegative(),
  models: z.array(ModelMetricsDtoSchema),
  dailyStats: z.array(DailyModelStatsSchema),
});

export type ModelPerformanceDto = z.infer<typeof ModelPerformanceDtoSchema>;
