/**
 * AgentSelector - Dropdown for selecting AI agent
 *
 * Allows users to select which AI agent to chat with for the selected game.
 * Integrates with ChatProvider for state management.
 */

import React from 'react';
import { useChatContext } from './ChatProvider';
import { SkeletonLoader } from '../loading/SkeletonLoader';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

export function AgentSelector() {
  const { agents, selectedAgentId, selectAgent, selectedGameId, loading } = useChatContext();

  if (loading.agents) {
    return (
      <div className="mb-3">
        <label className="block mb-1.5 font-medium text-sm">
          Seleziona Agente:
        </label>
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
      <label className="block mb-1.5 font-medium text-sm">
        Seleziona Agente:
      </label>
      <Select
        value={selectedAgentId ?? ''}
        onValueChange={(value) => selectAgent(value || null)}
        disabled={isDisabled}
      >
        <SelectTrigger
          className="w-full"
          aria-busy={loading.agents}
          title={!selectedGameId ? 'Seleziona prima un gioco' : undefined}
        >
          <SelectValue placeholder={placeholderText} />
        </SelectTrigger>
        <SelectContent>
          {selectedGameId && hasAgents && agents.map((agent) => (
            <SelectItem key={agent.id} value={agent.id}>
              {agent.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
