/**
 * ISSUE-3777: useOrchestrator Hook
 * React Query hook for multi-agent orchestration workflow
 */

'use client';

import { useMutation, useQuery, type UseMutationResult, type UseQueryResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import * as orchestratorClient from '@/lib/api/orchestrator-client';
import type {
  ExecuteWorkflowRequest,
  ExecuteWorkflowResponse,
  AgentType,
} from '@/lib/api/orchestrator-client';

// Query Keys
const orchestratorKeys = {
  all: ['orchestrator'] as const,
  health: () => [...orchestratorKeys.all, 'health'] as const,
  execute: (sessionId: string) => [...orchestratorKeys.all, 'execute', sessionId] as const,
};

/**
 * Check orchestrator service health status
 */
export function useOrchestratorHealth(): UseQueryResult<
  { status: 'healthy' | 'unhealthy'; version: string },
  Error
> {
  return useQuery({
    queryKey: orchestratorKeys.health(),
    queryFn: orchestratorClient.checkOrchestratorHealth,
    staleTime: 60 * 1000, // 1 minute
    retry: 1,
  });
}

/**
 * Execute orchestration workflow (query agent)
 */
export function useExecuteWorkflow(): UseMutationResult<
  ExecuteWorkflowResponse,
  Error,
  ExecuteWorkflowRequest
> {
  return useMutation({
    mutationFn: orchestratorClient.executeWorkflow,
    onSuccess: (data) => {
      // Show which agent responded
      const agentName = data.agent_type.charAt(0).toUpperCase() + data.agent_type.slice(1);
      toast.success(`${agentName} agent responded (${data.execution_time_ms.toFixed(0)}ms)`, {
        duration: 2000,
      });
    },
    onError: (error) => {
      toast.error(`Agent execution failed: ${error.message}`);
    },
  });
}

/**
 * Request explicit agent switch
 */
export function useSwitchAgent(): UseMutationResult<
  ExecuteWorkflowResponse,
  Error,
  { sessionId: string; gameId: string; targetAgent: AgentType }
> {
  return useMutation({
    mutationFn: ({ sessionId, gameId, targetAgent }) =>
      orchestratorClient.requestAgentSwitch(sessionId, gameId, targetAgent),
    onSuccess: (data) => {
      const agentName = data.agent_type.charAt(0).toUpperCase() + data.agent_type.slice(1);
      toast.success(`Switched to ${agentName} agent`, {
        icon: getAgentIcon(data.agent_type),
      });
    },
    onError: (error) => {
      toast.error(`Agent switch failed: ${error.message}`);
    },
  });
}

/**
 * Get agent icon for notifications
 */
function getAgentIcon(agentType: AgentType): string {
  const icons: Record<AgentType, string> = {
    tutor: '📚',
    arbitro: '⚖️',
    decisore: '♟️',
  };
  return icons[agentType];
}
