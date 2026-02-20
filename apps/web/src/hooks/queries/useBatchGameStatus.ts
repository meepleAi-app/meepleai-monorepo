/**
 * Batch Game Library Status Hook
 * Fetches library status for multiple games in a single API call.
 * Eliminates N+1 problem when rendering game grids.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { GameStatusSimple, BatchGameStatusResponse } from '@/lib/api/schemas/library.schemas';

export type { GameStatusSimple, BatchGameStatusResponse };

/**
 * Batch check library status for multiple games
 * @param gameIds Array of game IDs to check (max 100)
 * @param enabled Whether to execute the query
 */
export function useBatchGameStatus(gameIds: string[], enabled = true) {
  return useQuery({
    queryKey: ['batch-game-status', ...gameIds.sort()], // Sort for cache consistency
    queryFn: async () => {
      return api.library.getBatchGameStatus(gameIds);
    },
    enabled: enabled && gameIds.length > 0,
    staleTime: 30 * 1000, // Cache for 30 seconds
    gcTime: 5 * 60 * 1000, // Keep in cache for 5 minutes
  });
}
