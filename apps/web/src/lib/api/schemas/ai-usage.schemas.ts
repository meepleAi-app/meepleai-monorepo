/**
 * AI Usage Tracking API Schemas (Issue #3338)
 *
 * Zod schemas for validating AI token usage API responses.
 * Covers: User AI usage, model breakdown, operation breakdown, daily usage
 */

import { z } from 'zod';

// ========== Period ==========

export const UsagePeriodDtoSchema = z.object({
  from: z.string(), // DateOnly as string
  to: z.string(), // DateOnly as string
});

export type UsagePeriodDto = z.infer<typeof UsagePeriodDtoSchema>;

// ========== Model Usage ==========

export const ModelUsageDtoSchema = z.object({
  model: z.string(),
  tokens: z.number().int().nonnegative(),
  cost: z.number().nonnegative(),
});

export type ModelUsageDto = z.infer<typeof ModelUsageDtoSchema>;

// ========== Operation Usage ==========

export const OperationUsageDtoSchema = z.object({
  operation: z.string(),
  count: z.number().int().nonnegative(),
  tokens: z.number().int().nonnegative(),
});

export type OperationUsageDto = z.infer<typeof OperationUsageDtoSchema>;

// ========== Daily Usage ==========

export const DailyUsageDtoSchema = z.object({
  date: z.string(), // DateOnly as string (YYYY-MM-DD)
  tokens: z.number().int().nonnegative(),
});

export type DailyUsageDto = z.infer<typeof DailyUsageDtoSchema>;

// ========== User AI Usage (Full DTO) ==========

export const UserAiUsageDtoSchema = z.object({
  userId: z.string().uuid(),
  period: UsagePeriodDtoSchema,
  totalTokens: z.number().int().nonnegative(),
  totalCostUsd: z.number().nonnegative(),
  requestCount: z.number().int().nonnegative(),
  byModel: z.array(ModelUsageDtoSchema),
  byOperation: z.array(OperationUsageDtoSchema),
  dailyUsage: z.array(DailyUsageDtoSchema),
});

export type UserAiUsageDto = z.infer<typeof UserAiUsageDtoSchema>;

// ========== Issue #94: C3 — Multi-period Summary ==========

export const AiUsagePeriodSummaryDtoSchema = z.object({
  requestCount: z.number().int().nonnegative(),
  promptTokens: z.number().int().nonnegative(),
  completionTokens: z.number().int().nonnegative(),
  totalTokens: z.number().int().nonnegative(),
  costUsd: z.number().nonnegative(),
  averageLatencyMs: z.number().int().nonnegative(),
});

export type AiUsagePeriodSummaryDto = z.infer<typeof AiUsagePeriodSummaryDtoSchema>;

export const AiUsageSummaryDtoSchema = z.object({
  today: AiUsagePeriodSummaryDtoSchema,
  last7Days: AiUsagePeriodSummaryDtoSchema,
  last30Days: AiUsagePeriodSummaryDtoSchema,
});

export type AiUsageSummaryDto = z.infer<typeof AiUsageSummaryDtoSchema>;

// ========== Issue #94: C3 — Distributions ==========

export const DistributionItemDtoSchema = z.object({
  name: z.string(),
  count: z.number().int().nonnegative(),
  percentage: z.number().nonnegative(),
});

export type DistributionItemDto = z.infer<typeof DistributionItemDtoSchema>;

export const AiUsageDistributionsDtoSchema = z.object({
  models: z.array(DistributionItemDtoSchema),
  providers: z.array(DistributionItemDtoSchema),
  operations: z.array(DistributionItemDtoSchema),
});

export type AiUsageDistributionsDto = z.infer<typeof AiUsageDistributionsDtoSchema>;

// ========== Issue #94: C3 — Recent Requests ==========

export const RecentAiRequestDtoSchema = z.object({
  requestedAt: z.string(),
  model: z.string(),
  provider: z.string(),
  operation: z.string(),
  promptTokens: z.number().int().nonnegative(),
  completionTokens: z.number().int().nonnegative(),
  costUsd: z.number().nonnegative(),
  latencyMs: z.number().int().nonnegative(),
  success: z.boolean(),
});

export type RecentAiRequestDto = z.infer<typeof RecentAiRequestDtoSchema>;

export const AiUsageRecentDtoSchema = z.object({
  items: z.array(RecentAiRequestDtoSchema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  note: z.string(),
});

export type AiUsageRecentDto = z.infer<typeof AiUsageRecentDtoSchema>;
