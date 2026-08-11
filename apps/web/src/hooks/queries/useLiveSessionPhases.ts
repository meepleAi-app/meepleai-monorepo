/**
 * useLiveSessionPhases — TanStack Query loader for a live session's turn phases.
 *
 * Backs the #2787 Catan flavor turn/phase header (`currentPhaseName`). The
 * endpoint (`GET /api/v1/live-sessions/{id}/phases` → `TurnPhasesDto`) returns
 * `hasPhases:false` / `currentPhaseName:null` for sessions without configured
 * phases, so the flavor header degrades gracefully to round + active player.
 *
 * Mirrors the `useLiveSession` contract: a 404 maps to `null` (not an error).
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';
import type { TurnPhasesDto } from '@/lib/api/schemas/live-sessions.schemas';

import { liveSessionKeys } from './useLiveSession';

/**
 * Hook to fetch the turn-phases configuration for a live session.
 *
 * @param sessionId - LiveGameSession ID
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with the TurnPhasesDto, or null when not found
 */
export function useLiveSessionPhases(
  sessionId: string,
  enabled: boolean = true
): UseQueryResult<TurnPhasesDto | null, Error> {
  return useQuery({
    queryKey: [...liveSessionKeys.detail(sessionId), 'phases'],
    queryFn: async (): Promise<TurnPhasesDto | null> => {
      try {
        return await api.liveSessions.getPhases(sessionId);
      } catch (err) {
        if (err instanceof ApiError && err.statusCode === 404) return null;
        throw err;
      }
    },
    enabled: enabled && !!sessionId,
    staleTime: 30 * 1000,
  });
}
