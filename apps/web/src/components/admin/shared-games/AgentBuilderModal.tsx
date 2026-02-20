/**
 * Agent Builder Modal - Issue #4230, #4928
 *
 * Dialog wrapper for AgentBuilderForm with SharedGame context pre-population.
 * Handles agent creation and auto-linking to the SharedGame.
 * Issue #4928: Added KB card selection step.
 */

'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BookOpen, Loader2 } from 'lucide-react';
import { toast } from 'sonner';

import { AgentBuilderForm } from '@/components/admin/agent-definitions/AgentBuilderForm';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { api } from '@/lib/api';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';
import type { AgentDefinitionDto, CreateAgentDefinition, KbCardDto } from '@/lib/api/schemas/agent-definitions.schemas';

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
 * 1. Creates agent via API (with selected KB card IDs)
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
  const [selectedKbCardIds, setSelectedKbCardIds] = useState<string[]>([]);

  // Fetch completed KB cards for this game — only when modal is open
  const { data: kbCards, isLoading: isLoadingKbCards } = useQuery<KbCardDto[]>({
    queryKey: ['admin', 'shared-games', sharedGameContext.gameId, 'kb-cards', 'completed'],
    queryFn: () => api.sharedGames.getKbCards(sharedGameContext.gameId, 'completed'),
    enabled: open,
  });

  // Mutation for creating agent
  const createAgentMutation = useMutation({
    mutationFn: async (data: CreateAgentDefinition) => {
      // Step 1: Create agent with selected KB card IDs
      const createdAgent = await agentDefinitionsApi.create({
        ...data,
        kbCardIds: selectedKbCardIds,
      });

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

  const handleKbCardToggle = (cardId: string, checked: boolean) => {
    setSelectedKbCardIds((prev) =>
      checked ? [...prev, cardId] : prev.filter((id) => id !== cardId)
    );
  };

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
    kbCardIds: [],
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

        {/* KB Cards Selection - Issue #4928 */}
        <div className="space-y-3 border rounded-lg p-4 bg-muted/20">
          <div className="flex items-center gap-2">
            <BookOpen className="h-5 w-5 text-muted-foreground" />
            <h3 className="font-medium text-sm">Knowledge Base Documents</h3>
          </div>

          {isLoadingKbCards ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading documents...
            </div>
          ) : kbCards && kbCards.length > 0 ? (
            <div className="space-y-2">
              <p className="text-xs text-muted-foreground">
                Select which indexed documents this agent can retrieve from. Leave empty to use all
                available documents.
              </p>
              {kbCards.map((card) => (
                <div
                  key={card.id}
                  className="flex items-start gap-3 p-2 rounded hover:bg-muted/50 cursor-pointer"
                  onClick={() =>
                    handleKbCardToggle(card.id, !selectedKbCardIds.includes(card.id))
                  }
                >
                  <input
                    type="checkbox"
                    id={`kb-card-${card.id}`}
                    checked={selectedKbCardIds.includes(card.id)}
                    onChange={(e) => handleKbCardToggle(card.id, e.target.checked)}
                    onClick={(e) => e.stopPropagation()}
                    className="mt-0.5 h-4 w-4 shrink-0"
                  />
                  <label
                    htmlFor={`kb-card-${card.id}`}
                    className="flex-1 text-sm cursor-pointer"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <span className="font-medium">{card.fileName}</span>
                    {card.documentType && (
                      <span className="ml-2 text-xs text-muted-foreground capitalize">
                        ({card.documentType})
                      </span>
                    )}
                    {card.version && (
                      <span className="ml-1 text-xs text-muted-foreground">v{card.version}</span>
                    )}
                    <span className="ml-2 text-xs text-muted-foreground">
                      {card.chunkCount} chunk{card.chunkCount !== 1 ? 's' : ''}
                    </span>
                  </label>
                </div>
              ))}
              {selectedKbCardIds.length === 0 && (
                <p className="text-xs text-amber-600 mt-1">
                  No documents selected — the agent will use all available documents for this game.
                </p>
              )}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              No indexed documents available. Upload and index documents first to enable targeted
              knowledge retrieval.
            </p>
          )}
        </div>

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
