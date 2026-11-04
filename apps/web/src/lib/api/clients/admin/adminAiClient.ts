/**
 * Admin AI Sub-Client
 *
 * AI models, LLM config, tier/strategy, agent typologies, A/B testing,
 * RAG/KB/vector operations, PDFs, RAG executions, and caching.
 */

import {
  AiModelDtoSchema,
  PagedAiModelsSchema,
  CostTrackingDtoSchema,
  TestModelResponseSchema,
  LlmSystemConfigDtoSchema,
  LlmBudgetUsageSchema,
  AbTestSessionDtoSchema,
  AbTestSessionListDtoSchema,
  AbTestSessionRevealedDtoSchema,
  AbTestAnalyticsDtoSchema,
  AgentProfileAdminSchema,
  type AgentProfileAdmin,
  type SaveAgentProfileDraftRequest,
  type AiModelDto,
  type PagedAiModels,
  type ConfigureModelRequest,
  type SetPrimaryModelRequest,
  type UpdateModelPricingRequest,
  type CostTrackingDto,
  type TestModelRequest,
  type TestModelResponse,
  type LlmSystemConfigDto,
  type UpdateLlmSystemConfigRequest,
  type LlmBudgetUsage,
  type AbTestSessionDto,
  type AbTestSessionListDto,
  type AbTestSessionRevealedDto,
  type AbTestAnalyticsDto,
} from '../../schemas';
import {
  BulkDeleteResultSchema,
  ReindexResponseSchema,
  MaintenanceResultSchema,
  PdfStatusDistributionSchema,
  PdfStorageHealthSchema,
  ProcessingMetricsSchema,
  VectorStatsSchema,
  VectorSemanticSearchResultSchema,
  ProcessingQueueResponseSchema,
  PdfListResultSchema,
  GameKbStatusesSchema,
  KbSnapshotListSchema,
  KbExportResultSchema,
  KbImportResultSchema,
  EmbeddingServiceInfoSchema,
  EmbeddingServiceMetricsSchema,
  CleanupOrphansResultSchema,
  PipelineHealthResponseSchema,
  type EmbeddingServiceInfo,
  type EmbeddingServiceMetrics,
  type CleanupOrphansResult,
  type PipelineStageStatus,
  type PipelineStage,
  type PipelineRecentActivity,
  type PipelineDistribution,
  type PipelineHealthResponse,
  type BulkDeleteResult,
  type ReindexResponse,
  type MaintenanceResult,
  type PdfStatusDistribution,
  type PdfStorageHealth,
  type ProcessingMetrics,
  type VectorStats,
  type VectorSemanticSearchResult,
  type ProcessingQueueResponse,
  type PdfListResult,
  type GameKbStatuses,
  type KbSnapshotList,
  type KbExportResult,
  type KbImportResult,
} from '../../schemas/admin-knowledge-base.schemas';
import {
  type TierStrategyMatrixDto,
  type StrategyModelMappingDto,
  type ModelHealthResult,
  type ModelChangeHistoryResult,
} from '../../schemas/tier-strategy.schemas';

import type { HttpClient } from '../../core/httpClient';

// ========== Route Constants ==========

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
  processingQueue: '/api/v1/admin/kb/processing-queue',
  vectorStats: '/api/v1/admin/kb/vector-stats',
  vectorSearch: '/api/v1/admin/kb/vector-search',
} as const;

// ========== Embedding Service Types (Issue #4878) ==========

// #4446: both types are the schemas' (admin-knowledge-base.schemas), re-exported here.
export type { EmbeddingServiceInfo, EmbeddingServiceMetrics };

// ========== RAG Execution Types (Issue #4458) ==========

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

// #4511: the schemas' (admin-knowledge-base.schemas), re-exported here.
export type {
  PipelineStageStatus,
  PipelineStage,
  PipelineRecentActivity,
  PipelineDistribution,
  PipelineHealthResponse,
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

// ========== KB Clear Cache Types ==========

export type KBClearCacheResponse = {
  success: boolean;
  message: string;
  clearedAt: string | null;
};

// ========== Bulk Upload Types (Issue #117) ==========

export type BulkUploadItemResult = {
  fileName: string;
  success: boolean;
  documentId: string | null;
  error: string | null;
};

export type BulkUploadPdfsResult = {
  totalRequested: number;
  successCount: number;
  failedCount: number;
  items: BulkUploadItemResult[];
};

export function createAdminAiClient(http: HttpClient) {
  return {
    // ========== AI Models Management (Issue #2521) ==========

    async getAiModels(params?: {
      status?: 'active' | 'inactive' | 'all';
      page?: number;
      pageSize?: number;
    }): Promise<PagedAiModels> {
      const queryParams = new URLSearchParams();
      // #4169: il backend filtra per `isActive`; il parametro `status` che si mandava era ignorato,
      // e chi chiedeva solo i modelli attivi riceveva anche gli inattivi.
      if (params?.status && params.status !== 'all') {
        queryParams.append('isActive', String(params.status === 'active'));
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

      const result = await http.get(url, PagedAiModelsSchema);
      // #4093: un fallimento di validazione LANCIA (vedi il commento in ModelsTab.tsx su #4059);
      // questo fallback copre solo un corpo assente, e ora usa le chiavi del contratto reale.
      return result ?? { models: [], totalCount: 0, page: 1, pageSize: 20 };
    },

    async getAiModelById(modelId: string): Promise<AiModelDto> {
      const result = await http.get(
        `/api/v1/admin/ai-models/${encodeURIComponent(modelId)}`,
        AiModelDtoSchema
      );
      if (!result) {
        throw new Error(`AI model ${modelId} not found`);
      }
      return result;
    },

    async updateModelConfig(modelId: string, request: ConfigureModelRequest): Promise<AiModelDto> {
      const result = await http.put(
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
     * #4169: rende il modello il predefinito del suo provider (gli altri modelli dello stesso provider
     * smettono di esserlo). Prima chiamava `POST /ai-models/set-primary`, una rotta inesistente.
     */
    async setPrimaryModel(request: SetPrimaryModelRequest): Promise<AiModelDto> {
      const result = await http.post(
        `/api/v1/admin/ai-models/${encodeURIComponent(request.modelId)}/set-primary`,
        undefined,
        AiModelDtoSchema
      );
      if (!result) {
        throw new Error('Failed to set primary model');
      }
      return result;
    },

    /** #4169: il prezzo del modello, e nient'altro. */
    async updateModelPricing(
      modelId: string,
      request: UpdateModelPricingRequest
    ): Promise<AiModelDto> {
      const result = await http.patch(
        `/api/v1/admin/ai-models/${encodeURIComponent(modelId)}/pricing`,
        request,
        AiModelDtoSchema
      );
      if (!result) {
        throw new Error('Failed to update the model price');
      }
      return result;
    },

    /** #4169: attiva o disattiva il modello nell'ordine di fallback. */
    async toggleModelActive(modelId: string): Promise<AiModelDto> {
      const result = await http.patch(
        `/api/v1/admin/ai-models/${encodeURIComponent(modelId)}/toggle`,
        undefined,
        AiModelDtoSchema
      );
      if (!result) {
        throw new Error('Failed to toggle the model');
      }
      return result;
    },

    async testModel(modelId: string, request: TestModelRequest): Promise<TestModelResponse> {
      const result = await http.post(
        `/api/v1/admin/ai-models/${encodeURIComponent(modelId)}/test`,
        request,
        TestModelResponseSchema
      );
      if (!result) {
        throw new Error('Failed to test model');
      }
      return result;
    },

    async getCostTracking(): Promise<CostTrackingDto> {
      const result = await http.get('/api/v1/admin/ai-models/cost-tracking', CostTrackingDtoSchema);
      if (!result) {
        throw new Error('Failed to fetch cost tracking data');
      }
      return result;
    },

    // ========== Model Health (Issue #5503) ==========

    async getModelHealth(): Promise<ModelHealthResult> {
      const result = await http.get('/api/v1/admin/tier-strategy/model-health', undefined, {
        skipCircuitBreaker: true,
      });
      return (result as ModelHealthResult) || { models: [] };
    },

    async getModelChangeHistory(
      modelId?: string,
      limit: number = 50
    ): Promise<ModelChangeHistoryResult> {
      const params = new URLSearchParams();
      if (modelId) params.set('modelId', modelId);
      params.set('limit', limit.toString());
      const result = await http.get(
        `/api/v1/admin/tier-strategy/model-change-history?${params.toString()}`,
        undefined,
        { skipCircuitBreaker: true }
      );
      return (result as ModelChangeHistoryResult) || { changes: [] };
    },

    // ========== LLM System Configuration (Issue #5495) ==========

    async getLlmSystemConfig(): Promise<LlmSystemConfigDto> {
      const result = await http.get<LlmSystemConfigDto>(
        '/api/v1/admin/llm/config',
        LlmSystemConfigDtoSchema
      );
      if (!result) throw new Error('Failed to fetch LLM system config');
      return result;
    },

    /** #4169: today's and this month's LLM spend per budget class, as the gateway counts it. */
    async getLlmBudgetUsage(): Promise<LlmBudgetUsage> {
      const result = await http.get<LlmBudgetUsage>(
        '/api/v1/admin/llm/budget/usage',
        LlmBudgetUsageSchema
      );
      if (!result) throw new Error('Failed to fetch LLM budget usage');
      return result;
    },

    async updateLlmSystemConfig(
      request: UpdateLlmSystemConfigRequest
    ): Promise<LlmSystemConfigDto> {
      const result = await http.put<LlmSystemConfigDto>(
        '/api/v1/admin/llm/config',
        request,
        LlmSystemConfigDtoSchema
      );
      if (!result) throw new Error('Failed to update LLM system config');
      return result;
    },

    // ========== Agent profile (#4169, ADR-095 6c) ==========

    async getAgentProfile(): Promise<AgentProfileAdmin> {
      const result = await http.get('/api/v1/admin/agent-profile', AgentProfileAdminSchema);
      if (!result) throw new Error('Failed to fetch the agent profile');
      return result;
    },

    async saveAgentProfileDraft(request: SaveAgentProfileDraftRequest): Promise<AgentProfileAdmin> {
      const result = await http.put(
        '/api/v1/admin/agent-profile/draft',
        request,
        AgentProfileAdminSchema
      );
      if (!result) throw new Error('Failed to save the agent profile draft');
      return result;
    },

    async publishAgentProfileDraft(): Promise<AgentProfileAdmin> {
      const result = await http.post(
        '/api/v1/admin/agent-profile/draft/publish',
        undefined,
        AgentProfileAdminSchema
      );
      if (!result) throw new Error('Failed to publish the agent profile draft');
      return result;
    },

    async rollbackAgentProfile(versionNumber: number): Promise<AgentProfileAdmin> {
      const result = await http.post(
        `/api/v1/admin/agent-profile/versions/${versionNumber}/rollback`,
        undefined,
        AgentProfileAdminSchema
      );
      if (!result) throw new Error('Failed to roll the agent profile back');
      return result;
    },

    // ========== Tier/Strategy ==========

    async getTierStrategyMatrix(): Promise<TierStrategyMatrixDto> {
      const result = await http.get('/api/v1/admin/tier-strategy/matrix');
      return result as TierStrategyMatrixDto;
    },

    async getStrategyModelMappings(): Promise<StrategyModelMappingDto[]> {
      const result = await http.get('/api/v1/admin/tier-strategy/model-mappings');
      return (result as StrategyModelMappingDto[]) || [];
    },

    async updateTierStrategyAccess(payload: {
      tier: string;
      strategy: string;
      isEnabled: boolean;
    }): Promise<void> {
      await http.put('/api/v1/admin/tier-strategy/access', payload);
    },

    async updateStrategyModelMapping(payload: {
      strategy: string;
      provider: string;
      primaryModel: string;
      fallbackModels?: string[];
    }): Promise<void> {
      await http.put('/api/v1/admin/tier-strategy/model-mapping', payload);
    },

    async triggerModelAvailabilityCheck(): Promise<{ triggered: boolean; message: string }> {
      const result = await http.post('/api/v1/admin/tier-strategy/check-now', {});
      return result as { triggered: boolean; message: string };
    },

    // ========== A/B Testing (Issue #5497) ==========

    async createAbTest(request: {
      query: string;
      modelIds: string[];
      knowledgeBaseId?: string;
    }): Promise<AbTestSessionDto> {
      const result = await http.post<AbTestSessionDto>(
        '/api/v1/admin/ab-tests',
        request,
        AbTestSessionDtoSchema
      );
      if (!result) throw new Error('Failed to create A/B test');
      return result;
    },

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

      const result = await http.get(url, AbTestSessionListDtoSchema);
      return result ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
    },

    async getAbTest(id: string): Promise<AbTestSessionDto | null> {
      return http.get<AbTestSessionDto>(`/api/v1/admin/ab-tests/${id}`, AbTestSessionDtoSchema);
    },

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
      const result = await http.post<AbTestSessionRevealedDto>(
        `/api/v1/admin/ab-tests/${id}/evaluate`,
        request,
        AbTestSessionRevealedDtoSchema
      );
      if (!result) throw new Error('Failed to evaluate A/B test');
      return result;
    },

    async revealAbTest(id: string): Promise<AbTestSessionRevealedDto | null> {
      return http.get<AbTestSessionRevealedDto>(
        `/api/v1/admin/ab-tests/${id}/reveal`,
        AbTestSessionRevealedDtoSchema
      );
    },

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

      const result = await http.get(url, AbTestAnalyticsDtoSchema);
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

    // ========== RAG/KB/Vector Operations ==========

    async getProcessingMetrics(): Promise<ProcessingMetricsResponse | null> {
      return http.get<ProcessingMetricsResponse>(`/api/v1/admin/pdfs/metrics/processing`);
    },

    async getPipelineHealth(): Promise<PipelineHealthResponse | null> {
      return http.get(`/api/v1/admin/kb/pipeline/health`, PipelineHealthResponseSchema);
    },

    async getProcessingQueue(params?: {
      status?: string;
      search?: string;
      page?: number;
      pageSize?: number;
    }): Promise<ProcessingQueueResponse> {
      const queryParams = new URLSearchParams();
      if (params?.status) queryParams.set('statusFilter', params.status);
      if (params?.search) queryParams.set('searchText', params.search);
      if (params?.page) queryParams.set('page', params.page.toString());
      if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());

      const query = queryParams.toString();
      return (
        (await http.get(
          `/api/v1/admin/kb/processing-queue${query ? `?${query}` : ''}`,
          ProcessingQueueResponseSchema
        )) ?? { jobs: [], total: 0, page: 1, pageSize: 20, totalPages: 0 }
      );
    },

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
      const result = await http.get(
        `/api/v1/admin/kb/processing-queue${query ? `?${query}` : ''}`,
        ProcessingQueueResponseSchema
      );
      return result ?? { jobs: [], total: 0, page: 1, pageSize: 20, totalPages: 0 };
    },

    async getVectorStats(): Promise<VectorStats> {
      const result = await http.get(ADMIN_KB_ROUTES.vectorStats, VectorStatsSchema);
      if (!result) throw new Error('Failed to fetch vector stats');
      return result;
    },

    // ── KB Games Status (Admin KB Dashboard) ─────────────────────────────────

    async getGameKbStatuses(): Promise<GameKbStatuses> {
      const result = await http.get('/api/v1/admin/kb/games/', GameKbStatusesSchema);
      return result ?? { items: [] };
    },

    // ── RAG Backup Snapshots ─────────────────────────────────────────────────

    async listKbSnapshots(): Promise<KbSnapshotList> {
      const result = await http.get('/api/v1/admin/rag-backup/snapshots', KbSnapshotListSchema);
      return result ?? { snapshots: [], downloadUrl: null, error: null };
    },

    async exportKbSnapshot(gameId?: string, dryRun = false): Promise<KbExportResult> {
      const params = new URLSearchParams({ dryRun: String(dryRun) });
      if (gameId) params.set('gameId', gameId);
      const result = await http.post(
        `/api/v1/admin/rag-backup/export?${params}`,
        {},
        KbExportResultSchema
      );
      if (!result) throw new Error('Failed to export snapshot');
      return result;
    },

    async importKbSnapshot(snapshotPath: string, reEmbed = false): Promise<KbImportResult> {
      const params = new URLSearchParams({ snapshotPath, reEmbed: String(reEmbed) });
      const result = await http.post(
        `/api/v1/admin/rag-backup/import?${params}`,
        {},
        KbImportResultSchema
      );
      if (!result) throw new Error('Failed to import snapshot');
      return result;
    },

    async deleteKbSnapshot(snapshotId: string): Promise<void> {
      await http.delete(`/api/v1/admin/rag-backup/snapshots/${encodeURIComponent(snapshotId)}`);
    },

    async searchVectors(
      query: string,
      limit: number = 10,
      gameId?: string
    ): Promise<VectorSemanticSearchResult> {
      const result = await http.post(
        ADMIN_KB_ROUTES.vectorSearch,
        { query, limit, gameId },
        VectorSemanticSearchResultSchema
      );
      return result ?? { results: [], errorMessage: null };
    },

    /** #4446: the only reader of /admin/embedding/info; every admin page goes through it. */
    async getEmbeddingInfo(): Promise<EmbeddingServiceInfo | null> {
      return http.get('/api/v1/admin/embedding/info', EmbeddingServiceInfoSchema);
    },

    async getEmbeddingMetrics(): Promise<EmbeddingServiceMetrics | null> {
      return http.get('/api/v1/admin/embedding/metrics', EmbeddingServiceMetricsSchema);
    },

    // ========== PDF Management (Issue #4784) ==========

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
      const result = await http.get(
        `/api/v1/admin/pdfs${query ? `?${query}` : ''}`,
        PdfListResultSchema
      );
      return result ?? { items: [], total: 0, page: 1, pageSize: 50 };
    },

    async getPdfMetrics(): Promise<ProcessingMetrics> {
      const result = await http.get(
        '/api/v1/admin/pdfs/metrics/processing',
        ProcessingMetricsSchema
      );
      if (!result) throw new Error('Failed to fetch PDF processing metrics');
      return result;
    },

    async getPdfStatusDistribution(): Promise<PdfStatusDistribution> {
      const result = await http.get(
        '/api/v1/admin/pdfs/analytics/distribution',
        PdfStatusDistributionSchema
      );
      return result ?? { countByState: {}, totalDocuments: 0, topBySize: [] };
    },

    async getPdfStorageHealth(): Promise<PdfStorageHealth> {
      const result = await http.get('/api/v1/admin/pdfs/storage/health', PdfStorageHealthSchema);
      if (!result) throw new Error('Failed to fetch PDF storage health');
      return result;
    },

    async reindexPdf(pdfId: string): Promise<ReindexResponse> {
      return http.post(
        `/api/v1/admin/pdfs/${encodeURIComponent(pdfId)}/reindex`,
        {},
        ReindexResponseSchema
      );
    },

    async bulkUploadPdfs(gameId: string, files: File[]): Promise<BulkUploadPdfsResult> {
      const formData = new FormData();
      files.forEach(file => formData.append('files', file));
      const response = await http.post(
        `/api/v1/admin/shared-games/${gameId}/documents/bulk-upload`,
        formData
      );
      return response as BulkUploadPdfsResult;
    },

    async bulkDeletePdfs(ids: string[]): Promise<BulkDeleteResult> {
      return http.post('/api/v1/admin/pdfs/bulk/delete', { pdfIds: ids }, BulkDeleteResultSchema);
    },

    async purgeStaleDocuments(): Promise<MaintenanceResult> {
      return http.post('/api/v1/admin/pdfs/maintenance/purge-stale', {}, MaintenanceResultSchema);
    },

    async cleanupOrphans(): Promise<CleanupOrphansResult> {
      return http.post(
        '/api/v1/admin/pdfs/maintenance/cleanup-orphans',
        {},
        CleanupOrphansResultSchema
      );
    },

    // ========== RAG Executions (Issue #4458) ==========

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
      if (params?.skip !== undefined) searchParams.set('skip', params.skip.toString());
      if (params?.take !== undefined) searchParams.set('take', params.take.toString());
      if (params?.strategy) searchParams.set('strategy', params.strategy);
      if (params?.status) searchParams.set('status', params.status);
      if (params?.minLatencyMs !== undefined)
        searchParams.set('minLatencyMs', params.minLatencyMs.toString());
      if (params?.maxLatencyMs !== undefined)
        searchParams.set('maxLatencyMs', params.maxLatencyMs.toString());
      if (params?.minConfidence !== undefined)
        searchParams.set('minConfidence', params.minConfidence.toString());
      if (params?.dateFrom) searchParams.set('dateFrom', params.dateFrom);
      if (params?.dateTo) searchParams.set('dateTo', params.dateTo);
      const qs = searchParams.toString();
      const url = `/api/v1/admin/rag-executions${qs ? `?${qs}` : ''}`;
      const result = await http.get<RagExecutionListResult>(url);
      if (!result) {
        return { items: [], totalCount: 0 };
      }
      return result;
    },

    async getRagExecutionById(id: string): Promise<RagExecutionDetail | null> {
      return http.get<RagExecutionDetail>(`/api/v1/admin/rag-executions/${id}`);
    },

    async getRagExecutionStats(params?: {
      dateFrom?: string;
      dateTo?: string;
    }): Promise<RagExecutionStatsResult> {
      const searchParams = new URLSearchParams();
      if (params?.dateFrom) searchParams.set('dateFrom', params.dateFrom);
      if (params?.dateTo) searchParams.set('dateTo', params.dateTo);
      const qs = searchParams.toString();
      const url = `/api/v1/admin/rag-executions/stats${qs ? `?${qs}` : ''}`;
      const result = await http.get<RagExecutionStatsResult>(url);
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

    // ========== RAG Query Testing (Playground) ==========

    async testRagQuery(params: {
      query: string;
      strategy?: string;
      model?: string;
      temperature?: number;
      topK?: number;
      gameScope?: string;
    }): Promise<{
      answer: string;
      strategy: string;
      metrics: { latencyMs: number; tokens: number; cost: number; confidence: number };
      chunks: { id: string; text: string; score: number }[];
    }> {
      const response = await http.post('/api/v1/agents/chat/ask', {
        Question: params.query,
        Strategy: params.strategy,
        TopK: params.topK ?? 5,
        GameId: params.gameScope || undefined,
        Language: 'auto',
      });
      const res = response as Record<string, unknown>;
      const tokenUsage = res.tokenUsage as Record<string, number> | undefined;
      const costBreakdown = res.costBreakdown as Record<string, number> | undefined;
      const retrievedChunks = res.retrievedChunks as
        { id?: string; text?: string; score?: number }[] | undefined;
      return {
        answer: (res.answer as string) ?? '[Nessuna risposta]',
        strategy: (res.strategy as string) ?? params.strategy ?? 'HybridRAG',
        metrics: {
          latencyMs: (res.latencyMs as number) ?? 0,
          tokens: tokenUsage?.totalTokens ?? 0,
          cost: costBreakdown?.totalCost ?? 0,
          confidence: (res.confidence as number) ?? 0,
        },
        chunks: (retrievedChunks ?? []).map((c, i) => ({
          id: c.id ?? String(i),
          text: c.text ?? '',
          score: c.score ?? 0,
        })),
      };
    },

    // ========== Caching ==========

    async clearCache(): Promise<void> {
      await http.post('/api/v1/resources/cache/clear?confirmed=true');
    },

    async clearKBCache(): Promise<KBClearCacheResponse> {
      const result = await http.post<KBClearCacheResponse>(`/api/v1/admin/kb/cache/clear`, {});
      return result ?? { success: false, message: 'No response', clearedAt: null };
    },

    // ========== Agent Metrics (Dashboard) ==========

    async getAgentMetrics(startDate: string, endDate: string) {
      const params = new URLSearchParams({ startDate, endDate });
      return http.get(`/api/v1/admin/agents/metrics?${params}`);
    },

    async getTopAgents(params: {
      limit: number;
      sortBy: string;
      startDate: string;
      endDate: string;
    }) {
      const qs = new URLSearchParams({
        limit: params.limit.toString(),
        sortBy: params.sortBy,
        startDate: params.startDate,
        endDate: params.endDate,
      });
      return http.get(`/api/v1/admin/agents/metrics/top?${qs}`);
    },
  };
}

export type AdminAiClient = ReturnType<typeof createAdminAiClient>;

// Re-export tier-strategy types for convenience
export type {
  TierStrategyMatrixDto,
  StrategyModelMappingDto,
} from '../../schemas/tier-strategy.schemas';
