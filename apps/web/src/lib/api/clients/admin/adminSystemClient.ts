/**
 * Admin System Sub-Client
 *
 * Infrastructure, database, emergency controls,
 * batch jobs, queue management, and cache/vector metrics.
 */

import { z } from 'zod';

import {
  InfrastructureDetailsSchema,
  BatchJobDtoSchema,
  BatchJobListSchema,
  CreateBatchJobResponseSchema,
  PaginatedQueueSchema,
  QueueStatusSchema,
  ActiveOverrideSchema,
  DatabaseMetricsSchema,
  TableSizeSchema,
  CacheMetricsSchema,
  VectorStoreMetricsSchema,
  SystemResourcesResponseSchema,
  type BatchJobDto,
  type BatchJobList,
  type CreateBatchJobRequest,
  type PaginatedQueue,
  type QueueStatus,
  type ActiveOverride,
  type DatabaseMetrics,
  type TableSize,
  type CacheMetrics,
  type VectorStoreMetrics,
  type SystemResourcesResponse,
} from '../../schemas';

import type { HttpClient } from '../../core/httpClient';

export function createAdminSystemClient(http: HttpClient) {
  return {
    // ========== Infrastructure Monitoring (Issue #896) ==========

    async getInfrastructureDetails() {
      return http.get('/api/v1/admin/infrastructure/details', InfrastructureDetailsSchema);
    },

    // ========== Database (Issue #125) ==========

    async getResourceDatabaseMetrics(): Promise<DatabaseMetrics | null> {
      return http.get('/api/v1/resources/database/metrics', DatabaseMetricsSchema);
    },

    async getResourceDatabaseTopTables(limit = 10): Promise<TableSize[]> {
      const result = await http.get(
        `/api/v1/resources/database/tables/top?limit=${limit}`,
        z.array(TableSizeSchema)
      );
      return result ?? [];
    },

    async vacuumDatabase(fullVacuum = false): Promise<void> {
      await http.post(`/api/v1/resources/database/vacuum?confirmed=true&fullVacuum=${fullVacuum}`);
    },

    // ========== Emergency Controls (Issue #125) ==========

    async getActiveEmergencyOverrides(): Promise<ActiveOverride[]> {
      const result = await http.get(
        '/api/v1/admin/llm/emergency/active',
        z.array(ActiveOverrideSchema)
      );
      return result ?? [];
    },

    async activateEmergencyOverride(params: {
      action: string;
      reason: string;
      durationMinutes?: number;
      targetProvider?: string;
    }): Promise<void> {
      await http.post('/api/v1/admin/llm/emergency/activate', params);
    },

    async deactivateEmergencyOverride(action: string): Promise<void> {
      await http.post('/api/v1/admin/llm/emergency/deactivate', { action });
    },

    // ========== Batch Jobs (Issue #3693) ==========

    async createBatchJob(request: CreateBatchJobRequest): Promise<{ id: string }> {
      return http.post(
        '/api/v1/admin/operations/batch-jobs',
        request,
        CreateBatchJobResponseSchema
      );
    },

    async getBatchJob(id: string): Promise<BatchJobDto> {
      const result = await http.get<BatchJobDto>(
        `/api/v1/admin/operations/batch-jobs/${id}`,
        BatchJobDtoSchema
      );
      if (!result) throw new Error(`Batch job ${id} not found`);
      return result;
    },

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
      const result = await http.get<BatchJobList>(
        `/api/v1/admin/operations/batch-jobs${query ? `?${query}` : ''}`,
        BatchJobListSchema
      );
      return result ?? { jobs: [], total: 0, page: 1, pageSize: 20 };
    },

    async deleteBatchJob(id: string): Promise<void> {
      await http.delete(`/api/v1/admin/operations/batch-jobs/${id}`);
    },

    async cancelBatchJob(id: string): Promise<void> {
      await http.put(`/api/v1/admin/operations/batch-jobs/${id}/cancel`, {});
    },

    async retryBatchJob(id: string): Promise<void> {
      await http.put(`/api/v1/admin/operations/batch-jobs/${id}/retry`, {});
    },

    // ========== Queue Management (Issue #125) ==========

    async enqueueJob(params: {
      jobType: string;
      parameters?: Record<string, unknown>;
      priority?: number;
    }): Promise<void> {
      await http.post('/api/v1/admin/queue/enqueue', params);
    },

    async getQueueJobs(params: {
      gameId?: string;
      limit?: number;
      status?: string[];
    }): Promise<PaginatedQueue | null> {
      const qs = new URLSearchParams();
      if (params.gameId) qs.set('gameId', params.gameId);
      if (params.limit) qs.set('pageSize', String(params.limit));
      if (params.status) qs.set('status', params.status.join(','));
      const query = qs.toString();
      return http.get(`/api/v1/admin/queue${query ? `?${query}` : ''}`, PaginatedQueueSchema);
    },

    async getQueueStatus(): Promise<QueueStatus | null> {
      return http.get('/api/v1/admin/queue/status', QueueStatusSchema);
    },

    async reorderQueue(orderedJobIds: string[]): Promise<void> {
      await http.put('/api/v1/admin/queue/reorder', { orderedJobIds });
    },

    async removeJob(jobId: string): Promise<void> {
      await http.delete(`/api/v1/admin/queue/${jobId}`);
    },

    async cancelJob(jobId: string): Promise<void> {
      await http.post(`/api/v1/admin/queue/${jobId}/cancel`);
    },

    async retryJob(jobId: string): Promise<void> {
      await http.post(`/api/v1/admin/queue/${jobId}/retry`);
    },

    // ========== Cache/Vector Metrics (Issue #125) ==========

    async getResourceCacheMetrics(): Promise<CacheMetrics | null> {
      return http.get('/api/v1/resources/cache/metrics', CacheMetricsSchema);
    },

    async getResourceVectorMetrics(): Promise<VectorStoreMetrics | null> {
      return http.get('/api/v1/resources/vectors/metrics', VectorStoreMetricsSchema);
    },

    // System Resources (Issue #3041) — self-contained via System.Diagnostics
    async getSystemResources(): Promise<SystemResourcesResponse | null> {
      return http.get('/api/v1/resources/system', SystemResourcesResponseSchema);
    },

    async rebuildVectors(): Promise<void> {
      await http.post('/api/v1/resources/vectors/rebuild?confirmed=true');
    },
  };
}

export type AdminSystemClient = ReturnType<typeof createAdminSystemClient>;
