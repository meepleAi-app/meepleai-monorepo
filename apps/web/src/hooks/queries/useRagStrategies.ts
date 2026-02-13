/**
 * useRagStrategies - TanStack Query hook for RAG Strategies
 *
 * Issue #3: Agent Config - Strategy Selector
 *
 * Provides caching for fetching available RAG strategies from backend.
 * Strategies can be filtered by user tier for quota enforcement.
 */

import { useQuery, UseQueryResult } from '@tanstack/react-query';

import { ragStrategiesApi, type RagStrategyDto } from '@/lib/api/rag-strategies.api';

/**
 * Query key factory for RAG strategies queries
 */
export const ragStrategiesKeys = {
  all: ['ragStrategies'] as const,
  byTier: (tier: string) => [...ragStrategiesKeys.all, 'tier', tier] as const,
};

/**
 * Hook to fetch all available RAG strategies
 *
 * Features:
 * - Fetches from GET /api/v1/rag/strategies (Issue #8)
 * - Caches for 30 minutes (strategies are static)
 * - Ordered by complexity (backend returns ordered)
 *
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with strategies array
 */
export function useRagStrategies(
  enabled: boolean = true
): UseQueryResult<RagStrategyDto[], Error> {
  return useQuery({
    queryKey: ragStrategiesKeys.all,
    queryFn: ragStrategiesApi.getAll,
    enabled,
    // Strategies are static (30 minutes cache)
    staleTime: 30 * 60 * 1000,
  });
}

/**
 * Hook to fetch RAG strategies filtered by user tier
 *
 * Issue #9: Tier-based strategy filtering
 *
 * @param userTier - User's subscription tier (Free, Basic, Pro, Enterprise)
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with tier-filtered strategies
 */
export function useRagStrategiesByTier(
  userTier: 'Free' | 'Basic' | 'Pro' | 'Enterprise',
  enabled: boolean = true
): UseQueryResult<RagStrategyDto[], Error> {
  return useQuery({
    queryKey: ragStrategiesKeys.byTier(userTier),
    queryFn: async (): Promise<RagStrategyDto[]> => {
      const allStrategies = await ragStrategiesApi.getAll();
      return filterStrategiesByTier(allStrategies, userTier);
    },
    enabled,
    staleTime: 30 * 60 * 1000,
  });
}

/**
 * Filter strategies based on user tier (Issue #9)
 *
 * Tier-Strategy Mapping:
 * - Free: Fast, Balanced (complexity 0-1)
 * - Basic: Free + SentenceWindow, StepBack, QueryExpansion (complexity 0-5, 9-10)
 * - Pro: Basic + all except Custom (complexity 0-11, no Custom)
 * - Enterprise: All including Custom
 */
function filterStrategiesByTier(
  strategies: RagStrategyDto[],
  tier: 'Free' | 'Basic' | 'Pro' | 'Enterprise'
): RagStrategyDto[] {
  switch (tier) {
    case 'Free':
      // Only simplest strategies (complexity 0-1)
      return strategies.filter(s => s.complexity <= 1 && s.name !== 'Custom');

    case 'Basic':
      // Simple + some advanced (complexity 0-5, plus StepBack, QueryExpansion)
      return strategies.filter(s =>
        (s.complexity <= 5 || s.name === 'StepBack' || s.name === 'QueryExpansion') &&
        s.name !== 'Custom'
      );

    case 'Pro':
      // All except Custom
      return strategies.filter(s => s.name !== 'Custom');

    case 'Enterprise':
      // All including Custom
      return strategies;

    default:
      // Default to Free tier (safest)
      return strategies.filter(s => s.complexity <= 1 && s.name !== 'Custom');
  }
}
