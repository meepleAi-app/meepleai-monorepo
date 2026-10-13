/**
 * Admin Analytics & Dashboard Schemas
 *
 * Overview stats, dashboard metrics, activity events, token management,
 * engagement metrics, retention cohorts, feature adoption, and app usage stats.
 */

import { z } from 'zod';

// ========== Admin Stats ==========

export const AdminStatsSchema = z.object({
  totalRequests: z.number(),
  avgLatencyMs: z.number(),
  totalTokens: z.number(),
  successRate: z.number(),
  endpointCounts: z.record(z.string(), z.number()),
  feedbackCounts: z.record(z.string(), z.number()),
  totalFeedback: z.number(),
});

export type AdminStats = z.infer<typeof AdminStatsSchema>;

// Issue #4198: Lightweight overview stats for StatsOverview component
// Issue #113: Added activeAiUsers for MAU-AI monitoring
export const AdminOverviewStatsSchema = z.object({
  totalGames: z.number(),
  publishedGames: z.number(),
  totalUsers: z.number(),
  activeUsers: z.number(),
  activeAiUsers: z.number(),
  approvalRate: z.number(),
  pendingApprovals: z.number(),
  recentSubmissions: z.number(),
});

export type AdminOverviewStats = z.infer<typeof AdminOverviewStatsSchema>;

// ========== Analytics Dashboard (Issue #1977) ==========

/**
 * Time Series Data Point Schema
 * Matches TimeSeriesDataPoint from backend Contracts
 */
export const TimeSeriesDataPointSchema = z.object({
  date: z.string().datetime({ offset: true }),
  count: z.number().int().nonnegative(),
  averageValue: z.number().nullable().optional(),
});

export type TimeSeriesDataPoint = z.infer<typeof TimeSeriesDataPointSchema>;

/**
 * Dashboard Metrics Schema
 * Matches DashboardMetrics from backend Contracts (Issue #874: Extended to 16 metrics)
 */
export const DashboardMetricsSchema = z.object({
  totalUsers: z.number().int().nonnegative(),
  activeSessions: z.number().int().nonnegative(),
  apiRequestsToday: z.number().int().nonnegative(),
  totalPdfDocuments: z.number().int().nonnegative(),
  totalChatMessages: z.number().int().nonnegative(),
  averageConfidenceScore: z.number(),
  totalRagRequests: z.number().int().nonnegative(),
  totalTokensUsed: z.number().int().nonnegative(),
  // Issue #874: Additional metrics for centralized dashboard (16 total)
  totalGames: z.number().int().nonnegative(),
  apiRequests7d: z.number().int().nonnegative(),
  apiRequests30d: z.number().int().nonnegative(),
  averageLatency24h: z.number().nonnegative(),
  averageLatency7d: z.number().nonnegative(),
  errorRate24h: z.number().min(0).max(1),
  activeAlerts: z.number().int().nonnegative(),
  resolvedAlerts: z.number().int().nonnegative(),
  // Issue #3694: Extended KPIs for Enterprise Admin Dashboard
  tokenBalanceEur: z.number().nonnegative(),
  tokenLimitEur: z.number().nonnegative(),
  dbStorageGb: z.number().nonnegative(),
  dbStorageLimitGb: z.number().nonnegative(),
  dbGrowthMbPerDay: z.number().nonnegative(),
  cacheHitRatePercent: z.number().min(0).max(100),
  cacheHitRateTrendPercent: z.number(),
});

export type DashboardMetrics = z.infer<typeof DashboardMetricsSchema>;

/**
 * Dashboard Stats DTO Schema (Issue #1977)
 * Matches DashboardStatsDto from backend Contracts
 */
export const DashboardStatsSchema = z.object({
  metrics: DashboardMetricsSchema,
  userTrend: z.array(TimeSeriesDataPointSchema),
  sessionTrend: z.array(TimeSeriesDataPointSchema),
  apiRequestTrend: z.array(TimeSeriesDataPointSchema),
  pdfUploadTrend: z.array(TimeSeriesDataPointSchema),
  chatMessageTrend: z.array(TimeSeriesDataPointSchema),
  generatedAt: z.string().datetime({ offset: true }),
});

export type DashboardStats = z.infer<typeof DashboardStatsSchema>;

/**
 * Activity Event Schemas (Issue #874)
 * For admin dashboard activity feed
 */
export const ActivitySeveritySchema = z.enum(['Info', 'Warning', 'Error', 'Critical']);
export type ActivitySeverity = z.infer<typeof ActivitySeveritySchema>;

export const ActivityEventTypeSchema = z.enum([
  'UserRegistered',
  'UserLogin',
  'PdfUploaded',
  'PdfProcessed',
  'AlertCreated',
  'AlertResolved',
  'GameAdded',
  'ConfigurationChanged',
  'ErrorOccurred',
  'SystemEvent',
]);
export type ActivityEventType = z.infer<typeof ActivityEventTypeSchema>;

export const ActivityEventSchema = z.object({
  id: z.string(),
  eventType: ActivityEventTypeSchema,
  description: z.string(),
  userId: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
  entityId: z.string().nullable().optional(),
  entityType: z.string().nullable().optional(),
  timestamp: z.string().datetime({ offset: true }),
  severity: ActivitySeveritySchema.optional().default('Info'),
});

export type ActivityEvent = z.infer<typeof ActivityEventSchema>;

export const RecentActivityDtoSchema = z.object({
  events: z.array(ActivityEventSchema),
  totalCount: z.number().int().nonnegative(),
  generatedAt: z.string().datetime({ offset: true }),
});

export type RecentActivityDto = z.infer<typeof RecentActivityDtoSchema>;

// ========== App Usage Stats (Issue #3719) ==========

export const EngagementMetricsSchema = z.object({
  dau: z.number().nonnegative(),
  mau: z.number().nonnegative(),
  dauMauRatio: z.number().min(0).max(1),
  avgSessionDurationMinutes: z.number().nonnegative(),
  totalSessions: z.number().nonnegative(),
  bounceRate: z.number().min(0).max(1),
});
export type EngagementMetrics = z.infer<typeof EngagementMetricsSchema>;

export const RetentionCohortSchema = z.object({
  cohortDate: z.string(),
  cohortSize: z.number().nonnegative(),
  day1: z.number().min(0).max(100),
  day7: z.number().min(0).max(100),
  day14: z.number().min(0).max(100),
  day30: z.number().min(0).max(100),
});
export type RetentionCohort = z.infer<typeof RetentionCohortSchema>;

export const FeatureAdoptionItemSchema = z.object({
  featureName: z.string(),
  uniqueUsers: z.number().nonnegative(),
  totalUsages: z.number().nonnegative(),
  adoptionRate: z.number().min(0).max(100),
});
export type FeatureAdoptionItem = z.infer<typeof FeatureAdoptionItemSchema>;

export const GeoDistributionItemSchema = z.object({
  country: z.string(),
  countryCode: z.string(),
  users: z.number().nonnegative(),
  percentage: z.number().min(0).max(100),
});
export type GeoDistributionItem = z.infer<typeof GeoDistributionItemSchema>;

export const SessionDurationBucketSchema = z.object({
  label: z.string(),
  count: z.number().nonnegative(),
  percentage: z.number().min(0).max(100),
});
export type SessionDurationBucket = z.infer<typeof SessionDurationBucketSchema>;

// ========== App Usage Dashboard (Issue #3728) ==========

export const DauMauTrendPointSchema = z.object({
  date: z.string(),
  dau: z.number().nonnegative(),
  mau: z.number().nonnegative(),
});
export type DauMauTrendPoint = z.infer<typeof DauMauTrendPointSchema>;

export const PeakHourEntrySchema = z.object({
  hour: z.number().int().min(0).max(23),
  dayOfWeek: z.number().int().min(0).max(6),
  value: z.number().nonnegative(),
});
export type PeakHourEntry = z.infer<typeof PeakHourEntrySchema>;

export const AppUsageStatsSchema = z.object({
  engagement: EngagementMetricsSchema,
  retentionCohorts: z.array(RetentionCohortSchema),
  featureAdoption: z.array(FeatureAdoptionItemSchema),
  geoDistribution: z.array(GeoDistributionItemSchema),
  sessionDurationDistribution: z.array(SessionDurationBucketSchema),
  dauMauTrend: z.array(DauMauTrendPointSchema).optional(),
  peakHours: z.array(PeakHourEntrySchema).optional(),
  generatedAt: z.string(),
});
export type AppUsageStats = z.infer<typeof AppUsageStatsSchema>;
