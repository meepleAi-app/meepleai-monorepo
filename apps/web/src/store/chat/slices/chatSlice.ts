/* eslint-disable security/detect-object-injection -- Safe Zustand store key access with typed game/chat IDs */
/**
 * Chat Slice (Issue #1083)
 *
 * Manages chat threads:
 * - Threads organized by game
 * - Active thread per game
 * - Thread CRUD operations
 *
 * Dependencies: Session, UI slices
 */

import { StateCreator } from 'zustand';
import { ChatStore, ChatSlice } from '../types';
import { api } from '@/lib/api';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';
import { CHAT_CONFIG } from '@/config';

export const createChatSlice: StateCreator<
  ChatStore,
  [['zustand/immer', never], ['zustand/subscribeWithSelector', never]],
  [],
  ChatSlice
> = (set, get) => ({
  // ============================================================================
  // State
  // ============================================================================
  chatsByGame: {},
  activeChatIds: {},

  // ============================================================================
  // Actions
  // ============================================================================
  loadChats: async gameId => {
    const { setLoading, setError } = get();
    setLoading('chats', true);
    setError(null);

    try {
      const chatThreads = await api.chat.getThreadsByGame(gameId);
      set(state => {
        state.chatsByGame[gameId] = chatThreads ?? [];
      });
    } catch (err) {
      logger.error(
        'Failed to load chat threads',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('ChatSlice', 'loadChats', { gameId })
      );
      setError('Errore nel caricamento delle chat');
      set(state => {
        state.chatsByGame[gameId] = [];
      });
    } finally {
      setLoading('chats', false);
    }
  },

  createChat: async () => {
    const { selectedGameId, selectedAgentId, setLoading, setError } = get();

    if (!selectedGameId || !selectedAgentId) {
      setError('Seleziona un gioco e un agente');
      return;
    }

    setLoading('creating', true);
    setError(null);

    try {
      const newThread = await api.chat.createThread({
        gameId: selectedGameId,
      });

      if (newThread) {
        // Save the previous active ID before updating state
        const previousActiveId = get().activeChatIds[selectedGameId];

        set(state => {
          if (!state.chatsByGame[selectedGameId]) {
            state.chatsByGame[selectedGameId] = [];
          }
          state.chatsByGame[selectedGameId].unshift(newThread);
          state.activeChatIds[selectedGameId] = newThread.id;
          state.messagesByChat[newThread.id] = [];
        });

        // Auto-archive oldest thread if limit exceeded
        // Get fresh state after mutation, but exclude the newly created thread from archive check
        const { chatsByGame } = get();
        const activeThreads = (chatsByGame[selectedGameId] ?? []).filter(
          t => t.status !== 'Closed' && t.id !== newThread.id
        );
        if (activeThreads.length > CHAT_CONFIG.MAX_THREADS_PER_GAME) {
          const sorted = [...activeThreads].sort(
            (a, b) =>
              new Date(a.lastMessageAt ?? a.createdAt).getTime() -
              new Date(b.lastMessageAt ?? b.createdAt).getTime()
          );
          // Use the previous active ID (before creating new thread) to avoid archiving it
          const toArchive = sorted.find(t => t.id !== previousActiveId);

          if (toArchive) {
            try {
              await api.chat.closeThread(toArchive.id);
              await get().loadChats(selectedGameId);
            } catch (archiveErr) {
              logger.error(
                'Failed to auto-archive thread',
                archiveErr instanceof Error ? archiveErr : new Error(String(archiveErr)),
                createErrorContext('ChatSlice', 'createChat.autoArchive', {
                  threadId: toArchive.id,
                  gameId: selectedGameId,
                })
              );
            }
          }
        }
      }
    } catch (err) {
      logger.error(
        'Failed to create chat thread',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('ChatSlice', 'createChat', {
          gameId: selectedGameId,
          agentId: selectedAgentId,
        })
      );
      setError('Errore nella creazione della chat');
    } finally {
      setLoading('creating', false);
    }
  },

  deleteChat: async chatId => {
    const { selectedGameId, setLoading, setError, activeChatIds: _activeChatIds } = get();

    if (!selectedGameId) return;

    // Note: Confirmation should be handled by the calling component
    // using useConfirmDialog hook (Issue #1435)

    setLoading('deleting', true);
    setError(null);

    try {
      // Use typed deleteThread method from chatClient
      await api.chat.deleteThread(chatId);

      set(state => {
        const currentThreads = state.chatsByGame[selectedGameId] ?? [];
        state.chatsByGame[selectedGameId] = currentThreads.filter(c => c.id !== chatId);

        if (state.activeChatIds[selectedGameId] === chatId) {
          state.activeChatIds[selectedGameId] = null;
        }

        delete state.messagesByChat[chatId];
      });
    } catch (err) {
      logger.error(
        'Failed to delete chat',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('ChatSlice', 'deleteChat', { chatId, gameId: selectedGameId })
      );
      setError("Errore nell'eliminazione della chat");
    } finally {
      setLoading('deleting', false);
    }
  },

  selectChat: async chatId => {
    const { selectedGameId, loadMessages } = get();

    if (!selectedGameId) return;

    set(state => {
      state.activeChatIds[selectedGameId] = chatId;
    });

    await loadMessages(chatId);
  },

  updateChatTitle: (chatId, title) =>
    set(state => {
      const { selectedGameId } = get();
      if (!selectedGameId) return;

      const threads = state.chatsByGame[selectedGameId] ?? [];
      const thread = threads.find(t => t.id === chatId);
      if (thread) {
        thread.title = title;
      }
    }),
});
