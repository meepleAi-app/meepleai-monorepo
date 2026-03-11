/**
 * Administration API Schemas (Issue #1679)
 *
 * Zod schemas for validating Administration bounded context responses.
 * Covers: User Management, Prompt Template Management
 */

import { z } from 'zod';

import { ApiKeyDtoSchema } from './auth.schemas';

// ========== Publication Workflow (Issue #3480 + #3481) ==========

/**
 * Approval status for game publication workflow (matches C# ApprovalStatus enum)
 * Issue #3481: Backend publication workflow
 */
export const ApprovalStatusSchema = z.enum(['Draft', 'PendingReview', 'Approved', 'Rejected']);
export type ApprovalStatus = z.infer<typeof ApprovalStatusSchema>;

/**
 * Request to publish game to SharedGameCatalog
 * Issue #3480: Frontend wizard integration
 */
export const PublishGameRequestSchema = z.object({
  status: ApprovalStatusSchema,
});
export type PublishGameRequest = z.infer<typeof PublishGameRequestSchema>;

/**
 * Response from publish game endpoint
 */
export const PublishGameResponseSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  isPublished: z.boolean(),
  approvalStatus: ApprovalStatusSchema,
  publishedAt: z.string().datetime().nullable(),
});
export type PublishGameResponse = z.infer<typeof PublishGameResponseSchema>;

// ========== User Management ==========

export const AdminUserSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  displayName: z.string().min(1),
  role: z.string().min(1),
  tier: z.string().optional().default('Free'), // Issue #3698: User tier
  tokenUsage: z.number().int().optional().default(0), // Issue #3698: Tokens used
  tokenLimit: z.number().int().optional().default(10_000), // Issue #3698: Monthly limit
  createdAt: z.string().datetime(),
  lastSeenAt: z.string().datetime().nullable().optional(),
  isTwoFactorEnabled: z.boolean().optional(),
  isSuspended: z.boolean().optional().default(false),
  suspendReason: z.string().nullable().optional(),
});

export type AdminUser = z.infer<typeof AdminUserSchema>;

export const SuspendUserRequestSchema = z.object({
  reason: z.string().optional(),
});

export type SuspendUserRequest = z.infer<typeof SuspendUserRequestSchema>;

export const CreateUserRequestSchema = z.object({
  email: z.string().email(),
  password: z.string().min(8),
  displayName: z.string().min(1),
  role: z.string().min(1),
});

export type CreateUserRequest = z.infer<typeof CreateUserRequestSchema>;

export const UpdateUserRequestSchema = z.object({
  displayName: z.string().min(1).optional(),
  role: z.string().min(1).optional(),
});

export type UpdateUserRequest = z.infer<typeof UpdateUserRequestSchema>;

export const AdminUserResponseSchema = z.object({
  user: AdminUserSchema,
  message: z.string().optional(),
});

export type AdminUserResponse = z.infer<typeof AdminUserResponseSchema>;

export const DeleteUserResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type DeleteUserResponse = z.infer<typeof DeleteUserResponseSchema>;

// ========== Prompt Template Management ==========

export const PromptTemplateSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  category: z.string().min(1),
  description: z.string().nullable().optional(),
  isActive: z.boolean(),
  activeVersionId: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable().optional(),
});

export type PromptTemplate = z.infer<typeof PromptTemplateSchema>;

export const CreatePromptRequestSchema = z.object({
  name: z.string().min(1),
  description: z.string().optional(),
  content: z.string().min(1),
  isActive: z.boolean().optional(),
});

export type CreatePromptRequest = z.infer<typeof CreatePromptRequestSchema>;

export const UpdatePromptRequestSchema = z.object({
  name: z.string().min(1).optional(),
  description: z.string().optional(),
  content: z.string().min(1).optional(),
  isActive: z.boolean().optional(),
});

export type UpdatePromptRequest = z.infer<typeof UpdatePromptRequestSchema>;

export const PromptResponseSchema = z.object({
  template: PromptTemplateSchema,
  message: z.string().optional(),
});

export type PromptResponse = z.infer<typeof PromptResponseSchema>;

export const DeletePromptResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type DeletePromptResponse = z.infer<typeof DeletePromptResponseSchema>;

// ========== User Activity Timeline (Issue #911) ==========

export const UserActivityDtoSchema = z.object({
  id: z.string().uuid(),
  action: z.string().min(1),
  resource: z.string().min(1),
  resourceId: z.string().nullable().optional(),
  result: z.string().min(1),
  details: z.string().nullable().optional(),
  createdAt: z.string().datetime(),
  ipAddress: z.string().nullable().optional(),
});

export type UserActivityDto = z.infer<typeof UserActivityDtoSchema>;

export const GetUserActivityResultSchema = z.object({
  activities: z.array(UserActivityDtoSchema),
  totalCount: z.number().int().min(0),
});

export type GetUserActivityResult = z.infer<typeof GetUserActivityResultSchema>;

export interface UserActivityFilters {
  actionFilter?: string;
  resourceFilter?: string;
  startDate?: Date;
  endDate?: Date;
  limit?: number;
}

export const ActivateVersionResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type ActivateVersionResponse = z.infer<typeof ActivateVersionResponseSchema>;

// ========== Admin Sessions ==========

export const AdminSessionInfoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  userEmail: z.string().email(),
  createdAt: z.string().datetime(),
  expiresAt: z.string().datetime(),
  lastSeenAt: z.string().datetime().nullable().optional(),
  revokedAt: z.string().datetime().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  userAgent: z.string().nullable().optional(),
});

export type AdminSessionInfo = z.infer<typeof AdminSessionInfoSchema>;

export interface GetAdminSessionsParams {
  limit?: number;
  userId?: string;
}

// ========== Admin Stats & Analytics ==========

export const AiRequestSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid().nullable(),
  gameId: z.string().uuid().nullable(),
  endpoint: z.string(),
  query: z.string().nullable(),
  responseSnippet: z.string().nullable(),
  latencyMs: z.number(),
  tokenCount: z.number(),
  promptTokens: z.number(),
  completionTokens: z.number(),
  confidence: z.number().nullable(),
  status: z.string(),
  errorMessage: z.string().nullable(),
  ipAddress: z.string().nullable(),
  userAgent: z.string().nullable(),
  createdAt: z.string().datetime(),
  model: z.string().nullable(),
  finishReason: z.string().nullable(),
});

export type AiRequest = z.infer<typeof AiRequestSchema>;

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

export const AiRequestsResponseSchema = z.object({
  requests: z.array(AiRequestSchema),
  totalCount: z.number(),
});

export type AiRequestsResponse = z.infer<typeof AiRequestsResponseSchema>;

// ========== Prompt Versions & Audit ==========

export const PromptVersionSchema = z.object({
  id: z.string().uuid(),
  templateId: z.string().uuid(),
  versionNumber: z.number(),
  content: z.string(),
  isActive: z.boolean(),
  createdAt: z.string().datetime(),
  createdBy: z.string().uuid().nullable().optional(),
  activatedAt: z.string().datetime().nullable().optional(),
});

export type PromptVersion = z.infer<typeof PromptVersionSchema>;

export const PromptVersionsResponseSchema = z.object({
  versions: z.array(PromptVersionSchema),
});

export type PromptVersionsResponse = z.infer<typeof PromptVersionsResponseSchema>;

export const PromptAuditLogSchema = z.object({
  id: z.string().uuid(),
  templateId: z.string().uuid(),
  versionNumber: z.number(),
  action: z.string(),
  performedBy: z.string().uuid().nullable().optional(),
  performedAt: z.string().datetime(),
  details: z.string().nullable().optional(),
});

export type PromptAuditLog = z.infer<typeof PromptAuditLogSchema>;

export const PromptAuditLogsResponseSchema = z.object({
  logs: z.array(PromptAuditLogSchema),
  totalPages: z.number(),
});

export type PromptAuditLogsResponse = z.infer<typeof PromptAuditLogsResponseSchema>;

export const CreatePromptVersionRequestSchema = z.object({
  content: z.string().min(1),
  isActive: z.boolean().optional(),
});

export type CreatePromptVersionRequest = z.infer<typeof CreatePromptVersionRequestSchema>;

export const CreatePromptVersionResponseSchema = z.object({
  id: z.string().uuid(),
  versionNumber: z.number(),
  message: z.string().optional(),
});

export type CreatePromptVersionResponse = z.infer<typeof CreatePromptVersionResponseSchema>;

// ========== N8N Workflow Templates ==========

export const TemplateParameterSchema = z.object({
  name: z.string(),
  label: z.string(),
  type: z.enum(['string', 'number', 'boolean', 'select']),
  required: z.boolean(),
  defaultValue: z.string().optional(),
  description: z.string().optional(),
  options: z.array(z.string()).optional(),
  sensitive: z.boolean(),
});

export type TemplateParameter = z.infer<typeof TemplateParameterSchema>;

export const WorkflowTemplateSchema = z.object({
  id: z.string(),
  name: z.string(),
  version: z.string(),
  category: z.string(),
  description: z.string(),
  icon: z.string().optional(),
  tags: z.array(z.string()),
  parameters: z.array(TemplateParameterSchema),
});

export type WorkflowTemplate = z.infer<typeof WorkflowTemplateSchema>;

export const WorkflowTemplateDetailSchema = WorkflowTemplateSchema.extend({
  workflow: z.any(), // N8N workflow JSON structure
});

export type WorkflowTemplateDetail = z.infer<typeof WorkflowTemplateDetailSchema>;

export const ImportWorkflowResponseSchema = z.object({
  workflowId: z.string(),
  message: z.string(),
});

export type ImportWorkflowResponse = z.infer<typeof ImportWorkflowResponseSchema>;

// ========== N8N Configuration (Issue #60) ==========

export const N8nConfigDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  baseUrl: z.string(),
  webhookUrl: z.string().nullable().optional(),
  isActive: z.boolean(),
  lastTestedAt: z.string().datetime().nullable().optional(),
  lastTestResult: z.string().nullable().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});

export type N8nConfigDto = z.infer<typeof N8nConfigDtoSchema>;

export const N8nTestResultDtoSchema = z.object({
  success: z.boolean(),
  message: z.string(),
  latencyMs: z.number().nullable().optional(),
});

export type N8nTestResultDto = z.infer<typeof N8nTestResultDtoSchema>;

// ========== Analytics Dashboard (Issue #1977) ==========

/**
 * Time Series Data Point Schema
 * Matches TimeSeriesDataPoint from backend Contracts
 */
export const TimeSeriesDataPointSchema = z.object({
  date: z.string().datetime(),
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
  generatedAt: z.string().datetime(),
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
  timestamp: z.string().datetime(),
  severity: ActivitySeveritySchema.optional().default('Info'),
});

export type ActivityEvent = z.infer<typeof ActivityEventSchema>;

export const RecentActivityDtoSchema = z.object({
  events: z.array(ActivityEventSchema),
  totalCount: z.number().int().nonnegative(),
  generatedAt: z.string().datetime(),
});

export type RecentActivityDto = z.infer<typeof RecentActivityDtoSchema>;

/**
 * Infrastructure Monitoring Schemas (Issue #896)
 * Matches backend domain models from Issues #891-894
 */

export const HealthStateSchema = z.enum(['Healthy', 'Degraded', 'Unhealthy']);
export type HealthState = z.infer<typeof HealthStateSchema>;

export const ServiceHealthStatusSchema = z.object({
  serviceName: z.string(),
  state: HealthStateSchema,
  errorMessage: z.string().nullable().optional(),
  checkedAt: z.string().datetime(),
  responseTimeMs: z.number().nonnegative(), // Response time in milliseconds
});

export type ServiceHealthStatus = z.infer<typeof ServiceHealthStatusSchema>;

export const OverallHealthStatusSchema = z.object({
  state: HealthStateSchema,
  totalServices: z.number().int().nonnegative(),
  healthyServices: z.number().int().nonnegative(),
  degradedServices: z.number().int().nonnegative(),
  unhealthyServices: z.number().int().nonnegative(),
  checkedAt: z.string().datetime(),
});

export type OverallHealthStatus = z.infer<typeof OverallHealthStatusSchema>;

export const PrometheusMetricsSummarySchema = z.object({
  apiRequestsLast24h: z.number().int().nonnegative(),
  avgLatencyMs: z.number().nonnegative(),
  errorRate: z.number().min(0).max(1),
  llmCostLast24h: z.number().nonnegative(),
});

export type PrometheusMetricsSummary = z.infer<typeof PrometheusMetricsSummarySchema>;

export const InfrastructureDetailsSchema = z.object({
  overall: OverallHealthStatusSchema,
  services: z.array(ServiceHealthStatusSchema),
  prometheusMetrics: PrometheusMetricsSummarySchema,
});

export type InfrastructureDetails = z.infer<typeof InfrastructureDetailsSchema>;

// ========== Metrics Time Series (Issue #901) ==========

/**
 * Time-series data point from Prometheus range query.
 * Issue #901: Replaces mock chart data with real metrics.
 */
export const MetricsTimeSeriesDataPointSchema = z.object({
  timestamp: z.string().datetime(),
  value: z.number(),
});

export type MetricsTimeSeriesDataPoint = z.infer<typeof MetricsTimeSeriesDataPointSchema>;

/**
 * Response from /api/v1/admin/infrastructure/metrics/timeseries
 * Contains CPU, memory, and request rate time-series data.
 */
export const MetricsTimeSeriesResponseSchema = z.object({
  cpu: z.array(MetricsTimeSeriesDataPointSchema),
  memory: z.array(MetricsTimeSeriesDataPointSchema),
  requests: z.array(MetricsTimeSeriesDataPointSchema),
});

export type MetricsTimeSeriesResponse = z.infer<typeof MetricsTimeSeriesResponseSchema>;

// ========== API Key Management (Issue #908) ==========

// Re-export ApiKeyDto from auth.schemas to avoid duplication
export type { ApiKeyDto } from './auth.schemas';
export { ApiKeyDtoSchema } from './auth.schemas';

export const ApiKeyUsageStatsDtoSchema = z.object({
  keyId: z.string().uuid(),
  totalUsageCount: z.number().int().nonnegative(),
  lastUsedAt: z.string().datetime().nullable().optional(),
  usageCountLast24Hours: z.number().int().nonnegative(),
  usageCountLast7Days: z.number().int().nonnegative(),
  usageCountLast30Days: z.number().int().nonnegative(),
  averageRequestsPerDay: z.number().nonnegative(),
});

export type ApiKeyUsageStatsDto = z.infer<typeof ApiKeyUsageStatsDtoSchema>;

export const ApiKeyWithStatsDtoSchema = z.object({
  apiKey: ApiKeyDtoSchema,
  usageStats: ApiKeyUsageStatsDtoSchema,
});

export type ApiKeyWithStatsDto = z.infer<typeof ApiKeyWithStatsDtoSchema>;

// Re-export CreateApiKey schemas from auth.schemas to avoid duplication
export type { CreateApiKeyRequest, CreateApiKeyResponse } from './auth.schemas';
export { CreateApiKeyRequestSchema, CreateApiKeyResponseSchema } from './auth.schemas';

export const UpdateApiKeyRequestSchema = z.object({
  keyName: z.string().min(1).optional(),
  scopes: z.string().min(1).optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

export type UpdateApiKeyRequest = z.infer<typeof UpdateApiKeyRequestSchema>;

export const GetAllApiKeysWithStatsResponseSchema = z.object({
  keys: z.array(ApiKeyWithStatsDtoSchema),
  count: z.number().int().nonnegative(),
  filters: z.object({
    userId: z.string().uuid().nullable().optional(),
    includeRevoked: z.boolean(),
  }),
});

export type GetAllApiKeysWithStatsResponse = z.infer<typeof GetAllApiKeysWithStatsResponseSchema>;

export const BulkImportApiKeysResultSchema = z.object({
  successCount: z.number().int().nonnegative(),
  failedCount: z.number().int().nonnegative(),
  errors: z.array(z.string()),
});

export type BulkImportApiKeysResult = z.infer<typeof BulkImportApiKeysResultSchema>;

// ========== Audit Log (Issue #3691) ==========

export const AuditLogEntrySchema = z.object({
  id: z.string().uuid(),
  adminUserId: z.string().uuid().nullable(),
  action: z.string(),
  resource: z.string(),
  resourceId: z.string().nullable().optional(),
  result: z.string(),
  details: z.string().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  createdAt: z.string().datetime(),
});
export type AuditLogEntry = z.infer<typeof AuditLogEntrySchema>;

export const AuditLogListResultSchema = z.object({
  entries: z.array(AuditLogEntrySchema),
  totalCount: z.number().int().nonnegative(),
  limit: z.number().int().positive(),
  offset: z.number().int().nonnegative(),
});
export type AuditLogListResult = z.infer<typeof AuditLogListResultSchema>;

// ========== Token Management (Issue #3692) ==========

export const TokenTierSchema = z.enum(['Free', 'Basic', 'Pro', 'Enterprise']);
export type TokenTier = z.infer<typeof TokenTierSchema>;

export const TokenBalanceSchema = z.object({
  currentBalance: z.number(),
  totalBudget: z.number(),
  currency: z.string().default('EUR'),
  usagePercent: z.number().min(0).max(100),
  projectedDaysUntilDepletion: z.number().nullable(),
  lastUpdated: z.string().datetime(),
});
export type TokenBalance = z.infer<typeof TokenBalanceSchema>;

export const TokenConsumptionPointSchema = z.object({
  date: z.string(),
  tokens: z.number().nonnegative(),
  cost: z.number().nonnegative(),
});
export type TokenConsumptionPoint = z.infer<typeof TokenConsumptionPointSchema>;

export const TokenConsumptionDataSchema = z.object({
  points: z.array(TokenConsumptionPointSchema),
  totalTokens: z.number().nonnegative(),
  totalCost: z.number().nonnegative(),
  avgDailyTokens: z.number().nonnegative(),
  avgDailyCost: z.number().nonnegative(),
});
export type TokenConsumptionData = z.infer<typeof TokenConsumptionDataSchema>;

export const TierUsageSchema = z.object({
  tier: TokenTierSchema,
  limitPerMonth: z.number().nonnegative(),
  currentUsage: z.number().nonnegative(),
  userCount: z.number().int().nonnegative(),
  usagePercent: z.number().min(0).max(100),
});
export type TierUsage = z.infer<typeof TierUsageSchema>;

export const TierUsageListSchema = z.object({
  tiers: z.array(TierUsageSchema),
});
export type TierUsageList = z.infer<typeof TierUsageListSchema>;

export const TopConsumerSchema = z.object({
  userId: z.string().uuid(),
  displayName: z.string(),
  email: z.string(),
  tier: TokenTierSchema,
  tokensUsed: z.number().nonnegative(),
  percentOfTierLimit: z.number().min(0),
});
export type TopConsumer = z.infer<typeof TopConsumerSchema>;

export const TopConsumersListSchema = z.object({
  consumers: z.array(TopConsumerSchema),
});
export type TopConsumersList = z.infer<typeof TopConsumersListSchema>;

export const AddCreditsRequestSchema = z.object({
  amount: z.number().positive(),
  currency: z.string().default('EUR'),
  note: z.string().optional(),
});
export type AddCreditsRequest = z.infer<typeof AddCreditsRequestSchema>;

export const UpdateTierLimitsRequestSchema = z.object({
  tier: TokenTierSchema,
  tokensPerMonth: z.number().nonnegative(),
});
export type UpdateTierLimitsRequest = z.infer<typeof UpdateTierLimitsRequestSchema>;

// ========== Batch Jobs (Issue #3693) ==========

export const BatchJobTypeSchema = z.enum([
  'ResourceForecast',
  'CostAnalysis',
  'DataCleanup',
  'BggSync',
  'AgentBenchmark',
]);
export type BatchJobType = z.infer<typeof BatchJobTypeSchema>;

export const BatchJobStatusSchema = z.enum([
  'Queued',
  'Running',
  'Completed',
  'Failed',
  'Cancelled',
]);
export type BatchJobStatus = z.infer<typeof BatchJobStatusSchema>;

export const BatchJobDtoSchema = z.object({
  id: z.string().uuid(),
  type: BatchJobTypeSchema,
  status: BatchJobStatusSchema,
  parameters: z.record(z.string(), z.any()).nullable(),
  results: z.record(z.string(), z.any()).nullable(),
  errorMessage: z.string().nullable(),
  progress: z.number().min(0).max(100),
  createdAt: z.string().datetime(),
  startedAt: z.string().datetime().nullable(),
  completedAt: z.string().datetime().nullable(),
  duration: z.number().nullable(),
});
export type BatchJobDto = z.infer<typeof BatchJobDtoSchema>;

export const BatchJobListSchema = z.object({
  jobs: z.array(BatchJobDtoSchema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});
export type BatchJobList = z.infer<typeof BatchJobListSchema>;

export const CreateBatchJobRequestSchema = z.object({
  type: BatchJobTypeSchema,
  parameters: z.record(z.string(), z.any()).optional(),
});
export type CreateBatchJobRequest = z.infer<typeof CreateBatchJobRequestSchema>;

export const CreateBatchJobResponseSchema = z.object({
  id: z.string().uuid(),
});
export type CreateBatchJobResponse = z.infer<typeof CreateBatchJobResponseSchema>;

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

// ========== Game Bulk Import (Issue #4355) ==========

export const BulkImportErrorSchema = z.object({
  bggId: z.number().nullable().optional(),
  gameName: z.string().nullable().optional(),
  reason: z.string(),
  errorType: z.string(),
});
export type BulkImportError = z.infer<typeof BulkImportErrorSchema>;

export const BulkImportFromJsonResultSchema = z.object({
  total: z.number().int().nonnegative(),
  enqueued: z.number().int().nonnegative(),
  skipped: z.number().int().nonnegative(),
  failed: z.number().int().nonnegative(),
  errors: z.array(BulkImportErrorSchema),
});
export type BulkImportFromJsonResult = z.infer<typeof BulkImportFromJsonResultSchema>;

// Note: PagedResult is defined in config.schemas.ts and re-exported via index.ts
