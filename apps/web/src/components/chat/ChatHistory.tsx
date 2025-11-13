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
      <nav aria-label="Chat history" className="flex-1 overflow-y-auto p-2">
        <div role="status" aria-live="polite" className="p-4 text-center">
          <div className="mb-3 text-[13px] text-[#64748b]">Caricamento chat...</div>
          <SkeletonLoader variant="chatHistory" count={5} ariaLabel="Caricamento cronologia chat" />
        </div>
      </nav>
    );
  }

  if (chats.length === 0) {
    return (
      <nav aria-label="Chat history" className="flex-1 overflow-y-auto p-2">
        <div className="p-4 text-center text-[#64748b] text-[13px]">
          Nessuna chat. Creane una nuova!
        </div>
      </nav>
    );
  }

  return (
    <nav aria-label="Chat history" className="flex-1 overflow-y-auto p-2">
      <ul role="list" className="list-none m-0 p-0">
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
