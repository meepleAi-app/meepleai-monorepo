/**
 * useGameMemory - React Query hooks for game memory (house rules & notes)
 *
 * Wraps the AgentMemory API client for fetching game memory and adding house rules.
 * Uses React Query for caching, deduplication, and consistent loading/error states.
 *
 * US-53: House Rules — Phase 1
 */

import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';
import type { GameMemoryDto } from '@/lib/api/clients/agentMemoryClient';

// ========== Query Keys ==========

export const gameMemoryKeys = {
  all: ['game-memory'] as const,
  detail: (gameId: string) => [...gameMemoryKeys.all, gameId] as const,
};

// ========== Query Hook ==========

/**
 * Fetch game memory (house rules + notes) for a specific game.
 *
 * @param gameId - Game ID whose memory to fetch (empty/undefined disables the query)
 * @returns React Query result with GameMemoryDto | null
 *
 * @example
 * ```tsx
 * const { data: memory, isLoading } = useGameMemory(gameId);
 * const rules = memory?.houseRules ?? [];
 * ```
 */
export function useGameMemory(gameId: string | undefined): UseQueryResult<GameMemoryDto | null> {
  return useQuery({
    queryKey: gameMemoryKeys.detail(gameId ?? ''),
    queryFn: () => api.agentMemory.getGameMemory(gameId!),
    enabled: !!gameId,
    staleTime: 60_000,
  });
}

// ========== Mutation Hook ==========

/**
 * Mutation hook to add a house rule for a game.
 * Invalidates the game memory query on success.
 *
 * @param gameId - Game ID to add the house rule to
 * @returns Mutation object for adding house rules
 *
 * @example
 * ```tsx
 * const addRule = useAddHouseRule(gameId);
 * addRule.mutate('No trading on first round');
 * ```
 */
export function useAddHouseRule(gameId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (description: string) => api.agentMemory.addHouseRule(gameId, description),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: gameMemoryKeys.detail(gameId) });
      toast.success('Regola di casa aggiunta!');
    },
    onError: () => {
      toast.error("Errore nell'aggiunta della regola. Riprova.");
    },
  });
}
