/**
 * useDiscoverNewGames — TanStack Query hook for the /discover route's
 * "New games" rail (Wave 3 Phase 1, Issue #805 / PR #732 §4.3.2).
 *
 * Backend contract: `GET /api/v1/catalog/games/new?limit={n}` returns
 * `{ items: NewGame[] }`. Empty state is a 200 with `{ items: [] }` per the
 * PR #732 §3.4 empty-state contract.
 *
 * Cache: backend caches 1h via HybridCache; FE staleTime mirrors that to
 * avoid wasteful re-fetches while users browse the discover surface.
 *
 * Backend gate: `RequireAuthorization()` — any authenticated user.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  NewGamesResponseSchema,
  type NewGame,
  type NewGamesResponse,
} from '@/lib/api/schemas/discover.schemas';

export const DISCOVER_NEW_GAMES_DEFAULT_LIMIT = 10;
export const DISCOVER_NEW_GAMES_STALE_TIME_MS = 60 * 60 * 1000; // 1h, mirrors backend cache

export const discoverNewGamesKeys = {
  all: ['discover', 'newGames'] as const,
  list: (limit: number) => [...discoverNewGamesKeys.all, limit] as const,
};

/** Clamp the limit to the same window the backend validator enforces. */
function clampLimit(limit: number): number {
  if (!Number.isFinite(limit) || limit < 1) return DISCOVER_NEW_GAMES_DEFAULT_LIMIT;
  return Math.min(50, Math.floor(limit));
}

export interface UseDiscoverNewGamesOptions {
  readonly limit?: number;
  readonly enabled?: boolean;
}

/**
 * Fetch the most recently created games for the /discover "New games" rail.
 *
 * @example
 * const { data, isLoading } = useDiscoverNewGames({ limit: 8 });
 * // data — NewGame[] (empty array on empty state)
 */
export function useDiscoverNewGames(
  options: UseDiscoverNewGamesOptions = {}
): UseQueryResult<NewGame[], Error> {
  const { limit = DISCOVER_NEW_GAMES_DEFAULT_LIMIT, enabled = true } = options;
  const safeLimit = clampLimit(limit);

  return useQuery<NewGame[], Error>({
    queryKey: discoverNewGamesKeys.list(safeLimit),
    queryFn: async () => {
      const response = await apiClient.get<NewGamesResponse>(
        `/catalog/games/new?limit=${safeLimit}`,
        NewGamesResponseSchema
      );
      return response?.items ?? [];
    },
    enabled,
    staleTime: DISCOVER_NEW_GAMES_STALE_TIME_MS,
  });
}
