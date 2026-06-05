/**
 * useFeatureCosts — Issue #1838 SP5 F4-C5
 *
 * Per-feature breakdown for FeatureCostTable. The handler bundles the per-row
 * provider drill into the same payload (one round trip per range), so the
 * expand-row in the FE is a pure render operation.
 */

import { useQuery } from '@tanstack/react-query';

import { businessCostApi } from '@/lib/api/business-cost.api';
import type {
  CostBreakdownByFeature,
  CostBreakdownRange,
} from '@/lib/api/schemas/business-cost.schemas';

export const FEATURE_COSTS_QUERY_KEY = (range: CostBreakdownRange) =>
  ['admin', 'business', 'per-feature', range] as const;

export function useFeatureCosts(range: CostBreakdownRange) {
  return useQuery<CostBreakdownByFeature>({
    queryKey: FEATURE_COSTS_QUERY_KEY(range),
    queryFn: () => businessCostApi.getBreakdownByFeature(range),
    staleTime: 5 * 60_000,
    gcTime: 30 * 60_000,
    retry: 1,
  });
}
