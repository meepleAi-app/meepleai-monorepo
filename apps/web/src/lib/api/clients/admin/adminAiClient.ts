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
  AbTestSessionDtoSchema,
  AbTestSessionListDtoSchema,
  AbTestSessionRevealedDtoSchema,
  AbTestAnalyticsDtoSchema,
  type AiModelDto,
  type PagedAiModels,
  type ConfigureModelRequest,
  type SetPrimaryModelRequest,
  type CostTrackingDto,
  type TestModelRequest,
  type TestModelResponse,
  type LlmSystemConfigDto,
  type UpdateLlmSystemConfigRequest,
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
  vectorCollections: '/api/v1/admin/kb/vector-collections',
  processingQueue: '/api/v1/admin/kb/processing-queue',
} as const;

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

      const result = await http.get(url, PagedAiModelsSchema);
      return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
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

    async setPrimaryModel(request: SetPrimaryModelRequest): Promise<AiModelDto> {
      const result = await http.post(
        '/api/v1/admin/ai-models/set-primary',
        request,
        AiModelDtoSchema
      );
      if (!result) {
        throw new Error('Failed to set primary model');
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

    // ========== Agent Typologies Management (Issue #3179) ==========

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
      const result = await http.get<AgentTypologyListResponse>(url);
      return result || { typologies: [], total: 0, page: 1, pageSize: 20 };
    },

    async getAgentTypologyById(id: string) {
      return http.get<AgentTypology>(`/admin/agent-typologies/${id}`);
    },

    async deleteAgentTypology(id: string) {
      await http.delete(`/admin/agent-typologies/${id}`);
    },

    async approveAgentTypology(id: string) {
      return http.post<AgentTypology>(`/admin/agent-typologies/${id}/approve`, {});
    },

    async rejectAgentTypology(id: string, reason: string) {
      return http.post<AgentTypology>(`/admin/agent-typologies/${id}/reject`, {
        reason,
      });
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
      return http.get<PipelineHealthResponse>(`/api/v1/admin/kb/pipeline/health`);
    },

    async getProcessingQueue(params?: {
      status?: string;
      search?: string;
      page?: number;
      pageSize?: number;
    }): Promise<ProcessingQueueResponse> {
      const queryParams = new URLSearchParams();
      if (params?.status) queryParams.set('status', params.status);
      if (params?.search) queryParams.set('search', params.search);
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

    async getVectorCollections(): Promise<VectorCollectionsResponse> {
      const result = await http.get(
        '/api/v1/admin/kb/vector-collections',
        VectorCollectionsResponseSchema
      );
      return result ?? { collections: [] };
    },

    async getQdrantCollectionDetails(name: string): Promise<QdrantCollectionDetails | null> {
      return http.get<QdrantCollectionDetails>(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}`
      );
    },

    async deleteQdrantCollection(name: string): Promise<void> {
      await http.delete(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}?confirmed=true`
      );
    },

    async searchQdrantCollection(
      name: string,
      query: string,
      limit?: number,
      gameId?: string
    ): Promise<QdrantSearchResult> {
      const result = await http.post<QdrantSearchResult>(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}/search`,
        { query, limit: limit ?? 10, gameId: gameId ?? null }
      );
      return result ?? { query, results: [], total: 0 };
    },

    async browseQdrantPoints(name: string, limit?: number): Promise<QdrantBrowseResult> {
      const params = limit ? `?limit=${limit}` : '';
      const result = await http.get<QdrantBrowseResult>(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}/points${params}`
      );
      return result ?? { points: [], count: 0 };
    },

    async deleteQdrantPoints(
      name: string,
      opts: { gameId?: string; pdfId?: string }
    ): Promise<void> {
      const params = new URLSearchParams({ confirmed: 'true' });
      if (opts.gameId) params.set('gameId', opts.gameId);
      if (opts.pdfId) params.set('pdfId', opts.pdfId);
      await http.delete(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}/points?${params.toString()}`
      );
    },

    async rebuildQdrantIndex(name: string): Promise<{ rebuilding: boolean; collection: string }> {
      const result = await http.post<{ rebuilding: boolean; collection: string }>(
        `/api/v1/admin/qdrant/collections/${encodeURIComponent(name)}/rebuild?confirmed=true`,
        {}
      );
      return result ?? { rebuilding: false, collection: name };
    },

    async getEmbeddingInfo(): Promise<EmbeddingServiceInfo | null> {
      return http.get<EmbeddingServiceInfo>('/api/v1/admin/embedding/info');
    },

    async getEmbeddingMetrics(): Promise<EmbeddingServiceMetrics | null> {
      return http.get<EmbeddingServiceMetrics>('/api/v1/admin/embedding/metrics');
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

    async cleanupOrphans(): Promise<MaintenanceResult> {
      return http.post(
        '/api/v1/admin/pdfs/maintenance/cleanup-orphans',
        {},
        MaintenanceResultSchema
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
