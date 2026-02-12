/**
 * Agent Builder Modal - Issue #4230
 *
 * Dialog wrapper for AgentBuilderForm with SharedGame context pre-population.
 * Handles agent creation and auto-linking to the SharedGame.
 */

'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

import { AgentBuilderForm } from '@/components/admin/agent-definitions/AgentBuilderForm';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';
import { api } from '@/lib/api';
import type { AgentDefinitionDto, CreateAgentDefinition } from '@/lib/api/schemas/agent-definitions.schemas';

interface SharedGameContext {
  /**
   * SharedGame ID for auto-linking
   */
  gameId: string;

  /**
   * SharedGame title (used for suggested agent name)
   */
  gameTitle: string;

  /**
   * SharedGame description (optional context for agent)
   */
  gameDescription?: string;
}

interface AgentBuilderModalProps {
  /**
   * Whether the modal is open
   */
  open: boolean;

  /**
   * Callback when modal should close
   */
  onClose: () => void;

  /**
   * SharedGame context for pre-populating form
   */
  sharedGameContext: SharedGameContext;

  /**
   * Callback when agent is successfully created and linked
   */
  onSuccess?: (agent: AgentDefinitionDto) => void;
}

/**
 * AgentBuilderModal component
 *
 * Wraps AgentBuilderForm in a dialog with auto-linking functionality.
 * Pre-populates form with SharedGame context:
 * - Suggested name: "{GameTitle} Arbitro"
 * - Description mentions the game title
 *
 * On submit:
 * 1. Creates agent via API
 * 2. Auto-links agent to SharedGame
 * 3. Shows success toast
 * 4. Closes modal
 * 5. Invalidates queries to refetch linked agent
 *
 * @example
 * ```tsx
 * <AgentBuilderModal
 *   open={isOpen}
 *   onClose={() => setIsOpen(false)}
 *   sharedGameContext={{
 *     gameId: game.id,
 *     gameTitle: game.title,
 *     gameDescription: game.description
 *   }}
 *   onSuccess={() => console.log('Agent linked!')}
 * />
 * ```
 */
export function AgentBuilderModal({
  open,
  onClose,
  sharedGameContext,
  onSuccess,
}: AgentBuilderModalProps) {
  const queryClient = useQueryClient();

  // Mutation for creating agent
  const createAgentMutation = useMutation({
    mutationFn: async (data: CreateAgentDefinition) => {
      // Step 1: Create agent
      const createdAgent = await agentDefinitionsApi.create(data);

      // Step 2: Link agent to SharedGame
      await api.sharedGames.linkAgent(sharedGameContext.gameId, createdAgent.id);

      return createdAgent;
    },
    onSuccess: (agent) => {
      toast.success(`Agent "${agent.name}" created and linked to ${sharedGameContext.gameTitle}`);

      // Invalidate queries to refetch linked agent
      queryClient.invalidateQueries({
        queryKey: ['admin', 'shared-games', sharedGameContext.gameId, 'linked-agent'],
      });

      onSuccess?.(agent);
      onClose();
    },
    onError: (error: Error) => {
      toast.error(`Failed to create agent: ${error.message}`);
    },
  });

  // Generate default values with SharedGame context
  const defaultValues: Partial<CreateAgentDefinition> = {
    name: `${sharedGameContext.gameTitle} Arbitro`,
    description: `AI assistant for ${sharedGameContext.gameTitle}${
      sharedGameContext.gameDescription ? `: ${sharedGameContext.gameDescription.slice(0, 100)}` : ''
    }`,
    model: 'gpt-4',
    maxTokens: 2048,
    temperature: 0.7,
    prompts: [],
    tools: [],
  };

  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Create AI Agent for {sharedGameContext.gameTitle}</DialogTitle>
          <DialogDescription>
            Configure a new AI agent that will be linked to this game.
            The agent will have access to the game&apos;s knowledge base and documents.
          </DialogDescription>
        </DialogHeader>

        <div className="mt-4">
          <AgentBuilderForm
            defaultValues={defaultValues}
            onSubmit={(data) => createAgentMutation.mutate(data)}
            isLoading={createAgentMutation.isPending}
          />
        </div>
      </DialogContent>
    </Dialog>
  );
}
