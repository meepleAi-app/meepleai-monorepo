/**
 * React Query hook for player statistics and data.
 *
 * Uses the play records statistics API to get aggregated player data.
 * A dedicated players API may be added in the future.
 *
 * @see Issue #4692
 */

import { useQuery } from '@tanstack/react-query';

import { playRecordsApi } from '@/lib/api/play-records.api';
import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';

export const playersKeys = {
  all: ['players'] as const,
  stats: () => [...playersKeys.all, 'statistics'] as const,
};

/**
 * Fetch player statistics (total sessions, wins, game play counts).
 */
export function usePlayerStatistics() {
  return useQuery({
    queryKey: playersKeys.stats(),
    queryFn: () => playRecordsApi.getPlayerStatistics(),
    staleTime: 60_000,
  });
}
