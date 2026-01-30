/**
 * Administration Client (Issue #1679)
 *
 * Modular client for Administration bounded context.
 * Covers: User Management, Prompt Template Management
 */

import { z } from 'zod';

import { getApiBase } from '../core/httpClient';
import {
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
} from '../schemas';

import type { HttpClient } from '../core/httpClient';

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
     */
    async getUsers(params?: {
      page?: number;
      pageSize?: number;
      search?: string;
      role?: string;
      status?: string;
    }): Promise<PagedResult<AdminUser>> {
      const queryParams = new URLSearchParams();
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());
      if (params?.search) queryParams.set('search', params.search);
      if (params?.role && params.role !== 'all') queryParams.set('role', params.role);
      if (params?.status && params.status !== 'all') queryParams.set('status', params.status);

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
      const url = queryString ? `/api/v1/admin/ai-models?${queryString}` : '/api/v1/admin/ai-models';

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
    async updateModelConfig(
      modelId: string,
      request: ConfigureModelRequest
    ): Promise<AiModelDto> {
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
      if (params?.createdBy && params.createdBy !== 'All') queryParams.set('createdBy', params.createdBy);
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
  };
}

export type AdminClient = ReturnType<typeof createAdminClient>;

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
