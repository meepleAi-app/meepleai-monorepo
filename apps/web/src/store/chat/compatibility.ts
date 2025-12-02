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

import { useMemo, useCallback, createContext, useContext } from 'react';
import { useAuth } from '@/components/auth/AuthProvider';
import { useChatStore } from './store';
import { ChatThread, Message, Game, Agent } from '@/types';
import { AgentDto } from '@/lib/api/schemas/agents.schemas';
import type { FeedbackOutcome } from '@/lib/constants/feedback';
import { useStreamingChat } from '@/lib/hooks/useStreamingChat';

/**
 * ChatContext for Storybook/testing
 * Allows overriding the Zustand store values in isolated stories
 */
export const ChatContext = createContext<ChatContextValue | null>(null);

export interface ChatContextValue {
  // Authentication (from AuthProvider - not migrated)
  authUser: ReturnType<typeof useAuth>['user'];

  // Game & Agent Selection (from Zustand GameSlice)
  games: Game[];
  selectedGameId: string | null;
  agents: AgentDto[];
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
  setMessageFeedback: (messageId: string, feedback: FeedbackOutcome) => Promise<void>;
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

  // Streaming State (Issue #1007)
  isStreaming: boolean;
  streamingAnswer: string;
  streamingState: string;
  streamingCitations: import('@/lib/api/schemas/streaming.schemas').Citation[];
  stopStreaming: () => void;
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

  // Streaming hook (Issue #1007)
  const [streamingState, streamingControls] = useStreamingChat({
    onToken: useCallback((token: string, accumulated: string) => {
      // Optional: Could add real-time token display logic here
      // For now, state updates happen automatically via hook
    }, []),
    onStateUpdate: useCallback((state: string) => {
      // Optional: Could add custom state update logic here
      // For now, state updates happen automatically via hook
    }, []),
    onComplete: useCallback(
      (
        answer: string,
        citations: import('@/lib/api/schemas/streaming.schemas').Citation[],
        confidence: number | null
      ) => {
        // When streaming completes, add assistant message to chat
        if (!activeChatId) return;

        const assistantMessage: Message = {
          id: `temp-assistant-${Date.now()}`,
          role: 'assistant',
          content: answer,
          timestamp: new Date(),
          endpoint: 'qa-stream',
          gameId: selectedGameId || undefined,
        };

        // Add to messages via store
        store.addOptimisticMessage(assistantMessage, activeChatId);

        // Reload messages to get backend-persisted version
        void store.loadMessages(activeChatId);
      },
      [activeChatId, selectedGameId, store]
    ),
    onError: useCallback(
      (err: Error) => {
        store.setError(err.message || 'Errore durante lo streaming');
      },
      [store]
    ),
  });

  // Wrap sendMessage to trigger streaming (Issue #1007)
  const sendMessageWithStreaming = useCallback(
    async (content: string) => {
      if (!selectedGameId || !content.trim()) return;

      // Send user message via store (adds to thread, updates backend)
      await store.sendMessage(content);

      // Get active chat ID after message is sent (might have created new thread)
      const currentActiveChatId = selectedGameId ? store.activeChatIds[selectedGameId] : null;
      if (!currentActiveChatId) return;

      // Start SSE streaming for AI response
      await streamingControls.startStreaming(selectedGameId, content.trim(), currentActiveChatId);
    },
    [selectedGameId, store, streamingControls]
  );

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
      sendMessage: sendMessageWithStreaming, // Issue #1007: Wrapped to trigger SSE streaming
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

      // Streaming State (Issue #1007)
      isStreaming: streamingState.isStreaming,
      streamingAnswer: streamingState.currentAnswer,
      streamingState: streamingState.stateMessage,
      streamingCitations: streamingState.citations,
      stopStreaming: streamingControls.stopStreaming,
    }),
    [
      auth.user,
      store,
      selectedGameId,
      activeChatId,
      chats,
      messages,
      sendMessageWithStreaming,
      streamingState.isStreaming,
      streamingState.currentAnswer,
      streamingState.stateMessage,
      streamingState.citations,
      streamingControls.stopStreaming,
    ]
  );
}

/**
 * Re-export for backward compatibility
 * Existing imports will continue to work:
 *   import { useChatContext } from '@/hooks/useChatContext'
 */
export { useChatContext as useChatContextCompat };
