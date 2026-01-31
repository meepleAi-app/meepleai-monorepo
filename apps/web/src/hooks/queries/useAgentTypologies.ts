/**
 * useAgentTypologies - TanStack Query hooks for Agent Typologies
 *
 * Issue #3249: [FRONT-013] Agent Type Switcher & Dynamic Typology
 *
 * Provides caching and mutations for fetching approved typologies
 * and switching typology during active chat sessions.
 */

import { useQuery, useMutation, useQueryClient, UseQueryResult, UseMutationResult } from '@tanstack/react-query';

import { agentTypologiesApi } from '@/lib/api/agent-typologies.api';
import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';

/**
 * Query key factory for agent typologies queries
 */
export const agentTypologiesKeys = {
  all: ['agentTypologies'] as const,
  approved: () => [...agentTypologiesKeys.all, 'approved'] as const,
  byId: (id: string) => [...agentTypologiesKeys.all, id] as const,
};

/**
 * Hook to fetch all approved typologies for the agent switcher
 *
 * Features:
 * - Filters to only Approved status typologies
 * - Caches for 5 minutes (typologies change infrequently)
 *
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with approved typologies array
 */
export function useApprovedTypologies(
  enabled: boolean = true
): UseQueryResult<Typology[], Error> {
  return useQuery({
    queryKey: agentTypologiesKeys.approved(),
    queryFn: async (): Promise<Typology[]> => {
      const allTypologies = await agentTypologiesApi.getAll();
      // Filter to only approved, non-deleted typologies
      return allTypologies.filter(
        (t) => t.status === 'Approved' && !t.isDeleted
      );
    },
    enabled,
    // Typologies change infrequently (5 minutes)
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Hook to fetch a single typology by ID
 *
 * @param id - Typology UUID
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with typology or null
 */
export function useTypology(
  id: string,
  enabled: boolean = true
): UseQueryResult<Typology | null, Error> {
  return useQuery({
    queryKey: agentTypologiesKeys.byId(id),
    queryFn: async (): Promise<Typology | null> => {
      return agentTypologiesApi.getById(id);
    },
    enabled: enabled && !!id,
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Request type for switching typology
 */
export interface SwitchTypologyRequest {
  sessionId: string;
  typologyId: string;
}

/**
 * Response type for switch typology
 */
export interface SwitchTypologyResponse {
  success: boolean;
  newTypologyId: string;
  newTypologyName: string;
}

/**
 * Hook to switch typology during active chat session
 *
 * Features:
 * - PATCH request to update session typology
 * - Does NOT invalidate chat history (preserves messages)
 * - Invalidates session data to reflect new typology
 *
 * @returns UseMutationResult for switching typology
 */
export function useSwitchTypology(): UseMutationResult<
  SwitchTypologyResponse,
  Error,
  SwitchTypologyRequest,
  { previousTypologyId: string | undefined }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ sessionId, typologyId }): Promise<SwitchTypologyResponse> => {
      // PATCH /api/v1/game-sessions/{sessionId}/agent/typology
      const response = await fetch(`/api/v1/game-sessions/${sessionId}/agent/typology`, {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ typologyId }),
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ message: 'Failed to switch typology' }));
        throw new Error(error.message || 'Failed to switch typology');
      }

      return response.json();
    },

    // Invalidate session queries to reflect new typology
    onSuccess: (_data, { sessionId }) => {
      // Invalidate session-specific queries
      queryClient.invalidateQueries({ queryKey: ['agent-session', sessionId] });
      queryClient.invalidateQueries({ queryKey: ['sessions', sessionId] });
    },
  });
}
