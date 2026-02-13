/**
 * Agent Section Component - Issue #4230
 *
 * Main orchestration component for AI agent management in SharedGame detail page.
 * Handles agent linking, unlinking, and creation workflows.
 */

'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Link2 } from 'lucide-react';
import { toast } from 'sonner';

import { AgentBuilderModal } from '@/components/admin/shared-games/AgentBuilderModal';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/navigation/command';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/overlays/popover';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';

import { LinkedAgentCard } from './LinkedAgentCard';

interface AgentSectionProps {
  /**
   * SharedGame ID
   */
  gameId: string;

  /**
   * SharedGame title (for context in modal)
   */
  gameTitle: string;

  /**
   * Optional game description (for context in modal)
   */
  gameDescription?: string;
}

/**
 * AgentSection component
 *
 * Main component for AI agent management in SharedGame detail page.
 *
 * Features:
 * - Displays linked agent (if exists) with LinkedAgentCard
 * - "Create Agent" button to open AgentBuilderModal
 * - "Link Existing Agent" dropdown with agent search
 * - Unlink functionality with confirmation
 *
 * Workflow:
 * 1. Fetches linked agent on mount
 * 2. If no agent: shows "Create Agent" and "Link Existing" options
 * 3. If agent linked: shows LinkedAgentCard with unlink button
 *
 * @example
 * ```tsx
 * <AgentSection
 *   gameId={game.id}
 *   gameTitle={game.title}
 *   gameDescription={game.description}
 * />
 * ```
 */
export function AgentSection({ gameId, gameTitle, gameDescription }: AgentSectionProps) {
  const queryClient = useQueryClient();
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isLinkPopoverOpen, setIsLinkPopoverOpen] = useState(false);

  // Fetch linked agent
  const {
    data: linkedAgent,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['admin', 'shared-games', gameId, 'linked-agent'],
    queryFn: () => api.sharedGames.getLinkedAgent(gameId),
  });

  // Fetch available agents (for link existing dropdown)
  const { data: availableAgents } = useQuery({
    queryKey: ['admin', 'agent-definitions', 'active', linkedAgent === null],
    queryFn: () => agentDefinitionsApi.getAll({ activeOnly: true }),
    enabled: linkedAgent === null, // Explicitly check null to trigger refetch after unlink
  });

  // Link existing agent mutation
  const linkAgentMutation = useMutation({
    mutationFn: (agentId: string) => api.sharedGames.linkAgent(gameId, agentId),
    onSuccess: () => {
      toast.success('Agent linked successfully');
      queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games', gameId, 'linked-agent'] });
      setIsLinkPopoverOpen(false);
    },
    onError: (error: Error) => {
      toast.error(`Failed to link agent: ${error.message}`);
    },
  });

  // Unlink agent mutation
  const unlinkAgentMutation = useMutation({
    mutationFn: () => api.sharedGames.unlinkAgent(gameId),
    onSuccess: () => {
      toast.success('Agent unlinked successfully');
      queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games', gameId, 'linked-agent'] });
    },
    onError: (error: Error) => {
      toast.error(`Failed to unlink agent: ${error.message}`);
    },
  });

  // Loading state
  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>AI Agent</CardTitle>
          <CardDescription>Loading...</CardDescription>
        </CardHeader>
        <CardContent>
          <Skeleton className="h-40 w-full" />
        </CardContent>
      </Card>
    );
  }

  // Error state
  if (error) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>AI Agent</CardTitle>
          <CardDescription>Error loading agent information</CardDescription>
        </CardHeader>
        <CardContent>
          <Alert variant="destructive">
            <AlertDescription>
              {error instanceof Error ? error.message : 'Failed to load linked agent'}
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    );
  }

  // Agent is linked: show LinkedAgentCard
  if (linkedAgent) {
    return (
      <div className="space-y-4">
        <LinkedAgentCard
          agent={linkedAgent}
          onUnlink={() => unlinkAgentMutation.mutate()}
          isUnlinking={unlinkAgentMutation.isPending}
        />
      </div>
    );
  }

  // No agent linked: show create/link options
  return (
    <Card>
      <CardHeader>
        <CardTitle>AI Agent</CardTitle>
        <CardDescription>
          Create or link an AI agent to assist with this game.
          The agent will have access to the game&apos;s knowledge base and documents.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex gap-2">
          {/* Create new agent button */}
          <Button onClick={() => setIsCreateModalOpen(true)} className="flex-1">
            <Plus className="mr-2 h-4 w-4" />
            Create Agent
          </Button>

          {/* Link existing agent dropdown */}
          <Popover open={isLinkPopoverOpen} onOpenChange={setIsLinkPopoverOpen}>
            <PopoverTrigger asChild>
              <Button variant="outline" className="flex-1">
                <Link2 className="mr-2 h-4 w-4" />
                Link Existing Agent
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[300px] p-0" align="start">
              <Command>
                <CommandInput placeholder="Search agents..." />
                <CommandList>
                  <CommandEmpty>No agents found.</CommandEmpty>
                  <CommandGroup heading="Available Agents">
                    {availableAgents?.map((agent) => (
                      <CommandItem
                        key={agent.id}
                        value={agent.name}
                        onSelect={() => linkAgentMutation.mutate(agent.id)}
                      >
                        <div className="flex flex-col">
                          <span className="font-medium">{agent.name}</span>
                          <span className="text-xs text-muted-foreground">
                            {agent.config.model}
                          </span>
                        </div>
                      </CommandItem>
                    ))}
                  </CommandGroup>
                </CommandList>
              </Command>
            </PopoverContent>
          </Popover>
        </div>
      </CardContent>

      {/* Agent Builder Modal */}
      <AgentBuilderModal
        open={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        sharedGameContext={{
          gameId,
          gameTitle,
          gameDescription,
        }}
      />
    </Card>
  );
}
