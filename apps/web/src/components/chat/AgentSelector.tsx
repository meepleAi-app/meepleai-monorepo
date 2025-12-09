/**
 * AgentSelector - Dropdown for selecting AI agent
 *
 * Allows users to select which AI agent to chat with for the selected game.
 * Integrates with ChatProvider for state management.
 */

import React from 'react';
import { useChatStore } from '@/store/chat/store';
import { SkeletonLoader } from '../loading/SkeletonLoader';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { AgentDto } from '@/lib/api/schemas/agents.schemas';

export function AgentSelector() {
  // Issue #1676: Migrated from useChatContext to direct Zustand store
  const { agents, selectedAgentId, selectAgent, selectedGameId, loading } = useChatStore(state => ({
    agents: state.agents,
    selectedAgentId: state.selectedAgentId,
    selectAgent: state.selectAgent,
    selectedGameId: state.selectedGameId,
    loading: state.loading,
  }));

  if (loading.agents) {
    return (
      <div className="mb-3">
        <label className="block mb-1.5 font-medium text-sm">Seleziona Agente:</label>
        <SkeletonLoader variant="gameSelection" />
      </div>
    );
  }

  const isDisabled = loading.agents || !selectedGameId;
  const hasAgents = agents.length > 0;

  const placeholderText = !selectedGameId
    ? 'Seleziona prima un gioco'
    : !hasAgents
      ? 'Nessun agente disponibile'
      : 'Seleziona un agente';

  return (
    <div className="mb-3">
      <label className="block mb-1.5 font-medium text-sm">Seleziona Agente:</label>
      <Select
        value={selectedAgentId ?? ''}
        onValueChange={value => selectAgent(value || null)}
        disabled={isDisabled}
      >
        <SelectTrigger
          className="w-full"
          data-testid="agent-selector"
          aria-busy={loading.agents}
          title={!selectedGameId ? 'Seleziona prima un gioco' : undefined}
        >
          <SelectValue placeholder={placeholderText} />
        </SelectTrigger>
        <SelectContent>
          {selectedGameId &&
            hasAgents &&
            agents.map((agent: AgentDto) => (
              <SelectItem key={agent.id} value={agent.id} data-testid={`agent-option-${agent.id}`}>
                {agent.name}
              </SelectItem>
            ))}
        </SelectContent>
      </Select>
    </div>
  );
}
