/**
 * ChatStoreProvider - Initialization wrapper for Zustand chat store (Issue #1083)
 *
 * Responsibilities:
 * - Load initial games on mount
 * - Load chats when game changes
 * - Load messages when chat changes
 * - Hydrate persisted state from localStorage
 *
 * This replaces the provider hierarchy:
 * - GameProvider → Zustand GameSlice
 * - ChatProvider → Zustand ChatSlice + MessagesSlice
 * - UIProvider → Zustand UISlice
 *
 * Usage in _app.tsx:
 *   <AuthProvider>
 *     <ChatStoreProvider>
 *       {children}
 *     </ChatStoreProvider>
 *   </AuthProvider>
 */

import { PropsWithChildren, useEffect } from 'react';
import { useChatStore } from './store';

export function ChatStoreProvider({ children }: PropsWithChildren) {
  const loadGames = useChatStore((state) => state.loadGames);
  const selectedGameId = useChatStore((state) => state.selectedGameId);
  const loadAgents = useChatStore((state) => state.loadAgents);
  const loadChats = useChatStore((state) => state.loadChats);
  const activeChatIds = useChatStore((state) => state.activeChatIds);
  const loadMessages = useChatStore((state) => state.loadMessages);

  // Load games on mount
  useEffect(() => {
    void loadGames();
  }, [loadGames]);

  // Load agents when game changes
  useEffect(() => {
    if (selectedGameId) {
      void loadAgents(selectedGameId);
    }
  }, [selectedGameId, loadAgents]);

  // Load chats when game changes
  useEffect(() => {
    if (selectedGameId) {
      void loadChats(selectedGameId);
    }
  }, [selectedGameId, loadChats]);

  // Load messages when active chat changes
  useEffect(() => {
    if (selectedGameId) {
      const activeChatId = activeChatIds[selectedGameId];
      if (activeChatId) {
        void loadMessages(activeChatId);
      }
    }
  }, [selectedGameId, activeChatIds, loadMessages]);

  return <>{children}</>;
}

/**
 * Export for backward compatibility
 * Components can import from this file or from index.ts
 */
export { useChatStore } from './store';
export { useChatStoreWithSelectors } from './hooks';
export * from './compatibility';