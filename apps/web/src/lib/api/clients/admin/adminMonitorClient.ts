/**
 * Admin Monitor Sub-Client
 *
 * Service dashboard, metrics, reports, OpenRouter status,
 * Docker container management, and testing dashboard.
 */

import { z } from 'zod';

import { getApiBase } from '../../core/httpClient';
import {
  EnhancedServiceDashboardSchema,
  MetricsTimeSeriesResponseSchema,
  ScheduleReportResponseSchema,
  GetScheduledReportsResponseSchema,
  GetReportExecutionsResponseSchema,
  AccessibilityMetricsSchema,
  PerformanceMetricsSchema,
  E2EMetricsSchema,
  ContainerInfoSchema,
  ContainerLogsSchema,
  type AccessibilityMetrics,
  type PerformanceMetrics,
  type E2EMetrics,
  type GenerateReportRequest,
  type ScheduleReportRequest,
  type UpdateReportScheduleRequest,
  type ScheduledReportDto,
  type ReportExecutionDto,
  type ScheduleReportResponse,
  type ContainerInfo,
  type ContainerLogs,
} from '../../schemas';
import {
  OpenRouterStatusDtoSchema,
  type OpenRouterStatusDto,
} from '../../schemas/admin-knowledge-base.schemas';

import type { HttpClient } from '../../core/httpClient';

export function createAdminMonitorClient(http: HttpClient) {
  return {
    // ========== Enhanced Service Dashboard (Issue #132) ==========

    async getServiceDashboard() {
      return http.get(
        '/api/v1/admin/infrastructure/services/dashboard',
        EnhancedServiceDashboardSchema
      );
    },

    async restartService(
      serviceName: string
    ): Promise<{ message: string; estimatedDowntime: string }> {
      const result = await http.post(
        '/api/v1/admin/operations/restart-service',
        { serviceName },
        z.object({ message: z.string(), estimatedDowntime: z.string() })
      );
      if (!result) {
        throw new Error('Failed to restart service');
      }
      return result;
    },

    // ========== Docker Container Management (Issue #139) ==========

    async getDockerContainers(): Promise<ContainerInfo[]> {
      const res = await http.get('/api/v1/admin/docker/containers');
      return z.array(ContainerInfoSchema).parse(res);
    },

    async getContainerLogs(containerId: string, tail: number = 100): Promise<ContainerLogs> {
      const res = await http.get(
        `/api/v1/admin/docker/containers/${containerId}/logs?tail=${tail}`
      );
      return ContainerLogsSchema.parse(res);
    },

    // ========== Metrics ==========

    async getMetricsTimeSeries(range: '1h' | '6h' | '24h' | '7d' = '1h') {
      return http.get(
        `/api/v1/admin/infrastructure/metrics/timeseries?range=${range}`,
        MetricsTimeSeriesResponseSchema
      );
    },

    async getAccessibilityMetrics(): Promise<AccessibilityMetrics> {
      const result = await http.get(
        '/api/v1/admin/testing/accessibility',
        AccessibilityMetricsSchema
      );
      if (!result) {
        throw new Error('Failed to fetch accessibility metrics');
      }
      return result;
    },

    async getPerformanceMetrics(): Promise<PerformanceMetrics> {
      const result = await http.get('/api/v1/admin/testing/performance', PerformanceMetricsSchema);
      if (!result) {
        throw new Error('Failed to fetch performance metrics');
      }
      return result;
    },

    async getE2EMetrics(): Promise<E2EMetrics> {
      const result = await http.get('/api/v1/admin/testing/e2e', E2EMetricsSchema);
      if (!result) {
        throw new Error('Failed to fetch E2E metrics');
      }
      return result;
    },

    // ========== Report Generation & Scheduling (Issue #920) ==========

    async generateReport(request: GenerateReportRequest): Promise<Blob> {
      const response = await fetch(`${getApiBase()}/api/v1/admin/reports/generate`, {
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

    async scheduleReport(request: ScheduleReportRequest): Promise<ScheduleReportResponse> {
      return http.post('/api/v1/admin/reports/schedule', request, ScheduleReportResponseSchema);
    },

    async updateReportSchedule(
      reportId: string,
      request: UpdateReportScheduleRequest
    ): Promise<void> {
      await http.patch(`/api/v1/admin/reports/${reportId}/schedule`, request);
    },

    async deleteScheduledReport(reportId: string): Promise<void> {
      await http.delete(`/api/v1/admin/reports/${reportId}`);
    },

    async getScheduledReports(): Promise<ScheduledReportDto[]> {
      const result = await http.get(
        '/api/v1/admin/reports/scheduled',
        GetScheduledReportsResponseSchema
      );
      return result ?? [];
    },

    async getReportExecutions(params?: { reportId?: string }): Promise<ReportExecutionDto[]> {
      const queryParams = new URLSearchParams();
      if (params?.reportId) queryParams.set('reportId', params.reportId);

      const query = queryParams.toString();
      const result = await http.get(
        `/api/v1/admin/reports/executions${query ? `?${query}` : ''}`,
        GetReportExecutionsResponseSchema
      );
      return result ?? [];
    },

    // ========== OpenRouter Status ==========

    async getOpenRouterStatus(): Promise<OpenRouterStatusDto | null> {
      const result = await http.get('/api/v1/admin/openrouter/status', OpenRouterStatusDtoSchema);
      return result ?? null;
    },
  };
}

export type AdminMonitorClient = ReturnType<typeof createAdminMonitorClient>;
