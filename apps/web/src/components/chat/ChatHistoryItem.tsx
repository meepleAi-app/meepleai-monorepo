/**
 * ChatHistoryItem - Individual chat item in the history list
 *
 * Displays a chat session with agent name, timestamp, and delete button.
 * Highlights active chat and supports keyboard navigation.
 */

import React from 'react';
import { Chat } from '@/types';

interface ChatHistoryItemProps {
  chat: Chat;
  isActive: boolean;
  onSelect: () => void;
  onDelete: () => void;
}

function formatChatPreview(chat: Chat): string {
  const date = new Date(chat.lastMessageAt ?? chat.startedAt);
  return `${chat.agentName} - ${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
}

export function ChatHistoryItem({ chat, isActive, onSelect, onDelete }: ChatHistoryItemProps) {
  return (
    <li
      style={{
        padding: 12,
        marginBottom: 8,
        background: isActive ? '#e8f0fe' : 'white',
        border: `1px solid ${isActive ? '#1a73e8' : '#dadce0'}`,
        borderRadius: 4,
        cursor: 'pointer',
        position: 'relative',
        fontSize: 13
      }}
      onClick={onSelect}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onSelect();
        }
      }}
      aria-current={isActive ? 'true' : undefined}
    >
      <div style={{ fontWeight: 500, marginBottom: 4 }}>{chat.agentName}</div>
      <div style={{ fontSize: 11, color: '#64748b' }}>{formatChatPreview(chat)}</div>
      <button
        onClick={(e) => {
          e.stopPropagation();
          onDelete();
        }}
        aria-label={`Delete chat with ${chat.agentName}`}
        style={{
          position: 'absolute',
          top: 8,
          right: 8,
          padding: '4px 8px',
          background: '#ea4335',
          color: 'white',
          border: 'none',
          borderRadius: 3,
          fontSize: 11,
          cursor: 'pointer'
        }}
        title="Elimina chat"
      >
        🗑️
      </button>
    </li>
  );
}
