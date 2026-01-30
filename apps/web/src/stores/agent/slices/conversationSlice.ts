/* eslint-disable security/detect-object-injection -- Safe Zustand store key access with typed session IDs */
/**
 * Conversation Slice (Issue #3188)
 *
 * Manages conversation history:
 * - Message storage per session
 * - Backend sync for permanent storage
 * - localStorage cache (last 5 conversations)
 * - Optimistic updates for sendMessage
 *
 * Dependencies: sessionSlice (for incrementMessageCount)
 */

import { StateCreator } from 'zustand';

import { AgentMessage } from '@/types/agent';
import { ConversationCache, AgentStoreError } from '../types';
import type { AgentStore } from '../types/store.types';

const MAX_CACHED_CONVERSATIONS = 5;

export interface ConversationSlice {
  // ============================================================================
  // State
  // ============================================================================

  /** Conversations by session ID */
  conversations: Record<string, AgentMessage[]>;

  /** LocalStorage cache (last 5 conversations) */
  conversationCache: ConversationCache[];

  /** Loading state for message operations */
  sendingMessage: boolean;
  loadingHistory: boolean;

  /** Last error for conversation operations */
  conversationError: AgentStoreError | null;

  // ============================================================================
  // Actions
  // ============================================================================

  /** Send message with optimistic update */
  sendMessage: (sessionId: string, message: string, gameId: string) => Promise<void>;

  /** Load conversation history from backend */
  loadHistory: (sessionId: string, gameId: string) => Promise<void>;

  /** Add message to conversation (without backend sync) */
  addMessageLocal: (sessionId: string, message: AgentMessage) => void;

  /** Update cache for offline access */
  updateCache: (sessionId: string, gameId: string) => void;

  /** Clear conversation error */
  clearConversationError: () => void;
}

export const createConversationSlice: StateCreator<
  AgentStore,
  [['zustand/immer', never]],
  [],
  ConversationSlice
> = set => ({
  // ============================================================================
  // Initial State
  // ============================================================================
  conversations: {},
  conversationCache: [],
  sendingMessage: false,
  loadingHistory: false,
  conversationError: null,

  // ============================================================================
  // Actions
  // ============================================================================

  /**
   * Send message with optimistic update
   */
  sendMessage: async (sessionId, message, gameId) => {
    set(state => {
      state.sendingMessage = true;
      state.conversationError = null;
    });

    // Optimistic update: Add user message immediately
    const userMessage: AgentMessage = {
      type: 'user',
      content: message,
      timestamp: new Date(),
    };

    set(state => {
      if (!state.conversations[sessionId]) {
        state.conversations[sessionId] = [];
      }
      state.conversations[sessionId].push(userMessage);
    });

    try {
      // TODO: Replace with actual backend API call
      // const response = await fetch(`/api/v1/agent/sessions/${sessionId}/messages`, {
      //   method: 'POST',
      //   headers: { 'Content-Type': 'application/json' },
      //   body: JSON.stringify({ message }),
      // });
      // const agentResponse = await response.json();

      // Mock delay and response
      await new Promise(resolve => setTimeout(resolve, 1000));
      const mockAgentMessage: AgentMessage = {
        type: 'agent',
        content: `Mock response to: ${message}`,
        timestamp: new Date(),
        citations: [],
      };

      set(state => {
        state.conversations[sessionId].push(mockAgentMessage);
        state.sendingMessage = false;
      });

      // Update cache after successful send
      set(state => {
        updateCacheInternal(state, sessionId, gameId);
      });
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to send message';

      // Rollback optimistic update
      set(state => {
        const messages = state.conversations[sessionId];
        if (messages && messages.length > 0) {
          messages.pop(); // Remove optimistically added user message
        }

        state.sendingMessage = false;
        state.conversationError = {
          message: errorMessage,
          code: 'SEND_MESSAGE_ERROR',
          timestamp: new Date(),
        };
      });

      throw error;
    }
  },

  /**
   * Load conversation history from backend
   */
  loadHistory: async (sessionId, gameId) => {
    set(state => {
      state.loadingHistory = true;
      state.conversationError = null;
    });

    try {
      // TODO: Replace with actual backend API call
      // const response = await fetch(`/api/v1/agent/sessions/${sessionId}/messages`);
      // const messages = await response.json();

      // Mock delay and messages
      await new Promise(resolve => setTimeout(resolve, 500));
      const mockMessages: AgentMessage[] = [
        {
          type: 'system',
          content: 'Session started',
          timestamp: new Date(),
        },
      ];

      set(state => {
        state.conversations[sessionId] = mockMessages;
        state.loadingHistory = false;
      });

      // Update cache
      set(state => {
        updateCacheInternal(state, sessionId, gameId);
      });
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to load history';

      set(state => {
        state.loadingHistory = false;
        state.conversationError = {
          message: errorMessage,
          code: 'LOAD_HISTORY_ERROR',
          timestamp: new Date(),
        };
      });

      throw error;
    }
  },

  /**
   * Add message locally (without backend sync)
   */
  addMessageLocal: (sessionId, message) =>
    set(state => {
      if (!state.conversations[sessionId]) {
        state.conversations[sessionId] = [];
      }
      state.conversations[sessionId].push(message);
    }),

  /**
   * Update cache for offline access
   */
  updateCache: (sessionId, gameId) =>
    set(state => {
      updateCacheInternal(state, sessionId, gameId);
    }),

  /**
   * Clear conversation error
   */
  clearConversationError: () =>
    set(state => {
      state.conversationError = null;
    }),
});

/**
 * Internal helper to update cache (last 5 conversations)
 */
function updateCacheInternal(
  state: ConversationSlice,
  sessionId: string,
  gameId: string
): void {
  const messages = state.conversations[sessionId];
  if (!messages || messages.length === 0) return;

  // Create cache entry
  const cacheEntry: ConversationCache = {
    sessionId,
    gameId,
    messages: [...messages], // Clone to avoid mutation
    lastUpdated: new Date(),
    synced: true, // Assume synced after send/load
  };

  // Remove existing entry for this session if present
  state.conversationCache = state.conversationCache.filter(c => c.sessionId !== sessionId);

  // Add new entry at beginning
  state.conversationCache.unshift(cacheEntry);

  // Keep only last 5
  if (state.conversationCache.length > MAX_CACHED_CONVERSATIONS) {
    state.conversationCache = state.conversationCache.slice(0, MAX_CACHED_CONVERSATIONS);
  }
}
