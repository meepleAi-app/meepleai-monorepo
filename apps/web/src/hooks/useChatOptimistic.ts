/**
 * useChatOptimistic - Optimistic UI updates for chat messages
 *
 * Issue #1167: Chat Optimistic Updates
 *
 * Provides optimistic message sending with:
 * - Temporary message states with loading indicators
 * - Automatic rollback on error
 * - SWR integration for cache management
 * - Smooth UX without blocking spinners
 *
 * Usage:
 *   const { sendMessageOptimistic, isOptimisticUpdate } = useChatOptimistic();
 *   await sendMessageOptimistic(content);
 */

import { useCallback, useState } from 'react';
import useSWR, { useSWRConfig } from 'swr';
import { Message } from '@/types';
import { useChatStore, useActiveMessages } from '@/store/chat';

export interface UseChatOptimisticResult {
  /**
   * Send message with optimistic UI update
   * Automatically rollbacks on error
   */
  sendMessageOptimistic: (content: string) => Promise<void>;

  /**
   * Check if there's an active optimistic update
   */
  isOptimisticUpdate: boolean;

  /**
   * Current messages (including optimistic)
   */
  messages: Message[];
}

/**
 * Hook for optimistic chat message updates
 * Integrates SWR for cache management and automatic rollback
 */
export function useChatOptimistic(): UseChatOptimisticResult {
  // Use Zustand store instead of context
  const selectedGameId = useChatStore((state) => state.selectedGameId);
  const selectedAgentId = useChatStore((state) => state.selectedAgentId);
  const activeChatIds = useChatStore((state) => state.activeChatIds);
  const sendMessage = useChatStore((state) => state.sendMessage);

  const activeChatId = selectedGameId ? activeChatIds[selectedGameId] : null;
  const contextMessages = useActiveMessages();

  const { mutate } = useSWRConfig();
  const [optimisticId, setOptimisticId] = useState<string | null>(null);

  const swrKey = activeChatId ? `/api/v1/chats/${activeChatId}/messages` : null;

  const { data: swrMessages } = useSWR<Message[]>(
    swrKey,
    null,
    {
      fallbackData: contextMessages,
      revalidateOnFocus: false,
      revalidateOnReconnect: false,
    }
  );

  const messages = swrMessages ?? contextMessages;
  const isOptimisticUpdate = optimisticId !== null;

  /**
   * Send message with optimistic update
   * Pattern from Issue #1167:
   * 1. Create temporary optimistic message
   * 2. Optimistically update UI (SWR mutate without revalidate)
   * 3. Send to backend
   * 4. Revalidate with real data
   * 5. Rollback on error
   */
  const sendMessageOptimistic = useCallback(
    async (content: string) => {
      if (!selectedGameId || !selectedAgentId || !content.trim()) {
        return;
      }

      // 1. Create temporary optimistic message
      const tempId = `temp-${Date.now()}`;
      const tempMessage: Message = {
        id: tempId,
        role: 'user',
        content: content.trim(),
        timestamp: new Date(),
        isOptimistic: true, // #1167: Flag for UI rendering
      };

      // Track optimistic update (reactive state)
      setOptimisticId(tempId);

      // 2. Optimistically update UI
      // mutate with false = update cache without revalidation
      if (swrKey) {
        await mutate(
          swrKey,
          [...messages, tempMessage],
          false // Don't revalidate yet
        );
      }

      try {
        // 3. Send to backend (Zustand store handles thread creation)
        await sendMessage(content);

        // 4. Clear optimistic state (reactive)
        setOptimisticId(null);

        // 5. Revalidate to get real message from backend
        // Zustand store will reload messages, which updates our fallbackData
        // SWR will reflect this automatically
      } catch (err) {
        console.error('Failed to send message (optimistic):', err);

        // 5. Rollback on error
        // Remove the optimistic message from cache
        if (swrKey) {
          await mutate(
            swrKey,
            messages.filter((m) => m.id !== tempId),
            false
          );
        }

        // Clear optimistic state on error (reactive)
        setOptimisticId(null);

        // Re-throw so caller can show error toast
        throw err;
      }
    },
    [
      swrKey,
      messages,
      mutate,
      sendMessage,
      selectedGameId,
      selectedAgentId,
    ]
  );

  return {
    sendMessageOptimistic,
    isOptimisticUpdate,
    messages,
  };
}