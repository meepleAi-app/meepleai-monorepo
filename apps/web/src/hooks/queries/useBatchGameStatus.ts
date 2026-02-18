/**
 * Batch Game Library Status Hook
 * Fetches library status for multiple games in a single API call.
 * Eliminates N+1 problem when rendering game grids.
 */

import { useQuery } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/core/apiClient';

export interface GameStatusSimple {
  inLibrary: boolean;
  isFavorite: boolean;
  isOwned: boolean;
}

export interface BatchGameStatusResponse {
  results: Record<string, GameStatusSimple>;
  totalChecked: number;
}

/**
 * Batch check library status for multiple games
 * @param gameIds Array of game IDs to check
 * @param enabled Whether to execute the query
 */
export function useBatchGameStatus(gameIds: string[], enabled = true) {
  return useQuery({
    queryKey: ['batch-game-status', ...gameIds.sort()], // Sort for cache consistency
    queryFn: async () => {
      if (gameIds.length === 0) {
        return { results: {}, totalChecked: 0 };
      }

      const idsParam = gameIds.join(',');
      const response = await apiClient.get<BatchGameStatusResponse>(
        `/api/v1/library/games/batch-status?gameIds=${idsParam}`
      );

      return response.data;
    },
    enabled: enabled && gameIds.length > 0,
    staleTime: 30 * 1000, // Cache for 30 seconds
    gcTime: 5 * 60 * 1000, // Keep in cache for 5 minutes
  });
}
