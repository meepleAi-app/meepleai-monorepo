/* eslint-disable security/detect-object-injection -- Safe Zustand store key access */
/**
 * ChatHistory - List of chat threads (Issue #858)
 *
 * Displays all chat threads for the selected game.
 * Shows loading skeletons, empty state, and manages thread selection/deletion.
 * Updated to use ThreadListItem for thread display.
 * Issue #2258: Refactored to use shared useThreadDeletion hook.
 */

import React from 'react';

import { useThreadDeletion } from '@/hooks/useThreadDeletion';
import { useChatStore } from '@/store/chat/store';
import { ChatThread } from '@/types';

import { ThreadListItem } from './ThreadListItem';
import { SkeletonLoader } from '../loading/SkeletonLoader';

export function ChatHistory() {
  // Thread deletion hook (Issue #2258)
  const { handleThreadDelete, ConfirmDialogComponent } = useThreadDeletion();

  // Issue #1676: Migrated from useChatContext to direct Zustand store
  const { selectedGameId, chatsByGame, activeChatIds, selectChat, loading } = useChatStore(
    state => ({
      selectedGameId: state.selectedGameId,
      chatsByGame: state.chatsByGame,
      activeChatIds: state.activeChatIds,
      selectChat: state.selectChat,
      loading: state.loading,
    })
  );

  // Derived values
  const activeChatId = selectedGameId ? activeChatIds[selectedGameId] : null;
  const chats = selectedGameId ? (chatsByGame[selectedGameId] ?? []) : [];

  const handleSelectChat = (chatId: string) => {
    void selectChat(chatId);
  };

  const handleDeleteChat = (chatId: string) => {
    void handleThreadDelete(chatId);
  };

  if (loading.chats) {
    return (
      <nav aria-label="Thread history" className="flex-1 overflow-y-auto p-2">
        <div role="status" aria-live="polite" className="p-4 text-center">
          <div className="mb-3 text-[13px] text-[#64748b]">Caricamento thread...</div>
          <SkeletonLoader
            variant="chatHistory"
            count={5}
            ariaLabel="Caricamento cronologia thread"
          />
        </div>
      </nav>
    );
  }

  if (chats.length === 0) {
    return (
      <nav aria-label="Thread history" className="flex-1 overflow-y-auto p-2">
        <div className="p-4 text-center text-[#64748b] text-[13px]">
          Nessun thread. Invia un messaggio per iniziare!
        </div>
      </nav>
    );
  }

  // Separate active and archived threads (Issue #858)
  const activeThreads = chats.filter((thread: ChatThread) => thread.status !== 'Closed');
  const archivedThreads = chats.filter((thread: ChatThread) => thread.status === 'Closed');

  return (
    <nav aria-label="Thread history" className="flex-1 overflow-y-auto p-2">
      {/* Active Threads Section */}
      {activeThreads.length > 0 && (
        <div className="mb-4">
          <h3 className="px-3 py-1 text-[11px] font-semibold text-[#5f6368] uppercase tracking-wide">
            Active Threads
          </h3>
          <ul role="list" className="list-none m-0 p-0">
            {activeThreads.map((thread: ChatThread) => (
              <ThreadListItem
                key={thread.id}
                thread={thread}
                isActive={activeChatId === thread.id}
                onSelect={() => handleSelectChat(thread.id)}
                onDelete={() => handleDeleteChat(thread.id)}
              />
            ))}
          </ul>
        </div>
      )}

      {/* Archived Threads Section */}
      {archivedThreads.length > 0 && (
        <div>
          <h3 className="px-3 py-1 text-[11px] font-semibold text-[#5f6368] uppercase tracking-wide">
            Archived
          </h3>
          <ul role="list" className="list-none m-0 p-0">
            {archivedThreads.map((thread: ChatThread) => (
              <ThreadListItem
                key={thread.id}
                thread={thread}
                isActive={activeChatId === thread.id}
                onSelect={() => handleSelectChat(thread.id)}
                onDelete={() => handleDeleteChat(thread.id)}
              />
            ))}
          </ul>
        </div>
      )}

      {/* Confirmation dialog for thread deletion (Issue #2258) */}
      <ConfirmDialogComponent />
    </nav>
  );
}
