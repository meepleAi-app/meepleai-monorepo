/**
 * ChatHistory - List of chat sessions
 *
 * Displays all chat sessions for the selected game.
 * Shows loading skeletons, empty state, and manages chat selection/deletion.
 */

import React from 'react';
import { useChatContext } from './ChatProvider';
import { ChatHistoryItem } from './ChatHistoryItem';
import { SkeletonLoader } from '../loading/SkeletonLoader';

export function ChatHistory() {
  const { chats, activeChatId, selectChat, deleteChat, loading } = useChatContext();

  const handleSelectChat = (chatId: string) => {
    void selectChat(chatId);
  };

  const handleDeleteChat = (chatId: string) => {
    if (!confirm('Sei sicuro di voler eliminare questa chat?')) {
      return;
    }
    void deleteChat(chatId);
  };

  if (loading.chats) {
    return (
      <nav aria-label="Chat history" style={{ flex: 1, overflowY: 'auto', padding: 8 }}>
        <div role="status" aria-live="polite" style={{ padding: 16, textAlign: 'center' }}>
          <div style={{ marginBottom: 12, fontSize: 13, color: '#64748b' }}>Caricamento chat...</div>
          <SkeletonLoader variant="chatHistory" count={5} ariaLabel="Caricamento cronologia chat" />
        </div>
      </nav>
    );
  }

  if (chats.length === 0) {
    return (
      <nav aria-label="Chat history" style={{ flex: 1, overflowY: 'auto', padding: 8 }}>
        <div style={{ padding: 16, textAlign: 'center', color: '#64748b', fontSize: 13 }}>
          Nessuna chat. Creane una nuova!
        </div>
      </nav>
    );
  }

  return (
    <nav aria-label="Chat history" style={{ flex: 1, overflowY: 'auto', padding: 8 }}>
      <ul role="list" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
        {chats.map((chat) => (
          <ChatHistoryItem
            key={chat.id}
            chat={chat}
            isActive={activeChatId === chat.id}
            onSelect={() => handleSelectChat(chat.id)}
            onDelete={() => handleDeleteChat(chat.id)}
          />
        ))}
      </ul>
    </nav>
  );
}
