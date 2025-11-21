/**
 * ChatHistory - List of chat threads
 *
 * Migrated to Zustand (Issue #1083):
 * - Granular subscriptions (only chats, activeChatId, loading)
 * - Direct store access eliminates context nesting
 */

import React from 'react';
import { useChatStoreWithSelectors, useCurrentChats, useActiveChat } from '@/store/chat';
import { ThreadListItem } from './ThreadListItem';
import { SkeletonLoader } from '../loading/SkeletonLoader';
import { useConfirmDialog } from '@/hooks/useConfirmDialog';

export function ChatHistory() {
  const chats = useCurrentChats();
  const activeChat = useActiveChat();
  const selectChat = useChatStoreWithSelectors.use.selectChat();
  const deleteChat = useChatStoreWithSelectors.use.deleteChat();
  const loading = useChatStoreWithSelectors.use.loading();
  const { confirm, ConfirmDialogComponent } = useConfirmDialog();

  const activeChatId = activeChat?.id ?? null;

  const handleSelectChat = (chatId: string) => {
    void selectChat(chatId);
  };

  const handleDeleteChat = async (chatId: string) => {
    const confirmed = await confirm({
      title: "Elimina chat",
      message: "Sei sicuro di voler eliminare questa chat? Questa azione non può essere annullata.",
      variant: "destructive",
      confirmText: "Elimina",
      cancelText: "Annulla",
    });

    if (confirmed) {
      void deleteChat(chatId);
    }
  };

  if (loading.chats) {
    return (
      <nav aria-label="Thread history" className="flex-1 overflow-y-auto p-2">
        <div role="status" aria-live="polite" className="p-4 text-center">
          <div className="mb-3 text-[13px] text-[#64748b]">Caricamento thread...</div>
          <SkeletonLoader variant="chatHistory" count={5} ariaLabel="Caricamento cronologia thread" />
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

  // Separate active and archived threads
  const activeThreads = chats.filter(thread => thread.status !== 'Closed');
  const archivedThreads = chats.filter(thread => thread.status === 'Closed');

  return (
    <nav aria-label="Thread history" className="flex-1 overflow-y-auto p-2">
      {/* Active Threads Section */}
      {activeThreads.length > 0 && (
        <div className="mb-4">
          <h3 className="px-3 py-1 text-[11px] font-semibold text-[#5f6368] uppercase tracking-wide">
            Active Threads
          </h3>
          <ul role="list" className="list-none m-0 p-0">
            {activeThreads.map((thread) => (
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
            Archived Threads
          </h3>
          <ul role="list" className="list-none m-0 p-0">
            {archivedThreads.map((thread) => (
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

      {/* Confirm dialog */}
      <ConfirmDialogComponent />
    </nav>
  );
}
