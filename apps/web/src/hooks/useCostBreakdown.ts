/**
 * useCostBreakdown — Issue #1838 SP5 F4-C5
 *
 * Fetches stacked-area data (cost per provider per day) for the CostStackedArea
 * panel. The backend caches each range for 5 minutes via HybridCache; the FE
 * stale time mirrors that to avoid extra round-trips on tab focus.
 */

import { useQuery } from '@tanstack/react-query';

import { businessCostApi } from '@/lib/api/business-cost.api';
import type {
  CostBreakdownByProvider,
  CostBreakdownRange,
} from '@/lib/api/schemas/business-cost.schemas';

export const COST_BREAKDOWN_QUERY_KEY = (range: CostBreakdownRange) =>
  ['admin', 'business', 'breakdown', range] as const;

export function useCostBreakdown(range: CostBreakdownRange) {
  return useQuery<CostBreakdownByProvider>({
    queryKey: COST_BREAKDOWN_QUERY_KEY(range),
    queryFn: () => businessCostApi.getBreakdownByProvider(range),
    staleTime: 5 * 60_000,
    gcTime: 30 * 60_000,
    retry: 1,
  });
}
