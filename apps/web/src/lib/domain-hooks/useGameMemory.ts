/**
 * Game Memory Hook (Issue #1464)
 *
 * React Query hooks for the AgentMemory game-memory aggregate: query + 3 mutations
 * (add/update/remove house rules) with cache invalidation. Mirror useGameAgents
 * lazy-gating pattern; mutations invalidate the cached game-memory entry.
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { GameMemoryDto } from '@/lib/api/clients/agentMemoryClient';

export const GAME_MEMORY_QUERY_KEY = (gameId: string) => ['game-memory', gameId] as const;

export interface UseGameMemoryOptions {
  enabled?: boolean;
}

export function useGameMemory(gameId: string, options?: UseGameMemoryOptions) {
  const enabled = options?.enabled !== false;
  return useQuery<GameMemoryDto | null, Error>({
    queryKey: GAME_MEMORY_QUERY_KEY(gameId),
    queryFn: () => api.agentMemory.getGameMemory(gameId),
    staleTime: 5 * 60 * 1000,
    enabled: !!gameId && enabled,
  });
}

export function useAddHouseRule(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (description: string) => api.agentMemory.addHouseRule(gameId, description),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: GAME_MEMORY_QUERY_KEY(gameId) }),
  });
}

export function useUpdateHouseRule(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (vars: { ruleId: string; description: string }) =>
      api.agentMemory.updateHouseRule(gameId, vars.ruleId, vars.description),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: GAME_MEMORY_QUERY_KEY(gameId) }),
  });
}

export function useRemoveHouseRule(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (ruleId: string) => api.agentMemory.removeHouseRule(gameId, ruleId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: GAME_MEMORY_QUERY_KEY(gameId) }),
  });
}
