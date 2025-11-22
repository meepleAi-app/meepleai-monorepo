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
  const sendMessage = useChatStore((state) => state.sendMessage);
  const messages = useActiveMessages();

  const [optimisticId, setOptimisticId] = useState<string | null>(null);
  const isOptimisticUpdate = optimisticId !== null;

  /**
   * Send message with optimistic update
   * Pattern from Issue #1167 (Simplified for #1436):
   * 1. Delegate to Zustand store's sendMessage (handles everything)
   * 2. sendMessage creates thread if needed, adds optimistic message, sends to backend
   * 3. Track loading state for UI feedback
   *
   * Note: sendMessage already handles optimistic updates and rollback,
   * so we don't duplicate that logic here
   */
  const sendMessageOptimistic = useCallback(
    async (content: string) => {
      if (!selectedGameId || !selectedAgentId || !content.trim()) {
        return;
      }

      // Track optimistic update (reactive state for UI feedback)
      const tempId = `temp-${Date.now()}`;
      setOptimisticId(tempId);

      try {
        // Delegate to Zustand store's sendMessage
        // It handles: thread creation, optimistic message, backend call, rollback
        await sendMessage(content);

        // Clear optimistic state (reactive)
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

        // Clear optimistic state on error (reactive)
        // Rollback is handled by sendMessage
        setOptimisticId(null);

        // Re-throw so caller can show error toast
        throw err;
      }
    },
    [
      selectedGameId,
      selectedAgentId,
      sendMessage,
    ]
  );

  return {
    sendMessageOptimistic,
    isOptimisticUpdate,
    messages,
  };
}