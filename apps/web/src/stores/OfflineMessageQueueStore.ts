/**
 * Offline Message Queue Store (Issue #2054)
 *
 * Manages queued chat messages when offline with:
 * - Persistent storage (localStorage)
 * - Automatic replay on reconnection
 * - Message deduplication
 * - Queue status tracking
 *
 * Usage:
 * ```tsx
 * const { queueMessage, pendingMessages, replayQueue } = useOfflineMessageQueue();
 *
 * // When offline
 * queueMessage({ gameId, chatId, content });
 *
 * // When back online
 * await replayQueue((msg) => sendMessage(msg));
 * ```
 */

import { create } from 'zustand';
import { devtools, persist, createJSONStorage } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

// ============================================================================
// Types
// ============================================================================

export interface QueuedMessage {
  /** Unique message ID for deduplication */
  id: string;

  /** Game ID the message belongs to */
  gameId: string;

  /** Chat thread ID (optional, for existing threads) */
  chatId: string | null;

  /** Message content */
  content: string;

  /** Document IDs for scoped queries (Issue #2051) */
  documentIds: string[] | null;

  /** Timestamp when queued */
  queuedAt: number;

  /** Number of replay attempts */
  replayAttempts: number;

  /** Last replay error (if any) */
  lastError: string | null;

  /** Status of the queued message */
  status: 'pending' | 'replaying' | 'failed' | 'sent';
}

export interface OfflineMessageQueueState {
  /** Queued messages awaiting replay */
  messages: QueuedMessage[];

  /** Whether replay is currently in progress */
  isReplaying: boolean;

  /** Last replay timestamp */
  lastReplayAt: number | null;

  /** Total messages sent via queue */
  totalSent: number;

  /** Total failed messages */
  totalFailed: number;
}

export interface OfflineMessageQueueActions {
  /** Queue a message for sending when online */
  queueMessage: (
    message: Omit<QueuedMessage, 'id' | 'queuedAt' | 'replayAttempts' | 'lastError' | 'status'>
  ) => string;

  /** Replay all pending messages using the provided send function */
  replayQueue: (
    sendFn: (message: QueuedMessage) => Promise<void>,
    options?: { maxRetries?: number; delayMs?: number }
  ) => Promise<{ sent: number; failed: number }>;

  /** Mark a specific message as sent */
  markAsSent: (messageId: string) => void;

  /** Mark a specific message as failed */
  markAsFailed: (messageId: string, error: string) => void;

  /** Remove a message from the queue */
  removeMessage: (messageId: string) => void;

  /** Clear all sent/failed messages */
  clearCompleted: () => void;

  /** Clear entire queue */
  clearAll: () => void;

  /** Get count of pending messages */
  getPendingCount: () => number;
}

export type OfflineMessageQueueStore = OfflineMessageQueueState & OfflineMessageQueueActions;

// ============================================================================
// Initial State
// ============================================================================

const initialState: OfflineMessageQueueState = {
  messages: [],
  isReplaying: false,
  lastReplayAt: null,
  totalSent: 0,
  totalFailed: 0,
};

// ============================================================================
// Store Creation
// ============================================================================

export const useOfflineMessageQueueStore = create<OfflineMessageQueueStore>()(
  devtools(
    persist(
      immer((set, get) => ({
        ...initialState,

        queueMessage: message => {
          const id = `msg_${Date.now()}_${Math.random().toString(36).substring(7)}`;

          set(state => {
            // Check for duplicates (same content to same game/chat within 5 seconds)
            const isDuplicate = state.messages.some(
              m =>
                m.gameId === message.gameId &&
                m.chatId === message.chatId &&
                m.content === message.content &&
                Date.now() - m.queuedAt < 5000
            );

            if (!isDuplicate) {
              state.messages.push({
                ...message,
                id,
                queuedAt: Date.now(),
                replayAttempts: 0,
                lastError: null,
                status: 'pending',
              });
            }
          });

          return id;
        },

        replayQueue: async (sendFn, options = {}) => {
          const { maxRetries = 3, delayMs = 1000 } = options;
          const state = get();

          if (state.isReplaying) {
            return { sent: 0, failed: 0 };
          }

          set(draft => {
            draft.isReplaying = true;
          });

          let sent = 0;
          let failed = 0;

          // Get pending messages sorted by queue time (oldest first)
          const pendingMessages = [...state.messages]
            .filter(m => m.status === 'pending' || m.status === 'failed')
            .sort((a, b) => a.queuedAt - b.queuedAt);

          for (const message of pendingMessages) {
            // Mark as replaying
            set(draft => {
              const msg = draft.messages.find(m => m.id === message.id);
              if (msg) {
                msg.status = 'replaying';
                msg.replayAttempts += 1;
              }
            });

            try {
              await sendFn(message);

              // Success
              set(draft => {
                const msg = draft.messages.find(m => m.id === message.id);
                if (msg) {
                  msg.status = 'sent';
                  msg.lastError = null;
                }
                draft.totalSent += 1;
              });

              sent++;

              // Small delay between messages to avoid overwhelming the server
              if (delayMs > 0) {
                await new Promise(resolve => setTimeout(resolve, delayMs));
              }
            } catch (error) {
              const errorMessage = error instanceof Error ? error.message : 'Unknown error';
              const currentAttempts =
                get().messages.find(m => m.id === message.id)?.replayAttempts ?? 0;

              if (currentAttempts >= maxRetries) {
                // Max retries reached, mark as failed
                set(draft => {
                  const msg = draft.messages.find(m => m.id === message.id);
                  if (msg) {
                    msg.status = 'failed';
                    msg.lastError = errorMessage;
                  }
                  draft.totalFailed += 1;
                });
                failed++;
              } else {
                // Reset to pending for future retry
                set(draft => {
                  const msg = draft.messages.find(m => m.id === message.id);
                  if (msg) {
                    msg.status = 'pending';
                    msg.lastError = errorMessage;
                  }
                });
              }
            }
          }

          set(draft => {
            draft.isReplaying = false;
            draft.lastReplayAt = Date.now();
          });

          return { sent, failed };
        },

        markAsSent: (messageId: string) => {
          set(state => {
            const msg = state.messages.find(m => m.id === messageId);
            if (msg) {
              msg.status = 'sent';
              msg.lastError = null;
            }
          });
        },

        markAsFailed: (messageId: string, error: string) => {
          set(state => {
            const msg = state.messages.find(m => m.id === messageId);
            if (msg) {
              msg.status = 'failed';
              msg.lastError = error;
            }
          });
        },

        removeMessage: (messageId: string) => {
          set(state => {
            state.messages = state.messages.filter(m => m.id !== messageId);
          });
        },

        clearCompleted: () => {
          set(state => {
            state.messages = state.messages.filter(
              m => m.status === 'pending' || m.status === 'replaying'
            );
          });
        },

        clearAll: () => {
          set(state => {
            state.messages = [];
            state.isReplaying = false;
          });
        },

        getPendingCount: () => {
          return get().messages.filter(m => m.status === 'pending' || m.status === 'failed').length;
        },
      })),
      {
        name: 'meepleai-offline-message-queue',
        storage: createJSONStorage(() =>
          typeof window !== 'undefined'
            ? localStorage
            : {
                getItem: () => null,
                setItem: () => {},
                removeItem: () => {},
              }
        ),
        // Only persist messages and stats
        partialize: state => ({
          messages: state.messages,
          totalSent: state.totalSent,
          totalFailed: state.totalFailed,
        }),
      }
    ),
    {
      name: 'OfflineMessageQueueStore',
      enabled: process.env.NODE_ENV === 'development',
    }
  )
);

// ============================================================================
// Selectors
// ============================================================================

export const selectPendingMessages = (state: OfflineMessageQueueStore) =>
  state.messages.filter(m => m.status === 'pending');

export const selectFailedMessages = (state: OfflineMessageQueueStore) =>
  state.messages.filter(m => m.status === 'failed');

export const selectPendingCount = (state: OfflineMessageQueueStore) =>
  state.messages.filter(m => m.status === 'pending' || m.status === 'failed').length;

export const selectIsReplaying = (state: OfflineMessageQueueStore) => state.isReplaying;
