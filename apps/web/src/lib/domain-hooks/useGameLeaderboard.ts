/**
 * Game Leaderboard Hook (Issue #1467)
 *
 * React Query hook for the social game leaderboard. Fetches the top registered
 * players ranked by wins across the play records visible to the caller.
 * Consumed by the Stats-tab leaderboard component (#1468).
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { GameLeaderboardResponse } from '@/lib/api/schemas';

export interface GameLeaderboardOptions {
  /** ISO date lower bound on the play record session date. */
  since?: string;
  /** Top-N size (1..50, default 10). */
  limit?: number;
  /** External gating (Issue #1466: lazy fetch only when tab is active). Default true. */
  enabled?: boolean;
}

export const GAME_LEADERBOARD_QUERY_KEY = (gameId: string, since?: string, limit?: number) =>
  ['game-leaderboard', gameId, since ?? null, limit ?? 10] as const;

/**
 * Fetches the social leaderboard for a game.
 * @param gameId Game ID (GUID format)
 * @param options Optional since/limit filters; `enabled` for lazy gating (default true).
 */
export function useGameLeaderboard(gameId: string, options?: GameLeaderboardOptions) {
  const { enabled = true, ...apiOptions } = options ?? {};
  return useQuery<GameLeaderboardResponse | null, Error>({
    queryKey: GAME_LEADERBOARD_QUERY_KEY(gameId, apiOptions.since, apiOptions.limit),
    queryFn: () => api.games.getLeaderboard(gameId, apiOptions),
    staleTime: 5 * 60 * 1000, // 5 minutes (matches backend cache)
    retry: 2,
    enabled: !!gameId && enabled,
  });
}
