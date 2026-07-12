/**
 * Admin Mechanic Extractor — Metrics Dashboard Client (#532 ME-M2.3)
 *
 * Wraps the `/api/v1/admin/mechanic-analyses/metrics/*` endpoints (KPIs, daily cost, recent table,
 * CSV export). Composed into the flat `adminClient` object.
 */
import { getApiBase } from '../../core/httpClient';
import {
  MechanicMetricsSummarySchema,
  MechanicCostByDayArraySchema,
  MechanicRecentAnalysesResultSchema,
  type MechanicMetricsSummary,
  type MechanicCostByDay,
  type MechanicRecentAnalysesResult,
} from '../../schemas/admin-mechanic-metrics.schemas';

import type { HttpClient } from '../../core/httpClient';

const BASE = '/api/v1/admin/mechanic-analyses/metrics';

export interface MechanicMetricsFilters {
  gameId?: string;
  reviewerId?: string;
  status?: number;
  startDate?: string;
  endDate?: string;
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const sp = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') {
      sp.set(key, String(value));
    }
  }
  const qs = sp.toString();
  return qs ? `?${qs}` : '';
}

export function createAdminMechanicMetricsClient(http: HttpClient) {
  return {
    async getMechanicMetricsSummary(
      filters: MechanicMetricsFilters = {}
    ): Promise<MechanicMetricsSummary | null> {
      const qs = buildQuery({
        gameId: filters.gameId,
        reviewerId: filters.reviewerId,
        startDate: filters.startDate,
        endDate: filters.endDate,
      });
      return http.get(`${BASE}/summary${qs}`, MechanicMetricsSummarySchema);
    },

    async getMechanicCostByDay(
      days: number,
      filters: Pick<MechanicMetricsFilters, 'gameId' | 'reviewerId'> = {}
    ): Promise<MechanicCostByDay[] | null> {
      const qs = buildQuery({ days, gameId: filters.gameId, reviewerId: filters.reviewerId });
      return http.get(`${BASE}/cost-by-day${qs}`, MechanicCostByDayArraySchema);
    },

    async getMechanicRecentAnalyses(
      params: { limit?: number; offset?: number } & MechanicMetricsFilters = {}
    ): Promise<MechanicRecentAnalysesResult | null> {
      const qs = buildQuery({
        limit: params.limit,
        offset: params.offset,
        gameId: params.gameId,
        reviewerId: params.reviewerId,
        status: params.status,
      });
      return http.get(`${BASE}/recent${qs}`, MechanicRecentAnalysesResultSchema);
    },

    async exportMechanicAnalysesCsv(filters: MechanicMetricsFilters = {}): Promise<Blob> {
      const qs = buildQuery({
        gameId: filters.gameId,
        reviewerId: filters.reviewerId,
        status: filters.status,
        startDate: filters.startDate,
        endDate: filters.endDate,
      });
      const response = await fetch(`${getApiBase()}${BASE}/export${qs}`, {
        credentials: 'include',
      });
      if (!response.ok) {
        throw new Error(`CSV export failed: ${response.status} ${response.statusText}`);
      }
      return response.blob();
    },
  };
}

export type AdminMechanicMetricsClient = ReturnType<typeof createAdminMechanicMetricsClient>;
