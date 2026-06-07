import { HttpClient } from './core/httpClient';

import type {
  CostBreakdownByFeature,
  CostBreakdownByProvider,
  CostBreakdownRange,
} from './schemas/business-cost.schemas';

const api = new HttpClient();

/**
 * Issue #1838 SP5 F4-C5 — Cost breakdown queries used by the Business page
 * (CostStackedArea + FeatureCostTable).
 *
 * Backend: `apps/api/src/Api/Routing/AdminBusinessStatsEndpoints.cs` extended
 * with `/breakdown` and `/per-feature`. Both endpoints are cached server-side
 * (HybridCache 5 min) keyed by range — the FE may safely re-query on tab focus.
 */
export const businessCostApi = {
  getBreakdownByProvider: async (range: CostBreakdownRange): Promise<CostBreakdownByProvider> => {
    const result = await api.get<CostBreakdownByProvider>(
      `/api/v1/admin/business/breakdown?range=${range}`
    );
    if (!result) throw new Error('Cost breakdown returned empty payload');
    return result;
  },

  getBreakdownByFeature: async (range: CostBreakdownRange): Promise<CostBreakdownByFeature> => {
    const result = await api.get<CostBreakdownByFeature>(
      `/api/v1/admin/business/per-feature?range=${range}`
    );
    if (!result) throw new Error('Feature cost breakdown returned empty payload');
    return result;
  },
};
