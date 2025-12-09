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

'use client';

import { PropsWithChildren, useEffect, useRef } from 'react';
import { useChatStore } from './store';
import { useStreamingChat } from '@/lib/hooks/useStreamingChat';

// Import store instance for test hooks
import { useChatStore as chatStore } from './store';

export function ChatStoreProvider({ children }: PropsWithChildren) {
  const loadGames = useChatStore(state => state.loadGames);
  const selectedGameId = useChatStore(state => state.selectedGameId);
  const selectGame = useChatStore(state => state.selectGame);
  const selectAgent = useChatStore(state => state.selectAgent);
  const loadAgents = useChatStore(state => state.loadAgents);
  const loadChats = useChatStore(state => state.loadChats);
  const activeChatIds = useChatStore(state => state.activeChatIds);
  const loadMessages = useChatStore(state => state.loadMessages);

  // Streaming chat hook (for test hooks integration - Issue #1807)
  const [streamingState, streamingControls] = useStreamingChat({
    onComplete: (answer, citations, confidence) => {
      // Store streaming result globally for E2E tests to access
      if (typeof window !== 'undefined') {
        window.__TEST_STREAMING_STATE__ = {
          answer,
          citations,
          confidence,
          completed: true,
        };
      }
    },
    onError: error => {
      console.error('Streaming error:', error);
    },
  });

  // Expose streaming state globally during streaming for tests
  useEffect(() => {
    if (typeof window !== 'undefined' && streamingState.isStreaming) {
      window.__TEST_STREAMING_STATE__ = {
        answer: streamingState.currentAnswer,
        citations: streamingState.citations,
        confidence: streamingState.confidence,
        isStreaming: true,
      };
    }
  }, [streamingState]);

  // Load games on mount
  useEffect(() => {
    void loadGames();
  }, [loadGames]);

  // Load agents on mount (Issue #868: Agents are global, not per-game)
  useEffect(() => {
    void loadAgents();
  }, [loadAgents]);

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

  // E2E Test Hooks (Issue #1807) - Programmatic game/agent selection for tests
  // Bypasses Radix UI Select interaction (unreliable in Playwright)
  // Safe to expose in all environments (no security/functional impact)
  useEffect(() => {
    if (typeof window !== 'undefined') {
      window.__MEEPLEAI_TEST_HOOKS__ = {
        ...window.__MEEPLEAI_TEST_HOOKS__,
        chat: {
          selectGame: (gameId: string) => Promise.resolve(selectGame(gameId)),
          selectGameAndAgent: async (gameId: string, agentId: string) => {
            selectGame(gameId);
            // Wait for game selection and agents to load
            await new Promise(resolve => setTimeout(resolve, 500));
            selectAgent(agentId);
            // Wait for chats to load
            await new Promise(resolve => setTimeout(resolve, 1000));

            // Auto-select first chat if available (CRITICAL: Without this, sendMessage creates new thread)
            await new Promise(resolve => setTimeout(resolve, 500)); // Wait for loadChats to complete
            const state = chatStore.getState();
            const chats = state.chatsByGame[gameId];
            if (chats && chats.length > 0) {
              await chatStore.getState().selectChat(chats[0].id);
              await new Promise(resolve => setTimeout(resolve, 500)); // Wait for selectChat to complete
            }
          },
          sendMessage: async (content: string) => {
            // Send message + trigger Q&A streaming (for citation tests)
            const state = chatStore.getState();
            await state.sendMessage(content);

            // Get active thread ID for streaming
            const threadId = state.activeChatIds[state.selectedGameId || ''];
            if (threadId && state.selectedGameId) {
              // Trigger Q&A streaming (Issue #1807: Tests need SSE response with citations)
              await streamingControls.startStreaming(state.selectedGameId, content, threadId);
            }
          },
        },
      };
    }
  }, [selectGame, selectAgent, streamingControls]);

  return <>{children}</>;
}

/**
 * Export for backward compatibility
 * Components can import from this file or from index.ts
 *
 * Issue #1676: Removed compatibility layer export (migrated to direct Zustand)
 */
export { useChatStore } from './store';
export { useChatStoreWithSelectors } from './hooks';
