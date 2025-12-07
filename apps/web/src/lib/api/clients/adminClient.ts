/**
 * Administration Client (Issue #1679)
 *
 * Modular client for Administration bounded context.
 * Covers: User Management, Prompt Template Management
 */

import { z } from 'zod';
import type { HttpClient } from '../core/httpClient';
import {
  AdminUserSchema,
  AdminUserResponseSchema,
  DeleteUserResponseSchema,
  PromptTemplateSchema,
  PromptResponseSchema,
  DeletePromptResponseSchema,
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
  type CreateUserRequest,
  type UpdateUserRequest,
  type AdminUser,
  type DeleteUserResponse,
  type CreatePromptRequest,
  type UpdatePromptRequest,
  type PromptTemplate,
  type DeletePromptResponse,
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
} from '../schemas';

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
     * GET /api/v1/admin/prompts/{id}/audit
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
        `/api/v1/admin/prompts/${promptId}/audit${query ? `?${query}` : ''}`,
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
     */
    async getAnalytics(params?: {
      startDate?: Date;
      endDate?: Date;
      groupBy?: string;
      roleFilter?: string;
    }): Promise<any> {
      const queryParams = new URLSearchParams();
      if (params?.startDate) queryParams.set('startDate', params.startDate.toISOString());
      if (params?.endDate) queryParams.set('endDate', params.endDate.toISOString());
      if (params?.groupBy) queryParams.set('groupBy', params.groupBy);
      if (params?.roleFilter) queryParams.set('roleFilter', params.roleFilter);

      const query = queryParams.toString();
      return httpClient.get(`/api/v1/admin/analytics${query ? `?${query}` : ''}`);
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
    }): Promise<PagedResult<AdminUser>> {
      const queryParams = new URLSearchParams();
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());
      if (params?.search) queryParams.set('search', params.search);
      if (params?.role && params.role !== 'all') queryParams.set('role', params.role);

      const query = queryParams.toString();
      const result = await httpClient.get<PagedResult<AdminUser>>(
        `/api/v1/admin/users${query ? `?${query}` : ''}`,
        PagedResultSchema(AdminUserSchema)
      );
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
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
  };
}

export type AdminClient = ReturnType<typeof createAdminClient>;
