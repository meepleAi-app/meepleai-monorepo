/**
 * Main Chat Store (Issue #1083)
 *
 * Combines all slices with middleware:
 * - Immer: Mutable state updates
 * - subscribeWithSelector: Granular subscriptions
 * - Zundo: Undo/redo for message operations
 * - Persistence: localStorage for chat state
 *
 * Store Architecture:
 * - Session: User selections, UI state
 * - Game: Games catalog, agents
 * - Chat: Thread management per game
 * - Messages: Message operations with optimistic updates
 * - UI: Loading states, errors
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { subscribeWithSelector } from 'zustand/middleware';
import { temporal } from 'zundo';
import { devtools, persist, createJSONStorage } from 'zustand/middleware';

import { ChatStore } from './types';
import { createSessionSlice } from './slices/sessionSlice';
import { createGameSlice } from './slices/gameSlice';
import { createChatSlice } from './slices/chatSlice';
import { createMessagesSlice } from './slices/messagesSlice';
import { createUISlice } from './slices/uiSlice';

// ============================================================================
// Store Creation with Middleware Stack
// ============================================================================

/**
 * Middleware order (outer to inner):
 * 1. devtools: Browser DevTools integration
 * 2. persist: localStorage persistence
 * 3. temporal (zundo): Undo/redo capabilities
 * 4. subscribeWithSelector: Granular subscriptions
 * 5. immer: Mutable state updates
 */
export const useChatStore = create<ChatStore>()(
  devtools(
    persist(
      temporal(
        subscribeWithSelector(
          immer((set, get, store) => ({
            // Combine all slices
            ...createSessionSlice(set, get, store),
            ...createGameSlice(set, get, store),
            ...createChatSlice(set, get, store),
            ...createMessagesSlice(set, get, store),
            ...createUISlice(set, get, store),
          }))
        ),
        {
          // Zundo configuration for undo/redo
          // Only track message operations (send, edit, delete)
          partialize: (state) => ({
            messagesByChat: state.messagesByChat,
            chatsByGame: state.chatsByGame,
          }),
          limit: 20, // Keep last 20 states
          equality: (pastState, currentState) =>
            pastState.messagesByChat === currentState.messagesByChat,
        }
      ),
      {
        // Persistence configuration
        name: 'meepleai-chat-store',
        // SSR-safe storage: Guard against localStorage access during server-side rendering
        storage: createJSONStorage(() =>
          typeof window !== 'undefined'
            ? localStorage
            : {
                getItem: () => null,
                setItem: () => {},
                removeItem: () => {},
              }
        ),
        // Only persist essential state
        partialize: (state) => ({
          selectedGameId: state.selectedGameId,
          selectedAgentId: state.selectedAgentId,
          sidebarCollapsed: state.sidebarCollapsed,
          chatsByGame: state.chatsByGame,
          activeChatIds: state.activeChatIds,
          messagesByChat: state.messagesByChat,
        }),
        // Version for migration if structure changes
        version: 1,
      }
    ),
    {
      // DevTools configuration
      name: 'ChatStore',
      enabled: process.env.NODE_ENV === 'development',
    }
  )
);

// ============================================================================
// Temporal (Undo/Redo) Store Access
// ============================================================================

/**
 * Access temporal store for undo/redo operations
 * Usage:
 *   const { undo, redo, clear } = useTemporalStore();
 *   const canUndo = useTemporalStore(state => state.pastStates.length > 0);
 */
export const useTemporalStore = temporal(useChatStore);
