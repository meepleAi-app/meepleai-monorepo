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
import { ChatStore, MessagesSlice } from '../types';
import { Message } from '@/types';
import { api } from '@/lib/api';

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
  loadMessages: async (threadId) => {
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

        set((state) => {
          state.messagesByChat[threadId] = uiMessages;
        });
      } else {
        set((state) => {
          state.messagesByChat[threadId] = [];
        });
      }
    } catch (err) {
      console.error('Failed to load messages:', err);
      setError('Errore nel caricamento dei messaggi');
      set((state) => {
        state.messagesByChat[threadId] = [];
      });
    } finally {
      setLoading('messages', false);
    }
  },

  sendMessage: async (content) => {
    const { selectedGameId, selectedAgentId, activeChatIds, setLoading, setError, updateChatTitle } = get();

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

    try {
      // Create thread if none exists
      let threadId = activeChatIds[selectedGameId] ?? '';
      let isNewThread = false;

      if (!threadId) {
        const autoTitle = content.trim().substring(0, 50) + (content.length > 50 ? '...' : '');
        const newThread = await api.chat.createThread({
          gameId: selectedGameId,
          title: autoTitle,
          initialMessage: null,
        });

        if (!newThread) throw new Error('Failed to create chat thread');

        threadId = newThread.id;
        isNewThread = true;

        set((state) => {
          if (!state.chatsByGame[selectedGameId]) {
            state.chatsByGame[selectedGameId] = [];
          }
          state.chatsByGame[selectedGameId].unshift(newThread);
          state.activeChatIds[selectedGameId] = threadId;
          state.messagesByChat[threadId] = [];
        });
      }

      // Optimistic update
      set((state) => {
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
          const autoTitle = content.trim().substring(0, 50) + (content.length > 50 ? '...' : '');
          updateChatTitle(threadId, autoTitle);
        }
      }

      // Remove optimistic flag
      set((state) => {
        const messages = state.messagesByChat[threadId] ?? [];
        const messageIndex = messages.findIndex(m => m.id === tempUserId);
        if (messageIndex !== -1) {
          state.messagesByChat[threadId][messageIndex].isOptimistic = false;
        }
      });

    } catch (err) {
      console.error('Failed to send message:', err);
      setError("Errore nell'invio del messaggio");

      // Rollback optimistic update
      const threadId = activeChatIds[selectedGameId];
      if (threadId) {
        set((state) => {
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
      console.error('Failed to edit message:', err);
      setError("Errore nell'aggiornamento del messaggio");
    } finally {
      setLoading('updating', false);
    }
  },

  deleteMessage: async (messageId) => {
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
      console.error('Failed to delete message:', err);
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
    set((state) => {
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
          feedback: nextFeedback,
        });
      }
    } catch (err) {
      console.error('Failed to set feedback:', err);

      // Revert optimistic update
      set((state) => {
        const messages = state.messagesByChat[threadId] ?? [];
        const messageIndex = messages.findIndex(m => m.id === messageId);
        if (messageIndex !== -1) {
          state.messagesByChat[threadId][messageIndex].feedback = previousFeedback;
        }
      });
    }
  },

  addOptimisticMessage: (message, threadId) =>
    set((state) => {
      if (!state.messagesByChat[threadId]) {
        state.messagesByChat[threadId] = [];
      }
      state.messagesByChat[threadId].push({ ...message, isOptimistic: true });
    }),

  removeOptimisticMessage: (messageId, threadId) =>
    set((state) => {
      const messages = state.messagesByChat[threadId] ?? [];
      state.messagesByChat[threadId] = messages.filter(m => m.id !== messageId);
    }),

  updateMessageInThread: (threadId, messageId, updates) =>
    set((state) => {
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
