/**
 * Resource Forecasting Simulator Zod Schemas
 * Issue #3726: Resource Forecasting Simulator (Epic #3688)
 */

import { z } from 'zod';

// ========== Growth Pattern Constants ==========

export const GROWTH_PATTERNS = ['Linear', 'Exponential', 'Logarithmic', 'SCurve'] as const;

export type GrowthPattern = (typeof GROWTH_PATTERNS)[number];

export const GROWTH_PATTERN_LABELS: Record<GrowthPattern, string> = {
  Linear: 'Linear (constant rate)',
  Exponential: 'Exponential (compounding)',
  Logarithmic: 'Logarithmic (decelerating)',
  SCurve: 'S-Curve (slow → fast → plateau)',
};

// ========== Monthly Projection ==========

export const MonthlyProjectionSchema = z.object({
  month: z.number(),
  projectedUsers: z.number(),
  projectedDbGb: z.number(),
  projectedDailyTokens: z.number(),
  projectedCacheMb: z.number(),
  projectedVectorEntries: z.number(),
  estimatedMonthlyCostUsd: z.number(),
});
export type MonthlyProjection = z.infer<typeof MonthlyProjectionSchema>;

// ========== Forecast Recommendation ==========

export const ForecastRecommendationSchema = z.object({
  resourceType: z.string(),
  triggerMonth: z.number(),
  severity: z.string(),
  message: z.string(),
  action: z.string(),
});
export type ForecastRecommendation = z.infer<typeof ForecastRecommendationSchema>;

// ========== Estimation Result ==========

export const ResourceForecastEstimationResultSchema = z.object({
  growthPattern: z.string(),
  monthlyGrowthRate: z.number(),
  currentUsers: z.number(),
  projectedUsersMonth12: z.number(),
  projections: z.array(MonthlyProjectionSchema),
  recommendations: z.array(ForecastRecommendationSchema),
  projectedMonthlyCostMonth12: z.number(),
});
export type ResourceForecastEstimationResult = z.infer<
  typeof ResourceForecastEstimationResultSchema
>;

// ========== Forecast Scenario DTO ==========

export const ResourceForecastDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  growthPattern: z.string(),
  monthlyGrowthRate: z.number(),
  currentUsers: z.number(),
  currentDbSizeGb: z.number(),
  currentDailyTokens: z.number(),
  currentCacheMb: z.number(),
  currentVectorEntries: z.number(),
  dbPerUserMb: z.number(),
  tokensPerUserPerDay: z.number(),
  cachePerUserMb: z.number(),
  vectorsPerUser: z.number(),
  projectedMonthlyCost: z.number(),
  createdByUserId: z.string().uuid(),
  createdAt: z.string(),
});
export type ResourceForecastDto = z.infer<typeof ResourceForecastDtoSchema>;

export const ResourceForecastsResponseSchema = z.object({
  items: z.array(ResourceForecastDtoSchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
});
export type ResourceForecastsResponse = z.infer<typeof ResourceForecastsResponseSchema>;

export const SaveForecastResponseSchema = z.object({
  id: z.string().uuid(),
});
export type SaveForecastResponse = z.infer<typeof SaveForecastResponseSchema>;

// ========== Request Types ==========

export type EstimateResourceForecastRequest = {
  growthPattern: string;
  monthlyGrowthRate: number;
  currentUsers: number;
  currentDbSizeGb: number;
  currentDailyTokens: number;
  currentCacheMb: number;
  currentVectorEntries: number;
  dbPerUserMb: number;
  tokensPerUserPerDay: number;
  cachePerUserMb: number;
  vectorsPerUser: number;
};

export type SaveResourceForecastRequest = {
  name: string;
  growthPattern: string;
  monthlyGrowthRate: number;
  currentUsers: number;
  currentDbSizeGb: number;
  currentDailyTokens: number;
  currentCacheMb: number;
  currentVectorEntries: number;
  dbPerUserMb: number;
  tokensPerUserPerDay: number;
  cachePerUserMb: number;
  vectorsPerUser: number;
  projectionsJson: string;
  recommendationsJson?: string | null;
  projectedMonthlyCost: number;
};

export type GetResourceForecastsParams = {
  page?: number;
  pageSize?: number;
};
