/**
 * Admin Analytics Sub-Client
 *
 * Stats, usage, tokens, cost calculator, resource forecasting,
 * financial ledger, and data export.
 */

import { z } from 'zod';

import { getApiBase } from '../../core/httpClient';
import {
  AdminStatsSchema,
  AdminOverviewStatsSchema,
  AiRequestsResponseSchema,
  DashboardStatsSchema,
  GetUserActivityResultSchema,
  TokenBalanceSchema,
  TokenConsumptionDataSchema,
  TierUsageListSchema,
  TopConsumersListSchema,
  AppUsageStatsSchema,
  RecentActivityDtoSchema,
  PdfAnalyticsDtoSchema,
  ChatAnalyticsDtoSchema,
  ModelPerformanceDtoSchema,
  type AdminStats,
  type AdminOverviewStats,
  type AiRequest,
  type DashboardStats,
  type GetUserActivityResult,
  type RecentActivityDto,
  type TokenBalance,
  type TokenConsumptionData,
  type TierUsageList,
  type TopConsumersList,
  type AddCreditsRequest,
  type UpdateTierLimitsRequest,
  type AppUsageStats,
  type PdfAnalyticsDto,
  type ChatAnalyticsDto,
  type ModelPerformanceDto,
  type ExportUsageReportParams,
} from '../../schemas';
import {
  UsageTimelineDtoSchema,
  UsageCostsDtoSchema,
  FreeQuotaDtoSchema,
  RecentLlmRequestsDtoSchema,
  type UsageTimelineDto,
  type UsageCostsDto,
  type FreeQuotaDto,
  type RecentLlmRequestsDto,
} from '../../schemas/admin-knowledge-base.schemas';
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
} from '../../schemas/cost-calculator.schemas';
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
} from '../../schemas/financial-ledger.schemas';
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
} from '../../schemas/resource-forecast.schemas';

import type { HttpClient } from '../../core/httpClient';

// ========== MAU Monitoring Types (Issue #113) ==========

export type DailyActiveUsers = {
  date: string;
  activeUsers: number;
  aiChatUsers: number;
  pdfUploadUsers: number;
};

export type ActiveAiUsersResult = {
  totalActiveUsers: number;
  aiChatUsers: number;
  pdfUploadUsers: number;
  agentUsers: number;
  periodDays: number;
  periodStart: string;
  periodEnd: string;
  dailyBreakdown: DailyActiveUsers[];
};

export function createAdminAnalyticsClient(http: HttpClient) {
  return {
    // ========== Stats & Analytics ==========

    async getStats(): Promise<AdminStats> {
      const result = await http.get<AdminStats>('/api/v1/admin/stats', AdminStatsSchema);
      if (!result) {
        throw new Error('Failed to fetch admin stats');
      }
      return result;
    },

    async getOverviewStats(): Promise<AdminOverviewStats> {
      const result = await http.get<AdminOverviewStats>(
        '/api/v1/admin/overview-stats',
        AdminOverviewStatsSchema
      );
      if (!result) {
        throw new Error('Failed to fetch admin overview stats');
      }
      return result;
    },

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
      return http.get(`/api/v1/admin/analytics${query ? `?${query}` : ''}`, DashboardStatsSchema);
    },

    async getRecentActivity(params?: { limit?: number; since?: Date }): Promise<RecentActivityDto> {
      const queryParams = new URLSearchParams();
      if (params?.limit) queryParams.set('limit', params.limit.toString());
      if (params?.since) queryParams.set('since', params.since.toISOString());

      const query = queryParams.toString();
      const result = await http.get(
        `/api/v1/admin/activity${query ? `?${query}` : ''}`,
        RecentActivityDtoSchema
      );

      if (!result) {
        throw new Error('Failed to fetch recent activity');
      }

      return result;
    },

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
      const result = await http.get(
        `/api/v1/admin/requests${query ? `?${query}` : ''}`,
        AiRequestsResponseSchema
      );
      return result ?? { requests: [], totalCount: 0 };
    },

    async getChatAnalytics(days: number = 30): Promise<ChatAnalyticsDto | null> {
      return http.get(`/api/v1/admin/chat-analytics?days=${days}`, ChatAnalyticsDtoSchema);
    },

    async getPdfAnalytics(days: number = 30): Promise<PdfAnalyticsDto | null> {
      return http.get(`/api/v1/admin/pdf-analytics?days=${days}`, PdfAnalyticsDtoSchema);
    },

    async getModelPerformance(days: number = 30): Promise<ModelPerformanceDto | null> {
      return http.get(`/api/v1/admin/model-performance?days=${days}`, ModelPerformanceDtoSchema);
    },

    async getActiveAiUsers(period: number = 30): Promise<ActiveAiUsersResult> {
      const response = await http.get(`/api/v1/admin/monitoring/mau?period=${period}`);
      return response as ActiveAiUsersResult;
    },

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
      const result = await http.get(
        `/api/v1/admin/activity${query ? `?${query}` : ''}`,
        GetUserActivityResultSchema
      );

      return result || { activities: [], totalCount: 0 };
    },

    // ========== OpenRouter Usage (Issues #5078-#5083) ==========

    async getUsageStats(params?: { period?: '7d' | '30d' | '90d' }): Promise<AppUsageStats | null> {
      const queryParams = new URLSearchParams();
      if (params?.period) queryParams.set('period', params.period);

      const query = queryParams.toString();
      return http.get(`/api/v1/admin/usage-stats${query ? `?${query}` : ''}`, AppUsageStatsSchema);
    },

    async getUsageTimeline(period: '24h' | '7d' | '30d'): Promise<UsageTimelineDto | null> {
      return http.get(
        `/api/v1/admin/openrouter/usage/timeline?period=${period}`,
        UsageTimelineDtoSchema
      );
    },

    async getUsageCosts(period: '1d' | '7d' | '30d'): Promise<UsageCostsDto | null> {
      return http.get(`/api/v1/admin/openrouter/usage/costs?period=${period}`, UsageCostsDtoSchema);
    },

    async getUsageFreeQuota(): Promise<FreeQuotaDto | null> {
      return http.get('/api/v1/admin/openrouter/free-quota', FreeQuotaDtoSchema);
    },

    async getRecentRequests(params: {
      page?: number;
      pageSize?: number;
      source?: string;
      model?: string;
      successOnly?: boolean;
    }): Promise<RecentLlmRequestsDto | null> {
      const qs = new URLSearchParams();
      if (params.page !== null && params.page !== undefined) qs.set('page', String(params.page));
      if (params.pageSize !== null && params.pageSize !== undefined)
        qs.set('pageSize', String(params.pageSize));
      if (params.source) qs.set('source', params.source);
      if (params.model) qs.set('model', params.model);
      if (params.successOnly !== null && params.successOnly !== undefined)
        qs.set('successOnly', String(params.successOnly));
      const query = qs.toString();
      return http.get(
        `/api/v1/admin/openrouter/usage/requests${query ? `?${query}` : ''}`,
        RecentLlmRequestsDtoSchema
      );
    },

    async getTopConsumers(limit: number = 10): Promise<TopConsumersList> {
      const result = await http.get<TopConsumersList>(
        `/api/v1/admin/resources/tokens/top-consumers?limit=${limit}`,
        TopConsumersListSchema
      );
      if (!result) throw new Error('No consumer data returned');
      return result;
    },

    // ========== Token Management (Issue #3692) ==========

    async getTokenBalance(): Promise<TokenBalance> {
      const result = await http.get<TokenBalance>(
        '/api/v1/admin/resources/tokens',
        TokenBalanceSchema
      );
      if (!result) throw new Error('No token balance data returned');
      return result;
    },

    async getTokenConsumption(days: number = 30): Promise<TokenConsumptionData> {
      const result = await http.get<TokenConsumptionData>(
        `/api/v1/admin/resources/tokens/consumption?days=${days}`,
        TokenConsumptionDataSchema
      );
      if (!result) throw new Error('No consumption data returned');
      return result;
    },

    async getTokenTierUsage(): Promise<TierUsageList> {
      const result = await http.get<TierUsageList>(
        '/api/v1/admin/resources/tokens/tiers',
        TierUsageListSchema
      );
      if (!result) throw new Error('No tier usage data returned');
      return result;
    },

    async addTokenCredits(request: AddCreditsRequest): Promise<void> {
      await http.post('/api/v1/admin/resources/tokens/add-credits', request, z.any());
    },

    async updateTierLimits(request: UpdateTierLimitsRequest): Promise<void> {
      await http.put(`/api/v1/admin/resources/tokens/tiers/${request.tier}`, request, z.any());
    },

    // ========== Cost Calculator (Issue #3725) ==========

    async estimateAgentCost(request: EstimateAgentCostRequest): Promise<AgentCostEstimationResult> {
      return http.post(
        '/api/v1/admin/cost-calculator/estimate',
        request,
        AgentCostEstimationResultSchema
      );
    },

    async getCostScenarios(params?: GetCostScenariosParams): Promise<CostScenariosResponse> {
      const searchParams = new URLSearchParams();
      if (params?.page) searchParams.set('page', params.page.toString());
      if (params?.pageSize) searchParams.set('pageSize', params.pageSize.toString());
      const qs = searchParams.toString();
      const url = `/api/v1/admin/cost-calculator/scenarios${qs ? `?${qs}` : ''}`;
      const result = await http.get(url, CostScenariosResponseSchema);
      return result || { items: [], total: 0, page: 1, pageSize: 20 };
    },

    async saveCostScenario(request: SaveCostScenarioRequest): Promise<SaveScenarioResponse> {
      return http.post(
        '/api/v1/admin/cost-calculator/scenarios',
        request,
        SaveScenarioResponseSchema
      );
    },

    async deleteCostScenario(id: string): Promise<void> {
      await http.delete(`/api/v1/admin/cost-calculator/scenarios/${id}`);
    },

    // ========== Resource Forecasting (Issue #3726) ==========

    async estimateResourceForecast(
      request: EstimateResourceForecastRequest
    ): Promise<ResourceForecastEstimationResult> {
      return http.post(
        '/api/v1/admin/resource-forecast/estimate',
        request,
        ResourceForecastEstimationResultSchema
      );
    },

    async getResourceForecasts(
      params?: GetResourceForecastsParams
    ): Promise<ResourceForecastsResponse> {
      const searchParams = new URLSearchParams();
      if (params?.page) searchParams.set('page', params.page.toString());
      if (params?.pageSize) searchParams.set('pageSize', params.pageSize.toString());
      const qs = searchParams.toString();
      const url = `/api/v1/admin/resource-forecast/scenarios${qs ? `?${qs}` : ''}`;
      const result = await http.get(url, ResourceForecastsResponseSchema);
      return result || { items: [], total: 0, page: 1, pageSize: 20 };
    },

    async saveResourceForecast(
      request: SaveResourceForecastRequest
    ): Promise<SaveForecastResponse> {
      return http.post(
        '/api/v1/admin/resource-forecast/scenarios',
        request,
        SaveForecastResponseSchema
      );
    },

    async deleteResourceForecast(id: string): Promise<void> {
      await http.delete(`/api/v1/admin/resource-forecast/scenarios/${id}`);
    },

    // ========== Financial Ledger (Issue #3722) ==========

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
      const result = await http.get(url, LedgerEntriesResponseSchema);
      return result || { entries: [], total: 0, page: 1, pageSize: 20 };
    },

    async getLedgerEntryById(id: string): Promise<LedgerEntryDto> {
      const result = await http.get(`/api/v1/admin/financial-ledger/${id}`, LedgerEntryDtoSchema);
      if (!result) throw new Error('Ledger entry not found');
      return result;
    },

    async createLedgerEntry(request: CreateLedgerEntryRequest): Promise<CreateLedgerEntryResponse> {
      return http.post('/api/v1/admin/financial-ledger', request, CreateLedgerEntryResponseSchema);
    },

    async updateLedgerEntry(id: string, request: UpdateLedgerEntryRequest): Promise<void> {
      await http.put(`/api/v1/admin/financial-ledger/${id}`, request);
    },

    async deleteLedgerEntry(id: string): Promise<void> {
      await http.delete(`/api/v1/admin/financial-ledger/${id}`);
    },

    async getLedgerSummary(params: GetLedgerSummaryParams): Promise<LedgerSummary> {
      const qs = new URLSearchParams({
        dateFrom: params.dateFrom,
        dateTo: params.dateTo,
      }).toString();
      const result = await http.get(
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

    async getLedgerDashboard(): Promise<LedgerDashboardData> {
      const result = await http.get(
        '/api/v1/admin/financial-ledger/dashboard',
        LedgerDashboardDataSchema
      );
      if (!result) throw new Error('Failed to load ledger dashboard data');
      return result;
    },

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

    // ========== Data Export ==========

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
  };
}

export type AdminAnalyticsClient = ReturnType<typeof createAdminAnalyticsClient>;
