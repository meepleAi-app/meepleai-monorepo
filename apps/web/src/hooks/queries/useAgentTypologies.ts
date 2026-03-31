/**
 * useAgentTypologies — compatibility shim for agent type selection
 *
 * Typologies were collapsed into AgentDefinition. This hook now fetches
 * from the admin agent-definitions endpoint.
 */

import {
  useQuery,
  useMutation,
  useQueryClient,
  UseQueryResult,
  UseMutationResult,
} from '@tanstack/react-query';

import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';
import type { AgentDefinitionDto as Typology } from '@/lib/api/schemas/agent-definitions.schemas';

export const agentTypologiesKeys = {
  all: ['agentTypologies'] as const,
  approved: () => [...agentTypologiesKeys.all, 'approved'] as const,
  byId: (id: string) => [...agentTypologiesKeys.all, id] as const,
};

/**
 * Fetch active agent definitions for the agent type selector.
 */
export function useApprovedTypologies(enabled: boolean = true): UseQueryResult<Typology[], Error> {
  return useQuery({
    queryKey: agentTypologiesKeys.approved(),
    queryFn: () => agentDefinitionsApi.getAll({ activeOnly: true }),
    enabled,
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Fetch a single agent definition by ID.
 */
export function useTypology(
  id: string,
  enabled: boolean = true
): UseQueryResult<Typology | null, Error> {
  return useQuery({
    queryKey: agentTypologiesKeys.byId(id),
    queryFn: async (): Promise<Typology | null> => {
      try {
        return await agentDefinitionsApi.getById(id);
      } catch {
        return null;
      }
    },
    enabled: enabled && !!id,
    staleTime: 5 * 60 * 1000,
  });
}

export interface SwitchTypologyRequest {
  sessionId: string;
  agentDefinitionId: string;
}

export interface SwitchTypologyResponse {
  success: boolean;
  newagentDefinitionId: string;
  newTypologyName: string;
}

export function useSwitchTypology(): UseMutationResult<
  SwitchTypologyResponse,
  Error,
  SwitchTypologyRequest,
  { previousagentDefinitionId: string | undefined }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ sessionId, agentDefinitionId }): Promise<SwitchTypologyResponse> => {
      const response = await fetch(`/api/v1/game-sessions/${sessionId}/agent/typology`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ agentDefinitionId }),
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({ message: 'Failed to switch typology' }));
        throw new Error(error.message || 'Failed to switch typology');
      }

      return response.json();
    },

    onSuccess: (_data, { sessionId }) => {
      queryClient.invalidateQueries({ queryKey: ['agent-session', sessionId] });
      queryClient.invalidateQueries({ queryKey: ['sessions', sessionId] });
    },
  });
}
