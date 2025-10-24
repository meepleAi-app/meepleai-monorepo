/**
 * ChatContent - Main chat content area
 *
 * Composes the message list and input form.
 * Displays chat header with game/agent info and sidebar toggle.
 *
 * Simplified version for Phase 3 - will be enhanced in Phase 4 with:
 * - Export chat button
 * - Streaming response display with stop button
 * - Typing indicator
 * - Full integration with useChatStreaming hook
 */

import React from 'react';
import Link from 'next/link';
import { useChatContext } from './ChatProvider';
import { MessageList } from './MessageList';
import { MessageInput } from './MessageInput';

export function ChatContent() {
  const {
    games,
    selectedGameId,
    activeChatId,
    chats,
    errorMessage,
    sidebarCollapsed,
    toggleSidebar
  } = useChatContext();

  const selectedGame = games.find(g => g.id === selectedGameId);
  const activeChat = chats.find(c => c.id === activeChatId);

  return (
    <div
      style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        background: '#ffffff'
      }}
    >
      {/* Header */}
      <div
        style={{
          padding: 16,
          borderBottom: '1px solid #dadce0',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          background: 'white'
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <button
            onClick={toggleSidebar}
            aria-label={sidebarCollapsed ? 'Show sidebar' : 'Hide sidebar'}
            aria-expanded={!sidebarCollapsed}
            style={{
              padding: '8px 12px',
              background: '#f1f3f4',
              border: 'none',
              borderRadius: 4,
              cursor: 'pointer',
              fontSize: 18
            }}
            title={sidebarCollapsed ? 'Mostra sidebar' : 'Nascondi sidebar'}
          >
            {sidebarCollapsed ? '☰' : '✕'}
          </button>
          <div>
            <h1 style={{ margin: 0, fontSize: 20 }}>
              {activeChatId
                ? activeChat?.agentName ?? 'Chat'
                : 'Seleziona o crea una chat'}
            </h1>
            <p style={{ margin: '4px 0 0 0', color: '#64748b', fontSize: 13 }}>
              {selectedGameId
                ? selectedGame?.name ?? ''
                : 'Nessun gioco selezionato'}
            </p>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <Link
            href="/"
            style={{
              padding: '8px 16px',
              background: '#1a73e8',
              color: 'white',
              textDecoration: 'none',
              borderRadius: 4,
              fontSize: 14
            }}
          >
            Home
          </Link>
        </div>
      </div>

      {/* Error Message */}
      {errorMessage && (
        <div
          role="alert"
          aria-live="polite"
          style={{
            margin: 16,
            padding: 12,
            background: '#fce8e6',
            color: '#d93025',
            borderRadius: 4,
            fontSize: 14
          }}
        >
          {errorMessage}
        </div>
      )}

      {/* Messages Area */}
      <MessageList />

      {/* Message Input */}
      <MessageInput />
    </div>
  );
}
