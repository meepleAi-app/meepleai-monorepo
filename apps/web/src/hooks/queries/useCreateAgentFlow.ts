/**
 * useCreateAgentFlow - React Query mutation hook for orchestrated agent creation
 * Issue #4772: Agent Creation Orchestration Flow
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { api } from '@/lib/api';
import { agentSlotsKeys } from './useAgentSlots';

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

      toast.success(`Agente "${result.agentName}" creato! Avvio chat...`);
      options?.onSuccess?.(result);
    },

    onError: (error) => {
      const message = error.message || 'Errore nella creazione dell\'agente';

      if (message.includes('Agent limit reached')) {
        toast.error('Nessuno slot disponibile', {
          description: 'Effettua l\'upgrade per avere più slot agente.',
        });
      } else if (message.includes('unique name')) {
        toast.error('Nome agente già in uso', {
          description: 'Scegli un nome diverso.',
        });
      } else {
        toast.error('Creazione agente fallita', {
          description: message,
        });
      }

      options?.onError?.(error);
    },
  });
}
