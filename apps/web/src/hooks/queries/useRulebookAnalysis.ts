/**
 * useRulebookAnalysis - TanStack Query hook for Rulebook Analysis
 *
 * Issue #5584: Rules Explainer progressive presentation
 *
 * Fetches structured rulebook analysis data for the Rules Explainer component.
 * Uses the public endpoint: GET /api/v1/shared-games/{id}/analysis
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api, type RulebookAnalysisDto } from '@/lib/api';

/**
 * Query key factory for rulebook analysis queries
 */
export const rulebookAnalysisKeys = {
  all: ['rulebookAnalysis'] as const,
  byGame: (gameId: string) => [...rulebookAnalysisKeys.all, gameId] as const,
};

/**
 * Hook to fetch rulebook analysis for a shared game
 *
 * @param gameId - Shared game UUID
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with array of rulebook analyses
 */
export function useRulebookAnalysis(
  gameId: string,
  enabled: boolean = true
): UseQueryResult<RulebookAnalysisDto[], Error> {
  return useQuery({
    queryKey: rulebookAnalysisKeys.byGame(gameId),
    queryFn: async () => {
      return api.sharedGames.getGameAnalysis(gameId);
    },
    enabled: enabled && !!gameId,
    // Analysis data rarely changes (15 minutes)
    staleTime: 15 * 60 * 1000,
  });
}
