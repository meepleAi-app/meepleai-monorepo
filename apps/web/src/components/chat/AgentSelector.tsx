/**
 * AgentSelector - Dropdown for selecting AI agent
 *
 * Migrated to Zustand (Issue #1083):
 * - Direct store access with granular subscriptions
 * - Only re-renders when agents or selectedAgentId changes
 */

import React from 'react';
import { useChatStoreWithSelectors } from '@/store/chat';
import { SkeletonLoader } from '../loading/SkeletonLoader';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

export function AgentSelector() {
  const agents = useChatStoreWithSelectors.use.agents();
  const selectedAgentId = useChatStoreWithSelectors.use.selectedAgentId();
  const selectAgent = useChatStoreWithSelectors.use.selectAgent();
  const selectedGameId = useChatStoreWithSelectors.use.selectedGameId();
  const loading = useChatStoreWithSelectors.use.loading();

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
