/**
 * useGameSetup - TanStack Query hooks for Setup Wizard
 *
 * Issue #5583: Setup Wizard — guided game preparation
 *
 * Provides hooks to fetch:
 * - Rulebook analysis (game phases, resources, mechanics, victory conditions)
 * - Entity links filtered by ExpansionOf (expansion toggles)
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api, type RulebookAnalysisDto, type EntityLinkDto } from '@/lib/api';

/**
 * Query key factory for setup wizard queries
 */
export const gameSetupKeys = {
  all: ['gameSetup'] as const,
  analysis: (gameId: string) => [...gameSetupKeys.all, 'analysis', gameId] as const,
  expansions: (gameId: string) => [...gameSetupKeys.all, 'expansions', gameId] as const,
};

/**
 * Hook to fetch all active rulebook analyses for a game.
 *
 * Returns the first (most recent/active) analysis if available.
 *
 * @param gameId - Shared game UUID
 * @param enabled - Whether to run the query
 */
export function useGameAnalysis(
  gameId: string,
  enabled: boolean = true
): UseQueryResult<RulebookAnalysisDto | null, Error> {
  return useQuery({
    queryKey: gameSetupKeys.analysis(gameId),
    queryFn: async () => {
      const analyses = await api.sharedGames.getGameAnalysis(gameId);
      return analyses.length > 0 ? analyses[0] : null;
    },
    enabled: enabled && !!gameId,
    staleTime: 10 * 60 * 1000, // 10 minutes — analysis changes rarely
  });
}

/**
 * Hook to fetch expansion links for a game.
 *
 * Fetches entity links where linkType=ExpansionOf for the given game.
 *
 * @param gameId - Shared game UUID
 * @param enabled - Whether to run the query
 */
export function useGameExpansions(
  gameId: string,
  enabled: boolean = true
): UseQueryResult<EntityLinkDto[], Error> {
  return useQuery({
    queryKey: gameSetupKeys.expansions(gameId),
    queryFn: async () => {
      return api.entityLinks.getEntityLinks('Game', gameId, {
        linkType: 'ExpansionOf',
      });
    },
    enabled: enabled && !!gameId,
    staleTime: 10 * 60 * 1000,
  });
}
