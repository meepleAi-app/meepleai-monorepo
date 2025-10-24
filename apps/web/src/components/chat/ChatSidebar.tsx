/**
 * ChatSidebar - Collapsible sidebar with game/agent selection and chat history
 *
 * Composes GameSelector, AgentSelector, ChatHistory, and new chat button.
 * Manages sidebar collapse state and game context badge.
 */

import React from 'react';
import { useChatContext } from './ChatProvider';
import { GameSelector } from './GameSelector';
import { AgentSelector } from './AgentSelector';
import { ChatHistory } from './ChatHistory';
import { LoadingButton } from '../loading/LoadingButton';

export function ChatSidebar() {
  const {
    games,
    selectedGameId,
    selectedAgentId,
    sidebarCollapsed,
    loading,
    createChat
  } = useChatContext();

  const handleCreateChat = () => {
    void createChat();
  };

  return (
    <aside
      aria-label="Chat sidebar with game selection and chat history"
      style={{
        width: sidebarCollapsed ? 0 : 320,
        minWidth: sidebarCollapsed ? 0 : 320,
        background: '#f8f9fa',
        borderRight: '1px solid #dadce0',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        transition: 'width 0.3s ease, min-width 0.3s ease'
      }}
    >
      {/* Sidebar Header */}
      <div style={{ padding: 16, borderBottom: '1px solid #dadce0' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <h2 style={{ margin: 0, fontSize: 18 }}>MeepleAI Chat</h2>
          {/* Game context badge */}
          {selectedGameId && (
            <div
              style={{
                padding: '4px 12px',
                background: '#e8f0fe',
                color: '#1a73e8',
                borderRadius: 12,
                fontSize: 11,
                fontWeight: 600,
                border: '1px solid #1a73e8'
              }}
              title={`Currently chatting about: ${games.find(g => g.id === selectedGameId)?.name ?? 'Unknown game'}`}
              aria-label={`Active game context: ${games.find(g => g.id === selectedGameId)?.name ?? 'Unknown game'}`}
            >
              {games.find(g => g.id === selectedGameId)?.name ?? '...'}
            </div>
          )}
        </div>

        {/* Game Selector */}
        <GameSelector />

        {/* Agent Selector */}
        <AgentSelector />

        {/* New Chat Button */}
        <LoadingButton
          isLoading={loading.creating}
          loadingText="Creazione..."
          onClick={handleCreateChat}
          disabled={!selectedGameId || !selectedAgentId}
          spinnerSize="sm"
          aria-label="Create new chat"
          style={{
            width: '100%',
            padding: 10,
            background: !selectedGameId || !selectedAgentId || loading.creating ? '#dadce0' : '#1a73e8',
            color: 'white',
            border: 'none',
            borderRadius: 4,
            fontSize: 14,
            fontWeight: 500,
            cursor: !selectedGameId || !selectedAgentId || loading.creating ? 'not-allowed' : 'pointer'
          }}
        >
          + Nuova Chat
        </LoadingButton>
      </div>

      {/* Chat History */}
      <ChatHistory />
    </aside>
  );
}
