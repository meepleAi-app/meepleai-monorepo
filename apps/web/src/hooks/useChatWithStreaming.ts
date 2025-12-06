/**
 * useChatWithStreaming - Zustand store + SSE streaming integration
 *
 * Combines Zustand chat store with useStreamingChat hook for components
 * that need streaming functionality (MessageInput, MessageList).
 *
 * Issue #1676: Created during backward compatibility layer removal.
 * Issue #1007: Integrates SSE streaming with Zustand state.
 *
 * Usage:
 * ```tsx
 * const { sendMessage, isStreaming, stopStreaming, ... } = useChatWithStreaming();
 * ```
 */

import { useCallback, useMemo } from 'react';
import { useChatStore } from '@/store/chat/store';
import { useStreamingChat } from '@/lib/hooks/useStreamingChat';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';

export function useChatWithStreaming() {
  const store = useChatStore();

  // Extract derived values
  const selectedGameId = store.selectedGameId;
  const activeChatId = selectedGameId ? store.activeChatIds[selectedGameId] : null;
  const chats = selectedGameId ? (store.chatsByGame[selectedGameId] ?? []) : [];
  const messages = activeChatId ? (store.messagesByChat[activeChatId] ?? []) : [];

  // Streaming hook (Issue #1007)
  const [streamingState, streamingControls] = useStreamingChat({
    onToken: useCallback((token: string, accumulated: string) => {
      // State updates automatically via hook
    }, []),
    onStateUpdate: useCallback((state: string) => {
      // State updates automatically via hook
    }, []),
    onComplete: useCallback(
      (answer: string, citations: Citation[], confidence: number | null) => {
        if (!activeChatId) return;

        // Add assistant message to chat
        const assistantMessage = {
          id: `temp-assistant-${Date.now()}`,
          role: 'assistant' as const,
          content: answer,
          timestamp: new Date(),
          endpoint: 'qa-stream' as const,
          gameId: selectedGameId || undefined,
        };

        store.addOptimisticMessage(assistantMessage, activeChatId);
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

  // Wrap sendMessage to trigger streaming
  const sendMessageWithStreaming = useCallback(
    async (content: string) => {
      if (!selectedGameId || !content.trim()) return;

      await store.sendMessage(content);

      const currentActiveChatId = selectedGameId ? store.activeChatIds[selectedGameId] : null;
      if (!currentActiveChatId) return;

      await streamingControls.startStreaming(selectedGameId, content.trim(), currentActiveChatId);
    },
    [selectedGameId, store, streamingControls]
  );

  return useMemo(
    () => ({
      // Store state
      ...store,
      // Derived values
      chats,
      activeChatId,
      messages,
      // Streaming state
      isStreaming: streamingState.isStreaming,
      streamingAnswer: streamingState.currentAnswer,
      streamingState: streamingState.stateMessage,
      streamingCitations: streamingState.citations,
      // Streaming controls
      stopStreaming: streamingControls.stopStreaming,
      // Override sendMessage with streaming version
      sendMessage: sendMessageWithStreaming,
    }),
    [
      store,
      chats,
      activeChatId,
      messages,
      streamingState,
      streamingControls,
      sendMessageWithStreaming,
    ]
  );
}
