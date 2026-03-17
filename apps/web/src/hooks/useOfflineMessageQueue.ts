/**
 * useOfflineMessageQueue Hook (Issue #2054)
 *
 * React hook for offline message queue management with:
 * - Automatic queue replay on reconnection
 * - Integration with network status
 * - Message status tracking
 *
 * @example
 * ```tsx
 * function ChatInput() {
 *   const { queueMessage, pendingCount, isReplaying } = useOfflineMessageQueue();
 *   const { isOffline } = useNetworkStatus();
 *
 *   const handleSend = async (content: string) => {
 *     if (isOffline) {
 *       queueMessage({ gameId, chatId, content, documentIds: null });
 *       return;
 *     }
 *     // Send normally
 *   };
 * }
 * ```
 */

import { useCallback, useEffect, useRef } from 'react';

import { useNetworkStatus, useOnlineCallback } from '@/hooks/useNetworkStatus';
import { logger } from '@/lib/logger';
import {
  useOfflineMessageQueueStore,
  selectPendingMessages,
  selectFailedMessages,
  selectPendingCount,
  selectIsReplaying,
  type QueuedMessage,
} from '@/stores/OfflineMessageQueueStore';

// ============================================================================
// Hook Interface
// ============================================================================

export interface UseOfflineMessageQueueOptions {
  /** Callback to send a message (used during replay) */
  sendMessage?: (message: QueuedMessage) => Promise<void>;

  /** Whether to auto-replay on reconnection */
  autoReplay?: boolean;

  /** Max retries per message during replay */
  maxRetries?: number;

  /** Delay between messages during replay (ms) */
  replayDelayMs?: number;

  /** Callback when replay completes */
  onReplayComplete?: (result: { sent: number; failed: number }) => void;

  /** Callback when a message is queued */
  onMessageQueued?: (messageId: string) => void;
}

export interface UseOfflineMessageQueueReturn {
  /** Queue a message for sending when online */
  queueMessage: (
    message: Omit<QueuedMessage, 'id' | 'queuedAt' | 'replayAttempts' | 'lastError' | 'status'>
  ) => string;

  /** Manually trigger queue replay */
  replayQueue: () => Promise<{ sent: number; failed: number }>;

  /** Pending messages waiting to be sent */
  pendingMessages: QueuedMessage[];

  /** Failed messages that exceeded retry limit */
  failedMessages: QueuedMessage[];

  /** Count of pending + failed messages */
  pendingCount: number;

  /** Whether replay is currently in progress */
  isReplaying: boolean;

  /** Remove a specific message from queue */
  removeMessage: (messageId: string) => void;

  /** Retry a specific failed message */
  retryMessage: (messageId: string) => void;

  /** Clear all completed (sent/failed) messages */
  clearCompleted: () => void;

  /** Clear entire queue */
  clearAll: () => void;

  /** Whether the user is currently offline */
  isOffline: boolean;
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function useOfflineMessageQueue(
  options: UseOfflineMessageQueueOptions = {}
): UseOfflineMessageQueueReturn {
  const {
    sendMessage,
    autoReplay = true,
    maxRetries = 3,
    replayDelayMs = 500,
    onReplayComplete,
    onMessageQueued,
  } = options;

  const store = useOfflineMessageQueueStore();
  const { isOffline } = useNetworkStatus();

  // Use selectors for optimized subscriptions
  const pendingMessages = useOfflineMessageQueueStore(selectPendingMessages);
  const failedMessages = useOfflineMessageQueueStore(selectFailedMessages);
  const pendingCount = useOfflineMessageQueueStore(selectPendingCount);
  const isReplaying = useOfflineMessageQueueStore(selectIsReplaying);

  // Queue message with callback notification
  const queueMessage = useCallback(
    (
      message: Omit<QueuedMessage, 'id' | 'queuedAt' | 'replayAttempts' | 'lastError' | 'status'>
    ): string => {
      const messageId = store.queueMessage(message);
      onMessageQueued?.(messageId);
      return messageId;
    },
    [store, onMessageQueued]
  );

  // Manual replay trigger
  const replayQueue = useCallback(async () => {
    if (!sendMessage) {
      logger.warn('[useOfflineMessageQueue] No sendMessage function provided');
      return { sent: 0, failed: 0 };
    }

    const result = await store.replayQueue(sendMessage, {
      maxRetries,
      delayMs: replayDelayMs,
    });

    onReplayComplete?.(result);
    return result;
  }, [store, sendMessage, maxRetries, replayDelayMs, onReplayComplete]);

  // Ref to track auto-replay timeout for cleanup
  const autoReplayTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  // Auto-replay when coming back online
  useOnlineCallback(
    useCallback(() => {
      if (autoReplay && sendMessage && pendingCount > 0) {
        // Clear any existing timeout
        if (autoReplayTimeoutRef.current) {
          clearTimeout(autoReplayTimeoutRef.current);
        }
        // Small delay to ensure network is stable
        autoReplayTimeoutRef.current = setTimeout(() => {
          autoReplayTimeoutRef.current = null;
          void replayQueue();
        }, 1000);
      }
    }, [autoReplay, sendMessage, pendingCount, replayQueue])
  );

  // Cleanup timeout on unmount
  useEffect(() => {
    return () => {
      if (autoReplayTimeoutRef.current) {
        clearTimeout(autoReplayTimeoutRef.current);
      }
    };
  }, []);

  // Retry a specific failed message
  const retryMessage = useCallback(
    (messageId: string) => {
      // Reset the message status to pending so it gets picked up in next replay
      const messages = store.messages;
      const message = messages.find(m => m.id === messageId);

      if (message && message.status === 'failed') {
        // Remove and re-queue to reset attempts
        store.removeMessage(messageId);
        store.queueMessage({
          gameId: message.gameId,
          chatId: message.chatId,
          content: message.content,
          documentIds: message.documentIds,
        });

        // If online, trigger immediate replay
        if (!isOffline && sendMessage) {
          void replayQueue();
        }
      }
    },
    [store, isOffline, sendMessage, replayQueue]
  );

  return {
    queueMessage,
    replayQueue,
    pendingMessages,
    failedMessages,
    pendingCount,
    isReplaying,
    removeMessage: store.removeMessage,
    retryMessage,
    clearCompleted: store.clearCompleted,
    clearAll: store.clearAll,
    isOffline,
  };
}
