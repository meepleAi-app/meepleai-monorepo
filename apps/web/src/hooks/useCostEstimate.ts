/**
 * useCostEstimate - Fetch cost estimation for agent typology configuration
 * Issue #3383: Cost Estimation Preview Before Launch
 *
 * Fetches real-time cost estimates from the API based on typology ID.
 * Provides per-query, per-session, and monthly cost projections.
 */

import { useQuery, UseQueryResult } from '@tanstack/react-query';

/**
 * Cost estimation response from API
 * Matches CostEstimateDto from backend
 */
export interface CostEstimate {
  estimatedTokensPerQuery: number;
  estimatedCostPerQuery: number;
  estimatedMonthlyCost10K: number;
  costByPhase: Record<string, number>;
}

/**
 * Full API response wrapper
 */
export interface CostEstimateResponse {
  agentDefinitionId: string;
  typologyName: string;
  strategy: string;
  costEstimate: CostEstimate;
}

/**
 * Query key factory for cost estimate queries
 */
export const costEstimateKeys = {
  all: ['costEstimate'] as const,
  byTypology: (agentDefinitionId: string) => [...costEstimateKeys.all, agentDefinitionId] as const,
};

/**
 * Hook to fetch cost estimate for an agent typology
 *
 * Features:
 * - Real-time cost calculation based on backend pricing
 * - Automatic caching (stale after 30 seconds)
 * - Conditional execution (only when agentDefinitionId provided)
 *
 * @param agentDefinitionId - Agent typology UUID
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with cost estimate or null
 *
 * @example
 * ```tsx
 * const { data: estimate, isLoading } = useCostEstimate(agentDefinitionId);
 * if (estimate) {
 *   console.log(`Per query: $${estimate.costEstimate.estimatedCostPerQuery}`);
 * }
 * ```
 */
export function useCostEstimate(
  agentDefinitionId: string | null | undefined,
  enabled: boolean = true
): UseQueryResult<CostEstimateResponse | null, Error> {
  return useQuery({
    queryKey: costEstimateKeys.byTypology(agentDefinitionId || ''),
    queryFn: async (): Promise<CostEstimateResponse | null> => {
      if (!agentDefinitionId) return null;

      // Call admin API endpoint: GET /api/v1/admin/agent-typologies/{id}/cost-estimate
      // Note: This endpoint requires admin role
      const response = await fetch(
        `/api/v1/admin/agent-typologies/${agentDefinitionId}/cost-estimate`,
        {
          method: 'GET',
          credentials: 'include',
          headers: {
            'Content-Type': 'application/json',
          },
        }
      );

      if (!response.ok) {
        if (response.status === 404) {
          throw new Error('Agent typology not found');
        }
        if (response.status === 403) {
          throw new Error('Unauthorized: Admin access required');
        }
        throw new Error(`Failed to fetch cost estimate: ${response.status}`);
      }

      const data = await response.json();

      // Backend returns: { agentDefinitionId, typologyName, strategy, costEstimate }
      // Transform to match our interface
      return {
        agentDefinitionId: data.agentDefinitionId,
        typologyName: data.typologyName,
        strategy: data.strategy,
        costEstimate: {
          estimatedTokensPerQuery: data.costEstimate.estimatedTokensPerQuery,
          estimatedCostPerQuery: data.costEstimate.estimatedCostPerQuery,
          estimatedMonthlyCost10K: data.costEstimate.estimatedMonthlyCost10K,
          costByPhase: data.costEstimate.costByPhase || {},
        },
      };
    },
    enabled: enabled && !!agentDefinitionId,
    // Cost estimates change infrequently (30 seconds)
    staleTime: 30 * 1000,
    // Keep in cache for 5 minutes
    gcTime: 5 * 60 * 1000,
  });
}

/**
 * Calculate per-session cost based on estimated queries
 *
 * @param perQueryCost - Cost per individual query
 * @param estimatedQueriesPerSession - Expected number of queries in a session (default: 5)
 * @returns Estimated cost for one session
 */
export function calculateSessionCost(
  perQueryCost: number,
  estimatedQueriesPerSession: number = 5
): number {
  return perQueryCost * estimatedQueriesPerSession;
}

/**
 * Determine warning level based on cost threshold
 *
 * @param sessionCost - Estimated cost per session
 * @returns Warning level: 'low' | 'medium' | 'high'
 */
export function getCostWarningLevel(sessionCost: number): 'low' | 'medium' | 'high' {
  if (sessionCost >= 0.5) return 'high'; // $0.50+ per session
  if (sessionCost >= 0.2) return 'medium'; // $0.20-$0.49 per session
  return 'low'; // < $0.20 per session
}
