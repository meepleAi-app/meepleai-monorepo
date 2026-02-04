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
  to: z.string(),   // DateOnly as string
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
