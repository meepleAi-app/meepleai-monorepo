/**
 * useCreateAgentFlow - React Query mutation hook for orchestrated agent creation
 * Issue #4772: Agent Creation Orchestration Flow
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { api } from '@/lib/api';
import { agentSlotsKeys } from './useAgentSlots';

/** Centralized toast messages for agent creation flow — import in tests to avoid magic strings */
export const AGENT_FLOW_MESSAGES = {
  success: (agentName: string) => `Agente "${agentName}" creato! Avvio chat...`,
  slotLimit: { title: 'Nessuno slot disponibile', description: "Effettua l'upgrade per avere più slot agente." },
  nameConflict: { title: 'Nome agente già in uso', description: 'Scegli un nome diverso.' },
  genericError: (description: string) => ({ title: 'Creazione agente fallita', description }),
  fallbackErrorDesc: "Errore nella creazione dell'agente",
} as const;

export type CreateAgentFlowInput = {
  gameId: string;
  addToCollection: boolean;
  agentType: string;
  agentName?: string;
  strategyName?: string;
  strategyParameters?: Record<string, unknown>;
};

export type CreateAgentFlowResult = {
  agentId: string;
  agentName: string;
  threadId: string;
  slotUsed: number;
  gameAddedToCollection: boolean;
};

export function useCreateAgentFlow(options?: {
  onSuccess?: (result: CreateAgentFlowResult) => void;
  onError?: (error: Error) => void;
}) {
  const queryClient = useQueryClient();

  return useMutation<CreateAgentFlowResult, Error, CreateAgentFlowInput>({
    mutationFn: (input) => api.agents.createWithSetup(input),

    onSuccess: (result) => {
      // Invalidate agent slots cache (slot count changed)
      queryClient.invalidateQueries({ queryKey: agentSlotsKeys.all });
      // Invalidate agents list
      queryClient.invalidateQueries({ queryKey: ['agents'] });

      if (result.gameAddedToCollection) {
        queryClient.invalidateQueries({ queryKey: ['user-library'] });
      }

      toast.success(AGENT_FLOW_MESSAGES.success(result.agentName));
      options?.onSuccess?.(result);
    },

    onError: (error) => {
      const message = error.message || AGENT_FLOW_MESSAGES.fallbackErrorDesc;

      if (message.includes('Agent limit reached')) {
        toast.error(AGENT_FLOW_MESSAGES.slotLimit.title, {
          description: AGENT_FLOW_MESSAGES.slotLimit.description,
        });
      } else if (message.includes('unique name')) {
        toast.error(AGENT_FLOW_MESSAGES.nameConflict.title, {
          description: AGENT_FLOW_MESSAGES.nameConflict.description,
        });
      } else {
        toast.error(AGENT_FLOW_MESSAGES.genericError(message).title, {
          description: AGENT_FLOW_MESSAGES.genericError(message).description,
        });
      }

      options?.onError?.(error);
    },
  });
}
