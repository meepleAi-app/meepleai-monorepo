/**
 * React Query hooks for AI models and agent configuration.
 * Uses GET /api/v1/models and GET/PATCH /api/v1/agents/:id/configuration.
 */

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  BackendModelDto,
  BackendAgentConfigurationDto,
  UpdateAgentConfigurationRequest,
} from '@/lib/api/schemas/agent-config.schemas';

/** Fetch available AI models, optionally filtered by user tier */
export function useAvailableModels(tier?: string) {
  return useQuery<BackendModelDto[]>({
    queryKey: ['ai-models', tier],
    queryFn: () => api.agents.getModels(tier),
    staleTime: 10 * 60 * 1000, // Models rarely change — cache 10min
  });
}

/** Fetch current LLM configuration for an agent */
export function useAgentConfiguration(agentId: string | null) {
  return useQuery<BackendAgentConfigurationDto>({
    queryKey: ['agent-configuration', agentId],
    queryFn: () => api.agents.getAgentConfiguration(agentId!),
    enabled: !!agentId,
  });
}

/** Mutate agent LLM configuration via PATCH */
export function useUpdateAgentConfiguration(agentId: string | null) {
  const queryClient = useQueryClient();

  return useMutation<BackendAgentConfigurationDto, Error, UpdateAgentConfigurationRequest>({
    mutationFn: (config) => api.agents.updateAgentConfiguration(agentId!, config),
    onSuccess: () => {
      if (agentId) {
        queryClient.invalidateQueries({ queryKey: ['agent-configuration', agentId] });
      }
    },
  });
}
