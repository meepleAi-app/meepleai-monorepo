/**
 * ISSUE-3709: React Query Hooks for Agent Definitions
 * Hooks for Agent Builder UI data fetching and mutations
 */

'use client';

import { useMutation, useQuery, useQueryClient, type UseMutationResult, type UseQueryResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import * as agentClient from '@/lib/api/admin-agent-client';
import type {
  AgentDefinitionResponse,
  CreateAgentDefinitionRequest,
  UpdateAgentDefinitionRequest,
} from '@/lib/schemas/agent-definition-schema';

// Query Keys
const agentDefinitionsKeys = {
  all: ['agent-definitions'] as const,
  lists: () => [...agentDefinitionsKeys.all, 'list'] as const,
  list: (filters?: { activeOnly?: boolean; search?: string }) =>
    [...agentDefinitionsKeys.lists(), filters] as const,
  details: () => [...agentDefinitionsKeys.all, 'detail'] as const,
  detail: (id: string) => [...agentDefinitionsKeys.details(), id] as const,
  stats: () => [...agentDefinitionsKeys.all, 'stats'] as const,
};

/**
 * Fetch all agent definitions
 */
export function useAgentDefinitions(params?: {
  activeOnly?: boolean;
  search?: string;
}): UseQueryResult<AgentDefinitionResponse[], Error> {
  return useQuery({
    queryKey: agentDefinitionsKeys.list(params),
    queryFn: () => agentClient.getAgentDefinitions(params),
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

/**
 * Fetch single agent definition by ID
 */
export function useAgentDefinition(id: string | null): UseQueryResult<AgentDefinitionResponse, Error> {
  return useQuery({
    queryKey: agentDefinitionsKeys.detail(id || ''),
    queryFn: () => agentClient.getAgentDefinition(id!),
    enabled: !!id, // Only fetch if ID is provided
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Fetch agent definition statistics
 */
export function useAgentDefinitionStats(): UseQueryResult<{
  totalCount: number;
  byType: Record<string, number>;
  recentTemplates: AgentDefinitionResponse[];
}, Error> {
  return useQuery({
    queryKey: agentDefinitionsKeys.stats(),
    queryFn: () => agentClient.getAgentDefinitionStats(),
    staleTime: 10 * 60 * 1000, // 10 minutes for stats
  });
}

/**
 * Create agent definition mutation
 */
export function useCreateAgent(): UseMutationResult<
  AgentDefinitionResponse,
  Error,
  CreateAgentDefinitionRequest
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: agentClient.createAgentDefinition,
    onSuccess: (data) => {
      // Invalidate and refetch agent lists
      queryClient.invalidateQueries({ queryKey: agentDefinitionsKeys.lists() });
      queryClient.invalidateQueries({ queryKey: agentDefinitionsKeys.stats() });

      // Optimistically add to cache
      queryClient.setQueryData(agentDefinitionsKeys.detail(data.id), data);

      toast.success(`Agent "${data.name}" created successfully`);
    },
    onError: (error) => {
      toast.error(`Failed to create agent: ${error.message}`);
    },
  });
}

/**
 * Update agent definition mutation
 */
export function useUpdateAgent(): UseMutationResult<
  AgentDefinitionResponse,
  Error,
  { id: string; data: UpdateAgentDefinitionRequest }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }) => agentClient.updateAgentDefinition(id, data),
    onSuccess: (data) => {
      // Invalidate and refetch
      queryClient.invalidateQueries({ queryKey: agentDefinitionsKeys.lists() });
      queryClient.invalidateQueries({ queryKey: agentDefinitionsKeys.stats() });

      // Update cached detail
      queryClient.setQueryData(agentDefinitionsKeys.detail(data.id), data);

      toast.success(`Agent "${data.name}" updated successfully`);
    },
    onError: (error) => {
      toast.error(`Failed to update agent: ${error.message}`);
    },
  });
}

/**
 * Delete agent definition mutation
 */
export function useDeleteAgent(): UseMutationResult<void, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: agentClient.deleteAgentDefinition,
    onSuccess: (_, deletedId) => {
      // Invalidate lists and stats
      queryClient.invalidateQueries({ queryKey: agentDefinitionsKeys.lists() });
      queryClient.invalidateQueries({ queryKey: agentDefinitionsKeys.stats() });

      // Remove from detail cache
      queryClient.removeQueries({ queryKey: agentDefinitionsKeys.detail(deletedId) });

      toast.success('Agent deleted successfully');
    },
    onError: (error) => {
      toast.error(`Failed to delete agent: ${error.message}`);
    },
  });
}
