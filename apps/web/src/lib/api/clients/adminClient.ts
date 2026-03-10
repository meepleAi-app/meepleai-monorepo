/**
 * Administration Client (Issue #1679)
 *
 * Modular client for Administration bounded context.
 * Covers: User Management, Prompt Template Management
 */

import { z } from 'zod';

import { getApiBase } from '../core/httpClient';
import {
  PublishGameResponseSchema,
  AuditLogListResultSchema,
  type AuditLogListResult,
  type ApprovalStatus,
  type PublishGameResponse,
  AdminUserSchema,
  AdminUserResponseSchema,
  PromptTemplateSchema,
  PromptResponseSchema,
  ActivateVersionResponseSchema,
  AdminStatsSchema,
  AiRequestsResponseSchema,
  PromptVersionsResponseSchema,
  PromptAuditLogsResponseSchema,
  CreatePromptVersionResponseSchema,
  PagedResultSchema,
  WorkflowTemplateSchema,
  WorkflowTemplateDetailSchema,
  ImportWorkflowResponseSchema,
  DashboardStatsSchema,
  RecentActivityDtoSchema,
  InfrastructureDetailsSchema,
  MetricsTimeSeriesResponseSchema,
  GetUserActivityResultSchema,
  type CreateUserRequest,
  type UpdateUserRequest,
  type AdminUser,
  type CreatePromptRequest,
  type UpdatePromptRequest,
  type PromptTemplate,
  type ActivateVersionResponse,
  type AdminStats,
  type AiRequest,
  type PromptVersion,
  type PromptAuditLog,
  type CreatePromptVersionRequest,
  type CreatePromptVersionResponse,
  type PagedResult,
  type WorkflowTemplate,
  type WorkflowTemplateDetail,
  type ImportWorkflowResponse,
  type DashboardStats,
  type RecentActivityDto,
  type GetUserActivityResult,
  type UserActivityFilters,
  AdminSessionInfoSchema,
  type AdminSessionInfo,
  type GetAdminSessionsParams,
  GetAllApiKeysWithStatsResponseSchema,
  BulkImportApiKeysResultSchema,
  type GetAllApiKeysWithStatsResponse,
  type BulkImportApiKeysResult,
  ScheduleReportResponseSchema,
  GetScheduledReportsResponseSchema,
  GetReportExecutionsResponseSchema,
  type GenerateReportRequest,
  type ScheduleReportRequest,
  type UpdateReportScheduleRequest,
  type ScheduledReportDto,
  type ReportExecutionDto,
  type ScheduleReportResponse,
  AccessibilityMetricsSchema,
  PerformanceMetricsSchema,
  E2EMetricsSchema,
  type AccessibilityMetrics,
  type PerformanceMetrics,
  type E2EMetrics,
  AiModelDtoSchema,
  PagedAiModelsSchema,
  CostTrackingDtoSchema,
  TestModelResponseSchema,
  type AiModelDto,
  type PagedAiModels,
  type ConfigureModelRequest,
  type SetPrimaryModelRequest,
  type CostTrackingDto,
  type TestModelRequest,
  type TestModelResponse,
  type ExportUsageReportParams,
  TokenBalanceSchema,
  TokenConsumptionDataSchema,
  TierUsageListSchema,
  TopConsumersListSchema,
  type TokenBalance,
  type TokenConsumptionData,
  type TierUsageList,
  type TopConsumersList,
  type AddCreditsRequest,
  type UpdateTierLimitsRequest,
  BatchJobDtoSchema,
  BatchJobListSchema,
  CreateBatchJobResponseSchema,
  type BatchJobDto,
  type BatchJobList,
  type CreateBatchJobRequest,
  AppUsageStatsSchema,
  type AppUsageStats,
  BulkImportFromJsonResultSchema,
  type BulkImportFromJsonResult,
  PdfAnalyticsDtoSchema,
  type PdfAnalyticsDto,
  ChatAnalyticsDtoSchema,
  type ChatAnalyticsDto,
  ModelPerformanceDtoSchema,
  type ModelPerformanceDto,
  LlmSystemConfigDtoSchema,
  type LlmSystemConfigDto,
  type UpdateLlmSystemConfigRequest,
  AbTestSessionDtoSchema,
  type AbTestSessionDto,
  AbTestSessionListDtoSchema,
  type AbTestSessionListDto,
  AbTestSessionRevealedDtoSchema,
  type AbTestSessionRevealedDto,
  AbTestAnalyticsDtoSchema,
  type AbTestAnalyticsDto,
} from '../schemas';
import {
  BulkDeleteResultSchema,
  ReindexResponseSchema,
  MaintenanceResultSchema,
  PdfStatusDistributionSchema,
  PdfStorageHealthSchema,
  ProcessingMetricsSchema,
  VectorCollectionsResponseSchema,
  ProcessingQueueResponseSchema,
  PdfListResultSchema,
  type BulkDeleteResult,
  type ReindexResponse,
  type MaintenanceResult,
  type PdfStatusDistribution,
  type PdfStorageHealth,
  type ProcessingMetrics,
  type VectorCollectionsResponse,
  type ProcessingQueueResponse,
  type PdfListResult,
  OpenRouterStatusDtoSchema,
  type OpenRouterStatusDto,
  UsageTimelineDtoSchema,
  type UsageTimelineDto,
  UsageCostsDtoSchema,
  type UsageCostsDto,
  FreeQuotaDtoSchema,
  type FreeQuotaDto,
  RecentLlmRequestsDtoSchema,
  type RecentLlmRequestsDto,
} from '../schemas/admin-knowledge-base.schemas';
import {
  AgentCostEstimationResultSchema,
  CostScenariosResponseSchema,
  SaveScenarioResponseSchema,
  type AgentCostEstimationResult,
  type CostScenariosResponse,
  type SaveScenarioResponse,
  type EstimateAgentCostRequest,
  type SaveCostScenarioRequest,
  type GetCostScenariosParams,
} from '../schemas/cost-calculator.schemas';
import {
  EntityLinkDtoSchema,
  ImportBggExpansionsResponseSchema,
  type EntityLinkDto,
  type CreateEntityLinkRequest,
  type GetEntityLinksParams,
  type ImportBggExpansionsResponse,
} from '../schemas/entity-link.schemas';
import {
  LedgerEntriesResponseSchema,
  LedgerSummarySchema,
  LedgerEntryDtoSchema,
  LedgerDashboardDataSchema,
  CreateLedgerEntryResponseSchema,
  type LedgerEntriesResponse,
  type LedgerSummary,
  type LedgerEntryDto,
  type LedgerDashboardData,
  type CreateLedgerEntryResponse,
  type CreateLedgerEntryRequest,
  type UpdateLedgerEntryRequest,
  type GetLedgerEntriesParams,
  type GetLedgerSummaryParams,
  type ExportLedgerParams,
} from '../schemas/financial-ledger.schemas';
import * as MechanicExtractorSchemas from '../schemas/mechanic-extractor.schemas';
import {
  ResourceForecastEstimationResultSchema,
  ResourceForecastsResponseSchema,
  SaveForecastResponseSchema,
  type ResourceForecastEstimationResult,
  type ResourceForecastsResponse,
  type SaveForecastResponse,
  type EstimateResourceForecastRequest,
  type SaveResourceForecastRequest,
  type GetResourceForecastsParams,
} from '../schemas/resource-forecast.schemas';
import {
  type TierStrategyMatrixDto,
  type StrategyModelMappingDto,
  type ModelHealthResult,
  type ModelChangeHistoryResult,
} from '../schemas/tier-strategy.schemas';

import type { HttpClient } from '../core/httpClient';
import type * as MechanicExtractorTypes from '../schemas/mechanic-extractor.schemas';

// ============================================================================
// Route Constants — import in tests to avoid magic strings
// ============================================================================

export const ADMIN_PDF_ROUTES = {
  base: '/api/v1/admin/pdfs',
  bulkDelete: '/api/v1/admin/pdfs/bulk/delete',
  reindex: (pdfId: string) => `/api/v1/admin/pdfs/${encodeURIComponent(pdfId)}/reindex` as const,
  purgeStale: '/api/v1/admin/pdfs/maintenance/purge-stale',
  cleanupOrphans: '/api/v1/admin/pdfs/maintenance/cleanup-orphans',
  statusDistribution: '/api/v1/admin/pdfs/analytics/distribution',
  storageHealth: '/api/v1/admin/pdfs/storage/health',
  processingMetrics: '/api/v1/admin/pdfs/metrics/processing',
} as const;

export const ADMIN_KB_ROUTES = {
  vectorCollections: '/api/v1/admin/kb/vector-collections',
  processingQueue: '/api/v1/admin/kb/processing-queue',
} as const;

// ============================================================================

export interface CreateAdminClientParams {
  httpClient: HttpClient;
}

/**
 * Administration API client with Zod validation
 */
export function createAdminClient({ httpClient }: CreateAdminClientParams) {
  return {
    // ========== User Management ==========

    /**
     * Create new user (admin only)
     * POST /api/v1/admin/users
     */
    async createUser(request: CreateUserRequest): Promise<AdminUser> {
      const response = await httpClient.post(
        '/api/v1/admin/users',
        request,
        AdminUserResponseSchema
      );
      return response.user;
    },

    /**
     * Update existing user (admin only)
     * PUT /api/v1/admin/users/{userId}
     */
    async updateUser(userId: string, updates: UpdateUserRequest): Promise<void> {
      await httpClient.put(`/api/v1/admin/users/${userId}`, updates);
    },

    /**
     * Delete user (admin only)
     * DELETE /api/v1/admin/users/{userId}
     */
    async deleteUser(userId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/users/${userId}`);
    },

    /**
     * Update user tier (admin only) - Issue #3699
     * PUT /api/v1/admin/users/{id}/tier
     */
    async updateUserTier(userId: string, tier: string): Promise<AdminUser> {
      const result = await httpClient.put<AdminUser>(
        `/api/v1/admin/users/${userId}/tier`,
        { tier },
        AdminUserSchema
      );
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      return result!;
    },

    /**
     * Get all users (admin only) - Issue #903
     * GET /api/v1/admin/users
     */
    async getAllUsers(params?: {
      search?: string;
      role?: string;
      sortBy?: string;
      sortOrder?: 'asc' | 'desc';
      page?: number;
      limit?: number;
    }): Promise<{ users: AdminUser[]; total: number }> {
      const queryParams = new URLSearchParams();
      if (params?.search) queryParams.set('search', params.search);
      if (params?.role) queryParams.set('role', params.role);
      if (params?.sortBy) queryParams.set('sortBy', params.sortBy);
      if (params?.sortOrder) queryParams.set('sortOrder', params.sortOrder);
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.limit) queryParams.set('limit', params.limit.toString());

      const query = queryParams.toString();
      const result = await httpClient.get(
        `/api/v1/admin/users${query ? `?${query}` : ''}`,
        z.object({
          users: z.array(AdminUserSchema),
          total: z.number(),
        })
      );

      return result || { users: [], total: 0 };
    },

    /**
     * Export users to CSV (admin only) - Issue #903
     * GET /api/v1/admin/users/bulk/export
     */
    async exportUsersToCSV(params?: { role?: string; search?: string }): Promise<Blob> {
      const queryParams = new URLSearchParams();
      if (params?.role) queryParams.set('role', params.role);
      if (params?.search) queryParams.set('search', params.search);

      const query = queryParams.toString();
      const response = await fetch(
        `${httpClient['baseUrl']}/api/v1/admin/users/bulk/export${query ? `?${query}` : ''}`,
        {
          method: 'GET',
          credentials: 'include',
        }
      );

      if (!response.ok) {
        throw new Error(`Export failed: ${response.statusText}`);
      }

      return response.blob();
    },

    /**
     * Import users from CSV (admin only) - Issue #903
     * POST /api/v1/admin/users/bulk/import
     */
    async importUsersFromCSV(
      csvContent: string
    ): Promise<{ successCount: number; failureCount: number; errors: string[] }> {
      return httpClient.post(
        '/api/v1/admin/users/bulk/import',
        csvContent,
        z.object({
          successCount: z.number(),
          failureCount: z.number(),
          errors: z.array(z.string()),
        }),
        {
          headers: {
            'Content-Type': 'text/csv',
          },
        }
      );
    },

    // ========== Prompt Template Management ==========

    /**
     * Create new prompt template (admin only)
     * POST /api/v1/admin/prompts
     */
    async createPrompt(request: CreatePromptRequest): Promise<PromptTemplate> {
      const response = await httpClient.post(
        '/api/v1/admin/prompts',
        request,
        PromptResponseSchema
      );
      return response.template;
    },

    /**
     * Update existing prompt template (admin only)
     * PUT /api/v1/admin/prompts/{promptId}
     */
    async updatePrompt(promptId: string, updates: UpdatePromptRequest): Promise<PromptTemplate> {
      const response = await httpClient.put(
        `/api/v1/admin/prompts/${promptId}`,
        updates,
        PromptResponseSchema
      );
      return response.template;
    },

    /**
     * Delete prompt template (admin only)
     * DELETE /api/v1/admin/prompts/{promptId}
     */
    async deletePrompt(promptId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/prompts/${promptId}`);
    },

    /**
     * Activate specific prompt version (admin only)
     * POST /api/v1/admin/prompts/{id}/versions/{versionId}/activate
     */
    async activatePromptVersion(
      promptId: string,
      versionId: string
    ): Promise<ActivateVersionResponse> {
      return httpClient.post(
        `/api/v1/admin/prompts/${promptId}/versions/${versionId}/activate`,
        {},
        ActivateVersionResponseSchema
      );
    },

    /**
     * Get all prompt templates with pagination (admin only)
     * GET /api/v1/admin/prompts
     */
    async getPrompts(params?: {
      page?: number;
      pageSize?: number;
      search?: string;
      sortBy?: string;
      sortDirection?: 'asc' | 'desc';
    }): Promise<PagedResult<PromptTemplate>> {
      const queryParams = new URLSearchParams();
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());
      if (params?.search) queryParams.set('search', params.search);
      if (params?.sortBy) queryParams.set('sortBy', params.sortBy);
      if (params?.sortDirection) queryParams.set('sortDirection', params.sortDirection);

      const query = queryParams.toString();
      const result = await httpClient.get<PagedResult<PromptTemplate>>(
        `/api/v1/admin/prompts${query ? `?${query}` : ''}`,
        PagedResultSchema(PromptTemplateSchema)
      );
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Get single prompt template by ID (admin only)
     * GET /api/v1/admin/prompts/{id}
     */
    async getPromptById(promptId: string): Promise<PromptTemplate | null> {
      const response = await httpClient.get(
        `/api/v1/admin/prompts/${promptId}`,
        PromptResponseSchema
      );
      return response?.template ?? null;
    },

    /**
     * Get all versions of a prompt template (admin only)
     * GET /api/v1/admin/prompts/{id}/versions
     */
    async getPromptVersions(promptId: string): Promise<PromptVersion[]> {
      const response = await httpClient.get(
        `/api/v1/admin/prompts/${promptId}/versions`,
        PromptVersionsResponseSchema
      );
      return response?.versions ?? [];
    },

    /**
     * Get specific version of a prompt template (admin only)
     * GET /api/v1/admin/prompts/{id}/versions/{versionId}
     */
    async getPromptVersion(promptId: string, versionId: string): Promise<PromptVersion | null> {
      return httpClient.get<PromptVersion>(
        `/api/v1/admin/prompts/${promptId}/versions/${versionId}`
      );
    },

    /**
     * Create new version of prompt template (admin only)
     * POST /api/v1/admin/prompts/{id}/versions
     */
    async createPromptVersion(
      promptId: string,
      request: CreatePromptVersionRequest
    ): Promise<CreatePromptVersionResponse> {
      return httpClient.post(
        `/api/v1/admin/prompts/${promptId}/versions`,
        request,
        CreatePromptVersionResponseSchema
      );
    },

    /**
     * Get audit logs for prompt template (admin only)
     * GET /api/v1/prompts/{templateId}/audit-log
     */
    async getPromptAuditLogs(
      promptId: string,
      params?: {
        page?: number;
        pageSize?: number;
      }
    ): Promise<{ logs: PromptAuditLog[]; totalPages: number }> {
      const queryParams = new URLSearchParams();
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());

      const query = queryParams.toString();
      const result = await httpClient.get(
        `/api/v1/prompts/${promptId}/audit-log${query ? `?${query}` : ''}`,
        PromptAuditLogsResponseSchema
      );
      return result ?? { logs: [], totalPages: 0 };
    },

    // ========== Admin Stats & Monitoring ==========

    /**
     * Get admin dashboard statistics (admin only)
     * GET /api/v1/admin/stats
     */
    async getStats(): Promise<AdminStats> {
      const result = await httpClient.get<AdminStats>('/api/v1/admin/stats', AdminStatsSchema);
      if (!result) {
        throw new Error('Failed to fetch admin stats');
      }
      return result;
    },

    /**
     * Get AI requests with filtering (admin only)
     * GET /api/v1/admin/requests
     */
    async getAiRequests(params?: {
      page?: number;
      pageSize?: number;
      startDate?: Date;
      endDate?: Date;
      status?: string;
      endpoint?: string;
    }): Promise<{ requests: AiRequest[]; totalCount: number }> {
      const queryParams = new URLSearchParams();
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());
      if (params?.startDate) queryParams.set('startDate', params.startDate.toISOString());
      if (params?.endDate) queryParams.set('endDate', params.endDate.toISOString());
      if (params?.status) queryParams.set('status', params.status);
      if (params?.endpoint) queryParams.set('endpoint', params.endpoint);

      const query = queryParams.toString();
      const result = await httpClient.get(
        `/api/v1/admin/requests${query ? `?${query}` : ''}`,
        AiRequestsResponseSchema
      );
      return result ?? { requests: [], totalCount: 0 };
    },

    /**
     * Get analytics data (admin only)
     * GET /api/v1/admin/analytics
     *
     * Issue #1977: Added DashboardStatsSchema validation
     */
    async getAnalytics(params?: {
      startDate?: Date;
      endDate?: Date;
      groupBy?: string;
      roleFilter?: string;
    }): Promise<DashboardStats | null> {
      const queryParams = new URLSearchParams();
      if (params?.startDate) queryParams.set('fromDate', params.startDate.toISOString());
      if (params?.endDate) queryParams.set('toDate', params.endDate.toISOString());
      if (params?.groupBy) queryParams.set('groupBy', params.groupBy);
      if (params?.roleFilter) queryParams.set('roleFilter', params.roleFilter);

      const query = queryParams.toString();
      return httpClient.get(
        `/api/v1/admin/analytics${query ? `?${query}` : ''}`,
        DashboardStatsSchema
      );
    },

    /**
     * Get all users with pagination and filtering (admin only)
     * GET /api/v1/admin/users
     * Issue #3698: Added tier filter parameter
     */
    async getUsers(params?: {
      page?: number;
      pageSize?: number;
      search?: string;
      role?: string;
      status?: string;
      tier?: string; // Issue #3698: Filter by tier
    }): Promise<PagedResult<AdminUser>> {
      const queryParams = new URLSearchParams();
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());
      if (params?.search) queryParams.set('search', params.search);
      if (params?.role && params.role !== 'all') queryParams.set('role', params.role);
      if (params?.status && params.status !== 'all') queryParams.set('status', params.status);
      if (params?.tier && params.tier !== 'all') queryParams.set('tier', params.tier); // Issue #3698

      const query = queryParams.toString();
      const result = await httpClient.get<PagedResult<AdminUser>>(
        `/api/v1/admin/users${query ? `?${query}` : ''}`,
        PagedResultSchema(AdminUserSchema)
      );
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Suspend a user account (admin only)
     * POST /api/v1/admin/users/{userId}/suspend
     */
    async suspendUser(userId: string, reason?: string): Promise<AdminUser> {
      const response = await httpClient.post(
        `/api/v1/admin/users/${userId}/suspend`,
        { reason },
        AdminUserResponseSchema
      );
      return response.user;
    },

    /**
     * Unsuspend (reactivate) a user account (admin only)
     * POST /api/v1/admin/users/{userId}/unsuspend
     */
    async unsuspendUser(userId: string): Promise<AdminUser> {
      const response = await httpClient.post(
        `/api/v1/admin/users/${userId}/unsuspend`,
        {},
        AdminUserResponseSchema
      );
      return response.user;
    },

    // ========== N8N Workflow Templates ==========

    /**
     * Get all n8n workflow templates with optional category filter (admin only)
     * GET /api/v1/n8n/templates
     */
    async getWorkflowTemplates(category?: string): Promise<WorkflowTemplate[]> {
      const query = category ? `?category=${encodeURIComponent(category)}` : '';
      const result = await httpClient.get<WorkflowTemplate[]>(
        `/api/v1/n8n/templates${query}`,
        z.array(WorkflowTemplateSchema)
      );
      return result ?? [];
    },

    /**
     * Get workflow template details by ID (admin only)
     * GET /api/v1/n8n/templates/{templateId}
     */
    async getWorkflowTemplateById(templateId: string): Promise<WorkflowTemplateDetail | null> {
      return httpClient.get<WorkflowTemplateDetail>(
        `/api/v1/n8n/templates/${templateId}`,
        WorkflowTemplateDetailSchema
      );
    },

    /**
     * Import n8n workflow template (admin only)
     * POST /api/v1/n8n/templates/{templateId}/import
     */
    async importWorkflowTemplate(
      templateId: string,
      parameters: Record<string, string>
    ): Promise<ImportWorkflowResponse> {
      return httpClient.post(
        `/api/v1/n8n/templates/${templateId}/import`,
        { parameters },
        ImportWorkflowResponseSchema
      );
    },

    /**
     * Get recent activity feed (Issue #874)
     * GET /api/v1/admin/activity
     */
    async getRecentActivity(params?: { limit?: number; since?: Date }): Promise<RecentActivityDto> {
      const queryParams = new URLSearchParams();
      if (params?.limit) queryParams.set('limit', params.limit.toString());
      if (params?.since) queryParams.set('since', params.since.toISOString());

      const query = queryParams.toString();
      const result = await httpClient.get(
        `/api/v1/admin/activity${query ? `?${query}` : ''}`,
        RecentActivityDtoSchema
      );

      if (!result) {
        throw new Error('Failed to fetch recent activity');
      }

      return result;
    },

    // ========== Infrastructure Monitoring (Issue #896) ==========

    /**
     * Get comprehensive infrastructure details
     * GET /api/v1/admin/infrastructure/details
     *
     * Returns aggregated service health + Prometheus metrics
     * from backend Issues #891-894.
     */
    async getInfrastructureDetails() {
      return httpClient.get('/api/v1/admin/infrastructure/details', InfrastructureDetailsSchema);
    },

    /**
     * Get time-series metrics for infrastructure charts
     * GET /api/v1/admin/infrastructure/metrics/timeseries?range=1h
     *
     * Issue #901: Returns CPU, memory, and request rate data from Prometheus.
     */
    async getMetricsTimeSeries(range: '1h' | '6h' | '24h' | '7d' = '1h') {
      return httpClient.get(
        `/api/v1/admin/infrastructure/metrics/timeseries?range=${range}`,
        MetricsTimeSeriesResponseSchema
      );
    },

    // ========== API Key Management (Issue #908) ==========

    /**
     * Get all API keys with usage statistics (admin only)
     * GET /api/v1/admin/api-keys/stats
     */
    async getApiKeysWithStats(params?: {
      userId?: string;
      includeRevoked?: boolean;
    }): Promise<GetAllApiKeysWithStatsResponse> {
      const queryParams = new URLSearchParams();
      if (params?.userId) queryParams.set('userId', params.userId);
      if (params?.includeRevoked !== undefined)
        queryParams.set('includeRevoked', params.includeRevoked.toString());

      const query = queryParams.toString();
      const result = await httpClient.get(
        `/api/v1/admin/api-keys/stats${query ? `?${query}` : ''}`,
        GetAllApiKeysWithStatsResponseSchema
      );

      if (!result) {
        throw new Error('Failed to fetch API keys');
      }

      return result;
    },

    /**
     * Delete API key permanently (admin only)
     * DELETE /api/v1/admin/api-keys/{keyId}
     */
    async deleteApiKey(keyId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/api-keys/${keyId}`);
    },

    /**
     * Bulk export API keys to CSV (admin only)
     * GET /api/v1/admin/api-keys/bulk/export
     */
    async exportApiKeysToCSV(params?: {
      userId?: string;
      isActive?: boolean;
      searchTerm?: string;
    }): Promise<Blob> {
      const queryParams = new URLSearchParams();
      if (params?.userId) queryParams.set('userId', params.userId);
      if (params?.isActive !== undefined) queryParams.set('isActive', params.isActive.toString());
      if (params?.searchTerm) queryParams.set('searchTerm', params.searchTerm);

      const query = queryParams.toString();
      const response = await fetch(
        `${httpClient['baseUrl']}/api/v1/admin/api-keys/bulk/export${query ? `?${query}` : ''}`,
        {
          method: 'GET',
          credentials: 'include',
        }
      );

      if (!response.ok) {
        throw new Error(`Export failed: ${response.statusText}`);
      }

      return response.blob();
    },

    /**
     * Bulk import API keys from CSV (admin only)
     * POST /api/v1/admin/api-keys/bulk/import
     */
    async importApiKeysFromCSV(csvContent: string): Promise<BulkImportApiKeysResult> {
      return httpClient.post(
        '/api/v1/admin/api-keys/bulk/import',
        csvContent,
        BulkImportApiKeysResultSchema,
        {
          headers: {
            'Content-Type': 'text/csv',
          },
        }
      );
    },

    // ========== User Activity Timeline (Issue #911) ==========

    /**
     * Get system-wide activity timeline (admin only) - Issue #903
     * GET /api/v1/admin/activity
     */
    async getSystemActivity(params?: {
      actionFilter?: string;
      resourceFilter?: string;
      startDate?: Date;
      endDate?: Date;
      limit?: number;
    }): Promise<GetUserActivityResult> {
      const queryParams = new URLSearchParams();
      if (params?.actionFilter) queryParams.set('actionFilter', params.actionFilter);
      if (params?.resourceFilter) queryParams.set('resourceFilter', params.resourceFilter);
      if (params?.startDate) queryParams.set('startDate', params.startDate.toISOString());
      if (params?.endDate) queryParams.set('endDate', params.endDate.toISOString());
      if (params?.limit) queryParams.set('limit', params.limit.toString());

      const query = queryParams.toString();
      const result = await httpClient.get(
        `/api/v1/admin/activity${query ? `?${query}` : ''}`,
        GetUserActivityResultSchema
      );

      return result || { activities: [], totalCount: 0 };
    },

    /**
     * Get user activity timeline (admin - any user)
     * GET /api/v1/admin/users/{userId}/activity
     */
    async getUserActivity(
      userId: string,
      filters?: UserActivityFilters
    ): Promise<GetUserActivityResult> {
      const params = new URLSearchParams();
      if (filters?.actionFilter) params.append('actionFilter', filters.actionFilter);
      if (filters?.resourceFilter) params.append('resourceFilter', filters.resourceFilter);
      if (filters?.startDate) params.append('startDate', filters.startDate.toISOString());
      if (filters?.endDate) params.append('endDate', filters.endDate.toISOString());
      if (filters?.limit !== undefined) params.append('limit', filters.limit.toString());

      const query = params.toString();
      const result = await httpClient.get(
        `/api/v1/admin/users/${userId}/activity${query ? `?${query}` : ''}`,
        GetUserActivityResultSchema
      );
      return result ?? { activities: [], totalCount: 0 };
    },

    // ========== Admin Sessions Management ==========

    /**
     * Get all user sessions (admin only)
     * GET /api/v1/admin/sessions
     *
     * @param params - Optional filters (limit, userId)
     * @returns List of session info
     */
    async getAdminSessions(params?: GetAdminSessionsParams): Promise<AdminSessionInfo[]> {
      const queryParams = new URLSearchParams();
      if (params?.limit !== undefined) queryParams.append('limit', params.limit.toString());
      if (params?.userId) queryParams.append('userId', params.userId);

      const query = queryParams.toString();
      const result = await httpClient.get(
        `/api/v1/admin/sessions${query ? `?${query}` : ''}`,
        z.array(AdminSessionInfoSchema)
      );
      return result ?? [];
    },

    /**
     * Revoke a specific session (admin only)
     * DELETE /api/v1/admin/sessions/{sessionId}
     *
     * @param sessionId - Session UUID to revoke
     */
    async revokeSession(sessionId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/sessions/${sessionId}`);
    },

    /**
     * Revoke all sessions for a user (admin only)
     * DELETE /api/v1/admin/users/{userId}/sessions
     *
     * @param userId - User UUID
     */
    async revokeAllUserSessions(userId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/users/${userId}/sessions`);
    },

    // ========== Report Generation & Scheduling (Issue #920) ==========

    /**
     * Generate a report on-demand (admin only)
     * POST /api/v1/admin/reports/generate
     * Returns file download (Blob)
     */
    async generateReport(request: GenerateReportRequest): Promise<Blob> {
      const response = await fetch(`${httpClient['baseUrl']}/api/v1/admin/reports/generate`, {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        throw new Error(`Report generation failed: ${response.statusText}`);
      }

      return response.blob();
    },

    /**
     * Schedule a recurring report (admin only)
     * POST /api/v1/admin/reports/schedule
     */
    async scheduleReport(request: ScheduleReportRequest): Promise<ScheduleReportResponse> {
      return httpClient.post(
        '/api/v1/admin/reports/schedule',
        request,
        ScheduleReportResponseSchema
      );
    },

    /**
     * Get all scheduled reports (admin only)
     * GET /api/v1/admin/reports/scheduled
     */
    async getScheduledReports(): Promise<ScheduledReportDto[]> {
      const result = await httpClient.get(
        '/api/v1/admin/reports/scheduled',
        GetScheduledReportsResponseSchema
      );
      return result ?? [];
    },

    /**
     * Get report execution history (admin only)
     * GET /api/v1/admin/reports/executions
     */
    async getReportExecutions(params?: { reportId?: string }): Promise<ReportExecutionDto[]> {
      const queryParams = new URLSearchParams();
      if (params?.reportId) queryParams.set('reportId', params.reportId);

      const query = queryParams.toString();
      const result = await httpClient.get(
        `/api/v1/admin/reports/executions${query ? `?${query}` : ''}`,
        GetReportExecutionsResponseSchema
      );
      return result ?? [];
    },

    /**
     * Update report schedule (admin only)
     * PATCH /api/v1/admin/reports/{reportId}/schedule
     */
    async updateReportSchedule(
      reportId: string,
      request: UpdateReportScheduleRequest
    ): Promise<void> {
      await httpClient.patch(`/api/v1/admin/reports/${reportId}/schedule`, request);
    },

    /**
     * Delete scheduled report (admin only)
     * DELETE /api/v1/admin/reports/{reportId}
     */
    async deleteScheduledReport(reportId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/reports/${reportId}`);
    },

    // ========== Testing Dashboard (Issue #2139) ==========

    /**
     * Get accessibility test metrics (admin only)
     * GET /api/v1/admin/testing/accessibility
     */
    async getAccessibilityMetrics(): Promise<AccessibilityMetrics> {
      const result = await httpClient.get(
        '/api/v1/admin/testing/accessibility',
        AccessibilityMetricsSchema
      );
      if (!result) {
        throw new Error('Failed to fetch accessibility metrics');
      }
      return result;
    },

    /**
     * Get performance test metrics (admin only)
     * GET /api/v1/admin/testing/performance
     */
    async getPerformanceMetrics(): Promise<PerformanceMetrics> {
      const result = await httpClient.get(
        '/api/v1/admin/testing/performance',
        PerformanceMetricsSchema
      );
      if (!result) {
        throw new Error('Failed to fetch performance metrics');
      }
      return result;
    },

    /**
     * Get E2E test metrics (admin only)
     * GET /api/v1/admin/testing/e2e
     */
    async getE2EMetrics(): Promise<E2EMetrics> {
      const result = await httpClient.get('/api/v1/admin/testing/e2e', E2EMetricsSchema);
      if (!result) {
        throw new Error('Failed to fetch E2E metrics');
      }
      return result;
    },

    // ========== AI Models Management (Issue #2521) ==========

    /**
     * Get all AI models with usage statistics (admin only)
     * GET /api/v1/admin/ai-models
     */
    async getAiModels(params?: {
      status?: 'active' | 'inactive' | 'all';
      page?: number;
      pageSize?: number;
    }): Promise<PagedAiModels> {
      const queryParams = new URLSearchParams();
      if (params?.status && params.status !== 'all') {
        queryParams.append('status', params.status);
      }
      if (params?.page !== undefined) {
        queryParams.append('page', params.page.toString());
      }
      if (params?.pageSize !== undefined) {
        queryParams.append('pageSize', params.pageSize.toString());
      }

      const queryString = queryParams.toString();
      const url = queryString
        ? `/api/v1/admin/ai-models?${queryString}`
        : '/api/v1/admin/ai-models';

      const result = await httpClient.get(url, PagedAiModelsSchema);
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Get AI model by ID (admin only)
     * GET /api/v1/admin/ai-models/{modelId}
     */
    async getAiModelById(modelId: string): Promise<AiModelDto> {
      const result = await httpClient.get(
        `/api/v1/admin/ai-models/${encodeURIComponent(modelId)}`,
        AiModelDtoSchema
      );
      if (!result) {
        throw new Error(`AI model ${modelId} not found`);
      }
      return result;
    },

    /**
     * Update AI model configuration (admin only)
     * PUT /api/v1/admin/ai-models/{modelId}/configure
     */
    async updateModelConfig(modelId: string, request: ConfigureModelRequest): Promise<AiModelDto> {
      const result = await httpClient.put(
        `/api/v1/admin/ai-models/${encodeURIComponent(modelId)}/configure`,
        request,
        AiModelDtoSchema
      );
      if (!result) {
        throw new Error('Failed to update model configuration');
      }
      return result;
    },

    /**
     * Set primary AI model (admin only)
     * POST /api/v1/admin/ai-models/set-primary
     */
    async setPrimaryModel(request: SetPrimaryModelRequest): Promise<AiModelDto> {
      const result = await httpClient.post(
        '/api/v1/admin/ai-models/set-primary',
        request,
        AiModelDtoSchema
      );
      if (!result) {
        throw new Error('Failed to set primary model');
      }
      return result;
    },

    /**
     * Get cost tracking information (admin only)
     * GET /api/v1/admin/ai-models/cost-tracking
     */
    async getCostTracking(): Promise<CostTrackingDto> {
      const result = await httpClient.get(
        '/api/v1/admin/ai-models/cost-tracking',
        CostTrackingDtoSchema
      );
      if (!result) {
        throw new Error('Failed to fetch cost tracking data');
      }
      return result;
    },

    /**
     * Get OpenRouter account status and rate-limit utilization snapshot.
     * GET /api/v1/admin/openrouter/status
     * Issue #5077: Admin usage dashboard KPI cards.
     */
    async getOpenRouterStatus(): Promise<OpenRouterStatusDto | null> {
      const result = await httpClient.get(
        '/api/v1/admin/openrouter/status',
        OpenRouterStatusDtoSchema
      );
      return result ?? null;
    },

    /**
     * Test AI model with sample prompt (admin only)
     * POST /api/v1/admin/ai-models/{modelId}/test
     */
    async testModel(modelId: string, request: TestModelRequest): Promise<TestModelResponse> {
      const result = await httpClient.post(
        `/api/v1/admin/ai-models/${encodeURIComponent(modelId)}/test`,
        request,
        TestModelResponseSchema
      );
      if (!result) {
        throw new Error('Failed to test model');
      }
      return result;
    },

    /**
     * Export usage report (admin only)
     * GET /api/v1/admin/ai-models/export
     */
    async exportUsageReport(params: ExportUsageReportParams): Promise<Blob> {
      const queryParams = new URLSearchParams();
      if (params.modelId) queryParams.append('modelId', params.modelId);
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
      queryParams.append('format', params.format);

      const queryString = queryParams.toString();
      const url = `/api/v1/admin/ai-models/export?${queryString}`;

      const response = await fetch(`${getApiBase()}${url}`, {
        method: 'GET',
        credentials: 'include',
        headers: {
          Accept: params.format === 'csv' ? 'text/csv' : 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error(`Failed to export usage report: ${response.status}`);
      }

      return response.blob();
    },

    // ========== User Detail Endpoints (Issue #2890) ==========

    /**
     * Get complete user details (admin only)
     * GET /api/v1/admin/users/{userId}
     */
    async getUserDetail(userId: string): Promise<AdminUser> {
      const result = await httpClient.get(
        `/api/v1/admin/users/${encodeURIComponent(userId)}`,
        AdminUserSchema
      );
      if (!result) {
        throw new Error('User not found');
      }
      return result;
    },

    /**
     * Get user badges including hidden ones (admin only)
     * GET /api/v1/admin/users/{userId}/badges
     */
    async getUserBadges(userId: string): Promise<UserBadge[]> {
      const UserBadgeSchema = z.object({
        id: z.string(),
        code: z.string(),
        name: z.string(),
        description: z.string(),
        iconUrl: z.string().nullable(),
        tier: z.string(),
        earnedAt: z.string(),
        isDisplayed: z.boolean(),
      });
      const result = await httpClient.get(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/badges`,
        z.array(UserBadgeSchema)
      );
      return result || [];
    },

    /**
     * Get user library statistics (admin only)
     * GET /api/v1/admin/users/{userId}/library/stats
     */
    async getUserLibraryStats(userId: string): Promise<UserLibraryStats> {
      const UserLibraryStatsSchema = z.object({
        totalGames: z.number(),
        sessionsPlayed: z.number(),
        avgSessionDuration: z.number().nullable(),
      });
      const result = await httpClient.get(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/library/stats`,
        UserLibraryStatsSchema
      );
      if (!result) {
        throw new Error('Library stats not found');
      }
      return result;
    },

    /**
     * Get user role change history (admin only)
     * GET /api/v1/admin/users/{userId}/role-history
     */
    async getUserRoleHistory(userId: string): Promise<RoleChangeHistory[]> {
      const RoleChangeHistorySchema = z.object({
        changedAt: z.string(),
        oldRole: z.string(),
        newRole: z.string(),
        changedBy: z.string(),
        changedByDisplayName: z.string(),
        ipAddress: z.string().nullable(),
      });
      const result = await httpClient.get(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/role-history`,
        z.array(RoleChangeHistorySchema)
      );
      return result || [];
    },

    /**
     * Reset user password (admin only)
     * POST /api/v1/admin/users/{userId}/reset-password
     */
    async resetUserPassword(userId: string, newPassword: string): Promise<void> {
      await httpClient.post(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/reset-password`,
        { newPassword },
        z.object({ message: z.string() })
      );
    },

    /**
     * Send email to user (admin only)
     * POST /api/v1/admin/users/{userId}/send-email
     */
    async sendUserEmail(userId: string, subject: string, body: string): Promise<void> {
      await httpClient.post(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/send-email`,
        { subject, body },
        z.object({ message: z.string() })
      );
    },

    /**
     * Impersonate user for debugging (admin only - HIGH SECURITY RISK)
     * POST /api/v1/admin/users/{userId}/impersonate
     */
    async impersonateUser(userId: string): Promise<ImpersonateUserResponse> {
      const ImpersonateUserResponseSchema = z.object({
        sessionToken: z.string(),
        impersonatedUserId: z.string(),
        expiresAt: z.string(),
      });
      const result = await httpClient.post(
        `/api/v1/admin/users/${encodeURIComponent(userId)}/impersonate`,
        {},
        ImpersonateUserResponseSchema
      );
      if (!result) {
        throw new Error('Failed to impersonate user');
      }
      return result;
    },

    /**
     * End an impersonation session (admin only)
     * POST /api/v1/admin/impersonation/end
     * Issue #3349
     */
    async endImpersonation(sessionId: string): Promise<EndImpersonationResponse> {
      const EndImpersonationResponseSchema = z.object({
        success: z.boolean(),
        message: z.string(),
      });
      const result = await httpClient.post(
        '/api/v1/admin/impersonation/end',
        { sessionId },
        EndImpersonationResponseSchema
      );
      if (!result) {
        throw new Error('Failed to end impersonation');
      }
      return result;
    },

    // ========== Agent Typologies Management (Issue #3179) ==========

    /**
     * Get all agent typologies (Admin/Editor)
     * AGT-005: Admin typologies list page
     */
    async getAgentTypologies(params?: {
      status?: string;
      createdBy?: string;
      search?: string;
      page?: number;
      pageSize?: number;
    }) {
      const queryParams = new URLSearchParams();
      if (params?.status && params.status !== 'All') queryParams.set('status', params.status);
      if (params?.createdBy && params.createdBy !== 'All')
        queryParams.set('createdBy', params.createdBy);
      if (params?.search) queryParams.set('search', params.search);
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());

      const url = `/admin/agent-typologies${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;
      const result = await httpClient.get<AgentTypologyListResponse>(url);
      return result || { typologies: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Get agent typology by ID (Admin/Editor)
     * AGT-005: View typology details
     */
    async getAgentTypologyById(id: string) {
      return httpClient.get<AgentTypology>(`/admin/agent-typologies/${id}`);
    },

    /**
     * Delete agent typology (soft delete, Admin only)
     * AGT-005: Admin typology management
     */
    async deleteAgentTypology(id: string) {
      await httpClient.delete(`/admin/agent-typologies/${id}`);
    },

    /**
     * Approve agent typology (Admin only)
     * AGT-005: Admin typology approval workflow
     */
    async approveAgentTypology(id: string) {
      return httpClient.post<AgentTypology>(`/admin/agent-typologies/${id}/approve`, {});
    },

    /**
     * Reject agent typology (Admin only)
     * AGT-007: Admin typology rejection workflow
     */
    async rejectAgentTypology(id: string, reason: string) {
      return httpClient.post<AgentTypology>(`/admin/agent-typologies/${id}/reject`, {
        reason,
      });
    },

    // ========== Game Publication (Issue #3480 + #3481) ==========

    /**
     * Publish game to SharedGameCatalog with approval status
     * PUT /api/v1/games/{id}/publish
     *
     * Issue #3480: Admin wizard publish step
     * Issue #3481: Backend publication workflow
     */
    async publishGameToSharedLibrary(gameId: string, status: ApprovalStatus) {
      return httpClient.put<PublishGameResponse>(
        `/api/v1/games/${gameId}/publish`,
        { status },
        PublishGameResponseSchema
      );
    },

    // ========== Game Import Wizard (Issue #4163) ==========

    /**
     * Extract game metadata from uploaded PDF using AI
     * POST /api/v1/admin/games/wizard/extract-metadata
     *
     * Issue #4163: Step 2 - Metadata Extraction
     */
    async extractGameMetadata(filePath: string) {
      return httpClient.post<{
        title?: string;
        year?: number;
        minPlayers?: number;
        maxPlayers?: number;
        playingTime?: number;
        minAge?: number;
        description?: string;
        confidenceScore: number; // 0.0-1.0
      }>('/api/v1/admin/games/wizard/extract-metadata', { filePath });
    },

    // ========== Audit Log (Issue #3691) ==========

    /**
     * Get paginated audit log entries with optional filters
     * GET /api/v1/admin/audit-log
     */
    async getAuditLogs(params?: {
      limit?: number;
      offset?: number;
      adminUserId?: string;
      action?: string;
      resource?: string;
      result?: string;
      startDate?: string;
      endDate?: string;
    }): Promise<AuditLogListResult> {
      const searchParams = new URLSearchParams();
      if (params?.limit) searchParams.set('limit', String(params.limit));
      if (params?.offset) searchParams.set('offset', String(params.offset));
      if (params?.adminUserId) searchParams.set('adminUserId', params.adminUserId);
      if (params?.action) searchParams.set('action', params.action);
      if (params?.resource) searchParams.set('resource', params.resource);
      if (params?.result) searchParams.set('result', params.result);
      if (params?.startDate) searchParams.set('startDate', params.startDate);
      if (params?.endDate) searchParams.set('endDate', params.endDate);
      const qs = searchParams.toString();
      const url = `/api/v1/admin/audit-log${qs ? `?${qs}` : ''}`;
      const result = await httpClient.get<AuditLogListResult>(url, AuditLogListResultSchema);
      if (!result) {
        return {
          entries: [],
          totalCount: 0,
          limit: params?.limit ?? 50,
          offset: params?.offset ?? 0,
        };
      }
      return result;
    },

    /**
     * Export audit log entries as CSV
     * GET /api/v1/admin/audit-log/export
     */
    async exportAuditLogs(params?: {
      adminUserId?: string;
      action?: string;
      resource?: string;
      result?: string;
      startDate?: string;
      endDate?: string;
    }): Promise<Blob> {
      const searchParams = new URLSearchParams();
      if (params?.adminUserId) searchParams.set('adminUserId', params.adminUserId);
      if (params?.action) searchParams.set('action', params.action);
      if (params?.resource) searchParams.set('resource', params.resource);
      if (params?.result) searchParams.set('result', params.result);
      if (params?.startDate) searchParams.set('startDate', params.startDate);
      if (params?.endDate) searchParams.set('endDate', params.endDate);
      const qs = searchParams.toString();
      const url = `${getApiBase()}/api/v1/admin/audit-log/export${qs ? `?${qs}` : ''}`;
      const response = await fetch(url, { credentials: 'include' });
      if (!response.ok) throw new Error(`Export failed: ${response.statusText}`);
      return response.blob();
    },

    // ========== Token Management (Issue #3692) ==========

    /**
     * Get current token balance and usage summary
     * GET /api/v1/admin/resources/tokens
     */
    async getTokenBalance(): Promise<TokenBalance> {
      const result = await httpClient.get<TokenBalance>(
        '/api/v1/admin/resources/tokens',
        TokenBalanceSchema
      );
      if (!result) throw new Error('No token balance data returned');
      return result;
    },

    /**
     * Get token consumption trend data
     * GET /api/v1/admin/resources/tokens/consumption
     */
    async getTokenConsumption(days: number = 30): Promise<TokenConsumptionData> {
      const result = await httpClient.get<TokenConsumptionData>(
        `/api/v1/admin/resources/tokens/consumption?days=${days}`,
        TokenConsumptionDataSchema
      );
      if (!result) throw new Error('No consumption data returned');
      return result;
    },

    /**
     * Get token usage breakdown per tier
     * GET /api/v1/admin/resources/tokens/tiers
     */
    async getTokenTierUsage(): Promise<TierUsageList> {
      const result = await httpClient.get<TierUsageList>(
        '/api/v1/admin/resources/tokens/tiers',
        TierUsageListSchema
      );
      if (!result) throw new Error('No tier usage data returned');
      return result;
    },

    /**
     * Get top token consumers
     * GET /api/v1/admin/resources/tokens/top-consumers
     */
    async getTopConsumers(limit: number = 10): Promise<TopConsumersList> {
      const result = await httpClient.get<TopConsumersList>(
        `/api/v1/admin/resources/tokens/top-consumers?limit=${limit}`,
        TopConsumersListSchema
      );
      if (!result) throw new Error('No consumer data returned');
      return result;
    },

    /**
     * Update tier token limits
     * PUT /api/v1/admin/resources/tokens/tiers/{tier}
     */
    async updateTierLimits(request: UpdateTierLimitsRequest): Promise<void> {
      await httpClient.put(
        `/api/v1/admin/resources/tokens/tiers/${request.tier}`,
        request,
        z.any()
      );
    },

    /**
     * Add credits to token balance
     * POST /api/v1/admin/resources/tokens/add-credits
     */
    async addTokenCredits(request: AddCreditsRequest): Promise<void> {
      await httpClient.post('/api/v1/admin/resources/tokens/add-credits', request, z.any());
    },

    // ========== Batch Jobs (Issue #3693) ==========

    /**
     * Get all batch jobs with optional filters
     * GET /api/v1/admin/batch-jobs
     */
    async getAllBatchJobs(params?: {
      status?: string;
      page?: number;
      pageSize?: number;
    }): Promise<BatchJobList> {
      const queryParams = new URLSearchParams();
      if (params?.status && params.status !== 'all') queryParams.set('status', params.status);
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());

      const query = queryParams.toString();
      const result = await httpClient.get<BatchJobList>(
        `/api/v1/admin/batch-jobs${query ? `?${query}` : ''}`,
        BatchJobListSchema
      );
      return result ?? { jobs: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Get batch job by ID
     * GET /api/v1/admin/batch-jobs/{id}
     */
    async getBatchJob(id: string): Promise<BatchJobDto> {
      const result = await httpClient.get<BatchJobDto>(
        `/api/v1/admin/batch-jobs/${id}`,
        BatchJobDtoSchema
      );
      if (!result) throw new Error(`Batch job ${id} not found`);
      return result;
    },

    /**
     * Create new batch job
     * POST /api/v1/admin/batch-jobs
     */
    async createBatchJob(request: CreateBatchJobRequest): Promise<{ id: string }> {
      return httpClient.post('/api/v1/admin/batch-jobs', request, CreateBatchJobResponseSchema);
    },

    /**
     * Cancel batch job
     * POST /api/v1/admin/batch-jobs/{id}/cancel
     */
    async cancelBatchJob(id: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/batch-jobs/${id}/cancel`, {});
    },

    /**
     * Retry failed batch job
     * POST /api/v1/admin/batch-jobs/{id}/retry
     */
    async retryBatchJob(id: string): Promise<void> {
      await httpClient.post(`/api/v1/admin/batch-jobs/${id}/retry`, {});
    },

    /**
     * Delete batch job
     * DELETE /api/v1/admin/batch-jobs/{id}
     */
    async deleteBatchJob(id: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/batch-jobs/${id}`);
    },

    // ========== Financial Ledger (Issue #3722) ==========

    /**
     * Get paginated and filtered ledger entries
     * GET /api/v1/admin/financial-ledger
     */
    async getLedgerEntries(params?: GetLedgerEntriesParams): Promise<LedgerEntriesResponse> {
      const searchParams = new URLSearchParams();
      if (params?.page) searchParams.set('page', params.page.toString());
      if (params?.pageSize) searchParams.set('pageSize', params.pageSize.toString());
      if (params?.type !== undefined && params?.type !== null)
        searchParams.set('type', params.type.toString());
      if (params?.category !== undefined && params?.category !== null)
        searchParams.set('category', params.category.toString());
      if (params?.source !== undefined && params?.source !== null)
        searchParams.set('source', params.source.toString());
      if (params?.dateFrom) searchParams.set('dateFrom', params.dateFrom);
      if (params?.dateTo) searchParams.set('dateTo', params.dateTo);

      const qs = searchParams.toString();
      const url = `/api/v1/admin/financial-ledger${qs ? `?${qs}` : ''}`;
      const result = await httpClient.get(url, LedgerEntriesResponseSchema);
      return result || { entries: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Get a single ledger entry by ID
     * GET /api/v1/admin/financial-ledger/{id}
     */
    async getLedgerEntryById(id: string): Promise<LedgerEntryDto> {
      const result = await httpClient.get(
        `/api/v1/admin/financial-ledger/${id}`,
        LedgerEntryDtoSchema
      );
      if (!result) throw new Error('Ledger entry not found');
      return result;
    },

    /**
     * Get income/expense summary for a date range
     * GET /api/v1/admin/financial-ledger/summary
     */
    async getLedgerSummary(params: GetLedgerSummaryParams): Promise<LedgerSummary> {
      const qs = new URLSearchParams({
        dateFrom: params.dateFrom,
        dateTo: params.dateTo,
      }).toString();
      const result = await httpClient.get(
        `/api/v1/admin/financial-ledger/summary?${qs}`,
        LedgerSummarySchema
      );
      return (
        result || {
          totalIncome: 0,
          totalExpense: 0,
          netBalance: 0,
          from: params.dateFrom,
          to: params.dateTo,
        }
      );
    },

    /**
     * Create a new manual ledger entry
     * POST /api/v1/admin/financial-ledger
     */
    async createLedgerEntry(request: CreateLedgerEntryRequest): Promise<CreateLedgerEntryResponse> {
      return httpClient.post(
        '/api/v1/admin/financial-ledger',
        request,
        CreateLedgerEntryResponseSchema
      );
    },

    /**
     * Update an existing ledger entry
     * PUT /api/v1/admin/financial-ledger/{id}
     */
    async updateLedgerEntry(id: string, request: UpdateLedgerEntryRequest): Promise<void> {
      await httpClient.put(`/api/v1/admin/financial-ledger/${id}`, request);
    },

    /**
     * Delete a manual ledger entry
     * DELETE /api/v1/admin/financial-ledger/{id}
     */
    async deleteLedgerEntry(id: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/financial-ledger/${id}`);
    },

    /**
     * Get dashboard visualization data (12-month revenue vs costs, category breakdown, KPIs)
     * GET /api/v1/admin/financial-ledger/dashboard
     */
    async getLedgerDashboard(): Promise<LedgerDashboardData> {
      const result = await httpClient.get(
        '/api/v1/admin/financial-ledger/dashboard',
        LedgerDashboardDataSchema
      );
      if (!result) throw new Error('Failed to load ledger dashboard data');
      return result;
    },

    /**
     * Export ledger entries as CSV, Excel, or PDF
     * GET /api/v1/admin/financial-ledger/export
     * Issue #3724: Export Ledger (PDF/CSV/Excel)
     */
    async exportLedgerEntries(params: ExportLedgerParams): Promise<Blob> {
      const searchParams = new URLSearchParams();
      searchParams.set('format', String(params.format));
      if (params.dateFrom) searchParams.set('dateFrom', params.dateFrom);
      if (params.dateTo) searchParams.set('dateTo', params.dateTo);
      if (params.type !== undefined) searchParams.set('type', String(params.type));
      if (params.category !== undefined) searchParams.set('category', String(params.category));
      const qs = searchParams.toString();
      const url = `${getApiBase()}/api/v1/admin/financial-ledger/export${qs ? `?${qs}` : ''}`;
      const response = await fetch(url, { credentials: 'include' });
      if (!response.ok) throw new Error(`Export failed: ${response.statusText}`);
      return response.blob();
    },

    // ========== Cost Calculator (Issue #3725) ==========

    /**
     * Estimate agent costs based on strategy, model, and usage parameters
     * POST /api/v1/admin/cost-calculator/estimate
     */
    async estimateAgentCost(request: EstimateAgentCostRequest): Promise<AgentCostEstimationResult> {
      return httpClient.post(
        '/api/v1/admin/cost-calculator/estimate',
        request,
        AgentCostEstimationResultSchema
      );
    },

    /**
     * Save a cost estimation scenario
     * POST /api/v1/admin/cost-calculator/scenarios
     */
    async saveCostScenario(request: SaveCostScenarioRequest): Promise<SaveScenarioResponse> {
      return httpClient.post(
        '/api/v1/admin/cost-calculator/scenarios',
        request,
        SaveScenarioResponseSchema
      );
    },

    /**
     * Get saved cost scenarios for the current user
     * GET /api/v1/admin/cost-calculator/scenarios
     */
    async getCostScenarios(params?: GetCostScenariosParams): Promise<CostScenariosResponse> {
      const searchParams = new URLSearchParams();
      if (params?.page) searchParams.set('page', params.page.toString());
      if (params?.pageSize) searchParams.set('pageSize', params.pageSize.toString());
      const qs = searchParams.toString();
      const url = `/api/v1/admin/cost-calculator/scenarios${qs ? `?${qs}` : ''}`;
      const result = await httpClient.get(url, CostScenariosResponseSchema);
      return result || { items: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Delete a saved cost scenario
     * DELETE /api/v1/admin/cost-calculator/scenarios/{id}
     */
    async deleteCostScenario(id: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/cost-calculator/scenarios/${id}`);
    },

    // ========== Resource Forecast Methods (Issue #3726) ==========

    /**
     * Estimate resource forecast based on current metrics and growth parameters
     * POST /api/v1/admin/resource-forecast/estimate
     */
    async estimateResourceForecast(
      request: EstimateResourceForecastRequest
    ): Promise<ResourceForecastEstimationResult> {
      return httpClient.post(
        '/api/v1/admin/resource-forecast/estimate',
        request,
        ResourceForecastEstimationResultSchema
      );
    },

    /**
     * Save a resource forecast scenario
     * POST /api/v1/admin/resource-forecast/scenarios
     */
    async saveResourceForecast(
      request: SaveResourceForecastRequest
    ): Promise<SaveForecastResponse> {
      return httpClient.post(
        '/api/v1/admin/resource-forecast/scenarios',
        request,
        SaveForecastResponseSchema
      );
    },

    /**
     * Get saved resource forecast scenarios for the current user
     * GET /api/v1/admin/resource-forecast/scenarios
     */
    async getResourceForecasts(
      params?: GetResourceForecastsParams
    ): Promise<ResourceForecastsResponse> {
      const searchParams = new URLSearchParams();
      if (params?.page) searchParams.set('page', params.page.toString());
      if (params?.pageSize) searchParams.set('pageSize', params.pageSize.toString());
      const qs = searchParams.toString();
      const url = `/api/v1/admin/resource-forecast/scenarios${qs ? `?${qs}` : ''}`;
      const result = await httpClient.get(url, ResourceForecastsResponseSchema);
      return result || { items: [], total: 0, page: 1, pageSize: 20 };
    },

    /**
     * Delete a saved resource forecast scenario
     * DELETE /api/v1/admin/resource-forecast/scenarios/{id}
     */
    async deleteResourceForecast(id: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/resource-forecast/scenarios/${id}`);
    },

    // ========== App Usage Stats (Issue #3719) ==========

    /**
     * Get app usage statistics (DAU/MAU, retention, feature adoption, geo)
     * GET /api/v1/admin/usage-stats
     */
    async getUsageStats(params?: { period?: '7d' | '30d' | '90d' }): Promise<AppUsageStats | null> {
      const queryParams = new URLSearchParams();
      if (params?.period) queryParams.set('period', params.period);

      const query = queryParams.toString();
      return httpClient.get(
        `/api/v1/admin/usage-stats${query ? `?${query}` : ''}`,
        AppUsageStatsSchema
      );
    },

    // ========== Game Bulk Import (Issue #4355) ==========

    /**
     * Bulk import games from JSON
     * POST /api/v1/admin/games/bulk-import
     *
     * Issue #4355: Frontend - Bulk Import Upload UI
     */
    async bulkImportGames(jsonContent: string): Promise<BulkImportFromJsonResult> {
      return httpClient.post(
        '/api/v1/admin/games/bulk-import',
        { jsonContent },
        BulkImportFromJsonResultSchema
      );
    },

    // ========== RAG Execution History (Issue #4458) ==========

    /**
     * Get paginated RAG execution history with filters
     * GET /api/v1/admin/rag-executions
     */
    async getRagExecutions(params?: {
      skip?: number;
      take?: number;
      strategy?: string;
      status?: string;
      minLatencyMs?: number;
      maxLatencyMs?: number;
      minConfidence?: number;
      dateFrom?: string;
      dateTo?: string;
    }): Promise<RagExecutionListResult> {
      const searchParams = new URLSearchParams();
      if (params?.skip) searchParams.set('skip', params.skip.toString());
      if (params?.take) searchParams.set('take', params.take.toString());
      if (params?.strategy) searchParams.set('strategy', params.strategy);
      if (params?.status) searchParams.set('status', params.status);
      if (params?.minLatencyMs) searchParams.set('minLatencyMs', params.minLatencyMs.toString());
      if (params?.maxLatencyMs) searchParams.set('maxLatencyMs', params.maxLatencyMs.toString());
      if (params?.minConfidence) searchParams.set('minConfidence', params.minConfidence.toString());
      if (params?.dateFrom) searchParams.set('dateFrom', params.dateFrom);
      if (params?.dateTo) searchParams.set('dateTo', params.dateTo);
      const qs = searchParams.toString();
      const url = `/api/v1/admin/rag-executions${qs ? `?${qs}` : ''}`;
      const result = await httpClient.get<RagExecutionListResult>(url);
      if (!result) {
        return { items: [], totalCount: 0 };
      }
      return result;
    },

    /**
     * Get RAG execution detail by ID
     * GET /api/v1/admin/rag-executions/{id}
     */
    async getRagExecutionById(id: string): Promise<RagExecutionDetail | null> {
      return httpClient.get<RagExecutionDetail>(`/api/v1/admin/rag-executions/${id}`);
    },

    /**
     * Get RAG execution aggregated stats
     * GET /api/v1/admin/rag-executions/stats
     */
    async getRagExecutionStats(params?: {
      dateFrom?: string;
      dateTo?: string;
    }): Promise<RagExecutionStatsResult> {
      const searchParams = new URLSearchParams();
      if (params?.dateFrom) searchParams.set('dateFrom', params.dateFrom);
      if (params?.dateTo) searchParams.set('dateTo', params.dateTo);
      const qs = searchParams.toString();
      const url = `/api/v1/admin/rag-executions/stats${qs ? `?${qs}` : ''}`;
      const result = await httpClient.get<RagExecutionStatsResult>(url);
      if (!result) {
        return {
          totalExecutions: 0,
          avgLatencyMs: 0,
          errorRate: 0,
          cacheHitRate: 0,
          totalCost: 0,
          avgConfidence: 0,
        };
      }
      return result;
    },

    /**
     * Get tier-strategy access matrix
     * GET /api/v1/admin/tier-strategy/matrix
     */
    async getTierStrategyMatrix(): Promise<TierStrategyMatrixDto> {
      const result = await httpClient.get('/api/v1/admin/tier-strategy/matrix');
      return result as TierStrategyMatrixDto;
    },

    /**
     * Get all strategy-model mappings
     * GET /api/v1/admin/tier-strategy/model-mappings
     */
    async getStrategyModelMappings(): Promise<StrategyModelMappingDto[]> {
      const result = await httpClient.get('/api/v1/admin/tier-strategy/model-mappings');
      return (result as StrategyModelMappingDto[]) || [];
    },

    /**
     * Update tier-strategy access rules
     * PUT /api/v1/admin/tier-strategy/access
     */
    async updateTierStrategyAccess(payload: {
      tier: string;
      strategy: string;
      isEnabled: boolean;
    }): Promise<void> {
      await httpClient.put('/api/v1/admin/tier-strategy/access', payload);
    },

    /**
     * Update strategy-model mapping
     * PUT /api/v1/admin/tier-strategy/model-mapping
     */
    async updateStrategyModelMapping(payload: {
      strategy: string;
      provider: string;
      primaryModel: string;
      fallbackModels?: string[];
    }): Promise<void> {
      await httpClient.put('/api/v1/admin/tier-strategy/model-mapping', payload);
    },

    // ========== Model Health (Issue #5503) ==========

    /**
     * Get model health/availability status
     * GET /api/v1/admin/tier-strategy/model-health
     */
    async getModelHealth(): Promise<ModelHealthResult> {
      const result = await httpClient.get('/api/v1/admin/tier-strategy/model-health', undefined, {
        skipCircuitBreaker: true,
      });
      return (result as ModelHealthResult) || { models: [] };
    },

    /**
     * Get model change history (audit trail)
     * GET /api/v1/admin/tier-strategy/model-change-history
     */
    async getModelChangeHistory(
      modelId?: string,
      limit: number = 50
    ): Promise<ModelChangeHistoryResult> {
      const params = new URLSearchParams();
      if (modelId) params.set('modelId', modelId);
      params.set('limit', limit.toString());
      const result = await httpClient.get(
        `/api/v1/admin/tier-strategy/model-change-history?${params.toString()}`,
        undefined,
        { skipCircuitBreaker: true }
      );
      return (result as ModelChangeHistoryResult) || { changes: [] };
    },

    /**
     * Trigger immediate model availability check
     * POST /api/v1/admin/tier-strategy/check-now
     */
    async triggerModelAvailabilityCheck(): Promise<{ triggered: boolean; message: string }> {
      const result = await httpClient.post('/api/v1/admin/tier-strategy/check-now', {});
      return result as { triggered: boolean; message: string };
    },

    // ========== PDF Analytics (Issue #3715) ==========

    /**
     * Get PDF processing analytics
     * GET /api/v1/admin/pdf-analytics?days={days}
     *
     * Issue #3715: Aggregated PDF processing metrics
     */
    async getPdfAnalytics(days: number = 30): Promise<PdfAnalyticsDto | null> {
      return httpClient.get(`/api/v1/admin/pdf-analytics?days=${days}`, PdfAnalyticsDtoSchema);
    },

    /**
     * Get chat analytics
     * GET /api/v1/admin/chat-analytics?days={days}
     *
     * Issue #3714: Aggregated chat thread metrics
     */
    async getChatAnalytics(days: number = 30): Promise<ChatAnalyticsDto | null> {
      return httpClient.get(`/api/v1/admin/chat-analytics?days=${days}`, ChatAnalyticsDtoSchema);
    },

    /**
     * Get model performance analytics
     * GET /api/v1/admin/model-performance?days={days}
     *
     * Issue #3716: Per-model latency, cost, usage metrics
     */
    async getModelPerformance(days: number = 30): Promise<ModelPerformanceDto | null> {
      return httpClient.get(
        `/api/v1/admin/model-performance?days=${days}`,
        ModelPerformanceDtoSchema
      );
    },

    // ========== Admin PDF Management (Issue #4784) ==========

    /**
     * Bulk delete PDF documents
     * POST /api/v1/admin/pdfs/bulk/delete
     */
    async bulkDeletePdfs(ids: string[]): Promise<BulkDeleteResult> {
      return httpClient.post(
        '/api/v1/admin/pdfs/bulk/delete',
        { pdfIds: ids },
        BulkDeleteResultSchema
      );
    },

    /**
     * Reindex a PDF document (delete chunks, reset to Pending)
     * POST /api/v1/admin/pdfs/{pdfId}/reindex
     */
    async reindexPdf(pdfId: string): Promise<ReindexResponse> {
      return httpClient.post(
        `/api/v1/admin/pdfs/${encodeURIComponent(pdfId)}/reindex`,
        {},
        ReindexResponseSchema
      );
    },

    /**
     * Mark documents stuck in processing (>24h) as failed
     * POST /api/v1/admin/pdfs/maintenance/purge-stale
     */
    async purgeStaleDocuments(): Promise<MaintenanceResult> {
      return httpClient.post(
        '/api/v1/admin/pdfs/maintenance/purge-stale',
        {},
        MaintenanceResultSchema
      );
    },

    /**
     * Delete orphaned text chunks referencing non-existent PDFs
     * POST /api/v1/admin/pdfs/maintenance/cleanup-orphans
     */
    async cleanupOrphans(): Promise<MaintenanceResult> {
      return httpClient.post(
        '/api/v1/admin/pdfs/maintenance/cleanup-orphans',
        {},
        MaintenanceResultSchema
      );
    },

    /**
     * Get PDF status distribution for analytics
     * GET /api/v1/admin/pdfs/analytics/distribution
     */
    async getPdfStatusDistribution(): Promise<PdfStatusDistribution> {
      const result = await httpClient.get(
        '/api/v1/admin/pdfs/analytics/distribution',
        PdfStatusDistributionSchema
      );
      return result ?? { countByState: {}, totalDocuments: 0, topBySize: [] };
    },

    /**
     * Get PDF storage health across PG, Qdrant, and file storage
     * GET /api/v1/admin/pdfs/storage/health
     */
    async getPdfStorageHealth(): Promise<PdfStorageHealth> {
      const result = await httpClient.get(
        '/api/v1/admin/pdfs/storage/health',
        PdfStorageHealthSchema
      );
      if (!result) throw new Error('Failed to fetch PDF storage health');
      return result;
    },

    /**
     * Get aggregated PDF processing metrics
     * GET /api/v1/admin/pdfs/metrics/processing
     */
    async getPdfMetrics(): Promise<ProcessingMetrics> {
      const result = await httpClient.get(
        '/api/v1/admin/pdfs/metrics/processing',
        ProcessingMetricsSchema
      );
      if (!result) throw new Error('Failed to fetch PDF processing metrics');
      return result;
    },

    /**
     * Get all PDF documents with filtering and pagination (admin only)
     * GET /api/v1/admin/pdfs
     */
    async getAllPdfs(params?: {
      status?: string;
      state?: string;
      minSizeBytes?: number;
      maxSizeBytes?: number;
      uploadedAfter?: string;
      uploadedBefore?: string;
      gameId?: string;
      sortBy?: string;
      sortOrder?: string;
      page?: number;
      pageSize?: number;
    }): Promise<PdfListResult> {
      const queryParams = new URLSearchParams();
      if (params?.status) queryParams.set('status', params.status);
      if (params?.state) queryParams.set('state', params.state);
      if (params?.minSizeBytes) queryParams.set('minSizeBytes', params.minSizeBytes.toString());
      if (params?.maxSizeBytes) queryParams.set('maxSizeBytes', params.maxSizeBytes.toString());
      if (params?.uploadedAfter) queryParams.set('uploadedAfter', params.uploadedAfter);
      if (params?.uploadedBefore) queryParams.set('uploadedBefore', params.uploadedBefore);
      if (params?.gameId) queryParams.set('gameId', params.gameId);
      if (params?.sortBy) queryParams.set('sortBy', params.sortBy);
      if (params?.sortOrder) queryParams.set('sortOrder', params.sortOrder);
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());

      const query = queryParams.toString();
      const result = await httpClient.get(
        `/api/v1/admin/pdfs${query ? `?${query}` : ''}`,
        PdfListResultSchema
      );
      return result ?? { items: [], total: 0, page: 1, pageSize: 50 };
    },

    // ========== Admin Knowledge Base (Issue #4784) ==========

    /**
     * Get vector collections from Qdrant
     * GET /api/v1/admin/kb/vector-collections
     */
    async getVectorCollections(): Promise<VectorCollectionsResponse> {
      const result = await httpClient.get(
        '/api/v1/admin/kb/vector-collections',
        VectorCollectionsResponseSchema
      );
      return result ?? { collections: [] };
    },

    // ========== Admin Qdrant Operations (Issue #4877) ==========

    /**
     * Get detailed collection info from Qdrant
     * GET /api/v1/admin/qdrant/collections/{name}
     */
    async getQdrantCollectionDetails(name: string): Promise<QdrantCollectionDetails | null> {
      return httpClient.get<QdrantCollectionDetails>(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}`
      );
    },

    /**
     * Delete an entire Qdrant collection
     * DELETE /api/v1/admin/qdrant/collections/{name}?confirmed=true
     */
    async deleteQdrantCollection(name: string): Promise<void> {
      await httpClient.delete(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}?confirmed=true`
      );
    },

    /**
     * Test semantic search on a Qdrant collection
     * POST /api/v1/admin/qdrant/collections/{name}/search
     */
    async searchQdrantCollection(
      name: string,
      query: string,
      limit?: number,
      gameId?: string
    ): Promise<QdrantSearchResult> {
      const result = await httpClient.post<QdrantSearchResult>(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}/search`,
        { query, limit: limit ?? 10, gameId: gameId ?? null }
      );
      return result ?? { query, results: [], total: 0 };
    },

    /**
     * Browse/scroll points in a Qdrant collection
     * GET /api/v1/admin/qdrant/collections/{name}/points?limit=20
     */
    async browseQdrantPoints(name: string, limit?: number): Promise<QdrantBrowseResult> {
      const params = limit ? `?limit=${limit}` : '';
      const result = await httpClient.get<QdrantBrowseResult>(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}/points${params}`
      );
      return result ?? { points: [], count: 0 };
    },

    /**
     * Delete vectors from a Qdrant collection by filter
     * DELETE /api/v1/admin/qdrant/collections/{name}/points?gameId=&pdfId=&confirmed=true
     */
    async deleteQdrantPoints(
      name: string,
      opts: { gameId?: string; pdfId?: string }
    ): Promise<void> {
      const params = new URLSearchParams({ confirmed: 'true' });
      if (opts.gameId) params.set('gameId', opts.gameId);
      if (opts.pdfId) params.set('pdfId', opts.pdfId);
      await httpClient.delete(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}/points?${params.toString()}`
      );
    },

    /**
     * Rebuild/reindex a Qdrant collection
     * POST /api/v1/admin/qdrant/collections/{name}/rebuild?confirmed=true
     */
    async rebuildQdrantIndex(name: string): Promise<{ rebuilding: boolean; collection: string }> {
      const result = await httpClient.post<{ rebuilding: boolean; collection: string }>(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}/rebuild?confirmed=true`,
        {}
      );
      return result ?? { rebuilding: false, collection: name };
    },

    // ========== Admin Embedding Service (Issue #4878) ==========

    /**
     * Get embedding service info and health
     * GET /api/v1/admin/embedding/info
     */
    async getEmbeddingInfo(): Promise<EmbeddingServiceInfo | null> {
      return httpClient.get<EmbeddingServiceInfo>('/api/v1/admin/embedding/info');
    },

    /**
     * Get embedding service throughput metrics
     * GET /api/v1/admin/embedding/metrics
     */
    async getEmbeddingMetrics(): Promise<EmbeddingServiceMetrics | null> {
      return httpClient.get<EmbeddingServiceMetrics>('/api/v1/admin/embedding/metrics');
    },

    /**
     * Get admin processing queue with pagination
     * GET /api/v1/admin/kb/processing-queue
     */
    async getProcessingQueueAdmin(params?: {
      statusFilter?: string;
      searchText?: string;
      fromDate?: string;
      toDate?: string;
      page?: number;
      pageSize?: number;
    }): Promise<ProcessingQueueResponse> {
      const queryParams = new URLSearchParams();
      if (params?.statusFilter) queryParams.set('statusFilter', params.statusFilter);
      if (params?.searchText) queryParams.set('searchText', params.searchText);
      if (params?.fromDate) queryParams.set('fromDate', params.fromDate);
      if (params?.toDate) queryParams.set('toDate', params.toDate);
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());

      const query = queryParams.toString();
      const result = await httpClient.get(
        `/api/v1/admin/kb/processing-queue${query ? `?${query}` : ''}`,
        ProcessingQueueResponseSchema
      );
      return result ?? { jobs: [], total: 0, page: 1, pageSize: 20, totalPages: 0 };
    },

    /**
     * Get aggregated RAG pipeline health
     * GET /api/v1/admin/kb/pipeline/health
     * Issue #4879
     */
    async getPipelineHealth(): Promise<PipelineHealthResponse | null> {
      return httpClient.get<PipelineHealthResponse>(`/api/v1/admin/kb/pipeline/health`);
    },

    /**
     * Get processing step metrics (avg duration, percentiles)
     * GET /api/v1/admin/pdfs/metrics/processing
     * Issue #4880
     */
    async getProcessingMetrics(): Promise<ProcessingMetricsResponse | null> {
      return httpClient.get<ProcessingMetricsResponse>(`/api/v1/admin/pdfs/metrics/processing`);
    },

    /**
     * Get KB settings (read-only from env vars/config)
     * GET /api/v1/admin/kb/settings
     * Issue #4881
     */
    async getKBSettings(): Promise<KBSettingsResponse | null> {
      return httpClient.get<KBSettingsResponse>(`/api/v1/admin/kb/settings`);
    },

    /**
     * Clear KB cache
     * POST /api/v1/admin/kb/cache/clear
     * Issue #4881
     */
    async clearKBCache(): Promise<KBClearCacheResponse> {
      const result = await httpClient.post<KBClearCacheResponse>(
        `/api/v1/admin/kb/cache/clear`,
        {}
      );
      return result ?? { success: false, message: 'No response', clearedAt: null };
    },

    // ========== OpenRouter Usage Dashboard (Issues #5078-#5083) ==========

    /**
     * Get request timeline bucketed by source.
     * GET /api/v1/admin/openrouter/usage/timeline?period=24h|7d|30d
     * Issue #5078: Admin usage page — request timeline chart.
     */
    async getUsageTimeline(period: '24h' | '7d' | '30d'): Promise<UsageTimelineDto | null> {
      return httpClient.get(
        `/api/v1/admin/openrouter/usage/timeline?period=${period}`,
        UsageTimelineDtoSchema
      );
    },

    /**
     * Get aggregated cost breakdown by model, source, and tier.
     * GET /api/v1/admin/openrouter/usage/costs?period=1d|7d|30d
     * Issue #5080: Admin usage page — cost breakdown panel.
     */
    async getUsageCosts(period: '1d' | '7d' | '30d'): Promise<UsageCostsDto | null> {
      return httpClient.get(
        `/api/v1/admin/openrouter/usage/costs?period=${period}`,
        UsageCostsDtoSchema
      );
    },

    /**
     * Get today's free-tier model usage vs daily limits.
     * GET /api/v1/admin/openrouter/free-quota
     * Issue #5082: Admin usage page — free quota indicator.
     */
    async getUsageFreeQuota(): Promise<FreeQuotaDto | null> {
      return httpClient.get('/api/v1/admin/openrouter/free-quota', FreeQuotaDtoSchema);
    },

    /**
     * Get paginated recent LLM requests with optional filters.
     * GET /api/v1/admin/openrouter/usage/requests
     * Issue #5083: Admin usage page — recent requests table.
     */
    async getRecentRequests(params: {
      page?: number;
      pageSize?: number;
      source?: string;
      model?: string;
      successOnly?: boolean;
    }): Promise<RecentLlmRequestsDto | null> {
      const qs = new URLSearchParams();
      if (params.page != null) qs.set('page', String(params.page));
      if (params.pageSize != null) qs.set('pageSize', String(params.pageSize));
      if (params.source) qs.set('source', params.source);
      if (params.model) qs.set('model', params.model);
      if (params.successOnly != null) qs.set('successOnly', String(params.successOnly));
      const query = qs.toString();
      return httpClient.get(
        `/api/v1/admin/openrouter/usage/requests${query ? `?${query}` : ''}`,
        RecentLlmRequestsDtoSchema
      );
    },

    // ========== EntityLink Admin Methods (Issue #5142 — Epic A) ==========

    /**
     * Get entity links (admin — all scopes)
     * GET /api/v1/admin/entity-links?sourceType=...&sourceId=...&linkType=...
     */
    async getAdminEntityLinks(params: GetEntityLinksParams): Promise<EntityLinkDto[]> {
      const qs = new URLSearchParams();
      qs.set('sourceType', params.entityType);
      qs.set('sourceId', params.entityId);
      if (params.linkType) qs.set('linkType', params.linkType);
      const data = await httpClient.get<EntityLinkDto[]>(
        `/api/v1/admin/entity-links?${qs.toString()}`,
        z.array(EntityLinkDtoSchema)
      );
      return data ?? [];
    },

    /**
     * Create entity link (admin — shared scope)
     * POST /api/v1/admin/entity-links
     */
    async createAdminEntityLink(request: CreateEntityLinkRequest): Promise<EntityLinkDto> {
      const result = await httpClient.post<EntityLinkDto>(
        '/api/v1/admin/entity-links',
        request,
        EntityLinkDtoSchema
      );
      if (!result) throw new Error('Failed to create entity link');
      return result;
    },

    /**
     * Delete entity link (admin — any scope)
     * DELETE /api/v1/admin/entity-links/{linkId}
     */
    async deleteAdminEntityLink(linkId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/entity-links/${linkId}`);
    },

    /**
     * Import BGG expansion/reimplements links for a SharedGame (admin)
     * POST /api/v1/admin/entity-links/import-bgg/{sharedGameId}
     */
    async importBggExpansions(sharedGameId: string): Promise<ImportBggExpansionsResponse> {
      const result = await httpClient.post<ImportBggExpansionsResponse>(
        `/api/v1/admin/entity-links/import-bgg/${sharedGameId}`,
        {},
        ImportBggExpansionsResponseSchema
      );
      if (!result) throw new Error('Failed to import BGG expansions');
      return result;
    },

    // ========== Mechanic Extractor (Variant C Workflow) ==========

    /**
     * Get existing mechanic draft for a game+PDF pair.
     * GET /api/v1/admin/mechanic-extractor/draft?sharedGameId=...&pdfDocumentId=...
     */
    async getMechanicDraft(
      sharedGameId: string,
      pdfDocumentId: string
    ): Promise<MechanicExtractorTypes.MechanicDraftDto | null> {
      return httpClient.get(
        `/api/v1/admin/mechanic-extractor/draft?sharedGameId=${sharedGameId}&pdfDocumentId=${pdfDocumentId}`,
        MechanicExtractorSchemas.MechanicDraftDtoSchema
      );
    },

    /**
     * Save (create or update) a mechanic draft — auto-save.
     * POST /api/v1/admin/mechanic-extractor/draft
     */
    async saveMechanicDraft(
      request: MechanicExtractorTypes.SaveMechanicDraftRequest
    ): Promise<MechanicExtractorTypes.MechanicDraftDto> {
      const result = await httpClient.post(
        '/api/v1/admin/mechanic-extractor/draft',
        request,
        MechanicExtractorSchemas.MechanicDraftDtoSchema
      );
      if (!result) throw new Error('Failed to save mechanic draft');
      return result;
    },

    /**
     * Request AI assistance for a draft section.
     * POST /api/v1/admin/mechanic-extractor/ai-assist
     * CRITICAL: Only sends human notes to AI, never PDF content.
     */
    async aiAssistMechanicDraft(
      request: MechanicExtractorTypes.AiAssistRequest
    ): Promise<MechanicExtractorTypes.AiAssistResultDto> {
      const result = await httpClient.post(
        '/api/v1/admin/mechanic-extractor/ai-assist',
        request,
        MechanicExtractorSchemas.AiAssistResultDtoSchema
      );
      if (!result) throw new Error('AI assist failed');
      return result;
    },

    /**
     * Accept an AI-generated draft for a section.
     * POST /api/v1/admin/mechanic-extractor/accept-draft
     */
    async acceptMechanicDraft(
      request: MechanicExtractorTypes.AcceptDraftRequest
    ): Promise<MechanicExtractorTypes.MechanicDraftDto> {
      const result = await httpClient.post(
        '/api/v1/admin/mechanic-extractor/accept-draft',
        request,
        MechanicExtractorSchemas.MechanicDraftDtoSchema
      );
      if (!result) throw new Error('Failed to accept draft');
      return result;
    },

    /**
     * Finalize a mechanic draft into a RulebookAnalysis entry.
     * POST /api/v1/admin/mechanic-extractor/finalize
     */
    async finalizeMechanicAnalysis(
      request: MechanicExtractorTypes.FinalizeRequest
    ): Promise<unknown> {
      const result = await httpClient.post('/api/v1/admin/mechanic-extractor/finalize', request);
      if (!result) throw new Error('Failed to finalize mechanic analysis');
      return result;
    },

    // ========== LLM System Configuration (Issue #5495) ==========

    /**
     * Get current LLM system configuration (DB or appsettings defaults).
     * GET /api/v1/admin/llm/config
     */
    async getLlmSystemConfig(): Promise<LlmSystemConfigDto> {
      const result = await httpClient.get<LlmSystemConfigDto>(
        '/api/v1/admin/llm/config',
        LlmSystemConfigDtoSchema
      );
      if (!result) throw new Error('Failed to fetch LLM system config');
      return result;
    },

    /**
     * Update LLM system configuration (upserts in DB, invalidates cache).
     * PUT /api/v1/admin/llm/config
     */
    async updateLlmSystemConfig(
      request: UpdateLlmSystemConfigRequest
    ): Promise<LlmSystemConfigDto> {
      const result = await httpClient.put<LlmSystemConfigDto>(
        '/api/v1/admin/llm/config',
        request,
        LlmSystemConfigDtoSchema
      );
      if (!result) throw new Error('Failed to update LLM system config');
      return result;
    },

    // ========== A/B Testing (Issue #5497) ==========

    /**
     * Create a new A/B test session with parallel model generation.
     * POST /api/v1/admin/ab-tests
     */
    async createAbTest(request: {
      query: string;
      modelIds: string[];
      knowledgeBaseId?: string;
    }): Promise<AbTestSessionDto> {
      const result = await httpClient.post<AbTestSessionDto>(
        '/api/v1/admin/ab-tests',
        request,
        AbTestSessionDtoSchema
      );
      if (!result) throw new Error('Failed to create A/B test');
      return result;
    },

    /**
     * List A/B test sessions (paginated).
     * GET /api/v1/admin/ab-tests
     */
    async getAbTests(params?: {
      page?: number;
      pageSize?: number;
      status?: string;
    }): Promise<AbTestSessionListDto> {
      const queryParams = new URLSearchParams();
      if (params?.page !== undefined) queryParams.append('page', params.page.toString());
      if (params?.pageSize !== undefined)
        queryParams.append('pageSize', params.pageSize.toString());
      if (params?.status) queryParams.append('status', params.status);

      const qs = queryParams.toString();
      const url = qs ? `/api/v1/admin/ab-tests?${qs}` : '/api/v1/admin/ab-tests';

      const result = await httpClient.get(url, AbTestSessionListDtoSchema);
      return result ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
    },

    /**
     * Get A/B test session in blind mode.
     * GET /api/v1/admin/ab-tests/{id}
     */
    async getAbTest(id: string): Promise<AbTestSessionDto | null> {
      return httpClient.get<AbTestSessionDto>(
        `/api/v1/admin/ab-tests/${id}`,
        AbTestSessionDtoSchema
      );
    },

    /**
     * Submit evaluation scores for A/B test variants.
     * POST /api/v1/admin/ab-tests/{id}/evaluate
     */
    async evaluateAbTest(
      id: string,
      request: {
        evaluations: Array<{
          label: string;
          accuracy: number;
          completeness: number;
          clarity: number;
          tone: number;
          notes?: string;
        }>;
      }
    ): Promise<AbTestSessionRevealedDto> {
      const result = await httpClient.post<AbTestSessionRevealedDto>(
        `/api/v1/admin/ab-tests/${id}/evaluate`,
        request,
        AbTestSessionRevealedDtoSchema
      );
      if (!result) throw new Error('Failed to evaluate A/B test');
      return result;
    },

    /**
     * Reveal model identities (only after evaluation).
     * GET /api/v1/admin/ab-tests/{id}/reveal
     */
    async revealAbTest(id: string): Promise<AbTestSessionRevealedDto | null> {
      return httpClient.get<AbTestSessionRevealedDto>(
        `/api/v1/admin/ab-tests/${id}/reveal`,
        AbTestSessionRevealedDtoSchema
      );
    },

    /**
     * Get aggregated A/B test analytics.
     * GET /api/v1/admin/ab-tests/analytics
     */
    async getAbTestAnalytics(params?: {
      dateFrom?: string;
      dateTo?: string;
    }): Promise<AbTestAnalyticsDto> {
      const queryParams = new URLSearchParams();
      if (params?.dateFrom) queryParams.append('dateFrom', params.dateFrom);
      if (params?.dateTo) queryParams.append('dateTo', params.dateTo);

      const qs = queryParams.toString();
      const url = qs
        ? `/api/v1/admin/ab-tests/analytics?${qs}`
        : '/api/v1/admin/ab-tests/analytics';

      const result = await httpClient.get(url, AbTestAnalyticsDtoSchema);
      return (
        result ?? {
          totalTests: 0,
          completedTests: 0,
          totalCost: 0,
          modelWinRates: [],
          modelAvgScores: [],
        }
      );
    },

    // ========== Email Queue Management (Issue #39) ==========

    /**
     * Get email queue statistics.
     * GET /api/v1/admin/emails/stats
     */
    async getEmailQueueStats(): Promise<EmailQueueStats> {
      const result = await httpClient.get('/api/v1/admin/emails/stats', EmailQueueStatsSchema);
      return (
        result ?? {
          pendingCount: 0,
          processingCount: 0,
          sentCount: 0,
          failedCount: 0,
          deadLetterCount: 0,
          sentLastHour: 0,
          sentLast24Hours: 0,
        }
      );
    },

    /**
     * Get email history with pagination and optional search.
     * GET /api/v1/admin/emails/history
     */
    async getEmailHistory(params?: {
      skip?: number;
      take?: number;
      search?: string;
    }): Promise<EmailHistoryResult> {
      const queryParams = new URLSearchParams();
      if (params?.skip !== undefined) queryParams.append('skip', String(params.skip));
      if (params?.take !== undefined) queryParams.append('take', String(params.take));
      if (params?.search) queryParams.append('search', params.search);
      const qs = queryParams.toString();
      const url = qs ? `/api/v1/admin/emails/history?${qs}` : '/api/v1/admin/emails/history';
      const result = await httpClient.get(url, EmailHistoryResultSchema);
      return result ?? { items: [], totalCount: 0, skip: 0, take: 20 };
    },

    /**
     * Get dead-lettered emails with pagination.
     * GET /api/v1/admin/emails/dead-letter
     */
    async getDeadLetterEmails(params?: {
      skip?: number;
      take?: number;
    }): Promise<DeadLetterResult> {
      const queryParams = new URLSearchParams();
      if (params?.skip !== undefined) queryParams.append('skip', String(params.skip));
      if (params?.take !== undefined) queryParams.append('take', String(params.take));
      const qs = queryParams.toString();
      const url = qs
        ? `/api/v1/admin/emails/dead-letter?${qs}`
        : '/api/v1/admin/emails/dead-letter';
      const result = await httpClient.get(url, DeadLetterResultSchema);
      return result ?? { items: [], totalCount: 0, skip: 0, take: 20 };
    },

    /**
     * Retry a single dead-lettered email.
     * POST /api/v1/admin/emails/{id}/retry
     */
    async retryEmail(id: string): Promise<boolean> {
      const result = await httpClient.post(
        `/api/v1/admin/emails/${encodeURIComponent(id)}/retry`,
        {},
        z.object({ success: z.boolean() })
      );
      return result?.success ?? false;
    },

    /**
     * Retry all dead-lettered emails.
     * POST /api/v1/admin/emails/retry-all-dead-letters
     */
    async retryAllDeadLetters(): Promise<number> {
      const result = await httpClient.post(
        '/api/v1/admin/emails/retry-all-dead-letters',
        {},
        z.object({ retried: z.number() })
      );
      return result?.retried ?? 0;
    },

    /**
     * Send a test email (immediate, not queued).
     * POST /api/v1/admin/emails/test
     */
    async sendTestEmail(to: string): Promise<boolean> {
      const result = await httpClient.post(
        '/api/v1/admin/emails/test',
        { to },
        z.object({ success: z.boolean() })
      );
      return result?.success ?? false;
    },
  };
}

export type AdminClient = ReturnType<typeof createAdminClient>;

// ========== Email Queue Schemas (Issue #39) ==========

const EmailQueueStatsSchema = z.object({
  pendingCount: z.number(),
  processingCount: z.number(),
  sentCount: z.number(),
  failedCount: z.number(),
  deadLetterCount: z.number(),
  sentLastHour: z.number(),
  sentLast24Hours: z.number(),
});

export type EmailQueueStats = z.infer<typeof EmailQueueStatsSchema>;

const EmailQueueItemSchema = z.object({
  id: z.string(),
  userId: z.string(),
  to: z.string(),
  subject: z.string(),
  status: z.string(),
  retryCount: z.number(),
  maxRetries: z.number(),
  errorMessage: z.string().nullable(),
  createdAt: z.string(),
  processedAt: z.string().nullable(),
  failedAt: z.string().nullable(),
  correlationId: z.string().nullable().optional(),
});

export type EmailQueueItem = z.infer<typeof EmailQueueItemSchema>;

const EmailHistoryResultSchema = z.object({
  items: z.array(EmailQueueItemSchema),
  totalCount: z.number(),
  skip: z.number(),
  take: z.number(),
});

export type EmailHistoryResult = z.infer<typeof EmailHistoryResultSchema>;

const DeadLetterResultSchema = z.object({
  items: z.array(EmailQueueItemSchema),
  totalCount: z.number(),
  skip: z.number(),
  take: z.number(),
});

export type DeadLetterResult = z.infer<typeof DeadLetterResultSchema>;

// ========== Qdrant Admin Types (Issue #4877) ==========

export interface QdrantCollectionDetails {
  name: string;
  pointsCount: number;
  indexedVectorsCount: number;
  status: string;
  config: {
    vectorSize?: number;
    distance?: string;
  };
  exactCount: number;
  health: number;
}

export interface QdrantSearchResultItem {
  id: number;
  score: number;
  payload?: Record<string, string>;
}

export interface QdrantSearchResult {
  query: string;
  results: QdrantSearchResultItem[];
  total: number;
}

export interface QdrantBrowsePoint {
  id: number;
  payload?: Record<string, string>;
}

export interface QdrantBrowseResult {
  points: QdrantBrowsePoint[];
  count: number;
}

// ========== Embedding Service Types (Issue #4878) ==========

export interface EmbeddingServiceInfo {
  status: string;
  model: string | null;
  device: string | null;
  supportedLanguages: string[];
  dimension: number;
  maxInputChars: number;
  maxBatchSize: number;
}

export interface EmbeddingServiceMetrics {
  requestsTotal: number;
  failuresTotal: number;
  durationMsSum: number;
  totalCharsSum: number;
  avgDurationMs: number;
  failureRate: number;
}

// Re-export tier-strategy types for convenience
export type {
  TierStrategyMatrixDto,
  StrategyModelMappingDto,
} from '../schemas/tier-strategy.schemas';

// ========== Agent Typology Types (Issue #3179) ==========

export type AgentTypology = {
  id: string;
  name: string;
  description: string;
  systemMessage: string;
  status: 'Draft' | 'PendingReview' | 'Approved' | 'Rejected';
  createdBy: string;
  createdByDisplayName: string;
  createdAt: string;
  updatedAt: string;
};

export type AgentTypologyListResponse = {
  typologies: AgentTypology[];
  total: number;
  page: number;
  pageSize: number;
};

// ========== Additional Types for Issue #2890 ==========

export type UserBadge = {
  id: string;
  code: string;
  name: string;
  description: string;
  iconUrl: string | null;
  tier: string;
  earnedAt: string;
  isDisplayed: boolean;
};

export type UserLibraryStats = {
  totalGames: number;
  sessionsPlayed: number;
  avgSessionDuration: number | null;
};

export type RoleChangeHistory = {
  changedAt: string;
  oldRole: string;
  newRole: string;
  changedBy: string;
  changedByDisplayName: string;
  ipAddress: string | null;
};

export type ImpersonateUserResponse = {
  sessionToken: string;
  impersonatedUserId: string;
  expiresAt: string;
};

export type EndImpersonationResponse = {
  success: boolean;
  message: string;
};

// ========== RAG Execution History Types (Issue #4458) ==========

export type RagExecutionListItem = {
  id: string;
  query: string;
  agentDefinitionId: string | null;
  agentName: string | null;
  strategy: string;
  model: string | null;
  provider: string | null;
  gameId: string | null;
  isPlayground: boolean;
  totalLatencyMs: number;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  totalCost: number;
  confidence: number | null;
  cacheHit: boolean;
  status: string;
  errorMessage: string | null;
  createdAt: string;
};

export type RagExecutionListResult = {
  items: RagExecutionListItem[];
  totalCount: number;
};

export type RagExecutionDetail = RagExecutionListItem & {
  executionTrace: string | null;
};

export type RagExecutionStatsResult = {
  totalExecutions: number;
  avgLatencyMs: number;
  errorRate: number;
  cacheHitRate: number;
  totalCost: number;
  avgConfidence: number;
};

// ========== Pipeline Health Types (Issue #4879) ==========

export type PipelineStageStatus = 'healthy' | 'warning' | 'error';

export type PipelineStage = {
  name: string;
  status: PipelineStageStatus;
  metrics: Record<string, unknown>;
};

export type PipelineRecentActivity = {
  jobId: string;
  fileName: string;
  status: string;
  completedAt: string | null;
  durationMs: number | null;
};

export type PipelineDistribution = {
  totalDocuments: number;
  totalChunks: number;
  vectorCount: number;
  totalFiles: number;
  storageSizeFormatted: string;
};

export type PipelineHealthResponse = {
  stages: PipelineStage[];
  summary: {
    healthyCount: number;
    warningCount: number;
    errorCount: number;
  };
  recentActivity: PipelineRecentActivity[];
  distribution: PipelineDistribution;
  checkedAt: string;
};

// ========== Processing Metrics Types (Issue #4880) ==========

export type ProcessingStepAverages = {
  step: string;
  avgDuration: number;
  sampleSize: number;
};

export type ProcessingStepPercentiles = {
  p50: number;
  p95: number;
  p99: number;
};

export type ProcessingMetricsResponse = {
  averages: Record<string, ProcessingStepAverages>;
  percentiles: Record<string, ProcessingStepPercentiles>;
  lastUpdated: string;
};

// ========== KB Settings Types (Issue #4881) ==========

export type KBSettingsResponse = {
  embedding: {
    provider: string;
    model: string;
    serviceUrl: string;
  };
  vectorDatabase: {
    type: string;
    url: string;
    grpcPort: string;
  };
  chunking: {
    defaultChunkSize: number;
    chunkOverlap: number;
    minChunkSize: number;
    maxChunkSize: number;
    embeddingTokenLimit: number;
    charsPerToken: number;
  };
  cache: {
    redis: {
      host: string;
      port: string;
    };
    hybridCache: {
      defaultExpiration: string;
      l2Enabled: boolean;
    };
    multiTier: {
      enabled: boolean;
      l1Ttl: string;
      l2Ttl: string;
    };
  };
  reranker: {
    configured: boolean;
    url: string | null;
  };
  storage: {
    provider: string;
  };
};

export type KBClearCacheResponse = {
  success: boolean;
  message: string;
  clearedAt: string | null;
};
