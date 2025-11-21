/**
 * useChatOptimistic - Optimistic UI updates for chat messages
 *
 * Issue #1167: Chat Optimistic Updates
 * Issue #1436: Fixed SWR + Zustand State Duplication
 *
 * Provides optimistic message sending with:
 * - Temporary message states with loading indicators
 * - Automatic rollback on error
 * - Zustand-only state management (single source of truth)
 * - Smooth UX without blocking spinners
 *
 * Usage:
 *   const { sendMessageOptimistic, isOptimisticUpdate } = useChatOptimistic();
 *   await sendMessageOptimistic(content);
 */

import { useCallback, useState } from 'react';
import { Message } from '@/types';
import { useChatStore, useActiveMessages } from '@/store/chat';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

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
 * Uses Zustand as single source of truth (Issue #1436)
 */
export function useChatOptimistic(): UseChatOptimisticResult {
  // Zustand store - single source of truth
  const selectedGameId = useChatStore((state) => state.selectedGameId);
  const selectedAgentId = useChatStore((state) => state.selectedAgentId);
  const activeChatIds = useChatStore((state) => state.activeChatIds);
  const sendMessage = useChatStore((state) => state.sendMessage);
  const addOptimisticMessage = useChatStore((state) => state.addOptimisticMessage);
  const removeOptimisticMessage = useChatStore((state) => state.removeOptimisticMessage);

  const activeChatId = selectedGameId ? activeChatIds[selectedGameId] : null;
  const messages = useActiveMessages();

  const [optimisticId, setOptimisticId] = useState<string | null>(null);
  const isOptimisticUpdate = optimisticId !== null;

  /**
   * Send message with optimistic update
   * Pattern from Issue #1167 (Simplified for #1436):
   * 1. Create temporary optimistic message
   * 2. Optimistically update Zustand store
   * 3. Send to backend
   * 4. Backend updates store with real data
   * 5. Rollback on error
   */
  const sendMessageOptimistic = useCallback(
    async (content: string) => {
      if (!selectedGameId || !selectedAgentId || !content.trim() || !activeChatId) {
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

      // 2. Optimistically update Zustand store
      addOptimisticMessage(tempMessage, activeChatId);

      try {
        // 3. Send to backend (Zustand store handles thread creation and updates)
        await sendMessage(content);

        // 4. Clear optimistic state (reactive)
        // Backend updates are handled by Zustand store's sendMessage
        setOptimisticId(null);
      } catch (err) {
        logger.error(
          'Failed to send message (optimistic)',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('useChatOptimistic', 'sendMessageOptimistic', {
            gameId: selectedGameId,
            agentId: selectedAgentId,
            contentLength: content.length,
          })
        );

        // 5. Rollback on error
        // Remove the optimistic message from Zustand store
        removeOptimisticMessage(tempId, activeChatId);

        // Clear optimistic state on error (reactive)
        setOptimisticId(null);

        // Re-throw so caller can show error toast
        throw err;
      }
    },
    [
      selectedGameId,
      selectedAgentId,
      activeChatId,
      sendMessage,
      addOptimisticMessage,
      removeOptimisticMessage,
    ]
  );

  return {
    sendMessageOptimistic,
    isOptimisticUpdate,
    messages,
  };
}