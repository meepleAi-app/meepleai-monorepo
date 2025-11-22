/**
 * Backward Compatibility Layer (Issue #1083)
 *
 * Provides a useChatContext hook that reads from Zustand store
 * instead of React Context providers.
 *
 * This allows existing components to work without changing imports:
 * - Before: useChatContext() from ChatProvider/GameProvider/UIProvider aggregation
 * - After: useChatContext() reads from Zustand store
 *
 * Benefits:
 * - Drop-in replacement (no component changes needed)
 * - All Zustand performance benefits
 * - Gradual migration path to direct Zustand usage
 *
 * Migration Strategy:
 * 1. Replace provider imports with this compatibility layer
 * 2. Test all components work correctly
 * 3. Gradually migrate to direct Zustand hooks
 * 4. Remove compatibility layer when all components migrated
 */

import { useMemo } from 'react';
import { useAuth } from '@/components/auth/AuthProvider';
import { useChatStore } from './store';
import { ChatThread, Message, Game, Agent } from '@/types';

export interface ChatContextValue {
  // Authentication (from AuthProvider - not migrated)
  authUser: ReturnType<typeof useAuth>['user'];

  // Game & Agent Selection (from Zustand GameSlice)
  games: Game[];
  selectedGameId: string | null;
  agents: Agent[];
  selectedAgentId: string | null;
  selectGame: (gameId: string | null) => void;
  selectAgent: (agentId: string | null) => void;

  // Chat Management (from Zustand ChatSlice)
  chats: ChatThread[];
  activeChatId: string | null;
  messages: Message[];
  createChat: () => Promise<void>;
  deleteChat: (chatId: string) => Promise<void>;
  selectChat: (chatId: string) => Promise<void>;

  // Messaging (from Zustand MessagesSlice)
  sendMessage: (content: string) => Promise<void>;
  setMessageFeedback: (messageId: string, feedback: 'helpful' | 'not-helpful') => Promise<void>;
  editMessage: (messageId: string, content: string) => Promise<void>;
  deleteMessage: (messageId: string) => Promise<void>;

  // UI State (from Zustand UISlice)
  loading: {
    games: boolean;
    agents: boolean;
    chats: boolean;
    messages: boolean;
    sending: boolean;
    creating: boolean;
    updating: boolean;
    deleting: boolean;
  };
  errorMessage: string;
  sidebarCollapsed: boolean;
  toggleSidebar: () => void;

  // Message Edit State (from Zustand UISlice)
  editingMessageId: string | null;
  editContent: string;
  setEditContent: (content: string) => void;
  startEditMessage: (messageId: string, content: string) => void;
  cancelEdit: () => void;
  saveEdit: () => Promise<void>;

  // Input State (from Zustand UISlice)
  inputValue: string;
  setInputValue: (value: string) => void;

  // Search Mode State (from Zustand UISlice)
  searchMode: string;
  setSearchMode: (mode: string) => void;
}

/**
 * Backward-compatible useChatContext hook
 * Reads from Zustand store instead of React Context
 *
 * Usage (unchanged):
 *   const { games, selectedGameId, selectGame, loading } = useChatContext();
 */
export function useChatContext(): ChatContextValue {
  const auth = useAuth();

  // Subscribe to all store slices
  // Note: This is intentionally not optimized (subscribes to entire store)
  // to maintain 100% backward compatibility. Components using this hook
  // will re-render on any store change, just like with Context.
  //
  // For optimized performance, components should migrate to direct Zustand hooks:
  // - useChatStoreWithSelectors.use.games()
  // - useCurrentChats()
  // - useActiveMessages()
  const store = useChatStore();

  // Compute derived values
  const selectedGameId = store.selectedGameId;
  const activeChatId = selectedGameId ? store.activeChatIds[selectedGameId] : null;

  const chats = selectedGameId ? (store.chatsByGame[selectedGameId] ?? []) : [];
  const messages = activeChatId ? (store.messagesByChat[activeChatId] ?? []) : [];

  return useMemo<ChatContextValue>(
    () => ({
      // Authentication
      authUser: auth.user,

      // Game & Agent Selection
      games: store.games,
      selectedGameId,
      agents: store.agents,
      selectedAgentId: store.selectedAgentId,
      selectGame: store.selectGame,
      selectAgent: store.selectAgent,

      // Chat Management
      chats,
      activeChatId,
      messages,
      createChat: store.createChat,
      deleteChat: store.deleteChat,
      selectChat: store.selectChat,

      // Messaging
      sendMessage: store.sendMessage,
      setMessageFeedback: store.setMessageFeedback,
      editMessage: store.editMessage,
      deleteMessage: store.deleteMessage,

      // UI State
      loading: store.loading,
      errorMessage: store.error ?? '',
      sidebarCollapsed: store.sidebarCollapsed,
      toggleSidebar: store.toggleSidebar,

      // Message Edit State
      editingMessageId: store.editingMessageId,
      editContent: store.editContent,
      setEditContent: store.setEditContent,
      startEditMessage: store.startEdit,
      cancelEdit: store.cancelEdit,
      saveEdit: async () => {
        await store.saveEdit(store.editMessage);
      },

      // Input State
      inputValue: store.inputValue,
      setInputValue: store.setInputValue,

      // Search Mode State
      searchMode: store.searchMode,
      setSearchMode: store.setSearchMode,
    }),
    [
      auth.user,
      store,
      selectedGameId,
      activeChatId,
      chats,
      messages,
    ]
  );
}

/**
 * Re-export for backward compatibility
 * Existing imports will continue to work:
 *   import { useChatContext } from '@/hooks/useChatContext'
 */
export { useChatContext as useChatContextCompat };
