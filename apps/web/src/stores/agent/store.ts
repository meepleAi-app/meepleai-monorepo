/**
 * Agent Store (Issue #3188)
 *
 * Zustand store for agent state management with middleware:
 * - Immer: Mutable state updates
 * - Persist: localStorage for config and conversations
 * - DevTools: Browser DevTools integration
 *
 * Store Architecture:
 * - Config: Per-game agent configurations (backend sync)
 * - Session: Active sessions (ephemeral, not persisted)
 * - Conversation: Chat history (backend + localStorage cache)
 *
 * Note: Sessions are intentionally NOT persisted (cleared on reload)
 */

import { create } from 'zustand';
import { devtools, persist, createJSONStorage } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import { createConfigSlice, ConfigSlice } from './slices/configSlice';
import { createSessionSlice, SessionSlice } from './slices/sessionSlice';
import { createConversationSlice, ConversationSlice } from './slices/conversationSlice';

// ============================================================================
// Combined Store Type
// ============================================================================

export type AgentStore = ConfigSlice & SessionSlice & ConversationSlice;

// ============================================================================
// Store Creation with Middleware Stack
// ============================================================================

/**
 * Middleware order (outer to inner):
 * 1. devtools: Browser DevTools integration
 * 2. persist: localStorage persistence
 * 3. immer: Mutable state updates
 */
export const useAgentStore = create<AgentStore>()(
  devtools(
    persist(
      immer((set, get, store) => ({
        // Combine all slices
        ...createConfigSlice(set, get, store),
        ...createSessionSlice(set, get, store),
        ...createConversationSlice(set, get, store),
      })),
      {
        // Persistence configuration
        name: 'meepleai-agent-store',
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
        // Only persist essential state (exclude ephemeral sessions)
        partialize: state => ({
          gameConfigs: state.gameConfigs,
          conversationCache: state.conversationCache,
          // Note: activeSessions intentionally excluded (ephemeral)
        }),
        // Version for migration if structure changes
        version: 1,
      }
    ),
    {
      // DevTools configuration
      name: 'AgentStore',
      enabled: process.env.NODE_ENV === 'development',
    }
  )
);

// Expose store globally for E2E tests
if (
  typeof window !== 'undefined' &&
  (process.env.NODE_ENV === 'development' || process.env.NODE_ENV === 'test')
) {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (window as any).useAgentStore = useAgentStore;
}
