/**
 * React Query hook for agent cost estimation.
 *
 * Only fetches when at least one document ID is provided.
 * Results are cached for 1 minute (cost doesn't change frequently).
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { AgentCostEstimate } from '@/lib/api/schemas/rag-setup.schemas';

export const agentCostKeys = {
  all: ['agentCostEstimate'] as const,
  estimate: (gameId: string, documentIds: string[]) =>
    [...agentCostKeys.all, gameId, ...documentIds.sort()] as const,
};

export function useEstimateAgentCost(
  gameId: string,
  documentIds: string[],
  enabled = true
) {
  return useQuery<AgentCostEstimate | null, Error>({
    queryKey: agentCostKeys.estimate(gameId, documentIds),
    queryFn: () =>
      api.agents.estimateAgentCost({
        gameId,
        documentIds,
        strategyName: 'HybridSearch',
      }),
    enabled: enabled && !!gameId && documentIds.length > 0,
    staleTime: 60_000, // 1 minute
  });
}
