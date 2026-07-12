/**
 * Admin Mechanic Extractor — Metrics Dashboard Schemas (#532 ME-M2.3)
 *
 * Zod schemas + inferred types mirroring the backend DTOs in
 * apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicMetricsDtos.cs
 * (camelCase JSON per Program.cs). DateOnly → "yyyy-MM-dd" string; decimal → number.
 */
import { z } from 'zod';

export const RejectionReasonCountSchema = z.object({
  reason: z.string(),
  count: z.number(),
});
export type RejectionReasonCount = z.infer<typeof RejectionReasonCountSchema>;

export const MechanicMetricsSummarySchema = z.object({
  totalCostUsd: z.number(),
  totalAnalyses: z.number(),
  publishedCount: z.number(),
  rejectedCount: z.number(),
  inReviewCount: z.number(),
  averageCostUsd: z.number(),
  averageReviewTimeHours: z.number().nullable(),
  approvalRatePct: z.number(),
  rejectionBreakdown: z.array(RejectionReasonCountSchema),
});
export type MechanicMetricsSummary = z.infer<typeof MechanicMetricsSummarySchema>;

export const MechanicCostByDaySchema = z.object({
  date: z.string(),
  costUsd: z.number(),
  analysisCount: z.number(),
});
export const MechanicCostByDayArraySchema = z.array(MechanicCostByDaySchema);
export type MechanicCostByDay = z.infer<typeof MechanicCostByDaySchema>;

export const MechanicRecentAnalysisRowSchema = z.object({
  id: z.string(),
  sharedGameId: z.string(),
  gameName: z.string(),
  status: z.number(),
  reviewedBy: z.string().nullable(),
  reviewerName: z.string().nullable(),
  createdAt: z.string(),
  reviewedAt: z.string().nullable(),
  estimatedCostUsd: z.number(),
});
export type MechanicRecentAnalysisRow = z.infer<typeof MechanicRecentAnalysisRowSchema>;

export const MechanicRecentAnalysesResultSchema = z.object({
  items: z.array(MechanicRecentAnalysisRowSchema),
  totalCount: z.number(),
});
export type MechanicRecentAnalysesResult = z.infer<typeof MechanicRecentAnalysesResultSchema>;
