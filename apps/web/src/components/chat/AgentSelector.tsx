/**
 * AgentSelector - Dropdown for selecting AI agent
 *
 * Allows users to select which AI agent to chat with for the selected game.
 * Integrates with ChatProvider for state management.
 */

import React from 'react';
import { useChatContext } from './ChatProvider';
import { SkeletonLoader } from '../loading/SkeletonLoader';

export function AgentSelector() {
  const { agents, selectedAgentId, selectAgent, selectedGameId, loading } = useChatContext();

  if (loading.agents) {
    return (
      <div style={{ marginBottom: 12 }}>
        <label style={{ display: 'block', marginBottom: 6, fontWeight: 500, fontSize: 13 }}>
          Seleziona Agente:
        </label>
        <SkeletonLoader variant="gameSelection" />
      </div>
    );
  }

  return (
    <div style={{ marginBottom: 12 }}>
      <label
        htmlFor="agentSelect"
        style={{ display: 'block', marginBottom: 6, fontWeight: 500, fontSize: 13 }}
      >
        Seleziona Agente:
      </label>
      <select
        id="agentSelect"
        value={selectedAgentId ?? ''}
        onChange={(e) => {
          const value = e.target.value;
          selectAgent(value || null);
        }}
        disabled={loading.agents || !selectedGameId}
        aria-busy={loading.agents}
        title={!selectedGameId ? 'Seleziona prima un gioco' : undefined}
        style={{
          width: '100%',
          padding: 8,
          fontSize: 13,
          borderRadius: 4,
          border: '1px solid #dadce0',
          cursor: loading.agents || !selectedGameId ? 'not-allowed' : 'pointer',
          opacity: !selectedGameId ? 0.6 : 1
        }}
      >
        {!selectedGameId ? (
          <option value="">Seleziona prima un gioco</option>
        ) : agents.length === 0 ? (
          <option value="">Nessun agente disponibile</option>
        ) : (
          <>
            <option value="">Seleziona un agente</option>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name}
              </option>
            ))}
          </>
        )}
      </select>
    </div>
  );
}
