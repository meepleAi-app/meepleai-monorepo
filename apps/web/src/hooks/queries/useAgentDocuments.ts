/**
 * useAgentDocuments - Agent Document Selection Hooks
 *
 * Issue #2399: Knowledge Base Document Selection
 *
 * React Query hooks for fetching and updating agent document selections.
 * Used by DocumentSelector and SelectedDocumentsList components.
 */

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { AgentDocumentsDto, UpdateAgentDocumentsResponse } from '@/lib/api/schemas';

// ========== Query Keys ==========

export const agentDocumentsKeys = {
  all: ['agent-documents'] as const,
  byAgent: (agentId: string) => ['agent-documents', 'by-agent', agentId] as const,
} as const;

// ========== Query Options ==========

interface UseAgentDocumentsOptions {
  agentId: string | null;
  enabled?: boolean;
}

// ========== Query Hook ==========

/**
 * Fetch selected documents for an agent's knowledge base
 *
 * @param options - Query options
 * @returns Query result with agent documents data
 *
 * @example
 * ```tsx
 * const { data, isLoading, error } = useAgentDocuments({ agentId: 'xxx' });
 * ```
 */
export function useAgentDocuments({ agentId, enabled = true }: UseAgentDocumentsOptions) {
  return useQuery<AgentDocumentsDto | null, Error>({
    queryKey: agentDocumentsKeys.byAgent(agentId ?? ''),
    queryFn: async () => {
      if (!agentId) {
        throw new Error('Agent ID is required');
      }
      return api.agents.getDocuments(agentId);
    },
    enabled: enabled && agentId !== null,
    staleTime: 30_000, // 30 seconds - document selections don't change frequently
    gcTime: 5 * 60_000, // 5 minutes cache
  });
}

// ========== Mutation Hook ==========

interface UseUpdateAgentDocumentsOptions {
  onSuccess?: (data: UpdateAgentDocumentsResponse) => void;
  onError?: (error: Error) => void;
}

/**
 * Update selected documents for an agent's knowledge base (Admin only)
 *
 * @param options - Mutation options
 * @returns Mutation result with update function
 *
 * @example
 * ```tsx
 * const { mutate, isPending } = useUpdateAgentDocuments({
 *   onSuccess: () => toast.success('Documents updated'),
 * });
 *
 * mutate({ agentId: 'xxx', documentIds: ['doc1', 'doc2'] });
 * ```
 */
export function useUpdateAgentDocuments(options?: UseUpdateAgentDocumentsOptions) {
  const queryClient = useQueryClient();

  return useMutation<
    UpdateAgentDocumentsResponse,
    Error,
    { agentId: string; documentIds: string[] }
  >({
    mutationFn: async ({ agentId, documentIds }) => {
      return api.agents.updateDocuments(agentId, documentIds);
    },
    onSuccess: (data, variables) => {
      // Invalidate the agent documents query to refetch
      void queryClient.invalidateQueries({
        queryKey: agentDocumentsKeys.byAgent(variables.agentId),
      });
      options?.onSuccess?.(data);
    },
    onError: error => {
      options?.onError?.(error);
    },
  });
}
