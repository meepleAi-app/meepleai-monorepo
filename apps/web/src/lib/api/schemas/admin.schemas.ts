/**
 * Administration API Schemas (Issue #1679)
 *
 * Zod schemas for validating Administration bounded context responses.
 * Covers: User Management, Prompt Template Management
 */

import { z } from 'zod';

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

import { ApiKeyDtoSchema } from './auth.schemas';

// ========== User Management ==========

export const AdminUserSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  displayName: z.string().min(1),
  role: z.string().min(1),
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

// Note: PagedResult is defined in config.schemas.ts and re-exported via index.ts
