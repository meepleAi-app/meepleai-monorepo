/* eslint-disable security/detect-object-injection -- Safe Zustand store key access with typed thread/message IDs */
/**
 * Messages Slice (Issue #1083)
 *
 * Manages messages per thread:
 * - Message loading and caching
 * - Optimistic updates for send/edit/delete
 * - Feedback management
 *
 * Dependencies: Session, Chat, UI slices
 */

import { StateCreator } from 'zustand';

import { CHAT_CONFIG } from '@/config';
import { api } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';
import { Message } from '@/types';

import { ChatStore, MessagesSlice } from '../types';

export const createMessagesSlice: StateCreator<
  ChatStore,
  [['zustand/immer', never], ['zustand/subscribeWithSelector', never]],
  [],
  MessagesSlice
> = (set, get) => ({
  // ============================================================================
  // State
  // ============================================================================
  messagesByChat: {},

  // ============================================================================
  // Actions
  // ============================================================================
  loadMessages: async threadId => {
    const { setLoading, setError } = get();
    setLoading('messages', true);
    setError(null);

    try {
      const thread = await api.chat.getThreadById(threadId);
      if (thread) {
        const uiMessages: Message[] = thread.messages.map((msg, index) => ({
          id: `${threadId}-${index}`,
          role: msg.role as 'user' | 'assistant',
          content: msg.content,
          timestamp: new Date(msg.timestamp),
          backendMessageId: msg.backendMessageId,
          endpoint: msg.endpoint,
          gameId: msg.gameId,
          feedback: msg.feedback ?? null,
        }));

        set(state => {
          state.messagesByChat[threadId] = uiMessages;
        });
      } else {
        set(state => {
          state.messagesByChat[threadId] = [];
        });
      }
    } catch (err) {
      logger.error(
        'Failed to load messages',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('MessagesSlice', 'loadMessages', { threadId })
      );
      setError('Errore nel caricamento dei messaggi');
      set(state => {
        state.messagesByChat[threadId] = [];
      });
    } finally {
      setLoading('messages', false);
    }
  },

  sendMessage: async content => {
    const {
      selectedGameId,
      selectedAgentId,
      activeChatIds,
      setLoading,
      setError,
      updateChatTitle,
    } = get();

    if (!selectedGameId || !selectedAgentId || !content.trim()) return;

    const tempUserId = `temp-user-${Date.now()}`;
    const userMessage: Message = {
      id: tempUserId,
      role: 'user',
      content: content.trim(),
      timestamp: new Date(),
      isOptimistic: true,
    };

    setError(null);
    setLoading('sending', true);

    // Declare threadId outside try block so it's accessible in catch for rollback
    let threadId = activeChatIds[selectedGameId] ?? '';
    let isNewThread = false;

    try {
      // Create thread if none exists

      if (!threadId) {
        const autoTitle =
          content.trim().substring(0, CHAT_CONFIG.AUTO_TITLE_MAX_LENGTH) +
          (content.length > CHAT_CONFIG.AUTO_TITLE_MAX_LENGTH ? '...' : '');
        const newThread = await api.chat.createThread({
          gameId: selectedGameId,
          title: autoTitle,
          initialMessage: null,
        });

        if (!newThread) throw new Error('Failed to create chat thread');

        threadId = newThread.id;
        isNewThread = true;

        set(state => {
          if (!state.chatsByGame[selectedGameId]) {
            state.chatsByGame[selectedGameId] = [];
          }
          state.chatsByGame[selectedGameId].unshift(newThread);
          state.activeChatIds[selectedGameId] = threadId;
          state.messagesByChat[threadId] = [];
        });
      }

      // Optimistic update
      set(state => {
        if (!state.messagesByChat[threadId]) {
          state.messagesByChat[threadId] = [];
        }
        state.messagesByChat[threadId].push(userMessage);
      });

      // Send to backend
      await api.chat.addMessage(threadId, {
        content: content.trim(),
        role: 'user',
      });

      // Update title if first message in existing thread
      if (!isNewThread) {
        const messages = get().messagesByChat[threadId] ?? [];
        if (messages.length === 1) {
          const autoTitle =
            content.trim().substring(0, CHAT_CONFIG.AUTO_TITLE_MAX_LENGTH) +
            (content.length > CHAT_CONFIG.AUTO_TITLE_MAX_LENGTH ? '...' : '');
          updateChatTitle(threadId, autoTitle);
        }
      }

      // Remove optimistic flag
      set(state => {
        const messages = state.messagesByChat[threadId] ?? [];
        const messageIndex = messages.findIndex(m => m.id === tempUserId);
        if (messageIndex !== -1) {
          state.messagesByChat[threadId][messageIndex].isOptimistic = false;
        }
      });
    } catch (err) {
      logger.error(
        'Failed to send message',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('MessagesSlice', 'sendMessage', {
          selectedGameId,
          selectedAgentId,
          contentLength: content.trim().length,
        })
      );
      setError("Errore nell'invio del messaggio");

      // Rollback optimistic update
      // Use the threadId from try block scope (not activeChatIds snapshot)
      // This ensures rollback works even for newly created threads
      if (threadId) {
        set(state => {
          const messages = state.messagesByChat[threadId] ?? [];
          state.messagesByChat[threadId] = messages.filter(m => m.id !== tempUserId);
        });
      }
    } finally {
      setLoading('sending', false);
    }
  },

  editMessage: async (messageId, content) => {
    const { selectedGameId, activeChatIds, loadMessages, setLoading, setError } = get();

    if (!selectedGameId || !content.trim()) return;

    const threadId = activeChatIds[selectedGameId];
    if (!threadId) return;

    setLoading('updating', true);
    setError(null);

    try {
      await api.chat.updateMessage(threadId, messageId, content.trim());
      await loadMessages(threadId);
    } catch (err) {
      logger.error(
        'Failed to edit message',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('MessagesSlice', 'editMessage', { threadId, messageId })
      );
      setError("Errore nell'aggiornamento del messaggio");
    } finally {
      setLoading('updating', false);
    }
  },

  deleteMessage: async messageId => {
    const { selectedGameId, activeChatIds, loadMessages, setLoading, setError } = get();

    if (!selectedGameId) return;

    const threadId = activeChatIds[selectedGameId];
    if (!threadId) return;

    // Note: Confirmation should be handled by the calling component
    // using useConfirmDialog hook (Issue #1435)

    setLoading('deleting', true);
    setError(null);

    try {
      await api.chat.deleteMessage(threadId, messageId);
      await loadMessages(threadId);
    } catch (err) {
      logger.error(
        'Failed to delete message',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('MessagesSlice', 'deleteMessage', { threadId, messageId })
      );
      setError("Errore nell'eliminazione del messaggio");
    } finally {
      setLoading('deleting', false);
    }
  },

  setMessageFeedback: async (messageId, feedback) => {
    const { selectedGameId, activeChatIds, messagesByChat } = get();

    if (!selectedGameId) return;

    const threadId = activeChatIds[selectedGameId];
    if (!threadId) return;

    const messages = messagesByChat[threadId] ?? [];
    const targetMessage = messages.find(msg => msg.id === messageId);
    if (!targetMessage) return;

    const previousFeedback = targetMessage.feedback ?? null;
    const nextFeedback = previousFeedback === feedback ? null : feedback;
    const endpoint = targetMessage.endpoint ?? 'qa';
    const gameId = targetMessage.gameId ?? selectedGameId ?? '';
    const feedbackMessageId = targetMessage.backendMessageId ?? messageId;

    // Optimistic update
    set(state => {
      const messages = state.messagesByChat[threadId] ?? [];
      const messageIndex = messages.findIndex(m => m.id === messageId);
      if (messageIndex !== -1) {
        state.messagesByChat[threadId][messageIndex].feedback = nextFeedback;
      }
    });

    try {
      // Only submit feedback if it's not null (FE-IMP-005: API doesn't accept null)
      if (nextFeedback !== null) {
        await api.chat.submitAgentFeedback({
          messageId: feedbackMessageId,
          endpoint,
          gameId,
          outcome: nextFeedback,
        });
      }
    } catch (err) {
      logger.error(
        'Failed to set feedback',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('MessagesSlice', 'setMessageFeedback', {
          threadId,
          messageId,
          feedback: nextFeedback,
        })
      );

      // Revert optimistic update
      set(state => {
        const messages = state.messagesByChat[threadId] ?? [];
        const messageIndex = messages.findIndex(m => m.id === messageId);
        if (messageIndex !== -1) {
          state.messagesByChat[threadId][messageIndex].feedback = previousFeedback;
        }
      });
    }
  },

  addOptimisticMessage: (message, threadId) =>
    set(state => {
      if (!state.messagesByChat[threadId]) {
        state.messagesByChat[threadId] = [];
      }
      state.messagesByChat[threadId].push({ ...message, isOptimistic: true });
    }),

  removeOptimisticMessage: (messageId, threadId) =>
    set(state => {
      const messages = state.messagesByChat[threadId] ?? [];
      state.messagesByChat[threadId] = messages.filter(m => m.id !== messageId);
    }),

  updateMessageInThread: (threadId, messageId, updates) =>
    set(state => {
      const messages = state.messagesByChat[threadId] ?? [];
      const messageIndex = messages.findIndex(m => m.id === messageId);
      if (messageIndex !== -1) {
        state.messagesByChat[threadId][messageIndex] = {
          ...messages[messageIndex],
          ...updates,
        };
      }
    }),
});
