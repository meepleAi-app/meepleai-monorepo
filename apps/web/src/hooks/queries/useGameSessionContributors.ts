/**
 * useGameSessionContributors — TanStack Query hook for the session-contributor
 * strip on game-detail pages (Issue #2036).
 *
 * Returns up to N registered users with at least one finalized session for
 * the game, ordered by descending session count. Powers the
 * `SessionContributorsStrip` avatar component.
 *
 * Distinct from `useGameContributors` (Issue #2746) which tracks
 * catalog-sharing contributions (toolkit/agent/KB publishing) and hits
 * `/api/v1/shared-games/{id}/contributors`.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { SessionContributorDto } from '@/lib/api/schemas';

export const gameSessionContributorsKeys = {
  all: ['game-session-contributors'] as const,
  game: (gameId: string) => [...gameSessionContributorsKeys.all, gameId] as const,
  list: (gameId: string, limit: number) =>
    [...gameSessionContributorsKeys.game(gameId), { limit }] as const,
};

export function useGameSessionContributors(
  gameId: string,
  limit: number = 8,
  enabled: boolean = true
): UseQueryResult<SessionContributorDto[], Error> {
  return useQuery({
    queryKey: gameSessionContributorsKeys.list(gameId, limit),
    queryFn: async (): Promise<SessionContributorDto[]> => {
      return api.games.getSessionContributors(gameId, limit);
    },
    // Contributors only change when a session is finalized — 5-minute stale
    // is a good balance between freshness and request volume.
    staleTime: 5 * 60 * 1000,
    enabled: enabled && !!gameId,
  });
}
