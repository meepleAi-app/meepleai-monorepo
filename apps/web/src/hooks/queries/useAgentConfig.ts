/**
 * useAgentConfig - TanStack Query hooks for AI Agent Configuration
 *
 * Issue #2518: User Library - Catalog, Library & Agent Configuration UI
 *
 * Provides caching and mutations for per-game agent customization.
 */

import { useQuery, useMutation, useQueryClient, UseQueryResult, UseMutationResult } from '@tanstack/react-query';

import {
  api,
  type AgentConfigDto,
  type UpdateAgentConfigRequest,
} from '@/lib/api';

/**
 * Query key factory for agent configuration queries
 */
export const agentConfigKeys = {
  all: ['agentConfig'] as const,
  byGame: (gameId: string) => [...agentConfigKeys.all, gameId] as const,
};

/**
 * Hook to fetch agent configuration for a game
 *
 * Features:
 * - Automatic caching per game
 * - Returns null if no configuration exists (uses default)
 *
 * @param gameId - Game UUID
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with agent configuration or null
 */
export function useAgentConfig(
  gameId: string,
  enabled: boolean = true
): UseQueryResult<AgentConfigDto | null, Error> {
  return useQuery({
    queryKey: agentConfigKeys.byGame(gameId),
    queryFn: async (): Promise<AgentConfigDto | null> => {
      return api.library.getAgentConfig(gameId);
    },
    enabled: enabled && !!gameId,
    // Agent config changes infrequently (2 minutes)
    staleTime: 2 * 60 * 1000,
  });
}

/**
 * Hook to update agent configuration for a game
 *
 * Features:
 * - Optimistic updates for better UX
 * - Automatic cache invalidation
 * - Rollback on error
 *
 * @returns UseMutationResult for updating agent configuration
 */
export function useUpdateAgentConfig(): UseMutationResult<
  AgentConfigDto,
  Error,
  { gameId: string; request: UpdateAgentConfigRequest },
  { previousConfig: AgentConfigDto | null | undefined }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ gameId, request }) => {
      return api.library.updateAgentConfig(gameId, request);
    },

    // Optimistic update: immediately update cache before API responds
    onMutate: async ({ gameId, request }) => {
      // Cancel outgoing queries for this game
      await queryClient.cancelQueries({ queryKey: agentConfigKeys.byGame(gameId) });

      // Snapshot previous value
      const previousConfig = queryClient.getQueryData<AgentConfigDto | null>(
        agentConfigKeys.byGame(gameId)
      );

      // Optimistically update cache
      queryClient.setQueryData<AgentConfigDto>(agentConfigKeys.byGame(gameId), (old) => {
        if (!old) {
          // If no config exists, create optimistic one
          return {
            id: 'temp-id',
            userId: 'temp-user',
            gameId,
            ...request,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          } as AgentConfigDto;
        }
        // Update existing config
        return {
          ...old,
          ...request,
          updatedAt: new Date().toISOString(),
        };
      });

      return { previousConfig };
    },

    // On error, rollback to previous value
    onError: (_error, { gameId }, context) => {
      if (context?.previousConfig !== undefined) {
        queryClient.setQueryData(agentConfigKeys.byGame(gameId), context.previousConfig);
      }
    },

    // Always refetch after success or error
    onSettled: (_data, _error, { gameId }) => {
      queryClient.invalidateQueries({ queryKey: agentConfigKeys.byGame(gameId) });
    },
  });
}
