import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { GameContributorDto } from '@/lib/api';

/**
 * React Query hook for Game Contributors
 * Issue #2746: Frontend - Contributor Display su SharedGame
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 */

/**
 * Query key factory for game contributors
 */
export const gameContributorsKeys = {
  all: ['gameContributors'] as const,
  byGame: (gameId: string) => [...gameContributorsKeys.all, 'byGame', gameId] as const,
};

/**
 * Get all contributors for a shared game
 *
 * @param gameId - Shared game UUID
 * @param enabled - Whether the query should be enabled (default: true)
 * @returns Query result with array of contributors
 *
 * @example
 * ```tsx
 * const { data: contributors, isLoading } = useGameContributors(gameId);
 * const primary = contributors?.find(c => c.isPrimaryContributor);
 * ```
 */
export function useGameContributors(
  gameId: string,
  enabled: boolean = true
): UseQueryResult<GameContributorDto[], Error> {
  return useQuery({
    queryKey: gameContributorsKeys.byGame(gameId),
    queryFn: async (): Promise<GameContributorDto[]> => {
      return api.gameContributors.getGameContributors(gameId);
    },
    enabled: enabled && !!gameId,
    staleTime: 5 * 60 * 1000, // 5 minutes (contributors change infrequently)
  });
}
