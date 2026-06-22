/**
 * useHouseRulesMetadata — TanStack Query useQuery hook for the SP6 §F drawer
 * ConnectionBar 4-pip counts (agent · game · session · kb).
 *
 * Issue #2492. Wires the BE endpoint shipped alongside this hook:
 *   GET /api/v1/agent-memory/house-rules/metadata?gameId={gameId}
 *
 * - 200 OK: HouseRulesMetadataDto (4 counts)
 * - 401: apiClient returns null; the hook surfaces null in data
 * - 400: caller forgot to pass a valid gameId — handled by `enabled` guard
 *
 * Cache: 5-minute staleTime per issue spec. ConnectionBar is fetch-once on
 * drawer open per DEC-2 (no SSE).
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { HouseRulesMetadataDto } from '@/lib/api/clients/agentMemoryClient';

export const HOUSE_RULES_METADATA_STALE_TIME_MS = 5 * 60 * 1000; // 5 min.

export const houseRulesMetadataKeys = {
  all: ['agent-memory', 'house-rules', 'metadata'] as const,
  byGame: (gameId: string) => [...houseRulesMetadataKeys.all, gameId] as const,
};

export interface UseHouseRulesMetadataOptions {
  readonly gameId: string | null | undefined;
  readonly enabled?: boolean;
}

/**
 * Fetch the 4 ConnectionBar counts for a game's house-rule context.
 *
 * @example
 * const { data, isLoading } = useHouseRulesMetadata({ gameId });
 * if (data) renderPips(data.agentCount, data.gameCount, data.sessionCount, data.kbCount);
 */
export function useHouseRulesMetadata({ gameId, enabled = true }: UseHouseRulesMetadataOptions) {
  const isValid = typeof gameId === 'string' && gameId.length > 0;

  return useQuery<HouseRulesMetadataDto | null, Error>({
    queryKey: isValid ? houseRulesMetadataKeys.byGame(gameId) : houseRulesMetadataKeys.all,
    queryFn: async () => {
      if (!isValid) {
        throw new Error('gameId is required');
      }
      return api.agentMemory.getHouseRulesMetadata(gameId);
    },
    enabled: enabled && isValid,
    staleTime: HOUSE_RULES_METADATA_STALE_TIME_MS,
  });
}
