/**
 * Admin Content Sub-Client
 *
 * Prompt templates, audit logs, shared games, entity links, email management,
 * mechanic extractor, and game bulk import.
 */

import { z } from 'zod';

import {
  PromptResponseSchema,
  PromptTemplateSchema,
  ActivateVersionResponseSchema,
  PromptVersionsResponseSchema,
  PromptAuditLogsResponseSchema,
  CreatePromptVersionResponseSchema,
  PagedResultSchema,
  AuditLogListResultSchema,
  PublishGameResponseSchema,
  BulkImportFromJsonResultSchema,
  EmailTemplateDtoSchema,
  GetEmailTemplatesResponseSchema,
  type CreatePromptRequest,
  type UpdatePromptRequest,
  type PromptTemplate,
  type ActivateVersionResponse,
  type PromptVersion,
  type PromptAuditLog,
  type CreatePromptVersionRequest,
  type CreatePromptVersionResponse,
  type PagedResult,
  type AuditLogListResult,
  type ApprovalStatus,
  type PublishGameResponse,
  type BulkImportFromJsonResult,
  type EmailTemplateDto,
  type CreateEmailTemplateInput,
  type UpdateEmailTemplateInput,
} from '../../schemas';
import {
  EntityLinkDtoSchema,
  ImportBggExpansionsResponseSchema,
  type EntityLinkDto,
  type CreateEntityLinkRequest,
  type GetEntityLinksParams,
  type ImportBggExpansionsResponse,
} from '../../schemas/entity-link.schemas';
import * as MechanicExtractorSchemas from '../../schemas/mechanic-extractor.schemas';

import type { HttpClient } from '../../core/httpClient';
import type * as MechanicExtractorTypes from '../../schemas/mechanic-extractor.schemas';

// ========== Shared Game Documents Types (Issue #119) ==========

export type SharedGameDocumentDetail = {
  id: string;
  sharedGameId: string;
  fileName: string;
  documentType: string;
  approvalStatus: string;
  description: string | null;
  tags: string[];
  processingState: string | null;
  fileSizeBytes: number | null;
  uploadedAt: string | null;
};

export type SharedGameDocumentsResult = {
  sharedGameId: string;
  documents: SharedGameDocumentDetail[];
  totalCount: number;
};

// ========== Email Queue Types (Issue #39) ==========

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

export function createAdminContentClient(http: HttpClient) {
  return {
    // ========== Prompt Template Management ==========

    async createPrompt(request: CreatePromptRequest): Promise<PromptTemplate> {
      const response = await http.post('/api/v1/admin/prompts', request, PromptResponseSchema);
      return response.template;
    },

    async updatePrompt(promptId: string, updates: UpdatePromptRequest): Promise<PromptTemplate> {
      const response = await http.put(
        `/api/v1/admin/prompts/${promptId}`,
        updates,
        PromptResponseSchema
      );
      return response.template;
    },

    async deletePrompt(promptId: string): Promise<void> {
      await http.delete(`/api/v1/admin/prompts/${promptId}`);
    },

    async activatePromptVersion(
      promptId: string,
      versionId: string
    ): Promise<ActivateVersionResponse> {
      return http.post(
        `/api/v1/admin/prompts/${promptId}/versions/${versionId}/activate`,
        {},
        ActivateVersionResponseSchema
      );
    },

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
      const result = await http.get<PagedResult<PromptTemplate>>(
        `/api/v1/admin/prompts${query ? `?${query}` : ''}`,
        PagedResultSchema(PromptTemplateSchema)
      );
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
    },

    async getPromptById(promptId: string): Promise<PromptTemplate | null> {
      const response = await http.get(`/api/v1/admin/prompts/${promptId}`, PromptResponseSchema);
      return response?.template ?? null;
    },

    async getPromptVersions(promptId: string): Promise<PromptVersion[]> {
      const response = await http.get(
        `/api/v1/admin/prompts/${promptId}/versions`,
        PromptVersionsResponseSchema
      );
      return response?.versions ?? [];
    },

    async getPromptVersion(promptId: string, versionId: string): Promise<PromptVersion | null> {
      return http.get<PromptVersion>(`/api/v1/admin/prompts/${promptId}/versions/${versionId}`);
    },

    async createPromptVersion(
      promptId: string,
      request: CreatePromptVersionRequest
    ): Promise<CreatePromptVersionResponse> {
      return http.post(
        `/api/v1/admin/prompts/${promptId}/versions`,
        request,
        CreatePromptVersionResponseSchema
      );
    },

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
      const result = await http.get(
        `/api/v1/prompts/${promptId}/audit-log${query ? `?${query}` : ''}`,
        PromptAuditLogsResponseSchema
      );
      return result ?? { logs: [], totalPages: 0 };
    },

    // ========== Audit Log (Issue #3691) ==========

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
      const result = await http.get<AuditLogListResult>(url, AuditLogListResultSchema);
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

    // ========== Shared Games ==========

    async getSharedGameDocuments(gameId: string): Promise<SharedGameDocumentsResult> {
      const response = await http.get(`/api/v1/admin/shared-games/${gameId}/documents/overview`);
      return response as SharedGameDocumentsResult;
    },

    async publishGameToSharedLibrary(gameId: string, status: ApprovalStatus) {
      return http.put<PublishGameResponse>(
        `/api/v1/games/${gameId}/publish`,
        { status },
        PublishGameResponseSchema
      );
    },

    async extractGameMetadata(filePath: string) {
      return http.post<{
        title?: string;
        year?: number;
        minPlayers?: number;
        maxPlayers?: number;
        playingTime?: number;
        minAge?: number;
        description?: string;
        confidenceScore: number;
      }>('/api/v1/admin/games/wizard/extract-metadata', { filePath });
    },

    async setRagPublicAccess(sharedGameId: string, isPublic: boolean): Promise<void> {
      await http.put(`/api/v1/admin/shared-games/${sharedGameId}/rag-access`, {
        isRagPublic: isPublic,
      });
    },

    // ========== EntityLink Admin Methods (Issue #5142) ==========

    async getAdminEntityLinks(params: GetEntityLinksParams): Promise<EntityLinkDto[]> {
      const qs = new URLSearchParams();
      qs.set('sourceType', params.entityType);
      qs.set('sourceId', params.entityId);
      if (params.linkType) qs.set('linkType', params.linkType);
      const data = await http.get<EntityLinkDto[]>(
        `/api/v1/admin/entity-links?${qs.toString()}`,
        z.array(EntityLinkDtoSchema)
      );
      return data ?? [];
    },

    async createAdminEntityLink(request: CreateEntityLinkRequest): Promise<EntityLinkDto> {
      const result = await http.post<EntityLinkDto>(
        '/api/v1/admin/entity-links',
        request,
        EntityLinkDtoSchema
      );
      if (!result) throw new Error('Failed to create entity link');
      return result;
    },

    async deleteAdminEntityLink(linkId: string): Promise<void> {
      await http.delete(`/api/v1/admin/entity-links/${linkId}`);
    },

    async importBggExpansions(sharedGameId: string): Promise<ImportBggExpansionsResponse> {
      const result = await http.post<ImportBggExpansionsResponse>(
        `/api/v1/admin/entity-links/import-bgg/${sharedGameId}`,
        {},
        ImportBggExpansionsResponseSchema
      );
      if (!result) throw new Error('Failed to import BGG expansions');
      return result;
    },

    // ========== Email Queue Management (Issue #39) ==========

    async getEmailQueueStats(): Promise<EmailQueueStats> {
      const result = await http.get('/api/v1/admin/emails/stats', EmailQueueStatsSchema);
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
      const result = await http.get(url, EmailHistoryResultSchema);
      return result ?? { items: [], totalCount: 0, skip: 0, take: 20 };
    },

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
      const result = await http.get(url, DeadLetterResultSchema);
      return result ?? { items: [], totalCount: 0, skip: 0, take: 20 };
    },

    async retryEmail(id: string): Promise<boolean> {
      const result = await http.post(
        `/api/v1/admin/emails/${encodeURIComponent(id)}/retry`,
        {},
        z.object({ success: z.boolean() })
      );
      return result?.success ?? false;
    },

    async retryAllDeadLetters(): Promise<number> {
      const result = await http.post(
        '/api/v1/admin/emails/retry-all-dead-letters',
        {},
        z.object({ retried: z.number() })
      );
      return result?.retried ?? 0;
    },

    async sendTestEmail(to: string): Promise<boolean> {
      const result = await http.post(
        '/api/v1/admin/emails/test',
        { to },
        z.object({ success: z.boolean() })
      );
      return result?.success ?? false;
    },

    // ========== Email Templates (Issue #52-#56) ==========

    async getEmailTemplates(params?: {
      type?: string;
      locale?: string;
    }): Promise<EmailTemplateDto[]> {
      const searchParams = new URLSearchParams();
      if (params?.type) searchParams.set('type', params.type);
      if (params?.locale) searchParams.set('locale', params.locale);
      const query = searchParams.toString();
      const result = await http.get(
        `/api/v1/admin/email-templates${query ? `?${query}` : ''}`,
        GetEmailTemplatesResponseSchema
      );
      return result ?? [];
    },

    async getEmailTemplate(id: string): Promise<EmailTemplateDto> {
      const result = await http.get(`/api/v1/admin/email-templates/${id}`, EmailTemplateDtoSchema);
      if (!result) throw new Error(`Email template ${id} not found`);
      return result;
    },

    async getEmailTemplateVersions(name: string, locale?: string): Promise<EmailTemplateDto[]> {
      const params = locale ? `?locale=${locale}` : '';
      const result = await http.get(
        `/api/v1/admin/email-templates/${name}/versions${params}`,
        GetEmailTemplatesResponseSchema
      );
      return result ?? [];
    },

    async createEmailTemplate(data: CreateEmailTemplateInput): Promise<{ id: string }> {
      return http.post<{ id: string }>(
        '/api/v1/admin/email-templates',
        data,
        z.object({ id: z.string().uuid() })
      );
    },

    async updateEmailTemplate(
      id: string,
      data: UpdateEmailTemplateInput
    ): Promise<{ success: boolean }> {
      return http.put<{ success: boolean }>(
        `/api/v1/admin/email-templates/${id}`,
        data,
        z.object({ success: z.boolean() })
      );
    },

    async publishEmailTemplate(id: string): Promise<void> {
      await http.post('/api/v1/admin/email-templates/' + id + '/publish', {});
    },

    // ========== Mechanic Extractor ==========

    async getMechanicDraft(
      sharedGameId: string,
      pdfDocumentId: string
    ): Promise<MechanicExtractorTypes.MechanicDraftDto | null> {
      return http.get(
        `/api/v1/admin/mechanic-extractor/draft?sharedGameId=${sharedGameId}&pdfDocumentId=${pdfDocumentId}`,
        MechanicExtractorSchemas.MechanicDraftDtoSchema
      );
    },

    async saveMechanicDraft(
      request: MechanicExtractorTypes.SaveMechanicDraftRequest
    ): Promise<MechanicExtractorTypes.MechanicDraftDto> {
      const result = await http.post(
        '/api/v1/admin/mechanic-extractor/draft',
        request,
        MechanicExtractorSchemas.MechanicDraftDtoSchema
      );
      if (!result) throw new Error('Failed to save mechanic draft');
      return result;
    },

    async aiAssistMechanicDraft(
      request: MechanicExtractorTypes.AiAssistRequest
    ): Promise<MechanicExtractorTypes.AiAssistResultDto> {
      const result = await http.post(
        '/api/v1/admin/mechanic-extractor/ai-assist',
        request,
        MechanicExtractorSchemas.AiAssistResultDtoSchema
      );
      if (!result) throw new Error('AI assist failed');
      return result;
    },

    async acceptMechanicDraft(
      request: MechanicExtractorTypes.AcceptDraftRequest
    ): Promise<MechanicExtractorTypes.MechanicDraftDto> {
      const result = await http.post(
        '/api/v1/admin/mechanic-extractor/accept-draft',
        request,
        MechanicExtractorSchemas.MechanicDraftDtoSchema
      );
      if (!result) throw new Error('Failed to accept draft');
      return result;
    },

    async finalizeMechanicAnalysis(
      request: MechanicExtractorTypes.FinalizeRequest
    ): Promise<unknown> {
      const result = await http.post('/api/v1/admin/mechanic-extractor/finalize', request);
      if (!result) throw new Error('Failed to finalize mechanic analysis');
      return result;
    },

    async getKBSettings(): Promise<KBSettingsResponse | null> {
      return http.get<KBSettingsResponse>(`/api/v1/admin/kb/settings`);
    },

    async bulkImportGames(jsonContent: string): Promise<BulkImportFromJsonResult> {
      return http.post(
        '/api/v1/admin/games/bulk-import',
        { jsonContent },
        BulkImportFromJsonResultSchema
      );
    },
  };
}

export type AdminContentClient = ReturnType<typeof createAdminContentClient>;

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
